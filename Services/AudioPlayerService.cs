using KyoneMusic.Models;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KyoneMusic.Services
{
    public record DispositivoAudio(int Indice, string Nome, string Driver);

    public class AudioPlayerService : IDisposable
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        public event Action? MusicaTerminou;

        public event Action? ErroReproducao;

        public event Action? PlaybackStarted;

        private const int FadeDurationMs = 250;
        public bool Terminou { get; private set; }
        private int _mixerHandle;
        private int _streamAtual;
        private int _fxEqHandle;
        private readonly ConcurrentDictionary<int, SyncProcedure> _syncDelegates = new();
        private readonly PeakEQParameters[] _bandasEq = new PeakEQParameters[10];
        private readonly SemaphoreSlim _trocaSemaphore = new SemaphoreSlim(1, 1);
        private int _disposed;
        private long _playToken = 0;
        private int _volumeAlvo = 100;
        private static readonly double[] _frequenciasPadrao = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
        private List<PresetEqualizador> _presets = new();
        public List<double> _frequenciasBanda { get; set; } = new();
        private readonly float[] _ganhosBase = new float[10];
        private float _preampDb;
        private int _dispositivoAtual = 1;

        public AudioPlayerService(ConfiguracaoService configuracaoService)
        {
            Debug.WriteLine($"[EQ] Processo é 64-bit? {Environment.Is64BitProcess}");

            string nativePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Native");

            if (Directory.Exists(nativePath))
                SetDllDirectory(nativePath);

            if (!Bass.Init())
                throw new InvalidOperationException($"Não foi possível inicializar o BASS. Erro: {Bass.LastError}");

            _ = BassFx.Version;

            _mixerHandle = BassMix.CreateMixerStream(48000, 2, BassFlags.MixerNonStop | BassFlags.Float);

            if (_mixerHandle == 0)
                throw new InvalidOperationException($"Não foi possível criar o mixer. Erro: {Bass.LastError}");

            _presets = CarregarPresets();

            if (_frequenciasBanda.Count < 10)
                _frequenciasBanda = _frequenciasPadrao.ToList();

            CriarEqualizador();

            var config = configuracaoService.CarregarConfiguracao();

            if (config.EqualizadorGanhosCustom != null) {
                for (int i = 0; i < config.EqualizadorGanhosCustom.Length && i < 10; i++) {
                    DefinirGanhoBanda((uint)i, config.EqualizadorGanhosCustom[i]);
                }
            }
        }

        // ───────────────────────────── Equalizador ─────────────────────────────

        public List<PresetEqualizador> CarregarPresets()
        {
            string caminhoPasta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Presets");

            string caminhoArquivo = Path.Combine(caminhoPasta, "presets.json");

            try {
                string textJson = File.ReadAllText(caminhoArquivo);

                var opcoes = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var presetsFile = JsonSerializer.Deserialize<EqPresetsFile>(textJson, opcoes);

                if (presetsFile == null)
                    return new List<PresetEqualizador>();

                List<double> frequencias = presetsFile.FrequenciesHz?
                    .Where(f => f > 0 && !double.IsNaN(f) && !double.IsInfinity(f))
                    .Take(10)
                    .ToList() ?? new List<double>();

                _frequenciasBanda = frequencias.Count == 10
                    ? frequencias
                    : _frequenciasPadrao.ToList();

                List<PresetEqualizador> presets = new();

                foreach ((EqPresetJson preset, int indice) in presetsFile.Presets.Select((preset, indice) => (preset, indice))) {
                    List<BandaEq> bandas = new();

                    int quantidade = Math.Min(
                        _frequenciasBanda.Count,
                        preset.ValuesDb?.Count ?? 0);

                    for (int i = 0; i < quantidade; i++) {
                        double ganho = preset.ValuesDb is not null && i < preset.ValuesDb.Count ? preset.ValuesDb[i] : 0;

                        if (double.IsNaN(ganho) || double.IsInfinity(ganho)) ganho = 0;

                        bandas.Add(new BandaEq
                        {
                            Frequencia = _frequenciasBanda[i],
                            GanhoDb = Math.Clamp(ganho, -12, 12)
                        });
                    }

                    presets.Add(new PresetEqualizador
                    {
                        Indice = (uint)indice,
                        Nome = string.IsNullOrWhiteSpace(preset.Name)
                            ? $"Preset {indice + 1}"
                            : preset.Name,
                        Bandas = bandas
                    });
                }

                return presets;
            }
            catch (JsonException ex) {
                Debug.WriteLine($"[EQ] Erro ao ler presets: {ex.Message}");
                return new List<PresetEqualizador>();
            }
            catch (Exception ex) {
                Debug.WriteLine($"[EQ] Erro ao carregar presets: {ex.Message}");
                return new List<PresetEqualizador>();
            }
        }

        private void CriarEqualizador()
        {
            if (_mixerHandle == 0) return;

            _fxEqHandle = Bass.ChannelSetFX(_mixerHandle, EffectType.PeakEQ, 0);

            if (_fxEqHandle == 0) {
                Debug.WriteLine($"[EQ] Não foi possível criar o equalizador: {Bass.LastError}");
                return;
            }

            for (int i = 0; i < 10; i++) {
                _bandasEq[i] = new PeakEQParameters
                {
                    lBand = i,
                    fCenter = (float)_frequenciasBanda[i],
                    fBandwidth = 1.2f,
                    fGain = 0f,
                    lChannel = FXChannelFlags.All
                };

                Bass.FXSetParameters(_fxEqHandle, _bandasEq[i]);
            }
        }

        public void DefinirGanhoBanda(uint banda, float valorDb)
        {
            if (banda >= _bandasEq.Length)
                return;

            valorDb = Math.Clamp(valorDb, -12f, 12f);

            _ganhosBase[banda] = valorDb;
            _bandasEq[banda].fGain = valorDb + _preampDb;

            if (_fxEqHandle != 0)
                Bass.FXSetParameters(_fxEqHandle, _bandasEq[banda]);
        }

        public void DefinirPreamp(float valorDb)
        {
            _preampDb = Math.Clamp(valorDb, -12f, 12f);

            for (int i = 0; i < _bandasEq.Length; i++) {
                _bandasEq[i].fGain = _ganhosBase[i] + _preampDb;

                if (_fxEqHandle != 0)
                    Bass.FXSetParameters(_fxEqHandle, _bandasEq[i]);
            }
        }

        public void DefinirEqualizador(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= _presets.Count) return;

            var preset = _presets[presetIndex];

            for (int i = 0; i < preset.Bandas.Count && i < 10; i++) {
                DefinirGanhoBanda((uint)i, (float)preset.Bandas[i].GanhoDb);
            }
        }

        // ───────────────────────────── Reproducao ─────────────────────────────

        public void TocarAudio(string path)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            long meuToken = Interlocked.Increment(ref _playToken);
            _ = TocarAudioAsync(path, meuToken);
        }

        public async Task TocarAudioAsync(string path, long meuToken)
        {
            Terminou = false;
            int novoStream = 0;

            await _trocaSemaphore.WaitAsync().ConfigureAwait(false);

            try {
                if (meuToken != _playToken)
                    return;

                if (!File.Exists(path)) {
                    Debug.WriteLine($"[BASS] Arquivo não encontrado: {path}");
                    ErroReproducao?.Invoke();
                    return;
                }

                int streamAntigo = _streamAtual;

                float volumeAlvoFloat = Math.Clamp(_volumeAlvo, 0, 100) / 100f;

                novoStream = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Float);

                if (novoStream == 0) {
                    Debug.WriteLine($"[BASS] CreateStream falhou: {Bass.LastError}");
                    ErroReproducao?.Invoke();
                    return;
                }

                if (!BassMix.MixerAddChannel(_mixerHandle, novoStream, BassFlags.MixerChanBuffer)) {
                    Debug.WriteLine($"[BASS] MixerAddChannel falhou: {Bass.LastError}");

                    Bass.StreamFree(novoStream);
                    novoStream = 0;

                    ErroReproducao?.Invoke();
                    return;
                }

                Bass.ChannelSetAttribute(novoStream, ChannelAttribute.Volume, 0f);

                Bass.ChannelSlideAttribute(novoStream, ChannelAttribute.Volume, volumeAlvoFloat, FadeDurationMs);

                if (streamAntigo != 0) {
                    Bass.ChannelSlideAttribute(streamAntigo, ChannelAttribute.Volume, 0f, FadeDurationMs);
                }

                SyncProcedure syncDelegate = (int h, int channel, int data, IntPtr user) =>
                {
                    if (meuToken != _playToken) return;
                    Terminou = true;
                    MusicaTerminou?.Invoke();
                };

                _syncDelegates[novoStream] = syncDelegate;
                Bass.ChannelSetSync(novoStream, SyncFlags.End, 0, syncDelegate);
                _streamAtual = novoStream;
                bool mixerTocando = Bass.ChannelPlay(_mixerHandle, false);

                if (!mixerTocando) {
                    Debug.WriteLine($"[BASS] ChannelPlay do mixer falhou: {Bass.LastError}");
                    BassMix.MixerRemoveChannel(novoStream);
                    _syncDelegates.TryRemove(novoStream, out _);
                    Bass.StreamFree(novoStream);
                    if (_streamAtual == novoStream) _streamAtual = streamAntigo;
                    ErroReproducao?.Invoke();
                    return;
                }

                PlaybackStarted?.Invoke();

                await Task.Delay(FadeDurationMs).ConfigureAwait(false);

                if (_mixerHandle == 0)
                    return;

                if (streamAntigo != 0 && streamAntigo != novoStream) {
                    BassMix.MixerRemoveChannel(streamAntigo);
                    _syncDelegates.TryRemove(streamAntigo, out _);
                    Bass.StreamFree(streamAntigo);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"[AudioPlayerService] Erro em TocarAudioAsync: {ex}");

                if (novoStream != 0 &&
                    _streamAtual != novoStream) {
                    try {
                        BassMix.MixerRemoveChannel(novoStream);
                    }
                    catch { }

                    try {
                        _syncDelegates.TryRemove(novoStream, out _);
                    }
                    catch { }

                    try {
                        Bass.StreamFree(novoStream);
                    }
                    catch { }
                }

                ErroReproducao?.Invoke();
            }
            finally {
                _trocaSemaphore.Release();
            }
        }

        public void PausarAudio()
        {
            try {
                if (_mixerHandle == 0) return;

                Bass.ChannelPause(_mixerHandle);
            }
            catch (Exception ex) {
                Debug.WriteLine($"[BASS] Erro ao pausar: {ex}");
            }
        }

        public void RetomarAudio()
        {
            try {
                if (_mixerHandle == 0 || _streamAtual == 0) return;

                Bass.ChannelPlay(_mixerHandle, false);
            }
            catch (Exception ex) {
                Debug.WriteLine($"[BASS] Erro ao retomar: {ex}");
            }
        }

        public void PararAudio()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            try {
                Interlocked.Increment(ref _playToken);

                if (_streamAtual != 0) {
                    BassMix.MixerRemoveChannel(_streamAtual);
                    _syncDelegates.TryRemove(_streamAtual, out _);
                    Bass.StreamFree(_streamAtual);
                    _streamAtual = 0;
                }

                if (_mixerHandle != 0)
                    Bass.ChannelStop(_mixerHandle);
            }
            catch (Exception ex) {
                Debug.WriteLine($"[BASS] Erro ao parar: {ex}");
            }
        }

        public void DefinirVolume(int vol)
        {
            _volumeAlvo = Math.Clamp(vol, 0, 100);

            if (_streamAtual != 0) {
                Bass.ChannelSetAttribute(_streamAtual, ChannelAttribute.Volume, _volumeAlvo / 100f);
            }
        }

        public int ObterVolume()
        {
            return _volumeAlvo;
        }

        public long ObterPosicaoAtual()
        {
            if (_streamAtual == 0) return 0;

            try {
                long bytes = Bass.ChannelGetPosition(_streamAtual);

                if (bytes < 0) return 0;

                double segundos = Bass.ChannelBytes2Seconds(_streamAtual, bytes);

                if (double.IsNaN(segundos) || double.IsInfinity(segundos) || segundos < 0) {
                    return 0;
                }

                return (long)( segundos * 1000 );
            }
            catch {
                return 0;
            }
        }

        public long ObterDuracaoTotal()
        {
            if (_streamAtual == 0) return 0;

            try {
                long bytes = Bass.ChannelGetLength(_streamAtual);

                if (bytes <= 0) return 0;

                double segundos = Bass.ChannelBytes2Seconds(_streamAtual, bytes);

                if (double.IsNaN(segundos) || double.IsInfinity(segundos) || segundos < 0) {
                    return 0;
                }

                return (long)( segundos * 1000 );
            }
            catch {
                return 0;
            }
        }

        public void DefinirPosicao(long milissegundos)
        {
            if (_streamAtual == 0) return;

            try {
                if (milissegundos < 0) milissegundos = 0;

                long duracao = ObterDuracaoTotal();

                if (duracao > 0 && milissegundos > duracao) {
                    milissegundos = duracao;
                }

                long bytes = Bass.ChannelSeconds2Bytes(_streamAtual, milissegundos / 1000.0);

                BassMix.ChannelSetPosition(_streamAtual, bytes);
            }
            catch (Exception ex) {
                Debug.WriteLine($"[BASS] Erro ao definir posição: {ex}");
            }
        }

        // ───────────────────────────── Dispositivo ─────────────────────────────

        public List<DispositivoAudio> ListarDispositivos()
        {
            List<DispositivoAudio> lista = new();
            int i = 1;
            while (Bass.GetDeviceInfo(i, out DeviceInfo info)) {
                Debug.WriteLine($"{i} | {info.Name} | {info.IsDefault} | {info.IsInitialized}");
                if (info.IsEnabled) {
                    lista.Add(new DispositivoAudio(i, info.Name, info.Driver));
                }
                i++;
            }
            return lista;
        }

        public bool DefinirDispositivo(DispositivoAudio d)
        {
            if (d.Indice == _dispositivoAtual) return true;
            if (!Bass.GetDeviceInfo(d.Indice, out DeviceInfo info) || !info.IsEnabled) return false;

            bool inicializouAgora = false;
            if (!info.IsInitialized) {
                // usa os mesmos parâmetros do teu Init inicial
                if (!Bass.Init(d.Indice, 48000, DeviceInitFlags.Default, IntPtr.Zero)) return false;
                inicializouAgora = true;
            }

            if (!Bass.ChannelSetDevice(_mixerHandle, d.Indice)) {
                if (inicializouAgora) { Bass.CurrentDevice = d.Indice; Bass.Free(); }
                Bass.CurrentDevice = _dispositivoAtual;
                return false;
            }

            _dispositivoAtual = d.Indice;
            Debug.WriteLine($"pos={Bass.ChannelGetPosition(_streamAtual)} erro={Bass.LastError}");
            return true;
        }

        public void AplicarDispositivoGuardado(string? nome, string? driver)
        {
            if (string.IsNullOrEmpty(nome)) return;
            var d = ListarDispositivos().FirstOrDefault(x => x.Nome == nome && x.Driver == ( driver ?? "" ));
            if (d != null) DefinirDispositivo(d);
        }

        // ───────────────────────────── Dispose ─────────────────────────────

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try {
                Interlocked.Increment(ref _playToken);

                _trocaSemaphore.Wait();

                try {
                    if (_streamAtual != 0) {
                        BassMix.MixerRemoveChannel(_streamAtual);
                        _syncDelegates.TryRemove(_streamAtual, out _);
                        Bass.StreamFree(_streamAtual);
                        _streamAtual = 0;
                    }

                    if (_mixerHandle != 0) {
                        Bass.ChannelStop(_mixerHandle);

                        if (_fxEqHandle != 0) {
                            Bass.ChannelRemoveFX(_mixerHandle, _fxEqHandle);
                            _fxEqHandle = 0;
                        }

                        Bass.StreamFree(_mixerHandle);
                        _mixerHandle = 0;
                    }

                    _syncDelegates.Clear();
                    Bass.Free();
                }
                finally {
                    _trocaSemaphore.Release();
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"[AudioPlayerService] Erro no Dispose: {ex}");
            }
            finally {
                _trocaSemaphore.Dispose();
            }
        }
    }
}
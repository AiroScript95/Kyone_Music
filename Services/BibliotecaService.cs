using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Helpers;
using KyoneMusic.ViewModels;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using FileIO = Microsoft.VisualBasic.FileIO;

namespace KyoneMusic.Services
{
    public class BibliotecaService : IDisposable
    {
        private readonly DatabaseService databaseService = App.ServiceProvider.GetRequiredService<DatabaseService>();
        private OrigemLetraStore _origemLetra = new();

        public event Action? BibliotecaCarregada;

        public event Action<List<string>>? PastasEmFalta;

        private readonly SemaphoreSlim _carregamentoSemaphore = new(1, 1);
        private bool _inicializado;

        public ObservableCollection<Music> Musicas { get; } = new();

        private HashSet<string> ExtensoesValidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3",
            ".flac",
            ".wav",
            ".ogg",
            ".m4a",
            ".aac",
            ".wma",
            ".amr",
            ".opus",
            ".midi",
            ".ape",
            ".aiff"
        };

        private static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private readonly ConfiguracaoService _configuracaoService = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
        private static readonly Regex RegexTimestamp = new(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
        private readonly OrdenacaoService _ordenacaoService = App.ServiceProvider.GetRequiredService<OrdenacaoService>();
        private readonly List<FileSystemWatcher> _watchers = new();
        private const string UserAgentKitsune = "Kitsune/0.1";
        private const string UserAgentBrowser = "Mozilla/5.0";
        private const double ToleranciaSegundos = 4;
        private const double ToleranciaSoDuracao = 2;

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _alteracoesPendentes = new(StringComparer.OrdinalIgnoreCase);

        // ───────────────────────────── Pastas ─────────────────────────────

        private void LerMusicasDaPasta(string caminhoPasta, Action<List<Music>> aoCompletarLote, IReadOnlyDictionary<string, (Music Musica, long DataModificacao)> cacheCompleto)
        {
            if (!Directory.Exists(caminhoPasta)) return;

            List<string> arquivos;

            try {
                arquivos = Directory.EnumerateFiles(caminhoPasta, "*.*", SearchOption.TopDirectoryOnly).Where(f => ExtensoesValidas.Contains(Path.GetExtension(f))).ToList();
            }
            catch (IOException ex) {
                Debug.WriteLine($"Dispositivo indisponível ao ler '{caminhoPasta}': {ex.Message}");
                return;
            }
            catch (UnauthorizedAccessException ex) {
                Debug.WriteLine($"Sem permissão ao ler '{caminhoPasta}': {ex.Message}");
                return;
            }

            var loteUi = new ConcurrentQueue<Music>();
            var paraSalvar = new ConcurrentBag<(Music Musica, long DataMod)>();
            int processados = 0;

            ParallelOptions opcoes = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 6)
            };

            Parallel.ForEach(arquivos, opcoes, item =>
            {
                try {
                    FileInfo fileInfo = new(item);
                    long dataModificacaoAtual = fileInfo.LastWriteTimeUtc.Ticks;

                    bool temCache = cacheCompleto.TryGetValue(item, out var entradaCache);
                    Music? musicaCache = temCache ? entradaCache.Musica : null;
                    long dataModificacaoCache = temCache ? entradaCache.DataModificacao : 0;
                    Music music;

                    if (musicaCache != null && dataModificacaoCache == dataModificacaoAtual) {
                        musicaCache.DataAdicionado = fileInfo.CreationTimeUtc;
                        musicaCache.TemLetraDisponivel = TemLrcComConteudo(item);
                        music = musicaCache;
                    }
                    else {
                        using (TagLib.File tagFile = TagLib.File.Create(item)) {
                            byte[] imagem = Array.Empty<byte>();
                            if (tagFile.Tag.Pictures.Length > 0) imagem = tagFile.Tag.Pictures[0].Data.Data;
                            string title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(item);
                            music = new Music
                            {
                                Titulo = TextoHelper.NormalizarTexto(title),
                                Artista = tagFile.Tag.FirstPerformer ?? "Unknown",
                                Album = tagFile.Tag.Album ?? "Unknown",
                                Genero = tagFile.Tag.FirstGenre ?? "Unknown",
                                Caminho = item,
                                Duracao = tagFile.Properties.Duration,
                                DuracaoArredondada = TimeSpan.FromSeconds((int)tagFile.Properties.Duration.TotalSeconds),
                                DataAdicionado = fileInfo.CreationTimeUtc,
                                VezesTocada = musicaCache?.VezesTocada ?? 0,
                                Gosto = musicaCache?.Gosto ?? false,
                                Image = imagem,
                                TemLetraDisponivel = TemLrcComConteudo(item)
                            };
                        }

                        paraSalvar.Add((music, dataModificacaoAtual));
                    }

                    loteUi.Enqueue(music);
                    int total = Interlocked.Increment(ref processados);

                    if (total % 8 == 0) {
                        List<Music> lote = new List<Music>();
                        while (lote.Count < 8 && loteUi.TryDequeue(out var m)) lote.Add(m);
                        if (lote.Count > 0) aoCompletarLote(lote);
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"ERRO: {ex.Message}");
                }
            });

            List<Music> restante = new List<Music>();
            while (loteUi.TryDequeue(out var m)) restante.Add(m);
            if (restante.Count > 0) aoCompletarLote(restante);

            if (!paraSalvar.IsEmpty) {
                databaseService.SalvarMusicasEmLote(paraSalvar.ToList());
            }
        }

        public Music? LerMusicaPorCaminho(string caminho)
        {
            try {
                FileInfo fileInfo = new(caminho);

                using (TagLib.File tagFile = TagLib.File.Create(caminho)) {
                    byte[] imagem = Array.Empty<byte>();

                    if (tagFile.Tag.Pictures.Length > 0) {
                        imagem = tagFile.Tag.Pictures[0].Data.Data;
                    }
                    string title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(caminho);
                    return new Music
                    {
                        Titulo = TextoHelper.NormalizarTexto(title),
                        Artista = tagFile.Tag.FirstPerformer ?? "Unknown",
                        Album = tagFile.Tag.Album ?? "Unknown",
                        Genero = tagFile.Tag.FirstGenre ?? "Unknown",
                        Caminho = caminho,
                        Duracao = tagFile.Properties.Duration,
                        DuracaoArredondada = TimeSpan.FromSeconds((int)tagFile.Properties.Duration.TotalSeconds),
                        DataAdicionado = fileInfo.CreationTimeUtc,
                        VezesTocada = databaseService.ObterVezesTocada(caminho),
                        Gosto = databaseService.ObterMusicaCache(caminho).Musica?.Gosto ?? false,
                        Image = imagem
                    };
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Erro ao ler o caminho: {ex.Message}");
                return null;
            }
        }

        public static bool TemLrcComConteudo(string caminhoMusica)
        {
            FileInfo lrc = new(Path.ChangeExtension(caminhoMusica, ".lrc"));
            return lrc.Exists && lrc.Length > 0;
        }

        // ───────────────────────────── Letras ─────────────────────────────

        public async Task<Letra?> ObterLetra(Music musica, CancellationToken token)
        {
            string pasta = Path.GetDirectoryName(musica.Caminho)!;
            string nomeBase = Path.GetFileNameWithoutExtension(musica.Caminho);
            string caminhoLrcKitsune = Path.Combine(pasta, nomeBase + ".lrc");

            // 1. .lrc local tem prioridade (edição manual do utilizador)
            if (File.Exists(caminhoLrcKitsune)) {
                try {
                    string conteudo = await File.ReadAllTextAsync(caminhoLrcKitsune, token);
                    if (!string.IsNullOrEmpty(conteudo)) {
                        musica.TemLetraDisponivel = true;

                        var letra = ParsearLetra(conteudo, FonteLetra.ArquivoLrc, caminhoLrcKitsune);
                        var (fonte, editada) = _origemLetra.Obter(caminhoLrcKitsune);
                        letra.Fonte = fonte;
                        letra.Editada = editada;
                        return letra;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) {
                    Debug.WriteLine($"ERRO ao ler .lrc: {ex.Message}");
                }
            }

            // 2. busca na API LRCLIB
            try {
                string? letraLrclib = await BuscarLetraLrclib(musica.Titulo, musica.Artista, musica.Album, musica.Duracao, token);
                if (!string.IsNullOrWhiteSpace(letraLrclib)) {
                    token.ThrowIfCancellationRequested();
                    await File.WriteAllTextAsync(caminhoLrcKitsune, letraLrclib);
                    musica.TemLetraDisponivel = true;
                    Debug.WriteLine($"[Letra] {musica.Titulo} - {musica.Artista}: LRCLIB");
                    _origemLetra.Registar(caminhoLrcKitsune, FonteLetra.Lrclib);   // e FonteLetra.Kugou no outro
                    return ParsearLetra(letraLrclib, FonteLetra.Lrclib, caminhoLrcKitsune);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) {
                Debug.WriteLine($"ERRO ao buscar letra na LRCLIB: {ex.Message}");
            }

            // 3. busca na API Kugou
            try {
                string? letraKugou = await BuscarLetraKugou(musica.Titulo, musica.Artista, musica.Duracao, token);
                if (!string.IsNullOrWhiteSpace(letraKugou)) {
                    token.ThrowIfCancellationRequested();
                    await File.WriteAllTextAsync(caminhoLrcKitsune, letraKugou);
                    musica.TemLetraDisponivel = true;
                    Debug.WriteLine($"[Letra] {musica.Titulo} - {musica.Artista}: Kugou");
                    _origemLetra.Registar(caminhoLrcKitsune, FonteLetra.Kugou);   // e FonteLetra.Kugou no outro
                    return ParsearLetra(letraKugou, FonteLetra.Kugou, caminhoLrcKitsune);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) {
                Debug.WriteLine($"ERRO ao buscar letra na Kugou: {ex.Message}");
            }

            // 2. busca na API lyrics.Ovh
            try {
                string? letraOvh = await BuscarLetraOvh(musica, token);
                if (!string.IsNullOrWhiteSpace(letraOvh)) {
                    token.ThrowIfCancellationRequested();
                    await File.WriteAllTextAsync(caminhoLrcKitsune, letraOvh);
                    musica.TemLetraDisponivel = true;
                    Debug.WriteLine($"[Letra] {musica.Titulo} - {musica.Artista}: lyricsOvh");
                    _origemLetra.Registar(caminhoLrcKitsune, FonteLetra.Lrclib);   // e FonteLetra.Kugou no outro
                    return ParsearLetra(letraOvh, FonteLetra.Lrclib, caminhoLrcKitsune);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) {
                Debug.WriteLine($"ERRO ao buscar letra na lyricsOvh: {ex.Message}");
            }
            // 3. letra embutida na tag do ficheiro de áudio
            Letra? letraEmbutida = await Task.Run(() =>
            {
                try {
                    using (TagLib.File tagFile = TagLib.File.Create(musica.Caminho)) {
                        string? letra = tagFile.Tag.Lyrics;
                        if (!string.IsNullOrWhiteSpace(letra)) {
                            musica.TemLetraDisponivel = true;
                            return ParsearLetra(letra, FonteLetra.Embutida, musica.Caminho);
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine($"ERRO ao ler letra embutida: {ex.Message}");
                }
                return null;
            });

            if (letraEmbutida != null) return letraEmbutida;

            return null;
        }

        private Letra ParsearLetra(string conteudo, FonteLetra fonte, string? caminhoOrigem)
        {
            Letra letra = new Letra()
            {
                ConteudoBruto = conteudo,
                Fonte = fonte,
                CaminhoOrigem = caminhoOrigem
            };

            string conteudoNormalizado = conteudo.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] linhas = conteudoNormalizado.Split('\n');

            foreach (string linhaBruta in linhas) {
                string linha = linhaBruta.Trim();
                if (linha.Length == 0) continue;

                var extraidas = ExtrairLinhasComTempo(linha);
                if (extraidas.Count > 0)
                    letra.Linhas.AddRange(extraidas);
            }

            if (letra.Linhas.Count > 0) {
                letra.TemTimestampsReais = true;
            }
            else {
                // Fallback: texto plano, sem timestamps
                foreach (string linhaBruta in linhas) {
                    string texto = linhaBruta.Trim();
                    if (texto.Length > 0) {
                        letra.Linhas.Add(new LinhaLetra { Tempo = TimeSpan.Zero, Texto = texto });
                    }
                }
                letra.TemTimestampsReais = false;
            }

            letra.Linhas = letra.Linhas.OrderBy(l => l.Tempo).ToList();
            return letra;
        }

        private static async Task<string?> LerJsonAsync(string url, string userAgent, CancellationToken token)
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, url);
            pedido.Headers.UserAgent.ParseAdd(userAgent);

            using HttpResponseMessage resposta = await _httpClient.SendAsync(pedido, token);
            if (!resposta.IsSuccessStatusCode) return null;

            return await resposta.Content.ReadAsStringAsync(token);
        }

        private static bool Desconhecido(string valor) =>
            string.IsNullOrWhiteSpace(valor) || valor.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase);

        private static string Normalizar(string texto)
        {
            string decomposto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in decomposto) {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        private List<LinhaLetra> ExtrairLinhasComTempo(string linha)
        {
            var resultado = new List<LinhaLetra>();
            var matches = RegexTimestamp.Matches(linha);
            if (matches.Count == 0) return resultado;

            // texto é tudo depois do ÚLTIMO colchete de timestamp
            var ultimoMatch = matches[^1];
            string texto = linha.Substring(ultimoMatch.Index + ultimoMatch.Length).Trim();

            foreach (Match m in matches) {
                int minutos = int.Parse(m.Groups[1].Value);
                int segundos = int.Parse(m.Groups[2].Value);
                int centesimos = 0;
                if (m.Groups[3].Success) {
                    string frac = m.Groups[3].Value.PadRight(3, '0').Substring(0, 3);
                    centesimos = int.Parse(frac);
                }
                TimeSpan tempo = new(0, 0, minutos, segundos, centesimos);
                resultado.Add(new LinhaLetra { Tempo = tempo, Texto = texto });
            }
            return resultado;
        }

        // ───────────────────────────── LRCLIB ─────────────────────────────

        private async Task<string?> BuscarLetraLrclib(string titulo, string artista, string album, TimeSpan duracao, CancellationToken token)
        {
            try {
                if (!Desconhecido(artista)) {
                    string? letra = await LrclibGet(titulo, artista, album, duracao, token);
                    if (letra != null) return letra;
                }
                return await LrclibSearch(titulo, artista, duracao, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (TaskCanceledException) {
                Debug.WriteLine("LRCLIB: tempo limite excedido");
            }
            catch (HttpRequestException ex) {
                Debug.WriteLine($"LRCLIB: erro de rede - {ex.Message}");
            }
            catch (JsonException ex) {
                Debug.WriteLine($"LRCLIB: JSON inválido - {ex.Message}");
            }

            return null;
        }

        private static string? ExtrairLetraLrclib(JsonElement el)
        {
            if (el.TryGetProperty("syncedLyrics", out JsonElement synced) &&
                synced.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(synced.GetString())) {
                return synced.GetString();
            }

            if (el.TryGetProperty("plainLyrics", out JsonElement plain) &&
                plain.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(plain.GetString())) {
                return plain.GetString();
            }

            return null;
        }

        private async Task<string?> LrclibGet(string titulo, string artista, string album, TimeSpan duracao, CancellationToken token)
        {
            string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(titulo)}" +
                         $"&artist_name={Uri.EscapeDataString(artista)}" +
                         $"&duration={(int)Math.Round(duracao.TotalSeconds)}";

            if (!Desconhecido(album))
                url += $"&album_name={Uri.EscapeDataString(album)}";

            string? json = await LerJsonAsync(url, UserAgentKitsune, token);
            if (json == null) return null;

            using JsonDocument doc = JsonDocument.Parse(json);
            return ExtrairLetraLrclib(doc.RootElement);
        }

        private async Task<string?> LrclibSearch(string titulo, string artista, TimeSpan duracao, CancellationToken token)
        {
            string url = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(titulo)}";

            if (!Desconhecido(artista)) { url += $"&artist_name={Uri.EscapeDataString(artista)}"; }

            string? json = await LerJsonAsync(url, UserAgentKitsune, token);

            if (json == null) return null;

            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            string? melhorLetra = null;
            bool melhorSincronizada = false;
            double melhorDif = double.MaxValue;

            foreach (JsonElement item in doc.RootElement.EnumerateArray()) {
                if (!item.TryGetProperty("duration", out JsonElement durEl) || durEl.ValueKind != JsonValueKind.Number) continue;

                double dif = Math.Abs(durEl.GetDouble() - duracao.TotalSeconds);
                if (dif > ToleranciaSegundos) continue;

                string? letra = ExtrairLetraLrclib(item);
                if (letra == null) continue;

                bool sincronizada = item.TryGetProperty("syncedLyrics", out JsonElement s) &&
                                    s.ValueKind == JsonValueKind.String &&
                                    !string.IsNullOrWhiteSpace(s.GetString());

                bool melhor = ( sincronizada && !melhorSincronizada ) ||
                              ( sincronizada == melhorSincronizada && dif < melhorDif );

                if (melhor) {
                    melhorLetra = letra;
                    melhorSincronizada = sincronizada;
                    melhorDif = dif;
                }
            }

            return melhorLetra;
        }

        // ───────────────────────────── Kugou ─────────────────────────────

        private async Task<string?> BuscarLetraKugou(string titulo, string artista, TimeSpan duracao, CancellationToken token)
        {
            bool artistaConhecido = !Desconhecido(artista);
            string termo = Uri.EscapeDataString(artistaConhecido ? $"{artista} - {titulo}" : titulo);
            int duracaoMs = (int)duracao.TotalMilliseconds;
            string urlBusca = $"https://lyrics.kugou.com/search?ver=1&man=yes&client=pc&keyword={termo}&duration={duracaoMs}&hash=";

            try {
                string? jsonBusca = await LerJsonAsync(urlBusca, UserAgentBrowser, token);
                if (jsonBusca == null) return null;

                (string Id, string AccessKey)? candidato = EscolherCandidatoKugou(jsonBusca, titulo, artista, duracao);
                if (candidato == null) {
                    Debug.WriteLine($"[Letra] Kugou sem candidato: {titulo} - {artista}");
                    return null;
                }

                string urlLetra = $"https://lyrics.kugou.com/download?ver=1&client=pc" +
                                  $"&id={Uri.EscapeDataString(candidato.Value.Id)}" +
                                  $"&accesskey={Uri.EscapeDataString(candidato.Value.AccessKey)}" +
                                  $"&fmt=lrc&charset=utf8";

                string? jsonLetra = await LerJsonAsync(urlLetra, UserAgentBrowser, token);
                if (jsonLetra == null) return null;

                using JsonDocument doc = JsonDocument.Parse(jsonLetra);
                if (doc.RootElement.TryGetProperty("content", out JsonElement contentEl) &&
                    contentEl.ValueKind == JsonValueKind.String) {
                    byte[] bytes = Convert.FromBase64String(contentEl.GetString()!);
                    return Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (TaskCanceledException) { Debug.WriteLine("Kugou: tempo limite excedido"); }
            catch (HttpRequestException ex) { Debug.WriteLine($"Kugou: erro de rede - {ex.Message}"); }
            catch (JsonException ex) { Debug.WriteLine($"Kugou: JSON inválido - {ex.Message}"); }
            catch (FormatException ex) { Debug.WriteLine($"Kugou: base64 inválido - {ex.Message}"); }

            return null;
        }

        private static (string Id, string AccessKey)? EscolherCandidatoKugou(string json, string titulo, string artista, TimeSpan duracao)
        {
            string tituloNorm = Normalizar(titulo);
            string artistaNorm = Normalizar(artista);
            bool artistaConhecido = !Desconhecido(artista);

            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("candidates", out JsonElement candEl) ||
                candEl.ValueKind != JsonValueKind.Array) return null;

            (string Id, string AccessKey)? melhor = null;
            int melhorPontos = -1;
            double melhorDif = double.MaxValue;

            foreach (JsonElement c in candEl.EnumerateArray()) {
                string? id = LerTexto(c, "id");
                string? chave = LerTexto(c, "accesskey");
                if (id == null || chave == null) continue;

                // duração em ms
                if (!c.TryGetProperty("duration", out JsonElement durEl) ||
                    durEl.ValueKind != JsonValueKind.Number) continue;

                double dif = Math.Abs(durEl.GetDouble() / 1000.0 - duracao.TotalSeconds);
                if (dif > ToleranciaSegundos) continue;

                bool tituloOk = Confere(LerTexto(c, "song"), tituloNorm);
                bool artistaOk = artistaConhecido && Confere(LerTexto(c, "singer"), artistaNorm);
                int pontos = ( tituloOk ? 1 : 0 ) + ( artistaOk ? 1 : 0 );

                // nada bate (ex.: kanji vs romaji): só aceita com duração quase idêntica
                if (pontos == 0 && dif > ToleranciaSoDuracao) continue;

                if (pontos > melhorPontos || ( pontos == melhorPontos && dif < melhorDif )) {
                    melhor = (id, chave);
                    melhorPontos = pontos;
                    melhorDif = dif;
                }
            }

            return melhor;
        }

        private static string? LerTexto(JsonElement el, string nome)
        {
            if (!el.TryGetProperty(nome, out JsonElement v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null
            };
        }

        private static bool Confere(string? texto, string normEsperado)
        {
            if (texto == null || normEsperado.Length == 0) return false;
            string n = Normalizar(texto);
            return n.Length > 0 && ( n.Contains(normEsperado) || normEsperado.Contains(n) );
        }

        // ───────────────────────────── lyricsOvh ─────────────────────────────

        private async Task<string?> BuscarLetraOvh(Music musica, CancellationToken token)
        {
            if (musica.Artista.StartsWith("Unknown") || string.IsNullOrWhiteSpace(musica.Titulo))
                return null;

            var url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(musica.Artista)}/{Uri.EscapeDataString(musica.Titulo)}";
            try {
                using var resp = await _httpClient.GetAsync(url, token);
                if (!resp.IsSuccessStatusCode) return null; // 404 = sem letra

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(token));
                if (!doc.RootElement.TryGetProperty("lyrics", out var l)) return null;

                var texto = l.GetString()?.Trim();
                return string.IsNullOrEmpty(texto) ? null : texto;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { return null; }
        }

        // ───────────────────────────── Biblioteca ─────────────────────────────

        public async Task CarregarBibliotecaAsync(IEnumerable<string> pastas, string criterio, bool descendente)
        {
            var pastasValidas = pastas.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            var emFalta = pastasValidas.Where(p => !Directory.Exists(p)).ToList();
            if (emFalta.Count > 0) PastasEmFalta?.Invoke(emFalta);

            Dictionary<string, (Music Musica, long DataModificacao)> cacheCompleto =
                pastasValidas.Any(Directory.Exists)
                    ? databaseService.ObterTodoCache()
                    : new Dictionary<string, (Music Musica, long DataModificacao)>();

            List<Music> todasMusicas = await Task.Run(() =>
            {
                var resultado = new List<Music>();
                object lockMusicas = new();

                foreach (string pasta in pastasValidas) {
                    LerMusicasDaPasta(
                        pasta,
                        lote =>
                        {
                            lock (lockMusicas) {
                                resultado.AddRange(lote);
                            }
                        },
                        cacheCompleto);
                }

                return resultado;
            });

            List<Music> ordenadas = _ordenacaoService.Ordenar(todasMusicas, criterio, descendente);

            Application.Current.Dispatcher.Invoke(() =>
            {
                AtualizarMusicas(ordenadas);
            });
            WeakReferenceMessenger.Default.Send(new BibliotecaAlteradaMessage());
            BibliotecaCarregada?.Invoke();
        }

        private void AtualizarMusicas(List<Music> novasOrdenadas)
        {
            var existentes = new Dictionary<string, Music>(
                Musicas.Count,
                StringComparer.Ordinal);

            foreach (Music musica in Musicas) {
                if (!existentes.ContainsKey(musica.Caminho))
                    existentes.Add(musica.Caminho, musica);
            }

            bool mesmaOrdem = Musicas.Count == novasOrdenadas.Count;

            for (int i = 0; mesmaOrdem && i < novasOrdenadas.Count; i++) {
                Music musicaDesejada = existentes.TryGetValue(
                    novasOrdenadas[i].Caminho,
                    out Music? musicaExistente)
                        ? musicaExistente
                        : novasOrdenadas[i];

                if (!ReferenceEquals(Musicas[i], musicaDesejada))
                    mesmaOrdem = false;
            }

            if (mesmaOrdem) return;

            Musicas.Clear();

            foreach (Music nova in novasOrdenadas) {
                Music musica = existentes.TryGetValue(
                    nova.Caminho,
                    out Music? musicaExistente)
                        ? musicaExistente
                        : nova;

                Musicas.Add(musica);
            }
        }

        public void IniciarMonitorizacao(IEnumerable<string> pastas)
        {
            PararMonitorizacao();

            foreach (string pasta in pastas.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))) {
                var watcher = new FileSystemWatcher(pasta)
                {
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };

                watcher.Created += Watcher_Created;
                watcher.Changed += Watcher_Changed;
                watcher.Deleted += Watcher_Deleted;
                watcher.Renamed += Watcher_Renamed;
                watcher.Error += Watcher_Error;

                _watchers.Add(watcher);
            }
        }

        public void PararMonitorizacao()
        {
            foreach (FileSystemWatcher watcher in _watchers) {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();

            foreach (CancellationTokenSource cts in _alteracoesPendentes.Values)
                cts.Cancel();

            _alteracoesPendentes.Clear();
        }

        public void ReordenarMusicas()
        {
            Configuracao config = _configuracaoService.CarregarConfiguracao();
            List<Music> ordenadas = _ordenacaoService.Ordenar(Musicas.ToList(), config.OrdemMusicList, config.OrdemDescendente);
            AtualizarMusicas(ordenadas);
        }

        private async void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!ExtensoesValidas.Contains(Path.GetExtension(e.FullPath))) return;

            bool disponivel = await AguardarArquivoLivreAsync(e.FullPath);
            if (!disponivel) return;

            Music? musica = LerMusicaPorCaminho(e.FullPath);
            if (musica == null) return;
            databaseService.SalvarMusicaCompleta(musica, new FileInfo(e.FullPath).LastWriteTimeUtc.Ticks);

            Application.Current.Dispatcher.Invoke(() =>
            {
                bool jaExiste = Musicas.Any(m => string.Equals(m.Caminho, musica.Caminho, StringComparison.OrdinalIgnoreCase));
                if (!jaExiste) {
                    Musicas.Add(musica);
                    ReordenarMusicas();
                }
            });
        }

        private async void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Music? musicaAntiga = Musicas.FirstOrDefault(m => string.Equals(m.Caminho, e.OldFullPath, StringComparison.OrdinalIgnoreCase));
                if (musicaAntiga != null)
                    Musicas.Remove(musicaAntiga);
            });

            string extensao = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (!ExtensoesValidas.Select(ext => ext.ToLowerInvariant()).Contains(extensao)) return;

            bool disponivel = await AguardarArquivoLivreAsync(e.FullPath);
            if (!disponivel) return;

            Music? musicaNova = LerMusicaPorCaminho(e.FullPath);
            if (musicaNova == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                bool jaExiste = Musicas.Any(m => string.Equals(m.Caminho, musicaNova.Caminho, StringComparison.OrdinalIgnoreCase));
                if (!jaExiste) {
                    Musicas.Add(musicaNova);
                    databaseService.SalvarMusicaCompleta(musicaNova, new FileInfo(e.FullPath).LastWriteTimeUtc.Ticks);
                    ReordenarMusicas();
                }
            });
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Music? musica = Musicas.FirstOrDefault(m => string.Equals(m.Caminho, e.FullPath, StringComparison.OrdinalIgnoreCase));
                if (musica != null) Musicas.Remove(musica);
                ReordenarMusicas();
            });
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            if (sender is FileSystemWatcher watcher) {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _watchers.Remove(watcher);
            }
        }

        private async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!ExtensoesValidas.Contains(Path.GetExtension(e.FullPath)))
                return;

            CancellationTokenSource novoCts = new();
            CancellationTokenSource? anterior =
                _alteracoesPendentes.AddOrUpdate(
                    e.FullPath,
                    novoCts,
                    (_, existente) =>
                    {
                        existente.Cancel();
                        existente.Dispose();
                        return novoCts;
                    });

            try {
                await Task.Delay(500, novoCts.Token);

                if (!await AguardarArquivoLivreAsync(e.FullPath))
                    return;

                Music? musicaAtualizada = LerMusicaPorCaminho(e.FullPath);

                if (musicaAtualizada == null)
                    return;

                long dataModificacao =
                    new FileInfo(e.FullPath).LastWriteTimeUtc.Ticks;

                databaseService.SalvarMusicaCompleta(
                    musicaAtualizada,
                    dataModificacao);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Music? musicaExistente = Musicas.FirstOrDefault(m =>
                        string.Equals(
                            m.Caminho,
                            musicaAtualizada.Caminho,
                            StringComparison.OrdinalIgnoreCase));

                    if (musicaExistente == null) {
                        Musicas.Add(musicaAtualizada);
                    }
                    else {
                        musicaExistente.Titulo = musicaAtualizada.Titulo;
                        musicaExistente.Artista = musicaAtualizada.Artista;
                        musicaExistente.Album = musicaAtualizada.Album;
                        musicaExistente.Genero = musicaAtualizada.Genero;
                        musicaExistente.Duracao = musicaAtualizada.Duracao;
                        musicaExistente.DuracaoArredondada =
                            musicaAtualizada.DuracaoArredondada;
                        musicaExistente.Image = musicaAtualizada.Image;
                        musicaExistente.TemLetraDisponivel =
                            musicaAtualizada.TemLetraDisponivel;
                    }

                    ReordenarMusicas();
                });
            }
            catch (OperationCanceledException) {
            }
            catch (Exception ex) {
                Debug.WriteLine(
                    $"Erro ao atualizar ficheiro '{e.FullPath}': {ex}");
            }
            finally {
                if (_alteracoesPendentes.TryGetValue(
                        e.FullPath,
                        out CancellationTokenSource? atual) &&
                    ReferenceEquals(atual, novoCts)) {
                    _alteracoesPendentes.TryRemove(
                        e.FullPath,
                        out CancellationTokenSource? removido);

                    removido?.Dispose();
                }
            }
        }

        private async Task<bool> AguardarArquivoLivreAsync(string caminho)
        {
            for (int tentativa = 0; tentativa < 10; tentativa++) {
                try {
                    using FileStream stream = File.Open(caminho, FileMode.Open, FileAccess.Read, FileShare.None);
                    return true;
                }
                catch (IOException) {
                    await Task.Delay(300);
                }
            }
            return false;
        }

        public async Task GarantirBibliotecaCarregadaAsync(IEnumerable<string> pastas, bool forcar = false)
        {
            if (!await _carregamentoSemaphore.WaitAsync(0))
                return;

            try {
                if (_inicializado && !forcar)
                    return;

                Configuracao config = _configuracaoService.CarregarConfiguracao();

                await CarregarBibliotecaAsync(
                    pastas,
                    config.OrdemMusicList,
                    config.OrdemDescendente);

                IniciarMonitorizacao(pastas);
                _inicializado = true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Erro ao carregar a biblioteca: {ex}");
                _inicializado = false;
            }
            finally {
                _carregamentoSemaphore.Release();
            }
        }

        public void EliminarMusica(Music musica)
        {
            MoverParaLixeira(musica.Caminho);
        }

        public void MoverParaLixeira(string caminho)
        {
            if (File.Exists(caminho)) {
                FileIO.FileSystem.DeleteFile(caminho, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin);
            }
        }

        public void SalvarEdicaoMusica(Music musica, string novoTitulo, string novoArtista, string novoAlbum, string novoGenero)
        {
            var tagFile = TagLib.File.Create(musica.Caminho);
            tagFile.Tag.Title = novoTitulo;
            tagFile.Tag.Performers = new[] { novoArtista };
            tagFile.Tag.Album = novoAlbum;
            tagFile.Tag.Genres = new[] { novoGenero };
            tagFile.Save();

            databaseService.AtualizarMetadados(musica.Caminho, novoTitulo, novoArtista, novoAlbum, novoGenero);

            musica.Titulo = novoTitulo;
            musica.Artista = novoArtista;
            musica.Album = novoAlbum;
            musica.Genero = novoGenero;

            WeakReferenceMessenger.Default.Send(new BibliotecaAlteradaMessage());
            ReordenarMusicas();
        }

        public void SalvarEdicaoPlyalist(Playlist playlist, string nomeNovo)
        {
            databaseService.AtualizarPlaylist(playlist.Id, nomeNovo);
            playlist.Name = nomeNovo;
        }

        public async Task SalvarLetra(string caminhoLrc, string texto)
        {
            // texto vazio e ficheiro inexistente: não cria um .lrc vazio
            if (string.IsNullOrWhiteSpace(texto) && !File.Exists(caminhoLrc)) return;

            string tmp = caminhoLrc + ".tmp";

            try {
                await File.WriteAllTextAsync(tmp, texto);

                if (File.Exists(caminhoLrc))
                    File.Replace(tmp, caminhoLrc, null);
                else
                    File.Move(tmp, caminhoLrc);
            }
            catch {
                if (File.Exists(tmp)) File.Delete(tmp);
                throw;
            }
        }

        public void Dispose()
        {
            PararMonitorizacao();
            _carregamentoSemaphore.Dispose();
        }
    }
}
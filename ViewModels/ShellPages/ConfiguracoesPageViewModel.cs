using CommunityToolkit.Mvvm.Input;
using KyoneMusic.Models;
using KyoneMusic.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Windows.Devices.Radios;

namespace KyoneMusic.ViewModels.ShellPages
{
    public partial class ConfiguracoesPageViewModel : INotifyPropertyChanged
    {
        public string PastaPadrao { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        public ObservableCollection<string> PastasAdicionais { get; set; } = new ObservableCollection<string>();
        protected ShellViewModel Shell;
        public PlayerViewModel Player { get; set; }
        private readonly BibliotecaService bibliotecaService;
        private readonly AudioPlayerService audio;
        public string Versao => InfoApp.Versao;
        public string Copyright => InfoApp.Copyright;
        private Configuracao config = new();

        private bool _temImage;

        public bool TemImage {
            get => _temImage;
            set {
                _temImage = value;
                OnPropertyChanged();
            }
        }

        private string _caminhoBackground = string.Empty;

        public string ImagemBackground {
            get => _caminhoBackground;
            set {
                _caminhoBackground = value;
                OnPropertyChanged();
            }
        }

        private ConfiguracaoService configuracaoService;

        private string _temaSelecionado = string.Empty;

        public string TemaSelecionado {
            get => _temaSelecionado;
            set {
                _temaSelecionado = value;
                OnPropertyChanged();

                if (!string.IsNullOrEmpty(value)) {
                    configuracaoService.AtualizarTema(value);
                    ( (App)Application.Current ).AplicarTema(value);
                    Shell.AplicarMascote(value);
                }
            }
        }

        private bool _aCarregar;
        private DispositivoAudio? _dispositivoSelecionado;
        public List<DispositivoAudio> DispositivoAudios { get; private set; } = new();

        public DispositivoAudio? DispositivoSelecionado {
            get => _dispositivoSelecionado;
            set {
                if (_dispositivoSelecionado == value) return;
                var anterior = _dispositivoSelecionado;
                _dispositivoSelecionado = value;
                OnPropertyChanged();

                if (_aCarregar || value == null) return;

                if (audio.DefinirDispositivo(value)) {
                    configuracaoService.AtualizarConfiguracao(config =>
                    {
                        config.DispositivoNome = value.Nome;
                        config.DispositivoDriver = value.Driver;
                    });
                }
                else {
                    _aCarregar = true;
                    _dispositivoSelecionado = anterior;
                    OnPropertyChanged();
                    _aCarregar = false;
                }
            }
        }

        public record IdiomaItem(string Codigo, string Nome);

        public List<IdiomaItem> IdiomasDisponiveis { get; } = new List<IdiomaItem>()
        {
            new("pt", "Português (Portugal)"),
            new("pt-br", "Português (Brasil)"),
            new("en", "English"),
            new("ja", "日本語"),
            new("es", "Español")
        };

        private string _idiomaSelecionado = string.Empty;

        public string IdiomaSelecionado {
            get => _idiomaSelecionado;
            set {
                _idiomaSelecionado = value;
                OnPropertyChanged();
                if (!string.IsNullOrEmpty(value)) {
                    configuracaoService.AtualizarIdiona(value);
                    ( (App)Application.Current ).AplicarIdioma(value);
                }
            }
        }

        public ConfiguracoesPageViewModel(ShellViewModel shell)
        {
            Shell = shell;
            Player = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            bibliotecaService = App.ServiceProvider.GetRequiredService<BibliotecaService>();
            audio = App.ServiceProvider.GetRequiredService<AudioPlayerService>();
            configuracaoService = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
            config = configuracaoService.CarregarConfiguracao();
            _temaSelecionado = string.IsNullOrEmpty(config.Theme) ? "Asa" : config.Theme;
            _idiomaSelecionado = string.IsNullOrEmpty(config.Idioma) ? App.DetectarIdiomaSistema() : config.Idioma;
            foreach (string pasta in config.RotaMusica) {
                if (pasta != PastaPadrao) {
                    PastasAdicionais.Add(pasta);
                }
            }
            ImagemBackground = config.Background;
            TemImage = !string.IsNullOrEmpty(config.Background);
            _aCarregar = true;
            CarregarDispositivos();
        }

        private void CarregarDispositivos()
        {
            _aCarregar = true;
            DispositivoAudios = audio.ListarDispositivos();
            DispositivoSelecionado =
                DispositivoAudios.FirstOrDefault(d => d.Nome == config.DispositivoNome && d.Driver == ( config.DispositivoDriver ?? "" ))
                ?? DispositivoAudios.FirstOrDefault(d => d.Indice == 1);
            _aCarregar = false;
        }

        [RelayCommand]
        private async Task AdicionarPasta()
        {
            OpenFolderDialog dialog = new OpenFolderDialog();
            bool? resultado = dialog.ShowDialog();

            if (resultado == true) {
                string caminhoEscolhido = dialog.FolderName;
                bool sucesso = configuracaoService.AdicionarPastaMusica(caminhoEscolhido);

                if (sucesso) {
                    PastasAdicionais.Add(caminhoEscolhido);
                    Configuracao config = configuracaoService.CarregarConfiguracao();
                    await bibliotecaService.GarantirBibliotecaCarregadaAsync(config.RotaMusica, forcar: true);
                }
            }
        }

        [RelayCommand]
        private async Task RemoverPasta(string caminho)
        {
            configuracaoService.RemoverPastaMusica(caminho);
            PastasAdicionais.Remove(caminho);
            Configuracao config = configuracaoService.CarregarConfiguracao();
            await bibliotecaService.GarantirBibliotecaCarregadaAsync(config.RotaMusica, forcar: true);
        }

        [RelayCommand]
        private void IrParaTema()
        {
            Shell.CurrentPage = new ConfigThemePageViewModel(Shell, () => new ConfiguracoesPageViewModel(Shell));
        }

        [RelayCommand]
        private void AbrirLicencas()
        {
            var caminho = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
            if (!File.Exists(caminho)) return;

            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
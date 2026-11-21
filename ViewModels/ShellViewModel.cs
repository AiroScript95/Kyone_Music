using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Helpers;
using KyoneMusic.Services;
using KyoneMusic.ViewModels.ShellPages;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace KyoneMusic.ViewModels
{
    public partial class ShellViewModel : INotifyPropertyChanged
    {
        /* ESTADOS - (Campos/Propriedades) */
        protected DatabaseService _databaseService;
        public PlayerViewModel Player { get; set; }
        protected ConfiguracaoService configuracaoService;
        protected AudioPlayerService audio;
        private PlaylistsPageViewModel playlistPageViewModel;
        private Dictionary<string, Music> _porCaminho;
        private string _caminhoBackground = string.Empty;

        public string CaminhoBackground {
            get => _caminhoBackground;
            set {
                _caminhoBackground = value;
                OnPropertyChanged();
            }
        }

        private string _tema = string.Empty;

        public string Tema {
            get => _tema;
            set {
                _tema = value;
                OnPropertyChanged();
            }
        }

        private object? _currentPage;

        public object? CurrentPage {
            get => _currentPage;
            set {
                _currentPage = value;
                OnPropertyChanged();
            }
        }

        private string _border = string.Empty;

        public string Border {
            get => _border;
            set { _border = value; OnPropertyChanged(); }
        }

        private string _formatoLista = "List";

        public string FormatoLista {
            get => _formatoLista;
            set { _formatoLista = value; OnPropertyChanged(); }
        }

        private string _page = "MusicList";

        public string Page {
            get => _page;
            set {
                _page = value;
                OnPropertyChanged();
            }
        }

        public bool _mostrarImage = false;

        public bool MostrarImage {
            get => _mostrarImage;
            set {
                _mostrarImage = value;
                OnPropertyChanged();
            }
        }

        protected BibliotecaService bibliotecaService;

        private bool _estaCarregandoMusicas;

        public bool EstaCarregandoMusicas {
            get => _estaCarregandoMusicas;
            set { _estaCarregandoMusicas = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> PastasEmFalta { get; } = new();

        private bool _mostrarAvisoPastas;

        public bool MostrarAvisoPastas {
            get => _mostrarAvisoPastas;
            set { _mostrarAvisoPastas = value; OnPropertyChanged(); }
        }

        private bool _mostrarModalCriarPlaylist = false;

        public bool MostrarModalCriarPlaylist {
            get => _mostrarModalCriarPlaylist;
            set {
                _mostrarModalCriarPlaylist = value;
                OnPropertyChanged();
            }
        }

        private string _novaPlaylist = string.Empty;

        public string NovaPlaylist {
            get => _novaPlaylist;
            set {
                _novaPlaylist = value;
                OnPropertyChanged();
            }
        }

        private string _playlistParaExcluir = string.Empty;

        public string PlayListParaExcluir {
            get => _playlistParaExcluir;
            set {
                _playlistParaExcluir = value;
                OnPropertyChanged();
            }
        }

        private bool mostrarConfirmacaoExclusaoPlaylist;

        public bool MostrarConfirmacaoExclusaoPlaylist {
            get => mostrarConfirmacaoExclusaoPlaylist;
            set {
                mostrarConfirmacaoExclusaoPlaylist = value;
                OnPropertyChanged();
            }
        }

        public Playlist? Playlist;

        public ShellViewModel()
        {
            _databaseService = App.ServiceProvider.GetRequiredService<DatabaseService>();
            Player = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            configuracaoService = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
            bibliotecaService = App.ServiceProvider.GetRequiredService<BibliotecaService>();
            audio = App.ServiceProvider.GetRequiredService<AudioPlayerService>();
            playlistPageViewModel = new(this);

            Configuracao config = configuracaoService.CarregarConfiguracao();
            CaminhoBackground = config.Background;
            MostrarImage = config.MostarImage;
            Border = config.BorderColor;
            FormatoLista = config.FormatoLista;

            bibliotecaService.PastasEmFalta += OnPastasEmFalta;
            bibliotecaService.BibliotecaCarregada += OnBibliotecaCarregada;

            _ = CarregarBiblioteca(config.RotaMusica);

            CurrentPage = new MusicListPageViewModel(this);

            AplicarMascote(config.Theme);
            audio.AplicarDispositivoGuardado(config.DispositivoNome, config.DispositivoDriver);
            _porCaminho = bibliotecaService.Musicas.ToDictionary(m => m.Caminho, StringComparer.OrdinalIgnoreCase);
           
        }

        /* METODOS */

        private async Task CarregarBiblioteca(List<string> rotaMusica)
        {
            EstaCarregandoMusicas = true;
            try {
                await bibliotecaService.GarantirBibliotecaCarregadaAsync(rotaMusica);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Erro em CarregarBiblioteca: {ex}");
            }
            finally {
                EstaCarregandoMusicas = false;
            }
        }

        private void OnBibliotecaCarregada()
        {
            if (!Player.AbertoPorFicheiro)
                Player.RestaurarUltimaMusica();
        }

        private async void OnPastasEmFalta(List<string> pastas)
        {
            if (pastas.Count == 0) {
                MostrarAvisoPastas = false;
                return;
            }
            MostrarAvisoPastas = true;
            await Task.Delay(1000);
            foreach (string p in pastas) {
                string texto = TextoHelper.Texto("Str_PastasEmFalta", p);
                await Task.Delay(500);
                PastasEmFalta.Add(texto);
            }
        }

        public void AplicarMascote(string tema)
        {
            string caminhoMascote = tema switch
            {
                "Yoru" => "/Resources/Mascotes/yoru.svg",
                "Yako" => "/Resources/Mascotes/yako.svg",
                "Asa" => "/Resources/Mascotes/asa.svg",
                "Zenko" => "/Resources/Mascotes/zenko.svg",
                "Nogitsune" => "/Resources/Mascotes/nogitsune.svg",
                _ => "/Resources/Mascotes/asa.svg"
            };
            Tema = caminhoMascote;
        }

        /* RELAYCOMMANDS */

        [RelayCommand]
        private void IrParaMusicas()
        {
            Page = "MusicList";
            CurrentPage = new MusicListPageViewModel(this);
        }

        [RelayCommand]
        private void IrParaPlaylist()
        {
            Page = "Playlist";
            CurrentPage = new PlaylistsPageViewModel(this);
        }

        [RelayCommand]
        private void IrParaConfiguracoes()
        {
            Page = "Config";
            CurrentPage = new ConfiguracoesPageViewModel(this);
        }

        [RelayCommand]
        private void IrParaArtistas()
        {
            Page = "Artistas";
            CurrentPage = new ArtistasPageViewModel(this);
        }

        [RelayCommand]
        private void IrParaAlbuns()
        {
            Page = "Albuns";
            CurrentPage = new AlbunsPageViewModel(this);
        }

        [RelayCommand]
        private void AbrirFavoritos()
        {
            Playlist? favoritos = _databaseService.ListarPlaylists().FirstOrDefault(p => p.Name == "Favoritos");
            if (favoritos == null) return;
            PlaylistDetailPageViewModel detalhe = new(this, () => new PlaylistsPageViewModel(this));
            detalhe.Carregar(favoritos);
            Page = "Favoritos";
            CurrentPage = detalhe;
        }

        [RelayCommand]
        private void FecharAvisoPastas()
        {
            PastasEmFalta.Clear();
            MostrarAvisoPastas = false;
        }

        [RelayCommand]
        private void CriarPlaylist()
        {
            if (string.IsNullOrWhiteSpace(NovaPlaylist)) return;

            _databaseService.CriarPlaylist(NovaPlaylist);

            List<Playlist> atualizadas = _databaseService.ListarPlaylists();
            playlistPageViewModel.Playlists.Clear();
            playlistPageViewModel.Playlists.Add(playlistPageViewModel.PlaylistLetras());
            foreach (Playlist p in atualizadas) {
                playlistPageViewModel.Playlists.Add(playlistPageViewModel.CarregarComCapa(p, _porCaminho));
            }

            NovaPlaylist = string.Empty;
            MostrarModalCriarPlaylist = false;

            if (CurrentPage is PlaylistsPageViewModel)
                CurrentPage = new PlaylistsPageViewModel(this);
        }

        [RelayCommand]
        private void FecharModal()
        {
            NovaPlaylist = string.Empty;
            MostrarModalCriarPlaylist = false;
        }

        private void RemoverPlaylist(Playlist playlist)
        {
            // Impede apagar a playlist de sistema
            if (playlist.Name == "Favoritos") return;

            _databaseService.RemoverPlaylist(playlist.Id);
            PlaylistComCapa? item = playlistPageViewModel.Playlists.FirstOrDefault(p => p.Playlist.Id == playlist.Id);
            if (item != null) playlistPageViewModel.Playlists.Remove(item);

            if (CurrentPage is PlaylistsPageViewModel)
                CurrentPage = new PlaylistsPageViewModel(this);
        }

        [RelayCommand]
        private void ConfirmarExclusaoPlaylist()
        {
            if (Playlist != null)
                RemoverPlaylist(Playlist);
            MostrarConfirmacaoExclusaoPlaylist = false;
            Playlist = null;
        }

        [RelayCommand]
        private void CancelarExclusaoPlaylist()
        {
            MostrarConfirmacaoExclusaoPlaylist = false;
            Playlist = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
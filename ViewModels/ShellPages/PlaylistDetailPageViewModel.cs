using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Helpers;
using KyoneMusic.Models;
using KyoneMusic.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace KyoneMusic.ViewModels.ShellPages
{
    public partial class PlaylistDetailPageViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _database;
        private readonly BibliotecaService _biblioteca;
        private readonly ConfiguracaoService _configuracao;
        private readonly OrdenacaoService _ordenacaoService;

        private readonly ShellViewModel _shell;
        public PlayerViewModel PlayerViewModel { get; set; }
        public Playlist? _playlistAtual;
        public ObservableCollection<Music> Musicas { get; set; } = new();
        private List<Music> _musicasOriginais = new(); // ordem da BD

        private readonly Func<object> _voltarPara;

        private ObservableCollection<Music> _todasAsMusicas = new();

        public ObservableCollection<Music> TodasAsMusicas {
            get => _todasAsMusicas;
            set { _todasAsMusicas = value; OnPropertyChanged(); }
        }

        private ObservableCollection<Music> _musicasDaPlaylist = new();

        public ObservableCollection<Music> MusicasDaPlaylist {
            get => _musicasDaPlaylist;
            set { _musicasDaPlaylist = value; OnPropertyChanged(); }
        }

        private List<Music> _musicasParaRemoverOriginal = new();
        private List<Music> _todasAsMusicasOriginal = new();

        public string NomePlaylist => TextoHelper.NormalizarTexto(_playlistAtual?.Name ?? "");
        public bool EhFavoritos => _playlistAtual?.Name == "Favoritos";
        public bool EhComLetra => _playlistAtual?.Id == Playlist.IdComLetra;

        public string NumMusicas => TextoHelper.Texto("Str_NumMusicas", Musicas.Count.ToString());

        private bool _mostrarPainelAdicionar;

        public bool MostrarPainelAdicionar {
            get => _mostrarPainelAdicionar;
            set {
                _mostrarPainelAdicionar = value;
                OnPropertyChanged();
            }
        }

        private bool _mostrarPainelRemover;

        public bool MostrarPainelRemover {
            get => _mostrarPainelRemover;
            set {
                _mostrarPainelRemover = value;
                OnPropertyChanged();
            }
        }

        private byte[]? _capa;

        public byte[]? Capa {
            get => _capa;
            set {
                _capa = value;
                OnPropertyChanged();
            }
        }

        public string DuracaoTotalFormatada {
            get {
                var totalSegundos = Musicas.Sum(m => ( m.Duracao.TotalSeconds )); // supondo que existe esse campo em segundos
                var ts = TimeSpan.FromSeconds(totalSegundos);
                return ts.Hours > 0 ? $"{ts.Hours}h {ts.Minutes}min" : $"{ts.Minutes} min";
            }
        }

        private string _textoPesquisaAdicionar = string.Empty;

        public string TextoPesquisaAdicionar {
            get => _textoPesquisaAdicionar;
            set {
                _textoPesquisaAdicionar = value;
                OnPropertyChanged();
                AtualizarFiltroAdicionar();
            }
        }

        private string _textoPesquisaRemover = string.Empty;

        public string TextoPesquisaRemover {
            get => _textoPesquisaRemover;
            set {
                _textoPesquisaRemover = value;
                OnPropertyChanged();
                AtualizarFiltroRemover();
            }
        }

        private int _numeroSelecionadas;

        public int NumeroSelecionadas {
            get => _numeroSelecionadas;
            set { _numeroSelecionadas = value; OnPropertyChanged(); }
        }

        private string _ordemAtual = string.Empty;

        public string OrdemAtual {
            get => _ordemAtual;
            set {
                _ordemAtual = value;
                OnPropertyChanged();
            }
        }

        private bool _ordemDescendente;

        public bool OrdemDescendente {
            get => _ordemDescendente;
            set {
                _ordemDescendente = value;
                OnPropertyChanged();
            }
        }

        private bool _mostrarOrdenacao;

        public bool MostrarOrdenacao {
            get => _mostrarOrdenacao;
            set {
                _mostrarOrdenacao = value;
                OnPropertyChanged();
            }
        }

        private List<Music> AplicarOrdem(List<Music> origem) => EhComLetra
                    ? _ordenacaoService.Ordenar(origem, _playlistAtual!.Ordem, _playlistAtual.Direcao)
                    : _ordenacaoService.OrdenarPlaylist(origem, _playlistAtual!.Ordem, _playlistAtual.Direcao);

        public PlaylistDetailPageViewModel(ShellViewModel shell, Func<object> voltarPara)
        {
            _shell = shell;
            _voltarPara = voltarPara;
            _database = App.ServiceProvider.GetRequiredService<DatabaseService>();
            _biblioteca = App.ServiceProvider.GetRequiredService<BibliotecaService>();
            _configuracao = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
            _ordenacaoService = App.ServiceProvider.GetRequiredService<OrdenacaoService>();
            PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            Musicas.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(DuracaoTotalFormatada));
                OnPropertyChanged(nameof(NumMusicas));
            };
            WeakReferenceMessenger.Default.Register<GostoAlteradoMessage>(this, (r, m) => ( (PlaylistDetailPageViewModel)r ).AoGostoAlterado(m.Musica));
        }

        public void Carregar(Playlist playlist)
        {
            _playlistAtual = playlist;
            OrdemAtual = playlist.Ordem;
            OrdemDescendente = playlist.Direcao;
            OnPropertyChanged(nameof(NomePlaylist));

            Application.Current.Dispatcher.Invoke(() => Musicas.Clear());

            var porCaminho = _biblioteca.Musicas.ToDictionary(m => m.Caminho, StringComparer.OrdinalIgnoreCase);
            Task.Run(() =>
            {
                List<Music> origem = [];
                if (EhComLetra) {
                    origem = [.. playlist.Musics];
                    AtualizarCapa(playlist);
                }
                else {
                    List<string> caminhos = EhFavoritos ? _database.ListarMusicasFavoritas() : _database.ListarMusicasDaPlaylist(playlist.Id);
                    foreach (string caminho in caminhos) {
                        if (porCaminho.TryGetValue(caminho, out Music? musica)) origem.Add(musica);
                    }
                    if (EhFavoritos) AtualizarCapaFavoritos(); else AtualizarCapa(playlist);
                }
                _musicasOriginais = origem;
                List<Music> ordenadas = AplicarOrdem(origem);
                Application.Current.Dispatcher.Invoke(() => Repovoar(ordenadas));
            });
        }

        public void MudarOrdenacao(string criterio, bool ordem)
        {
            if (_playlistAtual != null) {
                _playlistAtual.Ordem = criterio;
                _playlistAtual.Direcao = ordem;
                OrdemAtual = criterio;
                OrdemDescendente = ordem;

                OnPropertyChanged(nameof(OrdemAtual));

                List<Music> ordenadas = AplicarOrdem(_musicasOriginais);
                Repovoar(ordenadas);
                AtualizarCapaDaLista();

                Debug.WriteLine($"{_playlistAtual.Id},{criterio}, {ordem}");
                _database.ReordenarPlaylist(_playlistAtual.Id, _playlistAtual.Ordem, _playlistAtual.Direcao);
            }
        }

        public void AtualizarCapa(Playlist playlist)
        {
            string? caminhoRecente = _database.ObterCaminhoMusicaMaisRecente(playlist.Id);
            byte[]? capaEncontrada = null;

            if (caminhoRecente != null) {
                Music? musicaRecente = _biblioteca.LerMusicaPorCaminho(caminhoRecente);
                capaEncontrada = musicaRecente?.Image;
            }

            Application.Current.Dispatcher.Invoke(() => Capa = capaEncontrada);
        }

        public void AtualizarCapaFavoritos()
        {
            string? caminhoRecente = _database.ListarMusicasFavoritas().FirstOrDefault();
            byte[]? capaEncontrada = null;
            if (caminhoRecente != null) {
                Music? musicaRecente = _biblioteca.LerMusicaPorCaminho(caminhoRecente);
                capaEncontrada = musicaRecente?.Image;
            }
            Application.Current.Dispatcher.Invoke(() => Capa = capaEncontrada);
        }

        private void AtualizarCapaDaLista()
        {
            byte[]? primeira = Musicas.FirstOrDefault()?.Image;
            if (ReferenceEquals(primeira, Capa)) return;
            Capa = primeira;
        }

        private void Repovoar(List<Music> itens)
        {
            Musicas.Clear();
            foreach (Music m in itens) Musicas.Add(m);
            AtualizarCapaDaLista();
        }

        public void TocarMusica(Music musica, List<Music> ListaMusica = null!)
        {
            PlayerViewModel.TocarMusica(musica, ListaMusica ?? Musicas.ToList());
        }

        public void AdicionarMusicaEscolhida(List<Music> musicas)
        {
            if (_playlistAtual == null || EhComLetra) return;

            foreach (Music music in musicas) {
                bool jaEstaNaPlaylist = Musicas.Any(m =>
                                                                                                                                                                                                                                string.Equals(m.Caminho, music.Caminho, StringComparison.OrdinalIgnoreCase));

                if (jaEstaNaPlaylist) continue;

                if (EhFavoritos) {
                    music.Gosto = true;
                    _database.AlternarGosto(music.Caminho, true);
                    continue;
                }

                int musicaId = _database.ObterIdMusicaPorCaminho(music.Caminho);
                if (musicaId == -1) continue;

                _database.AdicionarMusicaAPlaylist(_playlistAtual.Id, musicaId);
            }

            RecarregarMusicasDaPlaylist();
            MostrarPainelAdicionar = false;
        }

        private void AtualizarFiltroAdicionar()
        {
            if (string.IsNullOrWhiteSpace(TextoPesquisaAdicionar)) {
                TodasAsMusicas = new ObservableCollection<Music>(_todasAsMusicasOriginal);
                return;
            }

            var termo = TextoPesquisaAdicionar.Trim();
            var filtradas = _todasAsMusicasOriginal.Where(m =>
                                                                                                                                                                                                                                    ( m.Titulo?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false ) ||
                                                                                                                                                                                                                                    ( m.Artista?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false ) ||
                                                                                                                                                                                                                                    ( m.Album?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false ));

            TodasAsMusicas = new ObservableCollection<Music>(filtradas);
        }

        private void AoGostoAlterado(Music musica)
        {
            if (!EhFavoritos) return;
            var lista = Musicas.Where(m => m.Caminho != musica.Caminho).ToList();
            if (musica.Gosto) lista.Insert(0, musica);
            Repovoar(lista);
            AtualizarCapaFavoritos();
        }

        public void RemoverMusicaEscolhida(List<Music> musicas)
        {
            if (_playlistAtual == null || EhComLetra) return;

            foreach (Music music in musicas) {
                if (EhFavoritos) {
                    _database.AlternarGosto(music.Caminho, false);
                    music.Gosto = false;
                    continue;
                }
                int musicaId = _database.ObterIdMusicaPorCaminho(music.Caminho);
                if (musicaId == -1) continue;
                _database.RemoverMusicaDaPlaylist(_playlistAtual.Id, musicaId);
            }

            RecarregarMusicasDaPlaylist();
            MostrarPainelRemover = false;
        }

        private void AtualizarFiltroRemover()
        {
            IEnumerable<Music> filtradas = _musicasParaRemoverOriginal;

            if (!string.IsNullOrWhiteSpace(TextoPesquisaRemover)) {
                string termo = TextoPesquisaRemover.Trim();

                filtradas = filtradas.Where(m =>
                                                                                                                                                                                                                                                            ( m.Titulo?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false ) ||
                                                                                                                                                                                                                                                            ( m.Artista?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false ) ||
                                                                                                                                                                                                                                                            ( m.Album?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false ));
            }

            MusicasDaPlaylist = new ObservableCollection<Music>(filtradas);
        }

        [RelayCommand]
        private void Voltar() => _shell.CurrentPage = _voltarPara();

        [RelayCommand]
        private void AbrirPainelAdicionar()
        {
            MostrarPainelAdicionar = true;
            TextoPesquisaAdicionar = string.Empty;
            NumeroSelecionadas = 0;

            var candidatas = _biblioteca.Musicas.Where(m => !Musicas.Any(x => x.Caminho == m.Caminho));

            _todasAsMusicasOriginal = _ordenacaoService.Ordenar(candidatas.ToList(), "Alfabetica", false);
            TodasAsMusicas = new ObservableCollection<Music>(_todasAsMusicasOriginal);
        }

        [RelayCommand]
        private void FecharPainelAdicionar()
        {
            NumeroSelecionadas = 0;
            MostrarPainelAdicionar = false;
        }

        private void RecarregarMusicasDaPlaylist()
        {
            var porCaminho = _biblioteca.Musicas.ToDictionary(
m => m.Caminho,
                StringComparer.OrdinalIgnoreCase);

            List<string> caminhos = EhFavoritos
                ? _database.ListarMusicasFavoritas()
                : _database.ListarMusicasDaPlaylist(_playlistAtual!.Id);

            _musicasOriginais = caminhos
            .Where(porCaminho.ContainsKey)
.Select(caminho => porCaminho[caminho])
                .ToList();

            Repovoar(AplicarOrdem(_musicasOriginais));
        }

        [RelayCommand]
        private void RemoverMusica(Music musica)
        {
            RemoverMusicaEscolhida(new List<Music> { musica });
        }

        [RelayCommand]
        private void TocarOrdemAleatorio()
        {
            if (Musicas.Count == 0) return;

            Music[] embaralhada = Musicas.ToArray(); // cópia: Musicas não é alterada
            Random.Shared.Shuffle(embaralhada);

            PlayerViewModel.TocarMusica(embaralhada[0], embaralhada.ToList());
        }

        [RelayCommand]
        private void MostrarPainelOrdenacao()
        {
            MostrarOrdenacao = !MostrarOrdenacao;
        }

        [RelayCommand]
        private void FecharPainelOrdenacao()
        {
            MostrarOrdenacao = false;
        }

        [RelayCommand]
        private void AbrirPainelRemover()
        {
            MostrarPainelRemover = true;
            NumeroSelecionadas = 0;

            _musicasParaRemoverOriginal = Musicas.ToList();
            TextoPesquisaRemover = string.Empty;
        }

        [RelayCommand]
        private void FecharPainelRemover()
        {
            NumeroSelecionadas = 0;
            MostrarPainelRemover = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
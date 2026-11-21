using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Helpers;
using KyoneMusic.Models;
using KyoneMusic.Services;
using KyoneMusic.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace KyoneMusic.ViewModels.ShellPages
{
    public partial class PlaylistsPageViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PlaylistComCapa> Playlists { get; set; } = new ObservableCollection<PlaylistComCapa>();
        protected BibliotecaService _biblioteca;
        private OrdenacaoService _ordenacaoService;
        public ObservableCollection<Music> Musicas { get; set; } = new();
        protected DatabaseService _database;
        protected ShellViewModel _shell;
        private Dictionary<string, Music> _porCaminho;
        private const int maxItem = 2;

        public PlaylistsPageViewModel(ShellViewModel shell)
        {
            _database = App.ServiceProvider.GetRequiredService<DatabaseService>();
            _biblioteca = App.ServiceProvider.GetRequiredService<BibliotecaService>();
            _ordenacaoService = App.ServiceProvider.GetRequiredService<OrdenacaoService>();
            _shell = shell;

            List<Playlist> existentes = _database.ListarPlaylists();

            _porCaminho = _biblioteca.Musicas.ToDictionary(m => m.Caminho, StringComparer.OrdinalIgnoreCase);

            Playlists.Add(PlaylistLetras());
            foreach (Playlist p in existentes) {
                Playlists.Add(CarregarComCapa(p, _porCaminho));
            }
        }

        public PlaylistComCapa CarregarComCapa(Playlist playlist, Dictionary<string, Music> porCaminho)
        {
            List<string> todosCaminhos = playlist.Name == "Favoritos"
                ? _database.ListarMusicasFavoritas()
                : _database.ListarMusicasDaPlaylist(playlist.Id);

            List<Music> existentes = todosCaminhos.Select(c => porCaminho.TryGetValue(c, out Music? m) ? m : null).OfType<Music>().ToList();
            Debug.WriteLine($"{playlist.Ordem},{playlist.Direcao}");
            List<Music> lista = _ordenacaoService.OrdenarPlaylist(existentes, playlist.Ordem, playlist.Direcao);

            byte[]? capa = lista.FirstOrDefault()?.Image;
            bool menuOpen = true;

            List<string> listaMusicas = lista.Take(maxItem).Select(m => m.Titulo).ToList();

            if (playlist.Name == "Favoritos") {
                menuOpen = false;
            }
            playlist.NumMusicas = TextoHelper.Texto("Str_NumMusicas", $"{lista.Count}");

            return new PlaylistComCapa(playlist, capa, listaMusicas, menuOpen);
        }

        public PlaylistComCapa PlaylistLetras()
        {
            var musicasComLetra = _ordenacaoService.Ordenar(_biblioteca.Musicas.Where(m => BibliotecaService.TemLrcComConteudo(m.Caminho)).ToList(), "Alfabetica", false);

            var comLetra = new Playlist
            {
                Id = Playlist.IdComLetra,
                Name = (string)Application.Current.FindResource("Str_ComLetra"),
                DataCriacao = DateTime.MinValue,
                Ordem = "Alfabetica",
                Musics = musicasComLetra,
                NumMusicas = TextoHelper.Texto("Str_NumMusicas", $" {musicasComLetra.Count}")
            };

            byte[]? capa = musicasComLetra.FirstOrDefault()?.Image;
            List<string> listaMusicas = musicasComLetra.Take(maxItem).Select(m => m.Titulo).ToList();

            return new PlaylistComCapa(comLetra, capa, listaMusicas, false);
        }

        [RelayCommand]
        private void MostrarModal()
        {
            _shell.MostrarModalCriarPlaylist = true;
        }

        [RelayCommand]
        public void AbrirPlaylist(Playlist playlist)
        {
            PlaylistDetailPageViewModel detalhe = new(_shell, () => new PlaylistsPageViewModel(_shell));
            detalhe.Carregar(playlist);
            if (playlist.Name == "Favoritos") _shell.Page = "Favoritos";
            _shell.CurrentPage = detalhe;
        }

        [RelayCommand]
        private void PedirExclusao(Playlist playlist)
        {
            _shell.Playlist = playlist;
            _shell.PlayListParaExcluir = TextoHelper.Texto("Str_ConfirmarEliminarPlaylist", playlist.Name);
            _shell.MostrarConfirmacaoExclusaoPlaylist = true;
        }

        [RelayCommand]
        private void EditarPlaylist(Playlist playlist)
        {
            var window = new EditarPlaylistWindow(playlist, _biblioteca)
            {
                Owner = Application.Current.MainWindow
            }
            ;
            window.ShowDialog();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
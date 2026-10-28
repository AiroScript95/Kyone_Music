using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Project_Kitsune.Models;
using Project_Kitsune.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Project_Kitsune.ViewModels.ShellPages.SearchPages
{
    public partial class SearchPlaylistViewPageModel : INotifyPropertyChanged
    {
        private readonly ShellViewModel _shell;
        public string searchString { get; }
        private readonly BibliotecaService _biblioteca;
        public PlayerViewModel PlayerViewModel { get; }
        private Func<object> _voltarPara;

        private static string Texto(string chave, string termo) => string.Format((string)Application.Current.FindResource(chave), termo);

        public string Titulo => Texto("Str_ResultadosPara", searchString);

        private ObservableCollection<PlaylistComCapa> _resultadosPlaylists = new();

        public ObservableCollection<PlaylistComCapa> ResultadosPlaylists {
            get => _resultadosPlaylists;
            private set { _resultadosPlaylists = value; OnPropertyChanged(); }
        }

        public SearchPlaylistViewPageModel(ShellViewModel shell, Func<object> voltar, string resultado)
        {
            _shell = shell;
            searchString = resultado;
            _voltarPara = voltar;
            _biblioteca = App.ServiceProvider.GetRequiredService<BibliotecaService>();
            PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            Pesquisar();
        }

        private void Pesquisar()
        {
            string termo = searchString.Trim();
            if (termo.Length == 0) {
                ResultadosPlaylists = new();
                return;
            }

            var playlistsEncontradas = new PlaylistsPageViewModel(_shell).Playlists.Where(p =>
                   p.Playlist.Id != Playlist.IdComLetra &&
                   ( p.Playlist.Name?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false )).ToList();

            ResultadosPlaylists = new ObservableCollection<PlaylistComCapa>(playlistsEncontradas);
        }

        [RelayCommand]
        private void AbrirPlaylist(Playlist playlist)
        {
            if (playlist == null) return;
            PlaylistDetailPageViewModel detalhe = new(_shell, () => new SearchPlaylistViewPageModel(_shell, _voltarPara, searchString));
            detalhe.Carregar(playlist);
            _shell.CurrentPage = detalhe;
        }

        [RelayCommand]
        private void Voltar() => _shell.CurrentPage = _voltarPara();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
using CommunityToolkit.Mvvm.Input;
using KyoneMusic.Models;
using KyoneMusic.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace KyoneMusic.ViewModels
{
    public partial class EditarPlaylistViewModel : INotifyPropertyChanged
    {
        private readonly Playlist Playlist;
        private readonly BibliotecaService Biblioteca;

        public string Nome { get; set; }

        public event Action? FechadoComSucesso;

        public EditarPlaylistViewModel(Playlist playlist, BibliotecaService biblioteca)
        {
            Playlist = playlist;
            Biblioteca = biblioteca;
            Nome = playlist.Name;
        }

        [RelayCommand]
        private void Salvar()
        {
            try {
                Biblioteca.SalvarEdicaoPlyalist(Playlist, Nome);
                FechadoComSucesso?.Invoke();
            }
            catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
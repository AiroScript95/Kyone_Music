using CommunityToolkit.Mvvm.Input;
using KyoneMusic.Models;
using KyoneMusic.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace KyoneMusic.ViewModels
{
    public partial class EditarMusicaViewModel : INotifyPropertyChanged
    {
        private readonly Music _musicaOriginal;
        private readonly BibliotecaService _biblioteca;

        public string Titulo { get; set; }
        public string Artista { get; set; }
        public string Album { get; set; }
        public string Genero { get; set; }

        public event Action? FechadoComSucesso;

        public EditarMusicaViewModel(Music musica, BibliotecaService biblioteca)
        {
            _musicaOriginal = musica;
            _biblioteca = biblioteca;
            Titulo = musica.Titulo;
            Artista = musica.Artista;
            Album = musica.Album;
            Genero = musica.Genero;
        }

        [RelayCommand]
        private void Salvar()
        {
            try {
                _biblioteca.SalvarEdicaoMusica(_musicaOriginal, Titulo, Artista, Album, Genero);
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
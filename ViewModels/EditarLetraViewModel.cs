using CommunityToolkit.Mvvm.Input;
using Project_Kitsune.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace Project_Kitsune.ViewModels
{
    public partial class EditarLetraViewModel : INotifyPropertyChanged
    {
        private readonly BibliotecaService _biblioteca;
        private readonly string _caminhoLrc;

        public string Letra { get; set; }

        public event Action? FechadoComSucesso;

        public EditarLetraViewModel(string caminhoLrc, string textoInicial, BibliotecaService biblioteca)
        {
            _caminhoLrc = caminhoLrc;
            _biblioteca = biblioteca;
            Letra = textoInicial;
        }

        [RelayCommand]
        private async Task Salvar()
        {
            try {
                await _biblioteca.SalvarLetra(_caminhoLrc, Letra);
                FechadoComSucesso?.Invoke();
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                MessageBox.Show($"Não foi possível guardar a letra:\n{ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
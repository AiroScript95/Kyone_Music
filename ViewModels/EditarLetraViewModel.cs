using CommunityToolkit.Mvvm.Input;
using KyoneMusic.Services;
using KyoneMusic.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace KyoneMusic.ViewModels
{
    public partial class EditarLetraViewModel : INotifyPropertyChanged
    {
        private readonly BibliotecaService _biblioteca;
        private readonly string _caminhoLrc;

        public string Letra { get; set; }
        private FonteLetra Fonte;
        private OrigemLetraStore origemLetraStore = new();

        public event Action? FechadoComSucesso;

        public EditarLetraViewModel(string caminhoLrc, string textoInicial, FonteLetra fonte, BibliotecaService biblioteca)
        {
            _caminhoLrc = caminhoLrc;
            _biblioteca = biblioteca;
            Letra = textoInicial;
            Fonte = fonte;
        }

        [RelayCommand]
        private async Task Salvar()
        {
            try {
                await _biblioteca.SalvarLetra(_caminhoLrc, Letra);
                origemLetraStore.Registar(_caminhoLrc, Fonte);
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
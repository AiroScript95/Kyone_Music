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
    public partial class SearchArtistaPageViewModel : INotifyPropertyChanged
    {
        private readonly ShellViewModel _shell;
        public string searchString { get; }
        private readonly BibliotecaService _biblioteca;
        public PlayerViewModel PlayerViewModel { get; }
        private Func<object> _voltarPara;

        private static string Texto(string chave, string termo) => string.Format((string)Application.Current.FindResource(chave), termo);

        public string Titulo => Texto("Str_ResultadosPara", searchString);

        private ObservableCollection<GrupoMusicas> _resultadosArtistas = new();

        public ObservableCollection<GrupoMusicas> ResultadosArtistas {
            get => _resultadosArtistas;
            private set { _resultadosArtistas = value; OnPropertyChanged(); }
        }

        public SearchArtistaPageViewModel(ShellViewModel shell, Func<object> voltar, string resultado)
        {
            _shell = shell;
            searchString = resultado;
            _voltarPara = voltar;
            _biblioteca = App.ServiceProvider.GetRequiredService<BibliotecaService>();
            PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            _ = Pesquisar();
        }

        private async Task Pesquisar()
        {
            string termo = searchString.Trim();
            if (termo.Length == 0) {
                ResultadosArtistas = new();
                return;
            }

            var grupos = App.ServiceProvider.GetRequiredService<GruposService>();
            var artistas = await grupos.ObterArtistasAsync();

            var artistasEncontrados = artistas.Where(g => g.Nome?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            ResultadosArtistas = new ObservableCollection<GrupoMusicas>(artistasEncontrados);
        }

        [RelayCommand]
        private void Voltar() => _shell.CurrentPage = _voltarPara();

        [RelayCommand]
        private void AbrirArtista(GrupoMusicas grupo)
        {
            GrupoDetailPageViewModel detalhe = new(_shell, () => new SearchArtistaPageViewModel(_shell, _voltarPara, searchString), false);
            detalhe.Carregar(grupo);
            _shell.CurrentPage = detalhe;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace KyoneMusic.ViewModels.ShellPages.SearchPages
{
    public partial class SearchAlbumPageViewModel : INotifyPropertyChanged
    {
        private readonly ShellViewModel _shell;
        public string searchString { get; }
        private readonly BibliotecaService _biblioteca;
        public PlayerViewModel PlayerViewModel { get; }
        private Func<object> _voltarPara;

        private static string Texto(string chave, string termo) => string.Format((string)Application.Current.FindResource(chave), termo);

        public string Titulo => Texto("Str_ResultadosPara", searchString);

        private ObservableCollection<GrupoMusicas> _resultadosAlbuns = new();

        public ObservableCollection<GrupoMusicas> ResultadosAlbuns {
            get => _resultadosAlbuns;
            private set { _resultadosAlbuns = value; OnPropertyChanged(); }
        }

        public SearchAlbumPageViewModel(ShellViewModel shell, Func<object> voltar, string resultado)
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
                ResultadosAlbuns = new();
                return;
            }

            var grupos = App.ServiceProvider.GetRequiredService<GruposService>();
            var albuns = await grupos.ObterAlbunsAsync();

            var albunsEncontrados = albuns.Where(g => g.Nome?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            ResultadosAlbuns = new ObservableCollection<GrupoMusicas>(albunsEncontrados);
        }

        [RelayCommand]
        private void AbrirAlbum(GrupoMusicas grupo)
        {
            GrupoDetailPageViewModel detalhe = new(_shell, () => new SearchAlbumPageViewModel(_shell, _voltarPara, searchString), false);
            detalhe.Carregar(grupo);
            _shell.CurrentPage = detalhe;
        }

        [RelayCommand]
        private void Voltar() => _shell.CurrentPage = _voltarPara();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
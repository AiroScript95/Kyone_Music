using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace KyoneMusic.ViewModels
{
    public class LoadingViewModel : INotifyPropertyChanged
    {
        /* ESTADOS - (Campos/Propriedades) */
        private readonly DatabaseService databaseService;
        private readonly ConfiguracaoService configService;
        public ShellViewModel ShellViewModel { get; set; }

        public event Action? Terminado;

        public LoadingViewModel()
        {
            databaseService = App.ServiceProvider.GetRequiredService<DatabaseService>();
            configService = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
            ShellViewModel = App.ServiceProvider.GetRequiredService<ShellViewModel>();
            Configuracao configAtual = configService.CarregarConfiguracao();
            string idiomaInicial = string.IsNullOrEmpty(configAtual.Idioma) ?
             App.DetectarIdiomaSistema() : configAtual.Idioma;
            ( (App)Application.Current ).AplicarIdioma(idiomaInicial);

            if (!string.IsNullOrEmpty(configAtual.Theme)) {
                ( (App)Application.Current ).AplicarTema(configAtual.Theme);
            }
            ShellViewModel.AplicarMascote(configAtual.Theme);
            _ = Carregar();
        }

        /* METODOS */

        private async Task Carregar()
        {
            await Task.Run(() =>
            {
                databaseService.IniciarDatabase();
                databaseService.LimparMusicasOrfas();
            });
            Configuracao configAtual = configService.CarregarConfiguracao();
            configService.GuardarConfiguracao(configAtual);
            await Task.Delay(500);
            Terminado?.Invoke();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
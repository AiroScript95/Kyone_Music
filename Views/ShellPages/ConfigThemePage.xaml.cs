using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Services;
using KyoneMusic.ViewModels.ShellPages;
using System.Windows;
using System.Windows.Controls;

namespace KyoneMusic.Views.ShellPages
{
    /// <summary>
    /// Interaction logic for ConfigTemaPage.xaml
    /// </summary>
    public partial class ConfigThemePage : UserControl
    {
        protected ConfiguracaoService configuracaoService;

        public ConfigThemePage()
        {
            configuracaoService = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
            InitializeComponent();
            Configuracao configuracao = configuracaoService.CarregarConfiguracao();
            if (configuracao.FormatoLista == "List") RadioList.IsChecked = true;
            if (configuracao.FormatoLista == "Grid") RadioGrid.IsChecked = true;
            if (configuracao.FormatoLista == "Compact") RadioCompact.IsChecked = true;
            else RadioList.IsChecked = true;
        }

        private void TemaSelect_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.IsChecked == true) {
                string criterio = radio.Name switch
                {
                    "RadioAsa" => "Asa",
                    "RadioKuko" => "Kuko",
                    "RadioNogitsune" => "Nogitsune",
                    "RadioReiko" => "Reiko",
                    "RadioTenko" => "Tenko",
                    "RadioYako" => "Yako",
                    "RadioYoru" => "Yoru",
                    "RadioZenko" => "Zenko",
                    _ => "Asa"
                };

                if (DataContext is ConfigThemePageViewModel viewModel) {
                    viewModel.TemaSelecionado = criterio;
                }
            }
        }

        private void BorderSelect_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.IsChecked == true) {
                string criterio = radio.Name switch
                {
                    "RadioNormal" => "Normal",
                    "RadioForte" => "Forte",
                    "RadioColor" => "Color",
                    "RadioColor2" => "Color2",
                    "RadioColor3" => "Color3",
                    _ => "Normal"
                };

                if (DataContext is ConfigThemePageViewModel viewModel) {
                    viewModel.MudarBorder(criterio);
                }
            }
        }

        private void FormatoSelect_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.IsChecked == true) {
                string formato = radio.Name switch
                {
                    "RadioList" => "List",
                    "RadioGrid" => "Grid",
                    "RadioCompact" => "Compact",
                    _ => "List"
                };
                if (DataContext is ConfigThemePageViewModel viewModel) {
                    viewModel.MudarFormato(formato);
                }
            }
        }
    }
}
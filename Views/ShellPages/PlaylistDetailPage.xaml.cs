using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.ViewModels;
using KyoneMusic.ViewModels.ShellPages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KyoneMusic.Views.ShellPages
{
    /// <summary>
    /// Interaction logic for PlaylistDetailPage.xaml
    /// </summary>
    public partial class PlaylistDetailPage : UserControl
    {
        public PlaylistDetailPage()
        {
            InitializeComponent();
            Loaded += (_, _) => AtualizarRadios();
        }

        private void AtualizarRadios()
        {
            if (DataContext is not PlaylistDetailPageViewModel vm)
                return;

            RadioAlfabetica.IsChecked = vm.OrdemAtual == "Alfabetica";
            RadioMaisRecente.IsChecked = vm.OrdemAtual == "MaisRecente";
            RadioMaisTocada.IsChecked = vm.OrdemAtual == "MaisTocada";
            RadioAleatorio.IsChecked = vm.OrdemAtual == "Aleatorio";
            RadioDuracao.IsChecked = vm.OrdemAtual == "Duracao";

            RadioDescendete.IsChecked = vm.OrdemDescendente;
            RadioAscendente.IsChecked = !vm.OrdemDescendente;
        }

        private void Musica_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elemento && elemento.DataContext is Music musica) {
                if (DataContext is PlaylistDetailPageViewModel viewModel) {
                    viewModel.TocarMusica(musica);
                }
            }
        }

        /* MUSICA EVENTS*/

        private void ConfirmarSelecao_Click(object sender, RoutedEventArgs e)
        {
            List<Music> selecionadas = new List<Music>();
            foreach (object item in ListaTodasMusicas.SelectedItems) {
                if (item is Music musica) {
                    selecionadas.Add(musica);
                }
            }

            if (DataContext is PlaylistDetailPageViewModel viewModel) {
                viewModel.AdicionarMusicaEscolhida(selecionadas);
            }
        }

        private void ConfirmarRemocao_Click(object sender, RoutedEventArgs e)
        {
            List<Music> selecionadas = new List<Music>();
            foreach (object item in ListaParaRemover.SelectedItems) {
                if (item is Music musica) {
                    selecionadas.Add(musica);
                }
            }

            if (DataContext is PlaylistDetailPageViewModel viewModel) {
                viewModel.RemoverMusicaEscolhida(selecionadas);
            }
        }

        private void MenuMusica_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null) {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void ListaTodasMusicas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is PlaylistDetailPageViewModel vm) {
                vm.NumeroSelecionadas = ListaTodasMusicas.SelectedItems.Count;
            }
        }

        private void ListaParaRemover_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is PlaylistDetailPageViewModel vm) {
                vm.NumeroSelecionadas = ListaParaRemover.SelectedItems.Count;
            }
        }

        private void ContextMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            try {
                var playerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();

                if (sender is FrameworkElement { DataContext: Music musica }
                && musica.Caminho == playerViewModel.MusicaAtual?.Caminho) {
                    e.Handled = true; // música a tocar: não abre menu
                    return;
                }

                if (sender is FrameworkElement { ContextMenu: { } menu }
                && DataContext is PlaylistDetailPageViewModel vm) {
                    foreach (MenuItem item in menu.Items.OfType<MenuItem>().Where(i => i.Name == "ItemRemover"))
                        item.Visibility = vm.EhComLetra ? Visibility.Collapsed : Visibility.Visible;
                }

                playerViewModel.CarregarPlaylistsDisponiveis();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private void RemoverMusica_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item
            && item.Parent is ContextMenu menu
            && menu.PlacementTarget is FrameworkElement alvo
            && alvo.DataContext is Music musica
            && DataContext is PlaylistDetailPageViewModel vm) {
                vm.RemoverMusicaCommand.Execute(musica);
            }
        }

        private void Ordenacao_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PlaylistDetailPageViewModel vm) return;

            string ObterCriterioAtual()
            {
                if (RadioAlfabetica.IsChecked == true) return "Alfabetica";
                if (RadioMaisRecente.IsChecked == true) return "MaisRecente";
                if (RadioMaisTocada.IsChecked == true) return "MaisTocada";
                if (RadioDuracao.IsChecked == true) return "Duracao";
                return "Aleatorio";
            }

            bool DirecaoPadraoPara(string criterio)
            {
                return criterio switch
                {
                    "MaisRecente" => true,   // mais recentes primeiro
                    "MaisTocada" => true,    // mais tocadas primeiro
                    "Duracao" => true,       // (opcional) duracao maior primeiro
                    "Alfabetica" => false,   // A..Z
                    _ => false
                };
            }

            if (sender is RadioButton rb) {
                // Se o usuário clicou em um radio de CRITÉRIO
                if (rb.GroupName == "Ordenacao") {
                    string criterio = ObterCriterioAtual();
                    bool defaultDesc = DirecaoPadraoPara(criterio);

                    // Atualiza visuais de direção para refletir o default
                    RadioDescendete.IsChecked = defaultDesc;
                    RadioAscendente.IsChecked = !defaultDesc;

                    // Chama o ViewModel com o critério e a direção padrão
                    vm.MudarOrdenacao(criterio, defaultDesc);
                    return;
                }

                // Se o usuário clicou em um radio de DIREÇÃO
                if (rb.GroupName == "Direcao") {
                    string criterio = ObterCriterioAtual();
                    bool ordemDesc = RadioDescendete.IsChecked == true;
                    vm.MudarOrdenacao(criterio, ordemDesc);
                    return;
                }
            }

            // Caso genérico — reaplica com o estado atual dos radios
            string criterioAtual = ObterCriterioAtual();
            bool ordem = RadioDescendete.IsChecked == true;
            vm.MudarOrdenacao(criterioAtual, ordem);
        }
    }
}
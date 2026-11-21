using KyoneMusic.Models;
using KyoneMusic.ViewModels.ShellPages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KyoneMusic.Views.ShellPages
{
    /// <summary>
    /// Interaction logic for PlaylistsPages.xaml
    /// </summary>
    public partial class PlaylistsPages : UserControl
    {
        public PlaylistsPages()
        {
            InitializeComponent();
        }

        private void AbrirMenuPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            DependencyObject? elemento = button;

            while (elemento is not null && elemento is not Border)
                elemento = VisualTreeHelper.GetParent(elemento);

            if (elemento is Border { ContextMenu: not null } card) {
                ContextMenu menu = card.ContextMenu;
                menu.DataContext = card.DataContext;
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            }
        }

        private void Playlist_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (EstaDentroDeBotao(e.OriginalSource as DependencyObject))
                return;

            if (sender is FrameworkElement elemento && elemento.DataContext is PlaylistComCapa item) {
                if (DataContext is PlaylistsPageViewModel viewModel)
                    viewModel.AbrirPlaylist(item.Playlist);
            }
        }

        private static bool EstaDentroDeBotao(DependencyObject? elemento)
        {
            while (elemento is not null) {
                if (elemento is Button)
                    return true;

                elemento = VisualTreeHelper.GetParent(elemento);
            }

            return false;
        }
    }
}
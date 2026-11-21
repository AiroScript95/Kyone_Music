using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Helpers;
using KyoneMusic.Models;
using KyoneMusic.Services;
using KyoneMusic.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KyoneMusic.Views
{
    /// <summary>
    /// Lógica interna para EditarPlaylistWindow.xaml
    /// </summary>
    public partial class EditarPlaylistWindow : Window
    {
        private readonly ShellViewModel _shellViewModel;

        public EditarPlaylistWindow(Playlist playlist, BibliotecaService biblioteca)
        {
            _shellViewModel = App.ServiceProvider.GetRequiredService<ShellViewModel>();
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            ( (App)Application.Current ).TemaAlterado += AtualizarCorBordaNativa;
            ( (App)Application.Current ).TemaAlterado += AtualizarCorTitle;

            InitializeComponent();
            var vm = new EditarPlaylistViewModel(playlist, biblioteca);
            vm.FechadoComSucesso += () => { DialogResult = true; Close(); };
            DataContext = vm;

            this.Closing += EditarPlaylistWindow_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            DwmHelper.AtivarCantosArredondados(this);
            AtualizarCorBordaNativa();
            AtualizarCorTitle();
        }

        private void AtualizarCorTitle()
        {
            if (Application.Current.Resources["SurfaceBrush"] is SolidColorBrush brush) {
                DwmHelper.DefinirCorTitleBar(this, brush.Color);
            }
        }

        private void AtualizarCorBordaNativa()
        {
            string chaveRecurso = _shellViewModel.Border switch
            {
                "Normal" => "BorderBrush2",
                "Color" => "AccentBrush",
                "Color2" => "AccentAltBrush",
                "Color3" => "AccentTertiaryBrush",
                _ => "BorderBrush2"
            };

            if (TryFindResource(chaveRecurso) is SolidColorBrush brush) {
                DwmHelper.DefinirCorBorda(this, brush.Color);
            }
        }

        private void ShellViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShellViewModel.Border))
                AtualizarCorBordaNativa();
        }

        private void EditarPlaylistWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            ( (App)Application.Current ).TemaAlterado -= AtualizarCorBordaNativa;
            ( (App)Application.Current ).TemaAlterado -= AtualizarCorTitle;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
using Microsoft.Extensions.DependencyInjection;
using Project_Kitsune.Helpers;
using Project_Kitsune.Services;
using Project_Kitsune.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Project_Kitsune.Views
{
    /// <summary>
    /// Lógica interna para EditarLetraWindow.xaml
    /// </summary>
    public partial class EditarLetraWindow : Window
    {
        private readonly ShellViewModel _shellViewModel;

        public EditarLetraWindow(EditarLetraViewModel vm)
        {
            _shellViewModel = App.ServiceProvider.GetRequiredService<ShellViewModel>();
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            ( (App)Application.Current ).TemaAlterado += AtualizarCorBordaNativa;

            InitializeComponent();
            ( (App)Application.Current ).TemaAlterado += AtualizarCorTitle;
            vm.FechadoComSucesso += () => { DialogResult = true; Close(); };
            DataContext = vm;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            DwmHelper.AtivarCantosArredondados(this);
            AtualizarCorBordaNativa();
            AtualizarCorTitle();
            CustomTitleBar.Apply(this, MinimizeButton, MaximizeButton, CloseButton);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
            else SystemCommands.MaximizeWindow(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

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

        private void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShellViewModel.Border))
                AtualizarCorBordaNativa();
        }

        private void EditarMusicaWindow_Closing(object? sender, CancelEventArgs e)
        {
            _shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            ( (App)Application.Current ).TemaAlterado -= AtualizarCorBordaNativa;
            ( (App)Application.Current ).TemaAlterado -= AtualizarCorTitle;
        }
    }
}
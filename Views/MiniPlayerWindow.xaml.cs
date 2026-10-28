using Microsoft.Extensions.DependencyInjection;
using Project_Kitsune.Helpers;
using Project_Kitsune.Models;
using Project_Kitsune.Services;
using Project_Kitsune.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Project_Kitsune.Views
{
    public partial class MiniPlayerWindow : Window
    {
        private readonly PlayerViewModel player;
        private readonly ShellViewModel _shellViewModel;
        private readonly ConfiguracaoService configuracaoService;
        private readonly Window _mainWindow;
        private DispatcherTimer? _timerScroll;
        private readonly Dictionary<LinhaLetra, FrameworkElement> _mapaLinhas = new();

        public MiniPlayerWindow(Window mainWindow)
        {
            player = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            _shellViewModel = App.ServiceProvider.GetRequiredService<ShellViewModel>();
            configuracaoService = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
            _mainWindow = mainWindow;

            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            ( (App)Application.Current ).TemaAlterado += AtualizarCorBordaNativa;
            ( (App)Application.Current ).TemaAlterado += AtualizarCorTitle;

            InitializeComponent();

            DataContext = player;
            Topmost = true;

            player.PropertyChanged += Player_PropertyChanged;
            ItemsLetras.ItemContainerGenerator.StatusChanged += ItemsLetras_StatusChanged;

            var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(20))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            Rotacao.BeginAnimation(RotateTransform.AngleProperty, anim);

            Loaded += MiniPlayerWindow_Loaded;
            Closing += MiniPlayerWindow_Closing;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            DwmHelper.AtivarCantosArredondados(this);
            AtualizarCorBordaNativa();
            AtualizarCorTitle();
        }

        /* ===== JANELA ===== */

        private void Topo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
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

        private void MiniPlayerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            ( (App)Application.Current ).TemaAlterado -= AtualizarCorBordaNativa;
            ( (App)Application.Current ).TemaAlterado -= AtualizarCorTitle;

            player.PropertyChanged -= Player_PropertyChanged;
            ItemsLetras.ItemContainerGenerator.StatusChanged -= ItemsLetras_StatusChanged;

            _timerScroll?.Stop();
            _timerScroll = null;
        }

        private void MiniPlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Configuracao configuracao = configuracaoService.CarregarConfiguracao();

            double x = configuracao.MiniJanelaX;
            double y = configuracao.MiniJanelaY;
            double margin = 10;

            bool estaNaTela = x >= SystemParameters.VirtualScreenLeft &&
                              x < ( SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth ) &&
                              y >= SystemParameters.VirtualScreenTop &&
                              y < ( SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight );

            if (estaNaTela && ( x != 0 || y != 0 )) {
                Left = x;
                Top = y;
            }
            else {
                // Posiciona no canto inferior direito
                Left = SystemParameters.WorkArea.Right - ActualWidth - margin;
                Top = SystemParameters.WorkArea.Top + margin;
            }

            // Centra já a linha atual (dá tempo ao layout de gerar as linhas)
            Dispatcher.BeginInvoke(() => AtualizarScrollLetra(player), DispatcherPriority.Loaded);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Salva sempre a posição, independente do estado da janela principal
            Configuracao configuracao = configuracaoService.CarregarConfiguracao();
            configuracao.MiniJanelaX = Left;
            configuracao.MiniJanelaY = Top;
            configuracaoService.GuardarConfiguracao(configuracao);

            if (!_mainWindow.IsVisible) {
                _mainWindow.Show();
            }
        }

        /* ===== LETRAS / SCROLL ===== */

        private void Player_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayerViewModel.LinhaAtual)) {
                AtualizarScrollLetra(player);
            }
            else if (e.PropertyName == nameof(PlayerViewModel.PosicaoAtualMs)) {
                if (player.LetraAtual?.Sincronizada == false)
                    AtualizarScrollLetra(player);
            }
        }

        private void ItemsLetras_StatusChanged(object? sender, EventArgs e)
        {
            if (ItemsLetras.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                return;

            _mapaLinhas.Clear();
            foreach (var item in ItemsLetras.Items) {
                if (item is LinhaLetra linha &&
                    ItemsLetras.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement fe) {
                    _mapaLinhas[linha] = fe;
                }
            }
        }

        private void AtualizarScrollLetra(PlayerViewModel player)
        {
            if (player.LetraAtual == null || ScrollLetras == null) return;

            if (player.LetraAtual.Sincronizada) {
                if (player.LinhaAtual == null) return;
                _mapaLinhas.TryGetValue(player.LinhaAtual, out var elementoLinha);

                if (elementoLinha != null) {
                    Point posicaoRelativa = elementoLinha.TransformToVisual(ScrollLetras).Transform(new Point(0, 0));
                    double offsetAlvo = ScrollLetras.VerticalOffset + posicaoRelativa.Y - ( ScrollLetras.ViewportHeight / 2 ) + ( elementoLinha.RenderSize.Height / 2 );
                    AnimarScrollSuave(ScrollLetras, Math.Max(0, offsetAlvo));
                }
                else {
                    int indice = player.LetraAtual.Linhas.IndexOf(player.LinhaAtual);
                    if (indice >= 0) {
                        double offsetAlvo = Math.Max(0, ( indice * 25 ) - ( ScrollLetras.ViewportHeight / 2 ));
                        AnimarScrollSuave(ScrollLetras, offsetAlvo);
                    }
                }
            }
            else {
                if (player.DuracaoTotalMs <= 0) return;

                double progresso = (double)player.PosicaoAtualMs / player.DuracaoTotalMs;
                double offsetMaximo = ScrollLetras.ExtentHeight - ScrollLetras.ViewportHeight - ItemsLetras.Margin.Bottom;

                if (offsetMaximo <= 0) return;

                double offsetAlvo = progresso * offsetMaximo;
                AnimarScrollSuave(ScrollLetras, offsetAlvo, 200, linear: true);
            }
        }

        private void AnimarScrollSuave(ScrollViewer scrollViewer, double offsetAlvo, int duracaoMs = 130, bool linear = false)
        {
            if (scrollViewer == null) return;

            _timerScroll?.Stop();

            double offsetInicial = scrollViewer.VerticalOffset;
            double diferenca = offsetAlvo - offsetInicial;
            if (Math.Abs(diferenca) < 1) return;

            var relogio = System.Diagnostics.Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timerScroll = timer;

            timer.Tick += (s, e) =>
            {
                double progresso = Math.Min(1, relogio.ElapsedMilliseconds / (double)duracaoMs);
                double suavizado = linear ? progresso : 1 - Math.Pow(1 - progresso, 3);

                scrollViewer.ScrollToVerticalOffset(offsetInicial + diferenca * suavizado);

                if (progresso >= 1) timer.Stop();
            };

            timer.Start();
        }

        private void LinhaLetra_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elemento && elemento.DataContext is LinhaLetra linha) {
                player.SaltarParaLinhaCommand.Execute(linha);
            }
        }

        /* ===== SLIDER EVENTS ===== */

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1) return;

            if (sender is Slider slider) {
                player.EstaArrastando = true;
                slider.CaptureMouse();

                bool clicouNoThumb = FindParent<Thumb>((DependencyObject)e.OriginalSource) != null;
                if (clicouNoThumb) return;

                AtualizarValorPorPosicao(slider, e.GetPosition(slider));
                e.Handled = true;
            }
        }

        private void Slider_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (sender is not Slider slider) return;
            if (!player.EstaArrastando) return;

            AtualizarValorPorPosicao(slider, e.GetPosition(slider));
        }

        private void Slider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider) {
                if (slider.IsMouseCaptured) {
                    slider.ReleaseMouseCapture();
                }
                player.DefinirPosicaoManual((long)player.PosicaoAtualMs);
                player.EstaArrastando = false;
            }
        }

        /* ===== HELPERS ===== */

        private static void AtualizarValorPorPosicao(Slider slider, Point posicao)
        {
            if (slider.ActualWidth <= 0) return;

            double proporcao = Math.Clamp(posicao.X / slider.ActualWidth, 0, 1);
            slider.Value = slider.Minimum + proporcao * ( slider.Maximum - slider.Minimum );
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
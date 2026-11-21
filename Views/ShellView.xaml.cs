using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Services;
using KyoneMusic.ViewModels;
using KyoneMusic.ViewModels.ShellPages;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IOPath = System.IO.Path;
using System.ComponentModel;

namespace KyoneMusic.Views
{
    public partial class ShellView : UserControl
    {
        private readonly PlayerViewModel player;
        private DispatcherTimer? _timerScroll;
        private readonly Dictionary<LinhaLetra, FrameworkElement> _mapaLinhas = new();
        private BibliotecaService biblioteca;
        private Point _pontoInicial;
        private DateTime _ultimaInteracaoUsuarioScroll = DateTime.MinValue;
        private bool _recriarAncoraScroll;
        private double _offsetBaseScroll;
        private double _progressoBaseScroll;
        private double _offsetAlvoScroll;
        private DateTime _ultimoTickScroll;
        private DateTime _ultimaAtualizacaoAlvoScroll = DateTime.MinValue;
        private static readonly TimeSpan IntervaloAtualizacaoAlvoScroll = TimeSpan.FromMilliseconds(33);

        public ShellView()
        {
            player = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            biblioteca = App.ServiceProvider.GetRequiredService<BibliotecaService>();

            InitializeComponent();

            Loaded += ShellView_Loaded;
            Unloaded += ShellView_Unloaded;
        }

        /* ===== LIFECYCLE ===== */

        private void ShellView_Loaded(object sender, RoutedEventArgs e)
        {
            player.PropertyChanged += Player_PropertyChanged;
            ItemsLetras.ItemContainerGenerator.StatusChanged += ItemsLetras_StatusChanged;
        }

        private void ShellView_Unloaded(object sender, RoutedEventArgs e)
        {
            _timerScroll?.Stop();
            _timerScroll = null;

            player.PropertyChanged -= Player_PropertyChanged;
            ItemsLetras.ItemContainerGenerator.StatusChanged -= ItemsLetras_StatusChanged;
        }

        private void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayerViewModel.LinhaAtual)) {
                AtualizarScrollLetra(player);
            }
            else if (e.PropertyName == nameof(PlayerViewModel.PosicaoAtualMs)) {
                if (player.LetraAtual?.Sincronizada == false)
                    AtualizarScrollLetra(player);
            }
        }

        /* ===== LETRAS EVENTS / SCROLL ===== */

        private void RegistrarInteracaoUsuarioScroll(object sender, RoutedEventArgs e)
        {
            _ultimaInteracaoUsuarioScroll = DateTime.UtcNow;
            _timerScroll?.Stop();
            _recriarAncoraScroll = true;
        }

        private void ScrollLetras_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            RegistrarInteracaoUsuarioScroll(sender, e);
        }

        private void ScrollLetras_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            RegistrarInteracaoUsuarioScroll(sender, e);
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
            if (DateTime.UtcNow - _ultimaInteracaoUsuarioScroll < TimeSpan.FromMilliseconds(800)) {
                return;
            }
            if (!player.LetraAtual.Sincronizada && player.ScrollAutomaticoAtivo) {
                DateTime agora = DateTime.UtcNow;

                if (agora - _ultimaAtualizacaoAlvoScroll < IntervaloAtualizacaoAlvoScroll)
                    return;

                _ultimaAtualizacaoAlvoScroll = agora;
            }

            if (player.LetraAtual.Sincronizada) {
                LinhaLetra? linha = player.LinhaAtual;
                if (linha == null) return;

                if (!_mapaLinhas.TryGetValue(linha, out FrameworkElement? elementoLinha) ||
                    elementoLinha == null) {
                    ItemsLetras.UpdateLayout();
                    elementoLinha = ItemsLetras.ItemContainerGenerator.ContainerFromItem(linha) as FrameworkElement;

                    if (elementoLinha == null) return;

                    _mapaLinhas[linha] = elementoLinha;
                }

                Point posicaoRelativa = elementoLinha.TransformToVisual(ScrollLetras).Transform(new Point(0, 0));
                double offsetAlvo = ScrollLetras.VerticalOffset
                                  + posicaoRelativa.Y
                                  - ( ScrollLetras.ViewportHeight / 2.5 )
                                  + ( elementoLinha.RenderSize.Height / 2 );

                AnimarScrollSuave(ScrollLetras, Math.Max(0, offsetAlvo));
            }
            else {
                if (player.DuracaoTotalMs <= 0) return;

                if (player.ScrollAutomaticoAtivo) {
                    double progresso = (double)player.PosicaoAtualMs / player.DuracaoTotalMs;
                    double alturaFonte = TextoFonteLetra.Visibility == Visibility.Visible ? TextoFonteLetra.ActualHeight
                                       + TextoFonteLetra.Margin.Top
                                       + TextoFonteLetra.Margin.Bottom
                                     : 0;

                    double offsetMaximo = ScrollLetras.ExtentHeight
                                        - ScrollLetras.ViewportHeight
                                        - ItemsLetras.Margin.Bottom
                                        - alturaFonte;

                    if (offsetMaximo <= 0) return;

                    if (_recriarAncoraScroll) {
                        _offsetBaseScroll = ScrollLetras.VerticalOffset;
                        _progressoBaseScroll = progresso;
                        _recriarAncoraScroll = false;
                    }

                    double offsetAlvo = _offsetBaseScroll + ( progresso - _progressoBaseScroll ) * offsetMaximo;

                    offsetAlvo = Math.Clamp(offsetAlvo, 0, offsetMaximo);
                    AnimarScrollSuave(ScrollLetras, offsetAlvo);
                }
            }
        }

        private void AnimarScrollSuave(ScrollViewer scrollViewer, double offsetAlvo)
        {
            _offsetAlvoScroll = Math.Clamp(offsetAlvo, 0, scrollViewer.ScrollableHeight);

            if (_timerScroll == null) {
                var timer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(33)
                };

                _timerScroll = timer;

                timer.Tick += (_, _) =>
                {
                    DateTime agora = DateTime.UtcNow;
                    double deltaTempo = ( agora - _ultimoTickScroll ).TotalSeconds;
                    _ultimoTickScroll = agora;

                    double atual = scrollViewer.VerticalOffset;
                    double fator = 1 - Math.Exp(-deltaTempo / 0.12);
                    double proximo = atual + ( _offsetAlvoScroll - atual ) * fator;

                    if (Math.Abs(_offsetAlvoScroll - proximo) < 0.5) {
                        scrollViewer.ScrollToVerticalOffset(_offsetAlvoScroll);
                        timer.Stop();
                        _timerScroll = null;
                        return;
                    }

                    scrollViewer.ScrollToVerticalOffset(proximo);
                };
            }

            if (!_timerScroll.IsEnabled) {
                _ultimoTickScroll = DateTime.UtcNow;
                _timerScroll.Start();
            }
        }

        private void LinhaLetra_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elemento && elemento.DataContext is LinhaLetra linha) {
                player.SaltarParaLinhaCommand.Execute(linha);
            }
        }

        private void EditarLetra_Click(object sender, RoutedEventArgs e)
        {
            Music? musicaAtual = player.MusicaAtual;
            if (musicaAtual == null) return;

            string? caminhoLrc = player.LetraAtual?.CaminhoOrigem;

            if (string.IsNullOrEmpty(caminhoLrc) || player.LetraAtual?.Fonte == FonteLetra.Embutida) {
                string pasta = IOPath.GetDirectoryName(musicaAtual.Caminho)!;
                string nomeBase = IOPath.GetFileNameWithoutExtension(musicaAtual.Caminho);
                caminhoLrc = IOPath.Combine(pasta, nomeBase + ".lrc");
            }

            try {
                string fullPath = IOPath.GetFullPath(caminhoLrc);

                string? dir = IOPath.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                //if (!File.Exists(fullPath)) {
                //    await File.WriteAllTextAsync(fullPath, string.Empty, System.Text.Encoding.UTF8);
                //}

                string textoInicial = File.Exists(fullPath) ? File.ReadAllText(fullPath) : player.LetraAtual?.ConteudoBruto ?? "";

                var vm = new EditarLetraViewModel(fullPath, textoInicial, player.LetraAtual?.Fonte ?? FonteLetra.Nenhuma, biblioteca);
                var janela = new EditarLetraWindow(vm) { Owner = Window.GetWindow(this) };
                if (janela.ShowDialog() == true) _ = player.CarregarLetraAtual();
            }
            catch (UnauthorizedAccessException ex) {
                Debug.WriteLine($"Permissão negada ao criar/abrir o ficheiro: {ex.Message}");
            }
            catch (IOException ex) {
                Debug.WriteLine($"Erro de I/O ao criar/abrir o ficheiro: {ex.Message}");
            }
            catch (Exception ex) {
                Debug.WriteLine($"Erro inesperado ao editar letra: {ex.Message}");
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

        /* ===== UTILS ===== */

        private static void AtualizarValorPorPosicao(Slider slider, Point posicao)
        {
            if (slider.ActualWidth <= 0) return;

            double proporcao = Math.Clamp(posicao.X / slider.ActualWidth, 0, 1);
            double novoValor = slider.Minimum + proporcao * ( slider.Maximum - slider.Minimum );
            slider.Value = novoValor;
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private void AbrirMiniPlayer_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow;
            var miniPlayer = new MiniPlayerWindow(mainWindow);
            miniPlayer.Show();
            mainWindow.Hide();
        }

        private void Lista_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _pontoInicial = e.GetPosition(null);
        }

        private void Lista_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            Point pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _pontoInicial.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _pontoInicial.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            ListBoxItem? item = AcharItem(e.OriginalSource as DependencyObject);
            if (item == null) return;

            int indice = Lista.ItemContainerGenerator.IndexFromContainer(item);
            if (indice < 0) return;
            DragDrop.DoDragDrop(Lista, indice, DragDropEffects.Move);
        }

        private void Lista_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(int))) return;
            int de = (int)e.Data.GetData(typeof(int))!;

            ListBoxItem? item = AcharItem(e.OriginalSource as DependencyObject);
            if (item == null) return;

            int para = Lista.ItemContainerGenerator.IndexFromContainer(item);
            player.MoverNaFila(de, para);
        }

        private static ListBoxItem? AcharItem(DependencyObject? atual)
        {
            while (atual != null && atual is not ListBoxItem)
                atual = VisualTreeHelper.GetParent(atual);
            return atual as ListBoxItem;
        }

        private void Music_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elemento && elemento.DataContext is Music musica) {
                if (DataContext is ShellViewModel viewModel) {
                    viewModel.Player.TocarMusica(musica);
                }
            }
        }

        private void TextoFonteLetra_Click(object sender, MouseButtonEventArgs e)
        {
            if (player.LetraEditada || player.FonteUrl is null) return;

            try {
                Process.Start(new ProcessStartInfo(player.FonteUrl) { UseShellExecute = true });
            }
            catch (Exception ex) {
                Debug.WriteLine($"ERRO ao abrir link da letra: {ex.Message}");
            }
        }
    }
}
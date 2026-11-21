using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Services;
using KyoneMusic.ViewModels;
using KyoneMusic.ViewModels.ShellPages;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;

namespace KyoneMusic
{
    public partial class App : Application
    {
        [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int SetCurrentProcessExplicitAppUserModelID(string AppID);

        [LibraryImport("kernel32.dll")]
        private static partial ushort GetUserDefaultUILanguage();

        public event Action? TemaAlterado;

        public event Action? IdiomaAlterado;

        private static readonly string[] IdiomasSuportados = ["pt", "pt-br", "en", "ja", "es"];

        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            File.AppendAllText(
        Path.Combine(Path.GetTempPath(), "ProjectKitsune-args.txt"),
        $"{DateTime.Now}: {string.Join(" | ", e.Args)}{Environment.NewLine}");
            string? ficheiroInicial = e.Args.FirstOrDefault(File.Exists);

            if (!InstanciaUnica.EPrimeira()) {
                InstanciaUnica.EnviarParaPrimeira(ficheiroInicial);
                Shutdown();
                return;
            }

            _ = SetCurrentProcessExplicitAppUserModelID("ProjectKitsune");
            RenderOptions.ProcessRenderMode = RenderMode.Default;
            var services = new ServiceCollection();
            services.AddSingleton<ConfiguracaoService>();
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<BibliotecaService>();
            services.AddSingleton<AudioPlayerService>();
            services.AddSingleton<PlayerViewModel>();
            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<OrdenacaoService>();
            services.AddSingleton<SmtcService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<GruposService>();
            ServiceProvider = services.BuildServiceProvider();

            var mainWindow = new Views.MainWindow
            {
                DataContext = ServiceProvider.GetRequiredService<MainViewModel>()
            };
            mainWindow.Show();

            var player = ServiceProvider.GetRequiredService<PlayerViewModel>();
            if (ficheiroInicial != null) player.TocarForaDaLista(ficheiroInicial);
            InstanciaUnica.Escutar(caminho => Dispatcher.InvokeAsync(() =>
            {
                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                if (!string.IsNullOrEmpty(caminho)) player.TocarForaDaLista(caminho);
            }));
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try {
                if (ServiceProvider != null) {
                    var bibliotecaService = ServiceProvider.GetRequiredService<BibliotecaService>();
                    bibliotecaService.PararMonitorizacao();
                    bibliotecaService.Dispose();
                    var player = ServiceProvider.GetRequiredService<PlayerViewModel>();
                    player.Dispose();
                    var audioPlayerService = ServiceProvider.GetRequiredService<AudioPlayerService>();
                    audioPlayerService.Dispose();
                    ServiceProvider.Dispose();
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Erro ao encerrar a aplicação: {ex}");
            }
            finally {
                base.OnExit(e);
            }
        }

        public void AplicarTema(string nomeTema)
        {
            var md = Resources.MergedDictionaries;
            var temaAntigo = md.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Resources/Themes/Colors_") == true);
            if (temaAntigo != null) md.Remove(temaAntigo);
            md.Add(new ResourceDictionary { Source = new Uri($"/Resources/Themes/Colors_{nomeTema}.xaml", UriKind.Relative) });

            TemaAlterado?.Invoke();
        }

        public static string DetectarIdiomaSistema()
        {
            ushort languageId = GetUserDefaultUILanguage();

            if (languageId == 0)
                return "en";

            CultureInfo cultura;

            try {
                cultura = CultureInfo.GetCultureInfo((int)languageId);
            }
            catch (CultureNotFoundException) {
                return "en";
            }

            string completo = cultura.Name.ToLowerInvariant();
            if (IdiomasSuportados.Contains(completo))
                return completo;

            string curto = cultura.TwoLetterISOLanguageName.ToLowerInvariant();
            return IdiomasSuportados.Contains(curto) ? curto : "en";
        }

        public void AplicarIdioma(string codigoIdioma)
        {
            string caminho = $"/Resources/Languages/Strings.{codigoIdioma}.xaml";
            ResourceDictionary novoIdioma = new ResourceDictionary() { Source = new Uri(caminho, UriKind.Relative) };

            var idiomaAntigo = Resources.MergedDictionaries.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("/Resources/Languages/Strings.") == true);

            if (idiomaAntigo != null) Resources.MergedDictionaries.Remove(idiomaAntigo);
            Resources.MergedDictionaries.Add(novoIdioma);

            IdiomaAlterado?.Invoke();
        }
    }
}
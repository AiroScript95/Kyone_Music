using KyoneMusic.Models;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace KyoneMusic.Services
{
    public class ConfiguracaoService
    {
        private readonly object _configuracaoLock = new();
        private static string caminhoData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static string caminhoPasta = Path.Combine(caminhoData, "Kitsune");
        private static string caminhoPastaConfig = Path.Combine(caminhoPasta, "Settings");
        private static string caminhoArquivo = Path.Combine(caminhoPastaConfig, "settings.json");

        public Configuracao CarregarConfiguracao()
        {
            if (!File.Exists(caminhoArquivo)) {
                Configuracao novaConfig = new Configuracao();
                novaConfig.RotaMusica.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
                return novaConfig;
            }

            try {
                string TextJson = File.ReadAllText(caminhoArquivo);
                return JsonSerializer.Deserialize<Configuracao>(TextJson) ?? new Configuracao();
            }
            catch (JsonException) {
                Configuracao novaConfig = new Configuracao();
                novaConfig.RotaMusica.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
                return novaConfig;
            }
        }

        public void AtualizarConfiguracao(Action<Configuracao> alteracao)
        {
            ArgumentNullException.ThrowIfNull(alteracao);

            lock (_configuracaoLock) {
                Configuracao configuracao = CarregarConfiguracao();
                alteracao(configuracao);
                GuardarConfiguracao(configuracao);
            }
        }

        public void GuardarConfiguracao(Configuracao config)
        {
            ArgumentNullException.ThrowIfNull(config);

            lock (_configuracaoLock) {
                try {
                    string textJson = JsonSerializer.Serialize(config);
                    Directory.CreateDirectory(caminhoPastaConfig);

                    string caminhoTemp = caminhoArquivo + ".tmp";
                    File.WriteAllText(caminhoTemp, textJson);

                    if (File.Exists(caminhoArquivo))
                        File.Replace(caminhoTemp, caminhoArquivo, null);
                    else
                        File.Move(caminhoTemp, caminhoArquivo);
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Erro ao guardar configuração: {ex}");
                }
            }
        }

        public void GuardarBackground(string caminho)
        {
            try {
                HashSet<string> extensoesValidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".png",".jpg",".jpeg",
                };

                string extensao = Path.GetExtension(caminho);
                if (!extensoesValidas.Contains(extensao)) return;
                string caminhoPastaAssets = Path.Combine(caminhoPasta, "Assets");
                Directory.CreateDirectory(caminhoPastaAssets);

                foreach (string arquivoAntigo in Directory.EnumerateFiles(caminhoPastaAssets, "background.*")) {
                    File.Delete(arquivoAntigo);
                }

                string caminhoDestino = Path.Combine(caminhoPastaAssets, "background" + extensao);
                File.Copy(caminho, caminhoDestino, overwrite: true);

                AtualizarConfiguracao(config =>
                {
                    config.Background = caminhoDestino;
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"ERRO: {ex.Message}");
            }
        }

        public void RemoverBackground()
        {
            try {
                Configuracao config = CarregarConfiguracao();

                if (!string.IsNullOrWhiteSpace(config.Background) && File.Exists(config.Background)) {
                    File.Delete(config.Background);
                }
                AtualizarConfiguracao(config =>
                {
                    config.Background = string.Empty;
                });
            }
            catch (Exception ex) {
                Debug.WriteLine($"ERRO: {ex.Message}");
            }
        }

        public bool AdicionarPastaMusica(string caminho)
        {
            Configuracao config = CarregarConfiguracao();

            if (config.RotaMusica.Count >= 5) return false;

            if (config.RotaMusica.Contains(caminho)) return false;

            if (!Directory.Exists(caminho)) return false;

            AtualizarConfiguracao(config =>
            {
                config.RotaMusica.Add(caminho);
            });
            return true;
        }

        public void RemoverPastaMusica(string caminho)
        {
            AtualizarConfiguracao(config =>
            {
                config.RotaMusica.Remove(caminho);
            });
        }

        public void AtualizarTema(string nomeTema)
        {
            AtualizarConfiguracao(config =>
            {
                config.Theme = nomeTema;
            });
        }

        public void AtualizarIdiona(string nomeIdiona)
        {
            AtualizarConfiguracao(config =>
            {
                config.Idioma = nomeIdiona;
            });
        }
    }
}
using System.IO;
using System.Text.Json;

namespace KyoneMusic.Models;

public class OrigemLetra
{
    public string Fonte { get; set; } = string.Empty;
    public long TicksLrc { get; set; }
}

public class OrigemLetraStore
{
    private static string caminhoData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string caminhoPasta = Path.Combine(caminhoData, "Kitsune");
    private static string caminhoPastaConfig = Path.Combine(caminhoPasta, "Settings");
    private readonly string _caminhoJson = Path.Combine(caminhoPastaConfig, "letras.json");
    private readonly object _lock = new();
    private Dictionary<string, OrigemLetra> _dados = new(StringComparer.OrdinalIgnoreCase);

    public OrigemLetraStore()
    {
        Carregar();
    }

    private static string Chave(string caminhoLrc) => Path.GetFileNameWithoutExtension(caminhoLrc);

    private void Carregar()
    {
        try {
            if (!File.Exists(_caminhoJson)) return;
            var lido = JsonSerializer.Deserialize<Dictionary<string, OrigemLetra>>(File.ReadAllText(_caminhoJson));
            if (lido != null)
                _dados = new Dictionary<string, OrigemLetra>(lido, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) {
            _dados = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Guardar()
    {
        string tmp = _caminhoJson + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_dados, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(_caminhoJson)) File.Replace(tmp, _caminhoJson, null);
        else File.Move(tmp, _caminhoJson);
    }

    public void Registar(string caminhoLrc, FonteLetra fonte)
    {
        lock (_lock) {
            _dados[Chave(caminhoLrc)] = new OrigemLetra
            {
                Fonte = fonte.ToString(),
                TicksLrc = new FileInfo(caminhoLrc).LastWriteTimeUtc.Ticks
            };
            Guardar();
        }
    }

    public (FonteLetra Fonte, bool Editada) Obter(string caminhoLrc)
    {
        lock (_lock) {
            if (!_dados.TryGetValue(Chave(caminhoLrc), out var entrada) || !File.Exists(caminhoLrc))
                return (FonteLetra.ArquivoLrc, false);

            if (!Enum.TryParse<FonteLetra>(entrada.Fonte, out var fonte))
                return (FonteLetra.ArquivoLrc, false);

            bool editada = new FileInfo(caminhoLrc).LastWriteTimeUtc.Ticks != entrada.TicksLrc;
            return (fonte, editada);
        }
    }

    public void Limpar(HashSet<string> nomesComLrc)
    {
        lock (_lock) {
            var remover = _dados.Keys.Where(k => !nomesComLrc.Contains(k)).ToList();
            if (remover.Count == 0) return;
            foreach (var k in remover) _dados.Remove(k);
            Guardar();
        }
    }
}
using System.Reflection;

namespace KyoneMusic.Models;

public static class InfoApp
{
    public static string Versao { get; } = ObterVersao();
    public static string Copyright { get; } = ObterCopyright();

    private static string ObterVersao()
    {
        var versao = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return versao?.Split('+')[0] ?? "";
    }

    private static string ObterCopyright()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?
            .Copyright ?? "";
    }
}
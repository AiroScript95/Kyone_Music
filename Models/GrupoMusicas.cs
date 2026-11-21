using CommunityToolkit.Mvvm.Messaging;
using KyoneMusic.Services;
using KyoneMusic.Helpers;
using KyoneMusic.ViewModels;

namespace KyoneMusic.Models
{
    public class GrupoMusicas
    {
        public string Nome { get; set; } = "";
        public byte[]? Capa { get; set; }
        public List<string> ListaMusicas { get; set; } = new();
        public List<Music> Musicas { get; set; } = new();
        public string NumMusicas => TextoHelper.Texto("Str_NumMusicas", Musicas.Count.ToString());
    }
}
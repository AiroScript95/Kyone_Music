using CommunityToolkit.Mvvm.Messaging;
using KyoneMusic.Models;
using KyoneMusic.Helpers;
using KyoneMusic.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoneMusic.Services
{
    public class GruposService
    {
        private const string AlbumDesconhecido = "Unknown Album";
        private readonly BibliotecaService _biblioteca;
        private readonly object _lock = new();
        private List<GrupoMusicas>? _albuns;
        private List<GrupoMusicas>? _artistas;

        public GruposService(BibliotecaService biblioteca)
        {
            _biblioteca = biblioteca;
            WeakReferenceMessenger.Default.Register<BibliotecaAlteradaMessage>(this, (r, m) => ( (GruposService)r ).Invalidar());
        }

        public Task<List<GrupoMusicas>> ObterAlbunsAsync()
        {
            lock (_lock) { if (_albuns != null) return Task.FromResult(_albuns); }

            List<Music> copia = _biblioteca.Musicas.ToList();
            return Task.Run(() =>
            {
                List<GrupoMusicas> resultado = CalcularAlbuns(copia);
                if (copia.Count > 0) lock (_lock) _albuns = resultado;
                return resultado;
            });
        }

        public Task<List<GrupoMusicas>> ObterArtistasAsync()
        {
            lock (_lock) { if (_artistas != null) return Task.FromResult(_artistas); }
            List<Music> copia = _biblioteca.Musicas.ToList();
            return Task.Run(() =>
            {
                List<GrupoMusicas> resultado = CalcularArtistas(copia);
                if (copia.Count > 0) lock (_lock) _artistas = resultado;
                return resultado;
            });
        }

        public void Invalidar()
        { lock (_lock) { _albuns = null; _artistas = null; } }

        private static List<GrupoMusicas> CalcularAlbuns(List<Music> musicas) => musicas
            .GroupBy(m => m.Album == AlbumDesconhecido ? AlbumDesconhecido : $"{m.Album}|{m.Artista}",
                     StringComparer.OrdinalIgnoreCase)
            .Select(g => Criar(musicas: g.ToList(), nome: g.First().Album ?? ""))
            .OrderBy(g => g.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        private static List<GrupoMusicas> CalcularArtistas(List<Music> musicas) => musicas
            .GroupBy(m => m.Artista ?? "", StringComparer.OrdinalIgnoreCase)
            .Select(g => Criar(musicas: g.ToList(), nome: g.Key))
            .OrderBy(g => g.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        private static GrupoMusicas Criar(List<Music> musicas, string nome) => new()
        {
            Nome = TextoHelper.NormalizarTexto(nome),
            Capa = musicas.Select(m => m.Image).FirstOrDefault(i => i != null),
            ListaMusicas = musicas.Take(3).Select(m => m.Titulo).ToList(),
            Musicas = musicas
        };
    }
}
using KyoneMusic.Models;
using KyoneMusic.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoneMusic.ViewModels
{
    public class InfoViewModel
    {
        public string Titulo { get; set; } = string.Empty;
        public string Artista { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public DateTime DataAdicao { get; set; }
        public TimeSpan Duracao { get; set; }
        public string Caminho { get; set; } = string.Empty;
        public byte[] Image { get; set; } = Array.Empty<byte>();
        private readonly BibliotecaService BibliotecaService;

        public InfoViewModel(string caminho, BibliotecaService biblioteca)
        {
            BibliotecaService = biblioteca;
            Carregar(caminho);
        }

        private void Carregar(string caminho)
        {
            Music? music = BibliotecaService.LerMusicaPorCaminho(caminho);
            if (music != null) {
                Titulo = music?.Titulo ?? "";
                Artista = music?.Artista ?? "";
                Album = music?.Album ?? "";
                DataAdicao = music?.DataAdicionado ?? new();
                Duracao = music?.DuracaoArredondada ?? new();
                Caminho = music?.Caminho ?? "";
                Image = music?.Image ?? Array.Empty<byte>();
            }
        }
    }
}
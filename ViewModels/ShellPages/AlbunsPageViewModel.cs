using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace KyoneMusic.ViewModels.ShellPages
{
    public partial class AlbunsPageViewModel : ObservableObject
    {
        [ObservableProperty] private List<GrupoMusicas> _albuns = new();

        private readonly BibliotecaService biblioteca;

        private GrupoMusicas? _GrupoParaScroll;

        public GrupoMusicas? GrupoParaScroll {
            get => _GrupoParaScroll;
            set {
                _GrupoParaScroll = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Alfabeto { get; } = new();

        private readonly ShellViewModel _shell;

        public AlbunsPageViewModel(ShellViewModel shell)
        {
            _shell = shell;
            biblioteca = App.ServiceProvider.GetRequiredService<BibliotecaService>();

            if (_shell.EstaCarregandoMusicas)
                _shell.PropertyChanged += Shell_PropertyChanged;
            else
                _ = CarregarAsync();
        }

        private void Shell_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ShellViewModel.EstaCarregandoMusicas) ||
                _shell.EstaCarregandoMusicas)
                return;

            _shell.PropertyChanged -= Shell_PropertyChanged;
            _ = CarregarAsync();
        }

        private async Task CarregarAsync()
        {
            if (biblioteca.Musicas.Count == 0) {
                AtualizarAlfabeto();
                return;
            }
            var grupos = App.ServiceProvider.GetRequiredService<GruposService>();
            Albuns = await grupos.ObterAlbunsAsync();

            AtualizarAlfabeto();
        }

        private char NormalizarPrimeiraLetra(string? titulo)
        {
            if (string.IsNullOrEmpty(titulo)) return '\0';

            string primeiroElemento = StringInfo.GetNextTextElement(titulo);

            string decomposto;
            try {
                decomposto = primeiroElemento.Normalize(NormalizationForm.FormD);
            }
            catch (ArgumentException) {
                return char.ToUpperInvariant(titulo[0]);
            }

            foreach (char c in decomposto) {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria != UnicodeCategory.NonSpacingMark)
                    return char.ToUpperInvariant(c);
            }

            return char.ToUpperInvariant(titulo[0]);
        }

        private void AtualizarAlfabeto()
        {
            Alfabeto.Clear();
            foreach (var letra in SelecionarLetras())
                Alfabeto.Add(letra);
        }

        private List<string> SelecionarLetras()
        {
            var primeiras = Albuns
                .Select(m => NormalizarPrimeiraLetra(m.Nome))
                .ToList();

            bool temNumeros = primeiras.Any(c => c >= '0' && c <= '9');
            bool temSimbolos = primeiras.Any(c => c == '&');

            var letras = Enumerable.Range('A', 26)
                .Select(c => (char)c)
                .Where(letra => primeiras.Contains(letra));

            var extras = primeiras
                .Where(c => c != '\0' && c != '&' && c != '#'
                            && !( c >= '0' && c <= '9' )
                            && !( c >= 'A' && c <= 'Z' ))
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .Take(2)
                .Select(g => g.Key)
                .OrderBy(c => c);

            var resultado = new List<string>();
            if (temSimbolos) resultado.Add("&");
            if (temNumeros) resultado.Add("#");
            resultado.AddRange(letras.Select(l => l.ToString()));
            resultado.AddRange(extras.Select(e => e.ToString()));

            return resultado;
        }

        [RelayCommand]
        private void SaltarParaLetra(string letra)
        {
            GrupoMusicas? alvo;

            if (letra == "#") {
                alvo = Albuns.FirstOrDefault(m =>
                    !string.IsNullOrEmpty(m.Nome) && char.IsAsciiDigit(NormalizarPrimeiraLetra(m.Nome)));
            }
            else if (letra == "&") {
                alvo = Albuns.FirstOrDefault(m =>
                    string.IsNullOrEmpty(m.Nome) || ( !char.IsAsciiLetter(NormalizarPrimeiraLetra(m.Nome))
                    && !char.IsAsciiDigit(NormalizarPrimeiraLetra(m.Nome)) ));
            }
            else {
                char letraAlvo = char.ToUpperInvariant(letra[0]);
                alvo = Albuns.FirstOrDefault(m =>
                    !string.IsNullOrEmpty(m.Nome) && NormalizarPrimeiraLetra(m.Nome) == letraAlvo);
            }
            GrupoParaScroll = null;
            if (alvo != null) GrupoParaScroll = alvo;
        }

        [RelayCommand]
        private void AbrirAlbum(GrupoMusicas grupo)
        {
            GrupoDetailPageViewModel detalhe = new(_shell, () => new AlbunsPageViewModel(_shell), false);
            detalhe.Carregar(grupo);
            _shell.CurrentPage = detalhe;
        }
    }
}
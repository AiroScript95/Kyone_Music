using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using KyoneMusic.Models;
using KyoneMusic.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace KyoneMusic.ViewModels.ShellPages
{
    public partial class MusicListPageViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Music> Musicas => bibliotecaService.Musicas;
        protected BibliotecaService bibliotecaService;
        protected ConfiguracaoService configuracaoService;
        public PlayerViewModel PlayerViewModel { get; set; }
        protected DatabaseService databaseService;
        protected OrdenacaoService ordenacaoService;

        public ShellViewModel Shell { get; set; }

        public string FormatoLista {
            get => Shell.FormatoLista;
            set {
                Shell.FormatoLista = value;
                OnPropertyChanged();
            }
        }

        public bool EstaCarregandoMusicas => Shell.EstaCarregandoMusicas;

        private string _ordemAtual = string.Empty;

        public string OrdemAtual {
            get => _ordemAtual;
            set {
                _ordemAtual = value;
                OnPropertyChanged();
            }
        }

        private bool _ordemDescendente;

        public bool OrdemDescendente {
            get => _ordemDescendente;
            set {
                _ordemDescendente = value;
                OnPropertyChanged();
            }
        }

        private Music? _musicaParaScroll;

        public Music? MusicaParaScroll {
            get => _musicaParaScroll;
            set {
                _musicaParaScroll = value;
                OnPropertyChanged();
            }
        }

        private bool _mostrarOrdenacao;

        public bool MostrarOrdenacao {
            get => _mostrarOrdenacao;
            set {
                _mostrarOrdenacao = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Alfabeto { get; } = new();

        public MusicListPageViewModel(ShellViewModel shell)
        {
            Shell = shell;
            Shell.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.FormatoLista))
                    OnPropertyChanged(nameof(FormatoLista));
                else if (e.PropertyName == nameof(ShellViewModel.EstaCarregandoMusicas)) {
                    OnPropertyChanged(nameof(EstaCarregandoMusicas));
                    if (!Shell.EstaCarregandoMusicas)
                        AtualizarAlfabeto();
                }
            };

            bibliotecaService = App.ServiceProvider.GetRequiredService<BibliotecaService>();
            configuracaoService = App.ServiceProvider.GetRequiredService<ConfiguracaoService>();
            PlayerViewModel = App.ServiceProvider.GetRequiredService<PlayerViewModel>();
            databaseService = App.ServiceProvider.GetRequiredService<DatabaseService>();
            ordenacaoService = App.ServiceProvider.GetRequiredService<OrdenacaoService>();
            Configuracao config = configuracaoService.CarregarConfiguracao();
            OrdemAtual = config.OrdemMusicList;
            OrdemDescendente = config.OrdemDescendente;
            Musicas.CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Move) return;
                if (Shell.EstaCarregandoMusicas) return;
                AtualizarAlfabeto();
            };

            AtualizarAlfabeto();
            OnPropertyChanged(nameof(OrdemAtual));
        }

        public void TocarMusica(Music musica)
        {
            PlayerViewModel.TocarMusica(musica, Musicas.ToList());
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

        public void MudarOrdenacao(string criterio, bool ordem)
        {
            Configuracao config = configuracaoService.CarregarConfiguracao();
            config.OrdemMusicList = criterio;
            config.OrdemDescendente = ordem;
            OrdemAtual = criterio;
            OrdemDescendente = ordem;
            OnPropertyChanged(nameof(OrdemAtual));
            configuracaoService.GuardarConfiguracao(config);

            List<Music> ordenadas = ordenacaoService.Ordenar(Musicas.ToList(), criterio, ordem);

            for (int novoIndice = 0; novoIndice < ordenadas.Count; novoIndice++) {
                Music musica = ordenadas[novoIndice];
                int indiceAtual = Musicas.IndexOf(musica);
                if (indiceAtual != novoIndice)
                    Musicas.Move(indiceAtual, novoIndice);
            }
        }

        private List<string> SelecionarLetras()
        {
            var primeiras = Musicas
                .Select(m => NormalizarPrimeiraLetra(m.Titulo))
                .ToList();

            bool temNumeros = primeiras.Any(c => c >= '0' && c <= '9');
            bool temSimbolos = primeiras.Any(c => c == '&');

            var letras = Enumerable.Range('A', 26)
                .Select(c => (char)c)
                .Where(letra => primeiras.Contains(letra));

            var extras = primeiras
                .Where(c => c != '\0' && c != '&' && c != '#' && !( c >= '0' && c <= '9' ) && !( c >= 'A' && c <= 'Z' ))
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
            Music? alvo;

            if (letra == "#") {
                alvo = Musicas.FirstOrDefault(m =>
                    !string.IsNullOrEmpty(m.Titulo) && char.IsAsciiDigit(NormalizarPrimeiraLetra(m.Titulo)));
            }
            else if (letra == "&") {
                alvo = Musicas.FirstOrDefault(m =>
                    string.IsNullOrEmpty(m.Titulo) || ( !char.IsAsciiLetter(NormalizarPrimeiraLetra(m.Titulo))
                    && !char.IsAsciiDigit(NormalizarPrimeiraLetra(m.Titulo)) ));
            }
            else {
                char letraAlvo = char.ToUpperInvariant(letra[0]);
                alvo = Musicas.FirstOrDefault(m =>
                    !string.IsNullOrEmpty(m.Titulo) && NormalizarPrimeiraLetra(m.Titulo) == letraAlvo);
            }
            if (alvo != null) MusicaParaScroll = alvo;
        }

        [RelayCommand]
        private void MostrarPainelOrdenacao()
        {
            MostrarOrdenacao = !MostrarOrdenacao;
        }

        [RelayCommand]
        private void FecharPainelOrdenacao()
        {
            MostrarOrdenacao = false;
        }

        [RelayCommand]
        private void IrParaConfiguracoes()
        {
            Shell.Page = "Config";
            Shell.CurrentPage = new ConfiguracoesPageViewModel(Shell);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KyoneMusic.Models
{
    public class Playlist : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public const int IdComLetra = -2;

        private string name = string.Empty;

        public string Name {
            get => name;
            set {
                name = value;
                OnPropertyChanged();
            }
        }

        public DateTime DataCriacao { get; set; }
        public List<Music> Musics { get; set; } = new List<Music>();
        public string Ordem { get; set; } = "Manual";
        public bool Direcao { get; set; } = false;
        public string NumMusicas { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
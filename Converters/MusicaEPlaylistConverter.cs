using KyoneMusic.Models;
using System.Globalization;
using System.Windows.Data;

namespace KyoneMusic.Converters
{
    public class MusicaEPlaylistConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is Music musica && values[1] is Playlist playlist)
            {
                return (musica, playlist);
            }
            return null!;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
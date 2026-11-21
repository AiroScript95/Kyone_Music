using KyoneMusic.Models;
using System.Globalization;
using System.Windows.Data;

namespace KyoneMusic.Converters
{
    public class IndiceParaNumeroConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is Music musica && values[1] is IList<Music> lista)
            {
                int indice = lista.IndexOf(musica);
                return indice >= 0 ? $"#{indice + 1}" : "";
            }
            return "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
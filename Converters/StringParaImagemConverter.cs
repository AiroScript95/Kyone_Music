using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace KyoneMusic.Converters
{
    public class StringParaImagemConverter : IValueConverter
    {
        private const int LarguraPadrao = 1920;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            int largura = int.TryParse(parameter?.ToString(), out int l) ? l : LarguraPadrao;

            if (value is string caminho && !string.IsNullOrWhiteSpace(caminho) && File.Exists(caminho))
            {
                BitmapImage imagem = new BitmapImage();
                imagem.BeginInit();
                imagem.CacheOption = BitmapCacheOption.OnLoad;
                imagem.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                imagem.UriSource = new Uri(caminho);
                imagem.DecodePixelWidth = largura;
                imagem.EndInit();
                imagem.Freeze();
                return imagem;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
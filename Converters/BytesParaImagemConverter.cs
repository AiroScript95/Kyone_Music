using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace KyoneMusic.Converters
{
    public class BytesParaImagemConverter : IValueConverter
    {
        private const int LarguraPadrao = 600;
        private const int LarguraMinima = 32;
        private const int LarguraMaxima = 1200;

        private static readonly ConditionalWeakTable<byte[], ConcurrentDictionary<int, WeakReference<BitmapImage>>> Cache = new();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not byte[] bytes || bytes.Length == 0)
                return null;

            int largura = int.TryParse(parameter?.ToString(), out int valor) ? valor : LarguraPadrao;

            largura = Math.Clamp(
                largura,
                LarguraMinima,
                LarguraMaxima);

            ConcurrentDictionary<int, WeakReference<BitmapImage>> imagens =
                Cache.GetValue(
                    bytes,
                    _ => new ConcurrentDictionary<
                        int,
                        WeakReference<BitmapImage>>());

            if (imagens.TryGetValue(
                    largura,
                    out WeakReference<BitmapImage>? referencia) &&
                referencia.TryGetTarget(out BitmapImage? imagemExistente)) {
                return imagemExistente;
            }

            BitmapImage imagem = CriarImagem(bytes, largura);

            imagens[largura] =
                new WeakReference<BitmapImage>(imagem);

            return imagem;
        }

        private static BitmapImage CriarImagem(
            byte[] bytes,
            int largura)
        {
            using MemoryStream stream = new(bytes);

            BitmapImage imagem = new();

            imagem.BeginInit();
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.DecodePixelWidth = largura;
            imagem.StreamSource = stream;
            imagem.EndInit();
            imagem.Freeze();

            return imagem;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
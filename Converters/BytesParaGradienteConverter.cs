using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KyoneMusic.Converters
{
    public class BytesParaGradienteConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not byte[] capa || capa.Length == 0) return null;

            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(capa)) {
                bmp.BeginInit();
                bmp.DecodePixelWidth = 16;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }

            var conv = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            int largura = conv.PixelWidth, altura = conv.PixelHeight;
            if (altura < 2) return null;

            int stride = largura * 4;
            byte[] px = new byte[stride * altura];
            conv.CopyPixels(px, stride, 0);

            long[] cima = new long[3], baixo = new long[3];
            int nCima = 0, nBaixo = 0;
            int metade = altura / 2;

            for (int y = 0; y < altura; y++) {
                for (int x = 0; x < largura; x++) {
                    int i = y * stride + x * 4;
                    long[] soma = y < metade ? cima : baixo;
                    soma[0] += px[i + 2];
                    soma[1] += px[i + 1];
                    soma[2] += px[i];
                    if (y < metade) nCima++; else nBaixo++;
                }
            }

            var c1 = Color.FromRgb((byte)( cima[0] / nCima ), (byte)( cima[1] / nCima ), (byte)( cima[2] / nCima ));
            var c2 = Color.FromRgb((byte)( baixo[0] / nBaixo ), (byte)( baixo[1] / nBaixo ), (byte)( baixo[2] / nBaixo ));

            var brush = new LinearGradientBrush(c1, c2, new Point(0, 0), new Point(1, 1));
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
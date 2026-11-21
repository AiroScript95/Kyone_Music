using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace KyoneMusic.Converters
{
    public class StringToUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s))
                return string.Empty;

            if (s.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) ||
                Uri.TryCreate(s, UriKind.Absolute, out _)) {
                return new Uri(s, UriKind.RelativeOrAbsolute);
            }

            var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? Assembly.GetExecutingAssembly().GetName().Name;
            var normalized = s.StartsWith("/") ? s : "/" + s;
            var pack = $"pack://application:,,,/{assemblyName};component{normalized}";
            return new Uri(pack, UriKind.Absolute);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
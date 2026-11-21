using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace KyoneMusic.Helpers
{
    public class TextoHelper
    {
        public static string Texto(string chave, string termo)
        {
            return string.Format((string)Application.Current.FindResource(chave), termo);
        }

        public static string NormalizarTexto(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            return texto.Trim('\u200B', '\uFEFF').Normalize(NormalizationForm.FormC);
        }
    }
}
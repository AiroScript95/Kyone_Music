using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyoneMusic.Views.Controls
{
    public partial class BarrasEqualizador : UserControl
    {
        public static readonly DependencyProperty TocandoProperty =
            DependencyProperty.Register(nameof(Tocando), typeof(bool), typeof(BarrasEqualizador),
                new PropertyMetadata(false, (d, _) => ( (BarrasEqualizador)d ).Atualizar()));

        public bool Tocando {
            get => (bool)GetValue(TocandoProperty);
            set => SetValue(TocandoProperty, value);
        }

        private readonly ScaleTransform[] _escalas;

        // de, ate, duração (s), atraso (s)
        private static readonly (double De, double Ate, double Dur, double Atraso)[] Cfg =
        {
            (0.25, 0.90, 0.45, 0.00),
            (0.30, 1.00, 0.60, 0.15),
            (0.20, 0.80, 0.50, 0.30),
            (0.30, 1.00, 0.65, 0.10),
            (0.25, 0.85, 0.55, 0.25),
        };

        public BarrasEqualizador()
        {
            InitializeComponent();
            _escalas = new[] { Escala1, Escala2, Escala3, Escala4, Escala5 };
            IsVisibleChanged += (_, _) => Atualizar();
        }

        private void Atualizar()
        {
            if (_escalas == null) return;
            bool correr = Tocando && IsVisible;

            for (int i = 0; i < _escalas.Length; i++) {
                if (correr) {
                    var c = Cfg[i];
                    _escalas[i].BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(c.De, c.Ate, TimeSpan.FromSeconds(c.Dur))
                        {
                            BeginTime = TimeSpan.FromSeconds(c.Atraso),
                            AutoReverse = true,
                            RepeatBehavior = RepeatBehavior.Forever
                        });
                }
                else {
                    _escalas[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);
                }
            }
        }
    }
}
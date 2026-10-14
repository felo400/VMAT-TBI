using System.Windows.Media;

namespace AutoPlannerHelpers.UIHelpers
{
    /// <summary>
    /// Colores de la interfaz (tema llamativo). Brushes congelados: se pueden usar desde cualquier hilo.
    /// </summary>
    public static class ThemeBrushes
    {
        public static readonly SolidColorBrush Neutral = Make(0xFF, 0xE0, 0x82); //ámbar claro: pestaña sin completar
        public static readonly SolidColorBrush Pending = Make(0xFF, 0x70, 0x43); //naranja intenso: siguiente paso a realizar
        public static readonly SolidColorBrush Done = Make(0x43, 0xA0, 0x47);    //verde: paso completado

        private static SolidColorBrush Make(byte r, byte g, byte b)
        {
            SolidColorBrush br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }
    }
}

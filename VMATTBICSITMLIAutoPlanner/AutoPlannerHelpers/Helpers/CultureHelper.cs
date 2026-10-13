using System.Globalization;
using System.Threading;

namespace AutoPlannerHelpers.Helpers
{
    /// <summary>
    /// The configuration files, plan templates, and many internal string <-> number conversions use '.' as the decimal separator.
    /// On computers configured with a regional format that uses ',' as the decimal separator (e.g., Spanish - Colombia, es-CO),
    /// double.Parse("390.0") would return 3900 (or throw), silently corrupting jaw limits, collimator angles, doses, margins, etc.
    /// Forcing the invariant culture for the whole process (all threads, including the progress-window threads) removes this
    /// dependency on the Windows regional settings.
    /// </summary>
    public static class CultureHelper
    {
        public static void ForceInvariantCulture()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }
    }
}

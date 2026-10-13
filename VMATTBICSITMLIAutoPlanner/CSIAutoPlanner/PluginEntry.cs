using CSIAutoPlanner.ViewModels;
using CSIAutoPlanner.Views;

namespace CSIAutoPlanner
{
    /// <summary>
    /// Entry point used by the Eclipse binary plugin (VMATAutoPlanner.esapi.dll). The Eclipse context must already be initialized.
    /// </summary>
    public static class PluginEntry
    {
        public static void Run()
        {
            CSIMainView mv = new CSIMainView { DataContext = new CSIMainViewModel(new string[] { }) };
            mv.ShowDialog();
        }
    }
}

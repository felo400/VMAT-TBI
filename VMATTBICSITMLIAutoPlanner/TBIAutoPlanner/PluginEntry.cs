using TBIAutoPlanner.ViewModels;
using TBIAutoPlanner.Views;

namespace TBIAutoPlanner
{
    /// <summary>
    /// Entry point used by the Eclipse binary plugin (VMATAutoPlanner.esapi.dll). The Eclipse context must already be initialized.
    /// </summary>
    public static class PluginEntry
    {
        public static void Run()
        {
            TBIMainView mv = new TBIMainView { DataContext = new TBIMainViewModel(new string[] { }) };
            mv.ShowDialog();
        }
    }
}

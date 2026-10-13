using TMLIAutoPlanner.ViewModels;
using TMLIAutoPlanner.Views;

namespace TMLIAutoPlanner
{
    /// <summary>
    /// Entry point used by the Eclipse binary plugin (VMATAutoPlanner.esapi.dll). The Eclipse context must already be initialized.
    /// </summary>
    public static class PluginEntry
    {
        public static void Run()
        {
            TMLIMainView mv = new TMLIMainView { DataContext = new TMLIMainViewModel(new string[] { }) };
            mv.ShowDialog();
        }
    }
}

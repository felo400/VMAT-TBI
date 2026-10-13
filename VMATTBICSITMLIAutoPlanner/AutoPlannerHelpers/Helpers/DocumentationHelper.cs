using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AutoPlannerHelpers.Helpers
{
    /// <summary>
    /// Opens the PDF guides that are copied to the 'documentation' folder next to the executables during the build.
    /// Replaces the original placeholder "test" message boxes of the help buttons.
    /// </summary>
    public static class DocumentationHelper
    {
        public static void OpenDocument(string fileName)
        {
            try
            {
                string folder = ConfigurationHelper.GetDefaultDocumentationPath();
                string file = string.IsNullOrEmpty(fileName) ? "" : Path.Combine(folder, fileName);
                if (!string.IsNullOrEmpty(file) && File.Exists(file)) Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                else if (Directory.Exists(folder)) Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                else MessageBox.Show($"Documentation folder not found:\n{folder}");
            }
            catch (Exception e)
            {
                MessageBox.Show($"Unable to open documentation because: {e.Message}");
            }
        }
    }
}

using AutoPlannerHelpers.Enums;
using AutoPlannerHelpers.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AutoPlannerHelpers.Helpers
{
    public static class ImportListenerHelper
    {
        public static string GetImportListenerExePath()
        {
            string path = Assembly.GetExecutingAssembly().Location;
            return Directory.GetFiles(Path.GetDirectoryName(path), "*.exe").FirstOrDefault(x => x.Contains("ImportListener"));

        }

        public static bool LaunchImportListener(string listener, ImportExportDataModel IEData, string mrn, PlanType planType)
        {
            if (!string.IsNullOrEmpty(listener))
            {
                ProcessStartInfo p = new ProcessStartInfo(listener);
                //quote every argument so network paths or MRNs containing spaces are received intact by the listener
                string[] args = new string[]
                {
                    IEData.ImportLocation,
                    mrn,
                    IEData.AriaDBDaemon.AETitle,
                    IEData.AriaDBDaemon.IP,
                    IEData.AriaDBDaemon.Port.ToString(CultureInfo.InvariantCulture),
                    IEData.LocalDaemon.AETitle,
                    IEData.LocalDaemon.Port.ToString(CultureInfo.InvariantCulture),
                    3600.0.ToString(CultureInfo.InvariantCulture),
                    planType.ToString()
                };
                p.Arguments = string.Join(" ", args.Select(x => QuoteArgument(x)));
                Process.Start(p);
                return false;
            }
            else return true;
        }

        /// <summary>
        /// Quote a single command line argument following the Windows (CommandLineToArgvW) rules so it is received intact by the executable
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public static string QuoteArgument(string arg)
        {
            if (arg == null) arg = "";
            if (arg.Length > 0 && arg.IndexOfAny(new char[] { ' ', '\t', '"' }) < 0) return arg;
            StringBuilder sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"') { sb.Append('\\', backslashes * 2 + 1); sb.Append('"'); }
                else { sb.Append('\\', backslashes); sb.Append(c); }
                backslashes = 0;
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }
    }
}

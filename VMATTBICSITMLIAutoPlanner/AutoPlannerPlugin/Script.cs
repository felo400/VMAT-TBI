using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace VMS.TPS
{
    /// <summary>
    /// Eclipse binary plugin entry point (Tools > Scripts > VMATAutoPlanner.esapi.dll).
    /// Runs the VMAT CSI / TBI / TMLI planners and the optimization loop INSIDE Eclipse using the ScriptContext,
    /// so no stand-alone Application.CreateApplication() connection is needed.
    /// This class only sets up the environment; everything that touches the planner assemblies lives in PluginRunner
    /// (separate, non-inlined) so those assemblies are loaded after the AssemblyResolve handler is in place.
    /// </summary>
    public class Script
    {
        public Script() { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            //Eclipse may load the plugin from a cache/shadow copy, so Location is not always the real folder.
            //Collect every plausible folder (CodeBase = original location, Location, and the folders of the planner
            //assemblies already loaded) and search all of them for dependencies (e.g. Microsoft.Xaml.Behaviors.dll).
            string pluginDir = GetOriginalDirectory(Assembly.GetExecutingAssembly());
            ResolveEventHandler resolver = (sender, args) =>
            {
                try
                {
                    string name = new AssemblyName(args.Name).Name;
                    Assembly already = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
                    if (already != null) return already;
                    foreach (string dir in GetSearchDirectories(pluginDir))
                    {
                        foreach (string ext in new[] { ".dll", ".exe" })
                        {
                            string candidate = Path.Combine(dir, name + ext);
                            if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
                        }
                    }
                }
                catch { }
                return null;
            };
            AppDomain.CurrentDomain.AssemblyResolve += resolver;

            //the configuration files/templates use '.' as decimal separator. Force the invariant culture ONLY while the
            //plugin runs (Eclipse's own culture is restored afterwards)
            CultureInfo prevCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo prevUICulture = Thread.CurrentThread.CurrentUICulture;
            CultureInfo prevDefault = CultureInfo.DefaultThreadCurrentCulture;
            CultureInfo prevDefaultUI = CultureInfo.DefaultThreadCurrentUICulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            try
            {
                if (context == null || context.Patient == null)
                {
                    MessageBox.Show("Abra un paciente (y su structure set) antes de ejecutar el script.\nPlease open a patient first.", "VMAT AutoPlanner");
                    return;
                }
                PluginRunner.Run(context, pluginDir);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error en VMAT AutoPlanner:\n{e.GetType().Name}: {e.Message}\n\n{e.InnerException?.Message}\n\n{e.StackTrace}", "VMAT AutoPlanner");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = prevCulture;
                Thread.CurrentThread.CurrentUICulture = prevUICulture;
                CultureInfo.DefaultThreadCurrentCulture = prevDefault;
                CultureInfo.DefaultThreadCurrentUICulture = prevDefaultUI;
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }
            /// <summary>
        /// Original folder of an assembly (CodeBase survives shadow copying; Location is the fallback)
        /// </summary>
        internal static string GetOriginalDirectory(Assembly a)
        {
            try
            {
                if (!string.IsNullOrEmpty(a.CodeBase))
                {
                    string p = new Uri(a.CodeBase).LocalPath;
                    if (File.Exists(p)) return Path.GetDirectoryName(p);
                }
            }
            catch { }
            try { if (!string.IsNullOrEmpty(a.Location)) return Path.GetDirectoryName(a.Location); } catch { }
            return "";
        }

        internal static IEnumerable<string> GetSearchDirectories(string pluginDir)
        {
            List<string> dirs = new List<string>();
            if (!string.IsNullOrEmpty(pluginDir)) dirs.Add(pluginDir);
            try
            {
                string loc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(loc)) dirs.Add(loc);
            }
            catch { }
            string[] ours = { "VMATAutoPlanner.esapi", "AutoPlannerHelpers", "CSIAutoPlanner", "TBIAutoPlanner", "TMLIAutoPlanner", "AutoPlannerOptimizationLoop" };
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (a.IsDynamic || !ours.Contains(a.GetName().Name)) continue;
                    dirs.Add(GetOriginalDirectory(a));
                    if (!string.IsNullOrEmpty(a.Location)) dirs.Add(Path.GetDirectoryName(a.Location));
                }
                catch { }
            }
            return dirs.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

using System;
using System.Globalization;
using System.IO;
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
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ResolveEventHandler resolver = (sender, args) =>
            {
                try
                {
                    string name = new AssemblyName(args.Name).Name;
                    foreach (string ext in new[] { ".dll", ".exe" })
                    {
                        string candidate = Path.Combine(pluginDir, name + ext);
                        if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
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
    }
}

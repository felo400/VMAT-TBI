using AutoPlannerHelpers.Context;
using AutoPlannerHelpers.Logging;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using Application = System.Windows.Application;

namespace VMS.TPS
{
    internal static class PluginRunner
    {
        private enum Choice { None, CSI, TBI, TMLI, OptLoop }

        //the plugin assembly stays loaded in Eclipse between executions -> remember if the styles were already added
        private static bool _stylesLoaded = false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Run(ScriptContext context, string pluginDir)
        {
            PreloadDependencies(pluginDir);
            EnsureWpfApplicationAndStyles();

            Choice choice = AskWhatToRun(context);
            if (choice == Choice.None) return;

            //fresh state for this execution (static singletons survive between plugin runs)
            WeakReferenceMessenger.Default.Reset();
            Logger.ResetInstance();
            EclipseContextHelper.GenerateEclipseContextFromPlugin(context.Patient,
                                                                  context.StructureSet,
                                                                  context.Image,
                                                                  context.ExternalPlanSetup,
                                                                  context.Course,
                                                                  context.CurrentUser);
            try
            {
                switch (choice)
                {
                    case Choice.CSI: CSIAutoPlanner.PluginEntry.Run(); break;
                    case Choice.TBI: TBIAutoPlanner.PluginEntry.Run(); break;
                    case Choice.TMLI: TMLIAutoPlanner.PluginEntry.Run(); break;
                    case Choice.OptLoop: AutoPlannerOptimizationLoop.PluginEntry.Run(); break;
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.Reset();
                EclipseContext.ResetInstance();
            }
        }

        /// <summary>
        /// WPF's XAML parser loads assemblies referenced from XAML (e.g. Microsoft.Xaml.Behaviors) by name, without
        /// looking in the plugin folder. Loading them up-front from the plugin folder makes them available to the parser.
        /// </summary>
        private static void PreloadDependencies(string pluginDir)
        {
            string[] deps = { "Microsoft.Xaml.Behaviors", "CommunityToolkit.Mvvm", "AutoPlannerHelpers", "EvilDICOM",
                              "System.ComponentModel.Annotations", "System.Runtime.CompilerServices.Unsafe", "System.Memory",
                              "System.Buffers", "System.Numerics.Vectors", "System.Threading.Tasks.Extensions", "System.ValueTuple",
                              "Microsoft.Bcl.AsyncInterfaces" };
            List<string> missing = new List<string>();
            foreach (string dep in deps)
            {
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => string.Equals(a.GetName().Name, dep, StringComparison.OrdinalIgnoreCase))) continue;
                bool loaded = false;
                foreach (string dir in Script.GetSearchDirectories(pluginDir))
                {
                    string f = Path.Combine(dir, dep + ".dll");
                    if (File.Exists(f))
                    {
                        try { Assembly.LoadFrom(f); loaded = true; break; } catch { }
                    }
                }
                if (!loaded && (dep == "Microsoft.Xaml.Behaviors" || dep == "CommunityToolkit.Mvvm" || dep == "AutoPlannerHelpers")) missing.Add(dep);
            }
            if (missing.Any())
            {
                MessageBox.Show("No se encontraron estas DLL junto al plugin: " + string.Join(", ", missing) +
                                "\nCarpetas buscadas:\n" + string.Join("\n", Script.GetSearchDirectories(pluginDir)) +
                                "\n\nCopie TODA la carpeta VMAT-TBI-CSI-TMLI (no solo el .dll).", "VMAT AutoPlanner");
            }
        }

        /// <summary>
        /// The planner windows use styles that the stand-alone executables load at Application level (App.xaml).
        /// Inside Eclipse there is no such App, so create one (if Eclipse doesn't have one) and add the styles.
        /// If Eclipse already has a WPF Application, only KEYED resources are added so Eclipse's own look is not altered.
        /// </summary>
        private static void EnsureWpfApplicationAndStyles()
        {
            if (_stylesLoaded && Application.Current != null) return;
            bool createdByUs = false;
            if (Application.Current == null)
            {
                new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                createdByUs = true;
            }
            ResourceDictionary styles = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/AutoPlannerHelpers;component/ViewStyles/AllStyles.xaml", UriKind.Absolute)
            };
            if (createdByUs)
            {
                Application.Current.Resources.MergedDictionaries.Add(styles);
            }
            else
            {
                ResourceDictionary keyedOnly = new ResourceDictionary();
                CopyKeyedResources(styles, keyedOnly);
                Application.Current.Resources.MergedDictionaries.Add(keyedOnly);
            }
            _stylesLoaded = true;
        }

        private static void CopyKeyedResources(ResourceDictionary source, ResourceDictionary target)
        {
            foreach (ResourceDictionary merged in source.MergedDictionaries) CopyKeyedResources(merged, target);
            foreach (DictionaryEntry entry in source)
            {
                if (entry.Key is Type) continue; //implicit style (e.g. all Buttons) -> skip, would restyle Eclipse
                if (!target.Contains(entry.Key)) target.Add(entry.Key, entry.Value);
            }
        }

        private static Choice AskWhatToRun(ScriptContext context)
        {
            Choice result = Choice.None;
            Window w = new Window
            {
                Title = $"VMAT AutoPlanner - Eclipse 16.1 (v{Assembly.GetExecutingAssembly().GetName().Version})",
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.WhiteSmoke
            };
            StackPanel sp = new StackPanel { Margin = new Thickness(15) };
            sp.Children.Add(new TextBlock
            {
                Text = $"Paciente: {context.Patient.Id}" +
                       (context.StructureSet != null ? $"\nStructure set: {context.StructureSet.Id}" : "\nStructure set: (ninguno abierto)") +
                       (context.ExternalPlanSetup != null ? $"\nPlan abierto: {context.ExternalPlanSetup.Id}" : ""),
                Margin = new Thickness(0, 0, 0, 10)
            });
            sp.Children.Add(new TextBlock { Text = "Seleccione / Select:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });

            void AddButton(string text, Choice c, bool enabled, string tooltip)
            {
                Button b = new Button { Content = text, Width = 260, Height = 32, Margin = new Thickness(0, 4, 0, 4), IsEnabled = enabled, ToolTip = tooltip };
                ToolTipService.SetShowOnDisabled(b, true);
                b.Click += (s, e) => { result = c; w.Close(); };
                sp.Children.Add(b);
            }
            bool hasSS = context.StructureSet != null;
            bool hasPlan = context.ExternalPlanSetup != null;
            AddButton("VMAT CSI", Choice.CSI, hasSS, hasSS ? "Planificación CSI" : "Abra un structure set primero");
            AddButton("VMAT TBI", Choice.TBI, hasSS, hasSS ? "Planificación TBI" : "Abra un structure set primero");
            AddButton("VMAT TMLI", Choice.TMLI, hasSS, hasSS ? "Planificación TMLI" : "Abra un structure set primero");
            AddButton("Ciclo de optimización / Optimization loop", Choice.OptLoop, hasPlan, hasPlan ? "Optimizar el plan abierto" : "Abra el plan VMAT creado por el planificador");
            sp.Children.Add(new TextBlock
            {
                Text = "Al terminar, revise los cambios y GUÁRDELOS en Eclipse.",
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 10, 0, 0)
            });
            w.Content = sp;
            w.ShowDialog();
            return result;
        }
    }
}

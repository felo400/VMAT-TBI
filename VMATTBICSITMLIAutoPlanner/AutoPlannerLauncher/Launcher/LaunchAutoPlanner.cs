using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace VMS.TPS
{
    public class Script
    {
        /// <summary>
        /// Simple method to launch the launcher executable while passing the patient mrn and structure set id
        /// </summary>
        /// <param name="context"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                if (context.Patient != null)
                {
                    string exeName = "Launcher";
                    string path = AppExePath(exeName);
                    if (!string.IsNullOrEmpty(path))
                    {
                        ProcessStartInfo p = new ProcessStartInfo(path);
                        p.Arguments = SerializeEclipseContext(context);
                        Process.Start(p);
                    }
                    else MessageBox.Show("No se encontró AutoPlannerLauncher.exe.\n\n" +
                                         "Este script debe ejecutarse desde la carpeta COMPILADA 'bin\\' (la que contiene la subcarpeta 'VMAT-TBI-CSI-TMLI' con los .exe), " +
                                         "NO desde la carpeta del código fuente.\n\n" +
                                         "1) Compile la solución en Visual Studio (Release, x64).\n" +
                                         "2) En Eclipse: Tools > Scripts > Change Folder... y seleccione la carpeta 'bin' que se crea en la raíz del proyecto.\n" +
                                         "3) Ejecute LaunchAutoPlanner.cs desde esa carpeta.\n\n" +
                                         "Carpeta buscada:\n" + Path.GetDirectoryName(GetSourceFilePath()) + @"\VMAT-TBI-CSI-TMLI\");
                }
                else MessageBox.Show("Please open a patient before launching the autoplanning tool!");
            }
            catch (Exception e) { MessageBox.Show(e.Message); };
        }

        /// <summary>
        /// Retrieve the full file name of the executable
        /// </summary>
        /// <param name="exeName"></param>
        /// <returns></returns>
        private string AppExePath(string exeName)
        {
            string scriptDir = Path.GetDirectoryName(GetSourceFilePath());
            //normal location: <bin>\LaunchAutoPlanner.cs + <bin>\VMAT-TBI-CSI-TMLI\*.exe
            //fallback: script run from the source folder (AutoPlannerLauncher\Launcher) after the solution was compiled -> <repo>\bin\VMAT-TBI-CSI-TMLI
            string[] candidates = new string[]
            {
                Path.Combine(scriptDir, "VMAT-TBI-CSI-TMLI"),
                Path.GetFullPath(Path.Combine(scriptDir, @"..\..\..\bin\VMAT-TBI-CSI-TMLI"))
            };
            foreach (string dir in candidates)
            {
                string exe = FirstExePathIn(dir, exeName);
                if (!string.IsNullOrEmpty(exe)) return exe;
            }
            return null;
        }

        /// <summary>
        /// Return the first identified executable in the supplied directory that has a name matching the supplied name
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="exeName"></param>
        /// <returns></returns>
        private string FirstExePathIn(string dir, string exeName)
        {
            if (!Directory.Exists(dir)) return null;
            return Directory.GetFiles(dir, "*.exe").FirstOrDefault(x => Path.GetFileName(x).Contains(exeName));
        }

        /// <summary>
        /// Clever trick to return the full path of the currently executing file
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <returns></returns>
        private string GetSourceFilePath([CallerFilePath] string sourceFilePath = "")
        {
            return sourceFilePath;
        }

        private string SerializeEclipseContext(ScriptContext context)
        {
            string serializedContext = "";
            if (context != null)
            {
                //NOTE: every value is quoted so that IDs containing spaces (e.g., a course named "VMAT CSI") are passed as a single argument
                if (!ReferenceEquals(context.Patient, null)) serializedContext += string.Format(" -m {0}", QuoteArgument(context.Patient.Id));
                if (!ReferenceEquals(context.StructureSet,null)) serializedContext += string.Format(" -s {0}", QuoteArgument(context.StructureSet.UID));
                if (!ReferenceEquals(context.Image,null)) serializedContext += string.Format(" -i {0}", QuoteArgument(context.Image.FOR));
                if (!ReferenceEquals(context.ExternalPlanSetup,null)) serializedContext += string.Format(" -p {0}", QuoteArgument(context.ExternalPlanSetup.UID));
                if (!ReferenceEquals(context.Course,null)) serializedContext += string.Format(" -c {0}", QuoteArgument(context.Course.Id));
            }
            return serializedContext;
        }

        /// <summary>
        /// Quote a single command line argument following the Windows (CommandLineToArgvW) rules so it is received intact by the executable
        /// (single file plugins can't reference external libraries, so this is duplicated in the launcher view model)
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private static string QuoteArgument(string arg)
        {
            if (arg == null) arg = "";
            if (arg.Length > 0 && arg.IndexOfAny(new char[] { ' ', '\t', '"' }) < 0) return arg;
            System.Text.StringBuilder sb = new System.Text.StringBuilder("\"");
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

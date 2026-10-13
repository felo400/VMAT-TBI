using System.Collections.Generic;
using VMS.TPS.Common.Model.API;

namespace AutoPlannerHelpers.Context
{
    public class EclipseContext
    {
        private static EclipseContext _instance;
        public static EclipseContext GetInstance()
        {
            if (!ReferenceEquals(_instance, null)) return _instance;
            else return _instance = new EclipseContext();
        }

        /// <summary>
        /// Reset the singleton. Needed when running as an Eclipse binary plugin: the plugin assembly stays loaded in Eclipse
        /// between executions, so static state from a previous run (patient, plans...) must be discarded
        /// </summary>
        public static void ResetInstance() { _instance = null; }

        /// <summary>
        /// True when the code runs INSIDE Eclipse as a binary plugin (VMATAutoPlanner.esapi.dll). In that case there is no
        /// ESAPI Application object: the patient comes from the ScriptContext, and saving/closing the patient is done by the
        /// user in Eclipse (a plugin can't call SaveModifications/ClosePatient)
        /// </summary>
        public bool IsPluginMode { get; set; } = false;
        public bool IsInitialized { get => !ReferenceEquals(Application, null) || IsPluginMode; }
        public Application Application { get; set; } = null;
        public Patient Patient { get; set; } = null;
        public Course Course { get; set; } = null;
        public List<ExternalPlanSetup> VMATPlans { get; set; } = new List<ExternalPlanSetup> { };
        public StructureSet StructureSet { get; set; } = null;
        public IEnumerable<Registration> Registrations { get; set; } = new List<Registration> { };
        public IEnumerable<Image> CTImages { get; set; } = new List<Image> { };
        public string ImageFOR { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserId { get; set; } = "";

        public void ClearContext(bool closePatient = true)
        {
            if (!IsPluginMode && !ReferenceEquals(Application, null) && closePatient)
            {
                Application.ClosePatient();
                Patient = null;
            }
            Course = null;
            VMATPlans = new List<ExternalPlanSetup> { };
            StructureSet = null;
            Registrations = new List<Registration> { };
            CTImages = new List<Image> { };
            ImageFOR = string.Empty;
        }
    }
}

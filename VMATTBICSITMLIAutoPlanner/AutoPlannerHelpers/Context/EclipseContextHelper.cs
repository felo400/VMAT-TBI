using System;
using System.Collections.Generic;
using System.Linq;
using AutoPlannerHelpers.Enums;
using AutoPlannerHelpers.Logging;
using VMS.TPS.Common.Model.API;

namespace AutoPlannerHelpers.Context
{
    public static class EclipseContextHelper
    {
        //data members
        private static string PatientId = "";
        private static string StructureSetUID = "";
        private static string ImageFOR = "";
        private static string PlanUID = "";
        private static string CourseID = "";

        /// <summary>
        /// Helper method to de-serialize the list of arguments passed from Eclipse to generate a fake Eclipse context
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool GenerateEclipseContext(List<string> args) 
        {
            //each step is executed separately so that (1) a failure in a non-essential step (CT list, registrations, plan, course)
            //does not prevent the script from opening and (2) the user/physicist sees exactly WHICH step failed and why
            string step = "decode arguments";
            try
            {
                DecodeStringContext(args);
                step = "Application.CreateApplication()";
                EclipseContext.GetInstance().Application = Application.CreateApplication();
                step = "read current user";
                EclipseContext.GetInstance().UserName = EclipseContext.GetInstance().Application.CurrentUser?.Name ?? "";
                EclipseContext.GetInstance().UserId = EclipseContext.GetInstance().Application.CurrentUser?.Id ?? "";
                if (string.IsNullOrEmpty(PatientId))
                {
                    Logger.GetInstance().LogError($"Error! No patient Id received from Eclipse. Arguments received: '{string.Join(" | ", args)}'");
                    return true;
                }
                step = $"open patient '{PatientId}'";
                EclipseContext.GetInstance().Patient = EclipseContext.GetInstance().Application.OpenPatientById(PatientId);
                if (ReferenceEquals(EclipseContext.GetInstance().Patient, null))
                {
                    Logger.GetInstance().LogError($"Error! Patient '{PatientId}' could not be opened (OpenPatientById returned null).");
                    return true;
                }
            }
            catch (Exception e)
            {
                string hint = "";
                if (step.StartsWith("Application.CreateApplication"))
                {
                    string exe = System.IO.Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "");
                    hint = "\n\nCAUSA MAS PROBABLE: Eclipse no autorizo la conexion de este programa.\n" +
                           $"1) Apruebe '{exe}' (y AutoPlannerHelpers.dll) en Eclipse: Tools > Script Approvals (requiere usuario con permiso de aprobar scripts).\n" +
                           "   En una base de datos NO clinica (t-box) puede registrarse para investigacion/evaluacion.\n" +
                           "2) Ejecute el programa SIEMPRE desde Eclipse (Tools > Scripts > LaunchAutoPlanner.cs) en una estacion/Citrix con Eclipse 16.1.\n" +
                           "3) Si ya esta aprobado: la aprobacion se pierde si el .exe se reemplaza o se mueve de carpeta; vuelva a aprobarlo.";
                }
                Logger.GetInstance().LogError($"Error! Unable to generate Eclipse Context instance.\nStep: {step}\nReason: {e.GetType().Name}: {e.Message}{hint}\n\n{e.StackTrace}");
                return true;
            }

            Patient pat = EclipseContext.GetInstance().Patient;
            Logger.GetInstance().MRN = pat.Id;
            TryStep("read registrations", () => EclipseContext.GetInstance().Registrations = (pat.Registrations ?? Enumerable.Empty<Registration>()).ToList());
            TryStep("read CT images", () =>
            {
                //materialize the list now (the original lazy query was evaluated later and could fail outside of this try/catch)
                EclipseContext.GetInstance().CTImages = (pat.Studies ?? Enumerable.Empty<Study>())
                                                        .Where(st => st != null && st.Series != null)
                                                        .SelectMany(st => st.Series)
                                                        .Where(se => se != null && se.Modality == VMS.TPS.Common.Model.Types.SeriesModality.CT && se.Images != null)
                                                        .SelectMany(se => se.Images)
                                                        .Where(im => im != null && im.Series != null && !double.IsNaN(im.Origin.x))
                                                        .ToList();
                if (!EclipseContext.GetInstance().CTImages.Any()) Logger.GetInstance().LogError($"Patient ({pat.Id}) has NO CT images!");
            });
            if (!string.IsNullOrEmpty(StructureSetUID))
            {
                TryStep("open structure set", () =>
                {
                    EclipseContext.GetInstance().StructureSet = (pat.StructureSets ?? Enumerable.Empty<StructureSet>()).FirstOrDefault(x => x != null && string.Equals(StructureSetUID, x.UID));
                    if (!ReferenceEquals(EclipseContext.GetInstance().StructureSet, null)) Logger.GetInstance().StructureSet = StructureSetUID;
                    else Logger.GetInstance().LogError($"Warning! Structure set with UID {StructureSetUID} not found for patient {pat.Id}");
                });
            }
            EclipseContext.GetInstance().ImageFOR = ImageFOR ?? "";
            if (!string.IsNullOrEmpty(PlanUID))
            {
                TryStep("open plan", () =>
                {
                    ExternalPlanSetup plan = (pat.Courses ?? Enumerable.Empty<Course>()).Where(c => c != null && c.ExternalPlanSetups != null).SelectMany(x => x.ExternalPlanSetups).FirstOrDefault(x => x != null && string.Equals(PlanUID, x.UID));
                    //only add the plan if it was found (previously a null entry could be added to the list)
                    EclipseContext.GetInstance().VMATPlans = plan != null ? new List<ExternalPlanSetup> { plan } : new List<ExternalPlanSetup> { };
                    if (plan != null) Logger.GetInstance().PlanUIDs = new List<string> { plan.UID };
                });
            }
            if (!string.IsNullOrEmpty(CourseID))
            {
                TryStep("open course", () => EclipseContext.GetInstance().Course = (pat.Courses ?? Enumerable.Empty<Course>()).FirstOrDefault(x => x != null && string.Equals(CourseID, x.Id)));
            }
            return false;
        }

        /// <summary>
        /// Run a non-essential context step. A failure is reported (with the step name) but does not abort the script
        /// </summary>
        private static void TryStep(string step, Action a)
        {
            try { a(); }
            catch (Exception e)
            {
                Logger.GetInstance().LogError($"Warning! Eclipse context step '{step}' failed: {e.GetType().Name}: {e.Message}\n\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Helper method to parse the meaning of each of the string arguments from Eclipse
        /// </summary>
        /// <param name="contextArgs"></param>
        private static void DecodeStringContext(List<string> contextArgs)
        {
            //assumes there will be an even number of arguments that are "paired"
            for (int i = 0; i < contextArgs.Count(); i += 2)
            {
                if (Settings.AutoPlannerHelperSettings.ContextKeyDictionary.TryGetValue(contextArgs.ElementAt(i), out EclipseDecodeKey theKey))
                {
                    if (theKey == EclipseDecodeKey.Patient) PatientId = contextArgs.ElementAt(i + 1);
                    else if (theKey == EclipseDecodeKey.StructureSet) StructureSetUID = contextArgs.ElementAt(i + 1);
                    else if (theKey == EclipseDecodeKey.Image) ImageFOR = contextArgs.ElementAt(i + 1);
                    else if (theKey == EclipseDecodeKey.Plan) PlanUID = contextArgs.ElementAt(i + 1);
                    else if (theKey == EclipseDecodeKey.Course) CourseID = contextArgs.ElementAt(i + 1);
                }
            }
        }
    }
}

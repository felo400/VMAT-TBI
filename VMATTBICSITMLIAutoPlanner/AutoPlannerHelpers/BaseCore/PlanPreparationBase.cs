using AutoPlannerHelpers.Models;
using AutoPlannerHelpers.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace AutoPlannerHelpers.BaseCore
{
    public class PlanPreparationBase : SimpleProgressWindowViewModel
    {
        protected ExternalPlanSetup VMATPlan = null;
        protected int numVMATIsos = 0;
        protected int numIsos;
        protected List<ExternalPlanSetup> separatedPlans = new List<ExternalPlanSetup> { };
        public bool DoseRecalcNeeded { get; set; } = false;
        protected bool _recalculateDoseOnly = false;

        public bool RecalculateDoseOnly
        {
            get { return _recalculateDoseOnly = false; }
            set { _recalculateDoseOnly = value; }
        }

        /// <summary>
        /// Helper method to verify the name formatting of all the beam Ids is appropriate and will not cause problems with this script
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        protected bool CheckBeamNameFormatting(ExternalPlanSetup plan)
        {
            //Convencion de Stanford: Id del campo empieza con numero + espacio ("1 Brain CCW"). La separacion de planes NO depende
            //de esto (los campos se identifican por Id completo), asi que solo se informa y nunca se detiene el proceso.
            List<string> nonStandard = plan.Beams.Where(x => !x.IsSetupField).Select(x => x.Id)
                                           .Where(id => id.Length < 2 || !int.TryParse(id.Substring(0, 2), out int dummy)).ToList();
            if (nonStandard.Any())
            {
                ProvideUIUpdate($"Aviso: {nonStandard.Count} campo(s) no siguen la convencion 'numero + espacio' ({string.Join(", ", nonStandard)}). No afecta la separacion; se continua.");
            }
            else ProvideUIUpdate(100, "Nombres de campos OK");
            return false;
        }

        /// <summary>
        /// Helper method to check if the plan dose calculation option for the supplied plan is set to true. If so, this all separated plans will require
        /// recalculation
        /// </summary>
        /// <param name="thePlan"></param>
        /// <returns></returns>
        protected bool CheckIfDoseRecalcNeeded(ExternalPlanSetup thePlan)
        {
            if (thePlan.GetCalculationOptions(thePlan.GetCalculationModel(CalculationType.PhotonVolumeDose)).Any(x => string.Equals(x.Key, "PlanDoseCalculation")))
            {
                if (thePlan.GetCalculationOptions(thePlan.GetCalculationModel(CalculationType.PhotonVolumeDose)).First(x => string.Equals(x.Key, "PlanDoseCalculation")).Value == "ON")
                {
                    ProvideUIUpdate("Dose recalculation required for all separated plans!");
                    return true;
                }
            }
            ProvideUIUpdate("Dose recalculation NOT required for all separated plans");
            return false;
        }

        /// <summary>
        /// Utility method to take the supplied plan, list of isos with list of beams, and list of iso names and separate the isocenters in the supplied
        /// plan into separate plans
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="beamsPerIso"></param>
        /// <param name="names"></param>
        /// <param name="previousNumIsos"></param>
        /// <returns></returns>
        protected bool SeparatePlan(ExternalPlanSetup plan, List<List<Beam>> beamsPerIso, List<IsocenterModel> names, bool isAPPA = false, int previousNumIsos = 0)
        {
            int percentComplete = 0;
            int calcItems = 4 * beamsPerIso.Count;
            int count = previousNumIsos;
            //iterate through list of beams for each isocenter
            foreach (List<Beam> beams in beamsPerIso)
            {
                //copy the plan, set the plan id based on the counter, and make a empty list to hold the beams that need to be removed
                ExternalPlanSetup newplan = (ExternalPlanSetup)plan.Course.CopyPlanSetup(plan);
                string isoName = (names != null && count < names.Count) ? names.ElementAt(count).IsocenterId : $"Iso{count + 1}";
                string desiredId = isAPPA ? $"{count + 1} {(isoName.ToLower().Contains("upper") ? "Upper Legs" : "Lower Legs")}"
                                          : $"{count + 1} {isoName}";
                //plans with this Id may already exist (e.g. a previous separation): ESAPI can't delete them, so use a free Id
                newplan.Id = GetUniquePlanId(plan.Course, newplan, desiredId);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Created new plan {newplan.Id} as copy of {plan.Id}");
                List<Beam> beamsToRemove = new List<Beam> { };
                //can't add reference point to plan because it must be open in Eclipse for ESAPI to perform this function. Need to fix in v16
                //newplan.AddReferencePoint(newplan.StructureSet.Structures.First(x => x.Id.ToLower() == "ptv_body"), null, newplan.Id, newplan.Id);
                //add the current plan copy to the separatedPlans list
                separatedPlans.Add(newplan);
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Added {newplan.Id} to list of separated plans");
                //loop through each beam in the plan copy and compare it to the list of beams in the current isocenter
                foreach (Beam b in newplan.Beams)
                {
                    //if the current beam in newPlan is NOT found in the beams list, add it to the removeMe list. This logic has to be applied. You can't directly remove the beams in this loop as ESAPI will
                    //complain that the enumerable that it is using to index the loop changes on each iteration (i.e., newplan.Beams changes with each iteration). Do NOT add setup beams to the removeMe list. The
                    //idea is to have dosi add one set of setup fields to the original plan and then not remove those for each created plan. Unfortunately, dosi will have to manually adjust the iso position for
                    //the setup fields in each new plan (no way to adjust the existing isocenter of an existing beam, it has to be re-added)
                    if (!beams.Any(x => string.Equals(x.Id, b.Id)) && !b.IsSetupField)
                    {
                        ProvideUIUpdate($"Added {b.Id} to list of beams to remove from {newplan.Id}");
                        beamsToRemove.Add(b);
                    }
                }
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Finished identifying beams that need to be removed from {newplan.Id}");

                //Eclipse 16.1 refuses to delete a beam that still has calculated dose ("objects underneath which can not be deleted").
                //The copy carries the dose of the original plan; it has to be recalculated anyway after separation, so invalidate it first
                //by re-assigning the dose calculation model (same approach used elsewhere to reset the calculation matrix).
                ClearDoseOfPlan(newplan);
                if (RemoveExtraBeams(newplan, beamsToRemove)) return true;
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Removed excess beams from {newplan.Id}");

                count++;
            }
            return false;
        }

        protected void ClearDoseOfPlan(ExternalPlanSetup plan)
        {
            try
            {
                if (plan.Dose == null && !plan.IsDoseValid) return;
                string calcModel = plan.GetCalculationModel(CalculationType.PhotonVolumeDose);
                plan.ClearCalculationModel(CalculationType.PhotonVolumeDose);
                if (!string.IsNullOrEmpty(calcModel)) plan.SetCalculationModel(CalculationType.PhotonVolumeDose, calcModel);
                ProvideUIUpdate($"Dose of {plan.Id} invalidated before removing beams (dose must be recalculated)");
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"Warning! Could not invalidate dose of {plan.Id}: {e.Message}");
            }
        }

        /// <summary>
        /// Return 'desired' if no other plan in the course uses it, otherwise 'desired_2', 'desired_3'... (Eclipse plan Ids: max 13 characters)
        /// </summary>
        protected string GetUniquePlanId(Course course, ExternalPlanSetup self, string desired)
        {
            if (desired.Length > 13) desired = desired.Substring(0, 13);
            bool InUse(string id) => course.PlanSetups.Any(p => !ReferenceEquals(p, self) && string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (!InUse(desired)) return desired;
            for (int i = 2; i < 100; i++)
            {
                string suffix = $"_{i}";
                string root = desired.Length + suffix.Length > 13 ? desired.Substring(0, 13 - suffix.Length) : desired;
                if (!InUse(root + suffix))
                {
                    ProvideUIUpdate($"Plan Id '{desired}' already exists in course {course.Id} -> using '{root + suffix}'");
                    return root + suffix;
                }
            }
            return self.Id;
        }

        /// <summary>
        /// Helper method to remove extra beams from each separated plan. Each separated plan initially has all beams and this method removes the
        /// beams not associated with the isocenter of interest for this plan
        /// </summary>
        /// <param name="newPlan"></param>
        /// <param name="removeMe"></param>
        /// <returns></returns>
        protected bool RemoveExtraBeams(ExternalPlanSetup newPlan, List<Beam> removeMe)
        {
            //now remove the beams for the current plan copy
            try
            {
                foreach (Beam b in removeMe)
                {
                    ProvideUIUpdate($"Removing beam {b.Id} from {newPlan.Id}");
                    newPlan.RemoveBeam(b);
                }
            }
            catch (Exception e)
            {
                ProvideUIUpdate($"Failed to remove beams in plan {newPlan.Id}");
                ProvideUIUpdate($"{e.Message}");
                if (e.Message.ToLower().Contains("underneath"))
                {
                    ProvideUIUpdate("CAUSA PROBABLE: los campos de este plan tienen DRR (imagen de referencia). Eclipse 16.1 no deja borrar campos con DRR desde un script.");
                    ProvideUIUpdate("SOLUCION: separe este plan a mano en Eclipse, o vuelva a crear los campos (Beam Placement) con 'create DRRs=false' en el archivo .ini y repita la separacion.");
                }
                ProvideUIUpdate(e.StackTrace, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Helper method to recalculate dose for all plans in the separatedPlans list
        /// </summary>
        /// <returns></returns>
        public bool ReCalculateDose()
        {
            int percentComplete = 0;
            int calcItems = 4 * separatedPlans.Count;
            //loop through each plan in the separatedPlans list and calculate dose for each plan
            foreach (ExternalPlanSetup p in separatedPlans)
            {
                ProvideUIUpdate($"Recalculating dose for plan: {p.Id}");
                p.CalculateDose();
                ProvideUIUpdate(100 * ++percentComplete / calcItems, $"Dose recalculated for plan: {p.Id}");
            }
            return false;
        }
    }
}

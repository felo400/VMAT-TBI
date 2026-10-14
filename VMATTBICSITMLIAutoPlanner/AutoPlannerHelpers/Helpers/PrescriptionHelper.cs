using AutoPlannerHelpers.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace AutoPlannerHelpers.Helpers
{
    /// <summary>
    /// Prescription read from Eclipse (RT Prescription linked to a plan, or the plan's own dose/fractions/target)
    /// </summary>
    public class EclipsePrescriptionModel
    {
        public string SourcePlanId { get; set; } = "";
        public string SourceCourseId { get; set; } = "";
        public string TargetId { get; set; } = "";
        public double DosePerFractionCGy { get; set; } = double.NaN;
        public int NumberOfFractions { get; set; } = 0;
        public bool FromRTPrescription { get; set; } = false;
        public bool IsValid => !double.IsNaN(DosePerFractionCGy) && DosePerFractionCGy > 0 && NumberOfFractions > 0;
        public double TotalDoseCGy => DosePerFractionCGy * NumberOfFractions;
        public override string ToString() => $"{DosePerFractionCGy:0.0} cGy x {NumberOfFractions} = {TotalDoseCGy:0.0} cGy, target '{TargetId}' (plan {SourcePlanId}, curso {SourceCourseId}{(FromRTPrescription ? ", prescripción ARIA" : "")})";
    }

    public static class PrescriptionHelper
    {
        private static double ToCGy(DoseValue d)
        {
            if (d.IsRelativeDoseValue || double.IsNaN(d.Dose)) return double.NaN;
            if (d.Unit == DoseValue.DoseUnit.Gy) return d.Dose * 100.0;
            return d.Dose;
        }

        /// <summary>
        /// Look for the prescription of the patient: first the plan open in Eclipse, otherwise the most recent plan that uses the
        /// same CT (image) as the open structure set and has a prescription.
        /// </summary>
        public static EclipsePrescriptionModel Find(Patient patient, PlanSetup openPlan, StructureSet ss)
        {
            try
            {
                List<PlanSetup> candidates = new List<PlanSetup>();
                if (openPlan != null) candidates.Add(openPlan);
                if (patient != null)
                {
                    IEnumerable<PlanSetup> others = patient.Courses.SelectMany(c => c.PlanSetups)
                                                           .Where(p => p != null && !ReferenceEquals(p, openPlan))
                                                           .Where(p => ss == null || (p.StructureSet != null && p.StructureSet.Image != null && ss.Image != null && string.Equals(p.StructureSet.Image.Id, ss.Image.Id) && string.Equals(p.StructureSet.Image.FOR, ss.Image.FOR)))
                                                           .OrderByDescending(p => p.CreationDateTime ?? DateTime.MinValue);
                    candidates.AddRange(others);
                }
                foreach (PlanSetup p in candidates)
                {
                    EclipsePrescriptionModel rx = FromPlan(p);
                    if (rx != null && rx.IsValid) return rx;
                }
            }
            catch (Exception e)
            {
                Logger.GetInstance().LogError($"Warning! Could not read the prescription from Eclipse: {e.Message}", true);
            }
            return null;
        }

        private static EclipsePrescriptionModel FromPlan(PlanSetup p)
        {
            EclipsePrescriptionModel rx = new EclipsePrescriptionModel { SourcePlanId = p.Id, SourceCourseId = p.Course?.Id ?? "" };
            //1) ARIA RT prescription linked to the plan (highest total dose target)
            try
            {
                RTPrescription rtp = p.RTPrescription;
                if (rtp != null && rtp.Targets != null && rtp.Targets.Any())
                {
                    RTPrescriptionTarget t = rtp.Targets.Where(x => !double.IsNaN(ToCGy(x.DosePerFraction)) && x.NumberOfFractions > 0)
                                                        .OrderByDescending(x => ToCGy(x.DosePerFraction) * x.NumberOfFractions).FirstOrDefault();
                    if (t != null)
                    {
                        rx.TargetId = t.TargetId;
                        rx.DosePerFractionCGy = ToCGy(t.DosePerFraction);
                        rx.NumberOfFractions = t.NumberOfFractions;
                        rx.FromRTPrescription = true;
                        //prefer the plan target volume if the prescription target id is not a structure of the plan
                        if (p.StructureSet != null && !p.StructureSet.Structures.Any(s => string.Equals(s.Id, rx.TargetId, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(p.TargetVolumeID))
                            rx.TargetId = p.TargetVolumeID;
                        if (rx.IsValid) return rx;
                    }
                }
            }
            catch { }
            //2) the plan's own prescription
            try
            {
                rx.DosePerFractionCGy = ToCGy(p.DosePerFraction);
                rx.NumberOfFractions = p.NumberOfFractions ?? 0;
                rx.TargetId = p.TargetVolumeID ?? "";
                rx.FromRTPrescription = false;
            }
            catch { }
            return rx;
        }
    }
}

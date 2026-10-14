using AutoPlannerHelpers.Enums;
using System.Collections.Generic;

namespace AutoPlannerHelpers.Settings
{
    public class AutoPlannerHelperSettings
    {
        /// <summary>
        /// Create a DRR for every beam during beam placement (config: 'create DRRs=true/false', default false).
        /// In Eclipse 16.1 beams with a DRR can't be removed by ESAPI ("objects underneath which can not be deleted"),
        /// which breaks the automatic plan separation. With false, the DRRs are created manually on the final plans.
        /// </summary>
        public static bool CreateDRRs { get; set; } = false;
        public static Dictionary<string, EclipseDecodeKey> ContextKeyDictionary = new Dictionary<string, EclipseDecodeKey>
        {
            { "-m", EclipseDecodeKey.Patient },
            { "-s", EclipseDecodeKey.StructureSet },
            { "-i", EclipseDecodeKey.Image },
            { "-p", EclipseDecodeKey.Plan },
            { "-c", EclipseDecodeKey.Course }
        };
    }
}

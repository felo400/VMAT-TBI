using System.Reflection;
using System.Runtime.InteropServices;
using VMS.TPS.Common.Model.API;

[assembly: AssemblyTitle("VMATAutoPlanner.esapi")]
[assembly: AssemblyDescription("VMAT TBI/CSI/TMLI auto-planner (Eclipse 16.1 binary plugin)")]
[assembly: AssemblyProduct("VMATAutoPlanner")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.*")]
//write-enabled script (creates courses, plans, beams, structures and runs optimizations)
[assembly: ESAPIScript(IsWriteable = true)]

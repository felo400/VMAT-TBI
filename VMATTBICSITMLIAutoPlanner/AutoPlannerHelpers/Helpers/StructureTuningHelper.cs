using AutoPlannerHelpers.Context;
using AutoPlannerHelpers.Delegates;
using AutoPlannerHelpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace AutoPlannerHelpers.Helpers
{
    public static class StructureTuningHelper
    {
        public static List<string> GenerateStructureIdListPostUnion(List<string> ids)
        {
            //check if structures need to be unioned before adding defaults
            List<UnionStructureModel> structuresToUnion = new List<UnionStructureModel>(CheckStructuresToUnion(EclipseContext.GetInstance().StructureSet.Structures.Where(x => !x.IsEmpty).Select(x => x.Id)));
            ids.AddRange(structuresToUnion.Select(x => x.ProposedUnionStructureId));
            return ids;
        }

        /// <summary>
        /// Helper method to look through the structure set and identify a list of left and right structures that should be unioned together
        /// </summary>
        /// <param name="selectedSS"></param>
        /// <returns></returns>
        public static List<UnionStructureModel> CheckStructuresToUnion(IEnumerable<string> structureIds)
        {
            //left structure, right structure, unioned structure name
            List<UnionStructureModel> structuresToUnion = new List<UnionStructureModel> { };
            List<string> LStructs = structureIds.Where(x => x.Length > 2 && (x.Substring(x.Length - 2, 2).ToLower() == "_l" || x.Substring(x.Length - 2, 2).ToLower() == " l")).ToList();
            List<string> RStructs = structureIds.Where(x => x.Length > 2 && (x.Substring(x.Length - 2, 2).ToLower() == "_r" || x.Substring(x.Length - 2, 2).ToLower() == " r")).ToList();
            foreach (string itr in LStructs)
            {
                string RStruct = RStructs.FirstOrDefault(x => x.Substring(0, x.Length - 2) == itr.Substring(0, itr.Length - 2));
                string newName = AddProperEndingToName(itr.Substring(0, itr.Length - 2).ToLower());
                if (!string.IsNullOrEmpty(RStruct) && !structureIds.Any(x => string.Equals(x.ToLower(), newName.ToLower())))
                {
                    structuresToUnion.Add(new UnionStructureModel(itr, RStruct, newName));
                }
            }
            //pares en espanol "Der"/"Izq" (Pulmon Der + Pulmon Izq -> Pulmones). Solo organos bilaterales conocidos
            string[] spanishBases = { "pulmon", "ojo", "rinon", "cristalino", "parotida", "coclea" };
            foreach (string izq in structureIds.Where(x => x.Length > 4 && x.ToLower().EndsWith(" izq")))
            {
                string baseName = izq.Substring(0, izq.Length - 4).Trim();
                string der = structureIds.FirstOrDefault(x => x.Length > 4 && x.ToLower().EndsWith(" der") && string.Equals(x.Substring(0, x.Length - 4).Trim(), baseName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(der)) continue;
                string normBase = NormalizeStructureId(baseName);
                if (!spanishBases.Contains(normBase)) continue;
                string last = baseName.Substring(baseName.Length - 1).ToLower();
                string newName = ("aeiou".Contains(last) ? baseName + "s" : baseName + "es");
                if (newName.Length > 16) continue;
                if (!structureIds.Any(x => string.Equals(x, newName, StringComparison.OrdinalIgnoreCase)) && !structuresToUnion.Any(x => string.Equals(x.ProposedUnionStructureId, newName, StringComparison.OrdinalIgnoreCase)))
                {
                    structuresToUnion.Add(new UnionStructureModel(izq, der, newName));
                }
            }
            return structuresToUnion;
        }

        /// <summary>
        /// Simple helper method to add the proper ending to the unioned structure name
        /// </summary>
        /// <param name="initName"></param>
        /// <returns></returns>
        private static string AddProperEndingToName(string initName)
        {
            string unionedName;
            if (initName.Substring(initName.Length - 1, 1) == "y" && initName.Substring(initName.Length - 2, 2) != "ey") unionedName = initName.Substring(0, initName.Length - 1) + "ies";
            else if (initName.Substring(initName.Length - 1, 1) == "s") unionedName = initName + "es";
            else unionedName = initName + "s";
            return unionedName;
        }

        /// <summary>
        /// Helper method to union all identified left and right structures
        /// </summary>
        /// <returns></returns>
        public static bool UnionLRStructures(ProvideUIUpdateDelegate ProvideUIUpdate)
        {
            ProvideUIUpdate(0, "Checking for L and R structures to union!");
            List<UnionStructureModel> structuresToUnion = CheckStructuresToUnion(EclipseContext.GetInstance().StructureSet.Structures.Where(x => !x.IsEmpty).Select(x => x.Id));
            if (structuresToUnion.Any())
            {
                int calcItems = structuresToUnion.Count;
                int numUnioned = 0;
                foreach (UnionStructureModel itr in structuresToUnion)
                {
                    //union de estructuras bilaterales: es una ayuda, no debe detener el flujo si falla
                    Structure newStructure = GetStructureFromId(itr.ProposedUnionStructureId, true);
                    if (newStructure == null)
                    {
                        ProvideUIUpdate(0, $"Aviso: no se pudo crear {itr.ProposedUnionStructureId}. Se omite la union de {itr.Structure_Left} + {itr.Structure_Right}");
                        continue;
                    }
                    if (ContourHelper.PerformStructureOperation(new StructureOperationModel(itr.Structure_Left, Enums.StructureDerivationOperation.Union, itr.Structure_Right, itr.ProposedUnionStructureId)) || newStructure.IsEmpty)
                    {
                        ProvideUIUpdate(0, $"Aviso: la union {itr.Structure_Left} + {itr.Structure_Right} -> {itr.ProposedUnionStructureId} fallo o quedo vacia. Se continua");
                        continue;
                    }
                    ProvideUIUpdate(100* (++numUnioned / calcItems), $"Unioned {itr.ProposedUnionStructureId}");
                }
                ProvideUIUpdate(100, "Structures unioned successfully!");
            }
            else ProvideUIUpdate(100, "No structures to union!");
            return false;
        }

        /// <summary>
        /// Super helpful method to return the first structure with Id matching the supplied Id from the structure set
        /// </summary>
        /// <param name="id"></param>
        /// <param name="selectedSS"></param>
        /// <param name="createIfEmpty"></param>
        /// <returns></returns>
        public static Structure GetStructureFromId(string id, bool createIfEmpty = false, string dcmType = "")
        {
            if (!EclipseContext.GetInstance().IsInitialized || ReferenceEquals(EclipseContext.GetInstance().StructureSet, null))
            {
                throw new Exception("Error! Eclipse context not initialized! Unable to retrieve structure object from structure set!");
            }
            Structure theStructure = null;
            if (DoesStructureExistInSS(id))
            {
                theStructure = FindStructureWithAlias(id, false);
            }
            else if (createIfEmpty)
            {
                //DICOM types
                //Possible values are "AVOIDANCE", "CAVITY", "CONTRAST_AGENT", "CTV", "EXTERNAL", "GTV", "IRRAD_VOLUME", 
                //"ORGAN", "PTV", "TREATED_VOLUME", "SUPPORT", "FIXATION", "CONTROL", and "DOSE_REGION". 
                if(string.IsNullOrEmpty(dcmType))
                {
                    //try and figure out the dcm type based on supplied structure id
                    dcmType = "CONTROL";
                    if(!id.ToLower().StartsWith("ts_") && !id.ToLower().StartsWith("_"))
                    {
                        if (id.ToLower().Contains("gtv")) dcmType = "GTV";
                        else if (id.ToLower().Contains("ctv")) dcmType = "CTV";
                        else if (id.ToLower().Contains("ptv")) dcmType = "PTV";
                    }
                }
                if (EclipseContext.GetInstance().StructureSet.CanAddStructure(dcmType, id))
                {
                    theStructure = EclipseContext.GetInstance().StructureSet.AddStructure(dcmType, id);
                }
            }
            return theStructure;
        }

        public static List<Structure> GetStructuresFromIdList(IEnumerable<string> ids, bool returnNonNullStructuresOnly = false)
        {
            List<Structure> theStructures = new List<Structure> { };
            foreach (string itr in ids)
            {
                Structure s = GetStructureFromId(itr);
                if(returnNonNullStructuresOnly)
                {
                    if(!ReferenceEquals(s, null)) theStructures.Add(s);
                }
                else theStructures.Add(s);
            }
            return theStructures;
        }

        /// <summary>
        /// Super helpful method to determine if the supplied structur ids exists in the structure set
        /// </summary>
        /// <param name="id"></param>
        /// <param name="selectedSS"></param>
        /// <param name="checkIsEmpty"></param>
        /// <returns></returns>
        public static bool DoesStructureExistInSS(string id, bool checkIsEmpty = false)
        {
            if (!EclipseContext.GetInstance().IsInitialized || ReferenceEquals(EclipseContext.GetInstance().StructureSet, null))
            {
                throw new Exception("Error! Eclipse context not initialized! Unable to determine if structure exists in structure set!");
            }
            if (string.IsNullOrEmpty(id)) return false;
            return FindStructureWithAlias(id, checkIsEmpty) != null;
        }

        /// <summary>
        /// Helper method to determine if any of the supplied structure ids exist in the structure set
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="selectedSS"></param>
        /// <param name="checkIsEmpty"></param>
        /// <returns></returns>
        public static bool DoesStructureExistInSS(IEnumerable<string> ids, bool checkIsEmpty = false)
        {
            if (!EclipseContext.GetInstance().IsInitialized || ReferenceEquals(EclipseContext.GetInstance().StructureSet, null))
            {
                throw new Exception("Error! Eclipse context not initialized! Unable to determine if structure exists in structure set!");
            }
            foreach (string itr in ids)
            {
                if (string.IsNullOrEmpty(itr)) continue;
                if (FindStructureWithAlias(itr, checkIsEmpty) != null) return true;
            }
            return false;
        }

        #region Sinonimos de nombres de estructuras (nomenclatura en espanol)
        /// <summary>
        /// Grupos de nombres equivalentes. Si el codigo (o una plantilla) pide una estructura que no existe con ese
        /// Id exacto, se busca cualquier otro nombre del mismo grupo. La comparacion ignora mayusculas, tildes,
        /// espacios, guiones y guiones bajos. Las estructuras auxiliares (_Brain, _Spine, TS_...) NO tienen sinonimos.
        /// </summary>
        private static readonly List<string[]> AliasGroups = new List<string[]>
        {
            new[] { "brain", "cerebro", "encefalo", "encephalon" },
            new[] { "spinalcord", "medula", "medulaespinal", "canalmedular", "canalespinal", "cord", "medulla", "spinalcanal" },
            new[] { "larynx", "laringe" },
            new[] { "brainstem", "tallocerebral", "tallo", "troncocerebral", "tronco" },
            new[] { "body", "external", "cuerpo", "contornoexterno" },
            new[] { "opticchiasm", "chiasm", "quiasma", "quiasmaoptico" },
            new[] { "heart", "corazon" },
            new[] { "liver", "higado" },
            new[] { "esophagus", "esofago" },
            new[] { "bladder", "vejiga" },
            new[] { "rectum", "recto" },
            new[] { "thyroid", "tiroides" },
            new[] { "lungs", "pulmones" },
            new[] { "eyes", "ojos", "globosoculares" },
            new[] { "kidneys", "rinones" },
            new[] { "lenses", "cristalinos" },
            new[] { "oralcavity", "cavidadoral", "boca" },
            new[] { "bowel", "cavintestinal", "intestino", "intestinos", "bowelbag" },
        };

        /// <summary>
        /// Normaliza un Id: minusculas, sin tildes y sin espacios, guiones ni guiones bajos
        /// </summary>
        public static string NormalizeStructureId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            string decomposed = id.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in decomposed)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (c == ' ' || c == '_' || c == '-' || c == '.') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Busca la estructura con el Id exacto (sin distinguir mayusculas). Si no existe, prueba los sinonimos.
        /// Devuelve null si no encuentra nada (o si checkIsEmpty y todas las candidatas estan vacias)
        /// </summary>
        public static Structure FindStructureWithAlias(string id, bool checkIsEmpty)
        {
            StructureSet ss = EclipseContext.GetInstance().StructureSet;
            if (ss == null || string.IsNullOrEmpty(id)) return null;
            List<Structure> all = ss.Structures.ToList();
            Structure exact = all.FirstOrDefault(x => string.Equals(id.ToLower(), x.Id.ToLower()) && (!checkIsEmpty || !x.IsEmpty));
            if (exact != null) return exact;
            //nunca usar sinonimos para estructuras auxiliares generadas por el programa
            if (id.StartsWith("_") || id.ToLower().StartsWith("ts_")) return null;

            string norm = NormalizeStructureId(id);
            string[] group = AliasGroups.FirstOrDefault(g => g.Contains(norm));
            if (group == null) return null;
            List<Structure> candidates = all.Where(x => !checkIsEmpty || !x.IsEmpty).ToList();
            //1) coincidencia exacta normalizada, en el orden del grupo
            foreach (string alias in group)
            {
                Structure s = candidates.FirstOrDefault(x => NormalizeStructureId(x.Id) == alias);
                if (s != null) return s;
            }
            //2) Id que empieza por el sinonimo (p. ej. "Medula Espinal 1"), excluyendo PRV, TS, OPT, PTV, CTV, etc.
            string[] excludedPrefixes = { "prv", "ts", "opt", "ptv", "ctv", "gtv", "itv", "z", "ring", "anillo" };
            foreach (string alias in group.Where(a => a.Length >= 4))
            {
                Structure s = candidates.FirstOrDefault(x =>
                {
                    string n = NormalizeStructureId(x.Id);
                    return n.StartsWith(alias) && !excludedPrefixes.Any(p => n.StartsWith(p) && !alias.StartsWith(p));
                });
                if (s != null) return s;
            }
            return null;
        }
        #endregion

        /// <summary>
        /// Simple method to determine if there is overlap between the supplied target and normal structures
        /// </summary>
        /// <param name="target"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static bool IsOverlap(Structure target, System.Windows.Media.Media3D.Point3DCollection normal)
        {
            return normal.Any(x => target.IsPointInsideSegment(new VMS.TPS.Common.Model.Types.VVector(x.X, x.Y, x.Z)));
        }
    }
}

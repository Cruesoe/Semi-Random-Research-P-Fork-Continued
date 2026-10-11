using System.Collections.Generic;
using Verse;

namespace CM_Semi_Random_Research
{
    // Node Research's foundation and emergence tags, read from its mod extensions and cached per def.
    public static class NodeResearch
    {
        private const int TagFoundation = 1;
        private const int TagEmergence = 2;

        private static readonly Dictionary<ResearchProjectDef, int> tagCache =
            new Dictionary<ResearchProjectDef, int>();

        private static int GetTags(ResearchProjectDef def)
        {
            if (def == null)
                return 0;
            if (tagCache.TryGetValue(def, out int cached))
                return cached;

            int tags = 0;
            List<DefModExtension> extensions = def.modExtensions;
            if (extensions != null)
            {
                for (int i = 0; i < extensions.Count; i++)
                {
                    DefModExtension extension = extensions[i];
                    if (extension == null)
                        continue;

                    string name = extension.GetType().Name;
                    if (name == "ResearchFoundationExtension")
                        tags |= TagFoundation;
                    else if (name == "EmergenceExtension")
                        tags |= TagEmergence;
                }
            }

            tagCache[def] = tags;
            return tags;
        }

        public static bool IsFoundationTech(ResearchProjectDef def)
        {
            return (GetTags(def) & TagFoundation) != 0;
        }

        public static bool IsEmergenceTech(ResearchProjectDef def)
        {
            return (GetTags(def) & TagEmergence) != 0;
        }
    }
}

using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace CM_Semi_Random_Research
{
    // The monolith dialog's "View research" option casts the Research tab window to the vanilla one.
    [HarmonyPatch]
    public static class Patch_Building_VoidMonolith_ViewResearch
    {
        private static MethodBase target;

        // Looked up here so a missing compiler-generated method skips this patch instead of aborting PatchAll.
        public static bool Prepare()
        {
            if (!ModsConfig.AnomalyActive)
                return false;

            Type nested = AccessTools.Inner(typeof(Building_VoidMonolith), "<>c");
            target = nested == null ? null : AccessTools.Method(nested, "<OpenActivatedDialog>b__65_1");
            if (target == null)
                Log.Warning("[Semi Random Research] Could not patch Void Monolith View Research.");
            return target != null;
        }

        public static MethodBase TargetMethod()
        {
            return target;
        }

        public static bool Prefix()
        {
            ResearchTabCompatibility.Open(ResearchTabDefOf.Anomaly);
            return false;
        }
    }
}

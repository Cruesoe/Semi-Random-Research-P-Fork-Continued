using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace CM_Semi_Random_Research
{
    [HarmonyPatch(typeof(Alert_NeedAnomalyProject), "OnClick")]
    public static class Patch_Alert_NeedAnomalyProject_OnClick
    {
        public static bool Prepare()
        {
            return ModsConfig.AnomalyActive;
        }

        public static bool Prefix()
        {
            ResearchTabCompatibility.Open(ResearchTabDefOf.Anomaly);
            return false;
        }
    }

    // Drops paused categories from the missing list, since paused research has its own alert.
    [HarmonyPatch(typeof(Alert_NeedAnomalyProject), nameof(Alert_NeedAnomalyProject.GetReport))]
    public static class Patch_Alert_NeedAnomalyProject_GetReport
    {
        private static readonly AccessTools.FieldRef<Alert_NeedAnomalyProject, List<KnowledgeCategoryDef>> MissingCategories =
            AccessTools.FieldRefAccess<Alert_NeedAnomalyProject, List<KnowledgeCategoryDef>>("missingProjectCategories");

        public static bool Prepare()
        {
            return ModsConfig.AnomalyActive;
        }

        public static void Postfix(Alert_NeedAnomalyProject __instance, ref AlertReport __result)
        {
            if (!__result.active)
                return;

            ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
            if (tracker == null || !tracker.ResearchPaused)
                return;

            List<KnowledgeCategoryDef> missing = MissingCategories(__instance);
            missing.RemoveAll(category => tracker.IsPaused(ResearchTracker.GetCategoryKey(category)));
            __result = missing.Count > 0;
        }
    }
}

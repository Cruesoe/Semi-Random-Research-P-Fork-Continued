using HarmonyLib;
using RimWorld;
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

    // Paused research has its own alert.
    [HarmonyPatch(typeof(Alert_NeedAnomalyProject), nameof(Alert_NeedAnomalyProject.GetReport))]
    public static class Patch_Alert_NeedAnomalyProject_GetReport
    {
        public static bool Prepare()
        {
            return ModsConfig.AnomalyActive;
        }

        public static bool Prefix(ref AlertReport __result)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
                return true;

            ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
            if (tracker == null || !tracker.ResearchPaused)
                return true;

            __result = AlertReport.Inactive;
            return false;
        }
    }
}

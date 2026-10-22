using HarmonyLib;
using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    // A paused standard project has its own alert.
    [HarmonyPatch(typeof(Alert_NeedResearchProject), nameof(Alert_NeedResearchProject.GetReport))]
    public static class Patch_Alert_NeedResearchProject_GetReport
    {
        [HarmonyPrefix]
        public static bool Prefix(ref AlertReport __result)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
                return true;

            ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
            if (tracker == null || !tracker.IsPaused("Standard"))
                return true;

            __result = AlertReport.Inactive;
            return false;
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    [HarmonyPatch(typeof(MainTabWindow_Research), "AttemptBeginResearch")]
    public static class Patch_MainTabWindow_Research_AttemptBeginResearch
    {
        [HarmonyPrefix]
        public static bool Prefix(ResearchProjectDef projectToStart)
        {
            if (projectToStart == null || !SemiRandomResearchUtility.IsControllingResearchSelection)
                return true;

            if (ResearchTracker.ApplyingTrackedProject)
                return true;

            SemiRandomResearchUtility.RejectSelection();
            return false;
        }
    }
}

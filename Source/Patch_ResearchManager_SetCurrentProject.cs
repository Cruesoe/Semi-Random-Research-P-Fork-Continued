using HarmonyLib;
using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    // The gate every research UI passes through while "Prohibit normal project selection" is on.
    // This mod's own window and bookkeeping go through ResearchTracker.SetVanillaProjectUngated.
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject), new[] { typeof(ResearchProjectDef) })]
    public static class Patch_ResearchManager_SetCurrentProject
    {
        [HarmonyPrefix]
        public static bool Prefix(ResearchProjectDef proj)
        {
            if (proj == null || !SemiRandomResearchUtility.IsControllingResearchSelection)
                return true;

            if (ResearchTracker.ApplyingTrackedProject)
                return true;

            SemiRandomResearchUtility.RejectSelection();
            return false;
        }
    }
}

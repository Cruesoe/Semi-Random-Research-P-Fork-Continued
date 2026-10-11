using HarmonyLib;
using RimWorld;

namespace CM_Semi_Random_Research
{
    // Tree mods open the Research tab from their own FinishProject postfix. While this mod owns the tab, only "Auto-open on completion" decides.
    [HarmonyPatch(typeof(MainTabsRoot), nameof(MainTabsRoot.SetCurrentTab))]
    public static class Patch_MainTabsRoot_SetCurrentTab
    {
        [HarmonyPrefix]
        public static bool Prefix(MainButtonDef tab)
        {
            if (tab == null || tab != MainButtonDefOf.Research)
                return true;
            if (!Patch_ResearchManager_FinishProject.InsideFinishProject)
                return true;

            SemiRandomResearchSettings settings = SemiRandomResearchMod.settings;
            if (settings == null || settings.autoOpenOnCompletion)
                return true;

            return !ResearchTabWindowSwitcher.SemiRandomOwnsResearchTab;
        }
    }
}

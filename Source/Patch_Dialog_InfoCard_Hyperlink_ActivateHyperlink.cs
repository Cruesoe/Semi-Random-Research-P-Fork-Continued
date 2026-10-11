using HarmonyLib;
using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    // Research hyperlinks open whichever window owns the Research tab.
    [HarmonyPatch(typeof(Dialog_InfoCard.Hyperlink), nameof(Dialog_InfoCard.Hyperlink.ActivateHyperlink))]
    public static class Patch_Dialog_InfoCard_Hyperlink_ActivateHyperlink
    {
        public static bool Prefix(Quest ___quest, Ideo ___ideo, ResearchProjectDef ___researchProject)
        {
            if (___ideo != null || ___quest != null || ___researchProject == null)
                return true;

            ResearchTabCompatibility.Open(project: ___researchProject);
            return false;
        }
    }
}

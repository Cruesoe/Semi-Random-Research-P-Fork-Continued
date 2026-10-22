using HarmonyLib;
using RimWorld;

namespace CM_Semi_Random_Research
{
    [HarmonyPatch(typeof(Alert_NeedResearchProject), "OnClick")]
    public static class Patch_Alert_NeedResearchProject_OnClick
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            ResearchTabCompatibility.Open(ResearchTabDefOf.Main);
            return false;
        }
    }
}

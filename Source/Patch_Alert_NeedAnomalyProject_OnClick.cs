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
}

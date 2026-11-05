using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace CM_Semi_Random_Research
{
    // Research Reinvented writes progress straight to the research manager, bypassing AddProgress; offer the project here too.
    [HarmonyPatch]
    public static class Patch_ResearchOpportunity_ResearchPerformed
    {
        private static MethodBase target;

        // Skipped unless Research Reinvented is loaded.
        public static bool Prepare()
        {
            Type opportunity = AccessTools.TypeByName("PeteTimesSix.ResearchReinvented.Opportunities.ResearchOpportunity");
            if (opportunity == null)
                return false;

            target = AccessTools.Method(opportunity, "ResearchPerformed");
            if (target == null || AccessTools.Field(opportunity, "project")?.FieldType != typeof(ResearchProjectDef))
            {
                Log.Warning("[Semi Random Research] Research Reinvented's research progress wasn't found; its progress won't add projects to the offered choices.");
                target = null;
            }
            return target != null;
        }

        public static MethodBase TargetMethod()
        {
            return target;
        }

        [HarmonyPrefix]
        public static void Prefix(ResearchProjectDef ___project)
        {
            Patch_ResearchManager_AddProgress.OfferOnProgress(___project);
        }
    }
}

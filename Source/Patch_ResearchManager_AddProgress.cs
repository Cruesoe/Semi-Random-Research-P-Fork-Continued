using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace CM_Semi_Random_Research
{
    // Offers a project as an extra choice when it gains progress. Runs for every point of research, so the static checks come first.
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.AddProgress))]
    public static class Patch_ResearchManager_AddProgress
    {
        [HarmonyPrefix]
        public static void Prefix(ResearchProjectDef proj)
        {
            if (proj == null)
                return;

            SemiRandomResearchSettings settings = SemiRandomResearchMod.settings;
            if (settings == null || settings.progressAddsChoice == ProgressAddsChoice.Never)
                return;

            if (settings.progressAddsChoice != ProgressAddsChoice.AddChoiceOnlyOnGain && proj.ProgressReal != 0)
                return;

            ResearchTracker researchTracker = SemiRandomResearchUtility.Tracker;
            if (researchTracker == null)
                return;

            List<ResearchProjectDef> offers = researchTracker.PeekAvailableProjects();
            if (offers != null && offers.Contains(proj))
                return;

            if (!proj.CanStartNow)
                return;

            if (!settings.allowSwitchingResearch)
            {
                string typeKey = ResearchTracker.GetCategoryKey(proj);
                List<ResearchProjectDef> current = researchTracker.CurrentProject;
                for (int i = 0; i < current.Count; i++)
                {
                    if (current[i] != null && ResearchTracker.GetCategoryKey(current[i]) == typeKey)
                        return;
                }
            }

            researchTracker.AddProjectToAvailableProjects(proj);
        }
    }
}

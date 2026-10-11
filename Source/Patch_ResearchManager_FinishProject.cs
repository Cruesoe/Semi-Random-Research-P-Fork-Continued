using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace CM_Semi_Random_Research
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        private static bool isFinishingResearch;

        // Depth of nested FinishProject calls (unfinished prerequisites finish first), so the flag holds until the outermost returns.
        private static int finishDepth;

        public static bool InsideFinishProject => finishDepth > 0;

        [HarmonyPrefix]
        public static void Prefix(ResearchProjectDef proj, ref bool doCompletionDialog, Pawn researcher, ref bool doCompletionLetter)
        {
            finishDepth++;

            // The letter and auto-open settings replace vanilla's completion popup and letter.
            doCompletionDialog = false;
            doCompletionLetter = false;

            if (isFinishingResearch) return;
            isFinishingResearch = true;

            SemiRandomResearchUtility.Tracker?.ConsiderProjectFinished(proj);

            if (GenScene.InEntryScene || Current.Game == null || Current.Game.World == null ||
                Current.Game.World.worldObjects == null || LongEventHandler.AnyEventNowOrWaiting)
                return;

            if (SemiRandomResearchMod.settings.showCompletionLetter)
            {
                var letter = LetterMaker.MakeLetter(
                    "CM_Semi_Random_Research_LetterTitle".Translate(proj.LabelCap),
                    BuildCompletionLetterText(proj, researcher),
                    LetterDefOf.PositiveEvent,
                    researcher != null ? new LookTargets(researcher) : null);

                Find.LetterStack.ReceiveLetter(letter);
            }

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    if (!(Find.UIRoot is UIRoot_Play)) return;

                    if (SemiRandomResearchMod.settings.autoOpenOnCompletion)
                    {
                        Find.TickManager?.Pause();
                        Find.MainTabsRoot?.SetCurrentTab(MainButtonDefOf.Research);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[Semi Random Research] Error in queued UI update: {ex}");
                }
            });
        }

        [HarmonyFinalizer]
        public static void Finalizer()
        {
            isFinishingResearch = false;
            if (finishDepth > 0)
                finishDepth--;
        }

        // Plants a project unlocks for sowing are not in UnlockedDefs. Mapped once, on the first completion of a session.
        private static Dictionary<ResearchProjectDef, List<ThingDef>> sowUnlocksByProject;

        private static List<ThingDef> SowUnlocksFor(ResearchProjectDef proj)
        {
            if (sowUnlocksByProject == null)
            {
                sowUnlocksByProject = new Dictionary<ResearchProjectDef, List<ThingDef>>();
                List<ThingDef> allThings = DefDatabase<ThingDef>.AllDefsListForReading;
                for (int i = 0; i < allThings.Count; i++)
                {
                    ThingDef thingDef = allThings[i];
                    List<ResearchProjectDef> prerequisites = thingDef.plant?.sowResearchPrerequisites;
                    if (prerequisites == null)
                        continue;

                    for (int j = 0; j < prerequisites.Count; j++)
                    {
                        ResearchProjectDef prerequisite = prerequisites[j];
                        if (prerequisite == null)
                            continue;

                        if (!sowUnlocksByProject.TryGetValue(prerequisite, out List<ThingDef> unlocked))
                        {
                            unlocked = new List<ThingDef>();
                            sowUnlocksByProject[prerequisite] = unlocked;
                        }
                        if (!unlocked.Contains(thingDef))
                            unlocked.Add(thingDef);
                    }
                }
            }

            return sowUnlocksByProject.TryGetValue(proj, out List<ThingDef> result) ? result : null;
        }

        private static string BuildCompletionLetterText(ResearchProjectDef proj, Pawn researcher)
        {
            var rateInfo = SemiRandomResearchUtility.RateTracker?.GetResearchRateInfo(proj);

            StringBuilder letterText = new StringBuilder();
            letterText.AppendLine("CM_Semi_Random_Research_LetterCompleted".Translate(proj.LabelCap));

            if (rateInfo != null && rateInfo.TotalSamples > 0)
            {
                letterText.AppendLine();
                letterText.AppendLine("CM_Semi_Random_Research_LetterAverageRate".Translate(rateInfo.AverageRateFormatted));
            }

            if (researcher != null)
            {
                letterText.AppendLine();
                letterText.AppendLine("CM_Semi_Random_Research_LetterCompletedBy".Translate(researcher.LabelShort));
            }

            List<string> unlockedItems = new List<string>();

            List<Def> unlockedDefs = proj.UnlockedDefs;
            if (unlockedDefs != null)
            {
                for (int i = 0; i < unlockedDefs.Count; i++)
                    unlockedItems.Add(unlockedDefs[i].LabelCap);
            }

            List<ThingDef> sowUnlocks = SowUnlocksFor(proj);
            if (sowUnlocks != null)
            {
                for (int i = 0; i < sowUnlocks.Count; i++)
                {
                    string label = sowUnlocks[i].LabelCap;
                    if (!unlockedItems.Contains(label))
                        unlockedItems.Add(label);
                }
            }

            if (unlockedItems.Count > 0)
            {
                letterText.AppendLine();
                letterText.AppendLine("Unlocks".Translate() + ":");
                for (int i = 0; i < unlockedItems.Count; i++)
                    letterText.AppendLine($"  - {unlockedItems[i]}");
            }

            return letterText.ToString();
        }
    }
}

using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace CM_Semi_Random_Research
{
    // Vanilla finishes unfinished prerequisites first, through nested FinishProject calls. Each project is collected once
    // it is marked finished, and the whole chain is handed to the tracker when the outermost call returns.
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject
    {
        private static int finishDepth;

        // In completion order, so the requested project comes last.
        private static readonly List<ResearchProjectDef> finishedChain = new List<ResearchProjectDef>();

        public static bool InsideFinishProject => finishDepth > 0;

        [HarmonyPrefix]
        public static void Prefix(ref bool doCompletionDialog, ref bool doCompletionLetter)
        {
            finishDepth++;

            // The letter and auto-open settings replace vanilla's completion popup and letter.
            doCompletionDialog = false;
            doCompletionLetter = false;
        }

        [HarmonyPostfix]
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj != null && proj.IsFinished && !finishedChain.Contains(proj))
                finishedChain.Add(proj);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Pawn researcher)
        {
            if (finishDepth > 0)
                finishDepth--;
            if (finishDepth > 0 || finishedChain.Count == 0)
                return;

            List<ResearchProjectDef> chain = new List<ResearchProjectDef>(finishedChain);
            finishedChain.Clear();

            try
            {
                ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
                for (int i = 0; i < chain.Count; i++)
                    tracker?.ConsiderProjectFinished(chain[i]);

                // One letter and one auto-open for the project that was asked for.
                AnnounceCompletion(chain[chain.Count - 1], researcher);
            }
            catch (Exception ex)
            {
                Log.Error($"[Semi Random Research] Error handling finished research: {ex}");
            }
        }

        private static void AnnounceCompletion(ResearchProjectDef proj, Pawn researcher)
        {
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

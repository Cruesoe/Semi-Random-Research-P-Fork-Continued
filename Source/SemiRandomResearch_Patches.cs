using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    [StaticConstructorOnStartup]
    public static class MainTabWindow_Research_Patches
    {
        private static readonly Texture2D NextResearchButtonIcon = ContentFinder<Texture2D>.Get("UI/Buttons/MainButtons/CM_Semi_Random_Research_Random");

        [HarmonyPatch(typeof(MainTabWindow_Research))]
        [HarmonyPatch("DrawLeftRect", MethodType.Normal)]
        public static class MainTabWindow_Research_DrawLeftRect
        {
            [HarmonyPostfix]
            public static void Postfix(Rect leftOutRect, MainTabWindow_Research __instance)
            {
                float buttonSize = 32.0f;
                Rect buttonRect = new Rect(leftOutRect.xMax - buttonSize, leftOutRect.yMin, buttonSize, buttonSize);

                bool pressed = Widgets.ButtonImage(buttonRect, NextResearchButtonIcon);
                TooltipHandler.TipRegion(buttonRect, "CM_Semi_Random_Research_OpenSemiRandom".Translate());

                if (pressed)
                {
                    SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                    ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
                    Event.current.Use();
                }
            }
        }

        [HarmonyPatch(typeof(MainTabWindow_Research))]
        [HarmonyPatch("DrawStartButton", MethodType.Normal)]
        public static class MainTabWindow_Research_DrawStartButton
        {
            [HarmonyPrefix]
            public static void Prefix(List<string> ___lockedReasons)
            {
                ___lockedReasons.Clear();
                if (SemiRandomResearchUtility.IsControllingResearchSelection)
                {
                    ___lockedReasons.Add("CM_Semi_Random_Research_Active");
                }
            }

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo selectedProjectFieldInfo = typeof(RimWorld.MainTabWindow_Research).GetField("selectedProject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                MethodInfo canStartNowMethodInfo = AccessTools.Method(typeof(Verse.ResearchProjectDef), "get_CanStartNow");
                MethodInfo replacementCanStartCheck = AccessTools.Method(typeof(SemiRandomResearchUtility), nameof(SemiRandomResearchUtility.CanSelectNormalResearchNow));
                MethodInfo isCurrentProjectMethodInfo = AccessTools.Method(typeof(ResearchManager), "IsCurrentProject");
                MethodInfo replacementIsCurrentProject = AccessTools.Method(typeof(SemiRandomResearchUtility), nameof(SemiRandomResearchUtility.IsCurrentProject));

                MethodInfo clearListMethodInfo = AccessTools.Method(new List<string>().GetType(), "Clear");
                FieldInfo lockedReasonsFieldInfo = typeof(RimWorld.MainTabWindow_Research).GetField("lockedReasons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                List<CodeInstruction> instructionList = instructions.ToList();

                for (int i = 2; i < instructionList.Count; ++i)
                {
                    if (instructionList[i - 2].IsLdarg() &&
                        instructionList[i - 1].LoadsField(selectedProjectFieldInfo) &&
                        instructionList[i - 0].Calls(canStartNowMethodInfo))
                    {
                        instructionList[i - 0] = new CodeInstruction(OpCodes.Call, replacementCanStartCheck);
                    }

                    if (i > 5)
                    {
                        if (instructionList[i - 5].IsLdarg() &&
                            instructionList[i - 4].LoadsField(selectedProjectFieldInfo) &&
                            instructionList[i - 3].Calls(isCurrentProjectMethodInfo) &&
                            instructionList[i - 0].LoadsConstant("StopResearch"))
                        {
                            instructionList[i - 6].opcode = OpCodes.Nop;
                            instructionList[i - 3] = new CodeInstruction(OpCodes.Call, replacementIsCurrentProject);
                        }
                    }

                    if (
                        instructionList[i - 1].LoadsField(lockedReasonsFieldInfo) &&
                        instructionList[i - 0].Calls(clearListMethodInfo))
                    {
                        instructionList[i - 1].opcode = OpCodes.Nop;
                        instructionList[i - 0].opcode = OpCodes.Nop;
                    }
                }

                foreach (CodeInstruction instruction in instructionList)
                {
                    yield return instruction;
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class ResearchManager_Patches
    {
        private static float lastRejectionMessageTime = -99f;

        // A tree with a research queue can retry its selection every frame. Throttled on real
        // time because the game is usually paused while the research tab is open.
        internal static void RejectSelection()
        {
            if (Time.realtimeSinceStartup - lastRejectionMessageTime < 1.5f)
                return;

            lastRejectionMessageTime = Time.realtimeSinceStartup;
            Messages.Message("CM_Semi_Random_Research_Active".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        [HarmonyPatch(typeof(ResearchManager))]
        [HarmonyPatch("FinishProject", MethodType.Normal)]
        public static class ResearchManager_FinishProject
        {
            public static bool isFinishingResearch = false;

            [HarmonyPrefix]
            public static void Prefix(ResearchProjectDef proj, ref bool doCompletionDialog, Pawn researcher, ref bool doCompletionLetter)
            {
                if (!SemiRandomResearchMod.settings.featureEnabled)
                {
                    if (!isFinishingResearch)
                    {
                        isFinishingResearch = true;
                        SemiRandomResearchUtility.Tracker?.ConsiderProjectFinished(proj);
                    }
                    return;
                }

                doCompletionDialog = false;
                doCompletionLetter = false;

                if (isFinishingResearch) return;
                isFinishingResearch = true;

                ResearchTracker researchTracker = SemiRandomResearchUtility.Tracker;
                if (researchTracker != null)
                {
                    researchTracker.ConsiderProjectFinished(proj);
                }

                if (Verse.GenScene.InEntryScene || Current.Game == null || Current.Game.World == null ||
                    Current.Game.World.worldObjects == null || LongEventHandler.AnyEventNowOrWaiting)
                    return;

                // Building the letter walks every ThingDef looking for sow prerequisites, so none
                // of it runs when the letter is switched off.
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
                        if (Find.UIRoot == null || !(Find.UIRoot is UIRoot_Play)) return;

                        if (SemiRandomResearchMod.settings.autoOpenOnCompletion)
                        {
                            Find.TickManager?.Pause();
                            if (Find.MainTabsRoot != null)
                            {
                                Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Semi Random Research] Error in queued UI update: {ex}");
                    }
                });
            }

            // Plants that a project unlocks for sowing are not in UnlockedDefs, so they used to be
            // found by walking every ThingDef on each completion. The mapping is fixed once defs
            // are loaded, so it is built once on the first completion of a session instead.
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

            [HarmonyFinalizer]
            public static void Finalizer()
            {
                isFinishingResearch = false;
            }
        }

        [HarmonyPatch(typeof(ResearchManager))]
        [HarmonyPatch(nameof(ResearchManager.SetCurrentProject))]
        [HarmonyPatch(new[] { typeof(ResearchProjectDef) })]
        public static class ResearchManager_SetCurrentProject
        {
            // The gate every research UI passes through. With "Prohibit normal project selection"
            // on, the Semi-Random tab is the only place research is chosen - the vanilla and Sleek
            // trees already have their start button hard-locked by the DrawStartButton patch, and
            // this makes Node Research, Nice Research Tab and anything else behave the same way.
            // Our own window and the tracker's bookkeeping go through SetVanillaProjectUngated.
            [HarmonyPrefix]
            public static bool Prefix(ResearchProjectDef proj)
            {
                if (proj == null || !SemiRandomResearchUtility.IsControllingResearchSelection)
                {
                    return true;
                }

                if (ResearchTracker.ApplyingTrackedProject)
                {
                    return true;
                }

                RejectSelection();
                return false;
            }
        }

        [HarmonyPatch(typeof(ResearchManager))]
        [HarmonyPatch("AddProgress", MethodType.Normal)]
        public static class ResearchManager_AddProgress
        {
            // Runs for every point of research a pawn produces, so the cheap, purely static checks
            // are made first and the work that touches the world - the component lookup, the offer
            // list scan and CanStartNow - only happens for the rare call that can still qualify.
            [HarmonyPrefix]
            public static void Prefix(ResearchProjectDef proj, float amount, Pawn source)
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
                    List<ResearchProjectDef> current = researchTracker.CurrentProject;
                    for (int i = 0; i < current.Count; i++)
                    {
                        if (current[i] != null && current[i].knowledgeCategory == proj.knowledgeCategory)
                            return;
                    }
                }

                researchTracker.AddProjectToAvailableProjects(proj);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class Alert_NeedResearchProject_Patches
    {
        [HarmonyPatch(typeof(Alert_NeedResearchProject))]
        [HarmonyPatch("OnClick", MethodType.Normal)]
        public static class Alert_NeedResearchProject_OnClick
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                ResearchTabCompatibility.Open(ResearchTabDefOf.Main);
                return false;
            }
        }

        [HarmonyPatch(typeof(Alert_NeedResearchProject), nameof(Alert_NeedResearchProject.GetReport))]
        public static class Alert_NeedResearchProject_GetReport
        {
            [HarmonyPrefix]
            public static bool Prefix(ref AlertReport __result)
            {
                if (Current.ProgramState != ProgramState.Playing || Find.World == null)
                    return true;

                ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
                if (tracker == null || !tracker.ResearchPaused)
                    return true;

                __result = AlertReport.Inactive;
                return false;
            }
        }
    }

    [HarmonyPatch]
    public static class VoidMonolith_ViewResearch
    {
        public static bool Prepare()
        {
            return ModsConfig.AnomalyActive;
        }

        public static MethodBase TargetMethod()
        {
            Type nested = AccessTools.Inner(typeof(Building_VoidMonolith), "<>c");
            MethodBase method = nested == null ? null : AccessTools.Method(nested, "<OpenActivatedDialog>b__65_1");
            if (method == null)
                Log.Warning("[Semi Random Research] Could not patch Void Monolith View Research.");
            return method;
        }

        public static bool Prefix()
        {
            ResearchTabCompatibility.Open(ResearchTabDefOf.Anomaly);
            return false;
        }
    }

    [HarmonyPatch(typeof(Alert_NeedAnomalyProject), "OnClick")]
    public static class Alert_NeedAnomalyProject_OnClick
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

    [HarmonyPatch(typeof(Alert_NeedAnomalyProject), nameof(Alert_NeedAnomalyProject.GetReport))]
    public static class Alert_NeedAnomalyProject_GetReport
    {
        public static bool Prepare()
        {
            return ModsConfig.AnomalyActive;
        }

        public static bool Prefix(ref AlertReport __result)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
                return true;

            ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
            if (tracker == null || !tracker.ResearchPaused)
                return true;

            __result = AlertReport.Inactive;
            return false;
        }
    }

    [HarmonyPatch(typeof(Dialog_InfoCard.Hyperlink), nameof(Dialog_InfoCard.Hyperlink.ActivateHyperlink))]
    public static class Dialog_InfoCard_Hyperlink_ActivateHyperlink
    {
        public static bool Prefix(Quest ___quest, Ideo ___ideo, ResearchProjectDef ___researchProject)
        {
            if (___ideo != null || ___quest != null || ___researchProject == null)
                return true;

            ResearchTabCompatibility.Open(project: ___researchProject);
            return false;
        }
    }

    [HarmonyPatch(typeof(Dialog_EntityCodex), "LeftRect")]
    public static class Dialog_EntityCodex_LeftRect
    {
        public static bool Prepare()
        {
            return ModsConfig.AnomalyActive;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getTabWindow = AccessTools.PropertyGetter(typeof(MainButtonDef), nameof(MainButtonDef.TabWindow));
            MethodInfo select = AccessTools.Method(typeof(MainTabWindow_Research), nameof(MainTabWindow_Research.Select));
            MethodInfo helper = AccessTools.Method(typeof(ResearchTabCompatibility), nameof(ResearchTabCompatibility.SelectProject));
            FieldInfo researchButton = AccessTools.Field(typeof(MainButtonDefOf), nameof(MainButtonDefOf.Research));

            List<CodeInstruction> list = instructions.ToList();
            bool patched = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (i + 4 < list.Count &&
                    list[i].LoadsField(researchButton) &&
                    list[i + 1].Calls(getTabWindow) &&
                    list[i + 2].opcode == OpCodes.Castclass &&
                    list[i + 4].Calls(select))
                {
                    yield return list[i + 3];
                    yield return new CodeInstruction(OpCodes.Call, helper);
                    i += 4;
                    patched = true;
                    continue;
                }

                yield return list[i];
            }

            if (!patched)
                Log.Warning("[Semi Random Research] Could not patch Entity Codex research links.");
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "AttemptBeginResearch")]
    public static class MainTabWindow_Research_AttemptBeginResearch
    {
        [HarmonyPrefix]
            public static bool Prefix(ResearchProjectDef projectToStart)
            {
                if (projectToStart == null || !SemiRandomResearchUtility.IsControllingResearchSelection)
                    return true;

                if (ResearchTracker.ApplyingTrackedProject)
                    return true;

            ResearchManager_Patches.RejectSelection();
            return false;
        }
    }
}

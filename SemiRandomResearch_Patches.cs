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
                    ___lockedReasons.Add("Semi Random Research is active.");
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
                        Current.Game?.World?.GetComponent<ResearchTracker>()?.ConsiderProjectFinished(proj);
                    }
                    return;
                }

                doCompletionDialog = false;
                doCompletionLetter = false;

                if (isFinishingResearch) return;
                isFinishingResearch = true;

                ResearchTracker researchTracker = Current.Game?.World?.GetComponent<ResearchTracker>();
                if (researchTracker != null)
                {
                    researchTracker.ConsiderProjectFinished(proj);
                }

                if (Verse.GenScene.InEntryScene || Current.Game == null || Current.Game.World == null ||
                    Current.Game.World.worldObjects == null || LongEventHandler.AnyEventNowOrWaiting)
                    return;

                var rateTracker = Current.Game.World.GetComponent<ResearchRateTracker>();
                var rateInfo = rateTracker?.GetResearchRateInfo(proj);

                StringBuilder letterText = new StringBuilder();
                letterText.AppendLine($"Research completed: {proj.LabelCap}");

                if (rateInfo != null && rateInfo.TotalSamples > 0)
                {
                    letterText.AppendLine();
                    letterText.AppendLine($"Average rate: {rateInfo.AverageRateFormatted}");
                }

                if (researcher != null)
                {
                    letterText.AppendLine();
                    letterText.AppendLine($"Completed by: {researcher.LabelShort}");
                }

                List<string> unlockedItems = new List<string>();

                if (proj.UnlockedDefs != null)
                {
                    foreach (Def unlockedDef in proj.UnlockedDefs)
                    {
                        unlockedItems.Add(unlockedDef.LabelCap);
                    }
                }

                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (thingDef.plant != null && thingDef.plant.sowResearchPrerequisites != null)
                    {
                        if (thingDef.plant.sowResearchPrerequisites.Contains(proj))
                        {
                            if (!unlockedItems.Contains(thingDef.LabelCap))
                            {
                                unlockedItems.Add(thingDef.LabelCap);
                            }
                        }
                    }
                }

                if (unlockedItems.Count > 0)
                {
                    letterText.AppendLine();
                    letterText.AppendLine("Unlocks:");
                    foreach (string item in unlockedItems)
                    {
                        letterText.AppendLine($"  - {item}");
                    }
                }

                if (SemiRandomResearchMod.settings.showCompletionLetter)
                {
                    var letter = LetterMaker.MakeLetter(
                        $"Research Complete: {proj.LabelCap}",
                        letterText.ToString(),
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
            [HarmonyPrefix]
            public static bool Prefix(ResearchProjectDef proj)
            {
                if (proj == null || !SemiRandomResearchUtility.IsControllingResearchSelection)
                {
                    return true;
                }

                ResearchTracker tracker = Current.Game?.World?.GetComponent<ResearchTracker>();
                if (tracker == null || tracker.IsSelectableProject(proj))
                {
                    return true;
                }

                Messages.Message("Semi Random Research is active.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        [HarmonyPatch(typeof(ResearchManager))]
        [HarmonyPatch("AddProgress", MethodType.Normal)]
        public static class ResearchManager_AddProgress
        {
            [HarmonyPrefix]
            public static void Prefix(ResearchProjectDef proj, float amount, Pawn source)
            {
                ResearchTracker researchTracker = Current.Game?.World?.GetComponent<ResearchTracker>();
                if (researchTracker != null &&
                    (proj.ProgressReal == 0 || SemiRandomResearchMod.settings.progressAddsChoice == ProgressAddsChoice.AddChoiceOnlyOnGain) &&
                    SemiRandomResearchMod.settings.progressAddsChoice != ProgressAddsChoice.Never &&
                    !researchTracker.PeekAvailableProjects().Contains(proj) &&
                    proj.CanStartNow)
                {
                    if (!researchTracker.CurrentProject.Any(x => x.knowledgeCategory == proj.knowledgeCategory) ||
                        SemiRandomResearchMod.settings.allowSwitchingResearch)
                    {
                        researchTracker.AddProjectToAvailableProjects(proj);
                    }
                }
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
                if (SemiRandomResearchUtility.IsControllingResearchSelection)
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
                    return false;
                }
                return true;
            }
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

                ResearchTracker tracker = Current.Game?.World?.GetComponent<ResearchTracker>();
                if (tracker == null || tracker.IsSelectableProject(projectToStart))
                    return true;

            Messages.Message("Semi Random Research is active.", MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }
}

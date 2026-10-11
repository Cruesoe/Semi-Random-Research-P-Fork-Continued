using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace CM_Semi_Random_Research
{
    // Locks the vanilla (and Sleek) start and stop buttons while "Prohibit normal project selection" is on, and names the reason.
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawStartButton")]
    public static class Patch_MainTabWindow_Research_DrawStartButton
    {
        [HarmonyPrefix]
        public static void Prefix(List<string> ___lockedReasons)
        {
            ___lockedReasons.Clear();
            if (SemiRandomResearchUtility.IsControllingResearchSelection)
                ___lockedReasons.Add("CM_Semi_Random_Research_Active".Translate());
        }

        // Swaps CanStartNow and the stop button's IsCurrentProject for gated versions, and drops vanilla's own lockedReasons.Clear().
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo selectedProjectFieldInfo = AccessTools.Field(typeof(MainTabWindow_Research), "selectedProject");
            MethodInfo canStartNowMethodInfo = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CanStartNow));
            MethodInfo replacementCanStartCheck = AccessTools.Method(typeof(SemiRandomResearchUtility), nameof(SemiRandomResearchUtility.CanSelectNormalResearchNow));
            MethodInfo isCurrentProjectMethodInfo = AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.IsCurrentProject));
            MethodInfo replacementIsCurrentProject = AccessTools.Method(typeof(SemiRandomResearchUtility), nameof(SemiRandomResearchUtility.IsCurrentProject));
            MethodInfo clearListMethodInfo = AccessTools.Method(typeof(List<string>), nameof(List<string>.Clear));
            FieldInfo lockedReasonsFieldInfo = AccessTools.Field(typeof(MainTabWindow_Research), "lockedReasons");

            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 2; i < instructionList.Count; ++i)
            {
                if (instructionList[i - 2].IsLdarg() &&
                    instructionList[i - 1].LoadsField(selectedProjectFieldInfo) &&
                    instructionList[i - 0].Calls(canStartNowMethodInfo))
                {
                    instructionList[i - 0] = new CodeInstruction(OpCodes.Call, replacementCanStartCheck);
                }

                if (i > 5 &&
                    instructionList[i - 5].IsLdarg() &&
                    instructionList[i - 4].LoadsField(selectedProjectFieldInfo) &&
                    instructionList[i - 3].Calls(isCurrentProjectMethodInfo) &&
                    instructionList[i - 0].LoadsConstant("StopResearch"))
                {
                    instructionList[i - 6].opcode = OpCodes.Nop;
                    instructionList[i - 3] = new CodeInstruction(OpCodes.Call, replacementIsCurrentProject);
                }

                if (instructionList[i - 1].LoadsField(lockedReasonsFieldInfo) &&
                    instructionList[i - 0].Calls(clearListMethodInfo))
                {
                    instructionList[i - 1].opcode = OpCodes.Nop;
                    instructionList[i - 0].opcode = OpCodes.Nop;
                }
            }

            return instructionList;
        }
    }
}

using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace CM_Semi_Random_Research
{
    // Replaces the codex's cast of the Research tab window to the vanilla one with ResearchTabCompatibility.SelectProject.
    [HarmonyPatch(typeof(Dialog_EntityCodex), "LeftRect")]
    public static class Patch_Dialog_EntityCodex_LeftRect
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
}

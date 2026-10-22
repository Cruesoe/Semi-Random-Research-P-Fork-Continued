using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Adds a "switch to Semi Random Research" button to Node Research's graph controls.
    [StaticConstructorOnStartup]
    public static class NodeResearch_Integration
    {
        private static readonly Texture2D TexSemiRandom = ContentFinder<Texture2D>.Get("UI/semi", true);

        static NodeResearch_Integration()
        {
            if (!ResearchTabWindowSwitcher.NodeResearchInstalled)
                return;

            var type = ResearchTabWindowSwitcher.NodeResearchWindowType;
            if (type == null)
                return;

            var original = AccessTools.Method(type, "DrawGraphControls");
            if (original == null)
            {
                Log.Warning("[Semi Random Research] Node Research DrawGraphControls was not found. The swap button may be missing.");
                return;
            }

            var harmony = new Harmony("CM_Semi_Random_Research.NodeIntegration");
            var transpiler = AccessTools.Method(typeof(NodeResearch_Integration), nameof(DrawGraphControls_Transpiler));
            var postfix = AccessTools.Method(typeof(NodeResearch_Integration), nameof(DrawGraphControls_Postfix));
            harmony.Patch(original, transpiler: new HarmonyMethod(transpiler), postfix: new HarmonyMethod(postfix));

            NodeResearchAdvancement.TryPatch(harmony, type);
        }

        // Shifts Node Research's settings button 32px right to make room: the first Add after the "BRM_OpenVanillaMenu" tooltip.
        public static IEnumerable<CodeInstruction> DrawGraphControls_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool foundVanillaTip = false;
            bool shiftedSettings = false;

            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (!foundVanillaTip && instruction.opcode == OpCodes.Ldstr && instruction.operand is string str && str == "BRM_OpenVanillaMenu")
                    foundVanillaTip = true;

                if (foundVanillaTip && !shiftedSettings && instruction.opcode == OpCodes.Add)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 32f);
                    yield return new CodeInstruction(OpCodes.Add);
                    shiftedSettings = true;
                    foundVanillaTip = false;
                }
            }
        }

        public static void DrawGraphControls_Postfix(Rect controlAreaRect, Window __instance)
        {
            float btnSize = 24f;
            float btnGap = 8f;
            float xOffset = (btnSize + btnGap) * 2;

            Rect semiBtnRect = new Rect(controlAreaRect.x + xOffset, controlAreaRect.y, btnSize, btnSize);

            if (Widgets.ButtonImage(semiBtnRect, TexSemiRandom))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
                Event.current.Use();
            }
            TooltipHandler.TipRegion(semiBtnRect, "CM_Semi_Random_Research_OpenSemiRandom".Translate());
        }
    }
}

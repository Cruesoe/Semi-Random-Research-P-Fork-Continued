using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Adds a "Semi Random" button to Nice Research Tab's window. The click is taken in a prefix so it cannot also drag the canvas.
    [StaticConstructorOnStartup]
    public static class NiceResearchTab_Integration
    {
        private static readonly Color SemiRandomButtonColor = new Color(0.22f, 0.48f, 0.28f);
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 32f;
        private const float ButtonMargin = 12f;

        static NiceResearchTab_Integration()
        {
            if (!ResearchTabWindowSwitcher.NiceResearchTabInstalled)
                return;

            Type type = ResearchTabWindowSwitcher.NiceWindowType;
            if (type == null)
            {
                Log.Warning("[Semi Random Research] Nice Research Tab window type was not found. The swap button may be missing.");
                return;
            }

            MethodInfo original = AccessTools.Method(type, "DoWindowContents");
            if (original == null)
            {
                Log.Warning("[Semi Random Research] Nice Research Tab DoWindowContents was not found. The swap button may be missing.");
                return;
            }

            var harmony = new Harmony("CM_Semi_Random_Research.NiceResearchTabIntegration");
            harmony.Patch(
                original,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(NiceResearchTab_Integration), nameof(DoWindowContents_Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(NiceResearchTab_Integration), nameof(DoWindowContents_Postfix))));
        }

        // Bottom right: the only corner Nice Research Tab leaves free.
        private static Rect SemiRandomButtonRect(Rect canvas)
        {
            return new Rect(
                canvas.xMax - ButtonMargin - ButtonWidth,
                canvas.yMax - ButtonMargin - ButtonHeight,
                ButtonWidth,
                ButtonHeight);
        }

        public static void DoWindowContents_Prefix(Rect canvas, Window __instance)
        {
            if (!ResearchWidgets.Clicked(SemiRandomButtonRect(canvas)))
                return;

            SoundDefOf.Click.PlayOneShotOnCamera();
            ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
        }

        public static void DoWindowContents_Postfix(Rect canvas)
        {
            Rect rect = SemiRandomButtonRect(canvas);
            ResearchWidgets.DrawColoredButton(rect, "CM_Semi_Random_Research_YartButton".Translate(), SemiRandomButtonColor);
            if (Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, "CM_Semi_Random_Research_ReturnToSemiRandom".Translate());
        }
    }
}

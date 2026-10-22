using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Adds a "Semi Random" button to YART's window. The click is taken in a prefix so it cannot also drag the canvas.
    [StaticConstructorOnStartup]
    public static class Yart_Integration
    {
        private static readonly Color SemiRandomButtonColor = new Color(0.22f, 0.48f, 0.28f);
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 40f;
        private const float ButtonMargin = 16f;
        private const float ButtonTop = 36f;

        static Yart_Integration()
        {
            if (!ResearchTabWindowSwitcher.YartInstalled)
                return;

            Type type = ResearchTabWindowSwitcher.YartWindowType;
            if (type == null)
            {
                Log.Warning("[Semi Random Research] YART window type was not found. The swap button may be missing.");
                return;
            }

            MethodInfo original = AccessTools.Method(type, "DoWindowContents");
            if (original == null)
            {
                Log.Warning("[Semi Random Research] YART DoWindowContents was not found. The swap button may be missing.");
                return;
            }

            var harmony = new Harmony("CM_Semi_Random_Research.YartIntegration");
            harmony.Patch(
                original,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Yart_Integration), nameof(DoWindowContents_Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(Yart_Integration), nameof(DoWindowContents_Postfix))));
        }

        private static Rect SemiRandomButtonRect(Rect inRect)
        {
            return new Rect(inRect.xMax - ButtonMargin - ButtonWidth, ButtonTop, ButtonWidth, ButtonHeight);
        }

        public static void DoWindowContents_Prefix(Rect inRect, Window __instance)
        {
            if (!ResearchWidgets.Clicked(SemiRandomButtonRect(inRect)))
                return;

            SoundDefOf.Click.PlayOneShotOnCamera();
            ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
        }

        public static void DoWindowContents_Postfix(Rect inRect)
        {
            Rect rect = SemiRandomButtonRect(inRect);
            ResearchWidgets.DrawColoredButton(rect, "CM_Semi_Random_Research_YartButton".Translate(), SemiRandomButtonColor);
            if (Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, "CM_Semi_Random_Research_ReturnToSemiRandom".Translate());
        }
    }
}

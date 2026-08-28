using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Nice Research Tab support is opt-in: it only ever appears as an extra entry in the
    // "Tree button opens" dropdown. This adds the matching way back, so a player who sends
    // the footer button to Nice Research Tab is not stranded there.
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
            {
                return;
            }

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
            Log.Message("[Semi Random Research] Successfully integrated with Nice Research Tab UI.");
        }

        // Bottom right. Nice Research Tab keeps its description panel and Stop research button
        // down the left edge, the queue along the top, and the filter panel and close button in
        // the top right corner, which leaves this corner as the only reliably free one.
        private static Rect SemiRandomButtonRect(Rect canvas)
        {
            return new Rect(
                canvas.xMax - ButtonMargin - ButtonWidth,
                canvas.yMax - ButtonMargin - ButtonHeight,
                ButtonWidth,
                ButtonHeight);
        }

        // The click is consumed before the tree window sees it, the same way the YART
        // integration does, so it cannot double as a canvas drag.
        public static void DoWindowContents_Prefix(Rect canvas, Window __instance)
        {
            Rect buttonRect = SemiRandomButtonRect(canvas);
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0 || !Mouse.IsOver(buttonRect))
                return;

            Event.current.Use();
            SoundDefOf.Click.PlayOneShotOnCamera();
            ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
        }

        public static void DoWindowContents_Postfix(Rect canvas)
        {
            Rect rect = SemiRandomButtonRect(canvas);
            bool mouseOver = Mouse.IsOver(rect);

            if (Event.current.type == EventType.Repaint)
            {
                Color bg = mouseOver ? Color.Lerp(SemiRandomButtonColor, Color.white, 0.16f) : SemiRandomButtonColor;
                Widgets.DrawBoxSolid(rect, bg);

                Color old = GUI.color;
                GUI.color = Color.Lerp(SemiRandomButtonColor, Color.black, 0.4f);
                Widgets.DrawBox(rect);
                GUI.color = old;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(rect, "CM_Semi_Random_Research_YartButton".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (mouseOver)
                TooltipHandler.TipRegion(rect, "CM_Semi_Random_Research_ReturnToSemiRandom".Translate());
        }
    }
}

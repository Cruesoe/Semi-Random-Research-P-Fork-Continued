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
        // Nice Research Tab's theme: panel grey #1E2124 and its green accent #4AF85E.
        private static readonly Color PanelColor = new Color(30f / 255f, 33f / 255f, 36f / 255f);
        private static readonly Color AccentColor = new Color(74f / 255f, 248f / 255f, 94f / 255f);
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 32f;
        private const float ButtonMargin = 12f;
        // Nice Research Tab's outer margin plus its fixed-width Filter column.
        private const float FilterColumnWidth = 8f + 180f;

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

        // Bottom right of the tree area, left of the Filter column.
        private static Rect SemiRandomButtonRect(Rect canvas)
        {
            return new Rect(
                canvas.xMax - FilterColumnWidth - ButtonMargin - ButtonWidth,
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
            bool mouseOver = Mouse.IsOver(rect);
            if (Event.current.type == EventType.Repaint)
                DrawButtonFace(rect, mouseOver);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(rect, "CM_Semi_Random_Research_YartButton".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            if (mouseOver)
                TooltipHandler.TipRegion(rect, "CM_Semi_Random_Research_ReturnToSemiRandom".Translate());
        }

        // Dark panel with a faint green tint and outline, both brighter on hover, as Nice Research Tab's own buttons.
        private static void DrawButtonFace(Rect rect, bool mouseOver)
        {
            Widgets.DrawBoxSolid(rect, Color.Lerp(PanelColor, AccentColor, mouseOver ? 0.2f : 0.1f));

            Color border = AccentColor;
            border.a = mouseOver ? 0.5f : 0.2f;
            GUI.color = border;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }
    }
}

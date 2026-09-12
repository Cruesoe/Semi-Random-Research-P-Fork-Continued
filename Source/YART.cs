using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
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
            {
                return;
            }

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
            Log.Message("[Semi Random Research] Successfully integrated with YART UI.");
        }

        private static Rect SemiRandomButtonRect(Rect inRect)
        {
            return new Rect(inRect.xMax - ButtonMargin - ButtonWidth, ButtonTop, ButtonWidth, ButtonHeight);
        }

        public static void DoWindowContents_Prefix(Rect inRect, Window __instance)
        {
            Rect buttonRect = SemiRandomButtonRect(inRect);
            if (!Clicked(buttonRect))
                return;

            SoundDefOf.Click.PlayOneShotOnCamera();
            ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
            Event.current.Use();
        }

        public static void DoWindowContents_Postfix(Rect inRect)
        {
            DrawColoredButton(SemiRandomButtonRect(inRect), "CM_Semi_Random_Research_YartButton".Translate(), SemiRandomButtonColor);
        }

        private static void DrawColoredButton(Rect rect, string label, Color fill)
        {
            bool mouseOver = Mouse.IsOver(rect);
            bool held = mouseOver && Input.GetMouseButton(0);

            if (Event.current.type == EventType.Repaint)
            {
                Color bg = fill;
                if (held)
                    bg = fill * 0.75f;
                else if (mouseOver)
                    bg = Color.Lerp(fill, Color.white, 0.16f);

                Widgets.DrawBoxSolid(rect, bg);
                Color old = GUI.color;
                GUI.color = Color.Lerp(fill, Color.black, 0.4f);
                Widgets.DrawBox(rect);
                GUI.color = old;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (Event.current.type == EventType.Repaint && mouseOver)
                TooltipHandler.TipRegion(rect, "CM_Semi_Random_Research_ReturnToSemiRandom".Translate());
        }

        private static bool Clicked(Rect rect)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
                return false;
            if (!Mouse.IsOver(rect))
                return false;
            Event.current.Use();
            return true;
        }
    }
}

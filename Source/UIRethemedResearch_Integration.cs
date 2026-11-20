using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Adds a "Semi Random" button to UI Rethemed - Research's window, which replaces the vanilla tree's drawing (and with it the
    // info pane button). The click is taken in a prefix, before UI Rethemed's own prefix, so it cannot also drag the tree.
    [HarmonyPatch(typeof(MainTabWindow_Research), nameof(MainTabWindow_Research.DoWindowContents))]
    public static class UIRethemedResearch_Integration
    {
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 32f;
        // UI Rethemed's 6px window padding plus a gap inside its tree view's bottom-left corner.
        private const float ButtonInset = 6f + 12f;
        // Used only if Core's look failed this session.
        private static readonly Color FallbackFill = new Color(0.2f, 0.3f, 0.2f);

        // UI Rethemed turns the window background off while it draws the window itself, and back on when it falls back to vanilla.
        private static bool Themed(MainTabWindow_Research window) =>
            ResearchTabWindowSwitcher.UIRethemedInstalled && !window.doWindowBackground;

        private static Rect ButtonRect(Rect inRect) =>
            new Rect(inRect.x + ButtonInset, inRect.yMax - ButtonInset - ButtonHeight, ButtonWidth, ButtonHeight);

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Rect inRect, MainTabWindow_Research __instance)
        {
            if (!Themed(__instance) || !ResearchWidgets.Clicked(ButtonRect(inRect)))
                return;

            SoundDefOf.Click.PlayOneShotOnCamera();
            ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix(Rect inRect, MainTabWindow_Research __instance)
        {
            if (!Themed(__instance))
                return;

            Rect rect = ButtonRect(inRect);
            string label = "CM_Semi_Random_Research_YartButton".Translate();
            ResearchSkin skin = ResearchSkin.Loaded;
            if (skin == null || !skin.Button(rect, label, SkinTone.Green, true))
                ResearchWidgets.DrawColoredButton(rect, label, FallbackFill);

            if (Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, "CM_Semi_Random_Research_ReturnToSemiRandom".Translate());
        }
    }
}

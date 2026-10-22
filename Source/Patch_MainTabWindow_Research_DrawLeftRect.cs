using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Button in the top right of the vanilla tree's info pane that switches back to this mod's window.
    // Research: Organized finds this class by name to place its settings button beside it, so keep the name.
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawLeftRect")]
    public static class Patch_MainTabWindow_Research_DrawLeftRect
    {
        // Loaded on first draw: PatchAll runs off the main thread, where textures cannot load.
        private static Texture2D nextResearchButtonIcon;

        private const float ButtonSize = 32.0f;

        // Set when the prefix claims a click; the postfix then switches windows once the pane has drawn.
        private static bool clickClaimed;

        private static Rect ButtonRect(Rect leftOutRect) =>
            new Rect(leftOutRect.xMax - ButtonSize, leftOutRect.yMin, ButtonSize, ButtonSize);

        // Claims the mouse-down before other mods' prefixes draw their own buttons in the same corner
        // (Tech Advancing put a settings button there, so clicks opened its settings instead).
        // A plain rect test, like a vanilla button: Mouse.IsOver also rejects clicks inside tree mods' scroll views.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Rect leftOutRect)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !ButtonRect(leftOutRect).Contains(current.mousePosition))
                return;

            current.Use();
            clickClaimed = true;
        }

        // Drawn last so it sits on top of anything else in that corner.
        // Also takes a normal button click, for when another mod used the mouse-down before the prefix ran.
        [HarmonyPostfix]
        public static void Postfix(Rect leftOutRect, MainTabWindow_Research __instance)
        {
            if (nextResearchButtonIcon == null)
                nextResearchButtonIcon = ContentFinder<Texture2D>.Get("UI/Buttons/MainButtons/CM_Semi_Random_Research_Random");

            Rect buttonRect = ButtonRect(leftOutRect);
            bool pressed = Widgets.ButtonImage(buttonRect, nextResearchButtonIcon) || clickClaimed;
            clickClaimed = false;
            TooltipHandler.TipRegion(buttonRect, "CM_Semi_Random_Research_OpenSemiRandom".Translate());

            if (!pressed)
                return;

            SoundDefOf.ResearchStart.PlayOneShotOnCamera();
            ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
            Event.current.Use();
        }
    }
}

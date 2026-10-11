using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Button in the top right of the vanilla tree's info pane that switches back to this mod's window.
    [HarmonyPatch(typeof(MainTabWindow_Research), "DrawLeftRect")]
    public static class Patch_MainTabWindow_Research_DrawLeftRect
    {
        // Loaded on first draw: PatchAll runs off the main thread, where textures cannot load.
        private static Texture2D nextResearchButtonIcon;

        [HarmonyPostfix]
        public static void Postfix(Rect leftOutRect, MainTabWindow_Research __instance)
        {
            if (nextResearchButtonIcon == null)
                nextResearchButtonIcon = ContentFinder<Texture2D>.Get("UI/Buttons/MainButtons/CM_Semi_Random_Research_Random");

            float buttonSize = 32.0f;
            Rect buttonRect = new Rect(leftOutRect.xMax - buttonSize, leftOutRect.yMin, buttonSize, buttonSize);

            bool pressed = Widgets.ButtonImage(buttonRect, nextResearchButtonIcon);
            TooltipHandler.TipRegion(buttonRect, "CM_Semi_Random_Research_OpenSemiRandom".Translate());

            if (pressed)
            {
                SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
                Event.current.Use();
            }
        }
    }
}

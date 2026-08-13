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
        private static readonly Texture2D TexSemiRandom = ContentFinder<Texture2D>.Get("UI/semi", true);

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

            MethodInfo original = AccessTools.Method(type, "DoSettingsButton");
            if (original == null)
            {
                Log.Warning("[Semi Random Research] YART DoSettingsButton was not found. The swap button may be missing.");
                return;
            }

            var harmony = new Harmony("CM_Semi_Random_Research.YartIntegration");
            MethodInfo postfix = AccessTools.Method(typeof(Yart_Integration), nameof(DoSettingsButton_Postfix));
            harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            Log.Message("[Semi Random Research] Successfully integrated with YART UI.");
        }

        public static void DoSettingsButton_Postfix(Rect rect, Window __instance)
        {
            Rect semiBtnRect = new Rect(rect.xMax + 6f, rect.y, rect.width, rect.height);

            if (Widgets.ButtonImage(semiBtnRect, TexSemiRandom))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
                Event.current.Use();
            }
            TooltipHandler.TipRegion(semiBtnRect, "Open Semi-Random Research");
        }
    }
}

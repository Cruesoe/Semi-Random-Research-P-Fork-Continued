using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace CM_Semi_Random_Research
{
    [StaticConstructorOnStartup]
    public static class Sleek_Integration
    {
        private static bool hookedStandDown;

        static Sleek_Integration()
        {
            TryHookSleekStandDown();
            BindSleekSelectionGate();
        }

        // Must run from the Mod constructor (before StaticConstructorOnStartup) so Sleek's
        // Evaluate() does not stand down when we own the Research tab window class.
        public static void TryHookSleekStandDown()
        {
            if (hookedStandDown)
                return;

            Type sleekCompat = AccessTools.TypeByName("SleekResearchTab.SleekCompat");
            if (sleekCompat == null)
                return;

            MethodInfo evaluate = AccessTools.Method(sleekCompat, "Evaluate");
            if (evaluate == null)
                return;

            hookedStandDown = true;
            var harmony = new Harmony("CM_Semi_Random_Research.SleekStandDown");
            harmony.Patch(evaluate, postfix: new HarmonyMethod(typeof(Sleek_Integration), nameof(EvaluatePostfix)));
            BindSleekSelectionGate();
        }

        private static Type sleekCompatType;
        private static PropertyInfo sleekDisabledProperty;
        private static PropertyInfo sleekDisabledReasonProperty;
        private static MethodInfo sleekDisabledSetter;

        // Resolved once: TypeByName scans every loaded assembly, and Sleek decides how often Evaluate runs.
        private static bool ResolveSleekCompat()
        {
            if (sleekCompatType != null)
                return true;

            sleekCompatType = AccessTools.TypeByName("SleekResearchTab.SleekCompat");
            if (sleekCompatType == null)
                return false;

            sleekDisabledProperty = AccessTools.Property(sleekCompatType, "Disabled");
            sleekDisabledReasonProperty = AccessTools.Property(sleekCompatType, "DisabledReason");
            sleekDisabledSetter = AccessTools.PropertySetter(sleekCompatType, "Disabled");
            return true;
        }

        public static void EvaluatePostfix()
        {
            if (!ResolveSleekCompat())
                return;

            if (sleekDisabledProperty == null || !(bool)sleekDisabledProperty.GetValue(null, null))
                return;

            string reason = sleekDisabledReasonProperty?.GetValue(null, null) as string;
            if (string.IsNullOrEmpty(reason) ||
                reason.IndexOf("MainTabWindow_NextResearch", StringComparison.Ordinal) < 0)
            {
                return;
            }

            sleekDisabledSetter?.Invoke(null, new object[] { false });
        }

        // Sleek only auto-binds the original Semi Random package IDs. Point its start-button
        // gate at this continuation so Prohibit normal selection still applies.
        private static void BindSleekSelectionGate()
        {
            Type compat = AccessTools.TypeByName("SleekResearchTab.SemiRandomResearchCompat");
            if (compat == null)
                return;

            try
            {
                AccessTools.PropertySetter(compat, "Active")?.Invoke(null, new object[] { true });
                FieldInfo canSelect = AccessTools.Field(compat, "canSelectNormal");
                if (canSelect != null)
                {
                    canSelect.SetValue(null, new Func<ResearchProjectDef, bool>(
                        SemiRandomResearchUtility.CanSelectNormalResearchNow));
                }
            }
            catch (Exception e)
            {
                Log.Warning("[Semi Random Research] Could not bind Sleek Research Tab selection gate: " + e.Message);
            }
        }
    }
}

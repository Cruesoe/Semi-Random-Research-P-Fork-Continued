using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // Node Research measures era progress from the projects on the Main tab, and only moves them there while its own window
    // is open. This counts the projects its collapse would put on Main, wherever they sit now, so Emergence is not offered early.
    public static class NodeResearchAdvancement
    {
        private static FieldInfo settingsField;
        private static FieldInfo advancementTiedToField;
        private static FieldInfo eraCompletionPercentageField;

        private static readonly Dictionary<TechLevel, List<ResearchProjectDef>> foundationsByEra =
            new Dictionary<TechLevel, List<ResearchProjectDef>>();
        private static readonly Dictionary<TechLevel, List<ResearchProjectDef>> projectsByEra =
            new Dictionary<TechLevel, List<ResearchProjectDef>>();

        public static void TryPatch(Harmony harmony, Type windowType)
        {
            MethodInfo original = AccessTools.Method(windowType, "GetAdvancementProgressRaw");
            Type modType = AccessTools.TypeByName("BetterResearchMenu.BetterResearchMenuMod");
            settingsField = modType != null ? AccessTools.Field(modType, "settings") : null;
            Type settingsType = settingsField?.FieldType;
            advancementTiedToField = settingsType != null ? AccessTools.Field(settingsType, "advancementTiedTo") : null;
            eraCompletionPercentageField = settingsType != null ? AccessTools.Field(settingsType, "eraCompletionPercentage") : null;

            if (original == null || advancementTiedToField == null || eraCompletionPercentageField == null)
            {
                Log.Warning("[Semi Random Research] Node Research's era advancement check was not found. Emergence projects may be offered early while projects are off the Main tab.");
                return;
            }

            harmony.Patch(original, prefix: new HarmonyMethod(typeof(NodeResearchAdvancement), nameof(Prefix)));
        }

        public static bool Prefix(TechLevel playerEra, ResearchTabDef tab, out int finished, out int total, ref float __result)
        {
            finished = 0;
            total = 0;

            object settings = settingsField.GetValue(null);
            if (settings == null || tab == null || tab != ResearchTabDefOf.Main)
                return true;

            bool tiedToFoundations = advancementTiedToField.GetValue(settings).ToString() == "Foundations";
            List<ResearchProjectDef> counted = EraProjects(playerEra, tiedToFoundations);
            for (int i = 0; i < counted.Count; i++)
            {
                if (counted[i].IsFinished)
                    finished++;
            }

            if (tiedToFoundations)
            {
                total = counted.Count;
            }
            else
            {
                float percentage = (float)eraCompletionPercentageField.GetValue(settings);
                total = Mathf.Max(1, Mathf.RoundToInt(counted.Count * percentage));
            }

            __result = total <= 0 ? 1f : Mathf.Clamp01((float)finished / total);
            return false;
        }

        // Cached per era: the lists do not depend on tabs, the only thing that moves after startup.
        private static List<ResearchProjectDef> EraProjects(TechLevel era, bool foundationsOnly)
        {
            Dictionary<TechLevel, List<ResearchProjectDef>> cache = foundationsOnly ? foundationsByEra : projectsByEra;
            if (cache.TryGetValue(era, out List<ResearchProjectDef> cached))
                return cached;

            ResearchTabDef anomalyTab = DefDatabase<ResearchTabDef>.GetNamedSilentFail("Anomaly");
            ResearchTabDef gravtechTab = DefDatabase<ResearchTabDef>.GetNamedSilentFail("VGE_Gravtech");
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            List<ResearchProjectDef> allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ResearchProjectDef def = allDefs[i];
                if (def.techLevel != era || NodeResearch.IsEmergenceTech(def))
                    continue;
                // Node Research's collapse leaves these two tabs alone and moves knowledge projects onto the Anomaly tab.
                if (def.knowledgeCategory != null || def.knowledgeCost > 0f)
                    continue;
                if (def.tab != null && (def.tab == anomalyTab || def.tab == gravtechTab))
                    continue;
                if (foundationsOnly && !NodeResearch.IsFoundationTech(def))
                    continue;
                result.Add(def);
            }

            cache[era] = result;
            return result;
        }
    }
}

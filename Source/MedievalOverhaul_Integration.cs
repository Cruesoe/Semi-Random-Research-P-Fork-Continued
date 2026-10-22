using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace CM_Semi_Random_Research
{
    // Medieval Overhaul caches one schematic answer for every project for 250 ticks, so checking many projects in a row
    // gives them all the first project's answer. Clears that cache whenever CanStartNow moves to another schematic project.
    [StaticConstructorOnStartup]
    public static class MedievalOverhaul_Integration
    {
        private static readonly HashSet<ResearchProjectDef> schematicProjects = new HashSet<ResearchProjectDef>();
        private static FieldInfo cacheStaleAfterTicksField;
        private static ResearchProjectDef lastProject;

        static MedievalOverhaul_Integration()
        {
            Type canStartNowPatch = AccessTools.TypeByName("MedievalOverhaul.Patches.ResearchProjectsDefs+ResearchProjectDef_CanStartNow");
            Type requiredSchematic = AccessTools.TypeByName("MedievalOverhaul.RequiredSchematic");
            if (canStartNowPatch == null || requiredSchematic == null)
                return;

            cacheStaleAfterTicksField = AccessTools.Field(canStartNowPatch, "cacheStaleAfterTicks");
            if (cacheStaleAfterTicksField == null || cacheStaleAfterTicksField.FieldType != typeof(int))
            {
                Log.Warning("[CM_Semi_Random_Research] Medieval Overhaul's schematic cache wasn't found; schematic projects may be offered inconsistently.");
                return;
            }

            foreach (ResearchProjectDef def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (def.modExtensions == null)
                    continue;
                foreach (DefModExtension extension in def.modExtensions)
                {
                    if (requiredSchematic.IsInstanceOfType(extension))
                    {
                        schematicProjects.Add(def);
                        break;
                    }
                }
            }
            if (schematicProjects.Count == 0)
                return;

            var harmony = new Harmony("CM_Semi_Random_Research.MedievalOverhaul");
            harmony.Patch(AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.CanStartNow)),
                prefix: new HarmonyMethod(typeof(MedievalOverhaul_Integration), nameof(CanStartNowPrefix)));
        }

        private static void CanStartNowPrefix(ResearchProjectDef __instance)
        {
            if (__instance == lastProject || !schematicProjects.Contains(__instance))
                return;
            lastProject = __instance;
            cacheStaleAfterTicksField.SetValue(null, -1);
        }
    }
}

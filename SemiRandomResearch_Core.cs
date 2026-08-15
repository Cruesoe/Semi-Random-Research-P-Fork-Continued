using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace CM_Semi_Random_Research
{
    // =========================================================================
    // UTILITIES
    // =========================================================================
    public static class SemiRandomResearchUtility
    {
        public static bool IsControllingResearchSelection =>
            SemiRandomResearchMod.settings != null &&
            SemiRandomResearchMod.settings.featureEnabled;

        public static bool CanSelectNormalResearchNow(ResearchProjectDef rpd)
        {
            return !IsControllingResearchSelection && rpd.CanStartNow;
        }

        public static bool IsCurrentProject(ResearchProjectDef rpd)
        {
            return !IsControllingResearchSelection && Find.ResearchManager.IsCurrentProject(rpd);
        }
    }

    // Vanilla casts MainButtonDefOf.Research.TabWindow to MainTabWindow_Research.
    // That throws when this mod (or Node Research / YART) owns the Research tab.
    internal static class ResearchTabCompatibility
    {
        public static void Open(ResearchTabDef tab = null, ResearchProjectDef project = null)
        {
            if (Find.MainTabsRoot == null || MainButtonDefOf.Research == null)
                return;

            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research, true);
            Apply(tab, project);
        }

        public static void SelectProject(ResearchProjectDef project)
        {
            Apply(null, project);
        }

        private static void Apply(ResearchTabDef tab, ResearchProjectDef project)
        {
            MainTabWindow window = MainButtonDefOf.Research.TabWindow;
            if (window is MainTabWindow_Research vanilla)
            {
                if (tab != null)
                    vanilla.CurTab = tab;
                if (project != null)
                    vanilla.Select(project);
                return;
            }

            if (window is MainTabWindow_NextResearch ours && project != null)
                ours.SelectFromExternal(project);
        }
    }

    // =========================================================================
    // COMPATIBILITY
    // =========================================================================
    static class Compatibility
    {
        public static bool enabled_SoS2 = ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => m.PackageIdPlayerFacing == "kentington.saveourship2");
        public static bool enabled_CE = ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => m.PackageIdPlayerFacing == "CETeam.CombatExtended");

        public static bool DoCompatibilityChecks(ResearchProjectDef rpd)
        {
            return SatisfiesAlienRaceRestriction(rpd) &&
                !rpd.IsDummyResearch();
        }

        public static bool AnomalyResearchUnlocked()
        {
            if (!ModsConfig.AnomalyActive)
                return false;
            return Find.Anomaly != null && Find.Anomaly.AnomalyStudyEnabled;
        }

        public static bool IsAnomalyKnowledgeCategory(KnowledgeCategoryDef category)
        {
            if (category == null || !ModsConfig.AnomalyActive)
                return false;
            return category == KnowledgeCategoryDefOf.Basic || category == KnowledgeCategoryDefOf.Advanced;
        }

        public static bool IsHiddenResearch(ResearchProjectDef rpd)
        {
            if (rpd == null)
            {
                return false;
            }

            if (rpd.IsHidden)
            {
                return true;
            }

            // Vanilla CanStartNow is true for dark research from day one; the Anomaly
            // tab is what stays hidden until study is enabled. Mirror that here.
            if (IsAnomalyKnowledgeCategory(rpd.knowledgeCategory) && !AnomalyResearchUnlocked())
            {
                return true;
            }

            if (enabled_SoS2 && rpd.tab?.defName == "ResearchTabArchotech")
            {
                return !SaveOurShip2ArchotechUplinkUnlocked(rpd);
            }

            return false;
        }

        public static bool SatisfiesAlienRaceRestriction(ResearchProjectDef rpd)
        {
            return true;
        }

        public static bool IsDummyResearch(this ResearchProjectDef rpd)
        {
            if (rpd == null)
            {
                return false;
            }
            if (enabled_CE && rpd.defName == "VFES_Artillery_Debug")
            {
                return true;
            }
            if (rpd.Cost == 0)
            {
                return true;
            }
            if (rpd.prerequisites != null && rpd.prerequisites.Contains(rpd))
            {
                return true;
            }

            return false;
        }

        private static bool SaveOurShip2ArchotechUplinkUnlocked(ResearchProjectDef rpd)
        {
            try
            {
                // Use Harmony reflection so we don't need a hard compile-time reference to the SOS2 DLL
                Type modType = AccessTools.TypeByName("SaveOurShip2.ShipInteriorMod2");
                if (modType != null)
                {
                    object worldComp = AccessTools.Field(modType, "WorldComp")?.GetValue(null);
                    if (worldComp != null)
                    {
                        object unlocks = AccessTools.Field(worldComp.GetType(), "Unlocks")?.GetValue(worldComp);

                        // Check if it's a HashSet or List and contains our string
                        if (unlocks is HashSet<string> hashSet) return hashSet.Contains("ArchotechUplink");
                        if (unlocks is List<string> list) return list.Contains("ArchotechUplink");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[CM_Semi_Random_Research] Error checking SOS2 compatibility: " + ex);
            }

            return false;
        }
    }

    // =========================================================================
    // MANIFEST VERSION READER
    // =========================================================================

    public class VersionFromManifest
    {
        private const string ManifestFileName = "Manifest.xml";
        public string version;

        private static string AboutDir(ModMetaData mod)
        {
            return Path.Combine(mod.RootDir.FullName, "About");
        }

        public static string GetVersionFromModMetaData(ModMetaData modMetaData)
        {
            var manifestPath = Path.Combine(AboutDir(modMetaData), ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                var manifest = DirectXmlLoader.ItemFromXmlFile<VersionFromManifest>(manifestPath, false);
                return manifest.version;
            }
            catch (Exception e)
            {
                Log.ErrorOnce($"Error loading manifest for '{modMetaData.Name}':\n{e.Message}\n\n{e.StackTrace}",
                    modMetaData.Name.GetHashCode());
            }

            return null;
        }
    }
}
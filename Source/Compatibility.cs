using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace CM_Semi_Random_Research
{
    static class Compatibility
    {
        public static bool enabled_SoS2 = ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => m.PackageIdPlayerFacing == "kentington.saveourship2");
        public static bool enabled_CE = ModsConfig.ActiveModsInLoadOrder.Any((ModMetaData m) => m.PackageIdPlayerFacing == "CETeam.CombatExtended");

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
                return false;

            if (rpd.IsHidden)
                return true;

            // Vanilla CanStartNow allows dark research from day one; only the Anomaly tab stays hidden until study is enabled.
            if (IsAnomalyKnowledgeCategory(rpd.knowledgeCategory) && !AnomalyResearchUnlocked())
                return true;

            if (enabled_SoS2 && rpd.tab?.defName == "ResearchTabArchotech")
                return !SaveOurShip2ArchotechUplinkUnlocked();

            return false;
        }

        public static bool IsDummyResearch(this ResearchProjectDef rpd)
        {
            if (rpd == null)
                return false;
            if (enabled_CE && rpd.defName == "VFES_Artillery_Debug")
                return true;
            if (rpd.Cost == 0)
                return true;
            if (rpd.prerequisites != null && rpd.prerequisites.Contains(rpd))
                return true;

            return false;
        }

        // The uplink is colony-wide and never goes away once built. Re-read at most once a second until then, per game.
        private static bool sos2HandlesResolved;
        private static FieldInfo sos2WorldCompField;
        private static FieldInfo sos2UnlocksField;
        private static Type sos2UnlocksOwnerType;
        private static Game sos2Game;
        private static bool sos2UplinkUnlocked;
        private static int sos2UplinkCheckedTick = int.MinValue;

        private static bool SaveOurShip2ArchotechUplinkUnlocked()
        {
            if (!ReferenceEquals(Current.Game, sos2Game))
            {
                sos2Game = Current.Game;
                sos2UplinkUnlocked = false;
                sos2UplinkCheckedTick = int.MinValue;
            }

            if (sos2UplinkUnlocked)
                return true;

            int tick = Find.TickManager?.TicksGame ?? 0;
            if (sos2UplinkCheckedTick != int.MinValue && tick - sos2UplinkCheckedTick < 60)
                return false;
            sos2UplinkCheckedTick = tick;

            try
            {
                if (!sos2HandlesResolved)
                {
                    sos2HandlesResolved = true;
                    Type modType = AccessTools.TypeByName("SaveOurShip2.ShipInteriorMod2");
                    sos2WorldCompField = modType != null ? AccessTools.Field(modType, "WorldComp") : null;
                }

                object worldComp = sos2WorldCompField?.GetValue(null);
                if (worldComp == null)
                    return false;

                Type ownerType = worldComp.GetType();
                if (sos2UnlocksField == null || sos2UnlocksOwnerType != ownerType)
                {
                    sos2UnlocksOwnerType = ownerType;
                    sos2UnlocksField = AccessTools.Field(ownerType, "Unlocks");
                }

                object unlocks = sos2UnlocksField?.GetValue(worldComp);
                if (unlocks is HashSet<string> hashSet) sos2UplinkUnlocked = hashSet.Contains("ArchotechUplink");
                else if (unlocks is List<string> list) sos2UplinkUnlocked = list.Contains("ArchotechUplink");
            }
            catch (Exception ex)
            {
                Log.Warning("[CM_Semi_Random_Research] Error checking SOS2 compatibility: " + ex);
            }

            return sos2UplinkUnlocked;
        }
    }
}

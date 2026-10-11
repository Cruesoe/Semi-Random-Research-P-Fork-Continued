using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
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

        private static float lastRejectionMessageTime = -99f;

        // Throttled on real time: a tree with a research queue can retry every frame while the game is paused.
        public static void RejectSelection()
        {
            if (Time.realtimeSinceStartup - lastRejectionMessageTime < 1.5f)
                return;

            lastRejectionMessageTime = Time.realtimeSinceStartup;
            Messages.Message("CM_Semi_Random_Research_Active".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        // World components cached per world instance, so a new or loaded game never gets the previous world's.
        private static World cachedWorld;
        private static ResearchTracker cachedResearchTracker;
        private static ResearchRateTracker cachedRateTracker;

        private static World CurrentWorld()
        {
            World world = Current.Game?.World;
            if (world == null)
            {
                cachedWorld = null;
                cachedResearchTracker = null;
                cachedRateTracker = null;
                return null;
            }

            if (!ReferenceEquals(world, cachedWorld))
            {
                cachedWorld = world;
                cachedResearchTracker = null;
                cachedRateTracker = null;
            }
            return world;
        }

        public static ResearchTracker Tracker
        {
            get
            {
                World world = CurrentWorld();
                if (world == null)
                    return null;
                return cachedResearchTracker ?? (cachedResearchTracker = world.GetComponent<ResearchTracker>());
            }
        }

        public static ResearchRateTracker RateTracker
        {
            get
            {
                World world = CurrentWorld();
                if (world == null)
                    return null;
                return cachedRateTracker ?? (cachedRateTracker = world.GetComponent<ResearchRateTracker>());
            }
        }
    }
}

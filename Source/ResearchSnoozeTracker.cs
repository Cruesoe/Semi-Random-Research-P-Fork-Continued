using HarmonyLib;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace CM_Semi_Random_Research
{
    // Projects the player has snoozed. ResearchTracker leaves them out of the offers; the list is kept while snoozing is off.
    public class ResearchSnoozeTracker : WorldComponent
    {
        private HashSet<ResearchProjectDef> snoozedProjects = new HashSet<ResearchProjectDef>();

        public ResearchSnoozeTracker(World world) : base(world)
        {
        }

        public static bool SnoozingEnabled =>
            SemiRandomResearchMod.settings == null || SemiRandomResearchMod.settings.allowSnoozing;

        // Cached per world instance, so a new or loaded game never gets the previous world's.
        private static World cachedWorld;
        private static ResearchSnoozeTracker cachedTracker;

        public static ResearchSnoozeTracker Get()
        {
            World world = Find.World;
            if (world == null)
            {
                cachedWorld = null;
                cachedTracker = null;
                return null;
            }

            if (!ReferenceEquals(world, cachedWorld))
            {
                cachedWorld = world;
                cachedTracker = null;
            }
            else if (cachedTracker != null)
            {
                return cachedTracker;
            }

            ResearchSnoozeTracker tracker = world.GetComponent<ResearchSnoozeTracker>();
            if (tracker == null)
            {
                tracker = new ResearchSnoozeTracker(world);
                world.components.Add(tracker);
            }

            cachedTracker = tracker;
            return tracker;
        }

        // True when snoozing is on and the project is snoozed in the current world.
        public static bool IsSnoozedNow(ResearchProjectDef def)
        {
            return SnoozingEnabled && Get()?.IsSnoozed(def) == true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref snoozedProjects, "snoozedProjects", LookMode.Def);
            if (snoozedProjects == null)
                snoozedProjects = new HashSet<ResearchProjectDef>();
            snoozedProjects.RemoveWhere(def => def == null);
        }

        public bool IsSnoozed(ResearchProjectDef def)
        {
            return def != null && snoozedProjects.Contains(def);
        }

        // Fills a caller-owned buffer, since the snoozed view asks on every OnGUI pass.
        public void GetSnoozedProjects(List<ResearchProjectDef> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            foreach (ResearchProjectDef def in snoozedProjects)
            {
                if (def != null && !def.IsFinished)
                    buffer.Add(def);
            }
        }

        // Returns the reason the project cannot be snoozed, or null once it is snoozed.
        public string TrySnooze(ResearchProjectDef def)
        {
            if (!SnoozingEnabled)
                return "CM_Semi_Random_Research_SnoozeDisabled".Translate();
            if (def == null)
                return "CM_Semi_Random_Research_SnoozeMissing".Translate();
            if (def.IsFinished)
                return "CM_Semi_Random_Research_SnoozeFinished".Translate(def.LabelCap);
            if (NodeResearch.IsEmergenceTech(def))
                return "CM_Semi_Random_Research_SnoozeEmergence".Translate(def.LabelCap);
            if (ResearchTabWindowSwitcher.NodeResearchInstalled && NodeResearch.IsFoundationTech(def))
                return "CM_Semi_Random_Research_SnoozeFoundation".Translate(def.LabelCap);

            snoozedProjects.Add(def);
            SemiRandomResearchUtility.Tracker?.RemoveSnoozedOffer(def);
            SyncSnoozeToGraph(def, snoozed: true);
            return null;
        }

        public void Unsnooze(ResearchProjectDef def)
        {
            if (def == null)
                return;

            snoozedProjects.Remove(def);
            SyncSnoozeToGraph(def, snoozed: false);
        }

        private static FieldInfo cachedNodeStatesField;
        private static FieldInfo cachedOpenedNodesField;
        private static bool stateLookupFailed;

        // Collapses a snoozed project's node in Node Research's graph, and restores it on unsnooze.
        private static void SyncSnoozeToGraph(ResearchProjectDef def, bool snoozed)
        {
            if (def == null || !ResearchTabWindowSwitcher.NodeResearchInstalled)
                return;
            if (stateLookupFailed)
                return;
            if (cachedNodeStatesField == null || cachedOpenedNodesField == null)
            {
                System.Type stateType = AccessTools.TypeByName("BetterResearchMenu.State");
                if (stateType == null)
                {
                    stateLookupFailed = true;
                    return;
                }
                cachedNodeStatesField = AccessTools.Field(stateType, "nodeStates");
                cachedOpenedNodesField = AccessTools.Field(stateType, "openedNodes");
                if (cachedNodeStatesField == null || cachedOpenedNodesField == null)
                {
                    stateLookupFailed = true;
                    return;
                }
            }

            try
            {
                object nodeStates = cachedNodeStatesField.GetValue(null);
                object openedNodes = cachedOpenedNodesField.GetValue(null);
                if (nodeStates == null || openedNodes == null)
                    return;

                System.Type enumType = AccessTools.TypeByName("BetterResearchMenu.NodeState");
                if (enumType == null)
                    return;

                if (snoozed)
                {
                    object minimized = System.Enum.Parse(enumType, "Minimized");
                    nodeStates.GetType().GetMethod("set_Item")?.Invoke(nodeStates, new object[] { def.defName, minimized });
                    openedNodes.GetType().GetMethod("Add")?.Invoke(openedNodes, new object[] { def.defName });
                }
                else
                {
                    object restored = System.Enum.Parse(enumType, def.PrerequisitesCompleted ? "Dot" : "Expanded");
                    nodeStates.GetType().GetMethod("set_Item")?.Invoke(nodeStates, new object[] { def.defName, restored });
                }
            }
            catch (System.Exception)
            {
            }
        }
    }
}

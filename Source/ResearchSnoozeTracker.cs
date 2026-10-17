using HarmonyLib;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace CM_Semi_Random_Research
{
    // Projects the player has snoozed. ResearchTracker leaves them and their dependents out of the offers; the list is kept while snoozing is off.
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

        // True when snoozing is on and the project is snoozed or depends, directly or not, on an unfinished snoozed project.
        public static bool IsExcludedNow(ResearchProjectDef def)
        {
            return SnoozingEnabled && def != null && Get()?.IsExcluded(def) == true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref snoozedProjects, "snoozedProjects", LookMode.Def);
            if (snoozedProjects == null)
                snoozedProjects = new HashSet<ResearchProjectDef>();
            snoozedProjects.RemoveWhere(def => def == null);
            snoozeVersion++;
        }

        // Snoozed projects and everything depending on them, rebuilt when the snoozes change or a project finishes.
        private readonly HashSet<ResearchProjectDef> excluded = new HashSet<ResearchProjectDef>();
        // The excluded projects that are not snoozed themselves, by tech level and then cost.
        private readonly List<ResearchProjectDef> blockedSorted = new List<ResearchProjectDef>();
        private int snoozeVersion;
        private int excludedSnoozeVersion = -1;
        private int excludedCompletionRevision = -1;

        public bool IsExcluded(ResearchProjectDef def)
        {
            EnsureExcluded();
            return excluded.Contains(def);
        }

        private void EnsureExcluded()
        {
            int completionRevision = SemiRandomResearchUtility.Tracker?.CompletionRevision ?? 0;
            if (excludedSnoozeVersion == snoozeVersion && excludedCompletionRevision == completionRevision)
                return;

            excludedSnoozeVersion = snoozeVersion;
            excludedCompletionRevision = completionRevision;
            excluded.Clear();
            foreach (ResearchProjectDef def in snoozedProjects)
            {
                if (def != null && !def.IsFinished)
                    AddWithDependents(excluded, def);
            }

            blockedSorted.Clear();
            foreach (ResearchProjectDef def in excluded)
            {
                if (!snoozedProjects.Contains(def))
                    blockedSorted.Add(def);
            }
            blockedSorted.Sort(ByTechLevelThenCost);
        }

        private static void AddWithDependents(HashSet<ResearchProjectDef> result, ResearchProjectDef root)
        {
            if (!result.Add(root))
                return;
            Stack<ResearchProjectDef> pending = new Stack<ResearchProjectDef>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                List<ResearchProjectDef> next = Dependents(pending.Pop());
                for (int i = 0; i < next.Count; i++)
                {
                    if (result.Add(next[i]))
                        pending.Push(next[i]);
                }
            }
        }

        // Projects that list each project as a prerequisite or hidden prerequisite, built once from the def database.
        private static Dictionary<ResearchProjectDef, List<ResearchProjectDef>> dependents;
        private static readonly List<ResearchProjectDef> NoDependents = new List<ResearchProjectDef>();

        private static List<ResearchProjectDef> Dependents(ResearchProjectDef def)
        {
            if (dependents == null)
            {
                dependents = new Dictionary<ResearchProjectDef, List<ResearchProjectDef>>();
                foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                {
                    AddDependent(project.prerequisites, project);
                    AddDependent(project.hiddenPrerequisites, project);
                }
            }
            return dependents.TryGetValue(def, out List<ResearchProjectDef> list) ? list : NoDependents;
        }

        private static void AddDependent(List<ResearchProjectDef> prerequisites, ResearchProjectDef project)
        {
            if (prerequisites == null)
                return;
            for (int i = 0; i < prerequisites.Count; i++)
            {
                ResearchProjectDef prerequisite = prerequisites[i];
                if (prerequisite == null)
                    continue;
                if (!dependents.TryGetValue(prerequisite, out List<ResearchProjectDef> list))
                    dependents[prerequisite] = list = new List<ResearchProjectDef>();
                if (!list.Contains(project))
                    list.Add(project);
            }
        }

        private static bool Listable(ResearchProjectDef def) =>
            !def.IsFinished && !def.IsDummyResearch() && !Compatibility.IsHiddenResearch(def) &&
            Find.Storyteller.difficulty.AllowedBy(def.hideWhen);

        // Unfinished projects that snoozing this one would also set aside, by tech level and then cost.
        public List<ResearchProjectDef> BlockedDependents(ResearchProjectDef def)
        {
            EnsureExcluded();
            HashSet<ResearchProjectDef> affected = new HashSet<ResearchProjectDef>();
            AddWithDependents(affected, def);
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            foreach (ResearchProjectDef dependent in affected)
            {
                if (dependent != def && !excluded.Contains(dependent) && Listable(dependent))
                    result.Add(dependent);
            }
            result.Sort(ByTechLevelThenCost);
            return result;
        }

        // Projects set aside only because a prerequisite is snoozed, by tech level and then cost.
        public void GetBlockedBySnooze(List<ResearchProjectDef> buffer)
        {
            buffer.Clear();
            EnsureExcluded();
            for (int i = 0; i < blockedSorted.Count; i++)
            {
                if (Listable(blockedSorted[i]))
                    buffer.Add(blockedSorted[i]);
            }
        }

        // Snoozed projects that set this one aside through its prerequisites.
        public List<ResearchProjectDef> SnoozedPrerequisites(ResearchProjectDef def)
        {
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            HashSet<ResearchProjectDef> seen = new HashSet<ResearchProjectDef>();
            Stack<ResearchProjectDef> pending = new Stack<ResearchProjectDef>();
            pending.Push(def);
            while (pending.Count > 0)
            {
                ResearchProjectDef next = pending.Pop();
                foreach (ResearchProjectDef prerequisite in Prerequisites(next))
                {
                    if (prerequisite == null || !seen.Add(prerequisite) || prerequisite.IsFinished)
                        continue;
                    if (snoozedProjects.Contains(prerequisite))
                        result.Add(prerequisite);
                    pending.Push(prerequisite);
                }
            }
            return result;
        }

        private static IEnumerable<ResearchProjectDef> Prerequisites(ResearchProjectDef def)
        {
            if (def.prerequisites != null)
                foreach (ResearchProjectDef prerequisite in def.prerequisites)
                    yield return prerequisite;
            if (def.hiddenPrerequisites != null)
                foreach (ResearchProjectDef prerequisite in def.hiddenPrerequisites)
                    yield return prerequisite;
        }

        private static int ByTechLevelThenCost(ResearchProjectDef a, ResearchProjectDef b) =>
            a.techLevel != b.techLevel ? a.techLevel.CompareTo(b.techLevel) : a.CostApparent.CompareTo(b.CostApparent);

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

        // The reason the project cannot be snoozed, or null when it can.
        public static string SnoozeRefusal(ResearchProjectDef def)
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
            return null;
        }

        // Returns the reason the project cannot be snoozed, or null once it is snoozed.
        public string TrySnooze(ResearchProjectDef def)
        {
            string refusal = SnoozeRefusal(def);
            if (refusal != null)
                return refusal;

            snoozedProjects.Add(def);
            snoozeVersion++;
            SemiRandomResearchUtility.Tracker?.RemoveSnoozedOffer(def);
            SyncSnoozeToGraph(def, snoozed: true);
            return null;
        }

        public void Unsnooze(ResearchProjectDef def)
        {
            if (def == null)
                return;

            snoozedProjects.Remove(def);
            snoozeVersion++;
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

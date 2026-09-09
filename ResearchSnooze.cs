using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Snooze state lives here so the feature can land without rewriting ResearchTracker.cs
    // in the same commit. Offers are stripped after GetCurrentlyAvailableProjects returns.
    public class ResearchSnoozeTracker : WorldComponent
    {
        private HashSet<ResearchProjectDef> snoozedProjects = new HashSet<ResearchProjectDef>();

        public ResearchSnoozeTracker(World world) : base(world)
        {
        }

        public static ResearchSnoozeTracker Get()
        {
            World world = Find.World;
            if (world == null)
                return null;

            ResearchSnoozeTracker tracker = world.GetComponent<ResearchSnoozeTracker>();
            if (tracker != null)
                return tracker;

            tracker = new ResearchSnoozeTracker(world);
            world.components.Add(tracker);
            return tracker;
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
            return def != null && snoozedProjects != null && snoozedProjects.Contains(def);
        }

        public IEnumerable<ResearchProjectDef> SnoozedProjects
        {
            get
            {
                if (snoozedProjects == null)
                    return Enumerable.Empty<ResearchProjectDef>();
                return snoozedProjects.Where(def => def != null && !def.IsFinished);
            }
        }

        public string TrySnooze(ResearchProjectDef def)
        {
            if (def == null)
                return "CM_Semi_Random_Research_SnoozeMissing".Translate();
            if (def.IsFinished)
                return "CM_Semi_Random_Research_SnoozeFinished".Translate(def.LabelCap);
            if (NodeResearch.IsEmergenceTech(def))
                return "CM_Semi_Random_Research_SnoozeEmergence".Translate(def.LabelCap);
            if (ResearchTabWindowSwitcher.NodeResearchInstalled && NodeResearch.IsFoundationTech(def))
                return "CM_Semi_Random_Research_SnoozeFoundation".Translate(def.LabelCap);

            snoozedProjects ??= new HashSet<ResearchProjectDef>();
            snoozedProjects.Add(def);

            ResearchTracker research = Current.Game?.World?.GetComponent<ResearchTracker>();
            if (research != null)
            {
                string typeKey = ResearchTracker.GetCategoryKey(def);
                if (research.CurrentProject.Contains(def))
                    research.SetCurrentProjectByKey(null, typeKey);
                research.ForceAutoReseachCheckNextTick();
                research.GetCurrentlyAvailableProjects();
            }

            SyncSnoozeToGraph(def, snoozed: true);
            return null;
        }

        public void Unsnooze(ResearchProjectDef def)
        {
            if (def == null || snoozedProjects == null)
                return;

            snoozedProjects.Remove(def);
            SyncSnoozeToGraph(def, snoozed: false);

            ResearchTracker research = Current.Game?.World?.GetComponent<ResearchTracker>();
            research?.ForceAutoReseachCheckNextTick();
            research?.GetCurrentlyAvailableProjects();
        }

        private static FieldInfo cachedNodeStatesField;
        private static FieldInfo cachedOpenedNodesField;
        private static bool stateLookupFailed;

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

    [HarmonyPatch(typeof(ResearchTracker))]
    [HarmonyPatch(nameof(ResearchTracker.GetCurrentlyAvailableProjects))]
    public static class ResearchTracker_GetCurrentlyAvailableProjects_Snooze
    {
        public static void Postfix(List<ResearchProjectDef> __result)
        {
            ResearchSnoozeTracker snooze = ResearchSnoozeTracker.Get();
            if (snooze == null || __result == null || __result.Count == 0)
                return;
            __result.RemoveAll(snooze.IsSnoozed);
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_NextResearch))]
    [HarmonyPatch("DrawResearchButton")]
    public static class MainTabWindow_DrawResearchButton_Snooze
    {
        public static void Prefix(MainTabWindow_NextResearch __instance, ref Rect drawRect, ResearchProjectDef projectDef)
        {
            if (projectDef == null)
                return;
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1)
                return;
            if (!Mouse.IsOver(drawRect))
                return;

            Event.current.Use();
            SoundDefOf.Click.PlayOneShotOnCamera();

            ResearchSnoozeTracker snooze = ResearchSnoozeTracker.Get();
            string blockReason = snooze?.TrySnooze(projectDef);
            if (blockReason != null)
            {
                Messages.Message(blockReason, MessageTypeDefOf.RejectInput, false);
                return;
            }

            ResearchTracker research = Current.Game?.World?.GetComponent<ResearchTracker>();
            AccessTools.Method(typeof(MainTabWindow_NextResearch), "CopyAvailableProjects")
                ?.Invoke(__instance, new object[] { research?.PeekAvailableProjects() });
            AccessTools.Method(typeof(MainTabWindow_NextResearch), "InvalidateLeftColumnCache")
                ?.Invoke(__instance, null);
            Messages.Message("CM_Semi_Random_Research_Snoozed".Translate(projectDef.LabelCap), MessageTypeDefOf.NeutralEvent, false);
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_NextResearch))]
    [HarmonyPatch("DrawHistoryToggleButton")]
    public static class MainTabWindow_DrawHistoryToggleButton_Snooze
    {
        public static bool Prefix(MainTabWindow_NextResearch __instance, Rect rect)
        {
            FieldInfo historyField = AccessTools.Field(typeof(MainTabWindow_NextResearch), "showingHistory");
            FieldInfo snoozedField = AccessTools.Field(typeof(MainTabWindow_NextResearch), "showingSnoozed");
            bool showingHistory = historyField != null && (bool)historyField.GetValue(__instance);
            bool showingSnoozed = snoozedField != null && (bool)snoozedField.GetValue(__instance);

            Texture2D icon = AccessTools.Field(typeof(MainTabWindow_NextResearch), "HistoryIcon")?.GetValue(null) as Texture2D;
            if (Event.current.type == EventType.Repaint && icon != null)
            {
                Color old = GUI.color;
                GUI.color = (showingHistory || showingSnoozed)
                    ? (Color)AccessTools.Field(typeof(MainTabWindow_NextResearch), "ActiveProjectLabelColor").GetValue(null)
                    : Color.white;
                Widgets.DrawTextureFitted(rect, icon, 1f);
                GUI.color = old;
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Mouse.IsOver(rect))
            {
                Event.current.Use();
                SoundDefOf.Click.PlayOneShotOnCamera();
                if (showingSnoozed)
                    AccessTools.Method(typeof(MainTabWindow_NextResearch), "CloseHistory")?.Invoke(__instance, null);
                else if (showingHistory)
                    AccessTools.Method(typeof(MainTabWindow_NextResearch), "OpenSnoozed")?.Invoke(__instance, null);
                else
                    AccessTools.Method(typeof(MainTabWindow_NextResearch), "OpenHistory")?.Invoke(__instance, null);
            }

            string tip = showingSnoozed
                ? "CM_Semi_Random_Research_SnoozedBackTip".Translate()
                : showingHistory
                    ? "CM_Semi_Random_Research_HistoryToSnoozedTip".Translate()
                    : "CM_Semi_Random_Research_HistoryTip".Translate();
            TooltipHandler.TipRegion(rect, tip);
            return false;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_NextResearch))]
    [HarmonyPatch(nameof(MainTabWindow_NextResearch.DoWindowContents))]
    public static class MainTabWindow_DoWindowContents_Snooze
    {
        public static void Prefix(MainTabWindow_NextResearch __instance)
        {
            FieldInfo snoozedField = AccessTools.Field(typeof(MainTabWindow_NextResearch), "showingSnoozed");
            FieldInfo historyField = AccessTools.Field(typeof(MainTabWindow_NextResearch), "showingHistory");
            if (snoozedField == null || historyField == null)
                return;
            if ((bool)snoozedField.GetValue(__instance))
                historyField.SetValue(__instance, true);
        }
    }
}

using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // Swaps which window the Research main tab opens. No static constructor: the Mod constructor reaches in here
    // before defs exist, and ResearchTabStartup makes the first Apply call.
    public static class ResearchTabWindowSwitcher
    {
        public const string PackageId = "ferny.noderesearch";
        public const string WindowTypeName = "BetterResearchMenu.MainTabWindow_BetterResearch";
        public const string YartPackageId = "seohyeon.yart";
        public const string YartWindowTypeName = "YART.MainTabWindow_YART";
        public const string SleekPackageId = "squishyjellyfish.SleekResearchTab";
        public const string NicePackageId = "Andromeda.NiceResearchTab";
        public const string NiceWindowTypeName = "NiceResearchTab.MainTabWindow_ResearchTree";
        public const string OrganizedPackageId = "cruesoe.research.organized";

        private static Type cachedNodeResearchWindowType;
        private static Type cachedYartWindowType;
        private static Type cachedNiceWindowType;

        private static readonly FieldInfo TabWindowIntField =
            AccessTools.Field(typeof(MainButtonDef), "tabWindowInt");

        // One window instance per research tree, kept across swaps so each tree keeps its camera and scroll position.
        private static readonly Dictionary<Type, MainTabWindow> parkedWindows = new Dictionary<Type, MainTabWindow>();

        private static MainButtonDef ResearchMainButton =>
            DefDatabase<MainButtonDef>.GetNamedSilentFail("Research");

        // The active mod list cannot change while the game runs, so each answer is resolved once.
        private static bool? nodeResearchInstalledCached;
        private static bool? yartInstalledCached;
        private static bool? sleekInstalledCached;
        private static bool? niceResearchTabInstalledCached;
        private static bool? organizedInstalledCached;

        private static bool IsInstalled(ref bool? cache, string packageId)
        {
            if (cache == null)
                cache = ModLister.GetActiveModWithIdentifier(packageId) != null;
            return cache.Value;
        }

        public static Type NodeResearchWindowType =>
            cachedNodeResearchWindowType
            ?? (cachedNodeResearchWindowType = AccessTools.TypeByName(WindowTypeName));

        public static bool NodeResearchInstalled =>
            IsInstalled(ref nodeResearchInstalledCached, PackageId);

        public static Type YartWindowType =>
            cachedYartWindowType
            ?? (cachedYartWindowType = AccessTools.TypeByName(YartWindowTypeName));

        public static bool YartInstalled =>
            IsInstalled(ref yartInstalledCached, YartPackageId);

        public static bool SleekInstalled =>
            IsInstalled(ref sleekInstalledCached, SleekPackageId);

        public static Type NiceWindowType =>
            cachedNiceWindowType
            ?? (cachedNiceWindowType = AccessTools.TypeByName(NiceWindowTypeName));

        public static bool NiceResearchTabInstalled =>
            IsInstalled(ref niceResearchTabInstalledCached, NicePackageId);

        public static bool OrganizedInstalled =>
            IsInstalled(ref organizedInstalledCached, OrganizedPackageId);

        public static bool AnyTreeInstalled =>
            NodeResearchInstalled || YartInstalled || SleekInstalled || NiceResearchTabInstalled || OrganizedInstalled;

        // The tab owner records the window last switched to, so the Research button reopens it next session.
        public static bool IsTabOwnerAvailable(ResearchTabOwner owner)
        {
            switch (owner)
            {
                case ResearchTabOwner.SemiRandom:
                    return true;
                // Sleek and Research: Organized redress the vanilla window, so it is always there to go back to.
                case ResearchTabOwner.Vanilla:
                    return true;
                case ResearchTabOwner.NodeResearch:
                    return IsTreeAvailable(PreferredResearchTree.NodeResearch);
                case ResearchTabOwner.YART:
                    return IsTreeAvailable(PreferredResearchTree.YART);
                case ResearchTabOwner.Sleek:
                    return IsTreeAvailable(PreferredResearchTree.Sleek);
                case ResearchTabOwner.NiceResearchTab:
                    return IsTreeAvailable(PreferredResearchTree.NiceResearchTab);
                case ResearchTabOwner.Organized:
                    return IsTreeAvailable(PreferredResearchTree.Organized);
                default:
                    return false;
            }
        }

        // Falls back to this mod's window when the saved owner's mod has been removed.
        public static ResearchTabOwner GetEffectiveTabOwner()
        {
            ResearchTabOwner owner = SemiRandomResearchMod.settings != null
                ? SemiRandomResearchMod.settings.researchTabOwner
                : ResearchTabOwner.SemiRandom;

            return IsTabOwnerAvailable(owner) ? owner : ResearchTabOwner.SemiRandom;
        }

        public static bool SemiRandomOwnsResearchTab =>
            GetEffectiveTabOwner() == ResearchTabOwner.SemiRandom;

        private static Type ResolveWindowType(ResearchTabOwner owner)
        {
            switch (owner)
            {
                case ResearchTabOwner.NodeResearch:
                    return NodeResearchWindowType;
                case ResearchTabOwner.YART:
                    return YartWindowType;
                case ResearchTabOwner.NiceResearchTab:
                    return NiceWindowType;
                case ResearchTabOwner.Vanilla:
                case ResearchTabOwner.Sleek:
                case ResearchTabOwner.Organized:
                    return typeof(MainTabWindow_Research);
                default:
                    return typeof(MainTabWindow_NextResearch);
            }
        }

        public static void Apply()
        {
            MainButtonDef researchTab = ResearchMainButton;
            if (researchTab == null || SemiRandomResearchMod.settings == null)
                return;

            // A tree's window type can be null while its assembly is still loading; the next Apply resolves it.
            Type windowType = ResolveWindowType(GetEffectiveTabOwner());
            if (windowType == null)
                return;

            SetResearchWindowClass(researchTab, windowType);
        }

        private static void SetResearchWindowClass(MainButtonDef researchTab, Type windowType)
        {
            if (researchTab == null)
                return;

            MainTabWindow cached = TabWindowIntField?.GetValue(researchTab) as MainTabWindow;
            // Exact type match: Node Research subclasses MainTabWindow_Research.
            if (researchTab.tabWindowClass == windowType &&
                cached != null &&
                cached.GetType() == windowType)
            {
                return;
            }

            if (cached != null)
                parkedWindows[cached.GetType()] = cached;

            researchTab.tabWindowClass = windowType;
            researchTab.ClearCachedData();

            // Assigned after ClearCachedData so the cache reset cannot drop the restored window.
            parkedWindows.TryGetValue(windowType, out MainTabWindow parked);
            TabWindowIntField?.SetValue(researchTab, parked);
        }

        // Saved without UpdateSettings, so swapping trees does not rebuild the offer list.
        private static void RememberTabOwner(ResearchTabOwner owner)
        {
            if (SemiRandomResearchMod.settings == null ||
                SemiRandomResearchMod.settings.researchTabOwner == owner)
            {
                return;
            }

            SemiRandomResearchMod.settings.researchTabOwner = owner;
            SemiRandomResearchMod.Instance?.SaveSettingsOnly();
        }

        public static void OpenResearchWindow(Type windowType, Window windowToClose, ResearchTabOwner owner)
        {
            MainButtonDef researchTab = ResearchMainButton;
            if (researchTab == null)
                return;

            windowToClose?.Close();
            SetResearchWindowClass(researchTab, windowType);

            MainTabWindow newWindow = researchTab.TabWindow;
            if (newWindow == null)
                return;

            RememberTabOwner(owner);

            WindowStack stack = Find.WindowStack;
            IList<Window> windows = stack.Windows;
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                Window existing = windows[i];
                if (existing != newWindow && windowType.IsInstanceOfType(existing))
                    existing.Close(doCloseSound: false);
            }

            if (!stack.IsOpen(newWindow) && !stack.Windows.Contains(newWindow))
                stack.Add(newWindow);
        }

        private static void ShowHandoverMessage(string restrictedKey, string freeKey)
        {
            SemiRandomResearchSettings settings = SemiRandomResearchMod.settings;
            if (settings != null && settings.suppressHandoverMessages)
                return;
            string key = settings != null && settings.featureEnabled ? restrictedKey : freeKey;
            Messages.Message(key.Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        public static void SwitchToNodeResearch(Window windowToClose)
        {
            if (!NodeResearchInstalled || NodeResearchWindowType == null)
                return;

            ShowHandoverMessage("CM_Semi_Random_Research_Handover_Node_Restricted", "CM_Semi_Random_Research_Handover_Node_Free");
            OpenResearchWindow(NodeResearchWindowType, windowToClose, ResearchTabOwner.NodeResearch);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToNiceResearchTab(Window windowToClose)
        {
            if (!NiceResearchTabInstalled || NiceWindowType == null)
                return;

            ShowHandoverMessage("CM_Semi_Random_Research_Handover_Nice_Restricted", "CM_Semi_Random_Research_Handover_Nice");
            OpenResearchWindow(NiceWindowType, windowToClose, ResearchTabOwner.NiceResearchTab);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToSemiRandomResearch(Window windowToClose)
        {
            if (AnyTreeInstalled)
                ShowHandoverMessage("CM_Semi_Random_Research_Handover_Semi_Restricted", "CM_Semi_Random_Research_Handover_Semi");

            ResearchProjectDef activeProj = Find.ResearchManager.GetProject();
            ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
            if (tracker != null && activeProj != null && !tracker.CurrentProject.Contains(activeProj))
            {
                // Bookkeeping, not a selection: the tracker catches up with what vanilla is already researching.
                tracker.SetCurrentProjectByKey(activeProj, ResearchTracker.GetCategoryKey(activeProj));
            }

            OpenResearchWindow(typeof(MainTabWindow_NextResearch), windowToClose, ResearchTabOwner.SemiRandom);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToYart(Window windowToClose)
        {
            if (!YartInstalled || YartWindowType == null)
                return;

            ShowHandoverMessage("CM_Semi_Random_Research_Handover_Yart_Restricted", "CM_Semi_Random_Research_Handover_Yart");
            OpenResearchWindow(YartWindowType, windowToClose, ResearchTabOwner.YART);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToSleek(Window windowToClose)
        {
            if (!SleekInstalled)
                return;

            ShowHandoverMessage("CM_Semi_Random_Research_Handover_Sleek_Restricted", "CM_Semi_Random_Research_Handover_Sleek");
            OpenResearchWindow(typeof(MainTabWindow_Research), windowToClose, ResearchTabOwner.Sleek);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToOrganized(Window windowToClose)
        {
            if (!OrganizedInstalled)
                return;

            ShowHandoverMessage("CM_Semi_Random_Research_Handover_Organized_Restricted", "CM_Semi_Random_Research_Handover_Organized");
            OpenResearchWindow(typeof(MainTabWindow_Research), windowToClose, ResearchTabOwner.Organized);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static bool IsTreeAvailable(PreferredResearchTree tree)
        {
            switch (tree)
            {
                case PreferredResearchTree.NodeResearch:
                    return NodeResearchInstalled && NodeResearchWindowType != null;
                case PreferredResearchTree.YART:
                    return YartInstalled && YartWindowType != null;
                case PreferredResearchTree.Sleek:
                    return SleekInstalled;
                case PreferredResearchTree.NiceResearchTab:
                    return NiceResearchTabInstalled && NiceWindowType != null;
                case PreferredResearchTree.Organized:
                    return OrganizedInstalled;
                default:
                    return false;
            }
        }

        // Fallback order: Node Research, YART, Sleek, Nice Research Tab, Research: Organized, then vanilla.
        public static PreferredResearchTree GetEffectivePreferredTree()
        {
            PreferredResearchTree preferred = SemiRandomResearchMod.settings != null
                ? SemiRandomResearchMod.settings.preferredResearchTree
                : PreferredResearchTree.NodeResearch;

            if (IsTreeAvailable(preferred))
                return preferred;

            if (IsTreeAvailable(PreferredResearchTree.NodeResearch))
                return PreferredResearchTree.NodeResearch;
            if (IsTreeAvailable(PreferredResearchTree.YART))
                return PreferredResearchTree.YART;
            if (IsTreeAvailable(PreferredResearchTree.Sleek))
                return PreferredResearchTree.Sleek;
            if (IsTreeAvailable(PreferredResearchTree.NiceResearchTab))
                return PreferredResearchTree.NiceResearchTab;
            if (IsTreeAvailable(PreferredResearchTree.Organized))
                return PreferredResearchTree.Organized;

            return PreferredResearchTree.NodeResearch;
        }

        public static void SwitchToPreferredTree(Window windowToClose)
        {
            PreferredResearchTree tree = GetEffectivePreferredTree();
            if (IsTreeAvailable(tree))
            {
                switch (tree)
                {
                    case PreferredResearchTree.NodeResearch:
                        SwitchToNodeResearch(windowToClose);
                        return;
                    case PreferredResearchTree.YART:
                        SwitchToYart(windowToClose);
                        return;
                    case PreferredResearchTree.Sleek:
                        SwitchToSleek(windowToClose);
                        return;
                    case PreferredResearchTree.NiceResearchTab:
                        SwitchToNiceResearchTab(windowToClose);
                        return;
                    case PreferredResearchTree.Organized:
                        SwitchToOrganized(windowToClose);
                        return;
                }
            }

            OpenResearchWindow(typeof(MainTabWindow_Research), windowToClose, ResearchTabOwner.Vanilla);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }
    }
}

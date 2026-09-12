using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    [StaticConstructorOnStartup]
    public partial class MainTabWindow_NextResearch : MainTabWindow
    {
        protected ResearchProjectDef selectedProject;

        protected override float Margin => 6f;

        private Vector2 leftScrollPosition = Vector2.zero;

        private float leftScrollViewHeight;
        private float leftScrollHeightForFrame;

        private Vector2 rightScrollPosition = Vector2.zero;

        private float rightScrollViewHeight;
        private float rightScrollHeightForFrame;

        private static readonly Color FulfilledPrerequisiteColor = Color.green;

        // Loaded lazily by EnsureIconsLoaded() rather than here: Harmony's PatchAll() JIT-compiles
        // patch methods that statically reference this class from a background thread during mod
        // construction, which forces this beforefieldinit type's static ctor to run there too -
        // triggering "loaded from a different thread" errors if the textures load eagerly.
        private static Texture2D SettingsIcon;
        private static Texture2D PlusIcon;
        private static Texture2D TotalIcon;
        private static Texture2D PacingIcon;
        private static Texture2D HistoryIcon;
        private static Texture2D ToggleOnIcon;
        private static Texture2D ToggleOffIcon;

        private static void EnsureIconsLoaded()
        {
            if (SettingsIcon != null)
                return;

            SettingsIcon = ContentFinder<Texture2D>.Get("UI/Settings", true);
            PlusIcon = ContentFinder<Texture2D>.Get("UI/Plus", true);
            TotalIcon = ContentFinder<Texture2D>.Get("UI/Total", true);
            PacingIcon = ContentFinder<Texture2D>.Get("UI/Pacing", true);
            HistoryIcon = ContentFinder<Texture2D>.Get("UI/History", true);
            ToggleOnIcon = ContentFinder<Texture2D>.Get("UI/ToggleOn", true);
            ToggleOffIcon = ContentFinder<Texture2D>.Get("UI/ToggleOff", true);
        }

        private static readonly Color AutoToggleOnColor = new Color(0.35f, 0.8f, 0.42f);

        private static readonly Color ActiveProjectLabelColor = new ColorInt(219, 201, 126, 255).ToColor;
        private static readonly Color FooterTreeButtonColor = new Color(0.22f, 0.38f, 0.55f);
        private static readonly Color FooterRerollButtonColor = new Color(0.55f, 0.38f, 0.14f);
        private static readonly Color FooterPauseButtonColor = new Color(0.50f, 0.42f, 0.16f);
        private static readonly Color FooterStartButtonColor = new Color(0.22f, 0.48f, 0.28f);
        private static readonly Color FooterDebugButtonColor = new Color(0.55f, 0.22f, 0.22f);

        private int currentRandomSeed = 0;

        private KnowledgeCategoryDef rerollButtonType = null;

        // Keyed on the def rather than its defName: the offer cards look their animation progress
        // up every frame, and hashing a string for each one is pure overhead.
        private Dictionary<ResearchProjectDef, float> animationProgress = new Dictionary<ResearchProjectDef, float>();
        private float lastRerollTime = -1f;
        private const float ANIMATION_DURATION = 0.25f; // Quarter second per item
        private const float ITEM_DELAY = 0.05f; // Very short delay between items
        private List<ResearchProjectDef> animationOrder = new List<ResearchProjectDef>();

        // TechLevel is a small contiguous enum, so both of these are flat arrays indexed by it.
        // As dictionaries they cost two lookups per research def per rebuild, which is a scan of
        // the whole database once a second while the tab is open.
        internal static readonly int TechLevelCount = (int)TechLevel.Archotech + 1;
        private float[] techLevelHeaderProgress = new float[TechLevelCount];
        private (int completed, int total, float remainingCost, float spentCost)[] cachedTechLevelStats;
        private TechLevel cachedWorldTech = TechLevel.Undefined;
        private int cachedOffersRevision = -1;
        private int cachedCompletionRevision = -1;
        private Dictionary<ResearchProjectDef, Def> cachedFirstUnlockable = new Dictionary<ResearchProjectDef, Def>();
        private Building_ResearchBench cachedMatchingBench;
        private ResearchProjectDef cachedMatchingBenchProject;
        private ResearchTracker cachedTracker;
        private ResearchRateTracker cachedRateTracker;
        private ResearchTracker drawTracker;
        private ResearchRateInfo cachedRateInfo;
        private ResearchProjectDef cachedRateInfoProject;
        private int cachedRateInfoTick = -1;
        private string cachedCurrentRateText = "—";
        private string cachedAvgRateText = "—";
        private string cachedEtaText = "—";
        private Color cachedEtaColor = new Color(0.7f, 0.7f, 0.7f);
        private List<float> cachedGraphSamples = new List<float>();
        private float cachedGraphAverage;
        private float cachedTenDayAverage;
        private List<ResearchProjectDef> cachedActiveProjects = new List<ResearchProjectDef>();
        private readonly List<Pair<TechLevel, List<ResearchProjectDef>>> cachedStandardGroups = new List<Pair<TechLevel, List<ResearchProjectDef>>>();
        private List<ResearchProjectDef> cachedAnomalyBasic = new List<ResearchProjectDef>();
        private List<ResearchProjectDef> cachedAnomalyAdvanced = new List<ResearchProjectDef>();
        private List<ResearchProjectDef> cachedGravshipProjects = new List<ResearchProjectDef>();
        private readonly HashSet<ResearchProjectDef> cachedFoundationProjects = new HashSet<ResearchProjectDef>();
        private readonly HashSet<ResearchProjectDef> cachedEmergenceProjects = new HashSet<ResearchProjectDef>();
        private float cachedSharedCostWidth;
        private int cachedLeftListsRevision = -1;
        private int cachedLeftCurrentHash = int.MinValue;
        private bool cachedSelectedCanStartNow;
        private ResearchProjectDef cachedCanStartNowProject;
        private int cachedCanStartNowTick = -1;
        private bool cachedCanReroll;
        private FooterStartMode cachedFooterStartMode;

        private enum FooterStartMode
        {
            Empty,
            Finished,
            InProgress,
            CanStart,
            Locked
        }
        private List<Def> cachedSelectedUnlocks;
        private ResearchProjectDef cachedUnlocksProject;
        private float cachedRequiredProgress = 1f;
        private bool loggedDrawError;

        private static bool IsRepaint => Event.current.type == EventType.Repaint;

        private static bool AnomalyContentEnabled()
        {
            return Compatibility.AnomalyResearchUnlocked() && KnowledgeCategoryDefOf.Basic != null;
        }

        private static string SafeLabel(ResearchProjectDef project)
        {
            if (project == null)
                return string.Empty;
            if (project.label.NullOrEmpty())
                return project.defName;
            return project.LabelCap.ToString();
        }

        private static string SafeDefLabel(Def def)
        {
            if (def == null)
                return string.Empty;
            if (def.label.NullOrEmpty())
                return def.defName;
            return def.LabelCap.ToString();
        }

        private static string SafeDescription(ResearchProjectDef project)
        {
            return project?.description ?? string.Empty;
        }

        // MouseDown hit-test only. GUI.Button/ButtonInvisible/ButtonImage change control IDs
        // when the number of calls differs between Layout and Repaint, which makes Unity
        // retry OnGUI until FPS/TPS collapse.
        private static bool Clicked(Rect rect)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
                return false;
            if (!Mouse.IsOver(rect))
                return false;
            Event.current.Use();
            return true;
        }

        private void RefreshCanStartNow(int tick)
        {
            if (selectedProject != cachedCanStartNowProject || tick - cachedCanStartNowTick >= 30)
            {
                cachedCanStartNowProject = selectedProject;
                cachedCanStartNowTick = tick;
                cachedSelectedCanStartNow = selectedProject != null && selectedProject.CanStartNow;
            }

            cachedCanReroll = cachedTracker != null && cachedTracker.CanReroll(rerollButtonType);
            RefreshFooterStartMode();
        }

        private void RefreshFooterStartMode()
        {
            if (selectedProject == null)
            {
                cachedFooterStartMode = FooterStartMode.Empty;
                return;
            }
            if (selectedProject.IsFinished)
            {
                cachedFooterStartMode = FooterStartMode.Finished;
                return;
            }
            if (cachedTracker != null && cachedTracker.CurrentProject.Contains(selectedProject))
            {
                cachedFooterStartMode = FooterStartMode.InProgress;
                return;
            }
            // Same rule the selection gate applies to external research trees, so the start button
            // and a handover to another tree can never disagree about what is startable.
            bool canStart = cachedSelectedCanStartNow &&
                cachedTracker != null &&
                cachedTracker.PlayerCanStartProject(selectedProject);
            cachedFooterStartMode = canStart ? FooterStartMode.CanStart : FooterStartMode.Locked;
        }

        internal void SelectFromExternal(ResearchProjectDef project)
        {
            if (project != null)
                selectedProject = project;
        }

        private void SelectDefaultProject()
        {
            ResearchProjectDef mainProject = null;
            if (cachedTracker != null)
            {
                List<ResearchProjectDef> current = cachedTracker.CurrentProject;
                for (int i = 0; i < current.Count; i++)
                {
                    ResearchProjectDef p = current[i];
                    if (p != null && !p.IsFinished && ResearchTracker.GetCategoryKey(p) == "Standard")
                    {
                        mainProject = p;
                        break;
                    }
                }

                if (mainProject == null)
                {
                    ResearchProjectDef vanillaProject = Find.ResearchManager.GetProject();
                    if (vanillaProject != null && !vanillaProject.IsFinished &&
                        ResearchTracker.GetCategoryKey(vanillaProject) == "Standard")
                    {
                        mainProject = vanillaProject;
                    }
                }

                selectedProject = mainProject;
                if (selectedProject == null)
                {
                    for (int i = 0; i < current.Count; i++)
                    {
                        ResearchProjectDef p = current[i];
                        if (p != null && !p.IsFinished)
                        {
                            selectedProject = p;
                            break;
                        }
                    }
                }
            }
            else
            {
                selectedProject = null;
            }

            if (selectedProject == null && currentAvailableProjects != null)
            {
                for (int i = 0; i < currentAvailableProjects.Count; i++)
                {
                    ResearchProjectDef p = currentAvailableProjects[i];
                    if (p != null && !p.IsFinished)
                    {
                        selectedProject = p;
                        break;
                    }
                }
            }
        }

        private void CacheFirstUnlockable(ResearchProjectDef project)
        {
            if (project == null || cachedFirstUnlockable.ContainsKey(project))
                return;

            Def result = null;
            try
            {
                List<Def> unlocked = project.UnlockedDefs;
                if (!unlocked.NullOrEmpty())
                {
                    int randomIndex = Rand.RangeInclusiveSeeded(0, unlocked.Count - 1, currentRandomSeed);
                    result = unlocked[randomIndex];
                }
            }
            catch (Exception)
            {
            }

            cachedFirstUnlockable[project] = result;
        }

        private void WarmUnlockCaches()
        {
            if (currentAvailableProjects != null)
            {
                for (int i = 0; i < currentAvailableProjects.Count; i++)
                    CacheFirstUnlockable(currentAvailableProjects[i]);
            }

            if (cachedTracker != null && cachedTracker.CurrentProject != null)
            {
                List<ResearchProjectDef> current = cachedTracker.CurrentProject;
                for (int i = 0; i < current.Count; i++)
                    CacheFirstUnlockable(current[i]);
            }

            WarmSelectedUnlocks();
        }

        private void WarmSelectedUnlocks()
        {
            if (selectedProject == cachedUnlocksProject)
                return;

            cachedUnlocksProject = selectedProject;
            cachedSelectedUnlocks = null;
            if (selectedProject == null)
                return;

            CacheFirstUnlockable(selectedProject);
            try
            {
                List<Def> unlocked = selectedProject.UnlockedDefs;
                cachedSelectedUnlocks = unlocked;
            }
            catch (Exception)
            {
            }

            if (selectedProject.prerequisites != null)
            {
                for (int i = 0; i < selectedProject.prerequisites.Count; i++)
                    CacheFirstUnlockable(selectedProject.prerequisites[i]);
            }
            if (selectedProject.hiddenPrerequisites != null)
            {
                for (int i = 0; i < selectedProject.hiddenPrerequisites.Count; i++)
                    CacheFirstUnlockable(selectedProject.hiddenPrerequisites[i]);
            }
        }

        private void RecacheMatchingBenchIfNeeded()
        {
            if (selectedProject == cachedMatchingBenchProject)
                return;

            cachedMatchingBenchProject = selectedProject;
            cachedMatchingBench = null;
            if (selectedProject == null || selectedProject.requiredResearchFacilities.NullOrEmpty())
                return;

            ThingDef requiredBench = selectedProject.requiredResearchBuilding;
            List<ThingDef> requiredFacilities = selectedProject.requiredResearchFacilities;
            float bestScore = 0f;
            Building_ResearchBench best = null;
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                foreach (Building_ResearchBench bench in maps[i].listerBuildings.AllBuildingsColonistOfClass<Building_ResearchBench>())
                {
                    if (requiredBench != null && bench.def != requiredBench)
                        continue;

                    float score = GetResearchBenchRequirementsScore(bench, requiredFacilities);
                    if (best == null || score > bestScore)
                    {
                        bestScore = score;
                        best = bench;
                    }
                }
            }
            cachedMatchingBench = best;
        }

        private void RefreshRateTexts(int tick)
        {
            ResearchProjectDef mainProject = null;
            for (int i = 0; i < cachedActiveProjects.Count; i++)
            {
                ResearchProjectDef project = cachedActiveProjects[i];
                if (project != null && ResearchTracker.GetCategoryKey(project) == "Standard")
                {
                    mainProject = project;
                    break;
                }
            }

            if (mainProject == null || cachedRateTracker == null)
            {
                cachedCurrentRateText = "—";
                cachedAvgRateText = "CM_Semi_Random_Research_RateZeroShort".Translate();
                cachedEtaText = "CM_Semi_Random_Research_Unknown".Translate().ToString();
                cachedEtaColor = new Color(0.7f, 0.7f, 0.7f);
                cachedRateInfo = null;
                cachedRateInfoProject = mainProject;
                cachedGraphSamples.Clear();
                cachedGraphAverage = 0f;
                cachedTenDayAverage = 0f;
                return;
            }

            if (mainProject == cachedRateInfoProject && cachedRateInfo != null && tick - cachedRateInfoTick < 30)
                return;

            cachedRateInfo = cachedRateTracker.GetResearchRateInfo(mainProject);
            cachedRateInfoProject = mainProject;
            cachedRateInfoTick = tick;

            bool hasRateData = cachedRateInfo != null && cachedRateInfo.TotalSamples > 0;
            float globalAverageRate = cachedRateInfo != null ? cachedRateInfo.AverageRate : 0f;
            cachedCurrentRateText = hasRateData
                ? ResearchRateTracker.FormatRateShort(cachedRateInfo.CurrentRate)
                : "CM_Semi_Random_Research_Calculating".Translate().ToString();
            if (hasRateData)
                cachedAvgRateText = ResearchRateTracker.FormatRateShort(cachedRateInfo.AverageRate);
            else if (globalAverageRate > 0f)
                cachedAvgRateText = ResearchRateTracker.FormatRateShort(globalAverageRate);
            else
                cachedAvgRateText = "CM_Semi_Random_Research_RateZeroShort".Translate();

            float estimatedDays = -1f;
            if (hasRateData && cachedRateInfo.EstimatedDaysToCompletion >= 0)
            {
                cachedEtaText = cachedRateInfo.ETAFormatted;
                estimatedDays = cachedRateInfo.EstimatedDaysToCompletion;
            }
            else if (globalAverageRate > 0f)
            {
                float remainingProgress = mainProject.CostApparent - mainProject.ProgressApparent;
                estimatedDays = remainingProgress / globalAverageRate;
                cachedEtaText = ResearchRateTracker.FormatETA(estimatedDays);
            }
            else
            {
                cachedEtaText = "CM_Semi_Random_Research_Unknown".Translate().ToString();
            }

            cachedEtaColor = new Color(0.7f, 0.7f, 0.7f);
            if (estimatedDays >= 0)
            {
                if (estimatedDays < 1f) cachedEtaColor = new Color(0.0f, 0.7f, 0.0f);
                else if (estimatedDays < 3f) cachedEtaColor = new Color(0.7f, 0.7f, 0.0f);
                else if (estimatedDays > 10f) cachedEtaColor = new Color(0.75f, 0.5f, 0.3f);
            }

            cachedGraphSamples = hasRateData
                ? cachedRateTracker.GetRateSamplesPeriod(mainProject, 3)
                : cachedRateTracker.GetGlobalRateSamplesPeriod(3);
            cachedGraphAverage = hasRateData
                ? cachedRateTracker.GetAverageRate(mainProject)
                : cachedRateTracker.GetGlobalAverageRate();
            cachedTenDayAverage = hasRateData
                ? cachedRateInfo.AverageRate
                : cachedRateTracker.GetGlobalAverageRate();
        }

        private bool ColonistsHaveResearchBench
        {
            get
            {
                bool result = false;
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    if (maps[i].listerBuildings.ColonistsHaveResearchBench())
                    {
                        result = true;
                        break;
                    }
                }
                return result;
            }
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth * 0.585f, UI.screenHeight * 0.7f);

        public override Vector2 RequestedTabSize => InitialSize;

        public List<ResearchProjectDef> currentAvailableProjects = new List<ResearchProjectDef>();

        public MainTabWindow_NextResearch()
        {
            // Fully faded in until a reroll says otherwise. As a dictionary a missing entry read
            // as 1f; an array starts at 0f, which would hide every tech level header.
            SetAllHeaderProgress(1f);

            this.def = MainButtonDefOf.Research;
            this.doCloseX = false;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            // The history view is never remembered between openings.
            showingHistory = false;
            selectionBeforeHistory = null;

            currentRandomSeed = Rand.Int;
            cachedTracker = SemiRandomResearchUtility.Tracker;
            cachedRateTracker = SemiRandomResearchUtility.RateTracker;

            // Arms auto research. Until the tab has been opened once, a freshly started or
            // freshly loaded colony picks nothing on its own.
            cachedTracker?.NotifyResearchTabOpened();
            cachedRequiredProgress = ProgressionCoreActive ? GetRequiredProgressionPercent() : 1f;

            if (cachedTracker != null)
            {
                CopyAvailableProjects(cachedTracker.GetCurrentlyAvailableProjects());

                List<ResearchProjectDef> current = cachedTracker.CurrentProject;
                for (int i = current.Count - 1; i >= 0; i--)
                {
                    ResearchProjectDef def = current[i];
                    if (!Compatibility.SatisfiesAlienRaceRestriction(def))
                    {
                        string categoryKey = ResearchTracker.GetCategoryKey(def);
                        cachedTracker.SetCurrentProjectByKey(null, categoryKey);
                    }
                }

                SelectDefaultProject();

                RebuildAnimationOrder(currentAvailableProjects, 1f);

                SetAllHeaderProgress(1f);

                RebuildTechLevelStats();
                RefreshWorldTech();
                cachedOffersRevision = cachedTracker.OffersRevision;
                cachedCompletionRevision = cachedTracker.CompletionRevision;
            }

            cachedFirstUnlockable.Clear();
            cachedMatchingBench = null;
            cachedMatchingBenchProject = null;
            cachedLeftListsRevision = -1;
            cachedLeftCurrentHash = int.MinValue;
            cachedRateInfo = null;
            cachedRateInfoProject = null;
            cachedRateInfoTick = -1;
            cachedCanStartNowProject = null;
            cachedCanStartNowTick = -1;
            cachedUnlocksProject = null;
            cachedSelectedUnlocks = null;
            InvalidateLeftColumnCache();
            RebuildLeftColumnLists(cachedTracker);
            WarmUnlockCaches();
            RecacheMatchingBenchIfNeeded();
            int tick = Find.TickManager.TicksGame;
            RefreshCanStartNow(tick);
            RefreshRateTexts(tick);
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            try
            {
                UpdateWindowState();
            }
            catch (Exception ex)
            {
                if (!loggedDrawError)
                {
                    loggedDrawError = true;
                    Log.Error("[Semi Random Research] WindowUpdate failed: " + ex);
                }
            }
        }

        private void UpdateWindowState()
        {
            ResearchTracker researchTracker = cachedTracker;
            if (researchTracker == null)
                return;

            int tick = Find.TickManager.TicksGame;

            if (researchTracker.OffersRevision != cachedOffersRevision)
            {
                cachedOffersRevision = researchTracker.OffersRevision;
                CopyAvailableProjects(researchTracker.PeekAvailableProjects());

                RebuildAnimationOrder(currentAvailableProjects, 0f);
                lastRerollTime = Time.realtimeSinceStartup;
                InvalidateLeftColumnCache();
                RebuildLeftColumnLists(researchTracker);
                WarmUnlockCaches();
            }

            // Driven by real-time WindowUpdate rather than the tick-throttled RefreshTechLevelStats
            // below, so a completion that auto-pauses the game (frozen ticks) still refreshes the
            // progress bar right away instead of waiting for the window to be reopened.
            if (researchTracker.CompletionRevision != cachedCompletionRevision)
            {
                cachedCompletionRevision = researchTracker.CompletionRevision;
                RebuildTechLevelStats();
            }

            // The history view intentionally keeps a finished project selected so the right
            // column can show what it unlocked. Outside it, a selection whose card is no longer
            // drawn falls back to the default, so the right column never describes a card that
            // is not on screen.
            if (!showingHistory &&
                (selectedProject == null || selectedProject.IsFinished ||
                (currentAvailableProjects != null && !currentAvailableProjects.Contains(selectedProject) &&
                 (cachedTracker == null || !cachedTracker.CurrentProject.Contains(selectedProject))) ||
                SelectionHiddenByBusyCategory()))
            {
                SelectDefaultProject();
            }

            if (lastRerollTime > 0f)
            {
                float timeSinceReroll = Time.realtimeSinceStartup - lastRerollTime;
                bool allComplete = true;

                for (int i = 0; i < animationOrder.Count; i++)
                {
                    ResearchProjectDef project = animationOrder[i];
                    float startTime = i * ITEM_DELAY;
                    float endTime = startTime + ANIMATION_DURATION;

                    if (timeSinceReroll >= startTime && timeSinceReroll <= endTime)
                    {
                        animationProgress[project] = (timeSinceReroll - startTime) / ANIMATION_DURATION;
                        allComplete = false;
                    }
                    else if (timeSinceReroll < startTime)
                    {
                        animationProgress[project] = 0f;
                        allComplete = false;
                    }
                    else
                    {
                        animationProgress[project] = 1f;
                    }
                }

                SetAllHeaderProgress(Mathf.Clamp01(timeSinceReroll / ANIMATION_DURATION * 1.2f));

                if (allComplete)
                    lastRerollTime = -1f;
            }

            WarmSelectedUnlocks();
            RecacheMatchingBenchIfNeeded();
            RefreshCanStartNow(tick);
            RefreshRateTexts(tick);
            RefreshTechLevelStats(tick);
        }

        // True when the selected offer is one the left column hides because its category already
        // has a project running and switching is off. Mirrors the filter in RebuildLeftColumnLists.
        private bool SelectionHiddenByBusyCategory()
        {
            if (selectedProject == null || cachedTracker == null)
                return false;
            if (SemiRandomResearchMod.settings == null || SemiRandomResearchMod.settings.allowSwitchingResearch)
                return false;

            List<ResearchProjectDef> current = cachedTracker.CurrentProject;
            if (current == null || current.Contains(selectedProject))
                return false;

            string selectedKey = ResearchTracker.GetCategoryKey(selectedProject);
            for (int i = 0; i < current.Count; i++)
            {
                ResearchProjectDef active = current[i];
                if (active != null && !active.IsFinished && ResearchTracker.GetCategoryKey(active) == selectedKey)
                    return true;
            }

            return false;
        }

        private void CopyAvailableProjects(List<ResearchProjectDef> source)
        {
            if (currentAvailableProjects == null || ReferenceEquals(currentAvailableProjects, source))
                currentAvailableProjects = new List<ResearchProjectDef>();
            else
                currentAvailableProjects.Clear();

            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
                currentAvailableProjects.Add(source[i]);
        }

        // The tech level bands all animate together, so they are only ever written as a set.
        private void SetAllHeaderProgress(float progress)
        {
            for (int i = 0; i < techLevelHeaderProgress.Length; i++)
                techLevelHeaderProgress[i] = progress;
        }

        private float GetHeaderProgress(TechLevel techLevel)
        {
            int index = (int)techLevel;
            return index >= 0 && index < techLevelHeaderProgress.Length ? techLevelHeaderProgress[index] : 1f;
        }

        private static readonly Comparison<ResearchProjectDef> AnimationOrderComparison = (a, b) =>
        {
            int tech = ((int)a.techLevel).CompareTo((int)b.techLevel);
            if (tech != 0)
                return tech;
            return a.CostApparent.CompareTo(b.CostApparent);
        };

        private void RebuildAnimationOrder(IEnumerable<ResearchProjectDef> projects, float initialProgress)
        {
            animationOrder.Clear();
            if (projects == null)
                return;

            foreach (ResearchProjectDef projectDef in projects)
            {
                if (projectDef != null)
                    animationOrder.Add(projectDef);
            }

            animationOrder.Sort(AnimationOrderComparison);

            for (int i = 0; i < animationOrder.Count; i++)
                animationProgress[animationOrder[i]] = initialProgress;
        }

        public override void DoWindowContents(Rect canvas)
        {
            EventType eventType = Event.current.type;
            if (eventType == EventType.Ignore || eventType == EventType.MouseMove)
                return;

            EnsureIconsLoaded();

            try
            {
                DrawWindowBody(canvas);
            }
            catch (Exception ex)
            {
                if (!loggedDrawError)
                {
                    loggedDrawError = true;
                    Log.Error("[Semi Random Research] DoWindowContents failed: " + ex);
                }
            }
        }

        private void DrawWindowBody(Rect canvas)
        {
            drawTracker = cachedTracker;

            if (lastRerollTime > 0f && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                Event.current.Use();
                SoundDefOf.Click.PlayOneShotOnCamera();
                SkipAnimation();
            }

            float progressBarHeight = 70f;
            float progressLabelHeight = 55f;
            float totalProgressHeight = progressBarHeight + progressLabelHeight;
            float columnMargin = 16f;
            float topMargin = 6f;
            float arrowBottomPadding = 24f;

            Rect progressRect = new Rect(
                columnMargin,
                topMargin + progressLabelHeight,
                canvas.width - (columnMargin * 2),
                progressBarHeight + arrowBottomPadding
            );
            DrawTechLevelProgress(progressRect);

            float mainContentY = topMargin + totalProgressHeight + arrowBottomPadding + 6f;
            float availableHeight = canvas.height - mainContentY;

            float leftWidth = canvas.width * 0.55f;
            float rightWidth = canvas.width * 0.45f;

            // Match the left column's footer strip so both columns share the same bottom line break.
            float rightFooterPaddingTop = 12f;
            float footerButtonHeight = 40f;
            float footerButtonWidth = 120f;
            float iconSize = 28f;
            float rightFooterPaddingBottom = 12f;
            float rightFooterHeight = rightFooterPaddingTop + footerButtonHeight + rightFooterPaddingBottom;

            Rect leftRect = new Rect(columnMargin, mainContentY, leftWidth - columnMargin, availableHeight);
            Rect rightRect = new Rect(leftWidth + columnMargin, mainContentY, rightWidth - (columnMargin * 2), availableHeight);
            Rect rightContentRect = new Rect(rightRect.x, rightRect.y, rightRect.width, rightRect.height - rightFooterHeight);
            Rect rightFooterRect = new Rect(rightRect.x, rightRect.yMax - rightFooterHeight, rightRect.width, rightFooterHeight);

            if (showingHistory)
                DrawHistoryColumn(leftRect);
            else
                DrawLeftColumn(leftRect);

            DrawRightColumn(rightContentRect);

            if (IsRepaint)
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                Widgets.DrawLineHorizontal(rightFooterRect.x, rightFooterRect.y, rightFooterRect.width);
                GUI.color = Color.white;
            }

            float footerButtonY = rightFooterRect.y + rightFooterPaddingTop;

            if (Prefs.DevMode && selectedProject != null && !selectedProject.IsFinished)
            {
                Rect debugButtonRect = new Rect(
                    rightFooterRect.x + (rightFooterRect.width - footerButtonWidth) / 2f,
                    footerButtonY,
                    footerButtonWidth,
                    footerButtonHeight);

                if (ColoredButtonText(debugButtonRect, "CM_Semi_Random_Research_FinishNow".Translate(), FooterDebugButtonColor))
                {
                    // Ungated: the dev button finishes whatever is selected, gate or no gate.
                    ResearchTracker.SetVanillaProjectUngated(selectedProject);
                    Find.ResearchManager.FinishProject(selectedProject);

                    ResearchTracker researchTracker = cachedTracker ?? Current.Game.World.GetComponent<ResearchTracker>();
                    string categoryKey = ResearchTracker.GetCategoryKey(selectedProject);
                    researchTracker.SetCurrentProjectByKey(selectedProject, categoryKey);
                    researchTracker.ConsiderProjectFinished(selectedProject);
                    researchTracker.GetCurrentlyAvailableProjects();
                }
            }

            float iconGap = 4f;
            float iconY = footerButtonY + (footerButtonHeight - iconSize) / 2f;
            float nextIconX = rightFooterRect.xMax - iconSize;

            Rect settingsBtnRect = new Rect(nextIconX, iconY, iconSize, iconSize);
            DrawFooterIconButton(settingsBtnRect, SettingsIcon, null, "CM_Semi_Random_Research_SettingsTip", () =>
            {
                Mod ourMod = LoadedModManager.GetMod<SemiRandomResearchMod>();
                Find.WindowStack.Add(new Dialog_ModSettings(ourMod));
            });

            nextIconX -= iconSize + iconGap;
            DrawHistoryToggleButton(new Rect(nextIconX, iconY, iconSize, iconSize));

            float autoToggleHeight = 24f;
            DrawAutoPickToggle(new Rect(
                rightFooterRect.x,
                footerButtonY + (footerButtonHeight - autoToggleHeight) / 2f,
                autoToggleHeight * 1.78f,
                autoToggleHeight));

            DrawPackedModSettingsIcon(ResearchInflationInstalled, PlusIcon, "+", ResearchInflationPackageId,
                "CM_Semi_Random_Research_InflationSettingsTip", iconSize, iconGap, iconY, ref nextIconX);
            DrawPackedModSettingsIcon(ResearchTotalInstalled, TotalIcon, "T", ResearchTotalPackageId,
                "CM_Semi_Random_Research_TotalSettingsTip", iconSize, iconGap, iconY, ref nextIconX);
            DrawPackedModSettingsIcon(PacingManagerInstalled, PacingIcon, "P", PacingManagerPackageId,
                "CM_Semi_Random_Research_PacingSettingsTip", iconSize, iconGap, iconY, ref nextIconX);
        }

        private void DrawPackedModSettingsIcon(bool installed, Texture2D icon, string fallbackText, string packageId,
            string tooltipKey, float iconSize, float iconGap, float iconY, ref float nextIconX)
        {
            if (!installed)
                return;

            nextIconX -= iconSize + iconGap;
            Rect btnRect = new Rect(nextIconX, iconY, iconSize, iconSize);
            DrawFooterIconButton(btnRect, icon, fallbackText, tooltipKey, () =>
            {
                Mod mod = FindModByPackageId(packageId);
                if (mod != null)
                    Find.WindowStack.Add(new Dialog_ModSettings(mod));
            });
        }

        private void DrawFooterIconButton(Rect rect, Texture2D icon, string fallbackText, string tooltipKey, Action onClicked)
        {
            if (IsRepaint && icon != null)
                Widgets.DrawTextureFitted(rect, icon, 1f);
            else if (IsRepaint && !string.IsNullOrEmpty(fallbackText))
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(rect, fallbackText);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            if (Clicked(rect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                onClicked?.Invoke();
            }
            TooltipHandler.TipRegion(rect, tooltipKey.Translate());
        }

        private void DrawHistoryToggleButton(Rect rect)
        {
            if (IsRepaint && HistoryIcon != null)
            {
                Color old = GUI.color;
                GUI.color = showingHistory ? ActiveProjectLabelColor : Color.white;
                Widgets.DrawTextureFitted(rect, HistoryIcon, 1f);
                GUI.color = old;
            }

            if (Clicked(rect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                if (showingHistory)
                    CloseHistory();
                else
                    OpenHistory();
            }

            TooltipHandler.TipRegion(rect, showingHistory
                ? "CM_Semi_Random_Research_HistoryBackTip".Translate()
                : "CM_Semi_Random_Research_HistoryTip".Translate());
        }

        // Auto mode toggle. Deliberately does not pick anything while the window is open -
        // ResearchTracker.AutoPickNow runs on close, so a curious click cannot steal the choice.
        private void DrawAutoPickToggle(Rect toggleRect)
        {
            bool on = SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.autoPickNextResearch;

            Text.Font = GameFont.Tiny;
            string label = "CM_Semi_Random_Research_AutoToggleLabel".Translate();
            float labelWidth = Text.CalcSize(label).x + 4f;
            Rect labelRect = new Rect(toggleRect.xMax + 6f, toggleRect.y, labelWidth, toggleRect.height);
            Rect hitRect = new Rect(toggleRect.x, toggleRect.y, labelRect.xMax - toggleRect.x, toggleRect.height);

            if (IsRepaint)
            {
                Color old = GUI.color;
                GUI.color = on ? AutoToggleOnColor : Color.white;
                Texture2D icon = on ? ToggleOnIcon : ToggleOffIcon;
                if (icon != null)
                    Widgets.DrawTextureFitted(toggleRect, icon, 1f);

                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = on ? AutoToggleOnColor : new Color(0.8f, 0.8f, 0.8f);
                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = old;
            }
            Text.Font = GameFont.Small;

            if (Clicked(hitRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                if (SemiRandomResearchMod.settings != null)
                {
                    SemiRandomResearchMod.settings.autoPickNextResearch = !on;
                    SemiRandomResearchMod.Instance?.WriteSettings();
                }
            }

            TooltipHandler.TipRegion(hitRect, "CM_Semi_Random_Research_AutoToggleTip".Translate() + "\n\n" +
                (on
                    ? "CM_Semi_Random_Research_AutoToggleStateOn".Translate()
                    : "CM_Semi_Random_Research_AutoToggleStateOff".Translate()));
        }

        private const string ResearchInflationPackageId = "cruesoe.research.inflation";
        private const string ResearchTotalPackageId = "cruesoe.research.total";
        private const string PacingManagerPackageId = "ferny.pacingmanager";

        private static bool? researchInflationInstalledCached;
        private static bool? researchTotalInstalledCached;
        private static bool? pacingManagerInstalledCached;

        private static bool ResearchInflationInstalled
        {
            get
            {
                if (researchInflationInstalledCached == null)
                    researchInflationInstalledCached = ModLister.GetActiveModWithIdentifier(ResearchInflationPackageId) != null;
                return researchInflationInstalledCached.Value;
            }
        }

        private static bool ResearchTotalInstalled
        {
            get
            {
                if (researchTotalInstalledCached == null)
                    researchTotalInstalledCached = ModLister.GetActiveModWithIdentifier(ResearchTotalPackageId) != null;
                return researchTotalInstalledCached.Value;
            }
        }

        private static bool PacingManagerInstalled
        {
            get
            {
                if (pacingManagerInstalledCached == null)
                    pacingManagerInstalledCached = ModLister.GetActiveModWithIdentifier(PacingManagerPackageId) != null;
                return pacingManagerInstalledCached.Value;
            }
        }

        private static Mod FindModByPackageId(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return null;

            foreach (Mod mod in LoadedModManager.ModHandles)
            {
                if (mod?.Content == null)
                    continue;

                if (string.Equals(mod.Content.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mod.Content.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    return mod;
                }
            }

            return null;
        }

        private void SkipAnimation()
        {
            if (lastRerollTime <= 0f)
                return;

            for (int i = 0; i < animationOrder.Count; i++)
            {
                animationProgress[animationOrder[i]] = 1f;
            }

            SetAllHeaderProgress(1f);

            lastRerollTime = -1f;
        }

        public override void PreClose()
        {
            base.PreClose();

            SkipAnimation();

            showingHistory = false;
            selectionBeforeHistory = null;

            // Auto mode picks here rather than while the window is open, so the player always
            // gets the chance to choose (or to undo an accidental toggle) first.
            if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.autoPickNextResearch &&
                Current.Game != null)
            {
                ResearchTracker tracker = cachedTracker ?? Current.Game.World?.GetComponent<ResearchTracker>();
                tracker?.AutoPickNow();
            }
        }
    }
}

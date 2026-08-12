using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private Vector2 rightScrollPosition = Vector2.zero;

        private float rightScrollViewHeight;

        private static readonly Color FulfilledPrerequisiteColor = Color.green;

        private static readonly Texture2D SettingsIcon = ContentFinder<Texture2D>.Get("UI/Settings", true);

        private static readonly Color ActiveProjectLabelColor = new ColorInt(219, 201, 126, 255).ToColor;
        private static readonly Color FooterTreeButtonColor = new Color(0.22f, 0.38f, 0.55f);
        private static readonly Color FooterRerollButtonColor = new Color(0.55f, 0.38f, 0.14f);
        private static readonly Color FooterStartButtonColor = new Color(0.22f, 0.48f, 0.28f);

        private Dictionary<ResearchProjectDef, List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>> cachedUnlockedDefsGroupedByPrerequisites;

        private static List<Building> tmpAllBuildings = new List<Building>();

        private int currentRandomSeed = 0;

        bool errorDetected = false;

        private KnowledgeCategoryDef rerollButtonType = null;

        private Dictionary<string, float> animationProgress = new Dictionary<string, float>();
        private float lastRerollTime = -1f;
        private const float ANIMATION_DURATION = 0.25f; // Quarter second per item
        private const float ITEM_DELAY = 0.05f; // Very short delay between items
        private List<string> animationOrder = new List<string>();

        private Dictionary<TechLevel, float> techLevelHeaderProgress = new Dictionary<TechLevel, float>();

        private Func<bool> AnomalyContentEnabled = () => (KnowledgeCategoryDefOf.Basic != null);

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

        public List<ResearchProjectDef> currentAvailableProjects = new List<ResearchProjectDef>();

        public MainTabWindow_NextResearch()
        {
            this.def = MainButtonDefOf.Research;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            currentRandomSeed = Rand.Int;

            ResearchTracker researchTracker = Current.Game.World.GetComponent<ResearchTracker>();

            if (researchTracker != null)
            {
                researchTracker.WorldComponentTick();

                currentAvailableProjects = researchTracker.GetCurrentlyAvailableProjects();

                foreach (ResearchProjectDef def in researchTracker.CurrentProject.ToList())
                {
                    if (!Compatibility.SatisfiesAlienRaceRestriction(def))
                    {
                        // Fix applied here to prevent wiping standard research on race restriction failure
                        string categoryKey = ResearchTracker.GetCategoryKey(def);
                        researchTracker.SetCurrentProjectByKey(null, categoryKey);
                    }
                }

                ResearchProjectDef mainProject = researchTracker.CurrentProject.FirstOrDefault(p => ResearchTracker.GetCategoryKey(p) == "Standard");

                if (mainProject == null)
                {
                    ResearchProjectDef vanillaProject = Find.ResearchManager.GetProject();
                    if (vanillaProject != null && ResearchTracker.GetCategoryKey(vanillaProject) == "Standard")
                    {
                        mainProject = vanillaProject;
                    }
                }

                selectedProject = mainProject
                                  ?? researchTracker.CurrentProject.FirstOrDefault()
                                  ?? currentAvailableProjects.FirstOrDefault();

                // Use the same sorting logic as the reroll animation.
                // Items should be fully visible immediately (no animation) on first open.
                RebuildAnimationOrder(currentAvailableProjects, 1f);

                foreach (TechLevel techLevel in Enum.GetValues(typeof(TechLevel)))
                {
                    techLevelHeaderProgress[techLevel] = 1f;
                }
            }

            cachedUnlockedDefsGroupedByPrerequisites = null;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            ResearchTracker researchTracker = Current.Game.World.GetComponent<ResearchTracker>();

            if (researchTracker != null)
            {
                bool shouldUpdateProjects = false;
                List<ResearchProjectDef> newProjects = researchTracker.GetCurrentlyAvailableProjects();

                if (newProjects.Count != currentAvailableProjects.Count)
                {
                    shouldUpdateProjects = true;
                }
                else
                {
                    foreach (var project in newProjects)
                    {
                        if (!currentAvailableProjects.Contains(project))
                        {
                            shouldUpdateProjects = true;
                            break;
                        }
                    }
                }

                if (shouldUpdateProjects)
                {
                    currentAvailableProjects = newProjects;

                    // Items should start hidden and animate in.
                    RebuildAnimationOrder(currentAvailableProjects, 0f);

                    lastRerollTime = Time.realtimeSinceStartup;

                    foreach (TechLevel techLevel in Enum.GetValues(typeof(TechLevel)))
                    {
                        techLevelHeaderProgress[techLevel] = 0f;
                    }
                }

                if (lastRerollTime > 0f)
                {
                    float timeSinceReroll = Time.realtimeSinceStartup - lastRerollTime;
                    bool allComplete = true;

                    for (int i = 0; i < animationOrder.Count; i++)
                    {
                        string defName = animationOrder[i];

                        float startTime = i * ITEM_DELAY;
                        float endTime = startTime + ANIMATION_DURATION;

                        if (timeSinceReroll >= startTime && timeSinceReroll <= endTime)
                        {
                            float itemProgress = (timeSinceReroll - startTime) / ANIMATION_DURATION;
                            animationProgress[defName] = itemProgress;
                            allComplete = false;
                        }
                        else if (timeSinceReroll < startTime)
                        {
                            animationProgress[defName] = 0f;
                            allComplete = false;
                        }
                        else
                        {
                            animationProgress[defName] = 1f;
                        }
                    }

                    var groupedByTechLevel = currentAvailableProjects
                        .GroupBy(p => p.techLevel)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var techGroup in groupedByTechLevel)
                    {
                        TechLevel techLevel = techGroup.Key;
                        List<ResearchProjectDef> projects = techGroup.Value;

                        string firstProjectDefName = projects
                            .OrderBy(p => animationOrder.IndexOf(p.defName))
                            .Select(p => p.defName)
                            .FirstOrDefault();

                        if (firstProjectDefName != null)
                        {
                            // Header animation should be slightly ahead of the first project
                            float projectProgress = animationProgress.TryGetValue(firstProjectDefName, out float progress) ? progress : 0f;

                            // But never go backward (only increase)
                            float currentHeaderProgress = techLevelHeaderProgress.TryGetValue(techLevel, out float hp) ? hp : 0f;
                            float newHeaderProgress = Mathf.Max(currentHeaderProgress, projectProgress * 1.2f);
                            techLevelHeaderProgress[techLevel] = Mathf.Min(newHeaderProgress, 1f);
                        }
                    }

                    if (allComplete)
                    {
                        lastRerollTime = -1f;
                    }
                }

                if (!currentAvailableProjects.Contains(selectedProject))
                    selectedProject = researchTracker.CurrentProject.FirstOrFallback(null);
            }
        }

        // Shared "group by tech level, lowest tech first" query - used everywhere projects
        // need to be displayed/animated in tech-level order.
        private static IOrderedEnumerable<IGrouping<TechLevel, ResearchProjectDef>> GroupByTechLevel(IEnumerable<ResearchProjectDef> projects)
        {
            return projects
                .GroupBy(proj => proj.techLevel)
                .OrderBy(group => (int)group.Key);
        }

        // Rebuilds animationOrder (and the matching animationProgress entries) from a project
        // list, using the same tech-level-then-cost ordering the list is displayed in.
        // initialProgress should be 1f when the items should appear fully visible immediately
        // (e.g. on first window open) or 0f when they should animate in (e.g. after a reroll).
        private void RebuildAnimationOrder(IEnumerable<ResearchProjectDef> projects, float initialProgress)
        {
            animationOrder.Clear();
            foreach (var techGroup in GroupByTechLevel(projects))
            {
                foreach (ResearchProjectDef projectDef in techGroup.OrderBy(p => p.CostApparent))
                {
                    animationOrder.Add(projectDef.defName);
                    animationProgress[projectDef.defName] = initialProgress;
                }
            }
        }

        public override void DoWindowContents(Rect canvas)
        {
            if (lastRerollTime > 0f && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                Event.current.Use();
                SoundDefOf.Click.PlayOneShotOnCamera();
                SkipAnimation();
            }

            float progressBarHeight = 70f;
            float progressLabelHeight = 55f;  // Increased from 20f to 40f to provide more space for staggered labels
            float totalProgressHeight = progressBarHeight + progressLabelHeight;
            float horizontalMargin = 40f;  // Increased horizontal margin for progress bar
            float topMargin = 6f;  // Increased from 6f to 20f to provide more space at the top
            float arrowBottomPadding = 24f; // Extra space for the arrow and label

            Rect progressRect = new Rect(
                horizontalMargin,  // Left margin
                topMargin + progressLabelHeight,
                canvas.width - (horizontalMargin * 2),  // Account for both margins
                progressBarHeight + arrowBottomPadding  // Add padding for arrow
            );
            DrawTechLevelProgress(progressRect);

            float mainContentY = topMargin + totalProgressHeight + arrowBottomPadding + 6f;
            float availableHeight = canvas.height - mainContentY;

            float leftWidth = canvas.width * 0.55f;    // 55% for random list
            float rightWidth = canvas.width * 0.45f;   // 45% for details

            float columnMargin = 16f;

            Rect leftRect = new Rect(columnMargin, mainContentY, leftWidth - columnMargin, availableHeight);
            Rect rightRect = new Rect(leftWidth + columnMargin, mainContentY, rightWidth - (columnMargin * 2), availableHeight);

            DrawLeftColumn(leftRect);
            DrawRightColumn(rightRect);

            float iconSize = 24f;

            Rect settingsBtnRect = new Rect(canvas.width - iconSize, canvas.height - iconSize, iconSize, iconSize);

            if (Widgets.ButtonImage(settingsBtnRect, SettingsIcon))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                Mod ourMod = LoadedModManager.GetMod<SemiRandomResearchMod>();
                Dialog_ModSettings dialog = new Dialog_ModSettings(ourMod);
                Find.WindowStack.Add(dialog);
            }

            TooltipHandler.TipRegion(settingsBtnRect, "Open Semi-Random Research Settings");
        }

        private void SkipAnimation()
        {
            if (lastRerollTime <= 0f)
                return;

            foreach (string defName in animationOrder)
            {
                animationProgress[defName] = 1f;
            }

            foreach (TechLevel techLevel in Enum.GetValues(typeof(TechLevel)))
            {
                techLevelHeaderProgress[techLevel] = 1f;
            }

            lastRerollTime = -1f;
        }

        public override void PreClose()
        {
            base.PreClose();

            SkipAnimation();
        }
    }
}

using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CM_Semi_Random_Research
{
    public class ResearchTracker : WorldComponent
    {
        private List<ResearchProjectDef> currentAvailableProjects = new List<ResearchProjectDef>();
        private List<ResearchProjectDef> lastOfferedProjects;
        private List<ResearchProjectDef> lastOfferedForRevision;
        private Dictionary<ResearchProjectDef, int> notChosenProjects = new Dictionary<ResearchProjectDef, int>();
        private Dictionary<string, int> currentRerollState = new Dictionary<string, int>();
        private List<ResearchProjectDef> currentProjects = new List<ResearchProjectDef>();
        private HashSet<ResearchProjectDef> additionalAvailableProjects = new HashSet<ResearchProjectDef>();

        // Offer slots emptied by snoozing, per category. They stay empty until that category next rerolls or finishes a project.
        private Dictionary<string, int> snoozeHeldSlots = new Dictionary<string, int>();

        public List<ResearchProjectDef> CurrentProject => currentProjects;

        // Newest first.
        private List<ResearchHistoryEntry> completedHistory = new List<ResearchHistoryEntry>();
        private const int MaxCompletedHistoryEntries = 40;

        public List<ResearchHistoryEntry> CompletedHistory => completedHistory;

        // The paused category. A null key with the flag set comes from an older save and pauses every category.
        private bool researchPaused;
        private string pausedTypeKey;
        public bool ResearchPaused => researchPaused;

        public bool IsPaused(string typeKey)
        {
            return researchPaused && (pausedTypeKey == null || pausedTypeKey == typeKey);
        }

        // The tracked project that is paused, or null.
        public ResearchProjectDef PausedProject
        {
            get
            {
                if (!researchPaused)
                    return null;
                for (int i = 0; i < currentProjects.Count; i++)
                {
                    ResearchProjectDef project = currentProjects[i];
                    if (project != null && !project.IsFinished && IsPaused(GetCategoryKey(project)))
                        return project;
                }
                return null;
            }
        }

        public bool IsProjectPaused(ResearchProjectDef project)
        {
            return project != null &&
                IsPaused(GetCategoryKey(project)) &&
                currentProjects.Contains(project) &&
                !Find.ResearchManager.IsCurrentProject(project);
        }

        // Bumped when the offer list's contents change.
        public int OffersRevision { get; private set; }

        // Bumped when a project finishes. Separate from OffersRevision, and not tied to ticks, which stop while paused.
        public int CompletionRevision { get; private set; }

        public List<ResearchProjectDef> PeekAvailableProjects() => lastOfferedProjects ?? currentAvailableProjects;

        private void PublishOffers(List<ResearchProjectDef> offers)
        {
            if (offers == null)
                offers = new List<ResearchProjectDef>();
            lastOfferedProjects = offers;
            if (!SameProjectList(lastOfferedForRevision, offers))
            {
                lastOfferedForRevision = new List<ResearchProjectDef>(offers);
                OffersRevision++;
            }
        }

        private bool HasRerollableOffers(string typeKey)
        {
            for (int i = 0; i < currentProjects.Count; i++)
            {
                ResearchProjectDef current = currentProjects[i];
                if (current != null && !current.IsFinished && GetCategoryKey(current) == typeKey)
                    return false;
            }

            List<ResearchProjectDef> offers = PeekAvailableProjects();
            if (offers == null)
                return false;

            for (int i = 0; i < offers.Count; i++)
            {
                ResearchProjectDef project = offers[i];
                if (project != null && !project.IsFinished && GetCategoryKey(project) == typeKey)
                    return true;
            }
            return false;
        }

        private static bool SameProjectList(List<ResearchProjectDef> a, List<ResearchProjectDef> b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        // Set while the tracker writes vanilla's current project itself, so the selection gate lets it through.
        private static bool applyingTrackedProject;

        public static bool ApplyingTrackedProject => applyingTrackedProject;

        public static void SetVanillaProjectUngated(ResearchProjectDef proj)
        {
            applyingTrackedProject = true;
            try
            {
                Find.ResearchManager.SetCurrentProject(proj);
            }
            finally
            {
                applyingTrackedProject = false;
            }
        }

        // Whether the player may start this project now. Shared by this mod's start button and the gate on other research trees.
        public bool PlayerCanStartProject(ResearchProjectDef proj)
        {
            if (proj == null)
                return false;
            if (currentProjects.Contains(proj))
                return true;
            if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.allowSwitchingResearch)
                return true;
            // Gravship research runs alongside the standard project.
            if (GetCategoryKey(proj) == "Gravship")
                return true;

            return !CategoryOccupied(proj);
        }

        // A paused project still occupies its category.
        private bool CategoryOccupied(ResearchProjectDef proj)
        {
            string key = GetCategoryKey(proj);

            for (int i = 0; i < currentProjects.Count; i++)
            {
                ResearchProjectDef tracked = currentProjects[i];
                if (tracked != null && tracked != proj && !tracked.IsFinished && GetCategoryKey(tracked) == key)
                    return true;
            }

            ResearchProjectDef vanilla = proj.knowledgeCategory == null
                ? Find.ResearchManager.GetProject()
                : GetActiveProject(proj.knowledgeCategory);

            return vanilla != null && vanilla != proj && !vanilla.IsFinished && GetCategoryKey(vanilla) == key;
        }

        private Dictionary<string, bool> rerolled = new Dictionary<string, bool>();
        private Dictionary<string, List<ResearchProjectDef>> projectDefsCacheByType = new Dictionary<string, List<ResearchProjectDef>>();
        private Dictionary<string, List<ResearchProjectDef>> currentProjectDefsCacheByType = new Dictionary<string, List<ResearchProjectDef>>();
        private HashSet<string> completedTypes = new HashSet<string>();

        private int tickCounter = 0;
        private int tickShortOffset = 10;
        private int tickOffset = 360;
        private int previousDefCount = 0;
        private bool additionalProjectsRefresh = true;
        private bool pendingWorldGenOffers = false;

        private Dictionary<string, bool> lastPicked = new Dictionary<string, bool>();
        private Dictionary<string, string> loggedMessages = new Dictionary<string, string>();

        private List<string> all_typeKeys;

        public ResearchTracker(World world) : base(world)
        {
            previousDefCount = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count;
            RefreshTypeKeys();
        }

        // Whether the anomaly categories were included when the keys were last built.
        private bool typeKeysTrackAnomaly;

        private bool AnomalyTypeKeysNeedRefresh()
        {
            if (!ModsConfig.AnomalyActive || all_typeKeys == null)
                return false;
            return Compatibility.AnomalyResearchUnlocked() != typeKeysTrackAnomaly;
        }

        private void RefreshTypeKeys()
        {
            bool anomalyUnlocked = Compatibility.AnomalyResearchUnlocked();
            all_typeKeys = DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading
                .Where(cat => !Compatibility.IsAnomalyKnowledgeCategory(cat) || anomalyUnlocked)
                .Select(x => x.defName)
                .ToList();
            all_typeKeys.Add("Standard");

            bool hasGravship = false;
            bool hasDivinitech = false;
            List<ResearchProjectDef> allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ResearchProjectDef def = allDefs[i];
                if (!hasGravship && (def.tab?.defName == "VGE_Gravtech" || def.tab?.defName == "VGE_GravShip"))
                    hasGravship = true;
                if (!hasDivinitech && def.knowledgeCategory?.defName == "Information")
                    hasDivinitech = true;
                if (hasGravship && hasDivinitech)
                    break;
            }

            if (hasGravship)
                all_typeKeys.Add("Gravship");
            if (hasDivinitech)
                all_typeKeys.Add("Divinitech");

            all_typeKeys = all_typeKeys.Distinct().ToList();
            typeKeysTrackAnomaly = all_typeKeys.Contains("Basic") || all_typeKeys.Contains("Advanced");
        }

        // Vanilla's GetProject(category) throws when the save's Anomaly project list is missing or holds a broken entry,
        // which spammed errors every tick. Clean the list once per game and retry; if it still fails, treat the category as idle.
        private static ResearchManager anomalyProjectsRepairedFor;

        public static ResearchProjectDef GetActiveProject(KnowledgeCategoryDef category)
        {
            ResearchManager manager = Find.ResearchManager;
            if (category == null)
                return manager.GetProject();

            try
            {
                return manager.GetProject(category);
            }
            catch (NullReferenceException)
            {
                if (!ReferenceEquals(anomalyProjectsRepairedFor, manager))
                {
                    anomalyProjectsRepairedFor = manager;
                    RepairAnomalyProjects(manager);
                    try
                    {
                        return manager.GetProject(category);
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
                return null;
            }
        }

        private static void RepairAnomalyProjects(ResearchManager manager)
        {
            List<ResearchManager.KnowledgeCategoryProject> list = manager.CurrentAnomalyKnowledgeProjects;
            if (list == null)
            {
                AccessTools.Field(typeof(ResearchManager), "currentAnomalyKnowledgeProjects")
                    ?.SetValue(manager, new List<ResearchManager.KnowledgeCategoryProject>());
                Log.Warning("[Semi Random Research] Rebuilt the save's missing Anomaly research list.");
                return;
            }

            int removed = list.RemoveAll(entry => entry == null || entry.category == null);
            Log.Warning("[Semi Random Research] Removed " + removed + " broken entries from the save's Anomaly research list.");
        }

        // Pseudo-category of a project. A def's tab and knowledge category are fixed once loaded, so the answer is cached.
        private static readonly Dictionary<ResearchProjectDef, string> categoryKeyCache =
            new Dictionary<ResearchProjectDef, string>();

        public static string GetCategoryKey(ResearchProjectDef def)
        {
            if (def == null) return "Standard";
            if (categoryKeyCache.TryGetValue(def, out string cached))
                return cached;

            string key = ComputeCategoryKey(def);
            categoryKeyCache[def] = key;
            return key;
        }

        private static string ComputeCategoryKey(ResearchProjectDef def)
        {
            if (def.tab?.defName == "VGE_Gravtech" || def.tab?.defName == "VGE_GravShip") return "Gravship";
            if (def.knowledgeCategory != null) return GetCategoryKey(def.knowledgeCategory);
            return "Standard";
        }

        public static string GetCategoryKey(KnowledgeCategoryDef category)
        {
            if (category == null) return "Standard";
            return category.defName == "Information" ? "Divinitech" : category.defName;
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);

            ResearchTabWindowSwitcher.Apply();

            // Rolling needs Faction.OfPlayer, which does not exist yet during world generation.
            if (Faction.OfPlayerSilentFail != null)
                SettingsChanged();
            else
                pendingWorldGenOffers = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref currentAvailableProjects, "currentAvailableProjects", LookMode.Def);
            Scribe_Collections.Look(ref notChosenProjects, "notChosenProjects", LookMode.Def, LookMode.Value);
            Scribe_Collections.Look(ref additionalAvailableProjects, "additionalAvailableProjectsByGain", LookMode.Def);
            Scribe_Collections.Look(ref currentProjects, "currentProject", LookMode.Def);

            if (notChosenProjects == null) notChosenProjects = new Dictionary<ResearchProjectDef, int>();
            if (currentProjects == null) currentProjects = new List<ResearchProjectDef>();
            if (currentAvailableProjects == null) currentAvailableProjects = new List<ResearchProjectDef>();
            if (additionalAvailableProjects == null) additionalAvailableProjects = new HashSet<ResearchProjectDef>();

            // A project from a removed mod loads as null.
            currentProjects.RemoveAll(def => def == null);
            currentAvailableProjects.RemoveAll(def => def == null);
            additionalAvailableProjects.RemoveWhere(def => def == null);

            if (Scribe.mode == LoadSaveMode.LoadingVars && SemiRandomResearchMod.settings.verboseLogging)
            {
                LogIfNewMessage("Loaded Current Projects", string.Join(", ", currentProjects.Select(def => def.LabelCap.RawText)));
                LogIfNewMessage("Loaded Available Projects", string.Join(", ", currentAvailableProjects.Select(def => def.LabelCap.RawText)));
            }

            Scribe_Collections.Look(ref rerolled, "rerolled");
            if (rerolled == null) rerolled = new Dictionary<string, bool>();

            Scribe_Collections.Look(ref currentRerollState, "currentRerollState");
            if (currentRerollState == null) currentRerollState = new Dictionary<string, int>();

            Scribe_Collections.Look(ref lastPicked, "lastPicked");
            if (lastPicked == null) lastPicked = new Dictionary<string, bool>();

            Scribe_Collections.Look(ref snoozeHeldSlots, "snoozeHeldSlots", LookMode.Value, LookMode.Value);
            if (snoozeHeldSlots == null) snoozeHeldSlots = new Dictionary<string, int>();

            Scribe_Collections.Look(ref completedHistory, "completedHistory", LookMode.Deep);
            if (completedHistory == null)
                completedHistory = new List<ResearchHistoryEntry>();
            else
                completedHistory.RemoveAll(entry => entry == null || entry.project == null);

            Scribe_Values.Look(ref researchPaused, "researchPaused", false);
            Scribe_Values.Look(ref pausedTypeKey, "pausedTypeKey");
        }

        // Vanilla's active project per pseudo-category, rebuilt only when something in the pass changes the current project.
        private readonly Dictionary<string, ResearchProjectDef> activeByTypeKey = new Dictionary<string, ResearchProjectDef>();
        private bool activeByTypeKeyStale = true;

        // Standard first, then categories in list order, so the last match wins.
        private void RefreshActiveByTypeKey()
        {
            activeByTypeKeyStale = false;
            activeByTypeKey.Clear();

            ResearchProjectDef standardActive = GetActiveProject(null);
            if (standardActive != null)
                activeByTypeKey[GetCategoryKey(standardActive)] = standardActive;

            List<KnowledgeCategoryDef> categories = DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading;
            for (int i = 0; i < categories.Count; i++)
            {
                ResearchProjectDef catActive = GetActiveProject(categories[i]);
                if (catActive != null)
                    activeByTypeKey[GetCategoryKey(catActive)] = catActive;
            }
        }

        private static List<ResearchProjectDef> ProjectsOfType(List<ResearchProjectDef> source, string typeKey)
        {
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                ResearchProjectDef project = source[i];
                if (project != null && GetCategoryKey(project) == typeKey)
                    result.Add(project);
            }
            return result;
        }

        private static int CountOfType(List<ResearchProjectDef> source, string typeKey)
        {
            if (source == null)
                return 0;

            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && GetCategoryKey(source[i]) == typeKey)
                    count++;
            }
            return count;
        }

        // Cheapest startable offer of the category, falling back to a fresh roll when none is offered. Ties keep list order.
        private ResearchProjectDef CheapestStartableOffer(string typeKey, ref List<ResearchProjectDef> availableSnapshot)
        {
            ResearchProjectDef best = CheapestStartable(currentAvailableProjects, typeKey);
            if (best != null)
                return best;

            if (availableSnapshot == null)
                availableSnapshot = GetCurrentlyAvailableProjects();
            return CheapestStartable(availableSnapshot, typeKey);
        }

        private static ResearchProjectDef CheapestStartable(List<ResearchProjectDef> projects, string typeKey)
        {
            ResearchProjectDef best = null;
            float bestCost = 0f;
            for (int i = 0; i < projects.Count; i++)
            {
                ResearchProjectDef project = projects[i];
                if (project == null || project.IsFinished || GetCategoryKey(project) != typeKey || !project.CanStartNow)
                    continue;

                float cost = project.CostApparent;
                if (best == null || cost < bestCost)
                {
                    best = project;
                    bestCost = cost;
                }
            }
            return best;
        }

        private void AutoStart(ResearchProjectDef project, string typeKey)
        {
            bool alreadyActive = Find.ResearchManager.IsCurrentProject(project);
            SetCurrentProjectByKey(project, typeKey);

            if (!alreadyActive)
                Messages.Message("CM_Semi_Random_Research_AutoStarted".Translate(project.LabelCap), MessageTypeDefOf.NeutralEvent, false);
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (pendingWorldGenOffers && Faction.OfPlayerSilentFail != null)
            {
                pendingWorldGenOffers = false;
                SettingsChanged();
            }

            if ((tickCounter % tickShortOffset) == 0)
            {
                if (all_typeKeys == null)
                    RefreshTypeKeys();
                else if (AnomalyTypeKeysNeedRefresh())
                {
                    RefreshTypeKeys();
                    GetCurrentlyAvailableProjects();
                }

                List<ResearchProjectDef> availableSnapshot = null;

                bool slowPass = (tickCounter % tickOffset) == 0;
                if (slowPass)
                    activeByTypeKeyStale = true;

                for (int k = 0; k < all_typeKeys.Count; k++)
                {
                    string typeKey = all_typeKeys[k];
                    if (!currentProjectDefsCacheByType.TryGetValue(typeKey, out List<ResearchProjectDef> currentProjectOfType))
                    {
                        currentProjectOfType = ProjectsOfType(currentProjects, typeKey);
                        currentProjectDefsCacheByType[typeKey] = currentProjectOfType;
                    }

                    ResearchProjectDef finishedProject = null;
                    for (int i = 0; i < currentProjectOfType.Count; i++)
                    {
                        if (currentProjectOfType[i].IsFinished)
                        {
                            finishedProject = currentProjectOfType[i];
                            break;
                        }
                    }
                    bool finished = finishedProject != null;
                    if (finished)
                        ConsiderProjectFinished(finishedProject);

                    // Auto research waits until the tab has been opened once and is closed again (see AutoPickNow).
                    if ((currentProjectOfType.Count == 0 || finished) && !IsPaused(typeKey) &&
                        SemiRandomResearchMod.settings.autoPickNextResearch && AutoPickArmed &&
                        !SemiRandomResearchWindowOpen &&
                        (finished || slowPass))
                    {
                        ResearchProjectDef cheapestProject = CheapestStartableOffer(typeKey, ref availableSnapshot);
                        if (cheapestProject != null)
                        {
                            AutoStart(cheapestProject, typeKey);
                            currentProjectOfType = ProjectsOfType(currentProjects, typeKey);
                        }
                    }

                    if (slowPass)
                    {
                        if (activeByTypeKeyStale)
                            RefreshActiveByTypeKey();
                        activeByTypeKey.TryGetValue(typeKey, out ResearchProjectDef activeProject);
                        ResearchProjectDef trackedFirst = currentProjectOfType.Count > 0 ? currentProjectOfType[0] : null;

                        // VGE hides Gravship projects from GetProject() but reports them through IsCurrentProject().
                        if (activeProject == null && trackedFirst != null &&
                            Find.ResearchManager.IsCurrentProject(trackedFirst))
                        {
                            activeProject = trackedFirst;
                        }

                        // Keeps vanilla's current project and the tracked one in step.
                        if (!IsPaused(typeKey))
                        {
                            if (activeProject == null && trackedFirst != null && trackedFirst.CanStartNow)
                            {
                                SetCurrentProjectByKey(trackedFirst, typeKey);
                            }
                            else if (activeProject != null && !currentProjectOfType.Contains(activeProject) && activeProject.CanStartNow)
                            {
                                if (!SemiRandomResearchMod.settings.featureEnabled)
                                {
                                    SetCurrentProjectByKey(activeProject, typeKey);
                                }
                                else if (trackedFirst == null && currentAvailableProjects.Contains(activeProject))
                                {
                                    SetCurrentProjectByKey(activeProject, typeKey);
                                }
                                else if (trackedFirst != null)
                                {
                                    SetCurrentProjectByKey(trackedFirst, typeKey);
                                }
                                else
                                {
                                    LogIfNewMessage("WorldTickUnexpectedState" + typeKey, $"Error? Set as activeProject: {activeProject.LabelCap} currentAvailableProjects: {currentAvailableProjects.Count} and of type {typeKey}: {CountOfType(currentAvailableProjects, typeKey)}");
                                    SetCurrentProjectByKey(activeProject, typeKey);
                                }
                            }
                        }
                    }
                }
                if (SemiRandomResearchMod.settings.progressAddsChoice != ProgressAddsChoice.AddChoiceOnlyOnGain && additionalAvailableProjects.Count > 0)
                {
                    additionalAvailableProjects.Clear();
                }
            }
            tickCounter = (tickCounter + 1) % tickOffset;
        }

        private static bool SemiRandomResearchWindowOpen
        {
            get
            {
                WindowStack stack = Find.WindowStack;
                return stack != null && stack.IsOpen(typeof(MainTabWindow_NextResearch));
            }
        }

        // Auto research stays dormant until the research tab has been opened. Not saved: every load starts dormant.
        private bool researchTabOpened;

        public void NotifyResearchTabOpened()
        {
            researchTabOpened = true;
        }

        // When another tree owns the Research tab this mod's window never opens, so auto research is armed from the start.
        private bool AutoPickArmed => researchTabOpened || !ResearchTabWindowSwitcher.SemiRandomOwnsResearchTab;

        // Auto research's pick, made when the research tab closes so the player always gets to choose first.
        public void AutoPickNow()
        {
            if (SemiRandomResearchMod.settings == null || !SemiRandomResearchMod.settings.autoPickNextResearch)
                return;
            if (!AutoPickArmed || Current.Game == null || Faction.OfPlayerSilentFail == null)
                return;

            if (all_typeKeys == null || AnomalyTypeKeysNeedRefresh())
                RefreshTypeKeys();

            List<ResearchProjectDef> availableSnapshot = null;

            for (int k = 0; k < all_typeKeys.Count; k++)
            {
                string typeKey = all_typeKeys[k];
                if (IsPaused(typeKey))
                    continue;
                if (currentProjects.Any(x => !x.IsFinished && GetCategoryKey(x) == typeKey))
                    continue;

                ResearchProjectDef cheapestProject = CheapestStartableOffer(typeKey, ref availableSnapshot);
                if (cheapestProject != null)
                    AutoStart(cheapestProject, typeKey);
            }
        }

        private void RecordCompletedProject(ResearchProjectDef def)
        {
            if (def == null)
                return;

            completedHistory.RemoveAll(entry => entry == null || entry.project == def);
            completedHistory.Insert(0, new ResearchHistoryEntry(def, Find.TickManager?.TicksGame ?? 0));

            if (completedHistory.Count > MaxCompletedHistoryEntries)
                completedHistory.RemoveRange(MaxCompletedHistoryEntries, completedHistory.Count - MaxCompletedHistoryEntries);
        }

        private int HeldSlots(string typeKey)
        {
            return snoozeHeldSlots.TryGetValue(typeKey, out int held) ? held : 0;
        }

        private int TargetOfferCount(string typeKey, int additionalProjects)
        {
            SemiRandomResearchSettings settings = SemiRandomResearchMod.settings;
            return Math.Max(0, Math.Min(settings.availableProjectCount + additionalProjects, settings.maxProjectCount) - HeldSlots(typeKey));
        }

        // Takes a just-snoozed project out of the offers without backfilling its slot.
        public void RemoveSnoozedOffer(ResearchProjectDef def)
        {
            if (def == null || !currentAvailableProjects.Remove(def))
                return;

            string typeKey = GetCategoryKey(def);
            if (!additionalAvailableProjects.Remove(def))
                snoozeHeldSlots[typeKey] = HeldSlots(typeKey) + 1;

            PublishOffers(new List<ResearchProjectDef>(currentAvailableProjects));
        }

        public List<ResearchProjectDef> GetCurrentlyAvailableProjects()
        {
            // Rolling uses CostApparent, which needs the player faction.
            if (Faction.OfPlayerSilentFail == null)
                return currentAvailableProjects;

            if (all_typeKeys == null || AnomalyTypeKeysNeedRefresh())
                RefreshTypeKeys();
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            SemiRandomResearchMod.settings.DumpSettingToLog();

            currentAvailableProjects = currentAvailableProjects.Where(projectDef => projectDef != null &&
                !projectDef.IsFinished &&
                !Compatibility.IsHiddenResearch(projectDef) &&
                Find.Storyteller.difficulty.AllowedBy(projectDef.hideWhen) &&
                !ResearchSnoozeTracker.IsExcludedNow(projectDef)).ToList();

            for (int i = currentProjects.Count - 1; i >= 0; i--)
            {
                ResearchProjectDef current = currentProjects[i];
                if (Compatibility.IsHiddenResearch(current))
                    SetCurrentProjectByKey(null, GetCategoryKey(current));
            }

            int additionalProjects = SemiRandomResearchMod.settings.amountSelection == ChoiceAmountSelection.PerColonist ?
                PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned_FreeColonists_NoSuspended.
                Where(collonist => !collonist.GetDisabledWorkTypes().Any(workType => workType.defName == "Research")).Count()
                / SemiRandomResearchMod.settings.additionalProjectPerXColonists
                : 0;

            foreach (string typeKey in all_typeKeys)
            {
                List<ResearchProjectDef> currentAvailableValidProjectsOfType = currentAvailableProjects.Where(x => GetCategoryKey(x) == typeKey && x.CanStartNow).ToList();
                List<ResearchProjectDef> currentProjectOfType = currentProjects.Where(x => GetCategoryKey(x) == typeKey).ToList();

                // A category whose snoozes emptied it gets its slots back, so it is never left with nothing to research.
                if (currentAvailableValidProjectsOfType.Count == 0)
                    snoozeHeldSlots.Remove(typeKey);

                if (!SemiRandomResearchMod.settings.rerollAllEveryTime ||
                    SemiRandomResearchMod.settings.allowSwitchingResearch ||
                    currentProjectOfType.Empty() ||
                    currentProjectOfType.Any(x => x.IsFinished))
                {
                    bool handledProjects = false;
                    int numberOfMissingProjects = TargetOfferCount(typeKey, additionalProjects) - currentAvailableValidProjectsOfType.Count;

                    if (numberOfMissingProjects > 0 || additionalProjectsRefresh)
                    {
                        List<ResearchProjectDef> nextProjects = GetResearchableProjects(numberOfMissingProjects, typeKey);

                        if (!nextProjects.NullOrEmpty())
                        {
                            currentAvailableProjects.AddRange(nextProjects);
                            currentAvailableProjects = currentAvailableProjects.Distinct().ToList();
                            currentAvailableValidProjectsOfType.AddRange(nextProjects);
                            currentAvailableValidProjectsOfType = currentAvailableValidProjectsOfType.Distinct().ToList();
                            handledProjects = true;
                            result.AddRange(currentAvailableValidProjectsOfType);
                        }
                        numberOfMissingProjects = TargetOfferCount(typeKey, additionalProjects) - currentAvailableValidProjectsOfType.Count;
                    }
                    int projectsAddedAdditional = currentAvailableValidProjectsOfType.Count(x => additionalAvailableProjects.Contains(x));
                    int progressAddedProgressed = currentAvailableValidProjectsOfType.Count(x => x.ProgressReal > 0 && !currentProjectOfType.Contains(x) && !additionalAvailableProjects.Contains(x));
                    int extraAddedProgress = SemiRandomResearchMod.settings.progressAddsChoice == ProgressAddsChoice.AddChoice ? progressAddedProgressed : 0;
                    if (numberOfMissingProjects < -extraAddedProgress - projectsAddedAdditional)
                    {
                        int amountToRemove = -1 * numberOfMissingProjects - (extraAddedProgress + projectsAddedAdditional);
                        int amountTarget = currentAvailableValidProjectsOfType.Count - amountToRemove;
                        result.RemoveAll(x => currentAvailableValidProjectsOfType.Contains(x));
                        List<ResearchProjectDef> currentAvailableProjectsWithoutCurrentProject = new List<ResearchProjectDef>();
                        if (SemiRandomResearchMod.settings.progressAddsChoice == ProgressAddsChoice.ReplaceChoice)
                        {
                            IEnumerable<ResearchProjectDef> partiallyCompleted = currentAvailableValidProjectsOfType.Where(x => x.ProgressReal > 0 && !additionalAvailableProjects.Contains(x));
                            if (partiallyCompleted.Count() > amountTarget)
                            {
                                partiallyCompleted = partiallyCompleted.Skip(partiallyCompleted.Count() - amountTarget);
                            }
                            currentAvailableProjectsWithoutCurrentProject.AddRange(partiallyCompleted);
                        }
                        currentAvailableProjectsWithoutCurrentProject.AddRange(currentAvailableValidProjectsOfType.Where(x => additionalAvailableProjects.Contains(x)));
                        IEnumerable<ResearchProjectDef> keepable = currentAvailableValidProjectsOfType.Where(x => !currentProjects.Contains(x) && !currentAvailableProjectsWithoutCurrentProject.Contains(x));
                        currentAvailableProjectsWithoutCurrentProject.AddRange(keepable.Reverse().Skip(amountToRemove).Reverse());

                        if (currentProjectOfType.Any(x => !x.IsFinished))
                        {
                            currentAvailableProjectsWithoutCurrentProject.AddRange(currentProjectOfType);
                        }
                        handledProjects = true;
                        result.AddRange(currentAvailableProjectsWithoutCurrentProject);
                        if (SemiRandomResearchMod.settings.verboseLogging)
                            LogIfNewMessage("numberOfMissingProjects < 0" + typeKey, $"More projects available than expected. numberOfMissingProjects: {numberOfMissingProjects} Values: additionalProjects {additionalProjects} amountToRemove: {amountToRemove} keepable.Count: {keepable.Count()} extraAddedProgress: {extraAddedProgress} projectsAddedAdditional:{projectsAddedAdditional}");
                    }
                    if (!handledProjects)
                    {
                        if (SemiRandomResearchMod.settings.verboseLogging && currentAvailableValidProjectsOfType.Count == 0)
                            LogIfNewMessage("numberOfMissingProjects = 0" + typeKey, $"No projects are to be added even though non are available?Values: additionalProjects {additionalProjects} extraAddedProgress: {extraAddedProgress} projectsAddedAdditional:{projectsAddedAdditional}");

                        result.AddRange(currentAvailableValidProjectsOfType);
                    }
                    additionalProjectsRefresh = false;
                }
                else
                {
                    result.AddRange(currentProjectOfType);
                }
            }
            // The saved offer list matches what is published.
            currentAvailableProjects = result.Distinct().ToList();
            PublishOffers(result);
            return result;
        }

        private List<ResearchProjectDef> GetResearchableProjects(int count, string typeKey)
        {
            int defCount = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count;
            if (defCount != previousDefCount)
            {
                projectDefsCacheByType.Clear();
                completedTypes.Clear();
                previousDefCount = defCount;
            }

            if (completedTypes.Contains(typeKey))
            {
                if (SemiRandomResearchMod.settings.verboseLogging)
                {
                    LogIfNewMessage("Skipping" + typeKey, "Type Completed");
                }

                return new List<ResearchProjectDef>();
            }

            // Tech levels of this type's current offers only: other types' offers must not cap this type's tier.
            TechLevel maxCurrentProjectTechlevel = TechLevel.Archotech;
            TechLevel minCurrentProjectTechlevel = TechLevel.Archotech;
            bool anyOfType = false;
            for (int i = 0; i < currentAvailableProjects.Count; i++)
            {
                if (GetCategoryKey(currentAvailableProjects[i]) != typeKey)
                    continue;
                TechLevel techLevel = currentAvailableProjects[i].techLevel;
                if (!anyOfType || techLevel > maxCurrentProjectTechlevel) maxCurrentProjectTechlevel = techLevel;
                if (!anyOfType || techLevel < minCurrentProjectTechlevel) minCurrentProjectTechlevel = techLevel;
                anyOfType = true;
            }

            // Hidden projects stay in the cache and are skipped below: the Entity Codex and the SoS2 uplink reveal them mid-game.
            if (!projectDefsCacheByType.TryGetValue(typeKey, out List<ResearchProjectDef> projectsOfType))
            {
                List<ResearchProjectDef> allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
                projectsOfType = new List<ResearchProjectDef>();
                for (int i = 0; i < allDefs.Count; i++)
                {
                    ResearchProjectDef projectDef = allDefs[i];
                    if (!projectDef.IsFinished && GetCategoryKey(projectDef) == typeKey)
                        projectsOfType.Add(projectDef);
                }
                projectDefsCacheByType[typeKey] = projectsOfType;

                if (projectsOfType.Count == 0)
                {
                    completedTypes.Add(typeKey);
                }
            }

            // CanStartNow walks the colony's research benches, so the cheap rejections come first.
            HashSet<ResearchProjectDef> currentlyOffered = new HashSet<ResearchProjectDef>(currentAvailableProjects);
            List<ResearchProjectDef> availableList = new List<ResearchProjectDef>();
            for (int i = 0; i < projectsOfType.Count; i++)
            {
                ResearchProjectDef projectDef = projectsOfType[i];
                if (currentlyOffered.Contains(projectDef))
                    continue;
                if (projectDef.IsDummyResearch() || Compatibility.IsHiddenResearch(projectDef) || ResearchSnoozeTracker.IsExcludedNow(projectDef))
                    continue;
                // Projects the difficulty's trap, turret and mortar settings hide from the research tree.
                if (!Find.Storyteller.difficulty.AllowedBy(projectDef.hideWhen))
                    continue;
                if (!projectDef.CanStartNow)
                    continue;

                availableList.Add(projectDef);
            }

            IEnumerable<ResearchProjectDef> allAvailableProjects = availableList;

            if (SemiRandomResearchMod.settings.verboseLogging)
            {
                if (!allAvailableProjects.Any() && currentAvailableProjects.Count == 0)
                {
                    List<ResearchProjectDef> allAvailableProjectsDebug = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

                    LogIfNewMessage("NoAvailableProjects1" + typeKey, $"[CM_Semi_Random_Research] Total projects in game: {allAvailableProjectsDebug.Count}");
                    allAvailableProjectsDebug = allAvailableProjectsDebug.Where((ResearchProjectDef projectDef) => projectDef.CanStartNow).ToList();
                    LogIfNewMessage("NoAvailableProjects2" + typeKey, $"[CM_Semi_Random_Research] Of which {allAvailableProjectsDebug.Count} Could be started now");
                    allAvailableProjectsDebug = allAvailableProjectsDebug.Where((ResearchProjectDef projectDef) => !projectDef.IsDummyResearch()).ToList();
                    LogIfNewMessage("NoAvailableProjects4" + typeKey, $"[CM_Semi_Random_Research] Of which {allAvailableProjectsDebug.Count} are not Dummy researches");
                }
            }

            ResearchProjectDef randomProject = null;
            if (allAvailableProjects.Any() && SemiRandomResearchMod.settings.AllowOneHigherTechProjectActive &&
                (!SemiRandomResearchMod.settings.RestrictToFactionTechLevelActive || maxCurrentProjectTechlevel <= Faction.OfPlayer.def.techLevel) &&
                (!SemiRandomResearchMod.settings.forceLowestTechLevel || maxCurrentProjectTechlevel == minCurrentProjectTechlevel))
            {
                randomProject = allAvailableProjects.RandomElement();
            }

            if (SemiRandomResearchMod.settings.RestrictToFactionTechLevelActive)
            {
                TechLevel maxTechLevel = Faction.OfPlayer.def.techLevel;
                allAvailableProjects = allAvailableProjects.Where(projectDef => projectDef.techLevel <= maxTechLevel).ToList();

                if (SemiRandomResearchMod.settings.verboseLogging)
                {
                    LogIfNewMessage("AfterRestrictToFactionTechLevel" + typeKey, "Currently possible projects after restrictToFactionTechLevel: " + allAvailableProjects.Count());
                }
            }

            if (allAvailableProjects.Any() && SemiRandomResearchMod.settings.forceLowestTechLevel)
            {
                for (TechLevel techLevel = TechLevel.Animal; techLevel <= TechLevel.Archotech; ++techLevel)
                {
                    IEnumerable<ResearchProjectDef> projectsAtTechLevel = allAvailableProjects.Where(projectDef => projectDef.techLevel <= techLevel);
                    if (projectsAtTechLevel.Any() || minCurrentProjectTechlevel == techLevel)
                    {
                        allAvailableProjects = projectsAtTechLevel;
                        break;
                    }
                }

                if (SemiRandomResearchMod.settings.verboseLogging)
                {
                    LogIfNewMessage("AfterForceLowestTechLevel" + typeKey, "Currently possible projects after forceLowestTechLevel: " + allAvailableProjects.Count());
                }
            }
            List<ResearchProjectDef> selectedProjects = new List<ResearchProjectDef>();
            selectedProjects.AddRange(allAvailableProjects.Where(x => additionalAvailableProjects.Contains(x)));

            // Materialized once: ProgressReal is read for every candidate, and the set is checked repeatedly below.
            List<ResearchProjectDef> partiallyCompleted = new List<ResearchProjectDef>();
            HashSet<ResearchProjectDef> partiallyCompletedSet = new HashSet<ResearchProjectDef>();
            foreach (ResearchProjectDef candidate in allAvailableProjects)
            {
                if (candidate.ProgressReal > 0 && !additionalAvailableProjects.Contains(candidate) &&
                    partiallyCompletedSet.Add(candidate))
                {
                    partiallyCompleted.Add(candidate);
                }
            }

            if (SemiRandomResearchMod.settings.progressAddsChoice == ProgressAddsChoice.AddChoice)
            {
                selectedProjects.AddRange(partiallyCompleted);
            }
            else if (SemiRandomResearchMod.settings.progressAddsChoice == ProgressAddsChoice.ReplaceChoice)
            {
                selectedProjects.AddRange(partiallyCompleted);
                count -= partiallyCompleted.Count;
            }
            else if (SemiRandomResearchMod.settings.progressAddsChoice == ProgressAddsChoice.Never)
            {
                allAvailableProjects = allAvailableProjects.Where(x => !partiallyCompletedSet.Contains(x)).ToList();
            }

            allAvailableProjects = allAvailableProjects.Where(x => !selectedProjects.Contains(x));

            allAvailableProjects = allAvailableProjects.InRandomOrder();

            if (SemiRandomResearchMod.settings.reofferAfterAmountOfRerolls > 0)
            {
                List<ResearchProjectDef> possibleNotShownRecently = allAvailableProjects.Where(x => !notChosenProjects.ContainsKey(x)).ToList();

                if (SemiRandomResearchMod.settings.verboseLogging)
                {
                    LogIfNewMessage("ReofferAfterAmountOfRerollsCount" + typeKey, "This many researches were not offered recently: " + possibleNotShownRecently.Count + " while this many were shown recently: " + notChosenProjects.Keys.Count(x => GetCategoryKey(x) == typeKey && !x.IsFinished));
                }

                if (possibleNotShownRecently.Count < count)
                {
                    if (SemiRandomResearchMod.settings.verboseLogging)
                    {
                        LogIfNewMessage("PossibleNotShownRecently" + typeKey, "Picking from recently shown researches this many projects: " + (count - possibleNotShownRecently.Count));
                    }
                    possibleNotShownRecently.AddRange(allAvailableProjects.Where(x => notChosenProjects.ContainsKey(x)).Take(count - possibleNotShownRecently.Count));
                }

                allAvailableProjects = possibleNotShownRecently;
            }

            if (SemiRandomResearchMod.settings.equalizeCost && allAvailableProjects.Count() > count && count > 0)
            {
                int amountToRandomlyGenerate = count / 2;
                int amountToPick = count - amountToRandomlyGenerate;

                if (count == 1)
                {
                    if (!lastPicked.ContainsKey(typeKey))
                    {
                        lastPicked[typeKey] = false;
                    }
                    if (lastPicked[typeKey])
                    {
                        amountToPick = 0;
                        amountToRandomlyGenerate = 1;
                    }
                    lastPicked[typeKey] = !lastPicked[typeKey];
                }

                List<ResearchProjectDef> selectedProjectsFirstHalf = allAvailableProjects.Take(amountToRandomlyGenerate).ToList();

                if (SemiRandomResearchMod.settings.AllowOneHigherTechProjectActive && randomProject != null && !selectedProjectsFirstHalf.Contains(randomProject) && amountToRandomlyGenerate > 0)
                {
                    selectedProjectsFirstHalf[0] = randomProject;
                }

                selectedProjects.AddRange(selectedProjectsFirstHalf);

                if (amountToPick > 0)
                {
                    float averageAvailableCost = allAvailableProjects.Select(x => x.CostApparent).Sum() / allAvailableProjects.Count();
                    float averageCurrentCost = (currentAvailableProjects.Select(x => x.CostApparent).Sum() + selectedProjectsFirstHalf.Select(x => x.CostApparent).Sum() + selectedProjects.Sum(x => x.CostApparent))
                        / Math.Max(currentAvailableProjects.Count + selectedProjects.Count + selectedProjectsFirstHalf.Count, 1);
                    float targetAddedAverageCost = ((averageAvailableCost * (currentAvailableProjects.Count + count))
                        - (currentAvailableProjects.Count + selectedProjectsFirstHalf.Count) * averageCurrentCost) / (amountToPick);
                    // Materialized once and shuffled in place for each of the 25 tries.
                    List<ResearchProjectDef> equalizePool = allAvailableProjects.Where(x => !selectedProjectsFirstHalf.Contains(x)).ToList();
                    allAvailableProjects = equalizePool;

                    if (SemiRandomResearchMod.settings.verboseLogging)
                    {
                        LogIfNewMessage("equalizeCostPick1" + typeKey, $"Picking projects to equalize: Average research cost of all still available projects: {averageAvailableCost} \nAverage cost of the randomly selected projects: {averageCurrentCost}  \nTarget that the other projects added should have on average: {targetAddedAverageCost} \nThere were {amountToRandomlyGenerate} projects selected randomly. \nBefore adding projects there were {currentAvailableProjects.Count} already in the list. \nThere will be picked {amountToPick} projects.");
                    }

                    List<ResearchProjectDef> bestSelectedProjects = new List<ResearchProjectDef>();
                    float bestAverage = float.MaxValue;
                    float bestTotal = 0f;
                    int takeCount = Math.Min(amountToPick, equalizePool.Count);
                    for (int i = 0; i < 25; i++)
                    {
                        equalizePool.Shuffle();

                        float iterTotal = 0f;
                        for (int j = 0; j < takeCount; j++)
                            iterTotal += equalizePool[j].CostApparent;

                        float actualAverage = iterTotal / takeCount;
                        if (Math.Abs(bestAverage - targetAddedAverageCost) > Math.Abs(actualAverage - targetAddedAverageCost))
                        {
                            bestAverage = actualAverage;
                            bestTotal = iterTotal;
                            bestSelectedProjects.Clear();
                            for (int j = 0; j < takeCount; j++)
                                bestSelectedProjects.Add(equalizePool[j]);
                        }
                    }
                    selectedProjects.AddRange(bestSelectedProjects);

                    if (SemiRandomResearchMod.settings.verboseLogging)
                    {
                        LogIfNewMessage("equalizeCostPick2" + typeKey, $"Total cost of picked projects: {bestTotal} ");
                    }
                }
                else if (SemiRandomResearchMod.settings.verboseLogging)
                {
                    LogIfNewMessage("equalizeCostNoPick" + typeKey, $"[There were {amountToRandomlyGenerate} projects selected randomly as part of cost equalization");
                }
            }
            else
            {
                selectedProjects.AddRange(allAvailableProjects.Take(Math.Min(count, allAvailableProjects.Count())));

                if (SemiRandomResearchMod.settings.verboseLogging)
                {
                    LogIfNewMessage("selectCount" + typeKey, $"There were {selectedProjects.Count} projects selected randomly");
                }

                if (SemiRandomResearchMod.settings.AllowOneHigherTechProjectActive && randomProject != null && !selectedProjects.Contains(randomProject))
                {
                    if (selectedProjects.Count < count || selectedProjects.Count < 1)
                    {
                        selectedProjects.Add(randomProject);
                    }
                    else
                    {
                        selectedProjects[0] = randomProject;
                    }
                }
            }
            selectedProjects.Shuffle();
            int selectedProjectsCount = selectedProjects.Count;
            selectedProjects = selectedProjects.OrderByDescending(x => partiallyCompletedSet.Contains(x)).Distinct().ToList();
            if (selectedProjects.Count != selectedProjectsCount)
                LogIfNewMessage("Distinct error" + typeKey, $"There were {selectedProjects.Count} projects after distinct but {selectedProjectsCount} before.");
            return selectedProjects;
        }

        public void SetCurrentProjectByKey(ResearchProjectDef newCurrentProject, string typeKey)
        {
            loggedMessages.Clear();
            activeByTypeKeyStale = true;
            currentProjects = currentProjects.Where(x => GetCategoryKey(x) != typeKey).ToList();
            projectDefsCacheByType.Remove(typeKey);
            if (IsPaused(typeKey))
            {
                researchPaused = false;
                pausedTypeKey = null;
            }
            if (newCurrentProject != null)
            {
                currentProjects.Add(newCurrentProject);
                SetVanillaProjectUngated(newCurrentProject);

                if (!SemiRandomResearchMod.settings.featureEnabled && !currentAvailableProjects.Contains(newCurrentProject))
                    currentAvailableProjects.Add(newCurrentProject);

                if (SemiRandomResearchMod.settings.rerollAllEveryTime && !SemiRandomResearchMod.settings.allowSwitchingResearch)
                    currentAvailableProjects = currentAvailableProjects.Where(projectDef => GetCategoryKey(projectDef) != typeKey || projectDef == newCurrentProject).ToList();
            }
            else
            {
                StopVanillaProjectForKey(typeKey);
            }
            currentProjectDefsCacheByType[typeKey] = ProjectsOfType(currentProjects, typeKey);
            PublishOffers(new List<ResearchProjectDef>(currentAvailableProjects));
        }

        private void StopVanillaProjectForKey(string typeKey)
        {
            activeByTypeKeyStale = true;
            ResearchProjectDef active = null;
            ResearchProjectDef standardActive = GetActiveProject(null);
            if (standardActive != null && GetCategoryKey(standardActive) == typeKey) active = standardActive;
            foreach (var cat in DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading)
            {
                ResearchProjectDef catActive = GetActiveProject(cat);
                if (catActive != null && GetCategoryKey(catActive) == typeKey) active = catActive;
            }

            // Gravship projects are not returned by GetProject, so fall back to the tracked one.
            ResearchProjectDef trackedType = currentProjects.FirstOrDefault(x => GetCategoryKey(x) == typeKey);
            if (trackedType == null && currentProjectDefsCacheByType.ContainsKey(typeKey))
                trackedType = currentProjectDefsCacheByType[typeKey].FirstOrDefault();
            if (active == null && trackedType != null && Find.ResearchManager.IsCurrentProject(trackedType))
                active = trackedType;

            if (active != null)
                Find.ResearchManager.StopProject(active);
        }

        public void PauseResearch(ResearchProjectDef project)
        {
            if (project == null)
                return;

            string typeKey = GetCategoryKey(project);
            if (!currentProjects.Contains(project))
                currentProjects.Add(project);

            researchPaused = true;
            pausedTypeKey = typeKey;
            StopVanillaProjectForKey(typeKey);
            currentProjectDefsCacheByType[typeKey] = ProjectsOfType(currentProjects, typeKey);
        }

        public void ResumeResearch(ResearchProjectDef project)
        {
            if (project == null)
                return;

            SetCurrentProjectByKey(project, GetCategoryKey(project));
        }

        public void ManageNotChosenByKey(string typeKey)
        {
            if (SemiRandomResearchMod.settings.reofferAfterAmountOfRerolls == 0)
            {
                notChosenProjects.Clear();
            }
            else
            {
                if (!currentRerollState.ContainsKey(typeKey))
                {
                    currentRerollState[typeKey] = 0;
                }
                currentRerollState[typeKey]++;
                foreach (ResearchProjectDef rdef in currentAvailableProjects.Where(x => GetCategoryKey(x) == typeKey))
                {
                    notChosenProjects[rdef] = currentRerollState[typeKey];
                }
                notChosenProjects = notChosenProjects.Where(x => x.Value > currentRerollState[typeKey] - SemiRandomResearchMod.settings.reofferAfterAmountOfRerolls).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        public void SetRerolledByKey(string typeKey, bool newValue)
        {
            rerolled[typeKey] = newValue;
        }

        public bool CanRerollByKey(string typeKey)
        {
            return SemiRandomResearchMod.settings.allowManualReroll == ManualReroll.Always ||
                (SemiRandomResearchMod.settings.allowManualReroll == ManualReroll.Once && (!rerolled.ContainsKey(typeKey) || !rerolled[typeKey]));
        }

        public void RerollByKey(string typeKey)
        {
            if (!CanRerollByKey(typeKey) || !HasRerollableOffers(typeKey))
                return;

            SetRerolledByKey(typeKey, true);

            if (GetCurrentlyAvailableProjects().Any(x => GetCategoryKey(x) == typeKey))
            {
                ManageNotChosenByKey(typeKey);
                SetCurrentProjectByKey(null, typeKey);
                currentAvailableProjects = currentAvailableProjects.Where(x => GetCategoryKey(x) != typeKey).ToList();
                additionalAvailableProjects = additionalAvailableProjects.Where(x => GetCategoryKey(x) != typeKey).ToHashSet();
                snoozeHeldSlots.Remove(typeKey);
                GetCurrentlyAvailableProjects();
                tickCounter = 0;
            }
        }

        // Public entry points at the original mod's signatures. Nice Research Tab calls SetCurrentProject by reflection,
        // bypassing ResearchManager.SetCurrentProject, so the selection gate is applied here too. Clearing is always allowed.
        public void SetCurrentProject(ResearchProjectDef newCurrentProject, KnowledgeCategoryDef type)
        {
            if (newCurrentProject != null &&
                !applyingTrackedProject &&
                SemiRandomResearchUtility.IsControllingResearchSelection)
            {
                SemiRandomResearchUtility.RejectSelection();
                return;
            }

            if (newCurrentProject != null)
            {
                SetCurrentProjectByKey(newCurrentProject, GetCategoryKey(newCurrentProject));
            }
            else
            {
                ResearchProjectDef active = GetActiveProject(type);
                if (active != null) SetCurrentProjectByKey(null, GetCategoryKey(active));
            }
        }

        // True when any category has offers it can reroll.
        public bool CanReroll()
        {
            if (SemiRandomResearchMod.settings.allowManualReroll == ManualReroll.None)
                return false;
            if (all_typeKeys == null) RefreshTypeKeys();

            for (int i = 0; i < all_typeKeys.Count; i++)
            {
                string key = all_typeKeys[i];
                if (CanRerollByKey(key) && HasRerollableOffers(key))
                    return true;
            }
            return false;
        }

        // Rerolls every category.
        public void Reroll()
        {
            if (all_typeKeys == null) RefreshTypeKeys();
            for (int i = 0; i < all_typeKeys.Count; i++)
                RerollByKey(all_typeKeys[i]);
        }

        // Rebuilds the offers now, so count changes apply without reopening the tab.
        public void SettingsChanged()
        {
            RefreshOffers();
        }

        public void RefreshOffers()
        {
            ForceAutoReseachCheckNextTick();
            loggedMessages.Clear();

            if (Current.Game != null && Faction.OfPlayerSilentFail != null)
                GetCurrentlyAvailableProjects();
        }

        public void ForceAutoReseachCheckNextTick()
        {
            tickCounter = 0;
            additionalProjectsRefresh = true;
        }

        public void ConsiderProjectFinished(ResearchProjectDef def)
        {
            if (def.IsDummyResearch())
                return;

            if (SemiRandomResearchMod.settings.verboseLogging)
                LogIfNewMessage("Consider Completed", def.LabelCap);

            string typeKey = GetCategoryKey(def);

            RecordCompletedProject(def);
            SetRerolledByKey(typeKey, false);
            snoozeHeldSlots.Remove(typeKey);
            ForceAutoReseachCheckNextTick();
            CompletionRevision++;

            if (currentProjects.Contains(def))
                SetCurrentProjectByKey(null, typeKey);

            if (SemiRandomResearchMod.settings.rerollAllEveryTime)
            {
                ManageNotChosenByKey(typeKey);
                currentAvailableProjects = currentAvailableProjects.Where(x => GetCategoryKey(x) != typeKey).ToList();
                additionalAvailableProjects = additionalAvailableProjects.Where(x => GetCategoryKey(x) != typeKey).ToHashSet();
                GetCurrentlyAvailableProjects();
            }
        }

        // A project the game finished silently: dropped from the current project and offers, without history or a reroll.
        // The caller rebuilds the offers once with RefreshOffers.
        public void ConsiderProjectFinishedSilently(ResearchProjectDef def)
        {
            if (def.IsDummyResearch())
                return;

            CompletionRevision++;
            if (currentProjects.Contains(def))
                SetCurrentProjectByKey(null, GetCategoryKey(def));
            currentAvailableProjects.Remove(def);
            additionalAvailableProjects.Remove(def);
        }

        public void AddProjectToAvailableProjects(ResearchProjectDef rdef)
        {
            additionalAvailableProjects.Add(rdef);
            additionalProjectsRefresh = true;
        }

        private void LogIfNewMessage(string key, string message)
        {
            if (!loggedMessages.TryGetValue(key, out string logged) || logged != message)
            {
                Log.Message($"[CM_Semi_Random_Research] <{key}>: {message}");
                loggedMessages[key] = message;
            }
        }
    }
}

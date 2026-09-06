using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CM_Semi_Random_Research
{
    // One finished project, kept so the research tab can show a short "recently completed" list.
    // Deep-scribed (rather than two parallel lists) so a removed mod's project cannot shift the
    // remaining entries out of sync with their completion ticks.
    public class ResearchHistoryEntry : IExposable
    {
        public ResearchProjectDef project;
        public int tick;

        public ResearchHistoryEntry()
        {
        }

        public ResearchHistoryEntry(ResearchProjectDef project, int tick)
        {
            this.project = project;
            this.tick = tick;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref project, "project");
            Scribe_Values.Look(ref tick, "tick", 0);
        }
    }

    public class ResearchTracker : WorldComponent
    {
        private List<ResearchProjectDef> currentAvailableProjects = new List<ResearchProjectDef>();
        private List<ResearchProjectDef> lastOfferedProjects;
        private List<ResearchProjectDef> lastOfferedForRevision;
        private Dictionary<ResearchProjectDef, int> notChosenProjects = new Dictionary<ResearchProjectDef, int>();
        private Dictionary<string, int> currentRerollState = new Dictionary<string, int>();
        private List<ResearchProjectDef> currentProjects = new List<ResearchProjectDef>();
        private HashSet<ResearchProjectDef> additionalAvailableProjects = new HashSet<ResearchProjectDef>();
        private HashSet<KnowledgeCategoryDef> pendingResearchRerolls = new HashSet<KnowledgeCategoryDef>();

        public List<ResearchProjectDef> CurrentProject => currentProjects;

        // Newest first. Only the tail of it is ever shown, but a few spare entries cost nothing
        // and let the history screen stay full on tall windows.
        private List<ResearchHistoryEntry> completedHistory = new List<ResearchHistoryEntry>();
        private const int MaxCompletedHistoryEntries = 40;

        public List<ResearchHistoryEntry> CompletedHistory => completedHistory;

        private bool researchPaused;
        public bool ResearchPaused => researchPaused;

        public int OffersRevision { get; private set; }

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
            if (currentProjects != null)
            {
                for (int i = 0; i < currentProjects.Count; i++)
                {
                    ResearchProjectDef current = currentProjects[i];
                    if (current != null && !current.IsFinished && GetCategoryKey(current) == typeKey)
                        return false;
                }
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

        // The tracker's own bookkeeping writes vanilla's current project constantly (restoring
        // the tracked project, resuming, auto picking). Those writes are not player selections
        // and must never be judged by the selection gate.
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

        // The one rule for "may the player start this project right now", shared by the research
        // tab's start button and by the gate that gets applied to external research trees, so a
        // handover to Node Research or Nice Research Tab obeys the same limits as our own window.
        public bool PlayerCanStartProject(ResearchProjectDef proj)
        {
            if (proj == null)
                return false;
            if (currentProjects.Contains(proj))
                return true;
            if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.allowSwitchingResearch)
                return true;
            // Gravship research runs alongside the standard project rather than replacing it.
            if (GetCategoryKey(proj) == "Gravship")
                return true;

            return !CategoryOccupied(proj);
        }

        // A paused project still occupies its category: it is waiting to be resumed, not free.
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
                : Find.ResearchManager.GetProject(proj.knowledgeCategory);

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

        public bool usingNodeResearch;

        private List<string> all_typeKeys;

        public ResearchTracker(World world) : base(world)
        {
            previousDefCount = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Count;
            RefreshTypeKeys();
        }

        // Recorded when the keys are built rather than re-scanned: this is asked on the world tick.
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

        // ==============================================================================
        // PSEUDO-CATEGORY GENERATOR
        // ==============================================================================
        // Hottest helper in the mod: the tick loop, the offer roll and every card drawn call it.
        // A project's tab and knowledge category are fixed once defs are loaded, so the answer is
        // memoised per def rather than re-running the string comparisons on every call.
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
            if (def.knowledgeCategory?.defName == "Information") return "Divinitech";
            if (def.knowledgeCategory != null) return def.knowledgeCategory.defName;
            return "Standard";
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);

            if (!fromLoad)
            {
                usingNodeResearch = SemiRandomResearchMod.settings.usingNodeResearch;
            }
            else
            {
                SemiRandomResearchMod.settings.usingNodeResearch = usingNodeResearch;
            }

            ResearchTabWindowSwitcher.Apply();

            // CostApparent / RestrictToFactionTechLevel need Faction.OfPlayer.
            // During WorldGenerator.GenerateWorld that faction does not exist yet.
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

            if (SemiRandomResearchMod.settings.verboseLogging)
            {
                string allCurrentProjects = "";
                foreach (ResearchProjectDef def in currentProjects)
                    allCurrentProjects += def != null ? def.LabelCap.RawText : "Null" + " ";
                LogIfNewMessage("Loaded Current Projects", allCurrentProjects);

                string allAvailableProjects = "";
                foreach (ResearchProjectDef def in currentAvailableProjects)
                    allAvailableProjects += def != null ? def.LabelCap.RawText : "Null" + " ";
                LogIfNewMessage("Loaded Available Projects", allAvailableProjects);
            }

            Scribe_Collections.Look(ref rerolled, "rerolled");
            if (rerolled == null) rerolled = new Dictionary<string, bool>();

            Scribe_Collections.Look(ref currentRerollState, "currentRerollState");
            if (currentRerollState == null) currentRerollState = new Dictionary<string, int>();

            Scribe_Collections.Look(ref lastPicked, "lastPicked");
            if (lastPicked == null) lastPicked = new Dictionary<string, bool>();

            Scribe_Collections.Look(ref pendingResearchRerolls, "pendingResearchRerolls", LookMode.Def);
            if (pendingResearchRerolls == null) pendingResearchRerolls = new HashSet<KnowledgeCategoryDef>();

            Scribe_Collections.Look(ref completedHistory, "completedHistory", LookMode.Deep);
            if (completedHistory == null)
                completedHistory = new List<ResearchHistoryEntry>();
            else
                completedHistory.RemoveAll(entry => entry == null || entry.project == null);

            bool defaultUsingNodeResearch = SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.usingNodeResearch;
            Scribe_Values.Look(ref usingNodeResearch, "usingNodeResearch", defaultUsingNodeResearch);
            Scribe_Values.Look(ref researchPaused, "researchPaused", false);
        }

        // Reused every slow pass rather than rebuilt, and filled by RefreshActiveByTypeKey.
        private readonly Dictionary<string, ResearchProjectDef> activeByTypeKey = new Dictionary<string, ResearchProjectDef>();

        // Anything that writes the vanilla current project invalidates the snapshot, so the tick
        // loop still sees changes it made earlier in the same pass - exactly as it did when it
        // asked ResearchManager again for every key.
        private bool activeByTypeKeyStale = true;

        // One pass over the knowledge categories for all pseudo-categories at once. The type loop
        // used to do this lookup per key, so each extra category multiplied the GetProject calls.
        // Categories are visited after the standard project and in list order, so the same
        // "last match wins" answer comes out.
        private void RefreshActiveByTypeKey()
        {
            activeByTypeKeyStale = false;
            activeByTypeKey.Clear();

            ResearchProjectDef standardActive = Find.ResearchManager.GetProject(null);
            if (standardActive != null)
                activeByTypeKey[GetCategoryKey(standardActive)] = standardActive;

            List<KnowledgeCategoryDef> categories = DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading;
            for (int i = 0; i < categories.Count; i++)
            {
                ResearchProjectDef catActive = Find.ResearchManager.GetProject(categories[i]);
                if (catActive != null)
                    activeByTypeKey[GetCategoryKey(catActive)] = catActive;
            }
        }

        // Filtering by pseudo-category runs on the world tick and while drawing the tab, so it is
        // a plain loop: the LINQ form allocated a capturing closure and an enumerator every call.
        // Nulls are skipped - a save that lost a mod scribes them into currentProjects, and the
        // old code would have handed one to IsFinished.
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

        // Strictly-less comparison, so ties keep the first entry exactly as OrderBy().First() did.
        private static ResearchProjectDef Cheapest(List<ResearchProjectDef> projects)
        {
            ResearchProjectDef best = null;
            float bestCost = 0f;
            for (int i = 0; i < projects.Count; i++)
            {
                ResearchProjectDef project = projects[i];
                if (project == null)
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

                // The vanilla active project per pseudo-category used to be looked up inside the
                // type loop, so every extra category multiplied the GetProject calls. Now it is
                // resolved for all keys at once and only re-read when something in the pass has
                // actually changed the current project.
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
                    bool finished = false;
                    ResearchProjectDef finishedProject = null;
                    for (int i = 0; i < currentProjectOfType.Count; i++)
                    {
                        if (currentProjectOfType[i].IsFinished)
                        {
                            finishedProject = currentProjectOfType[i];
                            break;
                        }
                    }
                    if (finishedProject != null)
                    {
                        finished = true;
                        ConsiderProjectFinished(finishedProject);
                    }

                    if ((currentProjectOfType.Count == 0 || finished) && !researchPaused)
                    {
                        // While the research tab is open the player is still choosing. Auto mode
                        // waits for the window to close (MainTabWindow_NextResearch.PreClose ->
                        // AutoPickNow) so toggling it on does not snatch the choice away, and it
                        // stays dormant until the tab has been opened at least once this session.
                        if (SemiRandomResearchMod.settings.autoPickNextResearch && AutoPickArmed &&
                            !SemiRandomResearchWindowOpen &&
                            (finished || (tickCounter % tickOffset) == 0))
                        {
                            List<ResearchProjectDef> possibleProjectsOfType = ProjectsOfType(currentAvailableProjects, typeKey);
                            if (possibleProjectsOfType.Count == 0)
                            {
                                if (availableSnapshot == null)
                                    availableSnapshot = GetCurrentlyAvailableProjects();
                                possibleProjectsOfType = ProjectsOfType(availableSnapshot, typeKey);
                            }

                            if (possibleProjectsOfType.Count > 0)
                            {
                                // Sort by CostApparent so it always grabs the cheapest project
                                ResearchProjectDef cheapestProject = Cheapest(possibleProjectsOfType);

                                bool alreadyActive = Find.ResearchManager.IsCurrentProject(cheapestProject);

                                SetCurrentProjectByKey(cheapestProject, typeKey);
                                currentProjectOfType = ProjectsOfType(currentProjects, typeKey);

                                // Suppress the message if the game was already researching this project
                                if (!alreadyActive)
                                {
                                    Messages.Message("CM_Semi_Random_Research_AutoStarted".Translate(cheapestProject.LabelCap), MessageTypeDefOf.NeutralEvent, false);
                                }
                            }
                        }
                    }

                    if (slowPass)
                    {
                        // Active project for this pseudo-category, resolved for every key at once.
                        if (activeByTypeKeyStale)
                            RefreshActiveByTypeKey();
                        activeByTypeKey.TryGetValue(typeKey, out ResearchProjectDef activeProject);
                        ResearchProjectDef trackedFirst = currentProjectOfType.Count > 0 ? currentProjectOfType[0] : null;

                        // --- VGE SPAM FIX START ---
                        // VGE hides Gravship projects from GetProject(), but exposes them via IsCurrentProject().
                        // This sees through VGE's cloak and accurately tracks it!
                        if (activeProject == null && trackedFirst != null &&
                            Find.ResearchManager.IsCurrentProject(trackedFirst))
                        {
                            activeProject = trackedFirst;
                        }
                        // --- VGE SPAM FIX END ---

                        if (!researchPaused)
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

        // Auto research stays dormant until the player has opened the research tab, so a new
        // colony never has its first project chosen before the player has even seen the offers.
        // Deliberately not saved: every load starts dormant again.
        private bool researchTabOpened;

        public void NotifyResearchTabOpened()
        {
            researchTabOpened = true;
        }

        // With the UI delegated to Node Research our tab never opens, so there would be nothing
        // to arm the gate with - auto behaves as it always did for those players.
        private bool AutoPickArmed => researchTabOpened || usingNodeResearch;

        // Auto mode's pick. Called when the research tab closes, so the player always gets a
        // chance to choose first. Picks the cheapest of the offered cards per category.
        public void AutoPickNow()
        {
            if (researchPaused || SemiRandomResearchMod.settings == null || !SemiRandomResearchMod.settings.autoPickNextResearch)
                return;
            if (!AutoPickArmed || Current.Game == null || Faction.OfPlayerSilentFail == null)
                return;

            if (all_typeKeys == null || AnomalyTypeKeysNeedRefresh())
                RefreshTypeKeys();

            List<ResearchProjectDef> availableSnapshot = null;

            foreach (string typeKey in all_typeKeys)
            {
                if (currentProjects.Any(x => x != null && !x.IsFinished && GetCategoryKey(x) == typeKey))
                    continue;

                List<ResearchProjectDef> possibleProjectsOfType = currentAvailableProjects
                    .Where(x => x != null && !x.IsFinished && x.CanStartNow && GetCategoryKey(x) == typeKey).ToList();

                if (possibleProjectsOfType.Empty())
                {
                    if (availableSnapshot == null)
                        availableSnapshot = GetCurrentlyAvailableProjects();
                    possibleProjectsOfType = availableSnapshot
                        .Where(x => x != null && !x.IsFinished && x.CanStartNow && GetCategoryKey(x) == typeKey).ToList();
                }

                if (possibleProjectsOfType.Empty())
                    continue;

                ResearchProjectDef cheapestProject = possibleProjectsOfType.OrderBy(x => x.CostApparent).First();
                bool alreadyActive = Find.ResearchManager.IsCurrentProject(cheapestProject);

                SetCurrentProjectByKey(cheapestProject, typeKey);

                if (!alreadyActive)
                    Messages.Message("CM_Semi_Random_Research_AutoStarted".Translate(cheapestProject.LabelCap), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void RecordCompletedProject(ResearchProjectDef def)
        {
            if (def == null)
                return;

            if (completedHistory == null)
                completedHistory = new List<ResearchHistoryEntry>();

            completedHistory.RemoveAll(entry => entry == null || entry.project == def);
            completedHistory.Insert(0, new ResearchHistoryEntry(def, Find.TickManager?.TicksGame ?? 0));

            if (completedHistory.Count > MaxCompletedHistoryEntries)
                completedHistory.RemoveRange(MaxCompletedHistoryEntries, completedHistory.Count - MaxCompletedHistoryEntries);
        }

        public List<ResearchProjectDef> GetCurrentlyAvailableProjects()
        {
            // Rolling uses CostApparent, which NREs if the player faction is missing.
            if (Faction.OfPlayerSilentFail == null)
                return currentAvailableProjects ?? new List<ResearchProjectDef>();

            if (all_typeKeys == null || AnomalyTypeKeysNeedRefresh())
                RefreshTypeKeys();
            List<ResearchProjectDef> result = new List<ResearchProjectDef>();
            SemiRandomResearchMod.settings.DumpSettingToLog();

            currentAvailableProjects = currentAvailableProjects.Where(projectDef => projectDef != null &&
                !projectDef.IsFinished &&
                !Compatibility.IsHiddenResearch(projectDef) &&
                Compatibility.SatisfiesAlienRaceRestriction(projectDef)).ToList();

            if (currentProjects.Count > 0)
            {
                for (int i = currentProjects.Count - 1; i >= 0; i--)
                {
                    ResearchProjectDef current = currentProjects[i];
                    if (current != null && Compatibility.IsHiddenResearch(current))
                        SetCurrentProjectByKey(null, GetCategoryKey(current));
                }
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

                if (!SemiRandomResearchMod.settings.rerollAllEveryTime ||
                    SemiRandomResearchMod.settings.allowSwitchingResearch ||
                    currentProjectOfType.Empty() ||
                    currentProjectOfType.Any(x => x.IsFinished || !Compatibility.SatisfiesAlienRaceRestriction(x)))
                {

                    bool handledProjects = false;
                    int numberOfMissingProjects = Math.Min((SemiRandomResearchMod.settings.availableProjectCount + additionalProjects), SemiRandomResearchMod.settings.maxProjectCount) - currentAvailableValidProjectsOfType.Count;

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
                        numberOfMissingProjects = Math.Min((SemiRandomResearchMod.settings.availableProjectCount + additionalProjects), SemiRandomResearchMod.settings.maxProjectCount) - currentAvailableValidProjectsOfType.Count;
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

                        if (!currentProjectOfType.Empty() && currentProjectOfType.Any(x => !x.IsFinished && Compatibility.SatisfiesAlienRaceRestriction(x)))
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
            // Keep the persisted offer list in sync with what we publish (trim path
            // previously only updated the returned/published list).
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

            TechLevel maxCurrentProjectTechlevel = TechLevel.Archotech;
            TechLevel minCurrentProjectTechlevel = TechLevel.Archotech;
            if (currentAvailableProjects.Count > 0)
            {
                maxCurrentProjectTechlevel = currentAvailableProjects[0].techLevel;
                minCurrentProjectTechlevel = currentAvailableProjects[0].techLevel;
                for (int i = 1; i < currentAvailableProjects.Count; i++)
                {
                    TechLevel techLevel = currentAvailableProjects[i].techLevel;
                    if (techLevel > maxCurrentProjectTechlevel) maxCurrentProjectTechlevel = techLevel;
                    if (techLevel < minCurrentProjectTechlevel) minCurrentProjectTechlevel = techLevel;
                }
            }

            if (!projectDefsCacheByType.TryGetValue(typeKey, out List<ResearchProjectDef> projectsOfType))
            {
                List<ResearchProjectDef> allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
                projectsOfType = new List<ResearchProjectDef>();
                for (int i = 0; i < allDefs.Count; i++)
                {
                    ResearchProjectDef projectDef = allDefs[i];
                    if (!projectDef.IsFinished &&
                        GetCategoryKey(projectDef) == typeKey &&
                        !Compatibility.IsHiddenResearch(projectDef))
                    {
                        projectsOfType.Add(projectDef);
                    }
                }
                projectDefsCacheByType[typeKey] = projectsOfType;

                if (projectsOfType.Count == 0)
                {
                    completedTypes.Add(typeKey);
                }
            }

            // CanStartNow is by far the most expensive of these three - it walks the colony's
            // research benches - so the two cheap rejections are made first. Membership goes
            // through a set because this runs over every unfinished project of the category.
            HashSet<ResearchProjectDef> currentlyOffered = new HashSet<ResearchProjectDef>(currentAvailableProjects);
            List<ResearchProjectDef> availableList = new List<ResearchProjectDef>();
            for (int i = 0; i < projectsOfType.Count; i++)
            {
                ResearchProjectDef projectDef = projectsOfType[i];
                if (currentlyOffered.Contains(projectDef))
                    continue;
                if (!Compatibility.DoCompatibilityChecks(projectDef))
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
                    allAvailableProjectsDebug = allAvailableProjectsDebug.Where((ResearchProjectDef projectDef) => Compatibility.SatisfiesAlienRaceRestriction(projectDef)).ToList();
                    LogIfNewMessage("NoAvailableProjects3" + typeKey, $"[CM_Semi_Random_Research] Of which {allAvailableProjectsDebug.Count} you have the required races for");
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

            // Evaluated once and kept as a set. As a lazy query this was re-run for its Count, for
            // every Contains in the Never branch and again once per element by the final
            // OrderByDescending, so a single roll re-read ProgressReal for the whole category
            // hundreds of times over.
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
                int remainingCount = count;

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
                    // Materialised once and then shuffled in place. Every one of the 25 passes used
                    // to rebuild the candidate list from a lazy query and enumerate the chosen
                    // slice twice more for its sum and its count.
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

        // ==============================================================================
        // NEW STRING-BASED TRACKING
        // ==============================================================================

        public void SetCurrentProjectByKey(ResearchProjectDef newCurrentProject, string typeKey)
        {
            loggedMessages.Clear();
            activeByTypeKeyStale = true;
            currentProjects = currentProjects.Where(x => GetCategoryKey(x) != typeKey).ToList();
            projectDefsCacheByType.Remove(typeKey);
            researchPaused = false;
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
            ResearchProjectDef standardActive = Find.ResearchManager.GetProject(null);
            if (standardActive != null && GetCategoryKey(standardActive) == typeKey) active = standardActive;
            foreach (var cat in DefDatabase<KnowledgeCategoryDef>.AllDefsListForReading)
            {
                ResearchProjectDef catActive = Find.ResearchManager.GetProject(cat);
                if (catActive != null && GetCategoryKey(catActive) == typeKey) active = catActive;
            }

            // Add safety net for stopping Gravship projects since they hide from GetProject
            ResearchProjectDef trackedType = currentProjects.FirstOrDefault(x => GetCategoryKey(x) == typeKey);
            if (trackedType == null && currentProjectDefsCacheByType.ContainsKey(typeKey))
                trackedType = currentProjectDefsCacheByType[typeKey].FirstOrDefault();
            if (active == null && trackedType != null && Find.ResearchManager.IsCurrentProject(trackedType))
                active = trackedType;

            if (active != null)
                Find.ResearchManager.StopProject(active);
            else if (trackedType != null && Find.ResearchManager.IsCurrentProject(trackedType))
                Find.ResearchManager.StopProject(trackedType);
        }

        public void PauseResearch(ResearchProjectDef project)
        {
            if (project == null)
                return;

            string typeKey = GetCategoryKey(project);
            if (!currentProjects.Contains(project))
                currentProjects.Add(project);

            researchPaused = true;
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
                    if (!notChosenProjects.ContainsKey(rdef))
                    {
                        notChosenProjects.Add(rdef, currentRerollState[typeKey]);
                    }
                    else
                    {
                        notChosenProjects[rdef] = currentRerollState[typeKey];
                    }
                }
                notChosenProjects = notChosenProjects.Where(x => x.Value > currentRerollState[typeKey] - SemiRandomResearchMod.settings.reofferAfterAmountOfRerolls).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        public void SetRerolledByKey(string typeKey, bool newValue)
        {
            if (!rerolled.ContainsKey(typeKey))
            {
                rerolled.Add(typeKey, newValue);
            }
            else
            {
                rerolled[typeKey] = newValue;
            }
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
                GetCurrentlyAvailableProjects();
                tickCounter = 0;
            }
        }

        // ==============================================================================
        // COMPATIBILITY WRAPPERS FOR UI BUTTONS
        // These intercept calls from your UI and route them to the Pseudo-Categories
        // ==============================================================================

        // Public entry point kept at the original mod's signature, because other research UIs
        // find it by reflection: Nice Research Tab looks up CM_Semi_Random_Research.ResearchTracker
        // and calls this to start a project, which never touches ResearchManager.SetCurrentProject
        // and so slips straight past the gate there. Prohibit has to be enforced here as well.
        // Clearing the project (null) is a stop rather than a selection, so it stays allowed.
        public void SetCurrentProject(ResearchProjectDef newCurrentProject, KnowledgeCategoryDef type)
        {
            if (newCurrentProject != null &&
                !applyingTrackedProject &&
                SemiRandomResearchUtility.IsControllingResearchSelection)
            {
                ResearchManager_Patches.RejectSelection();
                return;
            }

            if (newCurrentProject != null)
            {
                SetCurrentProjectByKey(newCurrentProject, GetCategoryKey(newCurrentProject));
            }
            else
            {
                ResearchProjectDef active = Find.ResearchManager.GetProject(type);
                if (active != null) SetCurrentProjectByKey(null, GetCategoryKey(active));
            }
        }

        public bool CanReroll(KnowledgeCategoryDef type)
        {
            if (SemiRandomResearchMod.settings.allowManualReroll == ManualReroll.None)
                return false;
            if (all_typeKeys == null) RefreshTypeKeys();

            if (type == null)
            {
                for (int i = 0; i < all_typeKeys.Count; i++)
                {
                    string key = all_typeKeys[i];
                    if (CanRerollByKey(key) && HasRerollableOffers(key))
                        return true;
                }
                return false;
            }

            string typeKey = type.defName;
            return CanRerollByKey(typeKey) && HasRerollableOffers(typeKey);
        }

        public void Reroll(KnowledgeCategoryDef type)
        {
            if (type == null)
            {
                if (all_typeKeys == null) RefreshTypeKeys();
                for (int i = 0; i < all_typeKeys.Count; i++)
                    RerollByKey(all_typeKeys[i]);
            }
            else
            {
                RerollByKey(type.defName);
            }
        }

        // ==============================================================================

        public void SettingsChanged()
        {
            ForceAutoReseachCheckNextTick();
            loggedMessages.Clear();

            // Rebuild offers immediately so count / max / colonist-based amount
            // changes apply without needing a game restart or tab reopen.
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
            {
                return;
            }

            if (SemiRandomResearchMod.settings.verboseLogging)
            {
                LogIfNewMessage("Consider Completed", def?.LabelCap);
            }

            string typeKey = GetCategoryKey(def);

            RecordCompletedProject(def);
            SetRerolledByKey(typeKey, false);
            ForceAutoReseachCheckNextTick();

            // Clear current project
            if (currentProjects.Contains(def))
            {
                SetCurrentProjectByKey(null, typeKey);
            }

            // Immediately handle reroll
            if (SemiRandomResearchMod.settings.rerollAllEveryTime)
            {
                ManageNotChosenByKey(typeKey);
                currentAvailableProjects = currentAvailableProjects.Where(x => GetCategoryKey(x) != typeKey).ToList();
                additionalAvailableProjects = additionalAvailableProjects.Where(x => GetCategoryKey(x) != typeKey).ToHashSet();
                GetCurrentlyAvailableProjects();
            }
        }

        public void AddProjectToAvailableProjects(ResearchProjectDef rdef)
        {
            additionalAvailableProjects.Add(rdef);
            additionalProjectsRefresh = true;
        }

        private void LogIfNewMessage(string key, string message)
        {
            if (!loggedMessages.ContainsKey(key) || loggedMessages[key] != message)
            {
                Log.Message($"[CM_Semi_Random_Research] <{key}>: {message}");
                loggedMessages[key] = message;
            }
        }
    }
}
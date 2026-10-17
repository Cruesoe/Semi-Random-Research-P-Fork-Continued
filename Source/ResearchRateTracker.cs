using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CM_Semi_Random_Research
{
    // Samples research progress every game hour, per active project and overall, for rates, ETAs and the graph.
    public class ResearchRateTracker : WorldComponent
    {
        private const int SAMPLES_PER_DAY = 24;
        private const int DAYS_TO_TRACK = 10;
        private const int MAX_SAMPLES = SAMPLES_PER_DAY * DAYS_TO_TRACK;
        private const float SAMPLE_INTERVAL_TICKS = GenDate.TicksPerDay / SAMPLES_PER_DAY;

        // Keyed by defName. Entries are dropped once their project is finished.
        private Dictionary<string, ProjectRateData> projectDataCache = new Dictionary<string, ProjectRateData>();

        private List<float> globalRateSamples = new List<float>();

        private float nextSampleTick = 0f;

        // Projects sampled before, which keep getting zero samples while not being researched.
        private HashSet<string> previousProjectDefNames = new HashSet<string>();

        public ResearchRateTracker(World world) : base(world)
        {
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            if (nextSampleTick <= 0f)
                nextSampleTick = Find.TickManager.TicksGame + SAMPLE_INTERVAL_TICKS;

            PruneFinishedProjects();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (Find.TickManager.TicksGame >= nextSampleTick)
            {
                SampleCurrentResearchRate();
                nextSampleTick = Find.TickManager.TicksGame + SAMPLE_INTERVAL_TICKS;
            }
        }

        // Scratch buffers reused between samples.
        private readonly List<ResearchProjectDef> activeProjectsBuffer = new List<ResearchProjectDef>();
        private readonly HashSet<string> activeDefNamesBuffer = new HashSet<string>();
        private readonly List<string> trackedDefNamesBuffer = new List<string>();
        private readonly Dictionary<string, ResearchProjectDef> defsByName = new Dictionary<string, ResearchProjectDef>();
        private readonly Dictionary<string, float> categoryProgressBuffer = new Dictionary<string, float>();

        // Keyed by ResearchTracker category key (Standard, Gravship, Anomaly categories).
        private Dictionary<string, List<float>> categoryRateSamples = new Dictionary<string, List<float>>();

        // Average research points per day for a category over up to the last ten days; 0 before it has been sampled.
        // Standard falls back to the global average until it has samples of its own.
        public float GetCategoryAverageRate(string categoryKey)
        {
            if (categoryKey != null && categoryRateSamples.TryGetValue(categoryKey, out List<float> samples) && samples.Count > 0)
                return AverageRecent(samples, DAYS_TO_TRACK);
            return categoryKey == "Standard" ? GetGlobalAverageRate() : 0f;
        }

        private void SampleCurrentResearchRate()
        {
            ResearchManager researchManager = Find.ResearchManager;
            if (researchManager == null) return;

            List<ResearchProjectDef> activeProjects = activeProjectsBuffer;
            activeProjects.Clear();
            activeDefNamesBuffer.Clear();

            ResearchProjectDef currentProject = researchManager.GetProject();
            if (currentProject != null)
                activeProjects.Add(currentProject);

            ResearchTracker researchTracker = SemiRandomResearchUtility.Tracker;
            if (researchTracker != null)
            {
                List<ResearchProjectDef> tracked = researchTracker.CurrentProject;
                for (int i = 0; i < tracked.Count; i++)
                {
                    ResearchProjectDef proj = tracked[i];
                    if (proj != null && !activeProjects.Contains(proj))
                        activeProjects.Add(proj);
                }
            }

            float totalProgressChange = 0f;
            categoryProgressBuffer.Clear();
            for (int i = 0; i < activeProjects.Count; i++)
            {
                ResearchProjectDef project = activeProjects[i];
                float change = SampleProjectRate(project);
                totalProgressChange += change;
                string key = ResearchTracker.GetCategoryKey(project);
                categoryProgressBuffer.TryGetValue(key, out float categoryChange);
                categoryProgressBuffer[key] = categoryChange + change;
                previousProjectDefNames.Add(project.defName);
                activeDefNamesBuffer.Add(project.defName);
            }

            // Idle hours count too, so the global average matches the per-project one, which records zeros.
            globalRateSamples.Add(totalProgressChange * SAMPLES_PER_DAY);
            TrimSamples(globalRateSamples);

            // Each research category's own rate; a category gets zeros once it has been researched at all.
            foreach (string key in categoryProgressBuffer.Keys)
            {
                if (!categoryRateSamples.ContainsKey(key))
                    categoryRateSamples[key] = new List<float>();
            }
            foreach (KeyValuePair<string, List<float>> category in categoryRateSamples)
            {
                categoryProgressBuffer.TryGetValue(category.Key, out float change);
                category.Value.Add(change * SAMPLES_PER_DAY);
                TrimSamples(category.Value);
            }

            // Copied out first because finished projects are removed from the set while walking it.
            trackedDefNamesBuffer.Clear();
            trackedDefNamesBuffer.AddRange(previousProjectDefNames);
            for (int i = 0; i < trackedDefNamesBuffer.Count; i++)
            {
                string projectDefName = trackedDefNamesBuffer[i];
                if (activeDefNamesBuffer.Contains(projectDefName))
                    continue;

                ResearchProjectDef projectDef = ProjectByName(projectDefName);
                if (projectDef == null || projectDef.IsFinished)
                {
                    previousProjectDefNames.Remove(projectDefName);
                    projectDataCache.Remove(projectDefName);
                }
                else if (projectDataCache.TryGetValue(projectDefName, out ProjectRateData projectData) && projectData.rateSamples.Count > 0)
                {
                    projectData.rateSamples.Add(0f);
                    TrimSamples(projectData.rateSamples);
                }
            }
        }

        // Drops data kept for projects that are finished or no longer exist.
        private void PruneFinishedProjects()
        {
            foreach (string defName in projectDataCache.Keys.ToList())
            {
                ResearchProjectDef projectDef = ProjectByName(defName);
                if (projectDef == null || projectDef.IsFinished)
                {
                    projectDataCache.Remove(defName);
                    previousProjectDefNames.Remove(defName);
                }
            }
        }

        private ResearchProjectDef ProjectByName(string defName)
        {
            if (defsByName.TryGetValue(defName, out ResearchProjectDef cached))
                return cached;

            ResearchProjectDef projectDef = DefDatabase<ResearchProjectDef>.GetNamed(defName, false);
            defsByName[defName] = projectDef;
            return projectDef;
        }

        // Records the project's rate since the last sample and returns its progress change.
        private float SampleProjectRate(ResearchProjectDef project)
        {
            if (project == null) return 0f;

            if (!projectDataCache.TryGetValue(project.defName, out ProjectRateData projectData))
            {
                projectData = new ProjectRateData();
                projectDataCache[project.defName] = projectData;
            }

            float currentProgress = project.ProgressApparent;
            float progressChange = 0f;

            if (projectData.lastSampleProgress >= 0)
            {
                progressChange = currentProgress - projectData.lastSampleProgress;

                if (progressChange >= 0)
                {
                    projectData.rateSamples.Add(progressChange * SAMPLES_PER_DAY);
                    TrimSamples(projectData.rateSamples);
                }
                else
                {
                    // Progress went down (dev mode or another mod): start this project's history again.
                    projectData.rateSamples.Clear();
                    progressChange = 0f;
                }
            }

            projectData.lastSampleProgress = currentProgress;
            return progressChange;
        }

        public float GetCurrentRate(ResearchProjectDef project)
        {
            if (project == null) return GetGlobalCurrentRate();

            if (projectDataCache.TryGetValue(project.defName, out ProjectRateData projectData) && projectData.rateSamples.Count > 0)
                return projectData.rateSamples[projectData.rateSamples.Count - 1];

            return 0f;
        }

        private float GetGlobalCurrentRate()
        {
            return globalRateSamples.Count > 0 ? globalRateSamples[globalRateSamples.Count - 1] : 0f;
        }

        // Falls back to the global average when the project has no samples.
        public float GetAverageRate(ResearchProjectDef project, int days = DAYS_TO_TRACK)
        {
            if (project != null &&
                projectDataCache.TryGetValue(project.defName, out ProjectRateData projectData) &&
                projectData.rateSamples.Count > 0)
            {
                return AverageRecent(projectData.rateSamples, days);
            }

            return GetGlobalAverageRate(days);
        }

        public float GetGlobalAverageRate(int days = DAYS_TO_TRACK)
        {
            return AverageRecent(globalRateSamples, days);
        }

        private static void TrimSamples(List<float> samples)
        {
            if (samples.Count > MAX_SAMPLES)
                samples.RemoveRange(0, samples.Count - MAX_SAMPLES);
        }

        private static float AverageRecent(List<float> samples, int days)
        {
            int count = samples.Count;
            if (count == 0)
                return 0f;

            int samplesToAverage = Math.Min(days * SAMPLES_PER_DAY, count);
            float sum = 0f;
            for (int i = count - samplesToAverage; i < count; i++)
                sum += samples[i];

            return sum / samplesToAverage;
        }

        private static List<float> CopyRecent(List<float> samples, int maxCount)
        {
            int count = samples.Count;
            int samplesToGet = Math.Min(maxCount, count);
            var result = new List<float>(samplesToGet);
            for (int i = count - samplesToGet; i < count; i++)
                result.Add(samples[i]);

            return result;
        }

        public List<float> GetRateSamplesPeriod(ResearchProjectDef project, int days)
        {
            if (project == null) return CopyRecent(globalRateSamples, days * SAMPLES_PER_DAY);

            if (projectDataCache.TryGetValue(project.defName, out ProjectRateData projectData))
                return CopyRecent(projectData.rateSamples, days * SAMPLES_PER_DAY);

            return new List<float>();
        }

        public List<float> GetGlobalRateSamplesPeriod(int days)
        {
            return CopyRecent(globalRateSamples, days * SAMPLES_PER_DAY);
        }

        public ResearchRateInfo GetResearchRateInfo(ResearchProjectDef project)
        {
            float current = project != null ? GetCurrentRate(project) : GetGlobalCurrentRate();
            float average = project != null ? GetAverageRate(project) : GetGlobalAverageRate();
            float eta = -1f;
            if (project != null)
            {
                float averageForEta = average > 0f ? average : GetGlobalAverageRate();
                if (averageForEta > 0f)
                {
                    float remainingProgress = project.CostApparent - project.ProgressApparent;
                    eta = remainingProgress <= 0f ? 0f : remainingProgress / averageForEta;
                }
            }

            return new ResearchRateInfo
            {
                CurrentRate = current,
                AverageRate = average,
                EstimatedDaysToCompletion = eta,
                TotalSamples = project != null
                    ? (projectDataCache.TryGetValue(project.defName, out ProjectRateData data) ? data.rateSamples.Count : 0)
                    : globalRateSamples.Count
            };
        }

        public static string FormatRate(float ratePerDay)
        {
            if (ratePerDay <= 0) return "CM_Semi_Random_Research_RatePerDay".Translate("0").ToString();

            return "CM_Semi_Random_Research_RatePerDay".Translate(ratePerDay.ToString("F1")).ToString();
        }

        public static string FormatRateShort(float ratePerDay)
        {
            if (ratePerDay <= 0) return "CM_Semi_Random_Research_RateZeroShort".Translate();

            return "CM_Semi_Random_Research_RatePerDayShort".Translate(ratePerDay.ToString("F1")).ToString();
        }

        public static string FormatETA(float daysToCompletion)
        {
            return FormatDuration(daysToCompletion);
        }

        public static string FormatETAUntilComplete(float daysToCompletion)
        {
            if (daysToCompletion < 0)
                return "CM_Semi_Random_Research_Unknown".Translate();
            if (daysToCompletion == 0)
                return "CM_Semi_Random_Research_Complete".Translate();

            return "CM_Semi_Random_Research_ETA_UntilComplete".Translate(FormatDuration(daysToCompletion)).ToString();
        }

        // The in-game date the given number of days from now, at the current or first home map's longitude; null without a map.
        public static string DateAfter(float days)
        {
            Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null || days < 0f || days > 30000f)
                return null;
            long ticks = Find.TickManager.TicksAbs + (long)(days * GenDate.TicksPerDay);
            return GenDate.DateShortStringAt(ticks, Find.WorldGrid.LongLatOf(map.Tile));
        }

        // A time left, or the date it ends on when "Show completion dates" is on.
        public static string FormatEstimate(float daysToCompletion)
        {
            if (daysToCompletion > 0f && SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.showCompletionDates &&
                DateAfter(daysToCompletion) is string date)
                return date;
            return FormatDuration(daysToCompletion);
        }

        private static string FormatDuration(float daysToCompletion)
        {
            if (daysToCompletion < 0)
                return "CM_Semi_Random_Research_Unknown".Translate();
            if (daysToCompletion == 0)
                return "CM_Semi_Random_Research_Complete".Translate();

            if (daysToCompletion < 1f)
            {
                float hours = daysToCompletion * 24f;
                return "CM_Semi_Random_Research_ETA_Hours".Translate(hours.ToString("F1")).ToString();
            }

            int daysPerYear = GenDate.DaysPerYear;
            int totalDays = Math.Max(1, (int)Math.Round(daysToCompletion));
            int years = totalDays / daysPerYear;
            int days = totalDays % daysPerYear;

            string dayLabel = days == 1
                ? "CM_Semi_Random_Research_ETA_Day".Translate(1).ToString()
                : "CM_Semi_Random_Research_ETA_Days".Translate(days.ToString()).ToString();
            string yearLabel = years == 1
                ? "CM_Semi_Random_Research_ETA_Year".Translate(1).ToString()
                : "CM_Semi_Random_Research_ETA_Years".Translate(years.ToString()).ToString();

            if (years <= 0)
                return dayLabel;
            if (days <= 0)
                return yearLabel;
            return yearLabel + " " + dayLabel;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref nextSampleTick, "nextSampleTick", 0f);
            Scribe_Collections.Look(ref globalRateSamples, "globalRateSamples", LookMode.Value);
            Scribe_Collections.Look(ref previousProjectDefNames, "previousProjectDefNames", LookMode.Value);

            // The dictionary is saved as two parallel lists.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<string> projectIds = projectDataCache.Keys.ToList();
                List<ProjectRateData> projectDatas = projectDataCache.Values.ToList();

                Scribe_Collections.Look(ref projectIds, "projectIds", LookMode.Value);
                Scribe_Collections.Look(ref projectDatas, "projectDatas", LookMode.Deep);

                List<string> categoryKeys = categoryRateSamples.Keys.ToList();
                List<CategoryRateData> categoryDatas = categoryRateSamples.Values.Select(s => new CategoryRateData { rateSamples = s }).ToList();
                Scribe_Collections.Look(ref categoryKeys, "categoryKeys", LookMode.Value);
                Scribe_Collections.Look(ref categoryDatas, "categoryDatas", LookMode.Deep);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<string> projectIds = null;
                List<ProjectRateData> projectDatas = null;
                List<string> categoryKeys = null;
                List<CategoryRateData> categoryDatas = null;

                Scribe_Collections.Look(ref projectIds, "projectIds", LookMode.Value);
                Scribe_Collections.Look(ref projectDatas, "projectDatas", LookMode.Deep);
                Scribe_Collections.Look(ref categoryKeys, "categoryKeys", LookMode.Value);
                Scribe_Collections.Look(ref categoryDatas, "categoryDatas", LookMode.Deep);

                categoryRateSamples.Clear();
                if (categoryKeys != null && categoryDatas != null && categoryKeys.Count == categoryDatas.Count)
                {
                    for (int i = 0; i < categoryKeys.Count; i++)
                        categoryRateSamples[categoryKeys[i]] = categoryDatas[i].rateSamples;
                }

                if (globalRateSamples == null)
                    globalRateSamples = new List<float>();
                if (previousProjectDefNames == null)
                    previousProjectDefNames = new HashSet<string>();

                projectDataCache.Clear();
                if (projectIds != null && projectDatas != null && projectIds.Count == projectDatas.Count)
                {
                    for (int i = 0; i < projectIds.Count; i++)
                        projectDataCache[projectIds[i]] = projectDatas[i];
                }
            }
        }

        public class ProjectRateData : IExposable
        {
            public float lastSampleProgress = -1f;
            public List<float> rateSamples = new List<float>();

            public void ExposeData()
            {
                Scribe_Values.Look(ref lastSampleProgress, "lastSampleProgress", -1f);
                Scribe_Collections.Look(ref rateSamples, "rateSamples", LookMode.Value);
                if (rateSamples == null)
                    rateSamples = new List<float>();
            }
        }
    }

    public class CategoryRateData : IExposable
    {
        public List<float> rateSamples = new List<float>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref rateSamples, "rateSamples", LookMode.Value);
            if (rateSamples == null)
                rateSamples = new List<float>();
        }
    }

    public class ResearchRateInfo
    {
        public float CurrentRate { get; set; }
        public float AverageRate { get; set; }
        public float EstimatedDaysToCompletion { get; set; }
        public int TotalSamples { get; set; }

        public string AverageRateFormatted => ResearchRateTracker.FormatRate(AverageRate);
    }
}

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    public partial class MainTabWindow_NextResearch
    {
        private static bool? progressionCoreActiveCached;
        private static bool? genesisActiveCached;
        private static bool? vfeTribalsActiveCached;

        private static bool ProgressionCoreActive
        {
            get
            {
                if (progressionCoreActiveCached == null)
                    progressionCoreActiveCached = GenTypes.GetTypeInAnyAssembly("ProgressionCore.ProgressionCoreMod") != null;
                return progressionCoreActiveCached.Value;
            }
        }

        private static bool GenesisActive
        {
            get
            {
                if (genesisActiveCached == null)
                    genesisActiveCached = GenTypes.GetTypeInAnyAssembly("Genesis.GenesisEra") != null;
                return genesisActiveCached.Value;
            }
        }

        private static bool VfeTribalsActive
        {
            get
            {
                if (vfeTribalsActiveCached == null)
                    vfeTribalsActiveCached = ModLister.GetActiveModWithIdentifier("OskarPotocki.VFE.Tribals") != null;
                return vfeTribalsActiveCached.Value;
            }
        }

        private void RefreshWorldTech()
        {
            cachedWorldTech = Find.World.worldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.IsPlayer)
                .Select(s => s.Faction.def.techLevel)
                .DefaultIfEmpty(TechLevel.Undefined)
                .Max();
        }

        // Completed, total, spent and remaining cost per tech level, over the whole research database.
        private void RebuildTechLevelStats()
        {
            if (cachedTechLevelStats == null || cachedTechLevelStats.Length != TechLevelCount)
                cachedTechLevelStats = new (int completed, int total, float remainingCost, float spentCost)[TechLevelCount];

            var stats = cachedTechLevelStats;
            for (int i = 0; i < stats.Length; i++)
                stats[i] = (0, 0, 0f, 0f);

            bool countCosts = Faction.OfPlayerSilentFail != null;
            List<ResearchProjectDef> allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ResearchProjectDef def = allDefs[i];
                int level = (int)def.techLevel;
                if (level < 0 || level >= stats.Length)
                    continue;

                bool finished = def.IsFinished;
                stats[level].total++;
                if (finished)
                {
                    stats[level].completed++;
                }
                if (countCosts &&
                    !Compatibility.IsDummyResearch(def) &&
                    !Compatibility.IsHiddenResearch(def))
                {
                    if (finished)
                    {
                        stats[level].spentCost += def.CostApparent;
                    }
                    else
                    {
                        float progress = def.ProgressApparent;
                        stats[level].spentCost += progress;
                        float remaining = def.CostApparent - progress;
                        if (remaining > 0f)
                            stats[level].remainingCost += remaining;
                    }
                }
            }
            cachedTechLevelStatsTick = Find.TickManager.TicksGame;
            techLevelStatsVersion++;
        }

        // "Industrial (42/97)" labels and their widths, rebuilt with the stats.
        private struct TechLevelLabel
        {
            public string name;
            public string stats;
            public float nameWidth;
            public float fullWidth;
        }

        private TechLevelLabel[] cachedTechLevelLabels;
        private int cachedTechLevelLabelsVersion = -1;
        private int techLevelStatsVersion;

        private void EnsureTechLevelLabels()
        {
            if (cachedTechLevelLabels != null && cachedTechLevelLabelsVersion == techLevelStatsVersion)
                return;

            cachedTechLevelLabelsVersion = techLevelStatsVersion;
            if (cachedTechLevelLabels == null || cachedTechLevelLabels.Length != TechLevelCount)
                cachedTechLevelLabels = new TechLevelLabel[TechLevelCount];

            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Small;
            for (int i = 0; i < TechLevelCount; i++)
            {
                // Undefined is never drawn.
                if (i == (int)TechLevel.Undefined)
                {
                    cachedTechLevelLabels[i] = new TechLevelLabel { name = string.Empty, stats = string.Empty };
                    continue;
                }

                TryGetTechLevelStats((TechLevel)i, out var stats);
                string name = ((TechLevel)i).ToStringHuman().CapitalizeFirst();
                string statsText = $" ({stats.completed}/{stats.total})";
                cachedTechLevelLabels[i] = new TechLevelLabel
                {
                    name = name,
                    stats = statsText,
                    nameWidth = Text.CalcSize(name).x,
                    fullWidth = Text.CalcSize(name + statsText).x
                };
            }
            Text.Font = previousFont;
        }

        // Bounds-checked read, so an unexpected tech level cannot throw mid-draw.
        private bool TryGetTechLevelStats(TechLevel techLevel,
            out (int completed, int total, float remainingCost, float spentCost) stats)
        {
            int level = (int)techLevel;
            if (cachedTechLevelStats != null && level >= 0 && level < cachedTechLevelStats.Length)
            {
                stats = cachedTechLevelStats[level];
                return true;
            }

            stats = (0, 0, 0f, 0f);
            return false;
        }

        private int cachedTechLevelStatsTick = -1;

        private void RefreshTechLevelStats(int tick)
        {
            if (cachedTechLevelStats != null && tick - cachedTechLevelStatsTick < 60)
                return;
            cachedTechLevelStatsTick = tick;
            RebuildTechLevelStats();
        }

        private string FormatRemainingEta(float remainingCost)
        {
            if (remainingCost <= 0f)
                return "CM_Semi_Random_Research_Complete".Translate();

            float average = CategoryRate("Standard");
            if (average <= 0f)
                return "CM_Semi_Random_Research_ETA_UnknownNoAverage".Translate();

            return ResearchRateTracker.FormatETAUntilComplete(remainingCost / average);
        }

        private static readonly TechLevel[] ProgressTechLevels =
        {
            TechLevel.Neolithic,
            TechLevel.Medieval,
            TechLevel.Industrial,
            TechLevel.Spacer,
            TechLevel.Ultra,
            TechLevel.Archotech
        };

        private static readonly TechLevel[] ProgressTechLevelsWithAnimal =
        {
            TechLevel.Animal,
            TechLevel.Neolithic,
            TechLevel.Medieval,
            TechLevel.Industrial,
            TechLevel.Spacer,
            TechLevel.Ultra,
            TechLevel.Archotech
        };

        private static bool ColorAndGroupByTechLevel =>
            SemiRandomResearchMod.settings == null || SemiRandomResearchMod.settings.colorAndGroupByTechLevel;

        private void DrawTechLevelProgress(Rect rect)
        {
            if (cachedTechLevelStats == null)
                return;

            if (!ColorAndGroupByTechLevel)
            {
                DrawUnifiedResearchProgress(rect);
                return;
            }

            EnsureTechLevelLabels();

            TechLevel[] techLevels = VfeTribalsActive ? ProgressTechLevelsWithAnimal : ProgressTechLevels;
            TechLevel playerTechLevel = Faction.OfPlayer.def.techLevel;
            bool nodeResearchActive = ResearchTabWindowSwitcher.NodeResearchInstalled;
            bool progressionCoreActive = ProgressionCoreActive;
            bool genesisActive = GenesisActive;
            // Node Research (Emergence projects) and Genesis have no percentage threshold, so the marker sits at
            // the end of the current era's segment instead of at Progression Core's percentage.
            bool noPercentageThreshold = nodeResearchActive || genesisActive;
            float requiredProgress = noPercentageThreshold ? 1f : cachedRequiredProgress;
            bool showThresholdMarker = noPercentageThreshold || progressionCoreActive;

            // Labels alternate between two rows so neighbors do not overlap.
            float topLabelY = rect.y - 49f;   // Further row - moved even further away
            float bottomLabelY = rect.y - 27f; // Closer row - moved slightly further from bar
            float labelHeight = 16f;

            float actualBarHeight = 50f;

            Rect barRect = new Rect(rect.x, rect.y, rect.width, actualBarHeight);
            float currentBarX = barRect.x;
            float barWidth = barRect.width;

            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(barRect, new Color(0.1f, 0.1f, 0.1f));
            }

            float totalTechs = 0f;
            for (int i = 0; i < techLevels.Length; i++)
            {
                if (TryGetTechLevelStats(techLevels[i], out var levelStats))
                    totalTechs += levelStats.total;
            }

            if (totalTechs <= 0f)
            {
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            float advancementThresholdX = 0f;
            bool thresholdFound = false;

            int techLevelIndex = 0;

            foreach (TechLevel techLevel in techLevels)
            {
                if (!TryGetTechLevelStats(techLevel, out var stats) || stats.total == 0)
                    continue;

                float segmentWidth = (float)stats.total / totalTechs * barWidth;
                float progress = stats.total > 0 ? (float)stats.completed / stats.total : 0f;

                Rect segmentRect = new Rect(currentBarX, barRect.y, segmentWidth, barRect.height);

                if (IsRepaint)
                {
                    GUI.color = GetTechLevelColor(techLevel);
                    Widgets.DrawBoxSolid(new Rect(segmentRect.x, segmentRect.y, segmentWidth * progress, segmentRect.height), GUI.color);
                    GUI.color = Color.grey;
                    Widgets.DrawBox(segmentRect);
                }

                if (showThresholdMarker && techLevel == playerTechLevel)
                {
                    advancementThresholdX = segmentRect.x + (segmentWidth * requiredProgress);
                    thresholdFound = true;
                }

                bool isTopRow = (techLevelIndex % 2 == 0);
                float labelY = isTopRow ? topLabelY : bottomLabelY;

                float centerX = currentBarX + (segmentWidth / 2);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;

                TechLevelLabel label = cachedTechLevelLabels[(int)techLevel];
                string techLevelName = label.name;
                string statsText = label.stats;

                Rect labelRect = new Rect(
                    centerX - (label.fullWidth / 2),
                    labelY,
                    label.fullWidth,
                    labelHeight
                );
                if (labelRect.x < barRect.x)
                    labelRect.x = barRect.x;
                else if (labelRect.xMax > barRect.xMax)
                    labelRect.x = barRect.xMax - labelRect.width;

                if (IsRepaint)
                {
                    Color lineColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                    Vector2 lineStart = new Vector2(centerX, labelY + labelHeight);
                    Vector2 lineEnd = new Vector2(centerX, barRect.y - 1);
                    Widgets.DrawLine(lineStart, lineEnd, lineColor, 1f);
                    Widgets.DrawBoxSolid(labelRect.ExpandedBy(3f), new Color(0.1f, 0.1f, 0.1f, 0.7f));
                }

                GUI.color = GetTechLevelColor(techLevel);
                Rect techNameRect = new Rect(labelRect);
                techNameRect.width = label.nameWidth;
                Widgets.Label(techNameRect, techLevelName);

                GUI.color = new Color(0.95f, 0.95f, 0.95f);
                Rect statsRect = new Rect(
                    techNameRect.xMax,
                    labelRect.y,
                    labelRect.xMax - techNameRect.xMax,
                    labelHeight
                );
                Widgets.Label(statsRect, statsText);

                if (Mouse.IsOver(segmentRect))
                {
                    // Stable id, so the changing rate in the text does not restart the tooltip's fade.
                    TechLevel tooltipTechLevel = techLevel;
                    TooltipHandler.TipRegion(segmentRect,
                        () => BuildEraTooltip(tooltipTechLevel),
                        EraTooltipIdBase + (int)tooltipTechLevel);
                }

                currentBarX += segmentWidth;
                techLevelIndex++;
            }

            if (thresholdFound)
            {
                float lineExtension = 20f;
                float arrowSize = 5f;
                Color thresholdColor = Color.white;

                if (IsRepaint)
                {
                    Widgets.DrawLine(
                        new Vector2(advancementThresholdX, barRect.y - 2f),
                        new Vector2(advancementThresholdX, barRect.yMax + lineExtension),
                        thresholdColor,
                        2f);

                    Vector2 arrowBase = new Vector2(advancementThresholdX, barRect.yMax + lineExtension);
                    Vector2 arrowLeft = new Vector2(advancementThresholdX - arrowSize, barRect.yMax + lineExtension - arrowSize);
                    Vector2 arrowRight = new Vector2(advancementThresholdX + arrowSize, barRect.yMax + lineExtension - arrowSize);
                    Widgets.DrawLine(arrowBase, arrowLeft, thresholdColor, 2f);
                    Widgets.DrawLine(arrowBase, arrowRight, thresholdColor, 2f);
                }

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = Color.white;

                TechLevel currentTechLevel = playerTechLevel;
                float progress = 0f;
                if (TryGetTechLevelStats(currentTechLevel, out var currentStats) && currentStats.total > 0)
                    progress = (float)currentStats.completed / currentStats.total;

                // Node Research and Genesis have no percentage threshold, so only the line and arrow are drawn.
                if (!noPercentageThreshold)
                {
                    string thresholdLabel = progress >= requiredProgress
                        ? "CM_Semi_Random_Research_ReadyToAdvance".Translate()
                        : "CM_Semi_Random_Research_AdvanceTechLevel".Translate();

                    // Sized to the label and clamped inside the bar.
                    Vector2 thresholdLabelSize = Text.CalcSize(thresholdLabel);
                    float thresholdLabelWidth = thresholdLabelSize.x + 4f;
                    Rect labelRect = new Rect(
                        advancementThresholdX - (thresholdLabelWidth / 2f),
                        barRect.yMax + lineExtension + 2f,
                        thresholdLabelWidth,
                        thresholdLabelSize.y);
                    if (labelRect.x < barRect.x)
                        labelRect.x = barRect.x;
                    else if (labelRect.xMax > barRect.xMax)
                        labelRect.x = barRect.xMax - labelRect.width;

                    Widgets.Label(labelRect, thresholdLabel);
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        // Tooltip ids: era bars offset by tech level, the unified bar takes the last slot.
        private const int EraTooltipIdBase = 0x5252_0100;
        private const int UnifiedTooltipId = EraTooltipIdBase + 64;

        private string BuildEraTooltip(TechLevel techLevel)
        {
            if (!TryGetTechLevelStats(techLevel, out var stats) || stats.total == 0)
                return string.Empty;

            float progress = (float)stats.completed / stats.total;
            string tooltip = "CM_Semi_Random_Research_EraProgressTooltip".Translate(
                techLevel.ToStringHuman().CapitalizeFirst(),
                stats.completed,
                stats.total,
                (progress * 100f).ToString("F0"),
                stats.spentCost.ToString("N0"),
                (stats.spentCost + stats.remainingCost).ToString("N0"),
                FormatRemainingEta(stats.remainingCost));

            if (techLevel == Faction.OfPlayer.def.techLevel && ResearchTabWindowSwitcher.NodeResearchInstalled)
            {
                // Under Node Research the era ends at its Emergence project, not at a percentage.
                tooltip += "\n\n" + "CM_Semi_Random_Research_NodeResearchTechLimit".Translate();
            }
            else if (GenesisActive && techLevel == Faction.OfPlayer.def.techLevel)
            {
                // Genesis has no percentage threshold either.
                tooltip += "\n\n" + "CM_Semi_Random_Research_GenesisTechLimit".Translate();
            }
            else if (ProgressionCoreActive && techLevel == Faction.OfPlayer.def.techLevel)
            {
                float requiredProgress = cachedRequiredProgress;
                tooltip += "\n\n" + "CM_Semi_Random_Research_ProgressionCoreTooltip".Translate(
                    (progress * 100f).ToString("F0"),
                    (requiredProgress * 100f).ToString("F0"));

                if (progress >= requiredProgress)
                {
                    tooltip += "\n" + "CM_Semi_Random_Research_ProgressionCoreReady".Translate();
                }
                else
                {
                    int remaining = (int)((requiredProgress * stats.total) - stats.completed + 0.999f);
                    tooltip += "\n" + "CM_Semi_Random_Research_ProgressionCoreNeedMore".Translate(remaining);
                }
            }

            return tooltip;
        }

        private string BuildUnifiedTooltip()
        {
            if (cachedTechLevelStats == null)
                return string.Empty;

            SumAllTechLevels(out int completed, out int total, out float remainingCost, out float spentCost);
            if (total <= 0)
                return string.Empty;

            return "CM_Semi_Random_Research_UnifiedProgressTooltip".Translate(
                completed,
                total,
                ((float)completed / total * 100f).ToString("F0"),
                spentCost.ToString("N0"),
                (spentCost + remainingCost).ToString("N0"),
                FormatRemainingEta(remainingCost));
        }

        private void SumAllTechLevels(out int completed, out int total, out float remainingCost, out float spentCost)
        {
            completed = 0;
            total = 0;
            remainingCost = 0f;
            spentCost = 0f;
            if (cachedTechLevelStats == null)
                return;

            for (int i = 0; i < cachedTechLevelStats.Length; i++)
            {
                var stats = cachedTechLevelStats[i];
                completed += stats.completed;
                total += stats.total;
                remainingCost += stats.remainingCost;
                spentCost += stats.spentCost;
            }
        }

        private float GetRequiredProgressionPercent()
        {
            try
            {
                System.Type settingsType = GenTypes.GetTypeInAnyAssembly("ProgressionCore.ProgressionCoreSettings");
                if (settingsType != null)
                {
                    var field = settingsType.GetField("researchComplectionPercent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (field != null)
                    {
                        return (float)field.GetValue(null);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("[CM_Semi_Random_Research] Error accessing ProgressionCore settings: " + ex.Message);
            }

            return 1.0f;
        }

        private void DrawUnifiedResearchProgress(Rect rect)
        {
            SumAllTechLevels(out int completed, out int total, out float remainingCost, out float spentCost);

            if (total <= 0)
            {
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            float progress = (float)completed / total;
            float actualBarHeight = 50f;
            Rect barRect = new Rect(rect.x, rect.y, rect.width, actualBarHeight);

            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(barRect, new Color(0.1f, 0.1f, 0.1f));
                Color fillColor = Color.Lerp(TexUI.AvailResearchColor, Color.white, 0.45f);
                Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, barRect.width * progress, barRect.height), fillColor);
                GUI.color = Color.grey;
                Widgets.DrawBox(barRect);
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            string label = "CM_Semi_Random_Research_ProgressTotal".Translate(completed, total);
            Vector2 labelSize = Text.CalcSize(label);
            Rect labelRect = new Rect(
                barRect.center.x - (labelSize.x / 2f),
                barRect.y - 27f,
                labelSize.x,
                16f);

            if (IsRepaint)
            {
                Color lineColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                Vector2 lineStart = new Vector2(barRect.center.x, labelRect.yMax);
                Vector2 lineEnd = new Vector2(barRect.center.x, barRect.y - 1f);
                Widgets.DrawLine(lineStart, lineEnd, lineColor, 1f);
                Widgets.DrawBoxSolid(labelRect.ExpandedBy(3f), new Color(0.1f, 0.1f, 0.1f, 0.7f));
            }

            GUI.color = new Color(0.95f, 0.95f, 0.95f);
            Widgets.Label(labelRect, label);

            // Stable id, see BuildEraTooltip: the rate inside this text changes while hovering.
            if (Mouse.IsOver(barRect))
                TooltipHandler.TipRegion(barRect, BuildUnifiedTooltip, UnifiedTooltipId);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}

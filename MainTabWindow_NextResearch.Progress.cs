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
    public partial class MainTabWindow_NextResearch
    {
        private static bool? progressionCoreActiveCached;

        private static bool ProgressionCoreActive
        {
            get
            {
                if (progressionCoreActiveCached == null)
                    progressionCoreActiveCached = GenTypes.GetTypeInAnyAssembly("ProgressionCore.ProgressionCoreMod") != null;
                return progressionCoreActiveCached.Value;
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

        private void RebuildTechLevelStats()
        {
            cachedTechLevelStats = new Dictionary<TechLevel, (int, int)>
            {
                { TechLevel.Neolithic, (0, 0) },
                { TechLevel.Medieval, (0, 0) },
                { TechLevel.Industrial, (0, 0) },
                { TechLevel.Spacer, (0, 0) },
                { TechLevel.Ultra, (0, 0) },
                { TechLevel.Archotech, (0, 0) }
            };
            List<ResearchProjectDef> allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ResearchProjectDef def = allDefs[i];
                if (!cachedTechLevelStats.TryGetValue(def.techLevel, out var stats))
                    stats = (0, 0);
                stats.total++;
                if (def.IsFinished)
                    stats.completed++;
                cachedTechLevelStats[def.techLevel] = stats;
            }
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

        private void DrawTechLevelProgress(Rect rect)
        {
            if (cachedTechLevelStats == null)
                return;

            TechLevel[] techLevels = ProgressTechLevels;
            bool progressionCoreActive = ProgressionCoreActive;
            float requiredProgress = cachedRequiredProgress;

            // Two rows for labels with staggered positioning - more brick wall like
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

            Dictionary<TechLevel, (int completed, int total)> techLevelStats = cachedTechLevelStats;
            float totalTechs = 0f;
            for (int i = 0; i < techLevels.Length; i++)
            {
                if (techLevelStats.TryGetValue(techLevels[i], out var levelStats))
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
                var stats = techLevelStats[techLevel];
                if (stats.total == 0) continue;

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

                if (progressionCoreActive && techLevel == Faction.OfPlayer.def.techLevel)
                {
                    advancementThresholdX = segmentRect.x + (segmentWidth * requiredProgress);
                    thresholdFound = true;
                }

                bool isTopRow = (techLevelIndex % 2 == 0);
                float labelY = isTopRow ? topLabelY : bottomLabelY;

                float centerX = currentBarX + (segmentWidth / 2);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;

                string techLevelName = techLevel.ToStringHuman().CapitalizeFirst();
                string statsText = $" ({stats.completed}/{stats.total})";
                string fullLabel = techLevelName + statsText;
                Vector2 labelSize = Text.CalcSize(fullLabel);

                Rect labelRect = new Rect(
                    centerX - (labelSize.x / 2),
                    labelY,
                    labelSize.x,
                    labelHeight
                );

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
                techNameRect.width = Text.CalcSize(techLevelName).x;
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
                    string tooltip = $"{techLevel.ToStringHuman().CapitalizeFirst()}\n{stats.completed}/{stats.total} ({(progress * 100f):F0}%)";

                    // Add ProgressionCore info to tooltip
                    if (progressionCoreActive && techLevel == Faction.OfPlayer.def.techLevel)
                    {
                        tooltip += $"\n\nProgression Core: {(progress * 100f):F0}% of {(requiredProgress * 100f):F0}% required to advance";

                        if (progress >= requiredProgress)
                        {
                            tooltip += "\nReady to advance to next tech level!";
                        }
                        else
                        {
                            int remaining = (int)((requiredProgress * stats.total) - stats.completed + 0.999f);
                            tooltip += $"\nNeed {remaining} more research project(s)";
                        }
                    }

                    TooltipHandler.TipRegion(segmentRect, tooltip);
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

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                Rect labelRect = new Rect(advancementThresholdX - 60f, barRect.yMax + lineExtension + 4f, 120f, 20f);
                GUI.color = Color.white;

                TechLevel currentTechLevel = Faction.OfPlayer.def.techLevel;
                var stats = techLevelStats[currentTechLevel];
                float progress = stats.total > 0 ? (float)stats.completed / stats.total : 0f;

                string thresholdLabel = progress >= requiredProgress
                    ? "Ready to Advance!"
                    : "Advance Tech Level";

                Widgets.Label(labelRect, thresholdLabel);
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private float GetRequiredProgressionPercent()
        {
            try
            {
                // Use reflection to access the settings
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

            // Default value if we can't access the setting
            return 1.0f;
        }
    }
}

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
        private void DrawResearchRateUI(Rect rect, ResearchProjectDef project, bool isMainProject, float costColumnWidth)
        {
            if (project == null) return;

            Color originalColor = GUI.color;
            TextAnchor originalAnchor = Text.Anchor;

            ResearchRateTracker rateTracker = Current.Game.World.GetComponent<ResearchRateTracker>();
            if (rateTracker == null) return;

            ResearchRateInfo rateInfo = rateTracker.GetResearchRateInfo(project);
            bool hasRateData = rateInfo.TotalSamples > 0;
            float globalAverageRate = rateTracker.GetGlobalAverageRate();
            bool hasGlobalData = globalAverageRate > 0;

            float graphPadding = 6f;
            float sectionSpacing = 16f;
            float currentY = rect.y;

            Text.Font = GameFont.Small;
            float headerHeight = 48f;
            Rect headerRect = new Rect(rect.x, currentY, rect.width, headerHeight);

            float separatorWidth = 1f;
            string progressText = GetProjectCostText(project);

            CardRowLayout layout = ComputeCardRowLayout(headerRect, headerHeight, costColumnWidth);
            Rect iconRect = layout.IconRect;
            Rect firstSeparator = layout.FirstSeparator;
            Rect secondSeparator = layout.SecondSeparator;
            Rect nameRect = layout.NameRect;
            Rect costRect = layout.CostRect;

            Color techColor = GetCategoryColor(project);

            Color backgroundColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
            Widgets.DrawBoxSolid(headerRect, backgroundColor);

            Rect progressRect = new Rect(headerRect.x, headerRect.y, headerRect.width * project.ProgressPercent, headerRect.height);
            Color progressColor = techColor;
            progressColor.a = 0.4f;
            Widgets.DrawBoxSolid(progressRect, progressColor);

            Color borderColor = Color.Lerp(techColor, Color.white, 0.5f); // Brighter border for active projects
            float borderWidth = 1f;
            Widgets.DrawLine(new Vector2(headerRect.x, headerRect.y), new Vector2(headerRect.xMax, headerRect.y), borderColor, borderWidth);
            Widgets.DrawLine(new Vector2(headerRect.x, headerRect.yMax), new Vector2(headerRect.xMax, headerRect.yMax), borderColor, borderWidth);
            Widgets.DrawLine(new Vector2(headerRect.x, headerRect.y), new Vector2(headerRect.x, headerRect.yMax), borderColor, borderWidth);
            Widgets.DrawLine(new Vector2(headerRect.xMax, headerRect.y), new Vector2(headerRect.xMax, headerRect.yMax), borderColor, borderWidth);

            if (selectedProject == project)
            {
                DrawTransparentBox(headerRect, borderColor, 10, true);
            }

            Def firstUnlockable = GetFirstUnlockable(project);
            try
            {
                if (firstUnlockable != null)
                    Widgets.DefIcon(iconRect, firstUnlockable);
            }
            catch (Exception) { }

            Widgets.DrawLine(new Vector2(firstSeparator.x, firstSeparator.y), new Vector2(firstSeparator.x, firstSeparator.yMax), borderColor, separatorWidth);
            Widgets.DrawLine(new Vector2(secondSeparator.x, secondSeparator.y), new Vector2(secondSeparator.x, secondSeparator.yMax), borderColor, separatorWidth);

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(nameRect, project.LabelCap);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(costRect, progressText);
            Text.WordWrap = wordWrap;

            if (SemiRandomResearchMod.settings.allowSwitchingResearch)
            {
                float cancelButtonSize = 20f;
                Rect cancelRect = new Rect(headerRect.xMax - cancelButtonSize - 4f, headerRect.y + 4f, cancelButtonSize, cancelButtonSize);

                if (Mouse.IsOver(headerRect) || Mouse.IsOver(cancelRect))
                {
                    GUI.color = new Color(0.9f, 0.3f, 0.3f, 0.8f);
                    Widgets.DrawBoxSolid(cancelRect, GUI.color);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Text.Font = GameFont.Small;
                    Widgets.Label(cancelRect, "Ã—");
                    Text.Anchor = TextAnchor.UpperLeft;

                    if (Widgets.ButtonInvisible(cancelRect))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();

                        ResearchTracker cancelTracker = Current.Game.World.GetComponent<ResearchTracker>();
                        if (cancelTracker != null)
                        {
                            string categoryKey = ResearchTracker.GetCategoryKey(project);
                            cancelTracker.SetCurrentProjectByKey(null, categoryKey);

                            cancelTracker.ForceAutoReseachCheckNextTick();
                            Event.current.Use();
                            return; // Stop drawing to prevent null references
                        }
                    }
                }
            }

            if (Widgets.ButtonInvisible(headerRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                selectedProject = project;
            }

            // ==========================================
            // ACCORDION EXPANSION LOGIC
            // ==========================================
            if (isMainProject)
            {
                currentY += headerHeight + sectionSpacing;

                float statsLineHeight = 38f;
                Rect statsRowRect = new Rect(rect.x, currentY, rect.width, statsLineHeight);
                Widgets.DrawBoxSolid(statsRowRect, new Color(0.1f, 0.1f, 0.1f, 0.2f));

                float sectionWidth = statsRowRect.width / 3;

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;

                Rect currentRateRect = new Rect(statsRowRect.x, statsRowRect.y, sectionWidth, statsLineHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(currentRateRect.x, currentRateRect.y, currentRateRect.width, statsLineHeight / 2), "Current");
                GUI.color = new Color(0.65f, 0.8f, 0.9f);
                string currentRateText = hasRateData ? rateInfo.CurrentRateFormatted.Replace(" research/day", "/d") : "Calculating...";
                float currentTextWidth = Text.CalcSize(currentRateText).x + 8f;
                float currentCenterX = currentRateRect.x + (currentRateRect.width - currentTextWidth) / 2;
                Rect centeredCurrentRect = new Rect(currentCenterX, currentRateRect.y + statsLineHeight / 2, currentTextWidth, statsLineHeight / 2);
                Widgets.Label(centeredCurrentRect, currentRateText);

                Rect avgRateRect = new Rect(statsRowRect.x + sectionWidth, statsRowRect.y, sectionWidth, statsLineHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(avgRateRect.x, avgRateRect.y, avgRateRect.width, statsLineHeight / 2), "10d Avg");
                string averageRateText;
                if (hasRateData) averageRateText = rateInfo.AverageRateFormatted.Replace(" research/day", "/d");
                else if (hasGlobalData) averageRateText = ResearchRateTracker.FormatRate(globalAverageRate).Replace(" research/day", "/d");
                else averageRateText = "0/d";

                GUI.color = new Color(0.8f, 0.8f, 0.6f);
                float avgTextWidth = Text.CalcSize(averageRateText).x + 8f;
                float avgCenterX = avgRateRect.x + (avgRateRect.width - avgTextWidth) / 2;
                Rect centeredAvgRect = new Rect(avgCenterX, avgRateRect.y + statsLineHeight / 2, avgTextWidth, statsLineHeight / 2);
                Widgets.Label(centeredAvgRect, averageRateText);

                Rect etaRect = new Rect(statsRowRect.x + sectionWidth * 2, statsRowRect.y, sectionWidth, statsLineHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(etaRect.x, etaRect.y, etaRect.width, statsLineHeight / 2), "Est. Time");

                string etaText;
                float estimatedDays = -1f;
                if (hasRateData && rateInfo.EstimatedDaysToCompletion >= 0)
                {
                    etaText = rateInfo.ETAFormatted;
                    estimatedDays = rateInfo.EstimatedDaysToCompletion;
                }
                else if (hasGlobalData)
                {
                    float remainingProgress = project.CostApparent - project.ProgressApparent;
                    estimatedDays = remainingProgress / globalAverageRate;
                    etaText = ResearchRateTracker.FormatETA(estimatedDays);
                }
                else etaText = "Unknown";

                Color etaColor = new Color(0.7f, 0.7f, 0.7f);
                if (estimatedDays >= 0)
                {
                    if (estimatedDays < 1f) etaColor = new Color(0.0f, 0.7f, 0.0f);
                    else if (estimatedDays < 3f) etaColor = new Color(0.7f, 0.7f, 0.0f);
                    else if (estimatedDays > 10f) etaColor = new Color(0.75f, 0.5f, 0.3f);
                }

                GUI.color = etaColor;
                float etaTextWidth = Text.CalcSize(etaText).x + 8f;
                float etaCenterX = etaRect.x + (etaRect.width - etaTextWidth) / 2;
                Rect centeredEtaRect = new Rect(etaCenterX, etaRect.y + statsLineHeight / 2, etaTextWidth, statsLineHeight / 2);
                Widgets.Label(centeredEtaRect, etaText);

                currentY += statsLineHeight + sectionSpacing;

                if (SemiRandomResearchMod.settings.showResearchRateGraph && (hasRateData || hasGlobalData))
                {
                    float graphHeight = 140f;

                    if (rect.yMax < currentY + graphHeight + 10f)
                    {
                        float additionalHeightNeeded = (currentY + graphHeight + 10f) - rect.yMax;
                        rect.height += additionalHeightNeeded;
                    }

                    Rect graphRect = new Rect(rect.x + graphPadding, currentY, rect.width - (graphPadding * 2), graphHeight);

                    Widgets.DrawBoxSolid(graphRect, new Color(0.1f, 0.1f, 0.1f, 0.2f));
                    DrawTransparentBox(graphRect, new Color(0.4f, 0.4f, 0.4f, 0.3f), 1f);

                    List<float> samplesForGraph = hasRateData ?
                        rateTracker.GetRateSamplesPeriod(project, 3) :
                        rateTracker.GetGlobalRateSamplesPeriod(3);

                    if (samplesForGraph.Count > 0)
                    {
                        DrawRateGraph(graphRect, samplesForGraph, rateTracker.GetAverageRate(project));
                    }
                    else
                    {
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
                        Widgets.Label(graphRect, "Collecting Data...");
                    }
                }
            }

            Text.Anchor = originalAnchor;
            GUI.color = originalColor;
        }

        private void DrawRateGraph(Rect rect, List<float> samples, float averageRate)
        {
            if (samples.Count == 0) return;

            float padding = 10f;
            Rect graphAreaRect = rect.ContractedBy(padding);

            float maxValue = samples.Max() * 1.2f; // Add 20% headroom
            maxValue = Mathf.Max(maxValue, 0.1f); // Ensure we have a non-zero scale

            float barWidth = graphAreaRect.width / Mathf.Max(samples.Count, 1);

            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Widgets.DrawLine(
                new Vector2(graphAreaRect.x, graphAreaRect.yMax),
                new Vector2(graphAreaRect.xMax, graphAreaRect.yMax),
                GUI.color,
                1f);

            for (int i = 0; i < samples.Count; i++)
            {
                float normalizedValue = samples[i] / maxValue; // Scale to 0-1

                float barHeight = normalizedValue * graphAreaRect.height;
                Rect barRect = new Rect(
                    graphAreaRect.x + (i * barWidth),
                    graphAreaRect.yMax - barHeight,
                    barWidth, // No spacing between bars
                    barHeight
                );

                Color barColor = Color.Lerp(
                    new Color(0.4f, 0.5f, 0.6f), // Desaturated blue-gray for lower values
                    new Color(0.5f, 0.6f, 0.4f), // Desaturated sage green for higher values
                    normalizedValue
                );

                GUI.color = barColor;
                Widgets.DrawBoxSolid(barRect, barColor);
            }

            if (averageRate > 0)
            {
                if (averageRate <= maxValue)
                {
                    float avgY = graphAreaRect.yMax - (averageRate / maxValue * graphAreaRect.height);

                    GUI.color = new Color(0.7f, 0.65f, 0.45f, 0.8f);

                    Widgets.DrawLine(
                        new Vector2(graphAreaRect.x, avgY),
                        new Vector2(graphAreaRect.xMax, avgY),
                        GUI.color,
                        2f);

                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(
                        new Rect(graphAreaRect.x, avgY - 10f, graphAreaRect.width - 5f, 14f),
                        "Average");
                }
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}

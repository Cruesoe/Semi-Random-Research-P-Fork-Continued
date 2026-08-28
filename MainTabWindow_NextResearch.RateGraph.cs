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
        private static readonly Color PausedTextColor = new Color(0.62f, 0.62f, 0.64f);
        private static readonly Color PausedTagColor = new Color(0.82f, 0.70f, 0.36f);

        // Paused means the tracker still holds the project as current while nothing is being
        // researched on it. Checked per project so pausing standard research does not grey out
        // an anomaly or gravship project that is still running.
        private bool IsProjectPaused(ResearchProjectDef project)
        {
            return project != null
                && drawTracker != null
                && drawTracker.ResearchPaused
                && !Find.ResearchManager.IsCurrentProject(project);
        }

        // Drain the colour rather than just darkening it, so a paused card reads as inactive
        // at a glance next to the coloured offer cards below it.
        private static Color PausedTint(Color color)
        {
            float grey = color.grayscale;
            Color desaturated = new Color(grey, grey, grey, color.a);
            return Color.Lerp(desaturated, new Color(0.30f, 0.30f, 0.32f, color.a), 0.45f);
        }

        private void DrawResearchRateUI(Rect rect, ResearchProjectDef project, bool isMainProject, float costColumnWidth)
        {
            if (project == null) return;

            Color originalColor = GUI.color;
            TextAnchor originalAnchor = Text.Anchor;

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

            bool isPaused = IsProjectPaused(project);

            Color techColor = GetCategoryColor(project);
            Color structureAccent = GetCardStructureAccent(project, techColor);
            if (isPaused)
            {
                techColor = PausedTint(techColor);
                structureAccent = PausedTint(structureAccent);
            }

            Color backgroundColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
            if (isPaused)
                backgroundColor = PausedTint(backgroundColor);
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(headerRect, backgroundColor);
            }

            Rect progressRect = new Rect(headerRect.x, headerRect.y, headerRect.width * project.ProgressPercent, headerRect.height);
            Color progressColor = isPaused ? new Color(0.45f, 0.45f, 0.47f) : GetProgressFillAccent(project, techColor);
            progressColor.a = 0.55f;
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(progressRect, progressColor);
            }

            Color borderColor = Color.Lerp(structureAccent, Color.white, 0.35f);
            if (IsRepaint)
            {
                GUI.color = borderColor;
                Widgets.DrawBox(headerRect);
                GUI.color = Color.white;
            }

            if (selectedProject == project && IsRepaint)
            {
                DrawTransparentBox(headerRect, borderColor, 2f);
            }

            if (IsRepaint)
            {
                Def firstUnlockable = GetFirstUnlockable(project);
                if (firstUnlockable != null)
                {
                    try
                    {
                        Widgets.DefIcon(iconRect, firstUnlockable);
                    }
                    catch (Exception)
                    {
                    }
                }

                Widgets.DrawLine(new Vector2(firstSeparator.x, firstSeparator.y), new Vector2(firstSeparator.x, firstSeparator.yMax), borderColor, separatorWidth);
                Widgets.DrawLine(new Vector2(secondSeparator.x, secondSeparator.y), new Vector2(secondSeparator.x, secondSeparator.yMax), borderColor, separatorWidth);
            }

            if (isPaused)
            {
                // Name moves up a line so the paused tag can sit under it, the same two line
                // layout the offer cards use for their Foundation / Emergence tags.
                Rect pausedNameRect = new Rect(nameRect.x, nameRect.y + 2f, nameRect.width, 24f);
                Rect pausedTagRect = new Rect(nameRect.x, nameRect.y + 24f, nameRect.width, 20f);

                Text.Anchor = TextAnchor.LowerLeft;
                GUI.color = PausedTextColor;
                Widgets.Label(pausedNameRect, SafeLabel(project));

                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                GUI.color = PausedTagColor;
                Widgets.Label(pausedTagRect, "CM_Semi_Random_Research_PausedTag".Translate());
                Text.Font = GameFont.Small;
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.white;
                Widgets.Label(nameRect, SafeLabel(project));
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = isPaused ? PausedTextColor : new Color(1f, 1f, 1f, 0.8f);
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(costRect, progressText);
            Text.WordWrap = wordWrap;

            if (SemiRandomResearchMod.settings.allowSwitchingResearch)
            {
                float cancelButtonSize = 20f;
                Rect cancelRect = new Rect(headerRect.xMax - cancelButtonSize - 4f, headerRect.y + 4f, cancelButtonSize, cancelButtonSize);

                if (IsRepaint && (Mouse.IsOver(headerRect) || Mouse.IsOver(cancelRect)))
                    Widgets.DrawBoxSolid(cancelRect, new Color(0.9f, 0.3f, 0.3f, 0.8f));

                if (Clicked(cancelRect))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();

                    ResearchTracker cancelTracker = cachedTracker ?? Current.Game.World.GetComponent<ResearchTracker>();
                    if (cancelTracker != null)
                    {
                        string categoryKey = ResearchTracker.GetCategoryKey(project);
                        cancelTracker.SetCurrentProjectByKey(null, categoryKey);

                        cancelTracker.ForceAutoReseachCheckNextTick();
                        InvalidateLeftColumnCache();
                        Event.current.Use();
                    }
                }
            }

            if (Clicked(headerRect))
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
                Widgets.Label(new Rect(currentRateRect.x, currentRateRect.y, currentRateRect.width, statsLineHeight / 2), "CM_Semi_Random_Research_Current".Translate());
                GUI.color = new Color(0.65f, 0.8f, 0.9f);
                float currentTextWidth = Text.CalcSize(cachedCurrentRateText).x + 8f;
                float currentCenterX = currentRateRect.x + (currentRateRect.width - currentTextWidth) / 2;
                Widgets.Label(new Rect(currentCenterX, currentRateRect.y + statsLineHeight / 2, currentTextWidth, statsLineHeight / 2), cachedCurrentRateText);

                Rect avgRateRect = new Rect(statsRowRect.x + sectionWidth, statsRowRect.y, sectionWidth, statsLineHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(avgRateRect.x, avgRateRect.y, avgRateRect.width, statsLineHeight / 2), "CM_Semi_Random_Research_TenDayAvg".Translate());
                GUI.color = new Color(0.8f, 0.8f, 0.6f);
                float avgTextWidth = Text.CalcSize(cachedAvgRateText).x + 8f;
                float avgCenterX = avgRateRect.x + (avgRateRect.width - avgTextWidth) / 2;
                Widgets.Label(new Rect(avgCenterX, avgRateRect.y + statsLineHeight / 2, avgTextWidth, statsLineHeight / 2), cachedAvgRateText);

                Rect etaRect = new Rect(statsRowRect.x + sectionWidth * 2, statsRowRect.y, sectionWidth, statsLineHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(etaRect.x, etaRect.y, etaRect.width, statsLineHeight / 2), "CM_Semi_Random_Research_EstTime".Translate());
                // A countdown that is not counting down is worse than no countdown.
                string etaText = isPaused ? "CM_Semi_Random_Research_PausedTag".Translate().ToString() : cachedEtaText;
                GUI.color = isPaused ? PausedTagColor : cachedEtaColor;
                float etaTextWidth = Text.CalcSize(etaText).x + 8f;
                float etaCenterX = etaRect.x + (etaRect.width - etaTextWidth) / 2;
                Widgets.Label(new Rect(etaCenterX, etaRect.y + statsLineHeight / 2, etaTextWidth, statsLineHeight / 2), etaText);

                currentY += statsLineHeight + sectionSpacing;

                if (SemiRandomResearchMod.settings.showResearchRateGraph)
                {
                    float graphHeight = 140f;
                    Rect graphRect = new Rect(rect.x + graphPadding, currentY, rect.width - (graphPadding * 2), graphHeight);
                    if (IsRepaint)
                    {
                        Widgets.DrawBoxSolid(graphRect, new Color(0.1f, 0.1f, 0.1f, 0.2f));
                        DrawTransparentBox(graphRect, new Color(0.4f, 0.4f, 0.4f, 0.3f), 1f);
                        if (cachedGraphSamples != null && cachedGraphSamples.Count > 0)
                        {
                            DrawRateGraph(graphRect, cachedGraphSamples, cachedGraphAverage);
                        }
                        else
                        {
                            Text.Anchor = TextAnchor.MiddleCenter;
                            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
                            Widgets.Label(graphRect, "CM_Semi_Random_Research_CollectingData".Translate());
                        }
                    }
                }
            }

            Text.Anchor = originalAnchor;
            GUI.color = originalColor;
        }

        private void DrawRateGraph(Rect rect, List<float> samples, float averageRate)
        {
            if (samples == null || samples.Count == 0)
                return;

            float padding = 10f;
            Rect graphAreaRect = rect.ContractedBy(padding);

            float maxValue = 0.1f;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i] > maxValue)
                    maxValue = samples[i];
            }
            maxValue *= 1.2f;

            float barWidth = graphAreaRect.width / samples.Count;

            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Widgets.DrawLine(
                new Vector2(graphAreaRect.x, graphAreaRect.yMax),
                new Vector2(graphAreaRect.xMax, graphAreaRect.yMax),
                GUI.color,
                1f);

            for (int i = 0; i < samples.Count; i++)
            {
                float normalizedValue = Mathf.Clamp01(samples[i] / maxValue);
                float barHeight = normalizedValue * graphAreaRect.height;
                Rect barRect = new Rect(
                    graphAreaRect.x + (i * barWidth),
                    graphAreaRect.yMax - barHeight,
                    barWidth,
                    barHeight);

                Color barColor = Color.Lerp(
                    new Color(0.4f, 0.5f, 0.6f),
                    new Color(0.5f, 0.6f, 0.4f),
                    normalizedValue);
                Widgets.DrawBoxSolid(barRect, barColor);
            }

            if (averageRate > 0f && averageRate <= maxValue)
            {
                float avgY = graphAreaRect.yMax - (averageRate / maxValue * graphAreaRect.height);
                Color avgColor = new Color(0.7f, 0.65f, 0.45f, 0.8f);

                Text.Font = GameFont.Tiny;
                string avgLabel = "CM_Semi_Random_Research_GraphAverage".Translate();
                Vector2 avgLabelSize = Text.CalcSize(avgLabel);
                float labelWidth = avgLabelSize.x + 6f;
                float labelHeight = Mathf.Max(14f, avgLabelSize.y);
                Rect labelRect = new Rect(
                    graphAreaRect.xMax - labelWidth,
                    avgY - labelHeight / 2f,
                    labelWidth,
                    labelHeight);
                if (labelRect.y < graphAreaRect.y)
                    labelRect.y = graphAreaRect.y;
                if (labelRect.yMax > graphAreaRect.yMax)
                    labelRect.y = graphAreaRect.yMax - labelHeight;

                GUI.color = avgColor;
                Widgets.DrawLine(
                    new Vector2(graphAreaRect.x, avgY),
                    new Vector2(labelRect.x - 4f, avgY),
                    avgColor,
                    2f);

                Widgets.DrawBoxSolid(labelRect, new Color(0.08f, 0.08f, 0.08f, 0.85f));
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = avgColor;
                Widgets.Label(labelRect, avgLabel);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}

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
            Color structureAccent = GetCardStructureAccent(project, techColor);

            Color backgroundColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(headerRect, backgroundColor);
            }

            Rect progressRect = new Rect(headerRect.x, headerRect.y, headerRect.width * project.ProgressPercent, headerRect.height);
            Color progressColor = GetProgressFillAccent(project, techColor);
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

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(nameRect, SafeLabel(project));

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
                Widgets.Label(new Rect(currentRateRect.x, currentRateRect.y, currentRateRect.width, statsLineHeight / 2), "Current");
                GUI.color = new Color(0.65f, 0.8f, 0.9f);
                float currentTextWidth = Text.CalcSize(cachedCurrentRateText).x + 8f;
                float currentCenterX = currentRateRect.x + (currentRateRect.width - currentTextWidth) / 2;
                Widgets.Label(new Rect(currentCenterX, currentRateRect.y + statsLineHeight / 2, currentTextWidth, statsLineHeight / 2), cachedCurrentRateText);

                Rect avgRateRect = new Rect(statsRowRect.x + sectionWidth, statsRowRect.y, sectionWidth, statsLineHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(avgRateRect.x, avgRateRect.y, avgRateRect.width, statsLineHeight / 2), "10d Avg");
                GUI.color = new Color(0.8f, 0.8f, 0.6f);
                float avgTextWidth = Text.CalcSize(cachedAvgRateText).x + 8f;
                float avgCenterX = avgRateRect.x + (avgRateRect.width - avgTextWidth) / 2;
                Widgets.Label(new Rect(avgCenterX, avgRateRect.y + statsLineHeight / 2, avgTextWidth, statsLineHeight / 2), cachedAvgRateText);

                Rect etaRect = new Rect(statsRowRect.x + sectionWidth * 2, statsRowRect.y, sectionWidth, statsLineHeight);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(new Rect(etaRect.x, etaRect.y, etaRect.width, statsLineHeight / 2), "Est. Time");
                GUI.color = cachedEtaColor;
                float etaTextWidth = Text.CalcSize(cachedEtaText).x + 8f;
                float etaCenterX = etaRect.x + (etaRect.width - etaTextWidth) / 2;
                Widgets.Label(new Rect(etaCenterX, etaRect.y + statsLineHeight / 2, etaTextWidth, statsLineHeight / 2), cachedEtaText);

                currentY += statsLineHeight + sectionSpacing;

                if (SemiRandomResearchMod.settings.showResearchRateGraph)
                {
                    float graphHeight = 140f;
                    Rect graphRect = new Rect(rect.x + graphPadding, currentY, rect.width - (graphPadding * 2), graphHeight);
                    if (IsRepaint)
                    {
                        Widgets.DrawBoxSolid(graphRect, new Color(0.1f, 0.1f, 0.1f, 0.2f));
                    }
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                    Widgets.Label(graphRect, cachedCurrentRateText + "  ·  " + cachedAvgRateText);
                }
            }

            Text.Anchor = originalAnchor;
            GUI.color = originalColor;
        }
    }
}

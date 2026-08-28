using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // The history view replaces the left column with the most recently finished projects.
    // It is deliberately a snapshot: no scrolling, no grouping, no headers - just as many
    // cards as fit, so a player who missed the completion letter can still see what landed.
    public partial class MainTabWindow_NextResearch
    {
        private const float HistoryCardHeight = 48f;
        private const float HistoryCardGap = 12f;

        private bool showingHistory;
        private ResearchProjectDef selectionBeforeHistory;

        private void OpenHistory()
        {
            showingHistory = true;
            selectionBeforeHistory = selectedProject;

            List<ResearchHistoryEntry> history = cachedTracker?.CompletedHistory;
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                    CacheFirstUnlockable(history[i]?.project);

                if (history.Count > 0 && history[0]?.project != null)
                    selectedProject = history[0].project;
            }

            WarmSelectedUnlocks();
            RecacheMatchingBenchIfNeeded();
        }

        private void CloseHistory()
        {
            showingHistory = false;

            if (selectionBeforeHistory != null && !selectionBeforeHistory.IsFinished)
                selectedProject = selectionBeforeHistory;
            else
                SelectDefaultProject();

            selectionBeforeHistory = null;
            cachedCanStartNowTick = -1;
            RefreshCanStartNow(Find.TickManager.TicksGame);
        }

        private void DrawHistoryColumn(Rect leftRect)
        {
            List<ResearchHistoryEntry> history = drawTracker?.CompletedHistory;

            float footerPaddingTop = 12f;
            float footerHeight = 40f;
            float footerPaddingBottom = 12f;
            float totalFooterHeight = footerPaddingTop + footerHeight + footerPaddingBottom;

            GUI.BeginGroup(leftRect);
            try
            {
                float currentY = 0f;
                float headerHeight = 40f;

                Text.Font = GameFont.Medium;
                GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
                Widgets.Label(new Rect(0f, currentY, leftRect.width, headerHeight), "CM_Semi_Random_Research_HistoryHeader".Translate());
                GenUI.ResetLabelAlign();
                Text.Font = GameFont.Small;
                currentY += headerHeight + 4f;

                float listHeight = leftRect.height - totalFooterHeight - currentY;
                float listWidth = leftRect.width;

                if (history == null || history.Count == 0)
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(new Rect(0f, currentY, listWidth, 30f), "CM_Semi_Random_Research_HistoryEmpty".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    // Only as many cards as fit: this is a glance at what just finished, not an archive.
                    int maxCards = Mathf.FloorToInt((listHeight + HistoryCardGap) / (HistoryCardHeight + HistoryCardGap));
                    int shown = Mathf.Clamp(maxCards, 0, history.Count);

                    float costColumnWidth = MeasureHistoryCostWidth(history, shown);

                    for (int i = 0; i < shown; i++)
                    {
                        ResearchHistoryEntry entry = history[i];
                        if (entry?.project == null)
                            continue;

                        // Covers projects finished while this view is already open.
                        CacheFirstUnlockable(entry.project);

                        Rect cardRect = new Rect(0f, currentY, listWidth, HistoryCardHeight);
                        DrawHistoryCard(cardRect, entry, costColumnWidth);
                        currentY += HistoryCardHeight + HistoryCardGap;
                    }
                }
            }
            finally
            {
                GUI.EndGroup();
            }

            DrawHistoryFooter(leftRect, totalFooterHeight, footerPaddingTop, footerHeight);
        }

        private static float MeasureHistoryCostWidth(List<ResearchHistoryEntry> history, int count)
        {
            Text.Font = GameFont.Small;
            float width = 8f;
            for (int i = 0; i < count && i < history.Count; i++)
            {
                ResearchProjectDef project = history[i]?.project;
                if (project == null)
                    continue;

                width = Mathf.Max(width, Text.CalcSize(project.CostApparent.ToString("N0")).x + 12f);
            }
            return width;
        }

        private void DrawHistoryFooter(Rect leftRect, float totalFooterHeight, float footerPaddingTop, float footerHeight)
        {
            Text.Font = GameFont.Small;

            Rect footerContainerRect = new Rect(leftRect.x, leftRect.yMax - totalFooterHeight, leftRect.width, totalFooterHeight);

            if (IsRepaint)
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                Widgets.DrawLineHorizontal(footerContainerRect.x, footerContainerRect.y, footerContainerRect.width);
                GUI.color = Color.white;
            }

            float buttonWidth = 120f;
            Rect backButtonRect = new Rect(
                footerContainerRect.x + (footerContainerRect.width - buttonWidth) / 2f,
                footerContainerRect.y + footerPaddingTop,
                buttonWidth,
                footerHeight);

            if (ColoredButtonText(backButtonRect, "CM_Semi_Random_Research_HistoryBack".Translate(), FooterTreeButtonColor))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                CloseHistory();
            }

            if (IsRepaint && Mouse.IsOver(backButtonRect))
                TooltipHandler.TipRegion(backButtonRect, "CM_Semi_Random_Research_HistoryBackTip".Translate());

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        // Same card geometry and colouring as the offer list, minus the reroll animation,
        // the cancel button and the progress fill (everything here is finished).
        private void DrawHistoryCard(Rect drawRect, ResearchHistoryEntry entry, float costColumnWidth)
        {
            ResearchProjectDef projectDef = entry.project;

            Color originalColor = GUI.color;
            TextAnchor startingTextAnchor = Text.Anchor;
            Text.Font = GameFont.Small;

            drawRect.width -= 8f;

            bool isMouseOver = Mouse.IsOver(drawRect);
            bool isSelected = selectedProject == projectDef;

            CardRowLayout layout = ComputeCardRowLayout(drawRect, HistoryCardHeight, costColumnWidth);

            Color techColor = GetCategoryColor(projectDef);
            Color structureAccent = GetCardStructureAccent(projectDef, techColor);
            Color backgroundColor = isMouseOver
                ? Color.Lerp(TexUI.AvailResearchColor, techColor, 0.4f)
                : Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);

            Color borderColor = structureAccent;
            if (isSelected)
                borderColor = Color.Lerp(structureAccent, Color.white, 0.3f);
            else if (isMouseOver)
                borderColor = Color.Lerp(structureAccent, Color.white, 0.2f);

            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(drawRect, backgroundColor);

                if (isMouseOver)
                {
                    Color glowColor = structureAccent;
                    glowColor.a = 0.1f;
                    Widgets.DrawBoxSolid(drawRect.ExpandedBy(2f), glowColor);
                }

                GUI.color = borderColor;
                Widgets.DrawBox(drawRect);
                GUI.color = Color.white;

                Def firstUnlockable = GetFirstUnlockable(projectDef);
                if (firstUnlockable != null)
                {
                    try
                    {
                        Widgets.DefIcon(layout.IconRect, firstUnlockable);
                    }
                    catch (Exception)
                    {
                    }
                }

                Widgets.DrawLine(
                    new Vector2(layout.FirstSeparator.x, layout.FirstSeparator.y),
                    new Vector2(layout.FirstSeparator.x, layout.FirstSeparator.yMax),
                    structureAccent,
                    1f);
                Widgets.DrawLine(
                    new Vector2(layout.SecondSeparator.x, layout.SecondSeparator.y),
                    new Vector2(layout.SecondSeparator.x, layout.SecondSeparator.yMax),
                    structureAccent,
                    1f);
            }

            Rect nameRect = layout.NameRect;
            Rect topTextRect = new Rect(nameRect.x, nameRect.y + 2f, nameRect.width, 24f);
            Rect bottomTextRect = new Rect(nameRect.x, nameRect.y + 24f, nameRect.width, 20f);

            GUI.color = isMouseOver ? Color.white : new Color(0.95f, 0.95f, 0.95f);
            Text.Anchor = TextAnchor.LowerLeft;
            Widgets.Label(topTextRect, SafeLabel(projectDef));

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(bottomTextRect, FormatCompletedAgo(entry.tick));
            Text.Font = GameFont.Small;

            GUI.color = originalColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(layout.CostRect, projectDef.CostApparent.ToString("N0"));
            Text.WordWrap = wordWrap;

            if (isSelected && IsRepaint)
                DrawTransparentBox(drawRect, Color.Lerp(structureAccent, Color.white, 0.3f), 2f);

            if (Clicked(drawRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                selectedProject = projectDef;
                WarmSelectedUnlocks();
                RecacheMatchingBenchIfNeeded();
            }

            if (isMouseOver)
                TooltipHandler.TipRegion(drawRect, SafeLabel(projectDef));

            GUI.color = originalColor;
            Text.Anchor = startingTextAnchor;
        }

        private static string FormatCompletedAgo(int completedTick)
        {
            int ticksSince = Find.TickManager.TicksGame - completedTick;
            if (ticksSince < 0)
                ticksSince = 0;

            if (ticksSince < GenDate.TicksPerHour)
                return "CM_Semi_Random_Research_HistoryJustNow".Translate();

            return "CM_Semi_Random_Research_HistoryAgo".Translate(ticksSince.ToStringTicksToPeriod(false, false, false));
        }
    }
}

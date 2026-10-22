using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    public partial class MainTabWindow_NextResearch
    {
        private const float HistoryCardHeight = 48f;
        private const float HistoryCardGap = 12f;

        private bool showingHistory;
        private bool showingSnoozed;
        private ResearchProjectDef selectionBeforeHistory;

        private void OpenHistory()
        {
            showingHistory = true;
            showingSnoozed = false;
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
            showingSnoozed = false;
            // The offer list was not drawn while history was shown, so its last measurement is stale.
            leftScrollOutHeight = 0f;

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
            if (showingSnoozed)
            {
                DrawSnoozedColumn(leftRect);
                return;
            }

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
                    int maxCards = Mathf.FloorToInt((listHeight + HistoryCardGap) / (HistoryCardHeight + HistoryCardGap));
                    int shown = Mathf.Clamp(maxCards, 0, history.Count);
                    float costColumnWidth = MeasureHistoryCostWidth(history, shown);

                    for (int i = 0; i < shown; i++)
                    {
                        ResearchHistoryEntry entry = history[i];
                        if (entry?.project == null)
                            continue;
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
                DrawProjectIcon(layout.IconRect, projectDef);
                Widgets.DrawLine(new Vector2(layout.FirstSeparator.x, layout.FirstSeparator.y), new Vector2(layout.FirstSeparator.x, layout.FirstSeparator.yMax), structureAccent, 1f);
                Widgets.DrawLine(new Vector2(layout.SecondSeparator.x, layout.SecondSeparator.y), new Vector2(layout.SecondSeparator.x, layout.SecondSeparator.yMax), structureAccent, 1f);
            }

            Rect nameRect = layout.NameRect;
            Rect topTextRect = new Rect(nameRect.x, nameRect.y + 2f, nameRect.width, 22f);
            Rect bottomTextRect = new Rect(nameRect.x, nameRect.y + 24f, nameRect.width, 22f);
            GUI.color = isMouseOver ? Color.white : new Color(0.95f, 0.95f, 0.95f);
            Text.Anchor = TextAnchor.LowerLeft;
            Widgets.Label(topTextRect, SafeLabel(projectDef));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(bottomTextRect, FormatCompletedAgo(entry.tick));
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

        // The snoozed list is a sub-view of history, so showingHistory stays set and the pre-history selection is kept.
        private void OpenSnoozed()
        {
            showingSnoozed = true;
            showingHistory = true;
            snoozedOutHeight = 0f;
            snoozedScrollPosition = Vector2.zero;
        }

        // A reused copy, so unsnoozing from a card cannot disturb the loop walking it.
        private readonly List<ResearchProjectDef> snoozedDrawBuffer = new List<ResearchProjectDef>();
        private readonly List<ResearchProjectDef> blockedDrawBuffer = new List<ResearchProjectDef>();
        private const float BlockedHeaderHeight = 32f;

        // Measured on layout for adaptive size, like the offer list's scroll heights.
        private Vector2 snoozedScrollPosition = Vector2.zero;
        private float snoozedViewHeight;
        private float snoozedOutHeight;

        private void DrawSnoozedColumn(Rect leftRect)
        {
            ResearchSnoozeTracker tracker = ResearchSnoozeTracker.Get();
            List<ResearchProjectDef> snoozed = snoozedDrawBuffer;
            List<ResearchProjectDef> blocked = blockedDrawBuffer;
            if (tracker != null)
            {
                tracker.GetSnoozedProjects(snoozed);
                tracker.GetBlockedBySnooze(blocked);
            }
            else
            {
                snoozed.Clear();
                blocked.Clear();
            }

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
                Widgets.Label(new Rect(0f, currentY, leftRect.width, headerHeight), "CM_Semi_Random_Research_SnoozedHeader".Translate());
                GenUI.ResetLabelAlign();
                Text.Font = GameFont.Small;
                currentY += headerHeight + 4f;
                float listHeight = leftRect.height - totalFooterHeight - currentY;
                float listWidth = leftRect.width;
                // Projects set aside by a snoozed prerequisite follow under their own heading.
                float blockedTop = snoozed.Count * (HistoryCardHeight + HistoryCardGap);
                float viewHeight = blocked.Count > 0
                    ? blockedTop + BlockedHeaderHeight + blocked.Count * (HistoryCardHeight + HistoryCardGap) - HistoryCardGap
                    : Mathf.Max(0f, blockedTop - HistoryCardGap);
                if (Event.current.type == EventType.Layout)
                {
                    snoozedViewHeight = viewHeight;
                    snoozedOutHeight = listHeight;
                }

                if (snoozed.Count == 0 && blocked.Count == 0)
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(new Rect(0f, currentY, listWidth, 60f), "CM_Semi_Random_Research_SnoozedEmpty".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    Rect outRect = new Rect(0f, currentY, listWidth, listHeight);
                    bool useScroll = viewHeight > listHeight + 1f;
                    Rect viewRect = new Rect(0f, 0f, useScroll ? listWidth - 16f : listWidth, viewHeight);
                    if (useScroll)
                        Widgets.BeginScrollView(outRect, ref snoozedScrollPosition, viewRect);
                    else
                        GUI.BeginGroup(outRect);
                    try
                    {
                        float cardY = 0f;
                        for (int i = 0; i < snoozed.Count; i++)
                        {
                            ResearchProjectDef projectDef = snoozed[i];
                            if (projectDef == null)
                                continue;
                            CacheFirstUnlockable(projectDef);
                            DrawSnoozedCard(new Rect(0f, cardY, viewRect.width, HistoryCardHeight), projectDef, tracker);
                            cardY += HistoryCardHeight + HistoryCardGap;
                        }
                        if (blocked.Count > 0)
                        {
                            cardY = blockedTop;
                            Text.Anchor = TextAnchor.MiddleLeft;
                            GUI.color = new Color(0.7f, 0.7f, 0.7f);
                            Widgets.Label(new Rect(0f, cardY, viewRect.width, BlockedHeaderHeight - 4f), "CM_Semi_Random_Research_BlockedBySnoozeHeader".Translate());
                            GUI.color = Color.white;
                            Text.Anchor = TextAnchor.UpperLeft;
                            cardY += BlockedHeaderHeight;
                            for (int i = 0; i < blocked.Count; i++)
                            {
                                DrawBlockedBySnoozeCard(new Rect(0f, cardY, viewRect.width, HistoryCardHeight), blocked[i], tracker);
                                cardY += HistoryCardHeight + HistoryCardGap;
                            }
                        }
                    }
                    finally
                    {
                        if (useScroll)
                            Widgets.EndScrollView();
                        else
                            GUI.EndGroup();
                    }
                }
            }
            finally
            {
                GUI.EndGroup();
            }
            DrawHistoryFooter(leftRect, totalFooterHeight, footerPaddingTop, footerHeight);
        }

        private void DrawSnoozedCard(Rect drawRect, ResearchProjectDef projectDef, ResearchSnoozeTracker tracker)
        {
            Color originalColor = GUI.color;
            TextAnchor startingTextAnchor = Text.Anchor;
            bool isMouseOver = Mouse.IsOver(drawRect);
            Color techColor = GetCategoryColor(projectDef);
            Color structureAccent = GetCardStructureAccent(projectDef, techColor);
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(drawRect, Color.Lerp(TexUI.AvailResearchColor, techColor, 0.25f));
                GUI.color = structureAccent;
                Widgets.DrawBox(drawRect);
                GUI.color = Color.white;
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(drawRect.x + 8f, drawRect.y, drawRect.width - 16f, drawRect.height), SafeLabel(projectDef));
            if (ResearchWidgets.RightClicked(drawRect) || Clicked(drawRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                tracker?.Unsnooze(projectDef);
                CopyAvailableProjects((drawTracker ?? cachedTracker)?.PeekAvailableProjects());
                InvalidateLeftColumnCache();
                techLevelStatsStale = true;
                Messages.Message("CM_Semi_Random_Research_Unsnoozed".Translate(SafeLabel(projectDef)), MessageTypeDefOf.NeutralEvent, false);
            }
            if (isMouseOver)
                TooltipHandler.TipRegion(drawRect, "CM_Semi_Random_Research_UnsnoozeTip".Translate());
            GUI.color = originalColor;
            Text.Anchor = startingTextAnchor;
        }

        // Greyed and inert: it comes back when the snoozed prerequisite the tooltip names is unsnoozed.
        private void DrawBlockedBySnoozeCard(Rect drawRect, ResearchProjectDef projectDef, ResearchSnoozeTracker tracker)
        {
            Color originalColor = GUI.color;
            TextAnchor startingTextAnchor = Text.Anchor;
            Color techColor = GetCategoryColor(projectDef);
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(drawRect, Color.Lerp(TexUI.AvailResearchColor, techColor, 0.12f));
                GUI.color = Color.Lerp(GetCardStructureAccent(projectDef, techColor), Color.gray, 0.5f);
                Widgets.DrawBox(drawRect);
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(drawRect.x + 8f, drawRect.y, drawRect.width - 16f, drawRect.height), SafeLabel(projectDef));
            if (Mouse.IsOver(drawRect) && tracker != null)
            {
                ResearchProjectDef tipProject = projectDef;
                TooltipHandler.TipRegion(drawRect, () => "CM_Semi_Random_Research_BlockedBySnoozeTip".Translate(
                    string.Join(", ", tracker.SnoozedPrerequisites(tipProject).ConvertAll(SafeLabel))), tipProject.shortHash ^ 0x5a17);
            }
            GUI.color = originalColor;
            Text.Anchor = startingTextAnchor;
        }
    }
}

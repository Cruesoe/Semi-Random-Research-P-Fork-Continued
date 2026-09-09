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

            if (selectionBeforeHistory != null && !selectionBeforeHistory.IsFinished)
                selectedProject = selectionBeforeHistory;
            else
                SelectDefaultProject();

            selectionBeforeHistory = null;
            cachedCanStartNowTick = -1;
            RefreshCanStartNow(Find.TickManager.TicksGame);
        }

        private void OpenSnoozed()
        {
            showingSnoozed = true;
            showingHistory = false;
            selectionBeforeHistory = selectedProject;
        }
    }
}

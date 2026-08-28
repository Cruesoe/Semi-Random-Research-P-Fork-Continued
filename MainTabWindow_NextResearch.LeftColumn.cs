using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    public partial class MainTabWindow_NextResearch
    {
        private void InvalidateLeftColumnCache()
        {
            cachedLeftListsRevision = -1;
            cachedLeftCurrentHash = int.MinValue;
        }

        // Paused still counts as busy: the project keeps the category until it is resumed or cancelled.
        private static bool IsCategoryBusy(ResearchProjectDef activeProject)
        {
            return activeProject != null && !activeProject.IsFinished;
        }

        private static int CurrentProjectsHash(List<ResearchProjectDef> projects)
        {
            if (projects == null)
                return 0;

            int hash = 17;
            for (int i = 0; i < projects.Count; i++)
            {
                ResearchProjectDef project = projects[i];
                hash = hash * 31 + (project != null ? project.shortHash : 0);
            }
            return hash;
        }

        private void RebuildLeftColumnLists(ResearchTracker researchTracker)
        {
            int revision = researchTracker != null ? researchTracker.OffersRevision : -1;
            int currentHash = CurrentProjectsHash(researchTracker?.CurrentProject);

            if (revision == cachedLeftListsRevision && currentHash == cachedLeftCurrentHash)
            {
                return;
            }

            cachedLeftListsRevision = revision;
            cachedLeftCurrentHash = currentHash;

            ResearchProjectDef activeNonAnomalyProject = null;
            ResearchProjectDef activeAnomalyProjectBasic = null;
            ResearchProjectDef activeAnomalyProjectAdvanced = null;
            ResearchProjectDef activeGravshipProject = null;

            if (researchTracker != null && researchTracker.CurrentProject != null)
            {
                for (int i = 0; i < researchTracker.CurrentProject.Count; i++)
                {
                    ResearchProjectDef p = researchTracker.CurrentProject[i];
                    if (p == null)
                        continue;

                    string key = ResearchTracker.GetCategoryKey(p);
                    if (key == "Standard" && activeNonAnomalyProject == null)
                        activeNonAnomalyProject = p;
                    else if (key == "Gravship" && activeGravshipProject == null)
                        activeGravshipProject = p;
                    else if (AnomalyContentEnabled() && p.knowledgeCategory == KnowledgeCategoryDefOf.Basic && activeAnomalyProjectBasic == null)
                        activeAnomalyProjectBasic = p;
                    else if (AnomalyContentEnabled() && p.knowledgeCategory == KnowledgeCategoryDefOf.Advanced && activeAnomalyProjectAdvanced == null)
                        activeAnomalyProjectAdvanced = p;
                }
            }

            if (activeNonAnomalyProject == null)
            {
                ResearchProjectDef vanilla = Find.ResearchManager.GetProject();
                if (vanilla != null && ResearchTracker.GetCategoryKey(vanilla) == "Standard")
                    activeNonAnomalyProject = vanilla;
            }

            cachedActiveProjects.Clear();
            if (activeNonAnomalyProject != null) cachedActiveProjects.Add(activeNonAnomalyProject);
            if (activeAnomalyProjectBasic != null && !cachedActiveProjects.Contains(activeAnomalyProjectBasic))
                cachedActiveProjects.Add(activeAnomalyProjectBasic);
            if (activeAnomalyProjectAdvanced != null && !cachedActiveProjects.Contains(activeAnomalyProjectAdvanced))
                cachedActiveProjects.Add(activeAnomalyProjectAdvanced);
            if (activeGravshipProject != null && !cachedActiveProjects.Contains(activeGravshipProject))
                cachedActiveProjects.Add(activeGravshipProject);

            cachedFoundationProjects.Clear();
            cachedEmergenceProjects.Clear();

            cachedAnomalyBasic.Clear();
            cachedAnomalyAdvanced.Clear();
            cachedGravshipProjects.Clear();
            cachedStandardGroups.Clear();

            // "Allow switching between choices" off means nothing else can be started until the
            // active project finishes, so the rest of that category's offers are hidden instead of
            // being listed as locked. Only the display is filtered: the tracker still holds them,
            // so with "Reroll all choices every time" off they come back on completion.
            bool hideBusyCategoryOffers = SemiRandomResearchMod.settings == null || !SemiRandomResearchMod.settings.allowSwitchingResearch;
            bool standardBusy = hideBusyCategoryOffers && IsCategoryBusy(activeNonAnomalyProject);
            bool anomalyBasicBusy = hideBusyCategoryOffers && IsCategoryBusy(activeAnomalyProjectBasic);
            bool anomalyAdvancedBusy = hideBusyCategoryOffers && IsCategoryBusy(activeAnomalyProjectAdvanced);
            bool gravshipBusy = hideBusyCategoryOffers && IsCategoryBusy(activeGravshipProject);

            var standardByLevel = new Dictionary<TechLevel, List<ResearchProjectDef>>();
            bool anomalyOn = AnomalyContentEnabled();
            for (int i = 0; i < currentAvailableProjects.Count; i++)
            {
                ResearchProjectDef p = currentAvailableProjects[i];
                if (p == null)
                    continue;

                if (NodeResearch.IsFoundationTech(p))
                    cachedFoundationProjects.Add(p);
                if (NodeResearch.IsEmergenceTech(p))
                    cachedEmergenceProjects.Add(p);

                string key = ResearchTracker.GetCategoryKey(p);
                if (key == "Gravship")
                {
                    if (p != activeGravshipProject && !gravshipBusy)
                    {
                        cachedGravshipProjects.Add(p);
                    }
                    continue;
                }

                if (anomalyOn && p.knowledgeCategory == KnowledgeCategoryDefOf.Basic)
                {
                    if (p != activeAnomalyProjectBasic && !anomalyBasicBusy)
                    {
                        cachedAnomalyBasic.Add(p);
                    }
                    continue;
                }

                if (anomalyOn && p.knowledgeCategory == KnowledgeCategoryDefOf.Advanced)
                {
                    if (p != activeAnomalyProjectAdvanced && !anomalyAdvancedBusy)
                    {
                        cachedAnomalyAdvanced.Add(p);
                    }
                    continue;
                }

                if (key == "Standard" && p != activeNonAnomalyProject && !standardBusy)
                {
                    if (!standardByLevel.TryGetValue(p.techLevel, out List<ResearchProjectDef> list))
                    {
                        list = new List<ResearchProjectDef>();
                        standardByLevel[p.techLevel] = list;
                    }
                    list.Add(p);
                }
            }

            cachedAnomalyBasic.Sort((a, b) => a.CostApparent.CompareTo(b.CostApparent));
            cachedAnomalyAdvanced.Sort((a, b) => a.CostApparent.CompareTo(b.CostApparent));
            cachedGravshipProjects.Sort((a, b) => a.CostApparent.CompareTo(b.CostApparent));

            var levels = new List<TechLevel>(standardByLevel.Keys);
            levels.Sort();
            for (int i = 0; i < levels.Count; i++)
            {
                List<ResearchProjectDef> list = standardByLevel[levels[i]];
                list.Sort((a, b) => a.CostApparent.CompareTo(b.CostApparent));
                cachedStandardGroups.Add(new Pair<TechLevel, List<ResearchProjectDef>>(levels[i], list));
            }

            cachedSharedCostWidth = Mathf.Max(
                MeasureCostColumnWidth(currentAvailableProjects),
                MeasureCostColumnWidth(cachedActiveProjects));

            for (int i = 0; i < cachedActiveProjects.Count; i++)
            {
                ResearchProjectDef p = cachedActiveProjects[i];
                if (p == null)
                    continue;
                if (NodeResearch.IsFoundationTech(p))
                    cachedFoundationProjects.Add(p);
                if (NodeResearch.IsEmergenceTech(p))
                    cachedEmergenceProjects.Add(p);
            }
        }

        private void DrawLeftColumn(Rect leftRect)
        {
            ResearchTracker researchTracker = drawTracker;
            RebuildLeftColumnLists(researchTracker);

            Rect position = leftRect;
            float footerPaddingTop = 12f;
            float footerHeight = 40f;
            float footerPaddingBottom = 12f;
            float totalFooterHeight = footerPaddingTop + footerHeight + footerPaddingBottom;
            float footerButtonWidth = 120f;
            float buttonSpacing = 20f;

            GUI.BeginGroup(position);
            bool startedScroll = false;
            try
            {

            float currentY = 0f;
            float mainLabelHeight = 40.0f;
            float gapHeight = 8.0f;
            float researchProjectGapHeight = 12.0f;
            float buttonHeight = 48f;
            float techLevelHeaderHeight = 28f;

            bool hasAnomalyToShowBasic = cachedAnomalyBasic.Count > 0;
            bool hasAnomalyToShowAdvanced = cachedAnomalyAdvanced.Count > 0;
            bool hasGravshipToShow = cachedGravshipProjects.Count > 0;
            float sharedCostColumnWidth = cachedSharedCostWidth;

            Text.Font = GameFont.Medium;
            GenUI.SetLabelAlign(TextAnchor.MiddleLeft);

            float labelWidth = position.width * 0.5f;
            Rect mainLabelRect = new Rect(0f, currentY, labelWidth, mainLabelHeight);
            Widgets.Label(mainLabelRect, "CM_Semi_Random_Research_CurrentlyResearching".Translate());

            Text.Font = GameFont.Small;
            float techInfoX = labelWidth + 10f;
            float techInfoWidth = position.width - techInfoX;

            TechLevel colonyTech = Faction.OfPlayer.def.techLevel;
            Rect colonyTechRect = new Rect(techInfoX, currentY, techInfoWidth * 0.5f, mainLabelHeight);

            DrawTechLevelText(colonyTechRect, "CM_Semi_Random_Research_FactionPrefix".Translate(), colonyTech);

            Rect worldTechRect = new Rect(techInfoX + techInfoWidth * 0.5f, currentY, techInfoWidth * 0.5f, mainLabelHeight);
            DrawTechLevelText(worldTechRect, "CM_Semi_Random_Research_WorldPrefix".Translate(), cachedWorldTech);

            GenUI.ResetLabelAlign();
            currentY += mainLabelHeight + 4f;

            Rect scrollOutRect = new Rect(0f, currentY, position.width, position.height - (totalFooterHeight + currentY));
            if (Event.current.type == EventType.Layout)
                leftScrollHeightForFrame = leftScrollViewHeight;
            float viewHeight = Mathf.Max(leftScrollHeightForFrame, 1f);
            Rect scrollViewRect = new Rect(0f, 0f, scrollOutRect.width - 16f, viewHeight);
            bool useScroll = viewHeight > scrollOutRect.height + 1f;
            if (useScroll)
            {
                Widgets.BeginScrollView(scrollOutRect, ref leftScrollPosition, scrollViewRect);
                startedScroll = true;
            }
            else
            {
                GUI.BeginGroup(scrollOutRect);
            }
            try
            {
            currentY = 0f;

            if (cachedActiveProjects.Count > 0)
            {
                for (int i = 0; i < cachedActiveProjects.Count; i++)
                {
                    ResearchProjectDef activeProj = cachedActiveProjects[i];
                    // Only the Main/Standard project gets the expanded height
                    bool isMainProject = ResearchTracker.GetCategoryKey(activeProj) == "Standard";

                    float baseHeight = 48f;
                    float expandedHeight = baseHeight + 16f + 38f;
                    if (SemiRandomResearchMod.settings.showResearchRateGraph) expandedHeight += 16f + 140f;

                    // Apply the expanded height ONLY to the main project
                    float cardHeight = isMainProject ? expandedHeight : baseHeight;

                    Rect rateStatsRect = new Rect(0f, currentY, scrollViewRect.width, cardHeight);

                    DrawResearchRateUI(rateStatsRect, activeProj, isMainProject, sharedCostColumnWidth);

                    currentY += cardHeight + 12f;
                }
            }

            currentY += 8f;
            GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.4f);
            Widgets.DrawLineHorizontal(0f, currentY, scrollViewRect.width);
            GUI.color = Color.white;
            currentY += 16f;

            bool hasStandardToShow = cachedStandardGroups.Count > 0;

            if (hasStandardToShow || hasAnomalyToShowBasic || hasAnomalyToShowAdvanced || hasGravshipToShow)
            {
                Text.Font = GameFont.Medium;
                GUI.color = Color.white;
                Rect availableHeaderRect = new Rect(0f, currentY, scrollViewRect.width, 30f);
                Widgets.Label(availableHeaderRect, "CM_Semi_Random_Research_Available_Projects".Translate());
                currentY += 34f;
            }

            bool groupByTechLevel = SemiRandomResearchMod.settings == null || SemiRandomResearchMod.settings.colorAndGroupByTechLevel;
            bool isFirst = true;
            for (int g = 0; g < cachedStandardGroups.Count; g++)
            {
                Pair<TechLevel, List<ResearchProjectDef>> techGroup = cachedStandardGroups[g];
                if (techGroup.Second == null || techGroup.Second.Count == 0)
                    continue;

                float headerAnimProgress = 1f;
                if (techLevelHeaderProgress.TryGetValue(techGroup.First, out float progress))
                    headerAnimProgress = progress;

                if (headerAnimProgress <= 0.01f) continue;

                if (groupByTechLevel)
                {
                    if (!isFirst) currentY += gapHeight;
                    isFirst = false;

                    Color originalColor = GUI.color;
                    Text.Font = GameFont.Small;
                    Color techColor = GetTechLevelColor(techGroup.First);
                    techColor.a *= headerAnimProgress;
                    GUI.color = techColor;

                    Rect headerRect = new Rect(0f, currentY, scrollViewRect.width, techLevelHeaderHeight);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(headerRect, techGroup.First.ToStringHuman().CapitalizeFirst());
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = originalColor;
                    currentY += techLevelHeaderHeight;
                }

                List<ResearchProjectDef> groupProjects = techGroup.Second;
                for (int p = 0; p < groupProjects.Count; p++)
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, groupProjects[p], sharedCostColumnWidth);
                    currentY += buttonHeight + researchProjectGapHeight;
                }
            }

            // ==========================================
            // AVAILABLE ANOMALY / GRAVSHIP PROJECTS
            // ==========================================
            if ((AnomalyContentEnabled() && (hasAnomalyToShowBasic || hasAnomalyToShowAdvanced)) || hasGravshipToShow)
            {
                if (currentY > 0)
                {
                    currentY += gapHeight;
                    GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.4f);
                    Widgets.DrawLineHorizontal(0f, currentY, scrollViewRect.width);
                    GUI.color = Color.white;
                    currentY += gapHeight;
                }
            }

            if (AnomalyContentEnabled() && hasAnomalyToShowBasic)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect anomalyHeaderRect = new Rect(0f, currentY, scrollViewRect.width, techLevelHeaderHeight);

                GUI.color = AnomalyBasicColor;
                Widgets.Label(anomalyHeaderRect, "CM_Semi_Random_Research_AvailableDark".Translate());
                GUI.color = Color.white;
                currentY += techLevelHeaderHeight;

                for (int i = 0; i < cachedAnomalyBasic.Count; i++)
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, cachedAnomalyBasic[i], sharedCostColumnWidth);
                    currentY += buttonHeight + researchProjectGapHeight;
                }
            }

            if (AnomalyContentEnabled() && hasAnomalyToShowAdvanced)
            {
                if (hasAnomalyToShowBasic) currentY += gapHeight;

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect anomalyHeaderRect = new Rect(0f, currentY, scrollViewRect.width, techLevelHeaderHeight);

                GUI.color = AnomalyAdvancedColor;
                Widgets.Label(anomalyHeaderRect, "CM_Semi_Random_Research_AvailableAdvancedDark".Translate());
                GUI.color = Color.white;
                currentY += techLevelHeaderHeight;

                for (int i = 0; i < cachedAnomalyAdvanced.Count; i++)
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, cachedAnomalyAdvanced[i], sharedCostColumnWidth);
                    currentY += buttonHeight + researchProjectGapHeight;
                }
            }

            if (hasGravshipToShow)
            {
                if (hasAnomalyToShowBasic || hasAnomalyToShowAdvanced) currentY += gapHeight;

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect gravshipHeaderRect = new Rect(0f, currentY, scrollViewRect.width, techLevelHeaderHeight);

                GUI.color = GravshipColor;
                Widgets.Label(gravshipHeaderRect, "CM_Semi_Random_Research_AvailableGravtech".Translate());
                GUI.color = Color.white;
                currentY += techLevelHeaderHeight;

                for (int i = 0; i < cachedGravshipProjects.Count; i++)
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, cachedGravshipProjects[i], sharedCostColumnWidth);
                    currentY += buttonHeight + researchProjectGapHeight;
                }
            }

            if (Event.current.type == EventType.Layout)
                leftScrollViewHeight = currentY;
            }
            finally
            {
                if (startedScroll)
                    Widgets.EndScrollView();
                else
                    GUI.EndGroup();
            }

            }
            finally
            {
                GUI.EndGroup();
            }

            if (researchTracker != null)
            {
                DrawLeftColumnFooter(leftRect, researchTracker, totalFooterHeight, footerPaddingTop, footerHeight, footerButtonWidth, buttonSpacing);
            }
        }

        private void DrawLeftColumnFooter(Rect leftRect, ResearchTracker researchTracker, float totalFooterHeight, float footerPaddingTop, float footerHeight, float footerButtonWidth, float buttonSpacing)
        {
            Text.Font = GameFont.Small;

            Rect footerContainerRect = new Rect(leftRect.x, leftRect.yMax - totalFooterHeight, leftRect.width, totalFooterHeight);

            if (IsRepaint)
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                Widgets.DrawLineHorizontal(footerContainerRect.x, footerContainerRect.y, footerContainerRect.width);
                GUI.color = Color.white;
            }

            bool showRerollButton = SemiRandomResearchMod.settings.allowManualReroll != ManualReroll.None;
            int buttonCount = showRerollButton ? 3 : 2;
            float totalButtonsWidth = (footerButtonWidth * buttonCount) + (buttonSpacing * (buttonCount - 1));
            float startX = (footerContainerRect.width - totalButtonsWidth) / 2;
            float footerButtonY = footerContainerRect.y + footerPaddingTop;

            Rect researchTreeButtonRect = new Rect(footerContainerRect.x + startX, footerButtonY, footerButtonWidth, footerHeight);
            Rect rerollButtonRect = default;
            Rect researchButtonRect;
            if (showRerollButton)
            {
                rerollButtonRect = new Rect(researchTreeButtonRect.xMax + buttonSpacing, footerButtonY, footerButtonWidth, footerHeight);
                researchButtonRect = new Rect(rerollButtonRect.xMax + buttonSpacing, footerButtonY, footerButtonWidth, footerHeight);
            }
            else
            {
                researchButtonRect = new Rect(researchTreeButtonRect.xMax + buttonSpacing, footerButtonY, footerButtonWidth, footerHeight);
            }

            PreferredResearchTree treeTarget = ResearchTabWindowSwitcher.GetEffectivePreferredTree();
            bool opensNodeResearch = treeTarget == PreferredResearchTree.NodeResearch
                && ResearchTabWindowSwitcher.IsTreeAvailable(PreferredResearchTree.NodeResearch);
            string treeButtonLabel = opensNodeResearch
                ? "CM_Semi_Random_Research_Tree_NodeResearch".Translate()
                : "CM_Semi_Random_Research_TreeButton".Translate();
            string treeButtonTip = opensNodeResearch
                ? "CM_Semi_Random_Research_TreeButtonTip_Node".Translate()
                : "CM_Semi_Random_Research_TreeButtonTip".Translate();

            if (ColoredButtonText(researchTreeButtonRect, treeButtonLabel, FooterTreeButtonColor))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                ResearchTabWindowSwitcher.SwitchToPreferredTree(this);
                Event.current.Use();
            }

            if (IsRepaint && Mouse.IsOver(researchTreeButtonRect))
                TooltipHandler.TipRegion(researchTreeButtonRect, treeButtonTip);

            if (showRerollButton)
            {
                if (cachedCanReroll)
                {
                    if (ColoredButtonText(rerollButtonRect, "CM_Semi_Random_Research_Reroll_Label".Translate(), FooterRerollButtonColor))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        SoundDefOf.TabOpen.PlayOneShotOnCamera();
                        researchTracker.Reroll(rerollButtonType);
                        lastRerollTime = Time.realtimeSinceStartup;
                        cachedCanReroll = researchTracker.CanReroll(rerollButtonType);
                        CopyAvailableProjects(researchTracker.PeekAvailableProjects());
                        InvalidateLeftColumnCache();
                    }
                }
                else
                {
                    DrawInactiveFooterButton(rerollButtonRect, "CM_Semi_Random_Research_NoRerolls".Translate(), Color.grey);
                }
            }

            switch (cachedFooterStartMode)
            {
                case FooterStartMode.CanStart:
                    if (ColoredButtonText(researchButtonRect, "CM_Semi_Random_Research_StartResearch".Translate(), FooterStartButtonColor))
                    {
                        SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                        // Ungated: this button is only drawn when PlayerCanStartProject allows it,
                        // so the selection gate (which refuses every other UI) must not re-judge it.
                        ResearchTracker.SetVanillaProjectUngated(selectedProject);

                        string categoryKey = ResearchTracker.GetCategoryKey(selectedProject);
                        Current.Game.World.GetComponent<ResearchTracker>()?.SetCurrentProjectByKey(selectedProject, categoryKey);
                        InvalidateLeftColumnCache();
                        if (cachedTracker != null)
                            CopyAvailableProjects(cachedTracker.PeekAvailableProjects());
                        cachedCanStartNowTick = -1;
                        RefreshCanStartNow(Find.TickManager.TicksGame);

                        TutorSystem.Notify_Event("StartResearchProject");
                        if (!ColonistsHaveResearchBench)
                        {
                            Messages.Message("MessageResearchMenuWithoutBench".Translate(), MessageTypeDefOf.CautionInput);
                        }
                    }
                    break;
                case FooterStartMode.Finished:
                    DrawInactiveFooterButton(researchButtonRect, "CM_Semi_Random_Research_Finished".Translate(), Color.grey);
                    break;
                case FooterStartMode.InProgress:
                    if (researchTracker.ResearchPaused)
                    {
                        if (ColoredButtonText(researchButtonRect, "CM_Semi_Random_Research_ResumeResearch".Translate(), FooterStartButtonColor))
                        {
                            SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                            researchTracker.ResumeResearch(selectedProject);
                            InvalidateLeftColumnCache();
                            if (cachedTracker != null)
                                CopyAvailableProjects(cachedTracker.PeekAvailableProjects());
                            cachedCanStartNowTick = -1;
                            RefreshCanStartNow(Find.TickManager.TicksGame);

                            TutorSystem.Notify_Event("StartResearchProject");
                            if (!ColonistsHaveResearchBench)
                            {
                                Messages.Message("MessageResearchMenuWithoutBench".Translate(), MessageTypeDefOf.CautionInput);
                            }
                        }
                        TooltipHandler.TipRegion(researchButtonRect, "CM_Semi_Random_Research_ResumeResearchTip".Translate());
                    }
                    else
                    {
                        if (ColoredButtonText(researchButtonRect, "CM_Semi_Random_Research_PauseResearch".Translate(), FooterPauseButtonColor))
                        {
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            researchTracker.PauseResearch(selectedProject);
                            Messages.Message("CM_Semi_Random_Research_ResearchPausedMessage".Translate(), MessageTypeDefOf.NeutralEvent, false);
                            InvalidateLeftColumnCache();
                            if (cachedTracker != null)
                                CopyAvailableProjects(cachedTracker.PeekAvailableProjects());
                            cachedCanStartNowTick = -1;
                            RefreshCanStartNow(Find.TickManager.TicksGame);
                        }
                        TooltipHandler.TipRegion(researchButtonRect, "CM_Semi_Random_Research_PauseResearchTip".Translate());
                    }
                    break;
                case FooterStartMode.Locked:
                    DrawInactiveFooterButton(researchButtonRect, "CM_Semi_Random_Research_Locked".Translate(), Color.grey);
                    break;
                default:
                    DrawInactiveFooterButton(researchButtonRect, "CM_Semi_Random_Research_StartResearch".Translate(), Color.grey);
                    break;
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static void DrawInactiveFooterButton(Rect rect, string label, Color fill)
        {
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(rect, fill);
                GUI.color = Color.Lerp(fill, Color.black, 0.4f);
                Widgets.DrawBox(rect);
                GUI.color = Color.white;
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static bool ColoredButtonText(Rect rect, string label, Color fill)
        {
            bool mouseOver = Mouse.IsOver(rect);
            bool held = mouseOver && Input.GetMouseButton(0);

            if (IsRepaint)
            {
                Color bg = fill;
                if (held)
                    bg = fill * 0.75f;
                else if (mouseOver)
                    bg = Color.Lerp(fill, Color.white, 0.16f);

                Widgets.DrawBoxSolid(rect, bg);

                Color old = GUI.color;
                GUI.color = Color.Lerp(fill, Color.black, 0.4f);
                Widgets.DrawBox(rect);
                GUI.color = old;
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return Clicked(rect);
        }

        private void DrawTechLevelText(Rect rect, string prefix, TechLevel techLevel)
        {
            Text.Anchor = TextAnchor.MiddleLeft;

            GUI.color = Color.white;
            float prefixWidth = Text.CalcSize(prefix).x;
            Widgets.Label(rect, prefix);

            Rect techLevelRect = new Rect(rect.x + prefixWidth, rect.y, rect.width - prefixWidth, rect.height);
            GUI.color = GetTechLevelColor(techLevel);
            Widgets.Label(techLevelRect, techLevel.ToStringHuman().CapitalizeFirst());

            GUI.color = Color.white;
        }

        private void DrawResearchButton(ref Rect drawRect, ResearchProjectDef projectDef, float costColumnWidth)
        {
            float animProgress = 1f;
            if (animationProgress.TryGetValue(projectDef.defName, out float progress))
                animProgress = progress;

            if (animProgress <= 0.01f)
                return;

            Rect originalRect = new Rect(drawRect);
            Color originalColor = GUI.color;

            bool isMouseOver = Mouse.IsOver(drawRect);

            GUI.color = new Color(1f, 1f, 1f, animProgress);

            float rightMargin = 8f;
            float buttonHeight = 48f;
            float separatorWidth = 1f;

            TextAnchor startingTextAnchor = Text.Anchor;
            Text.Font = GameFont.Small;

            drawRect.height = buttonHeight;

            drawRect.width -= rightMargin;

            string costText = GetProjectCostText(projectDef);

            CardRowLayout layout = ComputeCardRowLayout(drawRect, buttonHeight, costColumnWidth);
            Rect iconRect = layout.IconRect;
            Rect firstSeparator = layout.FirstSeparator;
            Rect secondSeparator = layout.SecondSeparator;
            Rect nameRect = layout.NameRect;
            Rect costRect = layout.CostRect;

            Color techColor = GetCategoryColor(projectDef);
            Color structureAccent = GetCardStructureAccent(projectDef, techColor);

            Color backgroundColor = isMouseOver
                ? Color.Lerp(TexUI.AvailResearchColor, techColor, 0.4f)
                : Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);

            Color borderColor = selectedProject == projectDef ?
                TexUI.HighlightBorderResearchColor :
                (isMouseOver ? Color.Lerp(structureAccent, Color.white, 0.2f) : structureAccent);

            Color textColor = new Color(0.95f, 0.95f, 0.95f);

            bool isActive = cachedActiveProjects.Contains(projectDef);

            bool canCancel = SemiRandomResearchMod.settings.allowSwitchingResearch && isActive;

            if (canCancel)
            {
                float cancelButtonSize = 20f;
                Rect cancelRect = new Rect(
                    drawRect.xMax - cancelButtonSize - 4f,
                    drawRect.y + 4f,
                    cancelButtonSize,
                    cancelButtonSize
                );

                if (IsRepaint && (isMouseOver || Mouse.IsOver(cancelRect)))
                    Widgets.DrawBoxSolid(cancelRect, new Color(0.9f, 0.3f, 0.3f, animProgress * 0.8f));

                if (Clicked(cancelRect))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();

                    ResearchTracker cancelTracker = cachedTracker ?? Current.Game.World.GetComponent<ResearchTracker>();
                    if (cancelTracker != null)
                    {
                        string categoryKey = ResearchTracker.GetCategoryKey(projectDef);
                        cancelTracker.SetCurrentProjectByKey(null, categoryKey);

                        cancelTracker.ForceAutoReseachCheckNextTick();
                        InvalidateLeftColumnCache();
                        Event.current.Use();
                    }
                }
            }

            backgroundColor.a *= animProgress;
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(drawRect, backgroundColor);

                if (isMouseOver)
                {
                    Color glowColor = structureAccent;
                    glowColor.a = 0.1f * animProgress;
                    Widgets.DrawBoxSolid(drawRect.ExpandedBy(2f), glowColor);
                }
            }

            float progressFraction = 0f;
            if (projectDef.CostApparent > 0f)
            {
                float currentProg = projectDef.ProgressApparent;
                if (currentProg <= 0f)
                {
                    currentProg = Find.ResearchManager.GetProgress(projectDef);
                }

                if (currentProg > 0f)
                {
                    progressFraction = Mathf.Clamp01(currentProg / projectDef.CostApparent);
                }
            }

            if (progressFraction > 0f && IsRepaint)
            {
                Rect progressRect = new Rect(drawRect.x, drawRect.y, drawRect.width * progressFraction, drawRect.height);

                Color progressColor = GetProgressFillAccent(projectDef, techColor);
                progressColor.a = (isActive ? 0.6f : 0.45f) * animProgress;

                Widgets.DrawBoxSolid(progressRect, progressColor);
            }

            Color cardBorderColor = structureAccent;
            if (isActive)
            {
                cardBorderColor = Color.Lerp(structureAccent, Color.white, 0.5f); // Bright native color for active
            }
            else if (selectedProject == projectDef)
            {
                cardBorderColor = Color.Lerp(structureAccent, Color.white, 0.3f); // Semi-bright for selected
            }
            else if (isMouseOver)
            {
                cardBorderColor = Color.Lerp(structureAccent, Color.white, 0.2f);
            }
            cardBorderColor.a *= animProgress;

            if (IsRepaint)
            {
                GUI.color = cardBorderColor;
                Widgets.DrawBox(drawRect);
                GUI.color = Color.white;

                Def firstUnlockable = GetFirstUnlockable(projectDef);
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

                Color lineSeparatorColor = structureAccent;
                lineSeparatorColor.a *= animProgress;
                Widgets.DrawLine(
                    new Vector2(firstSeparator.x, firstSeparator.y),
                    new Vector2(firstSeparator.x, firstSeparator.yMax),
                    lineSeparatorColor,
                    separatorWidth
                );

                Widgets.DrawLine(
                    new Vector2(secondSeparator.x, secondSeparator.y),
                    new Vector2(secondSeparator.x, secondSeparator.yMax),
                    lineSeparatorColor,
                    separatorWidth
                );
            }

            Color usedTextColor = isActive ? ActiveProjectLabelColor : textColor;

            if (isMouseOver && !isActive)
            {
                usedTextColor = Color.white;  // Pure white for best visibility
                usedTextColor.a *= animProgress;
            }

            // --- NODE RESEARCH TECH INJECTIONS ---
            bool isFoundation = cachedFoundationProjects.Contains(projectDef);
            bool isEmergence = cachedEmergenceProjects.Contains(projectDef);

            if (isFoundation || isEmergence)
            {

                Rect topTextRect = new Rect(nameRect.x, nameRect.y + 2f, nameRect.width, 24f);
                Rect bottomTextRect = new Rect(nameRect.x, nameRect.y + 24f, nameRect.width, 20f);

                Text.Anchor = TextAnchor.LowerLeft;
                GUI.color = usedTextColor;
                Widgets.Label(topTextRect, SafeLabel(projectDef));

                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;

                Color nodeTagColor = new Color(0.95f, 0.75f, 0.25f);
                nodeTagColor.a *= animProgress;
                GUI.color = nodeTagColor;

                if (isFoundation)
                {
                    Widgets.Label(bottomTextRect, "CM_Semi_Random_Research_Foundation".Translate());
                }
                else if (isEmergence)
                {
                    Widgets.Label(bottomTextRect, "CM_Semi_Random_Research_Emergence".Translate());
                }

                Text.Font = GameFont.Small;
            }
            else
            {
                GUI.color = usedTextColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(nameRect, SafeLabel(projectDef));
            }

            GUI.color = originalColor;

            Text.Anchor = TextAnchor.MiddleCenter;
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(costRect, costText);
            Text.WordWrap = wordWrap;

            if (animProgress >= 0.7f && Clicked(drawRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                selectedProject = projectDef;
                cachedCanStartNowTick = -1;
                RefreshCanStartNow(Find.TickManager.TicksGame);
            }

            if (selectedProject == projectDef && IsRepaint)
            {
                Color highlightColor = Color.Lerp(structureAccent, Color.white, isActive ? 0.5f : 0.3f);
                highlightColor.a *= animProgress;
                DrawTransparentBox(drawRect, highlightColor, 2f);
            }

            if (isMouseOver)
                TooltipHandler.TipRegion(drawRect, SafeLabel(projectDef));

            GUI.color = originalColor;
            Text.Anchor = startingTextAnchor;

            drawRect = originalRect;
        }
    }
}

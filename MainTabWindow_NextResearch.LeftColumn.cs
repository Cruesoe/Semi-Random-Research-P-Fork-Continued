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
        private void DrawLeftColumn(Rect leftRect)
        {
            ResearchTracker researchTracker = Current.Game.World.GetComponent<ResearchTracker>();

            Rect position = leftRect;
            GUI.BeginGroup(position);

            float currentY = 0f;
            float mainLabelHeight = 40.0f;
            float gapHeight = 8.0f;
            float researchProjectGapHeight = 12.0f;
            float buttonHeight = 48f;
            float techLevelHeaderHeight = 28f;

            float footerPaddingTop = 12f;
            float footerHeight = 40f;
            float footerPaddingBottom = 12f;
            float totalFooterHeight = footerPaddingTop + footerHeight + footerPaddingBottom;
            float footerButtonWidth = 120f;
            float buttonSpacing = 20f;

            bool hasActiveNonAnomalyResearch = false;
            bool hasActiveAnomalyResearchBasic = false;
            bool hasActiveAnomalyResearchAdvanced = false;
            bool hasActiveGravshipResearch = false;

            ResearchProjectDef activeNonAnomalyProject = null;
            ResearchProjectDef activeAnomalyProjectBasic = null;
            ResearchProjectDef activeAnomalyProjectAdvanced = null;
            ResearchProjectDef activeGravshipProject = null;

            if (researchTracker != null && researchTracker.CurrentProject != null && researchTracker.CurrentProject.Count > 0)
            {
                activeNonAnomalyProject = researchTracker.CurrentProject.FirstOrDefault(p => ResearchTracker.GetCategoryKey(p) == "Standard");
                hasActiveNonAnomalyResearch = activeNonAnomalyProject != null;

                activeAnomalyProjectBasic = researchTracker.CurrentProject.FirstOrDefault(p => p.knowledgeCategory == KnowledgeCategoryDefOf.Basic);
                hasActiveAnomalyResearchBasic = activeAnomalyProjectBasic != null;

                activeAnomalyProjectAdvanced = researchTracker.CurrentProject.FirstOrDefault(p => p.knowledgeCategory == KnowledgeCategoryDefOf.Advanced);
                hasActiveAnomalyResearchAdvanced = activeAnomalyProjectAdvanced != null;

                activeGravshipProject = researchTracker.CurrentProject.FirstOrDefault(p => p.tab?.defName == "VGE_Gravtech" || p.tab?.defName == "VGE_GravShip");
                hasActiveGravshipResearch = activeGravshipProject != null;
            }

            if (!hasActiveNonAnomalyResearch && Find.ResearchManager.GetProject() != null &&
                ResearchTracker.GetCategoryKey(Find.ResearchManager.GetProject()) == "Standard")
            {
                activeNonAnomalyProject = Find.ResearchManager.GetProject();
                hasActiveNonAnomalyResearch = true;
            }

            var anomalyProjectsBasic = AnomalyContentEnabled() ?
                currentAvailableProjects.Where(p => p.knowledgeCategory == KnowledgeCategoryDefOf.Basic).ToList() :
                new List<ResearchProjectDef>();

            var anomalyProjectsAdvanced = AnomalyContentEnabled() ?
                currentAvailableProjects.Where(p => p.knowledgeCategory == KnowledgeCategoryDefOf.Advanced).ToList() :
                new List<ResearchProjectDef>();

            var gravshipProjects = currentAvailableProjects.Where(p => p.tab?.defName == "VGE_Gravtech" || p.tab?.defName == "VGE_GravShip").ToList();

            // REMOVE active projects from available lists so they don't draw twice
            if (hasActiveAnomalyResearchBasic) anomalyProjectsBasic.Remove(activeAnomalyProjectBasic);
            if (hasActiveAnomalyResearchAdvanced) anomalyProjectsAdvanced.Remove(activeAnomalyProjectAdvanced);
            if (hasActiveGravshipResearch) gravshipProjects.Remove(activeGravshipProject);

            bool hasAnomalyToShowBasic = anomalyProjectsBasic.Any();
            bool hasAnomalyToShowAdvanced = anomalyProjectsAdvanced.Any();
            bool hasGravshipToShow = gravshipProjects.Any();

            float sharedCostColumnWidth = MeasureCostColumnWidth(
                currentAvailableProjects
                    .Concat(new[] { activeNonAnomalyProject, activeAnomalyProjectBasic, activeAnomalyProjectAdvanced, activeGravshipProject }));

            Text.Font = GameFont.Medium;
            GenUI.SetLabelAlign(TextAnchor.MiddleLeft);

            // Increased from 0.4f to 0.5f to prevent text wrapping
            float labelWidth = position.width * 0.5f;
            Rect mainLabelRect = new Rect(0f, currentY, labelWidth, mainLabelHeight);
            Widgets.LabelCacheHeight(ref mainLabelRect, "Currently researching");

            Text.Font = GameFont.Small;
            float techInfoX = labelWidth + 10f; // Tucked slightly closer to make room
            float techInfoWidth = position.width - techInfoX;

            TechLevel colonyTech = Faction.OfPlayer.def.techLevel;
            Rect colonyTechRect = new Rect(techInfoX, currentY, techInfoWidth * 0.5f, mainLabelHeight);

            DrawTechLevelText(colonyTechRect, "Faction: ", colonyTech);

            TechLevel worldTech = Find.World.worldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.IsPlayer)
                .Select(s => s.Faction.def.techLevel)
                .DefaultIfEmpty(TechLevel.Undefined)
                .Max();
            Rect worldTechRect = new Rect(techInfoX + techInfoWidth * 0.5f, currentY, techInfoWidth * 0.5f, mainLabelHeight);
            DrawTechLevelText(worldTechRect, "World: ", worldTech);

            GenUI.ResetLabelAlign();
            currentY += mainLabelHeight + 4f;

            // ==========================================
            // START SCROLL VIEW
            // ==========================================
            Rect scrollOutRect = new Rect(0f, currentY, position.width, position.height - (totalFooterHeight + currentY));
            Rect scrollViewRect = new Rect(0f, 0f, scrollOutRect.width - 20f, leftScrollViewHeight);
            Widgets.BeginScrollView(scrollOutRect, ref leftScrollPosition, scrollViewRect);
            currentY = 0f;

            // ==========================================
            // THE DASHBOARD (All Active Projects)
            // ==========================================
            List<ResearchProjectDef> activeProjects = new List<ResearchProjectDef>();
            if (hasActiveNonAnomalyResearch) activeProjects.Add(activeNonAnomalyProject);
            if (hasActiveAnomalyResearchBasic) activeProjects.Add(activeAnomalyProjectBasic);
            if (hasActiveAnomalyResearchAdvanced) activeProjects.Add(activeAnomalyProjectAdvanced);
            if (hasActiveGravshipResearch) activeProjects.Add(activeGravshipProject);

            if (activeProjects.Count > 0)
            {
                foreach (var activeProj in activeProjects)
                {
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

            // ==========================================
            // AVAILABLE STANDARD PROJECTS
            // ==========================================
            var groupedProjects = GroupByTechLevel(
                currentAvailableProjects.Where(p => ResearchTracker.GetCategoryKey(p) == "Standard" && p != activeNonAnomalyProject));

            bool hasStandardToShow = groupedProjects.Any(g => g.Any());

            if (hasStandardToShow || hasAnomalyToShowBasic || hasAnomalyToShowAdvanced || hasGravshipToShow)
            {
                Text.Font = GameFont.Medium;
                GUI.color = Color.white;
                Rect availableHeaderRect = new Rect(0f, currentY, scrollViewRect.width, 30f);
                Widgets.Label(availableHeaderRect, "CM_Semi_Random_Research_Available_Projects".Translate());
                currentY += 34f;
            }

            bool isFirst = true;
            foreach (var techGroup in groupedProjects)
            {
                if (!techGroup.Any()) continue;

                if (!isFirst) currentY += gapHeight;
                isFirst = false;

                float headerAnimProgress = 1f;
                if (techLevelHeaderProgress.TryGetValue(techGroup.Key, out float progress))
                    headerAnimProgress = progress;

                if (headerAnimProgress <= 0.01f) continue;

                Color originalColor = GUI.color;
                GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, headerAnimProgress);

                Text.Font = GameFont.Small;
                Color techColor = GetTechLevelColor(techGroup.Key);
                techColor.a *= headerAnimProgress;
                GUI.color = techColor;

                Rect headerRect = new Rect(0f, currentY, scrollViewRect.width, techLevelHeaderHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(headerRect, techGroup.Key.ToStringHuman().CapitalizeFirst());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = originalColor;
                currentY += techLevelHeaderHeight;

                foreach (ResearchProjectDef projectDef in techGroup.OrderBy(p => p.CostApparent))
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, projectDef, sharedCostColumnWidth);
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
                Widgets.Label(anomalyHeaderRect, "Available Dark research");
                GUI.color = Color.white;
                currentY += techLevelHeaderHeight;

                foreach (ResearchProjectDef projectDef in anomalyProjectsBasic.OrderBy(p => p.CostApparent))
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, projectDef, sharedCostColumnWidth);
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
                Widgets.Label(anomalyHeaderRect, "Available Advanced dark research");
                GUI.color = Color.white;
                currentY += techLevelHeaderHeight;

                foreach (ResearchProjectDef projectDef in anomalyProjectsAdvanced.OrderBy(p => p.CostApparent))
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, projectDef, sharedCostColumnWidth);
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
                Widgets.Label(gravshipHeaderRect, "Available Gravtech research");
                GUI.color = Color.white;
                currentY += techLevelHeaderHeight;

                foreach (ResearchProjectDef projectDef in gravshipProjects.OrderBy(p => p.CostApparent))
                {
                    Rect buttonRect = new Rect(0f, currentY, scrollViewRect.width, buttonHeight);
                    DrawResearchButton(ref buttonRect, projectDef, sharedCostColumnWidth);
                    currentY += buttonHeight + researchProjectGapHeight;
                }
            }

            leftScrollViewHeight = currentY;
            Widgets.EndScrollView();

            // ==========================================
            // FOOTER CONTROLS
            // ==========================================
            if (researchTracker != null)
            {

                // FIX: Force font back to small to prevent text size leakage from the animation skips
                Text.Font = GameFont.Small;

                Rect footerContainerRect = new Rect(0f, position.height - totalFooterHeight, position.width, totalFooterHeight);

                GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                Widgets.DrawLineHorizontal(footerContainerRect.x, footerContainerRect.y, footerContainerRect.width);
                GUI.color = Color.white;

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

                bool nodeInstalled = ResearchTabWindowSwitcher.NodeResearchInstalled;
                string treeButtonText = nodeInstalled ? "Node Research" : "Research Tree";

                // switch to Node Research if installed, otherwise just view the standard Research Tree
                if (ColoredButtonText(researchTreeButtonRect, treeButtonText, FooterTreeButtonColor))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();

                    if (nodeInstalled)
                    {
                        ResearchTabWindowSwitcher.SwitchToNodeResearch(this);
                    }
                    else
                    {
                        ResearchTabWindowSwitcher.OpenResearchWindow(typeof(MainTabWindow_Research), this);
                        SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    }

                    Event.current.Use();
                }

                if (nodeInstalled)
                {
                    TooltipHandler.TipRegion(researchTreeButtonRect, "Switch to Node Research (Passes control allowing free selection).");
                }
                else
                {
                    TooltipHandler.TipRegion(researchTreeButtonRect, "View the standard Research Tree (View only).");
                }

                if (showRerollButton)
                {
                    bool canReroll = researchTracker.CanReroll(rerollButtonType);
                    string rerollText = canReroll ? "Reroll" : "No rerolls";

                    if (canReroll)
                    {
                        if (ColoredButtonText(rerollButtonRect, rerollText, FooterRerollButtonColor))
                        {
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            SoundDefOf.TabOpen.PlayOneShotOnCamera();
                            researchTracker.Reroll(rerollButtonType);
                            lastRerollTime = Time.realtimeSinceStartup;
                        }
                    }
                    else
                    {
                        GUI.color = Color.grey;
                        Widgets.DrawAtlas(rerollButtonRect, Widgets.ButtonSubtleAtlas);
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(rerollButtonRect, rerollText);
                        Text.Anchor = TextAnchor.UpperLeft;
                        GUI.color = Color.white;
                    }
                }

                string researchButtonText = "Start Research";

                if (selectedProject == null)
                {
                    GUI.color = Color.grey;
                    Widgets.DrawAtlas(researchButtonRect, Widgets.ButtonSubtleAtlas);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(researchButtonRect, researchButtonText);
                }
                else if (selectedProject.IsFinished)
                {
                    GUI.color = Color.grey;
                    Widgets.DrawAtlas(researchButtonRect, Widgets.ButtonSubtleAtlas);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(researchButtonRect, "Finished");
                }
                else if (researchTracker != null && researchTracker.CurrentProject.Contains(selectedProject))
                {
                    GUI.color = ActiveProjectLabelColor;
                    Widgets.DrawAtlas(researchButtonRect, Widgets.ButtonSubtleAtlas);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(researchButtonRect, "In Progress");
                }
                else if (selectedProject.CanStartNow &&
                                         (Find.ResearchManager.GetProject(selectedProject?.knowledgeCategory) == null ||
                                         ResearchTracker.GetCategoryKey(selectedProject) == "Gravship" ||
                                         SemiRandomResearchMod.settings.allowSwitchingResearch))
                {
                    if (ColoredButtonText(researchButtonRect, researchButtonText, FooterStartButtonColor))
                    {
                        SoundDefOf.ResearchStart.PlayOneShotOnCamera();
                        Find.ResearchManager.SetCurrentProject(selectedProject);

                        string categoryKey = ResearchTracker.GetCategoryKey(selectedProject);
                        Current.Game.World.GetComponent<ResearchTracker>()?.SetCurrentProjectByKey(selectedProject, categoryKey);

                        TutorSystem.Notify_Event("StartResearchProject");
                        if (!ColonistsHaveResearchBench)
                        {
                            Messages.Message("MessageResearchMenuWithoutBench".Translate(), MessageTypeDefOf.CautionInput);
                        }
                    }
                }
                else
                {
                    GUI.color = Color.grey;
                    Widgets.DrawAtlas(researchButtonRect, Widgets.ButtonSubtleAtlas);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(researchButtonRect, "Locked");
                }

                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            GUI.EndGroup();
        }

        private static bool ColoredButtonText(Rect rect, string label, Color fill)
        {
            bool mouseOver = Mouse.IsOver(rect);
            bool held = mouseOver && Input.GetMouseButton(0);

            Color bg = fill;
            if (held)
                bg = fill * 0.75f;
            else if (mouseOver)
                bg = Color.Lerp(fill, Color.white, 0.16f);

            Widgets.DrawBoxSolid(rect, bg);

            Color old = GUI.color;
            GUI.color = Color.Lerp(fill, Color.black, 0.4f);
            Widgets.DrawBox(rect);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = old;

            return Widgets.ButtonInvisible(rect);
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
            float borderWidth = isMouseOver ? 1.5f : 1f;

            ResearchRateTracker rateTracker = Current.Game.World.GetComponent<ResearchRateTracker>();
            float globalAverageRate = rateTracker != null ? rateTracker.GetGlobalAverageRate() : 0f;
            bool hasGlobalRateData = globalAverageRate > 0f;

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

            Color backgroundColor = isMouseOver
                ? Color.Lerp(TexUI.AvailResearchColor, techColor, 0.4f)
                : Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);

            Color borderColor = selectedProject == projectDef ?
                TexUI.HighlightBorderResearchColor :
                (isMouseOver ? Color.Lerp(techColor, Color.white, 0.2f) : techColor);

            Color textColor = new Color(0.95f, 0.95f, 0.95f);

            ResearchTracker tracker = Current.Game.World.GetComponent<ResearchTracker>();
            bool isActive = tracker != null && tracker.CurrentProject.Contains(projectDef);

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

                if (isMouseOver || Mouse.IsOver(cancelRect))
                {
                    GUI.color = new Color(0.9f, 0.3f, 0.3f, animProgress * 0.8f);
                    Widgets.DrawBoxSolid(cancelRect, GUI.color);

                    GUI.color = new Color(1f, 1f, 1f, animProgress);
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
                            string categoryKey = ResearchTracker.GetCategoryKey(projectDef);
                            cancelTracker.SetCurrentProjectByKey(null, categoryKey);

                            cancelTracker.ForceAutoReseachCheckNextTick();
                            Event.current.Use();
                        }
                    }
                }
            }

            backgroundColor.a *= animProgress;
            Widgets.DrawBoxSolid(drawRect, backgroundColor);

            if (isMouseOver)
            {
                Color glowColor = techColor;
                glowColor.a = 0.1f * animProgress;
                Widgets.DrawBoxSolid(drawRect.ExpandedBy(2f), glowColor);
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

            if (progressFraction > 0f)
            {
                Rect progressRect = new Rect(drawRect.x, drawRect.y, drawRect.width * progressFraction, drawRect.height);

                Color progressColor = Color.Lerp(techColor, Color.white, 0.15f);
                progressColor.a = (isActive ? 0.6f : 0.45f) * animProgress;

                Widgets.DrawBoxSolid(progressRect, progressColor);
            }

            Color cardBorderColor = techColor;
            if (isActive)
            {
                cardBorderColor = Color.Lerp(techColor, Color.white, 0.5f); // Bright native color for active
            }
            else if (selectedProject == projectDef)
            {
                cardBorderColor = Color.Lerp(techColor, Color.white, 0.3f); // Semi-bright for selected
            }
            else if (isMouseOver)
            {
                cardBorderColor = Color.Lerp(techColor, Color.white, 0.2f);
            }
            cardBorderColor.a *= animProgress;

            Widgets.DrawLine(new Vector2(drawRect.x, drawRect.y), new Vector2(drawRect.xMax, drawRect.y), cardBorderColor, borderWidth);
            Widgets.DrawLine(new Vector2(drawRect.x, drawRect.yMax), new Vector2(drawRect.xMax, drawRect.yMax), cardBorderColor, borderWidth);
            Widgets.DrawLine(new Vector2(drawRect.x, drawRect.y), new Vector2(drawRect.x, drawRect.yMax), cardBorderColor, borderWidth);
            Widgets.DrawLine(new Vector2(drawRect.xMax, drawRect.y), new Vector2(drawRect.xMax, drawRect.yMax), cardBorderColor, borderWidth);

            Def firstUnlockable = GetFirstUnlockable(projectDef);
            try
            {
                if (firstUnlockable != null)
                    Widgets.DefIcon(iconRect, firstUnlockable);
            }
            catch (Exception ex)
            {
                Log.Message("[CM_Semi_Random_Research] Error rendering icon for " +
                    (firstUnlockable != null ? firstUnlockable.defName : " null"));
                Log.Message(ex);
            }

            Color lineSeparatorColor = techColor;
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

            Color usedTextColor = isActive ? ActiveProjectLabelColor : textColor;

            if (isMouseOver && !isActive)
            {
                usedTextColor = Color.white;  // Pure white for best visibility
                usedTextColor.a *= animProgress;
            }

            // --- NODE RESEARCH TECH INJECTIONS ---
            bool isFoundation = NodeResearch.IsFoundationTech(projectDef);
            bool isEmergence = NodeResearch.IsEmergenceTech(projectDef);

            if (isFoundation || isEmergence)
            {

                Rect topTextRect = new Rect(nameRect.x, nameRect.y + 2f, nameRect.width, 24f);
                Rect bottomTextRect = new Rect(nameRect.x, nameRect.y + 24f, nameRect.width, 20f);

                Text.Anchor = TextAnchor.LowerLeft;
                GUI.color = usedTextColor;
                Widgets.Label(topTextRect, projectDef.LabelCap);

                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;

                Color nodeTagColor = new Color(0.95f, 0.75f, 0.25f);
                nodeTagColor.a *= animProgress;
                GUI.color = nodeTagColor;

                if (isFoundation)
                {
                    Widgets.Label(bottomTextRect, "Foundation");
                }
                else if (isEmergence)
                {
                    Widgets.Label(bottomTextRect, "Emergence");
                }

                Text.Font = GameFont.Small;
            }
            else
            {
                GUI.color = usedTextColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(nameRect, projectDef.LabelCap);
            }

            GUI.color = originalColor;

            Text.Anchor = TextAnchor.MiddleCenter;
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(costRect, costText);
            Text.WordWrap = wordWrap;

            if (animProgress >= 0.7f && Widgets.ButtonInvisible(drawRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                selectedProject = projectDef;
            }

            if (selectedProject == projectDef)
            {
                Color highlightColor = Color.Lerp(techColor, Color.white, isActive ? 0.5f : 0.3f);
                highlightColor.a *= animProgress;
                DrawTransparentBox(drawRect, highlightColor, 10, true);
            }

            if (isMouseOver)
            {
                StringBuilder tooltipText = new StringBuilder();

                tooltipText.AppendLine(projectDef.LabelCap);
                tooltipText.AppendLine("Cost: " + projectDef.CostApparent);
                tooltipText.AppendLine("Tech Level: " + projectDef.techLevel.ToStringHuman());

                if (hasGlobalRateData)
                {
                    float remainingWork = projectDef.CostApparent - projectDef.ProgressApparent;
                    float estimatedDays = remainingWork / globalAverageRate;
                    tooltipText.AppendLine("Estimated time: " + ResearchRateTracker.FormatETA(estimatedDays));
                }

                var unlocks = UnlockedDefsGroupedByPrerequisites(projectDef);
                int unlockCount = 0;

                if (!unlocks.NullOrEmpty())
                {
                    foreach (var unlockGroup in unlocks)
                    {
                        unlockCount += unlockGroup.Second.Count;
                    }

                    tooltipText.AppendLine("Unlocks: " + unlockCount + " items");
                }

                if (isActive)
                {
                    tooltipText.AppendLine();
                    tooltipText.AppendLine("Currently researching");
                }

                TooltipHandler.TipRegion(drawRect, tooltipText.ToString());
            }

            GUI.color = originalColor;
            Text.Anchor = startingTextAnchor;

            drawRect = originalRect;
        }
    }
}

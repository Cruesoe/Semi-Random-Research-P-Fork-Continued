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
        private void DrawRightColumn(Rect rightRect)
        {
            Rect position = rightRect;
            GUI.BeginGroup(position);
            if (selectedProject != null)
            {
                float projectNameHeight = 50.0f;
                float gapHeight = 10.0f;

                float debugFinishResearchNowButtonHeight = 30.0f;
                float debugButtonGap = Prefs.DevMode ? debugFinishResearchNowButtonHeight + gapHeight : 0f;

                float currentY = 0f;

                Rect outRect = new Rect(0f, 0f, position.width, position.height - debugButtonGap);
                Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, rightScrollViewHeight);

                Widgets.BeginScrollView(outRect, ref rightScrollPosition, viewRect);

                Text.Font = GameFont.Medium;
                GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
                Rect projectNameRect = new Rect(0f, currentY, viewRect.width, projectNameHeight);
                Widgets.LabelCacheHeight(ref projectNameRect, selectedProject.LabelCap);
                GenUI.ResetLabelAlign();
                currentY += projectNameRect.height;

                Text.Font = GameFont.Small;
                Rect projectDescriptionRect = new Rect(0f, currentY, viewRect.width, 0f);
                Widgets.LabelCacheHeight(ref projectDescriptionRect, selectedProject.description);
                currentY += projectDescriptionRect.height;

                if ((int)selectedProject.techLevel > (int)Faction.OfPlayer.def.techLevel)
                {
                    float costMultiplier = selectedProject.CostFactor(Faction.OfPlayer.def.techLevel);
                    Rect techLevelMultilplierDescriptionRect = new Rect(0f, currentY, viewRect.width, 0f);
                    string text = "TechLevelTooLow".Translate(Faction.OfPlayer.def.techLevel.ToStringHuman(), selectedProject.techLevel.ToStringHuman(), (1f / costMultiplier).ToStringPercent());
                    if (costMultiplier != 1f)
                    {
                        text += " " + "ResearchCostComparison".Translate(selectedProject.baseCost.ToString("F0"), selectedProject.CostApparent.ToString("F0"));
                    }
                    Widgets.LabelCacheHeight(ref techLevelMultilplierDescriptionRect, text);
                    currentY += techLevelMultilplierDescriptionRect.height;
                }

                currentY += DrawResearchPrereqs(rect: new Rect(0f, currentY, viewRect.width, outRect.height), project: selectedProject);
                currentY += DrawResearchBenchRequirements(rect: new Rect(0f, currentY, viewRect.width, outRect.height), project: selectedProject);
                currentY += DrawStudyRequirements(rect: new Rect(0f, currentY, viewRect.width, outRect.height), project: selectedProject);

                Rect projectUnlockablesRect = new Rect(0f, currentY, viewRect.width, outRect.height);
                currentY += DrawUnlockableHyperlinks(projectUnlockablesRect, selectedProject);
                currentY += DrawContentSource(rect: new Rect(0f, currentY, viewRect.width, outRect.height), selectedProject);
                currentY = (rightScrollViewHeight = currentY + 3f);

                Widgets.EndScrollView();

                if (Prefs.DevMode && !selectedProject.IsFinished)
                {
                    Rect debugButtonRect = new Rect(
                        0f,
                        outRect.yMax + gapHeight,
                        120f,
                        debugFinishResearchNowButtonHeight
                    );

                    if (Widgets.ButtonText(debugButtonRect, "Debug: Finish now"))
                    {
                        Find.ResearchManager.SetCurrentProject(selectedProject);
                        Find.ResearchManager.FinishProject(selectedProject);

                        ResearchTracker researchTracker = Current.Game.World.GetComponent<ResearchTracker>();

                        string categoryKey = ResearchTracker.GetCategoryKey(selectedProject);
                        researchTracker.SetCurrentProjectByKey(selectedProject, categoryKey);

                        researchTracker.ConsiderProjectFinished(selectedProject);
                        researchTracker.GetCurrentlyAvailableProjects();
                    }
                }
            }

            GUI.EndGroup();
        }

        private float DrawResearchPrereqs(ResearchProjectDef project, Rect rect)
        {
            if (project.prerequisites.NullOrEmpty() && (project.hiddenPrerequisites == null || project.hiddenPrerequisites.Count == 0))
            {
                return 0f;
            }
            float xMin = rect.xMin;
            float yMin = rect.yMin;

            Text.Font = GameFont.Medium;
            Widgets.LabelCacheHeight(ref rect, "Prerequisites".Translate() + ":");
            rect.yMin += rect.height + 6f; // Add extra padding after header

            Text.Font = GameFont.Small;

            List<ResearchProjectDef> allPrereqs = new List<ResearchProjectDef>();

            if (project.prerequisites != null)
                allPrereqs.AddRange(project.prerequisites);

            if (project.hiddenPrerequisites != null)
                allPrereqs.AddRange(project.hiddenPrerequisites);

            float itemHeight = 42f;  // Taller rows for prerequisites
            float iconSize = 28f;    // Icon size
            float iconPadding = 8f;  // Padding after icon

            foreach (ResearchProjectDef prereq in allPrereqs)
            {
                Rect prereqRect = new Rect(rect.xMin + 6f, rect.yMin, rect.width - 6f, itemHeight);

                Color techColor = GetTechLevelColor(prereq.techLevel);
                Color bgColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
                Color borderColor = techColor;

                Widgets.DrawBoxSolid(prereqRect, bgColor);
                DrawTransparentBox(prereqRect, borderColor, 1f);

                Rect iconRect = new Rect(
                    prereqRect.x + 6f,
                    prereqRect.y + (itemHeight - iconSize) / 2,
                    iconSize,
                    iconSize
                );

                Rect labelRect = new Rect(
                    iconRect.xMax + iconPadding,
                    prereqRect.y,
                    prereqRect.width - iconRect.width - (iconPadding * 2) - 6f,
                    itemHeight
                );

                Def firstUnlockable = null;
                try
                {
                    var unlockables = UnlockedDefsGroupedByPrerequisites(prereq);
                    if (!unlockables.NullOrEmpty() && !unlockables[0].Second.NullOrEmpty())
                    {
                        firstUnlockable = unlockables[0].Second[0];
                        Widgets.DefIcon(iconRect, firstUnlockable);
                    }
                }
                catch (Exception)
                {
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.white; // Clear white text
                Widgets.Label(labelRect, prereq.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(prereqRect))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    selectedProject = prereq;
                }

                rect.yMin += itemHeight + 4f; // Add spacing between prerequisites
            }

            GUI.color = Color.white;
            rect.xMin = xMin;

            rect.yMin += 6f;

            return rect.yMin - yMin;
        }

        private float DrawResearchBenchRequirements(ResearchProjectDef project, Rect rect)
        {
            float xMin = rect.xMin;
            float yMin = rect.yMin;
            if (project.requiredResearchBuilding != null)
            {
                List<Map> maps = Find.Maps;
                Widgets.LabelCacheHeight(ref rect, "RequiredResearchBench".Translate() + ":");
                rect.xMin += 6f;
                rect.yMin += rect.height;
                GUI.color = FulfilledPrerequisiteColor;
                rect.height = Text.CalcHeight(project.requiredResearchBuilding.LabelCap, rect.width - 24f - 6f);
                Widgets.HyperlinkWithIcon(rect, new Dialog_InfoCard.Hyperlink(project.requiredResearchBuilding));
                rect.yMin += rect.height + 4f;
                GUI.color = Color.white;
                rect.xMin = xMin;
            }
            if (!project.requiredResearchFacilities.NullOrEmpty())
            {
                Widgets.LabelCacheHeight(ref rect, "RequiredResearchBenchFacilities".Translate() + ":");
                rect.yMin += rect.height;
                Building_ResearchBench building_ResearchBench = FindBenchFulfillingMostRequirements(project.requiredResearchBuilding, project.requiredResearchFacilities);
                CompAffectedByFacilities bestMatchingBench = null;
                if (building_ResearchBench != null)
                {
                    bestMatchingBench = building_ResearchBench.TryGetComp<CompAffectedByFacilities>();
                }
                rect.xMin += 6f;
                for (int j = 0; j < project.requiredResearchFacilities.Count; j++)
                {
                    DrawResearchBenchFacilityRequirement(project.requiredResearchFacilities[j], bestMatchingBench, project, ref rect);
                    rect.yMin += rect.height;
                }
                rect.yMin += 4f;
            }
            GUI.color = Color.white;
            rect.xMin = xMin;
            return rect.yMin - yMin;
        }

        private float DrawStudyRequirements(ResearchProjectDef project, Rect rect)
        {
            float yMin = rect.yMin;
            if (project.RequiredAnalyzedThingCount > 0)
            {
                Widgets.LabelCacheHeight(ref rect, "StudyRequirements".Translate() + ":");
                rect.xMin += 6f;
                rect.yMin += rect.height;
                foreach (ThingDef item in project.requiredAnalyzed)
                {
                    Rect rect2 = new Rect(rect.x, rect.yMin, rect.width, 24f);
                    Color? color = null;
                    Dialog_InfoCard.Hyperlink hyperlink = new Dialog_InfoCard.Hyperlink(item);
                    Widgets.HyperlinkWithIcon(rect2, hyperlink, null, 2f, 6f, color, truncateLabel: false);
                    rect.yMin += 24f;
                }
            }
            return rect.yMin - yMin;
        }

        private Building_ResearchBench FindBenchFulfillingMostRequirements(ThingDef requiredResearchBench, List<ThingDef> requiredFacilities)
        {
            tmpAllBuildings.Clear();
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                tmpAllBuildings.AddRange(maps[i].listerBuildings.allBuildingsColonist);
            }
            float num = 0f;
            Building_ResearchBench building_ResearchBench = null;
            for (int j = 0; j < tmpAllBuildings.Count; j++)
            {
                Building_ResearchBench building_ResearchBench2 = tmpAllBuildings[j] as Building_ResearchBench;
                if (building_ResearchBench2 != null && (requiredResearchBench == null || building_ResearchBench2.def == requiredResearchBench))
                {
                    float researchBenchRequirementsScore = GetResearchBenchRequirementsScore(building_ResearchBench2, requiredFacilities);
                    if (building_ResearchBench == null || researchBenchRequirementsScore > num)
                    {
                        num = researchBenchRequirementsScore;
                        building_ResearchBench = building_ResearchBench2;
                    }
                }
            }
            tmpAllBuildings.Clear();
            return building_ResearchBench;
        }

        private void DrawResearchBenchFacilityRequirement(ThingDef requiredFacility, CompAffectedByFacilities bestMatchingBench, ResearchProjectDef project, ref Rect rect)
        {
            Thing thing = null;
            Thing thing2 = null;
            if (bestMatchingBench != null)
            {
                thing = bestMatchingBench.LinkedFacilitiesListForReading.Find((Thing x) => x.def == requiredFacility);
                thing2 = bestMatchingBench.LinkedFacilitiesListForReading.Find((Thing x) => x.def == requiredFacility && bestMatchingBench.IsFacilityActive(x));
            }
            GUI.color = FulfilledPrerequisiteColor;
            string text = requiredFacility.LabelCap;
            if (thing != null && thing2 == null)
            {
                text += " (" + "InactiveFacility".Translate() + ")";
            }
            rect.height = Text.CalcHeight(text, rect.width - 24f - 6f);
            Widgets.HyperlinkWithIcon(rect, new Dialog_InfoCard.Hyperlink(requiredFacility), text);
        }

        private float GetResearchBenchRequirementsScore(Building_ResearchBench bench, List<ThingDef> requiredFacilities)
        {
            float num = 0f;
            for (int i = 0; i < requiredFacilities.Count; i++)
            {
                CompAffectedByFacilities benchComp = bench.GetComp<CompAffectedByFacilities>();
                if (benchComp != null)
                {
                    List<Thing> linkedFacilitiesListForReading = benchComp.LinkedFacilitiesListForReading;
                    if (linkedFacilitiesListForReading.Find((Thing x) => x.def == requiredFacilities[i] && benchComp.IsFacilityActive(x)) != null)
                    {
                        num += 1f;
                    }
                    else if (linkedFacilitiesListForReading.Find((Thing x) => x.def == requiredFacilities[i]) != null)
                    {
                        num += 0.6f;
                    }
                }
            }
            return num;
        }

        private Def GetFirstUnlockable(ResearchProjectDef project)
        {
            List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>> list = UnlockedDefsGroupedByPrerequisites(project);

            if (list.NullOrEmpty())
                return null;

            List<Def> defList = list.First().Second;
            if (defList.NullOrEmpty())
                return null;

            int randomIndex = Rand.RangeInclusiveSeeded(0, defList.Count - 1, currentRandomSeed);

            return defList[randomIndex];
        }

        private float DrawUnlockableHyperlinks(Rect rect, ResearchProjectDef project)
        {
            List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>> list = UnlockedDefsGroupedByPrerequisites(project);

            if (list.NullOrEmpty())
            {
                if (errorDetected)
                {
                    GUI.color = Color.red;
                    Widgets.LabelCacheHeight(ref rect, "ERROR DETECTED: Check devlog for more information");
                    GUI.color = Color.white;
                    return rect.height;
                }
                return 0f;
            }
            float yMin = rect.yMin;
            float x = rect.x;

            Text.Font = GameFont.Medium;

            foreach (Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>> item in list)
            {
                ResearchPrerequisitesUtility.UnlockedHeader first = item.First;
                rect.x = x;

                if (!first.unlockedBy.Any())
                {
                    Widgets.LabelCacheHeight(ref rect, "Unlocks".Translate() + ":");
                }
                else
                {
                    Widgets.LabelCacheHeight(ref rect, string.Concat("UnlockedWith".Translate(), " ", HeaderLabel(first), ":"));
                }

                rect.x += 6f;
                rect.yMin += rect.height + 8f; // More padding after header

                Text.Font = GameFont.Small;

                bool useDoubleColumns = item.Second.Count > 8;
                float originalWidth = rect.width - 12f;
                float columnWidth = useDoubleColumns ? (originalWidth / 2) - 6f : originalWidth;
                float columnSpacing = useDoubleColumns ? 12f : 0f;
                float originalX = rect.x;
                float startingY = rect.yMin; // Store the starting Y position after the header
                float maxYColumn = rect.yMin;
                int columnCount = 0;

                foreach (Def item2 in item.Second)
                {
                    float itemHeight = 48f; // Much bigger than the original 24f

                    if (useDoubleColumns && columnCount >= (int)Math.Ceiling(item.Second.Count / 2.0f))
                    {
                        if (columnCount == (int)Math.Ceiling(item.Second.Count / 2.0f))
                        {
                            rect.x = originalX + columnWidth + columnSpacing;
                            rect.yMin = startingY; // Reset Y to starting position of the first item
                        }
                    }

                    Rect itemRect = new Rect(rect.x, rect.yMin, columnWidth, itemHeight);

                    bool isMouseOver = Mouse.IsOver(itemRect);

                    Color bgColor = isMouseOver
                        ? new Color(0.3f, 0.3f, 0.3f, 0.3f)
                        : new Color(0.1f, 0.1f, 0.1f, 0.1f);
                    Widgets.DrawBoxSolid(itemRect, bgColor);

                    Color borderColor = isMouseOver
                        ? new Color(0.8f, 0.8f, 0.8f, 0.5f)
                        : new Color(0.4f, 0.4f, 0.4f, 0.3f);
                    DrawTransparentBox(itemRect, borderColor, isMouseOver ? 1.5f : 1f);

                    Dialog_InfoCard.Hyperlink hyperlink = new Dialog_InfoCard.Hyperlink(item2);

                    Rect iconRect = new Rect(itemRect.x + 6f, itemRect.y + (itemHeight - 32f) / 2, 32f, 32f);
                    Rect labelRect = new Rect(iconRect.xMax + 12f, itemRect.y, itemRect.width - iconRect.width - 24f, itemHeight);

                    try
                    {
                        GUI.color = isMouseOver ? Color.white : new Color(0.9f, 0.9f, 0.9f);
                        Widgets.DefIcon(iconRect, item2);

                        Text.Anchor = TextAnchor.MiddleLeft;
                        GUI.color = isMouseOver ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                        string label = item2.LabelCap;
                        Widgets.Label(labelRect, label);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[CM_Semi_Random_Research] Error rendering icon for " + item2.defName + ": " + ex);

                        Widgets.HyperlinkWithIcon(itemRect, hyperlink);
                    }

                    if (Widgets.ButtonInvisible(itemRect))
                    {
                        hyperlink.ActivateHyperlink();
                    }

                    rect.yMin += itemHeight + 8f;

                    if (rect.yMin > maxYColumn)
                        maxYColumn = rect.yMin;

                    columnCount++;
                }

                if (useDoubleColumns)
                {
                    rect.yMin = maxYColumn;
                    rect.x = originalX;
                }

                rect.yMin += 16f;
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            return rect.yMin - yMin;
        }

        private float DrawContentSource(Rect rect, ResearchProjectDef project)
        {
            if (project.modContentPack == null || project.modContentPack.IsCoreMod)
            {
                return 0f;
            }
            float yMin = rect.yMin;
            TaggedString taggedString = "Stat_Source_Label".Translate() + ":  " + project.modContentPack.Name;
            Widgets.LabelCacheHeight(ref rect, taggedString.Colorize(Color.grey));
            ExpansionDef expansionDef = ModLister.AllExpansions.Find((ExpansionDef e) => e.linkedMod == project.modContentPack.PackageId);
            if (expansionDef != null)
            {
                GUI.DrawTexture(new Rect(Text.CalcSize(taggedString).x + 4f, rect.y, 20f, 20f), expansionDef.IconFromStatus);
            }
            return rect.yMax - yMin;
        }

        private string HeaderLabel(ResearchPrerequisitesUtility.UnlockedHeader headerProject)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string value = "";
            for (int i = 0; i < headerProject.unlockedBy.Count; i++)
            {
                ResearchProjectDef researchProjectDef = headerProject.unlockedBy[i];
                string text = researchProjectDef.LabelCap;
                stringBuilder.Append(text).Append(value);
                value = ", ";
            }
            return stringBuilder.ToString();
        }

        private List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>> UnlockedDefsGroupedByPrerequisites(ResearchProjectDef project)
        {
            if (cachedUnlockedDefsGroupedByPrerequisites == null)
            {
                cachedUnlockedDefsGroupedByPrerequisites = new Dictionary<ResearchProjectDef, List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>>();
            }
            List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>> value = new List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>();
            if (project != null && !cachedUnlockedDefsGroupedByPrerequisites.TryGetValue(project, out value))
            {
                // Seems that this function call can throw a NullReferenceException. This is not the problem of this mod, but the reports by people seeing SemiRandomResearch is in the stack trace is.
                try
                {
                    value = ResearchPrerequisitesUtility.UnlockedDefsGroupedByPrerequisites(project);
                }
                catch (NullReferenceException nullex)
                {
                    errorDetected = true;

                    Log.Error("[CM_Semi_Random_Research] Error while gathering information which research unlocks which items. " + (project == null ? " Function was called with null as parameter. This is a bug." : "This can indicate issues with your modpack. Do not report to Semi Random Research until you have confirmed that there is no error when opening the research screen without semi random research installed!"));
                    var erroringRecepies = DefDatabase<RecipeDef>.AllDefs.Where(x => x?.products == null || x.products.Any(y => y?.thingDef == null));
                    if (erroringRecepies.Any())
                    {
                        RecipeDef broken = erroringRecepies.RandomElement();
                        string errorRecipeInformation = (broken?.modContentPack?.Name != null ? (" Most likely from mod : " + broken?.modContentPack?.Name) : "") + (broken?.modContentPack?.PackageId != null ? " Suspected id of the mod that added the broken recipe: " + broken?.modContentPack?.PackageId : "");
                        Log.Error("[CM_Semi_Random_Research] Detected broken recepies! One of the broken recipes has the lable: " + broken?.label + " with DefName " + broken?.defName + errorRecipeInformation);
                    }
                    if (DefDatabase<ThingDef>.AllDefs.Any(x => x == null))
                    {
                        Log.Error("[CM_Semi_Random_Research] Detected null Thingdefs");
                    }
                    if (DefDatabase<TerrainDef>.AllDefs.Any(x => x == null))
                    {
                        Log.Error("[CM_Semi_Random_Research] Detected null TerrainDef");
                    }
                    value = new List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>();
                    Log.Error(nullex.StackTrace);
                }
                cachedUnlockedDefsGroupedByPrerequisites.Add(project, value);
            }
            return value;
        }
    }
}

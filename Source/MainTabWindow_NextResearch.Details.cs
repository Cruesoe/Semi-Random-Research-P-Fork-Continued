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
        private void DrawRightColumn(Rect rightRect)
        {
            Rect position = rightRect;
            GUI.BeginGroup(position);
            try
            {
            if (selectedProject != null)
            {
                float projectNameHeight = 50.0f;

                float currentY = 0f;

                Rect outRect = new Rect(0f, 0f, position.width, position.height);
                if (Event.current.type == EventType.Layout)
                    rightScrollHeightForFrame = rightScrollViewHeight;
                Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(rightScrollHeightForFrame, 1f));

                Widgets.BeginScrollView(outRect, ref rightScrollPosition, viewRect);
                try
                {

                Text.Font = GameFont.Medium;
                GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
                Rect projectNameRect = new Rect(0f, currentY, viewRect.width, projectNameHeight);
                Widgets.LabelCacheHeight(ref projectNameRect, SafeLabel(selectedProject));
                GenUI.ResetLabelAlign();
                currentY += projectNameRect.height;

                Text.Font = GameFont.Small;
                Rect projectDescriptionRect = new Rect(0f, currentY, viewRect.width, 0f);
                Widgets.LabelCacheHeight(ref projectDescriptionRect, SafeDescription(selectedProject));
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
                    Widgets.LabelCacheHeight(ref techLevelMultilplierDescriptionRect, text ?? string.Empty);
                    currentY += techLevelMultilplierDescriptionRect.height;
                }

                currentY += DrawResearchPrereqs(rect: new Rect(0f, currentY, viewRect.width, outRect.height), project: selectedProject);
                currentY += DrawResearchBenchRequirements(rect: new Rect(0f, currentY, viewRect.width, outRect.height), project: selectedProject);
                currentY += DrawStudyRequirements(rect: new Rect(0f, currentY, viewRect.width, outRect.height), project: selectedProject);

                Rect projectUnlockablesRect = new Rect(0f, currentY, viewRect.width, outRect.height);
                currentY += DrawUnlockableHyperlinks(projectUnlockablesRect, selectedProject);
                currentY += DrawContentSource(rect: new Rect(0f, currentY, viewRect.width, outRect.height), selectedProject);
                currentY += DrawDependentResearch(rect: new Rect(0f, currentY, viewRect.width, outRect.height), project: selectedProject);
                currentY += 3f;
                if (Event.current.type == EventType.Layout)
                    rightScrollViewHeight = currentY;
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }
            }
            finally
            {
                GUI.EndGroup();
            }
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
            rect.yMin += rect.height + 6f;

            Text.Font = GameFont.Small;

            if (project.prerequisites != null)
            {
                for (int i = 0; i < project.prerequisites.Count; i++)
                    DrawPrereqRow(project.prerequisites[i], ref rect);
            }

            if (project.hiddenPrerequisites != null)
            {
                for (int i = 0; i < project.hiddenPrerequisites.Count; i++)
                    DrawPrereqRow(project.hiddenPrerequisites[i], ref rect);
            }

            GUI.color = Color.white;
            rect.xMin = xMin;
            rect.yMin += 6f;
            return rect.yMin - yMin;
        }

        private void DrawPrereqRow(ResearchProjectDef prereq, ref Rect rect)
        {
            if (prereq == null)
                return;

            const float itemHeight = 42f;
            const float iconSize = 28f;
            const float iconPadding = 8f;
            Rect prereqRect = new Rect(rect.xMin + 6f, rect.yMin, rect.width - 6f, itemHeight);

            Color techColor = (SemiRandomResearchMod.settings == null || SemiRandomResearchMod.settings.colorAndGroupByTechLevel)
                ? GetTechLevelColor(prereq.techLevel)
                : TexUI.AvailResearchColor;
            Color bgColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(prereqRect, bgColor);
                DrawTransparentBox(prereqRect, techColor, 1f);
            }

            Rect iconRect = new Rect(
                prereqRect.x + 6f,
                prereqRect.y + (itemHeight - iconSize) / 2f,
                iconSize,
                iconSize
            );
            Rect labelRect = new Rect(
                iconRect.xMax + iconPadding,
                prereqRect.y,
                prereqRect.width - iconRect.width - (iconPadding * 2f) - 6f,
                itemHeight
            );

            if (IsRepaint)
            {
                Def firstUnlockable = GetFirstUnlockable(prereq);
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
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(labelRect, SafeLabel(prereq));
            Text.Anchor = TextAnchor.UpperLeft;

            if (Clicked(prereqRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                SelectRelatedProject(prereq);
            }

            rect.yMin += itemHeight + 4f;
        }

        private float DrawDependentResearch(ResearchProjectDef project, Rect rect)
        {
            List<ResearchProjectDef> dependents = GetDependentResearch(project);
            if (dependents.NullOrEmpty())
                return 0f;

            int unfinishedCount = 0;
            for (int i = 0; i < dependents.Count; i++)
            {
                if (dependents[i] != null && !dependents[i].IsFinished)
                    unfinishedCount++;
            }
            if (unfinishedCount == 0)
                return 0f;

            float yMin = rect.yMin;
            Text.Font = GameFont.Medium;
            Widgets.LabelCacheHeight(ref rect, "CM_Semi_Random_Research_RequiredForResearch".Translate() + ":");
            rect.yMin += rect.height + 6f;

            Text.Font = GameFont.Small;
            for (int i = 0; i < dependents.Count; i++)
            {
                ResearchProjectDef dependent = dependents[i];
                if (dependent == null || dependent.IsFinished)
                    continue;

                DrawDependentResearchRow(project, dependent, ref rect);
            }

            rect.yMin += 6f;
            return rect.yMin - yMin;
        }

        private void DrawDependentResearchRow(ResearchProjectDef prerequisite, ResearchProjectDef dependent, ref Rect rect)
        {
            const float itemHeight = 42f;
            const float iconSize = 28f;
            const float iconPadding = 8f;
            Rect rowRect = new Rect(rect.xMin + 6f, rect.yMin, rect.width - 6f, itemHeight);

            Color techColor = GetCategoryColor(dependent);
            Color bgColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
            if (IsRepaint)
            {
                Widgets.DrawBoxSolid(rowRect, bgColor);
                DrawTransparentBox(rowRect, techColor, 1f);
            }

            Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + (itemHeight - iconSize) / 2f, iconSize, iconSize);
            Rect labelRect = new Rect(iconRect.xMax + iconPadding, rowRect.y,
                rowRect.width - iconRect.width - (iconPadding * 2f) - 6f, itemHeight);

            if (IsRepaint)
            {
                Def firstUnlockable = GetFirstUnlockable(dependent);
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
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(labelRect, SafeLabel(dependent));
            Text.Anchor = TextAnchor.UpperLeft;

            if (Clicked(rowRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                SelectRelatedProject(dependent);
            }

            string remaining = GetOtherUnfinishedPrerequisites(dependent, prerequisite);
            TooltipHandler.TipRegion(rowRect, remaining.NullOrEmpty()
                ? "CM_Semi_Random_Research_FinalResearchPrerequisite".Translate()
                : "CM_Semi_Random_Research_AlsoRequires".Translate(remaining));

            rect.yMin += itemHeight + 4f;
        }

        private string GetOtherUnfinishedPrerequisites(ResearchProjectDef dependent, ResearchProjectDef shownPrerequisite)
        {
            List<string> labels = new List<string>();
            AddOtherUnfinishedPrerequisites(dependent.prerequisites, shownPrerequisite, labels);
            AddOtherUnfinishedPrerequisites(dependent.hiddenPrerequisites, shownPrerequisite, labels);
            return string.Join(", ", labels.ToArray());
        }

        private void AddOtherUnfinishedPrerequisites(List<ResearchProjectDef> prerequisites,
            ResearchProjectDef shownPrerequisite, List<string> labels)
        {
            if (prerequisites.NullOrEmpty())
                return;

            for (int i = 0; i < prerequisites.Count; i++)
            {
                ResearchProjectDef prerequisite = prerequisites[i];
                if (prerequisite == null || prerequisite == shownPrerequisite || prerequisite.IsFinished)
                    continue;

                string label = SafeLabel(prerequisite);
                if (!labels.Contains(label))
                    labels.Add(label);
            }
        }

        private float DrawResearchBenchRequirements(ResearchProjectDef project, Rect rect)
        {
            float xMin = rect.xMin;
            float yMin = rect.yMin;
            if (project.requiredResearchBuilding != null)
            {
                Widgets.LabelCacheHeight(ref rect, "RequiredResearchBench".Translate() + ":");
                rect.xMin += 6f;
                rect.yMin += rect.height;
                GUI.color = FulfilledPrerequisiteColor;
                rect.height = Text.CalcHeight(SafeDefLabel(project.requiredResearchBuilding), rect.width);
                DrawDefLink(rect, project.requiredResearchBuilding, SafeDefLabel(project.requiredResearchBuilding));
                rect.yMin += rect.height + 4f;
                GUI.color = Color.white;
                rect.xMin = xMin;
            }
            if (!project.requiredResearchFacilities.NullOrEmpty())
            {
                Widgets.LabelCacheHeight(ref rect, "RequiredResearchBenchFacilities".Translate() + ":");
                rect.yMin += rect.height;
                Building_ResearchBench building_ResearchBench = cachedMatchingBench;
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
                    DrawDefLink(rect2, item, SafeDefLabel(item));
                    rect.yMin += 24f;
                }
            }
            return rect.yMin - yMin;
        }

        private Def GetFirstUnlockable(ResearchProjectDef project)
        {
            if (project == null)
                return null;

            cachedFirstUnlockable.TryGetValue(project, out Def cached);
            return cached;
        }

        private float DrawUnlockableHyperlinks(Rect rect, ResearchProjectDef project)
        {
            List<Def> unlocked = project == cachedUnlocksProject ? cachedSelectedUnlocks : null;
            if (unlocked.NullOrEmpty())
            {
                return 0f;
            }

            float yMin = rect.yMin;
            float x = rect.x;

            Text.Font = GameFont.Small;
            Widgets.LabelCacheHeight(ref rect, "Unlocks".Translate() + ":");
            rect.yMin += rect.height + 6f;

            int visibleDrawn = 0;
            const int maxVisibleIcons = 20;
            const float itemHeight = 48f;
            for (int i = 0; i < unlocked.Count; i++)
            {
                Def def = unlocked[i];
                if (def == null)
                    continue;

                Rect itemRect = new Rect(rect.x, rect.yMin, rect.width, itemHeight);
                bool isMouseOver = Mouse.IsOver(itemRect);
                if (IsRepaint)
                {
                    Widgets.DrawBoxSolid(itemRect, isMouseOver
                        ? new Color(0.3f, 0.3f, 0.3f, 0.3f)
                        : new Color(0.1f, 0.1f, 0.1f, 0.1f));
                    DrawTransparentBox(itemRect, isMouseOver
                        ? new Color(0.8f, 0.8f, 0.8f, 0.5f)
                        : new Color(0.4f, 0.4f, 0.4f, 0.3f), isMouseOver ? 1.5f : 1f);
                }

                DrawDefLink(itemRect, def, SafeDefLabel(def), 32f);
                visibleDrawn++;
                if (visibleDrawn >= maxVisibleIcons)
                {
                    rect.yMin += itemHeight + 8f;
                    Widgets.Label(new Rect(rect.x, rect.yMin, rect.width, 24f), "CM_Semi_Random_Research_MoreUnlocks".Translate(unlocked.Count - i - 1));
                    rect.yMin += 26f;
                    break;
                }

                rect.yMin += itemHeight + 8f;
            }

            rect.x = x;
            GUI.color = Color.white;
            return rect.yMin - yMin;
        }

        private void DrawDefLink(Rect rect, Def def, string text, float iconSize = 24f)
        {
            Rect iconRect = new Rect(rect.x + 6f, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
            Rect labelRect = new Rect(iconRect.xMax + 12f, rect.y, rect.width - iconRect.width - 24f, rect.height);

            if (IsRepaint && def != null)
            {
                try
                {
                    GUI.color = Mouse.IsOver(rect) ? Color.white : new Color(0.9f, 0.9f, 0.9f);
                    Widgets.DefIcon(iconRect, def);
                }
                catch (Exception)
                {
                }
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Mouse.IsOver(rect) ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(labelRect, text ?? string.Empty);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (Clicked(rect) && def != null)
            {
                Find.WindowStack.Add(new Dialog_InfoCard(def));
            }
        }

        private void DrawResearchBenchFacilityRequirement(ThingDef requiredFacility, CompAffectedByFacilities bestMatchingBench, ResearchProjectDef project, ref Rect rect)
        {
            // Plain loop: this is drawn every frame, and List.Find with these two capturing
            // lambdas allocated a closure and a delegate per facility per pass.
            Thing thing = null;
            Thing thing2 = null;
            if (bestMatchingBench != null)
            {
                List<Thing> linked = bestMatchingBench.LinkedFacilitiesListForReading;
                for (int i = 0; i < linked.Count; i++)
                {
                    Thing facility = linked[i];
                    if (facility.def != requiredFacility)
                        continue;

                    if (thing == null)
                        thing = facility;
                    if (thing2 == null && bestMatchingBench.IsFacilityActive(facility))
                        thing2 = facility;
                    if (thing2 != null)
                        break;
                }
            }
            GUI.color = FulfilledPrerequisiteColor;
            string text = SafeDefLabel(requiredFacility);
            if (thing != null && thing2 == null)
            {
                text += " (" + "InactiveFacility".Translate() + ")";
            }
            rect.height = Text.CalcHeight(text, rect.width);
            DrawDefLink(rect, requiredFacility, text);
        }

        private float GetResearchBenchRequirementsScore(Building_ResearchBench bench, List<ThingDef> requiredFacilities)
        {
            // The comp lookup was repeated per required facility, and each of the two Find calls
            // built a closure over both loop variables. One lookup, plain loops.
            CompAffectedByFacilities benchComp = bench.GetComp<CompAffectedByFacilities>();
            if (benchComp == null)
                return 0f;

            List<Thing> linkedFacilities = benchComp.LinkedFacilitiesListForReading;
            float num = 0f;
            for (int i = 0; i < requiredFacilities.Count; i++)
            {
                ThingDef required = requiredFacilities[i];
                bool present = false;
                bool active = false;
                for (int j = 0; j < linkedFacilities.Count; j++)
                {
                    Thing facility = linkedFacilities[j];
                    if (facility.def != required)
                        continue;

                    present = true;
                    if (benchComp.IsFacilityActive(facility))
                    {
                        active = true;
                        break;
                    }
                }

                if (active)
                    num += 1f;
                else if (present)
                    num += 0.6f;
            }
            return num;
        }

        // Package id to expansion, resolved once per mod. The Find below ran every frame with a
        // fresh closure over the project just to draw one icon.
        private static readonly Dictionary<string, ExpansionDef> expansionByPackageId =
            new Dictionary<string, ExpansionDef>();

        private static ExpansionDef ExpansionForPackageId(string packageId)
        {
            if (packageId == null)
                return null;
            if (expansionByPackageId.TryGetValue(packageId, out ExpansionDef cached))
                return cached;

            ExpansionDef found = null;
            List<ExpansionDef> expansions = ModLister.AllExpansions;
            for (int i = 0; i < expansions.Count; i++)
            {
                if (expansions[i].linkedMod == packageId)
                {
                    found = expansions[i];
                    break;
                }
            }

            expansionByPackageId[packageId] = found;
            return found;
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
            ExpansionDef expansionDef = ExpansionForPackageId(project.modContentPack.PackageId);
            if (expansionDef != null)
            {
                GUI.DrawTexture(new Rect(Text.CalcSize(taggedString).x + 4f, rect.y, 20f, 20f), expansionDef.IconFromStatus);
            }
            return rect.yMax - yMin;
        }
    }
}

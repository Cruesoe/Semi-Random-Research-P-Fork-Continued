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

                if (relatedProjectHistory.Count > 0)
                {
                    const float backButtonHeight = 32f;
                    ResearchProjectDef previousProject = relatedProjectHistory.Peek();
                    Rect backButtonRect = new Rect(0f, currentY, viewRect.width, backButtonHeight);
                    if (ColoredButtonText(backButtonRect,
                        "CM_Semi_Random_Research_BackToResearch".Translate(SafeLabel(previousProject)),
                        FooterTreeButtonColor, SkinTone.Blue))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        ReturnToPreviousRelatedProject();
                    }
                    currentY += backButtonHeight + 8f;
                }

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

        // Only unfinished prerequisites are listed; the section is left out once all are done.
        private float DrawResearchPrereqs(ResearchProjectDef project, Rect rect)
        {
            if (!HasUnfinished(project.prerequisites) && !HasUnfinished(project.hiddenPrerequisites))
            {
                return 0f;
            }
            float yMin = rect.yMin;

            int rows = CountUnfinished(project.prerequisites) + CountUnfinished(project.hiddenPrerequisites);
            Rect rowsRect = SectionBegin(rect, "Prerequisites".Translate(), "lock", rows * (ListRowHeight + 4f) - 4f, GameFont.Medium);
            rect = new Rect(rowsRect.x - 6f, rowsRect.y, rowsRect.width + 6f, rect.height);

            if (project.prerequisites != null)
            {
                for (int i = 0; i < project.prerequisites.Count; i++)
                {
                    if (project.prerequisites[i] != null && !project.prerequisites[i].IsFinished)
                        DrawPrereqRow(project.prerequisites[i], ref rect);
                }
            }

            if (project.hiddenPrerequisites != null)
            {
                for (int i = 0; i < project.hiddenPrerequisites.Count; i++)
                {
                    if (project.hiddenPrerequisites[i] != null && !project.hiddenPrerequisites[i].IsFinished)
                        DrawPrereqRow(project.hiddenPrerequisites[i], ref rect);
                }
            }

            GUI.color = Color.white;
            return SectionEnd(rowsRect) - yMin;
        }

        private const float ListRowHeight = 42f;
        private const float SectionTitleHeight = 30f;
        private const float SectionPad = 8f;

        // Opens a titled section: a panel when themed, else a heading. Returns the area for its rows.
        private static Rect SectionBegin(Rect rect, string title, string icon, float rowsHeight, GameFont headingFont)
        {
            ResearchSkin skin = ResearchSkin.Active;
            if (skin != null)
            {
                Rect box = new Rect(rect.x, rect.y, rect.width, SectionTitleHeight + rowsHeight + SectionPad * 2f);
                if (skin.Section(box, title, icon))
                    return new Rect(box.x + SectionPad, box.y + SectionTitleHeight + SectionPad, box.width - SectionPad * 2f, rowsHeight);
            }
            Text.Font = headingFont;
            Rect heading = new Rect(rect.x, rect.y, rect.width, 0f);
            Widgets.LabelCacheHeight(ref heading, title + ":");
            Text.Font = GameFont.Small;
            return new Rect(rect.x, heading.yMax + 6f, rect.width, rowsHeight);
        }

        // The y below a section opened by SectionBegin, with the gap before the next one.
        private static float SectionEnd(Rect rowsRect) => rowsRect.yMax + (ResearchSkin.Active != null ? SectionPad + 10f : 10f);

        private static int CountUnfinished(List<ResearchProjectDef> projects)
        {
            int count = 0;
            if (projects == null)
                return count;
            for (int i = 0; i < projects.Count; i++)
            {
                if (projects[i] != null && !projects[i].IsFinished)
                    count++;
            }
            return count;
        }

        private static bool HasUnfinished(List<ResearchProjectDef> projects)
        {
            if (projects == null)
                return false;
            for (int i = 0; i < projects.Count; i++)
            {
                if (projects[i] != null && !projects[i].IsFinished)
                    return true;
            }
            return false;
        }

        private void DrawPrereqRow(ResearchProjectDef prereq, ref Rect rect)
        {
            if (prereq == null)
                return;

            const float itemHeight = ListRowHeight;
            const float iconSize = 28f;
            const float iconPadding = 8f;
            Rect prereqRect = new Rect(rect.xMin + 6f, rect.yMin, rect.width - 6f, itemHeight);

            Color techColor = (SemiRandomResearchMod.settings == null || SemiRandomResearchMod.settings.colorAndGroupByTechLevel)
                ? GetTechLevelColor(prereq.techLevel)
                : TexUI.AvailResearchColor;
            Color bgColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
            ResearchSkin skin = ResearchSkin.Active;
            if ((skin == null || !skin.Card(prereqRect, techColor, 0f, false, false, 1f)) && IsRepaint)
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
                DrawProjectIcon(iconRect, prereq);
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
                if (ShowsAsDependent(dependents[i]))
                    unfinishedCount++;
            }
            if (unfinishedCount == 0)
                return 0f;

            float yMin = rect.yMin;
            Rect rowsRect = SectionBegin(rect, "CM_Semi_Random_Research_RequiredForResearch".Translate(), "tree-structure",
                unfinishedCount * (ListRowHeight + 4f) - 4f, GameFont.Medium);
            rect = new Rect(rowsRect.x - 6f, rowsRect.y, rowsRect.width + 6f, rect.height);
            for (int i = 0; i < dependents.Count; i++)
            {
                ResearchProjectDef dependent = dependents[i];
                if (!ShowsAsDependent(dependent))
                    continue;

                DrawDependentResearchRow(project, dependent, ref rect);
            }

            return SectionEnd(rowsRect) - yMin;
        }

        private void DrawDependentResearchRow(ResearchProjectDef prerequisite, ResearchProjectDef dependent, ref Rect rect)
        {
            const float itemHeight = ListRowHeight;
            const float iconSize = 28f;
            const float iconPadding = 8f;
            Rect rowRect = new Rect(rect.xMin + 6f, rect.yMin, rect.width - 6f, itemHeight);

            Color techColor = GetCategoryColor(dependent);
            Color bgColor = Color.Lerp(TexUI.AvailResearchColor, techColor, 0.3f);
            ResearchSkin skin = ResearchSkin.Active;
            if ((skin == null || !skin.Card(rowRect, techColor, 0f, false, false, 1f)) && IsRepaint)
            {
                Widgets.DrawBoxSolid(rowRect, bgColor);
                DrawTransparentBox(rowRect, techColor, 1f);
            }

            Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + (itemHeight - iconSize) / 2f, iconSize, iconSize);
            Rect labelRect = new Rect(iconRect.xMax + iconPadding, rowRect.y,
                rowRect.width - iconRect.width - (iconPadding * 2f) - 6f, itemHeight);

            if (IsRepaint)
            {
                DrawProjectIcon(iconRect, dependent);
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

            if (Mouse.IsOver(rowRect))
            {
                string remaining = GetOtherUnfinishedPrerequisites(dependent, prerequisite);
                TooltipHandler.TipRegion(rowRect, remaining.NullOrEmpty()
                    ? "CM_Semi_Random_Research_FinalResearchPrerequisite".Translate()
                    : "CM_Semi_Random_Research_AlsoRequires".Translate(remaining));
            }

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

        // A card's icon: its first unlockable, else Node Research's era icon for an emergence project.
        private void DrawProjectIcon(Rect rect, ResearchProjectDef project)
        {
            Def firstUnlockable = GetFirstUnlockable(project);
            if (firstUnlockable != null)
            {
                try { DefIcons.Draw(rect, firstUnlockable); }
                catch (Exception) { }
                return;
            }
            Texture2D era = NodeResearch.EmergenceIcon(project);
            if (era != null)
                GUI.DrawTexture(rect, era, ScaleMode.ScaleToFit);
        }

        private float DrawUnlockableHyperlinks(Rect rect, ResearchProjectDef project)
        {
            List<Def> unlocked = project == cachedUnlocksProject ? cachedSelectedUnlocks : null;
            if (unlocked.NullOrEmpty())
            {
                return 0f;
            }

            float yMin = rect.yMin;

            // Tiles in as many columns as fit, each a large icon in a sunken well over its name.
            const int maxVisibleIcons = 20;
            const float minTileWidth = 100f;
            const float tileGap = 6f;
            const float tileHeight = 107f;
            const float moreHeight = 26f;
            int total = 0;
            for (int i = 0; i < unlocked.Count; i++)
            {
                if (unlocked[i] != null)
                    total++;
            }
            int shown = Mathf.Min(total, maxVisibleIcons);
            float innerWidth = ResearchSkin.Active != null ? rect.width - SectionPad * 2f : rect.width;
            int columns = Mathf.Max(1, Mathf.FloorToInt((innerWidth + tileGap) / (minTileWidth + tileGap)));
            int rows = (shown + columns - 1) / columns;
            float rowsHeight = rows * (tileHeight + tileGap) - tileGap + (total > shown ? moreHeight + 4f : 0f);

            Rect area = SectionBegin(rect, "Unlocks".Translate(), "package", rowsHeight, GameFont.Small);
            float tileWidth = (area.width - tileGap * (columns - 1)) / columns;
            Color accent = GetCategoryColor(project);

            int visibleDrawn = 0;
            for (int i = 0; i < unlocked.Count && visibleDrawn < shown; i++)
            {
                Def def = unlocked[i];
                if (def == null)
                    continue;

                int column = visibleDrawn % columns;
                int row = visibleDrawn / columns;
                Rect tile = new Rect(area.x + column * (tileWidth + tileGap), area.y + row * (tileHeight + tileGap), tileWidth, tileHeight);
                DrawUnlockTile(tile, def, accent);
                visibleDrawn++;
            }
            if (total > shown)
            {
                GUI.color = ResearchSkin.Active != null ? ResearchSkin.Active.TextDim : Color.white;
                Widgets.Label(new Rect(area.x, area.yMax - moreHeight, area.width, moreHeight), "CM_Semi_Random_Research_MoreUnlocks".Translate(total - shown));
            }

            GUI.color = Color.white;
            return SectionEnd(area) - yMin;
        }

        // One unlock: its icon in a sunken well above its name (two lines at most), the full name as a tooltip; a click opens its info card.
        private void DrawUnlockTile(Rect tile, Def def, Color accent)
        {
            const float iconSize = 42f;
            const float wellSize = 52f;
            bool isMouseOver = Mouse.IsOver(tile);
            Rect wellRect = new Rect(tile.center.x - wellSize / 2f, tile.y + 6f, wellSize, wellSize);
            ResearchSkin skin = ResearchSkin.Active;
            if ((skin == null || !skin.Tile(tile, wellRect, accent)) && IsRepaint)
            {
                Widgets.DrawBoxSolid(tile, isMouseOver
                    ? new Color(0.3f, 0.3f, 0.3f, 0.3f)
                    : new Color(0.1f, 0.1f, 0.1f, 0.1f));
                DrawTransparentBox(tile, isMouseOver
                    ? new Color(0.8f, 0.8f, 0.8f, 0.5f)
                    : new Color(0.4f, 0.4f, 0.4f, 0.3f), isMouseOver ? 1.5f : 1f);
                Widgets.DrawBoxSolid(wellRect, new Color(0f, 0f, 0f, 0.35f));
                DrawTransparentBox(wellRect, new Color(accent.r, accent.g, accent.b, isMouseOver ? 0.8f : 0.4f), 1f);
            }

            string label = SafeDefLabel(def) ?? string.Empty;
            Rect iconRect = new Rect(wellRect.center.x - iconSize / 2f, wellRect.center.y - iconSize / 2f, iconSize, iconSize);
            if (IsRepaint)
            {
                try
                {
                    GUI.color = isMouseOver ? Color.white : new Color(0.9f, 0.9f, 0.9f);
                    DefIcons.Draw(iconRect, def);
                }
                catch (Exception)
                {
                }

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = isMouseOver ? Color.white : skin != null ? skin.TextBright : new Color(0.85f, 0.85f, 0.85f);
                Rect labelRect = new Rect(tile.x + 4f, wellRect.yMax + 3f, tile.width - 8f, tile.yMax - wellRect.yMax - 4f);
                GUI.BeginGroup(labelRect);
                Widgets.Label(new Rect(0f, 0f, labelRect.width, 200f), label);
                GUI.EndGroup();
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            if (isMouseOver)
                TooltipHandler.TipRegion(tile, label);
            if (Clicked(tile))
                Find.WindowStack.Add(new Dialog_InfoCard(def));
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
                    DefIcons.Draw(iconRect, def);
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
            // First linked facility of this type, and the first active one.
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
            // One point per active required facility, 0.6 per inactive one.
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

        // Package id to expansion, resolved once per mod.
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

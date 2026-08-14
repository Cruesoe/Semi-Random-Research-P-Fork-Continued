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
        private Color GetTechLevelColor(TechLevel techLevel)
        {
            switch (techLevel)
            {
                case TechLevel.Animal:
                    return new Color(0.5f, 0.4f, 0.2f); // Warmer brown
                case TechLevel.Neolithic:
                    return new Color(0.6f, 0.35f, 0.35f); // Richer dark red
                case TechLevel.Medieval:
                    return new Color(0.6f, 0.6f, 0.3f); // Warmer yellow
                case TechLevel.Industrial:
                    return new Color(0.4f, 0.6f, 0.3f); // Brighter green-yellow
                case TechLevel.Spacer:
                    return new Color(0.3f, 0.5f, 0.6f); // Richer blue-green
                case TechLevel.Ultra:
                    return new Color(0.45f, 0.35f, 0.6f); // Deeper purple-blue
                case TechLevel.Archotech:
                    return new Color(0.6f, 0.35f, 0.6f); // Richer pink
                default:
                    return TexUI.AvailResearchColor;
            }
        }

        // Named colors for the special Anomaly/Gravship pseudo-categories, shared by every
        // place that draws a category header or card (used to be three separate hardcoded copies).
        private static readonly Color AnomalyBasicColor = new Color(0.65f, 0.35f, 0.5f);
        private static readonly Color AnomalyAdvancedColor = new Color(0.45f, 0.35f, 0.5f);
        private static readonly Color GravshipColor = new Color(0.2f, 0.6f, 0.8f);

        // Shared color lookup for a project's card/header color. Falls back to the plain
        // tech-level color, but overrides it for the special Anomaly/Gravship pseudo-categories
        // so all UI elements (list cards, section headers, dashboard cards) stay in sync.
        private Color GetCategoryColor(ResearchProjectDef projectDef)
        {
            if (projectDef.knowledgeCategory != null && projectDef.knowledgeCategory == KnowledgeCategoryDefOf.Basic)
                return AnomalyBasicColor;
            if (projectDef.knowledgeCategory != null && projectDef.knowledgeCategory == KnowledgeCategoryDefOf.Advanced)
                return AnomalyAdvancedColor;
            if (projectDef.tab?.defName == "VGE_Gravtech" || projectDef.tab?.defName == "VGE_GravShip")
                return GravshipColor;

            if (SemiRandomResearchMod.settings != null && !SemiRandomResearchMod.settings.colorAndGroupByTechLevel)
                return TexUI.AvailResearchColor;

            return GetTechLevelColor(projectDef.techLevel);
        }

        // Card fill accent for research progress. When standard cards use the flat neutral
        // category color, a plain techColor fill matches the background and disappears —
        // lift toward white so partial progress still reads.
        private bool UsesFlatNeutralCardColor(ResearchProjectDef projectDef)
        {
            return SemiRandomResearchMod.settings != null
                && !SemiRandomResearchMod.settings.colorAndGroupByTechLevel
                && projectDef != null
                && projectDef.knowledgeCategory == null
                && projectDef.tab?.defName != "VGE_Gravtech"
                && projectDef.tab?.defName != "VGE_GravShip";
        }

        // Separators / borders on flat neutral cards need a lifted accent or the
        // icon | name | cost sections disappear into the dark background.
        private Color GetCardStructureAccent(ResearchProjectDef projectDef, Color categoryColor)
        {
            if (UsesFlatNeutralCardColor(projectDef))
                return Color.Lerp(TexUI.AvailResearchColor, Color.white, 0.45f);

            return categoryColor;
        }

        private Color GetProgressFillAccent(ResearchProjectDef projectDef, Color categoryColor)
        {
            if (UsesFlatNeutralCardColor(projectDef))
                return Color.Lerp(TexUI.AvailResearchColor, Color.white, 0.5f);

            return Color.Lerp(categoryColor, Color.white, 0.15f);
        }

        private static string GetProjectCostText(ResearchProjectDef project)
        {
            if (project == null)
                return string.Empty;

            return project.ProgressApparent > 0
                ? $"{project.ProgressApparent:N0}/{project.CostApparent:N0}"
                : project.CostApparent.ToString("N0");
        }

        private float MeasureCostColumnWidth(IEnumerable<ResearchProjectDef> projects)
        {
            Text.Font = GameFont.Small;
            float width = 8f;
            foreach (ResearchProjectDef project in projects)
            {
                if (project == null)
                    continue;

                width = Mathf.Max(width, Text.CalcSize(GetProjectCostText(project)).x + 12f);
            }
            return width;
        }

        // The "icon | name | cost" card row is drawn in two places (the research list buttons
        // and the active-project dashboard header) with slightly different proportions/margins,
        // so this returns the shared icon/separator/name/cost geometry. costColumnWidth is the
        // measured width of the cost/progress label so values like 889/13,840 stay on one line.
        private readonly struct CardRowLayout
        {
            public readonly Rect IconRect;
            public readonly Rect FirstSeparator;
            public readonly Rect SecondSeparator;
            public readonly Rect NameRect;
            public readonly Rect CostRect;

            public CardRowLayout(Rect iconRect, Rect firstSeparator, Rect secondSeparator, Rect nameRect, Rect costRect)
            {
                IconRect = iconRect;
                FirstSeparator = firstSeparator;
                SecondSeparator = secondSeparator;
                NameRect = nameRect;
                CostRect = costRect;
            }
        }

        private CardRowLayout ComputeCardRowLayout(Rect containerRect, float rowHeight, float costColumnWidth)
        {
            const float iconSize = 32.0f;
            const float innerMargin = 4f;
            const float nameLeftPadding = 12f;
            const float separatorWidth = 1f;
            const float costSeparatorPadding = 4f;

            Rect iconRect = new Rect(containerRect.x + innerMargin, containerRect.y + (rowHeight - iconSize) / 2, iconSize, iconSize);

            Rect firstSeparator = new Rect(
                iconRect.xMax + innerMargin * 2,
                containerRect.y,
                separatorWidth,
                rowHeight
            );

            float costWidth = Mathf.Max(costColumnWidth, 8f);
            float secondSeparatorX = containerRect.xMax - innerMargin - costWidth - costSeparatorPadding - separatorWidth;
            secondSeparatorX = Mathf.Max(secondSeparatorX, firstSeparator.xMax + nameLeftPadding);

            Rect secondSeparator = new Rect(
                secondSeparatorX,
                containerRect.y,
                separatorWidth,
                rowHeight
            );

            Rect nameRect = new Rect(
                firstSeparator.xMax + nameLeftPadding,
                containerRect.y,
                Mathf.Max(0f, secondSeparator.x - (firstSeparator.xMax + nameLeftPadding)),
                rowHeight
            );

            Rect costRect = new Rect(
                secondSeparator.xMax + costSeparatorPadding,
                containerRect.y,
                Mathf.Max(0f, containerRect.xMax - innerMargin - (secondSeparator.xMax + costSeparatorPadding)),
                rowHeight
            );

            return new CardRowLayout(iconRect, firstSeparator, secondSeparator, nameRect, costRect);
        }

        private void DrawTransparentBox(Rect rect, Color borderColor, float borderThickness = 1f, bool cutOutside = false)
        {
            Color saveColor = GUI.color;
            GUI.color = borderColor;
            Widgets.DrawBox(rect, Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(borderThickness, 3f))));
            GUI.color = saveColor;
        }
    }
}

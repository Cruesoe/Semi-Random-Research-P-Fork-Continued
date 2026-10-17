using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    public partial class MainTabWindow_NextResearch
    {
        internal static Color GetTechLevelColor(TechLevel techLevel)
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

        // Colors for the Anomaly and Gravship pseudo-categories, shared by headers and cards.
        private static readonly Color AnomalyBasicColor = new Color(0.65f, 0.35f, 0.5f);
        private static readonly Color AnomalyAdvancedColor = new Color(0.45f, 0.35f, 0.5f);
        private static readonly Color GravshipColor = new Color(0.2f, 0.6f, 0.8f);

        // A project's card color: the Anomaly/Gravship color, else its tech level color (or neutral with grouping off).
        internal static Color GetCategoryColor(ResearchProjectDef projectDef)
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

        // Neutral standard cards get a lighter progress fill and structure accent so they show against the background.
        private static bool UsesFlatNeutralCardColor(ResearchProjectDef projectDef)
        {
            return SemiRandomResearchMod.settings != null
                && !SemiRandomResearchMod.settings.colorAndGroupByTechLevel
                && projectDef != null
                && projectDef.knowledgeCategory == null
                && projectDef.tab?.defName != "VGE_Gravtech"
                && projectDef.tab?.defName != "VGE_GravShip";
        }

        internal static Color GetCardStructureAccent(ResearchProjectDef projectDef, Color categoryColor)
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

        private static readonly Color NodeTechTagColor = new Color(0.95f, 0.75f, 0.25f);

        // Node Research's Foundation/Emergence tag, shown on both offer and active cards.
        private string GetNodeTechTag(ResearchProjectDef projectDef)
        {
            if (projectDef == null)
                return null;
            if (cachedFoundationProjects.Contains(projectDef))
                return "CM_Semi_Random_Research_Foundation".Translate();
            if (cachedEmergenceProjects.Contains(projectDef))
                return "CM_Semi_Random_Research_Emergence".Translate();
            return null;
        }

        // "889/13,840" cost labels, cached for the current tick.
        private readonly Dictionary<ResearchProjectDef, string> costTextCache =
            new Dictionary<ResearchProjectDef, string>();
        private int costTextCacheTick = int.MinValue;

        private void InvalidateCostTextCache()
        {
            costTextCache.Clear();
            durationTextCache.Clear();
            costTextCacheTick = int.MinValue;
        }

        private string GetProjectCostText(ResearchProjectDef project)
        {
            if (project == null)
                return string.Empty;

            int tick = Find.TickManager?.TicksGame ?? 0;
            if (tick != costTextCacheTick)
            {
                costTextCache.Clear();
                durationTextCache.Clear();
                costTextCacheTick = tick;
            }
            else if (costTextCache.TryGetValue(project, out string cached))
            {
                return cached;
            }

            string text = project.ProgressApparent > 0
                ? $"{project.ProgressApparent:N0}/{project.CostApparent:N0}"
                : project.CostApparent.ToString("N0");
            costTextCache[project] = text;
            return text;
        }

        // Each category's average research speed, read from the rate tracker at most once a game hour.
        private readonly Dictionary<string, float> cachedCategoryRates = new Dictionary<string, float>();
        private int cachedCategoryRatesTick = -1;

        private float CategoryRate(string categoryKey)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (cachedCategoryRatesTick < 0 || tick < cachedCategoryRatesTick || tick - cachedCategoryRatesTick >= GenDate.TicksPerHour)
            {
                cachedCategoryRates.Clear();
                cachedCategoryRatesTick = tick;
            }
            if (!cachedCategoryRates.TryGetValue(categoryKey, out float rate))
            {
                rate = cachedRateTracker?.GetCategoryAverageRate(categoryKey) ?? 0f;
                cachedCategoryRates[categoryKey] = rate;
            }
            return rate;
        }

        // "~4 days": the project's own remaining time at its category's research speed, cached with the cost labels.
        private readonly Dictionary<ResearchProjectDef, string> durationTextCache =
            new Dictionary<ResearchProjectDef, string>();

        private string GetProjectDurationText(ResearchProjectDef project)
        {
            if (project == null)
                return null;

            GetProjectCostText(project);
            if (durationTextCache.TryGetValue(project, out string cached))
                return cached;

            float rate = CategoryRate(ResearchTracker.GetCategoryKey(project));
            float remaining = project.CostApparent - project.ProgressApparent;
            string text = null;
            if (rate > 0f && remaining > 0f && remaining / rate <= 100000f)
                text = "CM_Semi_Random_Research_OfferDuration".Translate(ResearchRateTracker.FormatETA(remaining / rate));
            durationTextCache[project] = text;
            return text;
        }

        // Cost, with the project's own time below it once a research speed has been measured.
        private void DrawCostColumn(Rect costRect, ResearchProjectDef project, string costText, Color costColor)
        {
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.MiddleCenter;
            string duration = GetProjectDurationText(project);
            if (duration == null)
            {
                GUI.color = costColor;
                Widgets.Label(costRect, costText);
            }
            else
            {
                GUI.color = costColor;
                Widgets.Label(new Rect(costRect.x, costRect.y + 2f, costRect.width, 22f), costText);
                GUI.color = new Color(0.62f, 0.62f, 0.62f, costColor.a);
                Widgets.Label(new Rect(costRect.x, costRect.y + 24f, costRect.width, 22f), duration);
            }
            Text.WordWrap = wordWrap;
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
                string duration = GetProjectDurationText(project);
                if (duration != null)
                    width = Mathf.Max(width, Text.CalcSize(duration).x + 12f);
            }
            return width;
        }

        // "icon | name | cost" geometry shared by every card. costColumnWidth is the measured widest cost label.
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

        private static void DrawTransparentBox(Rect rect, Color borderColor, float borderThickness = 1f)
        {
            Color saveColor = GUI.color;
            GUI.color = borderColor;
            Widgets.DrawBox(rect, Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(borderThickness, 3f))));
            GUI.color = saveColor;
        }
    }
}

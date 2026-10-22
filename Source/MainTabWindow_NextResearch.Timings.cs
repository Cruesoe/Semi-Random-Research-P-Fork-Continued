using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // The timings panel under the standard research card: research speed, the current project and all remaining research (or the current era's).
    public partial class MainTabWindow_NextResearch
    {
        private const float TimingsHeight = 54f;
        private const float TimingsRefreshSeconds = 0.5f;
        private static readonly Color TimingsDividerColor = new ColorInt(135, 135, 135).ToColor;

        private readonly List<TimingStat> timings = new List<TimingStat>();
        private float timingsBuiltAt = -1f;

        // One tile: a gray label over its value, with the explanation as its tooltip.
        private readonly struct TimingStat
        {
            public readonly string Label;
            public readonly string Value;
            public readonly string Tip;

            public TimingStat(string label, string value, string tip)
            {
                Label = label;
                Value = value;
                Tip = label.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + tip;
            }
        }

        // Rebuilt twice a second of real time, so it follows snoozes and settings while the game is paused.
        private void RefreshTimings()
        {
            float now = Time.realtimeSinceStartup;
            if (timingsBuiltAt >= 0f && now - timingsBuiltAt < TimingsRefreshSeconds)
                return;
            timingsBuiltAt = now;
            timings.Clear();

            float rate = CategoryRate("Standard");
            bool measured = rate > 0f;
            string unknown = "CM_Semi_Random_Research_Unknown".Translate();
            string noRateTip = "CM_Semi_Random_Research_TimingNoRateTip".Translate();

            timings.Add(new TimingStat("CM_Semi_Random_Research_TimingRate".Translate(),
                measured ? ResearchRateTracker.FormatRateShort(rate) : unknown,
                measured ? "CM_Semi_Random_Research_TimingRateTip".Translate().ToString() : noRateTip));

            ResearchProjectDef current = null;
            for (int i = 0; i < cachedActiveProjects.Count; i++)
            {
                if (cachedActiveProjects[i] != null && ResearchTracker.GetCategoryKey(cachedActiveProjects[i]) == "Standard")
                {
                    current = cachedActiveProjects[i];
                    break;
                }
            }
            if (current == null)
            {
                timings.Add(new TimingStat("CM_Semi_Random_Research_TimingCurrent".Translate(),
                    "CM_Semi_Random_Research_TimingIdle".Translate(), "CM_Semi_Random_Research_TimingIdleTip".Translate()));
            }
            else if (drawTracker != null && drawTracker.IsProjectPaused(current))
            {
                timings.Add(new TimingStat("CM_Semi_Random_Research_TimingCurrent".Translate(),
                    "CM_Semi_Random_Research_PausedTag".Translate(), "CM_Semi_Random_Research_TimingPausedTip".Translate(SafeLabel(current))));
            }
            else
            {
                float work = Mathf.Max(0f, current.CostApparent - current.ProgressApparent);
                string tip = "CM_Semi_Random_Research_TimingCurrentTip".Translate(SafeLabel(current), work.ToString("N0")) + DateLine(work, rate);
                timings.Add(new TimingStat("CM_Semi_Random_Research_TimingCurrent".Translate(),
                    measured ? ResearchRateTracker.FormatEstimate(work / rate) : unknown, measured ? tip : noRateTip));
            }

            // Every unfinished project that counts toward the estimates, available or not. With the era setting, only those
            // of the player's current tech level.
            bool eraOnly = SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.timingsShowCurrentEra;
            TechLevel era = Faction.OfPlayer.def.techLevel;
            float treeWork = 0f;
            int treeCount = 0;
            List<ResearchProjectDef> allDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ResearchProjectDef def = allDefs[i];
                if (def.IsFinished || (eraOnly && def.techLevel != era) || !CountsTowardEstimates(def))
                    continue;
                treeWork += Mathf.Max(0f, def.CostApparent - def.ProgressApparent);
                treeCount++;
            }
            string eraName = era.ToStringHuman();
            string treeLabel = eraOnly ? "CM_Semi_Random_Research_TimingEra".Translate(eraName.CapitalizeFirst()).ToString()
                : "CM_Semi_Random_Research_TimingTree".Translate().ToString();
            string treeTip = (eraOnly ? "CM_Semi_Random_Research_TimingEraTip".Translate(treeCount, eraName, treeWork.ToString("N0"))
                : "CM_Semi_Random_Research_TimingTreeTip".Translate(treeCount, treeWork.ToString("N0"))) + DateLine(treeWork, rate);
            string treeValue = treeWork <= 0f ? "CM_Semi_Random_Research_Complete".Translate().ToString()
                : measured ? ResearchRateTracker.FormatEstimate(treeWork / rate) : unknown;
            timings.Add(new TimingStat(treeLabel, treeValue, measured || treeWork <= 0f ? treeTip : noRateTip));
        }

        private static string DateLine(float work, float rate)
        {
            string date = rate > 0f ? ResearchRateTracker.DateAfter(work / rate) : null;
            return date == null ? string.Empty : "\n\n" + "CM_Semi_Random_Research_TimingFinishes".Translate(date);
        }

        // Equal tiles split by dividers, each a gray label over its value, both centered in the tile.
        private void DrawTimings(Rect rect)
        {
            RefreshTimings();
            Widgets.DrawMenuSection(rect);
            if (timings.Count == 0)
                return;
            Rect inner = rect.ContractedBy(8f, 4f);
            float width = inner.width / timings.Count;
            float x = inner.x;
            const float lineHeight = 22f;
            float top = inner.center.y - lineHeight;
            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            foreach (TimingStat stat in timings)
            {
                Rect tile = new Rect(x, inner.y, width, inner.height);
                if (x > inner.x && IsRepaint)
                    Widgets.DrawLine(new Vector2(x, inner.y + 4f), new Vector2(x, inner.yMax - 4f), TimingsDividerColor, 1f);
                x += width;
                Widgets.DrawHighlightIfMouseover(tile);
                Rect text = tile.ContractedBy(6f, 0f);
                GUI.color = Color.gray;
                Widgets.Label(new Rect(text.x, top, text.width, lineHeight), stat.Label.Truncate(text.width));
                GUI.color = Color.white;
                Widgets.Label(new Rect(text.x, top + lineHeight, text.width, lineHeight), stat.Value.Truncate(text.width));
                TooltipHandler.TipRegion(tile, stat.Tip);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = wordWrap;
        }
    }
}

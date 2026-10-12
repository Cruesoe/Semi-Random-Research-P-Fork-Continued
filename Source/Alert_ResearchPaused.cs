using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    public class Alert_ResearchPaused : Alert
    {
        public Alert_ResearchPaused()
        {
            defaultLabel = "CM_Semi_Random_Research_ResearchPausedAlert".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        public override AlertReport GetReport()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
                return false;

            ResearchTracker tracker = SemiRandomResearchUtility.Tracker;
            return tracker != null && tracker.ResearchPaused;
        }

        public override TaggedString GetExplanation()
        {
            ResearchProjectDef paused = SemiRandomResearchUtility.Tracker?.PausedProject;
            return paused != null
                ? "CM_Semi_Random_Research_ResearchPausedAlertDescProject".Translate(paused.LabelCap)
                : "CM_Semi_Random_Research_ResearchPausedAlertDesc".Translate();
        }

        // Opens the paused project's tab and selects it.
        protected override void OnClick()
        {
            ResearchProjectDef paused = SemiRandomResearchUtility.Tracker?.PausedProject;
            ResearchTabDef tab = paused?.tab ?? ResearchTabDefOf.Main;
            ResearchTabCompatibility.Open(tab, paused);
        }
    }
}

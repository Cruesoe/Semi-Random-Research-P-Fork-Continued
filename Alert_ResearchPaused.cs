using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    public class Alert_ResearchPaused : Alert
    {
        public Alert_ResearchPaused()
        {
            defaultLabel = "CM_Semi_Random_Research_ResearchPausedAlert".Translate();
            defaultExplanation = "CM_Semi_Random_Research_ResearchPausedAlertDesc".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        public override AlertReport GetReport()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.World == null)
                return false;

            ResearchTracker tracker = Find.World.GetComponent<ResearchTracker>();
            return tracker != null && tracker.ResearchPaused;
        }

        protected override void OnClick()
        {
            ResearchTabCompatibility.Open(ResearchTabDefOf.Main);
        }
    }
}

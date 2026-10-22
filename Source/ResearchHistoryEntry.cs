using Verse;

namespace CM_Semi_Random_Research
{
    // One finished project for the history view, deep-saved so a removed mod's entry drops out with its tick.
    public class ResearchHistoryEntry : IExposable
    {
        public ResearchProjectDef project;
        public int tick;

        public ResearchHistoryEntry()
        {
        }

        public ResearchHistoryEntry(ResearchProjectDef project, int tick)
        {
            this.project = project;
            this.tick = tick;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref project, "project");
            Scribe_Values.Look(ref tick, "tick", 0);
        }
    }
}

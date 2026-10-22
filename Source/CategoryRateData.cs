using System.Collections.Generic;
using Verse;

namespace CM_Semi_Random_Research
{
    // One research category's hourly rate samples, saved by ResearchRateTracker.
    public class CategoryRateData : IExposable
    {
        public List<float> rateSamples = new List<float>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref rateSamples, "rateSamples", LookMode.Value);
            if (rateSamples == null)
                rateSamples = new List<float>();
        }
    }
}

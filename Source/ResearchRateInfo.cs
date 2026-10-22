namespace CM_Semi_Random_Research
{
    // A project's average research rate and how many samples it rests on, for the completion letter.
    public class ResearchRateInfo
    {
        public float AverageRate { get; set; }
        public int TotalSamples { get; set; }

        public string AverageRateFormatted => ResearchRateTracker.FormatRate(AverageRate);
    }
}

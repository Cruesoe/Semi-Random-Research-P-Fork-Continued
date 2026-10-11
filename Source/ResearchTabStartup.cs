using Verse;

namespace CM_Semi_Random_Research
{
    // Runs once defs are loaded, the earliest point the Research MainButtonDef exists.
    [StaticConstructorOnStartup]
    public static class ResearchTabStartup
    {
        static ResearchTabStartup()
        {
            ResearchTabWindowSwitcher.Apply();
        }
    }
}

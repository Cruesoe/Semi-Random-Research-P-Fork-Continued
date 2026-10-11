using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    // Vanilla casts the Research tab window to MainTabWindow_Research, which throws when another window owns the tab.
    internal static class ResearchTabCompatibility
    {
        public static void Open(ResearchTabDef tab = null, ResearchProjectDef project = null)
        {
            if (Find.MainTabsRoot == null || MainButtonDefOf.Research == null)
                return;

            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research, true);
            Apply(tab, project);
        }

        public static void SelectProject(ResearchProjectDef project)
        {
            Apply(null, project);
        }

        private static void Apply(ResearchTabDef tab, ResearchProjectDef project)
        {
            MainTabWindow window = MainButtonDefOf.Research.TabWindow;
            if (window is MainTabWindow_Research vanilla)
            {
                if (tab != null)
                    vanilla.CurTab = tab;
                if (project != null)
                    vanilla.Select(project);
                return;
            }

            if (window is MainTabWindow_NextResearch ours && project != null)
                ours.SelectFromExternal(project);
        }
    }
}

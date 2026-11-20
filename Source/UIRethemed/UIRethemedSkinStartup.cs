using CM_Semi_Random_Research;
using Verse;

namespace SemiRandomResearchUIRethemed
{
    [StaticConstructorOnStartup]
    public static class UIRethemedSkinStartup
    {
        static UIRethemedSkinStartup()
        {
            ResearchSkin.Register(new UIRethemedSkin());
        }
    }
}

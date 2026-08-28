using HarmonyLib;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    public class SemiRandomResearchMod : Mod
    {
        private static SemiRandomResearchMod _instance;
        public static SemiRandomResearchMod Instance => _instance;

        public static SemiRandomResearchSettings settings;
        public static string version;

        public SemiRandomResearchMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("CM_Semi_Random_Research");
            harmony.PatchAll();
            Sleek_Integration.TryHookSleekStandDown();

            _instance = this;
            settings = GetSettings<SemiRandomResearchSettings>();
            if (settings.MigrateTreeButtonDefaultToNode())
                WriteSettings();
            string versionFromManifest = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);

            if (!versionFromManifest.NullOrEmpty())
            {
                version = versionFromManifest;
            }
            else
            {
                version = "?.?.?";
            }
        }

        public override string SettingsCategory()
        {
            return "CM_Semi_Random_Research_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            settings.FlushPendingChoices();
            base.WriteSettings();
            settings.UpdateSettings();
        }

    }
}
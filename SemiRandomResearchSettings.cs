using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // =========================================================================
    // ENUMS
    // =========================================================================
    public enum ManualReroll
    {
        None,
        Once,
        Always
    }

    public enum ChoiceAmountSelection
    {
        Static,
        PerColonist
    }

    public enum ProgressAddsChoice
    {
        Always,
        Never,
        ReplaceChoice,
        AddChoice,
        AddChoiceOnlyOnGain
    }

    public enum PreferredResearchTree
    {
        NodeResearch,
        YART,
        Sleek
    }

    // =========================================================================
    // MOD SETTINGS CLASS
    // =========================================================================
    public class SemiRandomResearchSettings : ModSettings
    {
        public bool featureEnabled = true;
        public bool rerollAllEveryTime = true;
        public bool forceLowestTechLevel = false;
        public bool restrictToFactionTechLevel = false;
        public bool allowOneHigherTechProject = false;
        public bool allowSwitchingResearch = false;
        public ProgressAddsChoice progressAddsChoice = ProgressAddsChoice.Always;
        public bool showResearchRateGraph = true;
        public bool showCompletionLetter = true;
        public bool autoOpenOnCompletion = true;
        public bool autoPickNextResearch = false;
        public ManualReroll allowManualReroll = ManualReroll.None;
        public ChoiceAmountSelection amountSelection = ChoiceAmountSelection.Static;
        public int availableProjectCount = 3;
        public int additionalProjectPerXColonists = 3;
        public int maxProjectCount = 6;
        public int reofferAfterAmountOfRerolls = 3;
        public bool equalizeCost = false;
        public bool verboseLogging = false;
        public bool usingNodeResearch = false;
        public PreferredResearchTree preferredResearchTree = PreferredResearchTree.NodeResearch;
        public int settingsVersion;
        public bool suppressHandoverMessages = false;
        public bool colorAndGroupByTechLevel = true;

        public bool AllowOneHigherTechProjectActive =>
            allowOneHigherTechProject && !ResearchTabWindowSwitcher.NodeResearchInstalled;

        public bool RestrictToFactionTechLevelActive =>
            restrictToFactionTechLevel || ResearchTabWindowSwitcher.NodeResearchInstalled;

        private bool loggedSettings = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref featureEnabled, "featureEnabled", true);
            Scribe_Values.Look(ref rerollAllEveryTime, "rerollAllEveryTime", true);
            Scribe_Values.Look(ref allowManualReroll, "allowManualReroll", ManualReroll.None);
            Scribe_Values.Look(ref amountSelection, "amountSelection", ChoiceAmountSelection.Static);
            Scribe_Values.Look(ref reofferAfterAmountOfRerolls, "reofferAfterAmountOfRerolls", 3);
            Scribe_Values.Look(ref availableProjectCount, "availableProjectCount", 3);
            Scribe_Values.Look(ref additionalProjectPerXColonists, "additionalProjectPerXColonists", 3);
            Scribe_Values.Look(ref maxProjectCount, "maxProjectCount", 3 + availableProjectCount);
            Scribe_Values.Look(ref progressAddsChoice, "progressAddsChoice", ProgressAddsChoice.Always);
            Scribe_Values.Look(ref forceLowestTechLevel, "forceLowestTechLevel", false);
            Scribe_Values.Look(ref restrictToFactionTechLevel, "restrictToFactionTechLevel", false);
            Scribe_Values.Look(ref allowOneHigherTechProject, "allowOneHigherTechProject", false);
            Scribe_Values.Look(ref allowSwitchingResearch, "allowSwitchingResearch", false);
            Scribe_Values.Look(ref equalizeCost, "equalizeCost", false);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
            Scribe_Values.Look(ref showResearchRateGraph, "showResearchRateGraph", true);
            Scribe_Values.Look(ref showCompletionLetter, "showCompletionLetter", true);
            Scribe_Values.Look(ref autoOpenOnCompletion, "autoOpenOnCompletion", true);
            Scribe_Values.Look(ref autoPickNextResearch, "autoPickNextResearch", false);
            Scribe_Values.Look(ref usingNodeResearch, "usingNodeResearch", false);
            Scribe_Values.Look(ref preferredResearchTree, "preferredResearchTree", PreferredResearchTree.NodeResearch);
            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
            Scribe_Values.Look(ref suppressHandoverMessages, "suppressHandoverMessages", false);
            Scribe_Values.Look(ref colorAndGroupByTechLevel, "colorAndGroupByTechLevel", true);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();

            // Set column width to half the screen with a slight margin
            listing.ColumnWidth = (inRect.width - 34f) / 2f;
            listing.Begin(inRect);

            // ==========================================
            // COLUMN 1: GENERAL & MECHANICS
            // ==========================================

            listing.Label("CM_Semi_Random_Research_Setting_Section_General".Translate().Colorize(Color.gray));
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Feature_Enabled_Label".Translate(), ref featureEnabled, "CM_Semi_Random_Research_Setting_Feature_Enabled_Description".Translate());
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_AutoOpen_Label".Translate(), ref autoOpenOnCompletion, "CM_Semi_Random_Research_Setting_AutoOpen_Description".Translate());
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_AutoPick_Label".Translate(), ref autoPickNextResearch, "CM_Semi_Random_Research_Setting_AutoPick_Description".Translate());
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_ShowGraph_Label".Translate(), ref showResearchRateGraph, "CM_Semi_Random_Research_Setting_ShowGraph_Description".Translate());
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_ShowLetter_Label".Translate(), ref showCompletionLetter, "CM_Semi_Random_Research_Setting_ShowLetter_Description".Translate());
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_ColorGroup_Label".Translate(), ref colorAndGroupByTechLevel, "CM_Semi_Random_Research_Setting_ColorGroup_Description".Translate());
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Verbose_Logging_Label".Translate(), ref verboseLogging, "CM_Semi_Random_Research_Setting_Verbose_Logging_Description".Translate());

            if (ResearchTabWindowSwitcher.NodeResearchInstalled)
            {
                listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_DelegateNode_Label".Translate(), ref usingNodeResearch, "CM_Semi_Random_Research_Setting_DelegateNode_Description".Translate());
            }

            if (ResearchTabWindowSwitcher.NodeResearchInstalled || ResearchTabWindowSwitcher.YartInstalled || ResearchTabWindowSwitcher.SleekInstalled)
            {
                listing.Label("CM_Semi_Random_Research_Setting_TreeButtonOpens_Label".Translate());
                Rect treeButtonOptionRect = listing.GetRect(26);
                List<FloatMenuOption> treeOptions = new List<FloatMenuOption>();
                if (ResearchTabWindowSwitcher.NodeResearchInstalled)
                {
                    treeOptions.Add(new FloatMenuOption("CM_Semi_Random_Research_Tree_NodeResearch".Translate(), () => { SetPreferredTree(PreferredResearchTree.NodeResearch); }));
                }
                if (ResearchTabWindowSwitcher.YartInstalled)
                {
                    treeOptions.Add(new FloatMenuOption("CM_Semi_Random_Research_Tree_YART".Translate(), () => { SetPreferredTree(PreferredResearchTree.YART); }));
                }
                if (ResearchTabWindowSwitcher.SleekInstalled)
                {
                    treeOptions.Add(new FloatMenuOption("CM_Semi_Random_Research_Tree_Sleek".Translate(), () => { SetPreferredTree(PreferredResearchTree.Sleek); }));
                }
                if (!ResearchTabWindowSwitcher.IsTreeAvailable(preferredResearchTree))
                    preferredResearchTree = ResearchTabWindowSwitcher.GetEffectivePreferredTree();
                string treeButtonLabel = PreferredTreeLabel(ResearchTabWindowSwitcher.GetEffectivePreferredTree());
                DoButtonOption(treeButtonOptionRect, treeButtonLabel, "CM_Semi_Random_Research_Setting_TreeButtonOpens_Description".Translate(), treeOptions, treeButtonOptionRect.width / 10, treeButtonOptionRect.width / 10);
                listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_SuppressHandover_Label".Translate(), ref suppressHandoverMessages, "CM_Semi_Random_Research_Setting_SuppressHandover_Description".Translate());
            }

            listing.GapLine();

            listing.Label("CM_Semi_Random_Research_Setting_Section_Gameplay".Translate().Colorize(Color.gray));
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Force_Lowest_Tech_Level_Label".Translate(), ref forceLowestTechLevel, "CM_Semi_Random_Research_Setting_Force_Lowest_Tech_Level_Description".Translate());
            bool restrictFaction = restrictToFactionTechLevel;
            string restrictFactionTip = "CM_Semi_Random_Research_Setting_Restrict_To_Faction_Tech_Level_Description".Translate();
            if (ResearchTabWindowSwitcher.NodeResearchInstalled)
            {
                restrictFaction = true;
                restrictFactionTip = "CM_Semi_Random_Research_Setting_RestrictFaction_LockedTip".Translate();
                GUI.enabled = false;
            }
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Restrict_To_Faction_Tech_Level_Label".Translate(), ref restrictFaction, restrictFactionTip);
            GUI.enabled = true;
            if (!ResearchTabWindowSwitcher.NodeResearchInstalled)
            {
                restrictToFactionTechLevel = restrictFaction;
            }
            bool oneHigher = allowOneHigherTechProject;
            string oneHigherTip = "CM_Semi_Random_Research_Setting_Allow_One_Higher_Tech_Project_Description".Translate();
            if (ResearchTabWindowSwitcher.NodeResearchInstalled)
            {
                oneHigher = false;
                oneHigherTip = "CM_Semi_Random_Research_Setting_OneHigher_DisabledTip".Translate();
                GUI.enabled = false;
            }
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Allow_One_Higher_Tech_Project_Label".Translate(), ref oneHigher, oneHigherTip);
            GUI.enabled = true;
            if (!ResearchTabWindowSwitcher.NodeResearchInstalled)
            {
                allowOneHigherTechProject = oneHigher;
            }
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Allow_Switching_Research_Label".Translate(), ref allowSwitchingResearch, "CM_Semi_Random_Research_Setting_Allow_Switching_Research_Description".Translate());
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Equalize_Cost_Label".Translate(), ref equalizeCost, "CM_Semi_Random_Research_Setting_Equalize_Cost_Description".Translate());

            // Progress Adds Choice Option
            string progressAddChoiceLableTooltip = "CM_Semi_Random_Research_Setting_Progress_Adds_Choice_Description".Translate() + "\n\n";
            foreach (ProgressAddsChoice option in System.Enum.GetValues(typeof(ProgressAddsChoice)))
            {
                progressAddChoiceLableTooltip += ("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + option.ToString() + "_Label").Translate() + ": " + ("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + option.ToString() + "_Description").Translate() + "\n\n";
            }
            listing.Label("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_Label".Translate(), -1, progressAddChoiceLableTooltip);
            Rect button_rect_1 = listing.GetRect(26);
            List<FloatMenuOption> progressAddsChoiceOptions = new List<FloatMenuOption>();
            foreach (ProgressAddsChoice option in System.Enum.GetValues(typeof(ProgressAddsChoice)))
            {
                string keyLabel = ("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + option.ToString() + "_Label").Translate();
                var menuOption = new FloatMenuOption(keyLabel, () => { progressAddsChoice = option; });
                menuOption.tooltip = new TipSignal(("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + option.ToString() + "_Description").Translate());
                progressAddsChoiceOptions.Add(menuOption);
            }
            DoButtonOption(button_rect_1, ("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + progressAddsChoice.ToString() + "_Label").Translate(), progressAddChoiceLableTooltip, progressAddsChoiceOptions, button_rect_1.width / 10, button_rect_1.width / 10);

            // ==========================================
            // COLUMN 2: REROLLS & LIMITS
            // ==========================================
            listing.NewColumn();

            listing.Label("CM_Semi_Random_Research_Setting_Section_Reroll".Translate().Colorize(Color.gray));
            listing.CheckboxLabeled("CM_Semi_Random_Research_Setting_Reroll_All_Every_Time_Label".Translate(), ref rerollAllEveryTime, "CM_Semi_Random_Research_Setting_Reroll_All_Every_Time_Description".Translate());

            string rerollLableTooltip = "CM_Semi_Random_Research_Setting_Manual_Reroll_Label".Translate() + "\n\n";
            foreach (ManualReroll option in System.Enum.GetValues(typeof(ManualReroll)))
            {
                rerollLableTooltip += ("CM_Semi_Random_Research_Setting_Manual_Reroll_" + option.ToString() + "_Label").Translate() + ": " + ("CM_Semi_Random_Research_Setting_Manual_Reroll_" + option.ToString() + "_Description").Translate() + "\n\n";
            }
            listing.Label("CM_Semi_Random_Research_Setting_Manual_Reroll_Label".Translate(), -1, rerollLableTooltip);

            Rect button_rect_2 = listing.GetRect(26);
            List<FloatMenuOption> manualRerollOptions = new List<FloatMenuOption>();
            foreach (ManualReroll option in System.Enum.GetValues(typeof(ManualReroll)))
            {
                string keyLabel = ("CM_Semi_Random_Research_Setting_Manual_Reroll_" + option.ToString() + "_Label").Translate();
                var menuOption = new FloatMenuOption(keyLabel, () => { allowManualReroll = option; });
                menuOption.tooltip = new TipSignal(("CM_Semi_Random_Research_Setting_Manual_Reroll_" + option.ToString() + "_Description").Translate());
                manualRerollOptions.Add(menuOption);
            }
            DoButtonOption(button_rect_2, ("CM_Semi_Random_Research_Setting_Manual_Reroll_" + allowManualReroll.ToString() + "_Label").Translate(), rerollLableTooltip, manualRerollOptions, button_rect_2.width / 10, button_rect_2.width / 10);

            if (allowManualReroll != ManualReroll.None)
            {
                listing.Label(("CM_Semi_Random_Research_Setting_Prevent_Rerolled_From_Appearing_Label".Translate()) + ": " + reofferAfterAmountOfRerolls.ToString(), -1, "CM_Semi_Random_Research_Setting_Prevent_Rerolled_From_Appearing_Description".Translate());
                listing.IntAdjuster(ref reofferAfterAmountOfRerolls, 1);
            }

            listing.GapLine();

            listing.Label("CM_Semi_Random_Research_Setting_Section_Limits".Translate().Colorize(Color.gray));
            listing.Label("CM_Semi_Random_Research_Setting_Type_Of_Projects_Count_Label".Translate());
            if (listing.RadioButton("CM_Semi_Random_Research_Setting_Static_Projects_Count_Label".Translate(), amountSelection == ChoiceAmountSelection.Static, 8f, "CM_Semi_Random_Research_Setting_Static_Projects_Count_Description".Translate()))
            {
                amountSelection = ChoiceAmountSelection.Static;
            }
            if (listing.RadioButton("CM_Semi_Random_Research_Setting_Dynamic_Projects_Count_Label".Translate(), amountSelection == ChoiceAmountSelection.PerColonist, 8f, "CM_Semi_Random_Research_Setting_Dynamic_Projects_Count_Description".Translate()))
            {
                amountSelection = ChoiceAmountSelection.PerColonist;
            }

            listing.Label(("CM_Semi_Random_Research_Setting_Available_Projects_Count_Label".Translate()) + ": " + availableProjectCount.ToString(), -1, "CM_Semi_Random_Research_Setting_Available_Projects_Count_Description".Translate());
            listing.IntAdjuster(ref availableProjectCount, 1, 0);
            if (availableProjectCount > maxProjectCount)
            {
                maxProjectCount = availableProjectCount;
            }

            if (amountSelection == ChoiceAmountSelection.PerColonist)
            {
                listing.Label(("CM_Semi_Random_Research_Setting_Additional_Project_Per_XColonists_Label".Translate()) + ": " + additionalProjectPerXColonists.ToString(), -1, "CM_Semi_Random_Research_Setting_Additional_Project_Per_XColonists_Description".Translate());
                listing.IntAdjuster(ref additionalProjectPerXColonists, 1, 1);

                listing.Label(("CM_Semi_Random_Research_Setting_Max_Projects_Label".Translate()) + ": " + maxProjectCount.ToString(), -1, "CM_Semi_Random_Research_Setting_Max_Projects_Description".Translate());
                listing.IntAdjuster(ref maxProjectCount, 1, 1);
                if (availableProjectCount > maxProjectCount)
                {
                    availableProjectCount = maxProjectCount;
                }
            }

            listing.End();

            DumpSettingToLog();
        }

        private void SetPreferredTree(PreferredResearchTree tree)
        {
            preferredResearchTree = tree;
            if (tree != PreferredResearchTree.NodeResearch)
                ResearchTabWindowSwitcher.SetUsingNodeResearch(false);
        }

        private static string PreferredTreeLabel(PreferredResearchTree preferred)
        {
            if (preferred == PreferredResearchTree.YART)
                return "CM_Semi_Random_Research_Tree_YART".Translate();
            if (preferred == PreferredResearchTree.Sleek)
                return "CM_Semi_Random_Research_Tree_Sleek".Translate();
            return "CM_Semi_Random_Research_Tree_NodeResearch".Translate();
        }

        private void DoButtonOption(Rect rect, string text, string tooltip, List<FloatMenuOption> options, float leftPad = 0, float rightPad = 0)
        {
            rect.x += leftPad;
            rect.width -= leftPad + rightPad;
            bool button1 = Widgets.ButtonImage(rect, null, true, tooltip);
            bool button2 = Widgets.ButtonText(rect, text);
            if (button1 || button2)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        public void UpdateSettings()
        {
            loggedSettings = false;
            ResearchTracker researchTracker = Current.Game?.World?.GetComponent<ResearchTracker>();
            if (researchTracker != null)
            {
                researchTracker.usingNodeResearch = usingNodeResearch;
                researchTracker.SettingsChanged();
            }
            ResearchTabWindowSwitcher.Apply();
            DumpSettingToLog();
        }

        // One-time: old configs saved Sleek as the tree button target. Reset to Node Research.
        // Users can still pick Sleek or YART after this version is written.
        public bool MigrateTreeButtonDefaultToNode()
        {
            if (settingsVersion >= 1)
                return false;

            preferredResearchTree = PreferredResearchTree.NodeResearch;
            settingsVersion = 1;
            return true;
        }

        public void DumpSettingToLog()
        {
            if (loggedSettings || !verboseLogging)
                return;

            loggedSettings = true;
            Log.Message($"[CM_Semi_Random_Research] Current settings are: featureEnabled: {featureEnabled} " +
                $"rerollAllEveryTime: {rerollAllEveryTime} " +
                $"forceLowestTechLevel: {forceLowestTechLevel} " +
                $"restrictToFactionTechLevel: {restrictToFactionTechLevel} " +
                $"allowOneHigherTechProject: {allowOneHigherTechProject} " +
                $"allowSwitchingResearch: {allowSwitchingResearch} " +
                $"progressAddsChoice: {progressAddsChoice} " +
                $"allowManualReroll: {allowManualReroll} " +
                $"amountSelection: {amountSelection} " +
                $"availableProjectCount: {availableProjectCount} " +
                $"additionalProjectPerXColonists: {additionalProjectPerXColonists} " +
                $"maxProjectCount: {maxProjectCount} " +
                $"reofferAfterAmountOfRerolls: {reofferAfterAmountOfRerolls} " +
                $"equalizeCost: {equalizeCost} " +
                $"verboseLogging: {verboseLogging} " +
                $"showResearchRateGraph: {showResearchRateGraph} " +
                $"showCompletionLetter: {showCompletionLetter} " +
                $"autoOpenOnCompletion: {autoOpenOnCompletion} " +
                $"autoPickNextResearch: {autoPickNextResearch} " +
                $"suppressHandoverMessages: {suppressHandoverMessages} " +
                $"colorAndGroupByTechLevel: {colorAndGroupByTechLevel}");
        }
    }
}
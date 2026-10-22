using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
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
        Sleek,
        NiceResearchTab,
        Organized
    }

    // Which window the Research main tab button opens. PreferredResearchTree is where this mod's tree button goes.
    public enum ResearchTabOwner
    {
        SemiRandom,
        Vanilla,
        NodeResearch,
        YART,
        Sleek,
        NiceResearchTab,
        Organized
    }

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
        public bool allowSnoozing = true;
        public ManualReroll allowManualReroll = ManualReroll.None;
        public ChoiceAmountSelection amountSelection = ChoiceAmountSelection.Static;
        public int availableProjectCount = 3;
        public int additionalProjectPerXColonists = 3;
        public int maxProjectCount = 6;
        public int reofferAfterAmountOfRerolls = 3;
        public bool equalizeCost = false;
        public bool verboseLogging = false;
        public ResearchTabOwner researchTabOwner = ResearchTabOwner.SemiRandom;
        public PreferredResearchTree preferredResearchTree = PreferredResearchTree.NodeResearch;
        public int settingsVersion;

        // The old "Delegate UI to Node Research" checkbox, read only by MigrateResearchTabOwner.
        private bool legacyUsingNodeResearch = false;
        public bool suppressHandoverMessages = false;
        public bool colorAndGroupByTechLevel = true;
        public bool adaptiveSize = false;
        public bool confirmDependentSnooze = true;
        public bool showCompletionDates = false;
        public bool timingsShowCurrentEra = false;

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
            Scribe_Values.Look(ref allowSnoozing, "allowSnoozing", true);
            Scribe_Values.Look(ref researchTabOwner, "researchTabOwner", ResearchTabOwner.SemiRandom);
            Scribe_Values.Look(ref legacyUsingNodeResearch, "usingNodeResearch", false);
            Scribe_Values.Look(ref preferredResearchTree, "preferredResearchTree", PreferredResearchTree.NodeResearch);
            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
            Scribe_Values.Look(ref suppressHandoverMessages, "suppressHandoverMessages", false);
            Scribe_Values.Look(ref colorAndGroupByTechLevel, "colorAndGroupByTechLevel", true);
            Scribe_Values.Look(ref adaptiveSize, "adaptiveSize", false);
            Scribe_Values.Look(ref confirmDependentSnooze, "confirmDependentSnooze", true);
            Scribe_Values.Look(ref showCompletionDates, "showCompletionDates", false);
            Scribe_Values.Look(ref timingsShowCurrentEra, "timingsShowCurrentEra", false);
        }

        private static readonly Color SectionColor = new Color(0.62f, 0.72f, 0.80f);

        // Choices from float menus and radio buttons, held until the next layout pass. Applying one mid-frame can
        // show or hide rows between layout and repaint, and Unity then discards the pass along with the choice.
        private ManualReroll? pendingManualReroll;
        private ProgressAddsChoice? pendingProgressAddsChoice;
        private ChoiceAmountSelection? pendingAmountSelection;

        private void ApplyPendingChoices(bool force)
        {
            if (!force && Event.current != null && Event.current.type != EventType.Layout)
                return;

            if (pendingManualReroll.HasValue)
            {
                allowManualReroll = pendingManualReroll.Value;
                pendingManualReroll = null;
            }
            if (pendingProgressAddsChoice.HasValue)
            {
                progressAddsChoice = pendingProgressAddsChoice.Value;
                pendingProgressAddsChoice = null;
            }
            if (pendingAmountSelection.HasValue)
            {
                amountSelection = pendingAmountSelection.Value;
                pendingAmountSelection = null;
            }
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            ApplyPendingChoices(false);

            Listing_Standard listing = new Listing_Standard();

            // Two columns, each half the window minus the gutter.
            listing.ColumnWidth = (inRect.width - 34f) / 2f;
            listing.Begin(inRect);

            // Column 1: general, display, research trees.
            // Auto research is toggled from the research tab, not here.
            SectionHeader(listing, "CM_Semi_Random_Research_Setting_Section_General");
            Checkbox(listing, "Feature_Enabled", ref featureEnabled);
            Checkbox(listing, "AutoOpen", ref autoOpenOnCompletion);
            Checkbox(listing, "ShowLetter", ref showCompletionLetter);
            Checkbox(listing, "AllowSnoozing", ref allowSnoozing);
            Checkbox(listing, "ConfirmDependentSnooze", ref confirmDependentSnooze);

            listing.GapLine();

            SectionHeader(listing, "CM_Semi_Random_Research_Setting_Section_Display");
            Checkbox(listing, "ColorGroup", ref colorAndGroupByTechLevel);
            Checkbox(listing, "ShowGraph", ref showResearchRateGraph);
            Checkbox(listing, "ShowCompletionDates", ref showCompletionDates);
            Checkbox(listing, "TimingsCurrentEra", ref timingsShowCurrentEra);
            Checkbox(listing, "AdaptiveSize", ref adaptiveSize);

            listing.GapLine();
            SectionHeader(listing, "CM_Semi_Random_Research_Setting_Section_Trees");

            // Without a tree mod the switch button always opens the vanilla tree, so these are greyed out.
            bool anyTree = ResearchTabWindowSwitcher.AnyTreeInstalled;
            string noTreeTip = anyTree ? null : "CM_Semi_Random_Research_Setting_Trees_LockedTip".Translate().ToString();
            string treeTooltip = noTreeTip ?? "CM_Semi_Random_Research_Setting_TreeButtonOpens_Description".Translate();
            GUI.color = anyTree ? Color.white : Color.gray;
            listing.Label("CM_Semi_Random_Research_Setting_TreeButtonOpens_Label".Translate(), -1, treeTooltip);
            GUI.color = Color.white;

            List<FloatMenuOption> treeOptions = new List<FloatMenuOption>();
            AddTreeOption(treeOptions, PreferredResearchTree.NodeResearch);
            AddTreeOption(treeOptions, PreferredResearchTree.YART);
            AddTreeOption(treeOptions, PreferredResearchTree.Sleek);
            AddTreeOption(treeOptions, PreferredResearchTree.NiceResearchTab);
            AddTreeOption(treeOptions, PreferredResearchTree.Organized);

            if (anyTree && !ResearchTabWindowSwitcher.IsTreeAvailable(preferredResearchTree))
                preferredResearchTree = ResearchTabWindowSwitcher.GetEffectivePreferredTree();

            Rect treeButtonOptionRect = listing.GetRect(26);
            DoButtonOption(treeButtonOptionRect,
                anyTree ? PreferredTreeLabel(ResearchTabWindowSwitcher.GetEffectivePreferredTree()) : "CM_Semi_Random_Research_TreeButton".Translate().ToString(),
                treeTooltip,
                treeOptions, treeButtonOptionRect.width / 10, treeButtonOptionRect.width / 10, anyTree);

            Checkbox(listing, "SuppressHandover", ref suppressHandoverMessages, noTreeTip);

            listing.GapLine();

            SectionHeader(listing, "CM_Semi_Random_Research_Setting_Section_Debug");
            Checkbox(listing, "Verbose_Logging", ref verboseLogging,
                Prefs.DevMode ? null : "CM_Semi_Random_Research_Setting_Verbose_Logging_LockedTip".Translate().ToString());

            // Column 2: selection rules, rerolls, amounts.
            listing.NewColumn();

            SectionHeader(listing, "CM_Semi_Random_Research_Setting_Section_Gameplay");
            Checkbox(listing, "Force_Lowest_Tech_Level", ref forceLowestTechLevel);

            // Node Research owns tech-level progression, so these two are locked while it is installed.
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

            Checkbox(listing, "Allow_Switching_Research", ref allowSwitchingResearch);
            Checkbox(listing, "Equalize_Cost", ref equalizeCost);

            string progressAddChoiceTooltip = EnumTooltip<ProgressAddsChoice>(
                "CM_Semi_Random_Research_Setting_Progress_Adds_Choice_Description",
                "CM_Semi_Random_Research_Setting_Progress_Adds_Choice_");
            listing.Label("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_Label".Translate(), -1, progressAddChoiceTooltip);
            Rect progressAddsRect = listing.GetRect(26);
            List<FloatMenuOption> progressAddsChoiceOptions = new List<FloatMenuOption>();
            foreach (ProgressAddsChoice option in System.Enum.GetValues(typeof(ProgressAddsChoice)))
            {
                ProgressAddsChoice captured = option;
                string keyLabel = ("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + option + "_Label").Translate();
                var menuOption = new FloatMenuOption(keyLabel, () => { pendingProgressAddsChoice = captured; });
                menuOption.tooltip = new TipSignal(("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + option + "_Description").Translate());
                progressAddsChoiceOptions.Add(menuOption);
            }
            DoButtonOption(progressAddsRect,
                ("CM_Semi_Random_Research_Setting_Progress_Adds_Choice_" + (pendingProgressAddsChoice ?? progressAddsChoice) + "_Label").Translate(),
                progressAddChoiceTooltip, progressAddsChoiceOptions, progressAddsRect.width / 10, progressAddsRect.width / 10);

            listing.GapLine();

            SectionHeader(listing, "CM_Semi_Random_Research_Setting_Section_Reroll");
            Checkbox(listing, "Reroll_All_Every_Time", ref rerollAllEveryTime);

            string rerollTooltip = EnumTooltip<ManualReroll>(
                "CM_Semi_Random_Research_Setting_Manual_Reroll_Description",
                "CM_Semi_Random_Research_Setting_Manual_Reroll_");
            listing.Label("CM_Semi_Random_Research_Setting_Manual_Reroll_Label".Translate(), -1, rerollTooltip);
            Rect rerollRect = listing.GetRect(26);
            List<FloatMenuOption> manualRerollOptions = new List<FloatMenuOption>();
            foreach (ManualReroll option in System.Enum.GetValues(typeof(ManualReroll)))
            {
                ManualReroll captured = option;
                string keyLabel = ("CM_Semi_Random_Research_Setting_Manual_Reroll_" + option + "_Label").Translate();
                var menuOption = new FloatMenuOption(keyLabel, () => { pendingManualReroll = captured; });
                menuOption.tooltip = new TipSignal(("CM_Semi_Random_Research_Setting_Manual_Reroll_" + option + "_Description").Translate());
                manualRerollOptions.Add(menuOption);
            }
            DoButtonOption(rerollRect,
                ("CM_Semi_Random_Research_Setting_Manual_Reroll_" + (pendingManualReroll ?? allowManualReroll) + "_Label").Translate(),
                rerollTooltip, manualRerollOptions, rerollRect.width / 10, rerollRect.width / 10);

            // Projects are suppressed after manual rerolls and after the reroll when a project is finished.
            bool rerollsHappen = allowManualReroll != ManualReroll.None || rerollAllEveryTime;
            IntSetting(listing, "Prevent_Rerolled_From_Appearing", ref reofferAfterAmountOfRerolls, 0,
                rerollsHappen ? null : "CM_Semi_Random_Research_Setting_Prevent_Rerolled_LockedTip".Translate().ToString());

            listing.GapLine();

            SectionHeader(listing, "CM_Semi_Random_Research_Setting_Section_Limits");
            string amountTooltip = "CM_Semi_Random_Research_Setting_Type_Of_Projects_Count_Description".Translate();
            listing.Label("CM_Semi_Random_Research_Setting_Type_Of_Projects_Count_Label".Translate(), -1, amountTooltip);
            ChoiceAmountSelection shownAmountSelection = pendingAmountSelection ?? amountSelection;
            if (listing.RadioButton("CM_Semi_Random_Research_Setting_Static_Projects_Count_Label".Translate(), shownAmountSelection == ChoiceAmountSelection.Static, 8f, "CM_Semi_Random_Research_Setting_Static_Projects_Count_Description".Translate()))
            {
                pendingAmountSelection = ChoiceAmountSelection.Static;
            }
            if (listing.RadioButton("CM_Semi_Random_Research_Setting_Dynamic_Projects_Count_Label".Translate(), shownAmountSelection == ChoiceAmountSelection.PerColonist, 8f, "CM_Semi_Random_Research_Setting_Dynamic_Projects_Count_Description".Translate()))
            {
                pendingAmountSelection = ChoiceAmountSelection.PerColonist;
            }

            // A constant count of 0 would offer nothing; per colonist, colonists can add choices to a base of 0.
            int minimumProjects = amountSelection == ChoiceAmountSelection.Static ? 1 : 0;
            IntSetting(listing, "Available_Projects_Count", ref availableProjectCount, minimumProjects);
            if (availableProjectCount < minimumProjects)
                availableProjectCount = minimumProjects;
            if (availableProjectCount > maxProjectCount)
            {
                maxProjectCount = availableProjectCount;
            }

            // Only the per colonist count uses these two.
            bool perColonist = amountSelection == ChoiceAmountSelection.PerColonist;
            string perColonistTip = perColonist ? null : "CM_Semi_Random_Research_Setting_PerColonist_LockedTip".Translate().ToString();
            IntSetting(listing, "Additional_Project_Per_XColonists", ref additionalProjectPerXColonists, 1, perColonistTip);
            IntSetting(listing, "Max_Projects", ref maxProjectCount, 1, perColonistTip);
            if (perColonist && availableProjectCount > maxProjectCount)
            {
                availableProjectCount = maxProjectCount;
            }

            listing.End();

            DumpSettingToLog();
        }

        private static void SectionHeader(Listing_Standard listing, string translationKey)
        {
            Text.Font = GameFont.Small;
            listing.Label(translationKey.Translate().Colorize(SectionColor));
        }

        // Checkbox labeled by <stem>_Label with <stem>_Description as its tooltip. With lockedTip it is greyed out and inert,
        // and lockedTip names the requirement.
        private static void Checkbox(Listing_Standard listing, string keyStem, ref bool value, string lockedTip = null)
        {
            string label = ("CM_Semi_Random_Research_Setting_" + keyStem + "_Label").Translate();
            if (lockedTip == null)
            {
                listing.CheckboxLabeled(label, ref value, ("CM_Semi_Random_Research_Setting_" + keyStem + "_Description").Translate());
                return;
            }

            Rect rect = listing.GetRect(Text.CalcHeight(label, listing.ColumnWidth));
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, lockedTip);
            GUI.color = Color.gray;
            Widgets.CheckboxLabeled(rect, label, ref value, disabled: true);
            GUI.color = Color.white;
            listing.Gap(listing.verticalSpacing);
        }

        // Label with its value and tooltip, and -/+ buttons on the same row so the limits section fits the window's height.
        // With lockedTip the row is greyed out and inert, and lockedTip names the requirement.
        private static void IntSetting(Listing_Standard listing, string keyStem, ref int value, int min, string lockedTip = null)
        {
            string label = ("CM_Semi_Random_Research_Setting_" + keyStem + "_Label").Translate() + ": " + value.ToString();
            string tooltip = lockedTip ?? ("CM_Semi_Random_Research_Setting_" + keyStem + "_Description").Translate();
            bool active = lockedTip == null;

            const float buttonWidth = 42f;
            const float buttonsWidth = buttonWidth * 2f + 2f;
            float labelWidth = listing.ColumnWidth - buttonsWidth - 8f;
            Rect row = listing.GetRect(Mathf.Max(24f, Text.CalcHeight(label, labelWidth)));

            Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            GUI.color = active ? Color.white : Color.gray;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            if (Mouse.IsOver(labelRect))
                Widgets.DrawHighlight(labelRect);
            TooltipHandler.TipRegion(labelRect, tooltip);

            Rect minusRect = new Rect(row.xMax - buttonsWidth, row.y + (row.height - 24f) / 2f, buttonWidth, 24f);
            Rect plusRect = new Rect(minusRect.xMax + 2f, minusRect.y, buttonWidth, 24f);
            if (Widgets.ButtonText(minusRect, "-1", active: active))
            {
                SoundDefOf.DragSlider.PlayOneShotOnCamera();
                value = Mathf.Max(min, value - GenUI.CurrentAdjustmentMultiplier());
            }
            if (Widgets.ButtonText(plusRect, "+1", active: active))
            {
                SoundDefOf.DragSlider.PlayOneShotOnCamera();
                value += GenUI.CurrentAdjustmentMultiplier();
            }
            listing.Gap(listing.verticalSpacing);
        }

        private static string EnumTooltip<T>(string headerKey, string optionKeyPrefix)
        {
            string tooltip = headerKey.Translate() + "\n\n";
            foreach (T option in System.Enum.GetValues(typeof(T)))
            {
                tooltip += (optionKeyPrefix + option + "_Label").Translate() + ": " +
                    (optionKeyPrefix + option + "_Description").Translate() + "\n\n";
            }
            return tooltip;
        }

        private void AddTreeOption(List<FloatMenuOption> options, PreferredResearchTree tree)
        {
            if (!ResearchTabWindowSwitcher.IsTreeAvailable(tree))
                return;

            options.Add(new FloatMenuOption(PreferredTreeLabel(tree), () => { SetPreferredTree(tree); }));
        }

        private void SetPreferredTree(PreferredResearchTree tree)
        {
            preferredResearchTree = tree;
        }

        private static string PreferredTreeLabel(PreferredResearchTree preferred)
        {
            if (preferred == PreferredResearchTree.YART)
                return "CM_Semi_Random_Research_Tree_YART".Translate();
            if (preferred == PreferredResearchTree.Sleek)
                return "CM_Semi_Random_Research_Tree_Sleek".Translate();
            if (preferred == PreferredResearchTree.NiceResearchTab)
                return "CM_Semi_Random_Research_Tree_Nice".Translate();
            if (preferred == PreferredResearchTree.Organized)
                return "CM_Semi_Random_Research_Tree_Organized".Translate();
            return "CM_Semi_Random_Research_Tree_NodeResearch".Translate();
        }

        private void DoButtonOption(Rect rect, string text, string tooltip, List<FloatMenuOption> options, float leftPad = 0, float rightPad = 0, bool active = true)
        {
            rect.x += leftPad;
            rect.width -= leftPad + rightPad;

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(rect, tooltip);

            if (Widgets.ButtonText(rect, text, active: active) && options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        // Applies a held choice before saving, in case the window closes before the next layout pass.
        public void FlushPendingChoices()
        {
            ApplyPendingChoices(true);
        }

        public void UpdateSettings()
        {
            loggedSettings = false;
            ResearchTracker researchTracker = Current.Game?.World?.GetComponent<ResearchTracker>();
            if (researchTracker != null)
            {
                researchTracker.SettingsChanged();
            }
            ResearchTabWindowSwitcher.Apply();
            DumpSettingToLog();
        }

        public bool MigrateSettings()
        {
            bool changed = MigrateTreeButtonDefaultToNode();
            changed |= MigrateResearchTabOwner();
            return changed;
        }

        // One-time: resets the tree button target, which old configs saved as Sleek, to Node Research.
        private bool MigrateTreeButtonDefaultToNode()
        {
            if (settingsVersion >= 1)
                return false;

            preferredResearchTree = PreferredResearchTree.NodeResearch;
            settingsVersion = 1;
            return true;
        }

        // One-time: derives the tab owner from the old "Delegate UI to Node Research" checkbox and the
        // "Prohibit normal project selection" rule, so the tab opens the same window after upgrading.
        // Checks package ids only: tree mods' assemblies may not be loaded yet in the Mod constructor.
        private bool MigrateResearchTabOwner()
        {
            if (settingsVersion >= 2)
                return false;

            if (legacyUsingNodeResearch)
            {
                researchTabOwner = ResearchTabOwner.NodeResearch;
            }
            else if (!featureEnabled)
            {
                // With the rule off the tab showed the vanilla window, which Sleek and Research: Organized redress.
                if (ResearchTabWindowSwitcher.SleekInstalled)
                    researchTabOwner = ResearchTabOwner.Sleek;
                else if (ResearchTabWindowSwitcher.OrganizedInstalled)
                    researchTabOwner = ResearchTabOwner.Organized;
                else
                    researchTabOwner = ResearchTabOwner.Vanilla;
            }
            else
            {
                researchTabOwner = ResearchTabOwner.SemiRandom;
            }

            settingsVersion = 2;
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
                $"allowSnoozing: {allowSnoozing} " +
                $"confirmDependentSnooze: {confirmDependentSnooze} " +
                $"showCompletionDates: {showCompletionDates} " +
                $"timingsShowCurrentEra: {timingsShowCurrentEra} " +
                $"suppressHandoverMessages: {suppressHandoverMessages} " +
                $"colorAndGroupByTechLevel: {colorAndGroupByTechLevel} " +
                $"adaptiveSize: {adaptiveSize}");
        }
    }
}

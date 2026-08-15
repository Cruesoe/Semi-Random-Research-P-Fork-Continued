using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CM_Semi_Random_Research
{
    // All Node Research integration lives here so a Node Research update
    // (window class name, DrawGraphControls layout, foundation/emergence extensions)
    // can be reviewed in one file.

    [StaticConstructorOnStartup]
    public static class ResearchTabWindowSwitcher
    {
        public const string PackageId = "ferny.noderesearch";
        public const string WindowTypeName = "BetterResearchMenu.MainTabWindow_BetterResearch";
        public const string YartPackageId = "seohyeon.yart";
        public const string YartWindowTypeName = "YART.MainTabWindow_YART";
        public const string SleekPackageId = "squishyjellyfish.SleekResearchTab";

        private static Type cachedNodeResearchWindowType;
        private static Type cachedYartWindowType;

        private static readonly FieldInfo TabWindowIntField =
            AccessTools.Field(typeof(MainButtonDef), "tabWindowInt");

        static ResearchTabWindowSwitcher()
        {
            Apply();
        }

        private static MainButtonDef ResearchMainButton =>
            DefDatabase<MainButtonDef>.GetNamedSilentFail("Research");

        public static Type NodeResearchWindowType =>
            cachedNodeResearchWindowType
            ?? (cachedNodeResearchWindowType = AccessTools.TypeByName(WindowTypeName));

        public static bool NodeResearchInstalled =>
            ModLister.GetActiveModWithIdentifier(PackageId) != null;

        public static Type YartWindowType =>
            cachedYartWindowType
            ?? (cachedYartWindowType = AccessTools.TypeByName(YartWindowTypeName));

        public static bool YartInstalled =>
            ModLister.GetActiveModWithIdentifier(YartPackageId) != null;

        public static bool SleekInstalled =>
            ModLister.GetActiveModWithIdentifier(SleekPackageId) != null;

        public static void SetUsingNodeResearch(bool value)
        {
            if (SemiRandomResearchMod.settings != null)
            {
                SemiRandomResearchMod.settings.usingNodeResearch = value;
            }

            ResearchTracker tracker = Current.Game?.World?.GetComponent<ResearchTracker>();
            if (tracker != null)
            {
                tracker.usingNodeResearch = value;
            }
        }

        public static void Apply()
        {
            MainButtonDef researchTab = ResearchMainButton;
            if (researchTab == null || SemiRandomResearchMod.settings == null)
            {
                return;
            }

            Type windowType;
            if (SemiRandomResearchMod.settings.usingNodeResearch && NodeResearchWindowType != null)
            {
                windowType = NodeResearchWindowType;
            }
            else if (SemiRandomResearchMod.settings.featureEnabled)
            {
                windowType = typeof(MainTabWindow_NextResearch);
            }
            else
            {
                windowType = typeof(MainTabWindow_Research);
            }

            SetResearchWindowClass(researchTab, windowType);
        }

        private static void SetResearchWindowClass(MainButtonDef researchTab, Type windowType)
        {
            if (researchTab == null)
            {
                return;
            }

            MainTabWindow cached = TabWindowIntField?.GetValue(researchTab) as MainTabWindow;
            if (researchTab.tabWindowClass == windowType &&
                cached != null &&
                windowType.IsInstanceOfType(cached))
            {
                return;
            }

            researchTab.tabWindowClass = windowType;
            TabWindowIntField?.SetValue(researchTab, null);
            researchTab.ClearCachedData();
        }

        public static void OpenResearchWindow(Type windowType, Window windowToClose)
        {
            MainButtonDef researchTab = ResearchMainButton;
            if (researchTab == null)
            {
                return;
            }

            windowToClose?.Close();
            SetResearchWindowClass(researchTab, windowType);

            MainTabWindow newWindow = researchTab.TabWindow;
            if (newWindow == null)
            {
                return;
            }

            WindowStack stack = Find.WindowStack;
            IList<Window> windows = stack.Windows;
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                Window existing = windows[i];
                if (existing != newWindow && windowType.IsInstanceOfType(existing))
                    existing.Close(doCloseSound: false);
            }

            if (!stack.IsOpen(newWindow) && !stack.Windows.Contains(newWindow))
            {
                stack.Add(newWindow);
            }
        }

        private static void ShowHandoverMessage(string text)
        {
            if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.suppressHandoverMessages)
                return;
            Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
        }

        public static void SwitchToNodeResearch(Window windowToClose)
        {
            if (!NodeResearchInstalled || NodeResearchWindowType == null)
            {
                return;
            }

            SetUsingNodeResearch(true);
            if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.featureEnabled)
            {
                ShowHandoverMessage("Switched to Node Research. Prohibit normal project selection is still on.");
            }
            else
            {
                ShowHandoverMessage("Switched to Node Research. Free selection enabled.");
            }

            OpenResearchWindow(NodeResearchWindowType, windowToClose);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToSemiRandomResearch(Window windowToClose)
        {
            SetUsingNodeResearch(false);
            if (NodeResearchInstalled || YartInstalled || SleekInstalled)
            {
                if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.featureEnabled)
                {
                    ShowHandoverMessage("Switched to Semi-Random Research. Selection restricted.");
                }
                else
                {
                    ShowHandoverMessage("Switched to Semi-Random Research.");
                }
            }

            ResearchProjectDef activeProj = Find.ResearchManager.GetProject();
            ResearchTracker tracker = Current.Game?.World?.GetComponent<ResearchTracker>();
            if (tracker != null && activeProj != null && !tracker.CurrentProject.Contains(activeProj))
            {
                tracker.SetCurrentProject(activeProj, activeProj.knowledgeCategory);
            }

            OpenResearchWindow(typeof(MainTabWindow_NextResearch), windowToClose);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToYart(Window windowToClose)
        {
            if (!YartInstalled || YartWindowType == null)
            {
                return;
            }

            SetUsingNodeResearch(false);
            if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.featureEnabled)
            {
                ShowHandoverMessage("Switched to YART. Prohibit normal project selection is still on.");
            }
            else
            {
                ShowHandoverMessage("Switched to YART.");
            }

            OpenResearchWindow(YartWindowType, windowToClose);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static void SwitchToSleek(Window windowToClose)
        {
            if (!SleekInstalled)
            {
                return;
            }

            SetUsingNodeResearch(false);
            if (SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.featureEnabled)
            {
                ShowHandoverMessage("Switched to Sleek Research Tab. Prohibit normal project selection is still on.");
            }
            else
            {
                ShowHandoverMessage("Switched to Sleek Research Tab.");
            }

            OpenResearchWindow(typeof(MainTabWindow_Research), windowToClose);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }

        public static Type ResolvePreferredTreeWindowType()
        {
            PreferredResearchTree preferred = SemiRandomResearchMod.settings != null
                ? SemiRandomResearchMod.settings.preferredResearchTree
                : PreferredResearchTree.NodeResearch;

            if (preferred == PreferredResearchTree.Sleek && SleekInstalled)
            {
                return typeof(MainTabWindow_Research);
            }

            if (preferred == PreferredResearchTree.YART && YartInstalled && YartWindowType != null)
            {
                return YartWindowType;
            }

            if (preferred == PreferredResearchTree.NodeResearch && NodeResearchInstalled && NodeResearchWindowType != null)
            {
                return NodeResearchWindowType;
            }

            if (SleekInstalled)
            {
                return typeof(MainTabWindow_Research);
            }

            if (YartInstalled && YartWindowType != null)
            {
                return YartWindowType;
            }

            if (NodeResearchInstalled && NodeResearchWindowType != null)
            {
                return NodeResearchWindowType;
            }

            return typeof(MainTabWindow_Research);
        }

        public static void SwitchToPreferredTree(Window windowToClose)
        {
            Type windowType = ResolvePreferredTreeWindowType();
            if (windowType == NodeResearchWindowType)
            {
                SwitchToNodeResearch(windowToClose);
                return;
            }

            if (windowType == YartWindowType)
            {
                SwitchToYart(windowToClose);
                return;
            }

            if (SleekInstalled)
            {
                SwitchToSleek(windowToClose);
                return;
            }

            OpenResearchWindow(typeof(MainTabWindow_Research), windowToClose);
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }
    }

    [StaticConstructorOnStartup]
    public static class NodeResearch_Integration
    {
        private static readonly Texture2D TexSemiRandom = ContentFinder<Texture2D>.Get("UI/semi", true);

        static NodeResearch_Integration()
        {
            if (!ResearchTabWindowSwitcher.NodeResearchInstalled)
            {
                return;
            }

            var type = ResearchTabWindowSwitcher.NodeResearchWindowType;
            if (type == null)
            {
                return;
            }

            var original = AccessTools.Method(type, "DrawGraphControls");
            if (original == null)
            {
                Log.Warning("[Semi Random Research] Node Research DrawGraphControls was not found. The swap button may be missing.");
                return;
            }

            var harmony = new Harmony("CM_Semi_Random_Research.NodeIntegration");
            var transpiler = AccessTools.Method(typeof(NodeResearch_Integration), nameof(DrawGraphControls_Transpiler));
            var postfix = AccessTools.Method(typeof(NodeResearch_Integration), nameof(DrawGraphControls_Postfix));
            harmony.Patch(original, transpiler: new HarmonyMethod(transpiler), postfix: new HarmonyMethod(postfix));
            Log.Message("[Semi Random Research] Successfully integrated with Node Research UI.");
        }

        // Shifts Node Research's settings button 32px right so our third-slot button fits.
        // Anchored on the "BRM_OpenVanillaMenu" tooltip string, then the next Add
        // (vanillaBtnRect.xMax + gap). If Node Research reorders those controls, this
        // is the first place to update.
        public static IEnumerable<CodeInstruction> DrawGraphControls_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool foundVanillaTip = false;
            bool shiftedSettings = false;

            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (!foundVanillaTip && instruction.opcode == OpCodes.Ldstr && instruction.operand is string str && str == "BRM_OpenVanillaMenu")
                {
                    foundVanillaTip = true;
                }

                if (foundVanillaTip && !shiftedSettings && instruction.opcode == OpCodes.Add)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 32f);
                    yield return new CodeInstruction(OpCodes.Add);
                    shiftedSettings = true;
                    foundVanillaTip = false;
                }
            }
        }

        public static void DrawGraphControls_Postfix(Rect controlAreaRect, Window __instance)
        {
            float btnSize = 24f;
            float btnGap = 8f;
            float xOffset = (btnSize + btnGap) * 2;

            Rect semiBtnRect = new Rect(controlAreaRect.x + xOffset, controlAreaRect.y, btnSize, btnSize);

            if (Widgets.ButtonImage(semiBtnRect, TexSemiRandom))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                ResearchTabWindowSwitcher.SwitchToSemiRandomResearch(__instance);
                Event.current.Use();
            }
            TooltipHandler.TipRegion(semiBtnRect, "Open Semi-Random Research");
        }
    }

    public static class NodeResearch
    {
        public static bool IsFoundationTech(ResearchProjectDef def)
        {
            if (def == null || def.modExtensions == null)
                return false;

            return def.modExtensions.Any(ext => ext.GetType().Name == "ResearchFoundationExtension");
        }

        public static bool IsEmergenceTech(ResearchProjectDef def)
        {
            if (def == null || def.modExtensions == null)
                return false;

            return def.modExtensions.Any(ext => ext.GetType().Name == "EmergenceExtension");
        }
    }
}

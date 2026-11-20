using CM_Semi_Random_Research;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace SemiRandomResearchUIRethemed
{
    // Semi Random Research drawn with UI Rethemed - Core. Every Core call sits in its own non-inlined method inside
    // try/catch, so a Core API change fails at JIT time into ResearchSkin.Fail and the mod's own look.
    public sealed class UIRethemedSkin : ResearchSkin
    {
        private const string PackageId = "cruesoe.semirandompfork.continued";

        private static bool IsRepaint => Event.current.type == EventType.Repaint;

        private bool Ok => Available;

        private bool Caught(Exception e)
        {
            Fail(e);
            return false;
        }

        public override void Begin()
        {
            if (!Ok)
                return;
            try { BeginCore(); }
            catch (Exception e) { Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BeginCore() => Theme.BeginMod(SemiRandomResearchMod.Instance);

        public override void End()
        {
            try { EndCore(); }
            catch (Exception e) { Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EndCore() => Theme.EndMod();

        public override Color TextBright => ReadColor(0);
        public override Color TextDim => ReadColor(1);
        public override Color TextFaint => ReadColor(2);
        public override Color Track => ReadColor(3);

        private Color ReadColor(int which)
        {
            try { return ThemeColorCore(which); }
            catch (Exception e) { Caught(e); return Color.white; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Color ThemeColorCore(int which)
        {
            switch (which)
            {
                case 0: return Theme.TextBright;
                case 1: return Theme.TextDim;
                case 2: return Theme.TextFaint;
                default: return Theme.BGDark;
            }
        }

        public override Color? CategoryColor(string key)
        {
            if (!Ok)
                return null;
            try { return CategoryColorCore(key); }
            catch (Exception e) { Caught(e); return null; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Color CategoryColorCore(string key) => Theme.ModColor(PackageId, key);

        public override bool TitleHasSettings
        {
            get
            {
                if (!Ok)
                    return false;
                try { return SettingsGearCore(); }
                catch (Exception e) { return Caught(e); }
            }
        }

        // Core's "settings gear on title bars" option.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool SettingsGearCore() => UIRethemedCoreMod.Settings == null || UIRethemedCoreMod.Settings.settingsGear;

        public override bool Frame(Rect inRect, string title, Texture icon, float age, Action close, Mod settings, out Rect content)
        {
            content = inRect;
            if (!Ok)
                return false;
            try
            {
                content = FrameCore(inRect, title, icon, age, close, settings);
                return true;
            }
            catch (Exception e) { content = inRect; return Caught(e); }
        }

        private static Texture titleIcon;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Rect FrameCore(Rect inRect, string title, Texture icon, float age, Action close, Mod settings)
        {
            if (icon == null)
                icon = titleIcon ?? (titleIcon = CoreWidgets.Icon("flask"));
            // Main tab windows sit on the bottom bar at the screen's left edge, so only the top right corner is rounded.
            Rounded.WindowBackground(inRect, Corners.TopRight);
            Rect inner = inRect.ContractedBy(6f);
            Rect titleBar = new Rect(inner.x, inner.y, inner.width, 40f);
            CoreWidgets.TitleBar(titleBar, icon, title, ThemeMotion.On ? age : float.MaxValue, close, settings, 0f);
            return new Rect(inner.x, titleBar.yMax + 6f, inner.width, inner.yMax - titleBar.yMax - 6f);
        }

        public override bool PopupFrame(Rect inRect, string title, float age, Action close, out Rect content)
        {
            content = inRect;
            if (!Ok)
                return false;
            try
            {
                content = PopupFrameCore(inRect, title, age, close);
                return true;
            }
            catch (Exception e) { content = inRect; return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Rect PopupFrameCore(Rect inRect, string title, float age, Action close)
        {
            Rect titleBar = new Rect(inRect.x, inRect.y, inRect.width, 36f);
            Rounded.BeginBatch();
            try
            {
                Rounded.WindowBackground(inRect);
                Rounded.Fill(titleBar, Theme.TitleBarFill, Corners.Top);
            }
            finally
            {
                Rounded.EndBatch();
            }
            Faces.FadingHex(titleBar, Theme.TitleAccent, Theme.TitleBarFill, 1f, 0.6f);
            CoreWidgets.Accent(titleBar, Theme.TitleAccent, ThemeMotion.On ? age : float.MaxValue);
            Rounded.Border(inRect, Rounded.WindowBorderColor);
            float right = CoreWidgets.HeaderButtons(titleBar, close);
            CoreWidgets.Line(new Rect(titleBar.x + 12f, titleBar.y, Mathf.Max(0f, right - titleBar.x - 16f), titleBar.height), title, Color.white);
            return new Rect(inRect.x + 10f, titleBar.yMax + 8f, inRect.width - 20f, inRect.height - titleBar.height - 18f);
        }

        public override bool Card(Rect rect, Color accent, float progress, bool selected, bool dim, float alpha)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                CardCore(rect, accent, progress, selected, dim, alpha);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CardCore(Rect rect, Color accent, float progress, bool selected, bool dim, float alpha)
        {
            float hover = dim ? 0f : ThemeMotion.Hover(rect, 52917, Mouse.IsOver(rect));
            Rounded.BeginBatch();
            try
            {
                Rounded.Fill(rect, Faces.Fade(Color.Lerp(Theme.BG, accent, dim ? 0.1f : 0.28f), alpha));
                if (progress > 0f)
                {
                    float width = Mathf.Round(rect.width * Mathf.Clamp01(progress));
                    if (width >= 1f)
                    {
                        Rounded.Fill(new Rect(rect.x, rect.y, width, rect.height),
                            Faces.Fade(Color.Lerp(Theme.BG, accent, 0.55f), alpha),
                            width >= rect.width - Rounded.Radius ? Corners.All : Corners.TopLeft | Corners.BottomLeft);
                    }
                }
                if (hover > 0f)
                    Rounded.Fill(rect.ContractedBy(2f), new Color(1f, 1f, 1f, 0.06f * hover * alpha));
                Color edge = dim ? Theme.BGDark : selected ? Color.Lerp(accent, Color.white, 0.45f) : accent;
                Rounded.Border(rect, Faces.Fade(edge, alpha));
                if (selected)
                    Rounded.Border(rect.ContractedBy(1f), Faces.Fade(edge, alpha * 0.8f));
            }
            finally
            {
                Rounded.EndBatch();
            }
            if (hover > 0f)
                CoreWidgets.EdgeGlow(rect, Faces.Fade(accent, alpha));
        }

        public override bool Row(Rect rect)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                RowCore(rect);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RowCore(Rect rect) => CoreWidgets.ListRow(rect, false, false, Theme.HighlightGray);

        private static readonly Dictionary<string, Texture> panelIcons = new Dictionary<string, Texture>();

        public override bool Section(Rect rect, string title, string icon)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                SectionCore(rect, title, icon);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SectionCore(Rect rect, string title, string icon)
        {
            Texture texture = null;
            if (icon != null && !panelIcons.TryGetValue(icon, out texture))
                panelIcons[icon] = texture = CoreWidgets.Icon(icon);
            CoreWidgets.Panel(rect, title, texture, out Rect titleBar);
            Faces.FadingHex(titleBar, Theme.TitleAccent, Theme.TitleBarFill, 0.6f, 0.5f);
            Rounded.Border(rect, Theme.BGDark);
        }

        public override bool Tile(Rect tile, Rect well, Color accent)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                TileCore(tile, well, accent);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TileCore(Rect tile, Rect well, Color accent)
        {
            float hover = ThemeMotion.Hover(tile, 41203, Mouse.IsOver(tile));
            Rounded.BeginBatch();
            try
            {
                Rounded.Fill(tile, Theme.BG);
                if (hover > 0f)
                    Rounded.Fill(tile.ContractedBy(1f), new Color(1f, 1f, 1f, 0.05f * hover));
                Rounded.Border(tile, Color.Lerp(Theme.BGDark, accent, 0.3f + 0.7f * hover));
            }
            finally
            {
                Rounded.EndBatch();
            }
            Faces.SlotBack(well, accent, 1f, true);
            if (hover > 0f)
                Theme.SlotGlow(well, Faces.Fade(accent, 0.6f * hover));
            Faces.SlotOutline(well, Color.Lerp(Theme.BGLight, accent, 0.4f + 0.6f * hover));
        }

        public override bool Button(Rect rect, string label, SkinTone tone, bool enabled)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                ButtonCore(rect, label, tone, enabled);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        // Drawn on repaint only, where Core's ButtonInvisible never reports a click.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ButtonCore(Rect rect, string label, SkinTone tone, bool enabled)
        {
            CoreWidgets.Button(rect, label, null, ToneColor(tone), enabled, null, GameFont.Small, null, false, enabled && tone != SkinTone.Gray);
        }

        private static Color ToneColor(SkinTone tone)
        {
            switch (tone)
            {
                case SkinTone.Green: return Theme.HighlightGreen;
                case SkinTone.Blue: return Theme.HighlightBlue;
                case SkinTone.Yellow: return Theme.HighlightYellow;
                case SkinTone.Red: return Theme.HighlightRed;
                default: return Theme.HighlightGray;
            }
        }

        public override bool IconButton(Rect rect, Texture icon, bool on)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                IconButtonCore(rect, icon, on);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void IconButtonCore(Rect rect, Texture icon, bool on)
        {
            CoreWidgets.IconButton(rect, icon, null, null, on, Theme.TextYellow, true, Mathf.Round(rect.width * 0.75f));
        }

        public override bool Switch(Rect rect, bool on)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                SwitchCore(rect, on);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SwitchCore(Rect rect, bool on) => Theme.Switch(rect, on);

        public override bool Divider(float x, float y, float width)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                DividerCore(x, y, width);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DividerCore(float x, float y, float width) => Theme.Divider(x, y, width);

        public override bool BarTrack(Rect rect)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                BarTrackCore(rect);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        // The title bar's face: its fill with the drifting pattern in the title accent, fading across the whole track.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BarTrackCore(Rect rect)
        {
            Rounded.Fill(rect, Theme.TitleBarFill);
            Faces.FadingHex(rect, Theme.TitleAccent, Theme.TitleBarFill, 1f, 1f);
            Rounded.Border(rect, Theme.BGLight);
        }

        public override bool BarFill(Rect rect, float fraction, Color color)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                BarFillCore(rect, fraction, color);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BarFillCore(Rect rect, float fraction, Color color)
        {
            Rect inner = rect.ContractedBy(2f);
            inner.width = Mathf.Round(inner.width * Mathf.Clamp01(fraction));
            if (inner.width < 1f)
                return;
            Theme.Fill(inner, color);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width, inner.height / 2f), Theme.VGradientTex);
            GUI.color = Color.white;
        }

        public override bool Panel(Rect rect)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                PanelCore(rect);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PanelCore(Rect rect)
        {
            Rounded.BeginBatch();
            try
            {
                Rounded.Fill(rect, Theme.BG);
                Rounded.Border(rect, Theme.BGLight);
            }
            finally
            {
                Rounded.EndBatch();
            }
        }

        private object plot;
        private readonly List<Vector2> plotPoints = new List<Vector2>();

        public override bool Graph(Rect rect, List<float> samples, float average, string averageLabel, int key)
        {
            if (!Ok)
                return false;
            if (!IsRepaint)
                return true;
            try
            {
                GraphCore(rect, samples, average, averageLabel, key);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        // The line is rendered into a texture only when the samples, size or theme change; LinePlot keeps it between frames.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GraphCore(Rect rect, List<float> samples, float average, string averageLabel, int key)
        {
            Rounded.Fill(rect, Theme.BGDark);
            if (samples == null || samples.Count == 0)
                return;

            Rect area = rect.ContractedBy(10f);
            float max = 0.1f;
            for (int i = 0; i < samples.Count; i++)
                max = Mathf.Max(max, samples[i]);
            max *= 1.2f;

            LinePlot linePlot = plot as LinePlot;
            if (linePlot == null)
            {
                linePlot = new LinePlot();
                plot = linePlot;
            }
            if (linePlot.Begin(area, key))
            {
                plotPoints.Clear();
                float step = samples.Count > 1 ? area.width / (samples.Count - 1) : 0f;
                for (int i = 0; i < samples.Count; i++)
                    plotPoints.Add(new Vector2(i * step, area.height - Mathf.Clamp01(samples[i] / max) * area.height));
                linePlot.Line(plotPoints, Theme.HighlightGreen, 2f, 0.35f);
                linePlot.End();
            }
            linePlot.Draw(area);

            if (average > 0f && average <= max)
            {
                float y = Mathf.Round(area.yMax - average / max * area.height);
                Text.Font = GameFont.Small;
                float labelWidth = Text.CalcSize(averageLabel).x + 10f;
                Rect label = new Rect(area.xMax - labelWidth, Mathf.Clamp(y - 11f, area.y, area.yMax - 22f), labelWidth, 22f);
                Theme.Fill(new Rect(area.x, y, label.x - 4f - area.x, 1f), Faces.Fade(Theme.HighlightYellow, 0.8f));
                Rounded.Fill(label, Faces.Fade(Theme.BG, 0.9f));
                Theme.Label(label, averageLabel, TextAnchor.MiddleCenter, Theme.TextYellow);
            }
        }

        private static readonly HashSet<string> openSections = new HashSet<string>();
        private static readonly HashSet<string> seenSections = new HashSet<string>();
        private object settingsCards;
        private Vector2 leftScroll;
        private Vector2 rightScroll;
        private float leftHeight = 600f;
        private float rightHeight = 600f;
        private List<SkinSettingsSection> drawingLeft;
        private List<SkinSettingsSection> drawingRight;
        private Func<float, float, float> leftCards;
        private Func<float, float, float> rightCards;

        public override bool SettingsPage(Rect inRect, List<SkinSettingsSection> left, List<SkinSettingsSection> right)
        {
            if (!Ok)
                return false;
            try
            {
                SettingsPageCore(inRect, left, right);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SettingsPageCore(Rect inRect, List<SkinSettingsSection> left, List<SkinSettingsSection> right)
        {
            SettingsCards cards = settingsCards as SettingsCards;
            if (cards == null)
            {
                cards = new SettingsCards("SemiRandomResearch", () => openSections);
                settingsCards = cards;
                leftCards = (y, width) => DrawCards(drawingLeft, y, width);
                rightCards = (y, width) => DrawCards(drawingRight, y, width);
            }
            // Every section starts open; a player's collapse lasts for the session.
            OpenNew(left);
            OpenNew(right);
            drawingLeft = left;
            drawingRight = right;

            Theme.BeginMod(SemiRandomResearchMod.Instance);
            try
            {
                float columnWidth = Mathf.Floor((inRect.width - 20f) / 2f);
                Rect leftRect = new Rect(inRect.x, inRect.y, columnWidth, inRect.height);
                Rect rightRect = new Rect(inRect.xMax - columnWidth, inRect.y, columnWidth, inRect.height);
                leftHeight = cards.Column(leftRect, ref leftScroll, leftHeight, leftCards);
                rightHeight = cards.Column(rightRect, ref rightScroll, rightHeight, rightCards);
            }
            finally
            {
                Theme.CloseScrolls();
                Theme.Reset();
                Theme.EndMod();
            }
            cards.Finish();
        }

        private static void OpenNew(List<SkinSettingsSection> sections)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                if (seenSections.Add(sections[i].key))
                    openSections.Add(sections[i].key);
            }
        }

        private float DrawCards(List<SkinSettingsSection> sections, float y, float width)
        {
            SettingsCards cards = (SettingsCards)settingsCards;
            for (int i = 0; i < sections.Count; i++)
            {
                SkinSettingsSection section = sections[i];
                y = cards.Card(y, width, section.key, SectionIcon(section.key), section.title, null, section.body);
            }
            return y;
        }

        private static readonly Dictionary<string, Texture> sectionIcons = new Dictionary<string, Texture>();

        private static Texture SectionIcon(string key)
        {
            if (sectionIcons.TryGetValue(key, out Texture icon))
                return icon;
            string name;
            switch (key)
            {
                case "General": name = "sliders"; break;
                case "Display": name = "eye"; break;
                case "Trees": name = "tree-structure"; break;
                case "Debug": name = "bug"; break;
                case "Gameplay": name = "dice-five"; break;
                case "Reroll": name = "arrow-clockwise"; break;
                case "Limits": name = "list-numbers"; break;
                default: name = "flask"; break;
            }
            icon = CoreWidgets.Icon(name);
            sectionIcons[key] = icon;
            return icon;
        }

        public override bool Tick(Listing_Standard listing, string label, ref bool value, string tip, bool enabled)
        {
            if (!Ok)
                return false;
            try
            {
                TickCore(listing, label, ref value, tip, enabled);
                return true;
            }
            catch (Exception e) { return Caught(e); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TickCore(Listing_Standard listing, string label, ref bool value, string tip, bool enabled)
        {
            SettingsCards.Tick(listing, null, Theme.TextDim, label, ref value, tip, enabled);
        }
    }
}

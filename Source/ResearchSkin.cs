using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // A look supplied by an optional assembly (the UI Rethemed add-on, loaded only with UI Rethemed - Core).
    // Draw methods return false when they drew nothing, and the caller then draws the mod's own look.
    public abstract class ResearchSkin
    {
        private static ResearchSkin registered;
        private static bool failed;

        public static void Register(ResearchSkin skin)
        {
            registered = skin;
        }

        // The add-on is loaded and has not failed this session.
        public static bool Available => registered != null && !failed;

        public static ResearchSkin Active =>
            registered != null && !failed && (SemiRandomResearchMod.settings == null || SemiRandomResearchMod.settings.matchUIRethemed)
                ? registered
                : null;

        // A themed draw threw (usually a Core update that changed its API): the mod's own look is used for the rest of the session.
        public static void Fail(Exception e)
        {
            if (failed)
                return;
            failed = true;
            Log.Warning("[Semi Random Research] UI Rethemed look turned off for this session after an error: " + e);
        }

        // Applies the player's colors for this mod (Core's per-mod palette) to everything drawn until End.
        public abstract void Begin();
        public abstract void End();

        public abstract Color TextBright { get; }
        public abstract Color TextDim { get; }
        public abstract Color TextFaint { get; }
        public abstract Color Track { get; }

        // A category color by key (an era's name, "anomalyBasic", "anomalyAdvanced", "gravship"); null leaves the mod's own.
        public abstract Color? CategoryColor(string key);

        // True when Frame's title bar shows a settings gear for the mod passed to it.
        public abstract bool TitleHasSettings { get; }

        // Window background and title bar; content is the area left for the window's own drawing.
        public abstract bool Frame(Rect inRect, string title, Texture icon, float age, Action close, Mod settings, out Rect content);

        // A popup's background and title bar with a close button.
        public abstract bool PopupFrame(Rect inRect, string title, float age, Action close, out Rect content);

        // A project card: accent face with optional progress fill. Hover is animated by the skin.
        public abstract bool Card(Rect rect, Color accent, float progress, bool selected, bool dim, float alpha);

        // A plain list row, hover-lit.
        public abstract bool Row(Rect rect);

        // A raised panel with a title bar around a group of rows; icon is one of the skin's icon names.
        public abstract bool Section(Rect rect, string title, string icon);

        // An unlock tile: a card with a sunken well behind the icon, tinted by accent and lit on hover.
        public abstract bool Tile(Rect tile, Rect well, Color accent);

        // Button face and label only; clicks stay with the caller.
        public abstract bool Button(Rect rect, string label, SkinTone tone, bool enabled);

        // Icon button face only; clicks stay with the caller.
        public abstract bool IconButton(Rect rect, Texture icon, bool on);

        // On/off switch face only.
        public abstract bool Switch(Rect rect, bool on);

        public abstract bool Divider(float x, float y, float width);

        // Progress bar parts: the empty track and a filled share of it.
        public abstract bool BarTrack(Rect rect);
        public abstract bool BarFill(Rect rect, float fraction, Color color);

        // A sunken panel behind a group of values (the timings strip).
        public abstract bool Panel(Rect rect);

        // Research speed graph: samples as a filled line, average as a labeled level line. key changes when the samples change.
        public abstract bool Graph(Rect rect, List<float> samples, float average, string averageLabel, int key);

        // Settings page: sections as cards in two columns.
        public abstract bool SettingsPage(Rect inRect, List<SkinSettingsSection> left, List<SkinSettingsSection> right);

        // Checkbox row inside a settings card; value is flipped on click.
        public abstract bool Tick(Listing_Standard listing, string label, ref bool value, string tip, bool enabled);
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // Reads the cost level Research: Costs tags each project with (its ResearchLevelExtension), by type and field name so there
    // is no reference to that mod, and draws it as pips. Inert without it: no project has a level. Lifetime: session.
    [StaticConstructorOnStartup]
    public static class ResearchLevels
    {
        private const string PackageId = "cruesoe.research.costs";
        private const string ExtensionTypeName = "ResearchCosts.ResearchLevelExtension";
        private const int MaxLevels = 5;
        public const float PipSize = 7f;
        public const float PipGap = 3f;

        private static readonly Texture2D PipFilledTex = BuildPipTexture(32, true);
        private static readonly Texture2D PipEmptyTex = BuildPipTexture(32, false);

        private static bool resolved;
        private static Type extensionType;
        private static FieldInfo levelField;
        private static FieldInfo levelsField;

        // Level and level count per project; (0, 0) for a project without a level.
        private static readonly Dictionary<ResearchProjectDef, (int level, int levels)> cache = new Dictionary<ResearchProjectDef, (int, int)>();

        public static bool Available
        {
            get
            {
                Resolve();
                return extensionType != null;
            }
        }

        public static bool ShowLevels => SemiRandomResearchMod.settings != null && SemiRandomResearchMod.settings.showResearchLevels && Available;

        public static float PipsWidth(int count) => count <= 0 ? 0f : count * PipSize + (count - 1) * PipGap;

        // The project's level and its era's level count, when Research: Costs set it.
        public static bool TryGet(ResearchProjectDef project, out int level, out int levels)
        {
            level = levels = 0;
            if (project == null || !Available)
                return false;
            if (!cache.TryGetValue(project, out var entry))
            {
                entry = Read(project);
                cache[project] = entry;
            }
            level = entry.level;
            levels = entry.levels;
            return levels > 0;
        }

        // Pips filled up to the level, with the era's other levels empty, vertically centered in the rect.
        public static void DrawPips(Rect rect, int level, int levels, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            float y = rect.y + (rect.height - PipSize) / 2f;
            for (int i = 0; i < levels; i++)
            {
                Rect pip = new Rect(rect.x + i * (PipSize + PipGap), y, PipSize, PipSize);
                GUI.DrawTexture(pip, i < level ? PipFilledTex : PipEmptyTex);
            }
            GUI.color = old;
        }

        private static (int, int) Read(ResearchProjectDef project)
        {
            if (project.modExtensions == null)
                return (0, 0);
            foreach (DefModExtension extension in project.modExtensions)
            {
                if (extension == null || extension.GetType() != extensionType)
                    continue;
                int level = levelField.GetValue(extension) is int l ? l : 0;
                int levels = levelsField.GetValue(extension) is int c ? c : 0;
                if (level >= 1 && level <= levels && levels <= MaxLevels)
                    return (level, levels);
            }
            return (0, 0);
        }

        private static void Resolve()
        {
            if (resolved)
                return;
            resolved = true;
            if (!ModsConfig.IsActive(PackageId))
                return;
            Type type = GenTypes.GetTypeInAnyAssembly(ExtensionTypeName);
            FieldInfo level = type?.GetField("level", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo levels = type?.GetField("levels", BindingFlags.Instance | BindingFlags.Public);
            if (level == null || levels == null || level.FieldType != typeof(int) || levels.FieldType != typeof(int))
            {
                if (type != null)
                    Log.Warning("[Semi Random Research] Research: Costs' level tag has changed shape; levels aren't shown.");
                return;
            }
            extensionType = type;
            levelField = level;
            levelsField = levels;
        }

        // A white disc, filled or as a ring, tinted by GUI.color when drawn. Built in code so the mod ships no texture.
        private static Texture2D BuildPipTexture(int size, bool filled)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float radius = size / 2f;
            float ring = size * 0.2f;
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    float distance = Vector2.Distance(new Vector2(column + 0.5f, row + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - distance);
                    if (!filled)
                        alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - (radius - ring)));
                    texture.SetPixel(column, row, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}

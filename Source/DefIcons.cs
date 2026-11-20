using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // Draws a def's icon, covering the defs Widgets.DefIcon leaves blank (memes, precepts, styles and similar).
    public static class DefIcons
    {
        // Icons found by member name, per def; session lifetime (defs never change after load).
        private static readonly Dictionary<Def, Texture2D> fallbackIcons = new Dictionary<Def, Texture2D>();

        private static readonly string[] TextureMembers = { "Icon", "uiIcon", "icon", "IconTex" };
        private static readonly string[] PathMembers = { "iconPath", "uiIconPath", "texPath" };

        public static void Draw(Rect rect, Def def)
        {
            if (def == null)
                return;
            try
            {
                if (VanillaDraws(def))
                {
                    Widgets.DefIcon(rect, def);
                    return;
                }
                Texture2D icon = Fallback(def);
                if (icon != null)
                    Widgets.DrawTextureFitted(rect, icon, 1f);
            }
            catch (Exception)
            {
            }
        }

        // The defs Widgets.DefIcon gives an icon.
        private static bool VanillaDraws(Def def)
        {
            if (def is RecipeDef recipe)
                return recipe.UIIconThing != null || recipe.UIIcon != null;
            if (def is TerrainDef terrain)
                return terrain.uiIcon != null;
            return def is ThingDef || def is PawnKindDef || def is FactionDef || def is StyleItemDef || def is BodyTypeDef
                || def is HeadTypeDef || def is GeneDef || def is XenotypeDef || def is PsychicRitualDef;
        }

        private static Texture2D Fallback(Def def)
        {
            if (fallbackIcons.TryGetValue(def, out Texture2D cached))
                return cached;

            Texture2D icon = null;
            if (def is RecipeDef recipe)
                icon = RecipeIcon(recipe);
            if (icon == null)
                icon = FindTexture(def);
            fallbackIcons[def] = icon;
            return icon;
        }

        // A surgery or recipe without its own icon: what it makes, else the part it installs.
        private static Texture2D RecipeIcon(RecipeDef recipe)
        {
            if (!recipe.products.NullOrEmpty() && recipe.products[0].thingDef != null)
                return recipe.products[0].thingDef.uiIcon;
            ThingDef part = recipe.addsHediff?.spawnThingOnRemoved;
            return part?.uiIcon;
        }

        private static Texture2D FindTexture(Def def)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = def.GetType();
            for (int i = 0; i < TextureMembers.Length; i++)
            {
                PropertyInfo property = type.GetProperty(TextureMembers[i], flags);
                if (property != null && typeof(Texture).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0
                    && property.GetValue(def, null) is Texture2D fromProperty && fromProperty != BaseContent.BadTex)
                    return fromProperty;
                FieldInfo field = type.GetField(TextureMembers[i], flags);
                if (field != null && field.GetValue(def) is Texture2D fromField && fromField != BaseContent.BadTex)
                    return fromField;
            }
            for (int i = 0; i < PathMembers.Length; i++)
            {
                FieldInfo field = type.GetField(PathMembers[i], flags);
                if (field != null && field.GetValue(def) is string path && !path.NullOrEmpty())
                {
                    Texture2D texture = ContentFinder<Texture2D>.Get(path, false);
                    if (texture != null)
                        return texture;
                }
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace CM_Semi_Random_Research
{
    // A project's unlocks narrowed to what finishing it makes available, leaving out what also waits on other unfinished research.
    public static class UnlocksNow
    {
        // Research fields on def types added by other mods, per type; session lifetime.
        private static readonly Dictionary<Type, FieldInfo[]> researchFields = new Dictionary<Type, FieldInfo[]>();

        // A new list; built when the selection changes, never per frame.
        public static List<Def> Filter(ResearchProjectDef project, List<Def> unlocked)
        {
            List<Def> result = new List<Def>();
            if (project == null || unlocked == null)
                return result;

            List<RecipeDef> recipes = null;
            for (int i = 0; i < unlocked.Count; i++)
            {
                Def def = unlocked[i];
                if (def != null && AvailableAfter(def, project, ref recipes))
                    result.Add(def);
            }
            return result;
        }

        // True when some route from project to def needs no other unfinished research; a def with no route found is kept.
        private static bool AvailableAfter(Def def, ResearchProjectDef project, ref List<RecipeDef> recipes)
        {
            bool linked = false;

            if (def is BuildableDef buildable && Links(buildable.researchPrerequisites, null, project, ref linked))
                return true;
            if (def is RecipeDef recipe && Links(recipe.researchPrerequisites, recipe.researchPrerequisite, project, ref linked))
                return true;
            if (def is ThingDef thing)
            {
                if (thing.plant != null && Links(thing.plant.sowResearchPrerequisites, null, project, ref linked))
                    return true;

                if (recipes == null)
                    recipes = RecipesFor(project);
                for (int i = 0; i < recipes.Count; i++)
                {
                    if (Makes(recipes[i], thing) && Links(recipes[i].researchPrerequisites, recipes[i].researchPrerequisite, project, ref linked))
                        return true;
                }
            }
            if (!(def is BuildableDef) && !(def is RecipeDef) && OtherModLinks(def, project, ref linked))
                return true;

            return !linked;
        }

        // Whether list or single names project (sets linked), and every other entry is finished.
        private static bool Links(List<ResearchProjectDef> list, ResearchProjectDef single, ResearchProjectDef project, ref bool linked)
        {
            bool names = single == project || (list != null && list.Contains(project));
            if (!names)
                return false;
            linked = true;
            if (single != null && single != project && !single.IsFinished)
                return false;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    ResearchProjectDef other = list[i];
                    if (other != null && other != project && !other.IsFinished)
                        return false;
                }
            }
            return true;
        }

        private static List<RecipeDef> RecipesFor(ResearchProjectDef project)
        {
            List<RecipeDef> result = new List<RecipeDef>();
            List<RecipeDef> all = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                RecipeDef recipe = all[i];
                if (recipe.researchPrerequisite == project || (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Contains(project)))
                    result.Add(recipe);
            }
            return result;
        }

        private static bool Makes(RecipeDef recipe, ThingDef thing)
        {
            if (recipe.products == null)
                return false;
            for (int i = 0; i < recipe.products.Count; i++)
            {
                if (recipe.products[i]?.thingDef == thing)
                    return true;
            }
            return false;
        }

        // Defs another mod adds to the unlocks (memes, rituals and similar) by researchPrerequisite(s) fields.
        private static bool OtherModLinks(Def def, ResearchProjectDef project, ref bool linked)
        {
            FieldInfo[] fields = ResearchFields(def.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                object value = fields[i].GetValue(def);
                if (Links(value as List<ResearchProjectDef>, value as ResearchProjectDef, project, ref linked))
                    return true;
            }
            return false;
        }

        private static FieldInfo[] ResearchFields(Type type)
        {
            if (researchFields.TryGetValue(type, out FieldInfo[] cached))
                return cached;

            List<FieldInfo> found = new List<FieldInfo>();
            FieldInfo[] all = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < all.Length; i++)
            {
                Type fieldType = all[i].FieldType;
                if (fieldType == typeof(ResearchProjectDef) || fieldType == typeof(List<ResearchProjectDef>))
                    found.Add(all[i]);
            }
            cached = found.ToArray();
            researchFields[type] = cached;
            return cached;
        }
    }
}

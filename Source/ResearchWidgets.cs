using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // Controls shared by the research window and the buttons added to other research trees.
    internal static class ResearchWidgets
    {
        // MouseDown hit-test only. Widgets.Button* change control IDs when Layout and Repaint draw different counts.
        public static bool Clicked(Rect rect)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
                return false;
            if (!Mouse.IsOver(rect))
                return false;
            Event.current.Use();
            return true;
        }

        public static bool RightClicked(Rect rect)
        {
            if (Event.current.type != EventType.MouseDown || Event.current.button != 1)
                return false;
            if (!Mouse.IsOver(rect))
                return false;
            Event.current.Use();
            return true;
        }

        // Filled, bordered button face with a centered label. Click handling is left to the caller.
        public static void DrawColoredButton(Rect rect, string label, Color fill)
        {
            bool mouseOver = Mouse.IsOver(rect);

            if (Event.current.type == EventType.Repaint)
            {
                Color bg = fill;
                if (mouseOver && Input.GetMouseButton(0))
                    bg = fill * 0.75f;
                else if (mouseOver)
                    bg = Color.Lerp(fill, Color.white, 0.16f);

                Widgets.DrawBoxSolid(rect, bg);

                Color old = GUI.color;
                GUI.color = Color.Lerp(fill, Color.black, 0.4f);
                Widgets.DrawBox(rect);
                GUI.color = old;
            }

            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        public static bool ColoredButton(Rect rect, string label, Color fill)
        {
            DrawColoredButton(rect, label, fill);
            return Clicked(rect);
        }
    }
}

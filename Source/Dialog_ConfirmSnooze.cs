using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CM_Semi_Random_Research
{
    // Vanilla confirmation size, listing the projects a snooze would also set aside; Enter confirms, Escape goes back.
    public class Dialog_ConfirmSnooze : Window
    {
        private const float DialogWidth = 640f;
        private const float TitleHeight = 42f;
        private const float ButtonHeight = 35f;
        private const float CardHeight = 36f;

        private readonly ResearchProjectDef project;
        private readonly List<ResearchProjectDef> blocked;
        private readonly Action onConfirm;
        private readonly string body;
        private readonly float[] cardY;
        private readonly float listHeight;
        private Vector2 scroll;

        public Dialog_ConfirmSnooze(ResearchProjectDef project, List<ResearchProjectDef> blocked, Action onConfirm)
        {
            this.project = project;
            this.blocked = blocked;
            this.onConfirm = onConfirm;
            body = "CM_Semi_Random_Research_ConfirmSnooze".Translate();
            // Projects arrive sorted by tech level; each new tech level starts after a wider gap.
            cardY = new float[blocked.Count];
            float next = 0f;
            for (int i = 0; i < blocked.Count; i++)
            {
                if (i > 0)
                    next += blocked[i].techLevel != blocked[i - 1].techLevel ? 14f : 4f;
                cardY[i] = next;
                next += CardHeight;
            }
            listHeight = next;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        // Vanilla 640 x 460 at least, taller to fit every card; scrolls only when the screen can't fit them.
        public override Vector2 InitialSize
        {
            get
            {
                Text.Font = GameFont.Small;
                float bodyHeight = Text.CalcHeight(body, DialogWidth - Margin * 2f);
                float height = Margin * 2f + TitleHeight + 4f + bodyHeight + 10f + listHeight + 14f + ButtonHeight;
                return new Vector2(DialogWidth, Mathf.Clamp(height, 460f, UI.screenHeight - 80f));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), "CM_Semi_Random_Research_ConfirmSnoozeTitle".Translate(project.LabelCap));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float y = inRect.y + TitleHeight + 4f;
            float bodyHeight = Text.CalcHeight(body, inRect.width);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, bodyHeight), body);
            y += bodyHeight + 10f;

            Rect outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - ButtonHeight - 14f - y);
            bool scrolls = listHeight > outRect.height;
            Rect view = new Rect(0f, 0f, outRect.width - (scrolls ? 24f : 0f), listHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                for (int i = 0; i < blocked.Count; i++)
                    DrawCard(new Rect(0f, cardY[i], view.width, CardHeight), blocked[i]);
            }
            finally
            {
                Widgets.EndScrollView();
            }

            float buttonWidth = (inRect.width - 20f) / 2f;
            Rect back = new Rect(inRect.x, inRect.yMax - ButtonHeight, buttonWidth, ButtonHeight);
            if (Widgets.ButtonText(back, "GoBack".Translate()))
                Close();
            if (Widgets.ButtonText(new Rect(back.xMax + 20f, back.y, buttonWidth, ButtonHeight), "CM_Semi_Random_Research_SnoozeButton".Translate()))
                Confirm();
        }

        // Colored like the research tab's cards, with the project's name.
        private static void DrawCard(Rect rect, ResearchProjectDef def)
        {
            Color color = MainTabWindow_NextResearch.GetCategoryColor(def);
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawBoxSolid(rect, Color.Lerp(TexUI.AvailResearchColor, color, 0.25f));
                GUI.color = MainTabWindow_NextResearch.GetCardStructureAccent(def, color);
                Widgets.DrawBox(rect);
                GUI.color = Color.white;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), def.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override void OnAcceptKeyPressed() => Confirm();

        private void Confirm()
        {
            onConfirm();
            Close();
        }
    }
}

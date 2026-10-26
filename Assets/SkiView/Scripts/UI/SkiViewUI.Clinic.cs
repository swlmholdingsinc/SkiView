using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>b10.17 Clinic Phase A UI — T16 handoff + T33 frames. List → Plan → Pass → Results. Stance only playable.</summary>
    public partial class SkiViewUI
    {
        VisualElement clinicList, clinicPlan, clinicResults;
        VisualElement clinicListBody, clinicCueBody, clinicNotesBody;
        Label clinicListTitle, clinicPlanTitle, clinicPlanPrompt, clinicPlanTip, clinicResultsTitle, clinicData, clinicPending;
        Button clinicStartBtn;
        string clinicPendingLesson, clinicPendingCue;
        readonly List<Button> clinicCueBtns = new List<Button>();

        void BuildClinic()
        {
            SkiViewClinic.Ensure();
            string mode = SkiViewClinic.ModeName().ToUpperInvariant();

            clinicList = Menu(out var listSafe, "TITLE", mode, () => { CloseMenus(); OpenTitle(); });
            clinicListTitle = listSafe.Q(className: "sv-header")?.Q<Label>(className: "t-h1");
            if (clinicListTitle != null) clinicListTitle.text = mode;
            var listPlate = El("sv-plate sv-plate--card", El("sv-body", listSafe)); El("sv-strip", listPlate);
            Lb("LESSONS", "t-micro", listPlate);
            Lb("Pick a lesson. Stance is playable now — unlock the rest by completing the ladder.", "t-caption", listPlate).style.whiteSpace = WhiteSpace.Normal;
            clinicListBody = El(null, listPlate);
            clinicListBody.style.marginTop = 12;

            clinicPlan = Menu(out var planSafe, mode, "PLAN YOUR PASS", () => OpenClinicList());
            var planPlate = El("sv-plate sv-plate--card", El("sv-body", planSafe)); El("sv-strip", planPlate);
            clinicPlanTitle = Lb("", "t-h2", planPlate);
            clinicPlanPrompt = Lb("Pick one focus for this run.", "t-caption", planPlate);
            clinicPlanPrompt.style.whiteSpace = WhiteSpace.Normal;
            clinicPlanTip = Lb("", "t-caption", planPlate);
            clinicPlanTip.style.whiteSpace = WhiteSpace.Normal; clinicPlanTip.style.marginTop = 8; clinicPlanTip.style.marginBottom = 8;
            clinicCueBody = El(null, planPlate); clinicCueBody.style.marginTop = 16; clinicCueBody.style.marginBottom = 16;
            clinicStartBtn = Btn("START PASS", "sv-btn sv-btn--primary", planPlate, StartClinicPass, "play");
            clinicStartBtn.SetEnabled(false);
            Lb("PENDING_WADE_APPROVAL", "t-micro", planPlate).style.unityFontStyleAndWeight = FontStyle.Bold;

            clinicResults = El("sv-screen sv-menu", root, "clinic-results"); menus.Add(clinicResults);
            El("sv-scrim-full", clinicResults);
            var rSafe = El("sv-safe", clinicResults);
            var rPlate = El("sv-plate sv-plate--card sv-results19", rSafe); El("sv-strip", rPlate);
            clinicResultsTitle = Lb("CLINIC RESULT", "t-h2", rPlate);
            clinicPending = Lb("PENDING_WADE_APPROVAL", "t-micro", rPlate);
            clinicPending.style.color = new StyleColor(new Color(0.85f, 0.7f, 0.2f));
            Lb("DATA", "t-micro", rPlate);
            clinicData = Lb("", "t-caption", rPlate); clinicData.style.whiteSpace = WhiteSpace.Normal;
            Lb("NOTES", "t-micro", rPlate);
            clinicNotesBody = El(null, rPlate);
            var act = El("sv-results19__act", rPlate);
            Btn("RETRY", "sv-btn sv-btn--primary", act, () => { SkiViewClinic.CloseResults(); CloseMenus(); OpenClinicPlan(SkiViewClinic.ActiveLessonId ?? "stance"); }, "retry");
            Btn("LESSONS", "sv-btn", act, () => { SkiViewClinic.CloseResults(); OpenClinicList(); }, "back");
            Btn("TITLE", "sv-btn sv-btn--ghost", act, () => { SkiViewClinic.ClearActive(); CloseMenus(); OpenTitle(); }, null);
        }

        public void OpenClinicList()
        {
            SkiViewClinic.Ensure();
            RebuildClinicList();
            Open(clinicList);
        }

        void RebuildClinicList()
        {
            clinicListBody.Clear();
            var data = SkiViewClinic.Data;
            if (data?.lessons == null) return;
            // order: ladder first then optional deep_water_start at end if present
            var ordered = new List<SkiViewClinic.Lesson>();
            foreach (var id in SkiViewClinic.Ladder)
            {
                var L = SkiViewClinic.Get(id); if (L != null) ordered.Add(L);
            }
            foreach (var L in data.lessons)
                if (L.id == "deep_water_start") ordered.Add(L);
            foreach (var L in data.lessons)
                if (!ordered.Contains(L)) ordered.Add(L);

            foreach (var L in ordered)
            {
                bool unlocked = SkiViewClinic.IsUnlocked(L.id);
                bool playable = SkiViewClinic.IsPlayable(L.id);
                bool done = SkiViewClinic.IsComplete(L.id);
                string title = L.title?.text ?? L.id;
                string goal = L.goal?.text ?? "";
                var row = new Button(() => OnClinicLessonTap(L.id)) { focusable = true };
                row.AddToClassList("srow");
                row.style.marginBottom = 8;
                row.SetEnabled(true);
                var txt = El("srow__txt", row);
                string tag = done ? "DONE" : (playable ? "PLAY" : (unlocked ? "COMING SOON" : "LOCKED"));
                Lb($"{title.ToUpperInvariant()}  ·  {tag}", "t-label", txt);
                string sub = playable ? ((L.optional || L.id == "deep_water_start") ? "Optional · " + goal : goal)
                    : (unlocked ? "Coming soon" : "Complete the previous lesson to unlock");
                if (done) sub = $"Best {SkiViewClinic.BestBuoys(L.id):0.#} buoys · {goal}";
                var cap = Lb(sub, "t-caption", txt); cap.style.whiteSpace = WhiteSpace.Normal;
                if (!unlocked && !playable) row.style.opacity = 0.55f;
                clinicListBody.Add(row);
            }
        }

        void OnClinicLessonTap(string id)
        {
            if (!SkiViewClinic.IsPlayable(id))
            {
                // toast via title SoonToast if available — otherwise banner
                if (game != null) { game.banner = SkiViewClinic.IsUnlocked(id) ? "COMING SOON" : "LOCKED · COMPLETE PRIOR LESSON"; game.bannerT = 2f; }
                return;
            }
            OpenClinicPlan(id);
        }

        void OpenClinicPlan(string lessonId)
        {
            var L = SkiViewClinic.Get(lessonId);
            if (L == null) return;
            clinicPendingLesson = lessonId;
            clinicPendingCue = null;
            clinicPlanTitle.text = (L.title?.text ?? lessonId).ToUpperInvariant();
            var plan = SkiViewClinic.Data?.plan_step;
            clinicPlanPrompt.text = plan?.prompt?.text ?? "Pick one focus for this run.";
            string tip = SkiViewClinic.PlanTip(lessonId);
            if (clinicPlanTip != null)
            {
                if (!string.IsNullOrEmpty(tip)) { clinicPlanTip.text = tip; clinicPlanTip.style.display = DisplayStyle.Flex; }
                else clinicPlanTip.style.display = DisplayStyle.None;
            }
            clinicCueBody.Clear(); clinicCueBtns.Clear();
            if (L.focus_cues != null)
            {
                foreach (var c in L.focus_cues)
                {
                    string cid = c.id;
                    string label = c.text?.text ?? cid;
                    var b = Btn(label.ToUpperInvariant(), "sv-btn", clinicCueBody, () => SelectClinicCue(cid), null);
                    b.style.marginBottom = 8; b.style.width = Length.Percent(100);
                    clinicCueBtns.Add(b);
                    b.userData = cid;
                }
            }
            clinicStartBtn.SetEnabled(false);
            Open(clinicPlan);
        }

        void SelectClinicCue(string cueId)
        {
            clinicPendingCue = cueId;
            foreach (var b in clinicCueBtns)
                b.EnableInClassList("sv-btn--primary", (string)b.userData == cueId);
            clinicStartBtn.SetEnabled(true);
        }

        void StartClinicPass()
        {
            if (game == null || string.IsNullOrEmpty(clinicPendingLesson) || string.IsNullOrEmpty(clinicPendingCue)) return;
            SkiViewClinic.BeginLesson(game, clinicPendingLesson, clinicPendingCue);
            CloseMenus();
        }

        void TickClinicResults()
        {
            if (clinicResults == null || game == null) return;
            if (SkiViewClinic.Active && SkiViewClinic.ResultsOpen)
            {
                if (!clinicResults.ClassListContains("sv-menu--open"))
                {
                    var L = SkiViewClinic.Get(SkiViewClinic.ActiveLessonId);
                    bool ok = SkiViewClinic.LastComplete;
                    clinicResultsTitle.text = ok ? "LESSON COMPLETE" : (SkiViewClinic.LastFell ? "PASS ENDED · FALL" : "PASS ENDED");
                    float hl = SkiViewClinic.LastHandleLow;
                    string hlTxt = hl < 0f ? "— (not recorded yet)" : $"{hl * 100f:0}%";
                    clinicData.text = SkiViewClinic.DataReadout(game);
                    clinicNotesBody.Clear();
                    foreach (var line in SkiViewClinic.FeedbackLines())
                    {
                        var n = Lb("• " + line, "t-caption", clinicNotesBody);
                        n.style.whiteSpace = WhiteSpace.Normal; n.style.marginBottom = 6;
                    }
                    if (clinicNotesBody.childCount == 0)
                        Lb("• Keep the focus cue for the next try.", "t-caption", clinicNotesBody);
                    Open(clinicResults);
                }
            }
            else if (clinicResults.ClassListContains("sv-menu--open") && !SkiViewClinic.ResultsOpen)
            {
                clinicResults.RemoveFromClassList("sv-menu--open");
            }
        }
    }
}

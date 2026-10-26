using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>Run Setup: T28 ski picker (slalom); T31 Jump speed/ramp/rope note; Tricks speed/rope.</summary>
    public partial class SkiViewUI
    {
        VisualElement runSetup, runSetupBody; int suSki, suBind, suRope, suMph, suTrickKmh, suTrickRope, suJumpKmh, suJumpRamp;

        void BuildRunSetup()
        {
            runSetup = Menu(out var safe, "STEP 1 OF 3", "RUN SETUP", NavBack);
            var body = El("sv-body", safe);
            var plate = El("sv-plate sv-plate--card sv-panel", body); El("sv-strip", plate);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; plate.Add(sv); runSetupBody = sv.contentContainer;
        }

        static string RopeLen(int i)
        {
            float m = SkierSim.ROPE_M[i];
            return Kmh ? $"{m:0.00} M" : $"{m * 3.2808f:0.0} FT";
        }

        void OpenRunSetup()
        {
            if (runSetup == null) BuildRunSetup();
            suSki = SkiViewGame.SetupSki; suBind = SkiViewGame.SetupBinding; suRope = SkiViewGame.SetupRope; suMph = SkiViewGame.SetupMph;
            suTrickKmh = SkiViewGame.SetupTrickKmhIdx; suTrickRope = SkiViewGame.SetupTrickRopeIdx;
            suJumpKmh = SkiViewGame.SetupJumpKmhIdx; suJumpRamp = SkiViewGame.SetupJumpRampIdx;
            var keep = panelBody; panelBody = runSetupBody; panelBody.Clear();
            if (panelTricks)
            {
                // b10.26 T29b: opaque plate, T17b real BAS/REV ints (no TBD), T17 speeds locked
                runSetup.Query(className: "sv-plate").First()?.AddToClassList("sv-panel--trick");
                Lb("TRICK SKI", "t-label", panelBody);
                Lb("Finless trick-v1 · double boot · unbranded. Speed and rope are skier-chosen for the whole round.", "t-caption", panelBody);
                Seg(Row("BOAT SPEED", "CONSTANT FOR BOTH PASSES · IWWF SKIER-CHOSEN · DEF 31"), SkiViewGame.TRICK_KMH_LABEL, suTrickKmh, k => suTrickKmh = k);
                Seg(Row("ROPE LENGTH", "ANY LENGTH · PHASE A STEPS"), SkiViewGame.TRICK_ROPE_LABEL, suTrickRope, k => suTrickRope = k);
                var ptsRow = El("srow", panelBody);
                Lb("POINT VALUES", "t-micro", ptsRow);
                Lb("S40 · B/F60 · O/BB90 · WB/WF80", "sv-pts-chip", ptsRow);
                Lb("1-ski BAS=REV · IWWF S2 / T17b · Holly Nov-2 re-check non-blocking", "t-caption", panelBody);
            }
            else if (panelJump)
            {
                // T31 Jump Phase A setup: speed chips + ramp + fixed 23.0 m rope · Choose lake
                Lb("JUMP ROUND", "t-label", panelBody);
                Lb("3 passes · longest jump counts · helmet on · two skis", "t-caption", panelBody);
                var sr = Row("BOAT SPEED", "IWWF 10.05 STEPS · DEFAULT 54 km/h");
                Seg(sr, SkiViewGame.JUMP_KMH_LABEL, suJumpKmh, k => suJumpKmh = k); sr.AddToClassList("srow--wrap");
                Seg(Row("RAMP HEIGHT", "DEFAULT 1.65 m · WOMEN 1.80 GREY LATER"), SkiViewGame.JUMP_RAMP_LABEL, suJumpRamp, k => suJumpRamp = k);
                var rope = Row("ROPE", "FIXED FOR JUMP · AWSA / T15");
                Lb("23.0 m", "t-label", rope);
                Lb("Pylon to handle · including bridle", "t-caption", rope);
            }
            else
            {
                SkiPicker();
                var labels = new string[SkiViewGame.SETUP_ROPES];
                for (int i = 0; i < labels.Length; i++) labels[i] = SkierSim.ROPE_LABEL[i] + "\n" + RopeLen(i);
                var rr = Row("STARTING ROPE", "PROGRESSION SHORTENS THE ROPE FROM HERE AFTER EACH FULL PASS");
                Seg(rr, labels, suRope, k => suRope = k); rr.AddToClassList("srow--wrap");
                Seg(Row("BOAT SPEED", "FIXED FOR THE WHOLE RUN"), new[] { SpeedMph(34f).ToUpperInvariant(), SpeedMph(36f).ToUpperInvariant() }, suMph == 36 ? 1 : 0, k => suMph = k == 1 ? 36 : 34);
            }
            var act = El("srow srow--setup-act", panelBody);
            // b10.27 T24 D1: phone Done/Choose lake ≥144 (sv-btn--start-tall under .sv-phone)
            if (!panelTricks) Btn("CHOOSE LAKE", "sv-btn sv-btn--start-tall", act, () => { Save(); lakesFromSetup = true; Open(lakes); });
            Btn("START", "sv-btn sv-btn--primary sv-btn--start-tall", act, () => {
                Save(); game?.ApplySetup();
                if (panelTricks) game?.BeginTrickRound();
                else if (panelJump) game?.BeginJumpRound();
                titleReturn = false; CloseMenus();
            }, "play");
            panelBody = keep;
            Open(runSetup);
        }

        /// <summary>Public for ?ui=setup deep link / capture.</summary>
        public void OpenSetupCapture() { panelTricks = false; panelJump = false; OpenRunSetup(); }
        /// <summary>Public for Jump Phase A / T31 setup capture.</summary>
        public void OpenJumpSetupCapture() { panelTricks = false; panelJump = true; OpenRunSetup(); }
        public void OpenTricksSetupCapture() { panelTricks = true; panelJump = false; OpenRunSetup(); }

        /// <summary>b10.13 T28: Gear / Your ski — left colourway list with thumbs, large hero preview, Toe plate | Double boot.</summary>
        void SkiPicker()
        {
            var blk = El("sv-skipick", panelBody); blk.focusable = true;
            Lb("GEAR", "t-micro sv-skipick__kick", blk);
            Lb("Your ski", "t-label sv-skipick__title", blk);
            Lb("Pick a colourway · then binding", "t-caption", blk);

            var body = El("sv-skipick__body", blk);
            var list = El("sv-skipick__list", body);
            var hero = El("sv-skipick__hero", body);
            var hn = Lb("", "sv-skipick__name", hero);
            var img = El("sv-skipick__img", hero);
            var cap = Lb("", "t-caption sv-skipick__cap", hero);

            var items = new System.Collections.Generic.List<VisualElement>();
            void Show()
            {
                string path = suBind == 1 ? ("SkiViewUI/Skis/ski_" + suSki + "_db") : ("SkiViewUI/Skis/ski_" + suSki);
                var tex = Resources.Load<Texture2D>(path);
                if (tex == null) tex = Resources.Load<Texture2D>("SkiViewUI/Skis/ski_" + suSki);
                img.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground();
                hn.text = SkiViewGame.SKI_NAMES[suSki];
                cap.text = SkiViewGame.SKI_NAMES[suSki] + " · " + SkiViewGame.BIND_NAMES[suBind];
                for (int i = 0; i < items.Count; i++)
                {
                    items[i].EnableInClassList("sv-skipick__it--on", i == suSki);
                    var eq = items[i].Q<Label>("eq");
                    if (eq != null) eq.style.display = i == suSki ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            for (int i = 0; i < SkiViewGame.SKI_NAMES.Length; i++)
            {
                int k = i;
                var it = new Button(() => { suSki = k; Show(); }) { focusable = true };
                it.AddToClassList("sv-skipick__it");
                var sw = El("sv-skipick__sw", it);
                var th = Resources.Load<Texture2D>("SkiViewUI/Skis/thumb_" + k);
                if (th != null) sw.style.backgroundImage = new StyleBackground(th);
                Lb(SkiViewGame.SKI_NAMES[k], "sv-skipick__it-nm", it);
                var eq = Lb("Equipped", "sv-skipick__eq", it); eq.name = "eq";
                list.Add(it); items.Add(it);
            }

            var bindRow = El("sv-skipick__bind", hero);
            var bl = El("sv-skipick__bind-lab", bindRow);
            Lb("Rear binding", "sv-skipick__bind-t", bl);
            Lb("Front stays a single boot", "t-caption", bl);
            var op = El("sv-skipick__op", bindRow);
            var bs = new System.Collections.Generic.List<Button>();
            for (int i = 0; i < SkiViewGame.BIND_NAMES.Length; i++)
            {
                int k = i;
                var b = new Button(() =>
                {
                    suBind = k;
                    for (int j = 0; j < bs.Count; j++) bs[j].EnableInClassList("sv-skipick__op-on", j == k);
                    Show();
                }) { text = SkiViewGame.BIND_NAMES[i] };
                b.AddToClassList("sv-skipick__op-item");
                if (i == suBind) b.AddToClassList("sv-skipick__op-on");
                op.Add(b); bs.Add(b);
            }

            Show();
        }

        void Save()
        {
            if (panelTricks) SkiViewGame.SaveTrickSetup(suTrickKmh, suTrickRope);
            else if (panelJump) SkiViewGame.SaveJumpSetup(suJumpKmh, suJumpRamp);
            else SkiViewGame.SaveSetup(suSki, suBind, suRope, suMph);
        }

        string SetupSummaryTricks()
        {
            int k = SkiViewGame.SetupTrickKmhIdx, r = SkiViewGame.SetupTrickRopeIdx;
            return $"Last setup: {game?.lakeName ?? "Ski View"} · {SkiViewGame.TRICK_KMH[k]:0} km/h · {SkiViewGame.TRICK_ROPE_M[r]:0.0} m · trick-v1";
        }

        string SetupSummary(bool jump)
        {
            string ski = SkiViewGame.SKI_NAMES[SkiViewGame.SetupSki];
            string bind = SkiViewGame.BIND_NAMES[SkiViewGame.SetupBinding];
            if (jump)
            {
                int ki = SkiViewGame.SetupJumpKmhIdx, ri = SkiViewGame.SetupJumpRampIdx;
                return $"Last setup: {game?.lakeName ?? "Ski View"} · {SkiViewGame.JUMP_KMH[ki]:0} km/h · {SkiViewGame.JUMP_RAMP_M[ri]:0.00} m · 23 m";
            }
            return $"Last setup: {game?.lakeName ?? "Ski View"} · {SkierSim.ROPE_LABEL[SkiViewGame.SetupRope]} · {SpeedMph(SkiViewGame.SetupMph)} · {ski} · {bind}";
        }
    }
}

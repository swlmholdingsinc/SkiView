using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>Pass 19: the rest of Julia's kit - kit HUD (scorebug, buoy pips, call plate, skip pill, pause, steer hints; replaces the
    /// IMGUI bar), Pause, Lake select with pack states (03 / 03b / 03c / 03d), Controls help (14), first-run tutorial coach cards (13-13d),
    /// SVG arrow key caps. Copy from skiview-assets-staging/copy (lake-blurbs.md, tutorial-script.md).</summary>
    public partial class SkiViewUI
    {
        VisualElement pause, lakes, help, bug, pips, call, skipPill, pauseBtn, tutCard, tutSkip, steerL, steerR, lakeList, lakeCard;
        Label bugRope, bugSpeed, bugPass, bugRopeLab, bugSpeedLab, callText, tutCrumb, tutTitle, tutText, tutProg; VisualElement tutDots, tutKeys;
        Button pauseJump; Label coach; int lakeSel = 0; string forcedPack;
        List<VisualElement> keysJump;   // T5: Space cut/pop, jump runs only
        readonly List<VisualElement> pipEls = new List<VisualElement>(); readonly List<VisualElement> lakeRows = new List<VisualElement>();

        // T25: speed units (mph default, km/h option) and the desktop key-hint strip toggle; both persist
        public const string UNITS_PREF = "sv.units.kmh", HINTS_PREF = "sv.hints";
        public static bool Kmh => PlayerPrefs.GetInt(UNITS_PREF, 0) == 1;
        public static bool Hints => PlayerPrefs.GetInt(HINTS_PREF, 1) == 1;
        public static string SpeedKmh(float kmh) => Kmh ? $"{Mathf.RoundToInt(kmh)} km/h" : $"{Mathf.RoundToInt(kmh / 1.609344f)} mph";
        public static string SpeedMph(float mph) => Kmh ? $"{Mathf.RoundToInt(mph * 1.609344f)} km/h" : $"{Mathf.RoundToInt(mph)} mph";
        static string RopeOff(int i) => SkierSim.ROPE_LABEL[Mathf.Clamp(i, 0, SkierSim.ROPE_LABEL.Length - 1)].Replace(" OFF", " off");
        VisualElement keyStrip; Label pipCount, rsDetails;
        public const string JUMPBAR_PREF = "sv.jumpbar";
        public static bool JumpBar => PlayerPrefs.GetInt(JUMPBAR_PREF, 1) == 1;   // T26: on by default
        VisualElement jbar, jbarZone, jbarMark; Label jbarLab;

        static VisualElement Key(VisualElement parent, string text, string icon = null)
        {
            var k = new VisualElement(); k.AddToClassList("sv-key"); k.AddToClassList("sv-key19"); k.pickingMode = PickingMode.Ignore; parent.Add(k);
            if (icon != null) { var i = new VisualElement(); i.AddToClassList("sv-key19__icon"); i.pickingMode = PickingMode.Ignore; var vi = Resources.Load<VectorImage>("SkiViewUI/Icons/" + icon); if (vi != null) i.style.backgroundImage = new StyleBackground(vi); k.Add(i); }
            if (text != null) { var l = new Label(text); l.pickingMode = PickingMode.Ignore; l.AddToClassList("sv-key19__txt"); k.Add(l); }
            return k;
        }

        // ---------------- lakes (lake-blurbs.md) ----------------
        public struct LakeCopy { public string id, name, region, blurb; public bool jump; public string deck, band, accent; }
        public static readonly LakeCopy[] LAKES =
        {
            new LakeCopy { id = "skiview", name = "SKI VIEW", region = "PACIFIC NORTHWEST · HOME LAKE", blurb = "Fir-lined home water with two turn islands and a row of lakeside homes. Late-summer afternoon light.", jump = true, deck = "#F4F5F2", band = "#1C3F9E", accent = "#B9C0C9" },
            new LakeCopy { id = "cedarbend", name = "CEDAR BEND", region = "CASCADE FOOTHILLS", blurb = "A long, gently bent lane between a mown lawn and a wall of fir and cedar. Cold, flat and still.", jump = false, deck = "#F3F2EE", band = "#B3202A", accent = "#1C1D21" },
            new LakeCopy { id = "saltwhite", name = "SALTWHITE", region = "GULF COAST", blurb = "White-sand banks round teal water, estate homes and a judges' tower. Low morning sun, glassy lanes.", jump = true, deck = "#FAFAF7", band = "#9EA5AD", accent = "#101114" },
            new LakeCopy { id = "villaserena", name = "VILLA SERENA", region = "CENTRAL FLORIDA", blurb = "Hard, bright noon on a long blue lane. Cream villas and tile roofs on one bank, a dark pine wall on the other.", jump = false, deck = "#F1E8D6", band = "#6B1E38", accent = "#C9A866" },
            new LakeCopy { id = "tallpine", name = "TALL PINE", region = "VALLEY EDGE, CALIFORNIA", blurb = "Golden hour on a dead-calm mirror lane. A towering conifer windbreak and straw-gold fields all round.", jump = true, deck = "#F2F3EF", band = "#1E5A3B", accent = "#C8A24A" },
            new LakeCopy { id = "heronmarsh", name = "HERON MARSH", region = "LOWCOUNTRY", blurb = "A dark, glassy channel at sunset. Moss-hung live oaks crowd both banks and reflect almost black.", jump = true, deck = "#ECEEED", band = "#3B4047", accent = "#8CC63F" },
            new LakeCopy { id = "harrowpond", name = "HARROW POND", region = "HUDSON VALLEY", blurb = "A calm valley morning. A wide lake split by a tree-lined berm, club lawns and a red-and-yellow jump ramp.", jump = true, deck = "#F4F4F1", band = "#E8681C", accent = "#2A2C30" },
            new LakeCopy { id = "gulfwind", name = "GULFWIND", region = "GULF COAST", blurb = "Hot, hazy midday on a dead-straight teal canal. A covered boathouse at every lot, one tree on every island.", jump = true, deck = "#F6F7F4", band = "#0097A7", accent = "#FF6F5E" },
            new LakeCopy { id = "saguaro", name = "SAGUARO", region = "SONORAN DESERT", blurb = "A dry, clear desert morning on jade-green twin lanes. A fan palm on each island, mountains on the horizon.", jump = false, deck = "#EDE2CC", band = "#5A2D82", accent = "#F28C28" },
            new LakeCopy { id = "marrowind", name = "MARROWIND", region = "OLD FLORIDA", blurb = "Big open water at dawn. Dark glass, a far ring of low homes and all the room you could want.", jump = true, deck = "#F5F5F2", band = "#F2C200", accent = "#121316" },
        };
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static VisualElement Livery(LakeCopy l, VisualElement parent)
        {
            var v = El("sv-livery", parent); v.pickingMode = PickingMode.Ignore;
            foreach (var (cls, col) in new[] { ("deck", l.deck), ("accent", l.accent), ("band", l.band) }) { var e = El(cls, v); e.style.backgroundColor = Hex(col); e.pickingMode = PickingMode.Ignore; }
            return v;
        }

        enum PackState { Included, Installed, NotDownloaded, Downloading, Failed, Offline, Unknown }
        PackState State(string id, out float pct, out long size, out long got)
        {
            pct = 0f; size = 0; got = 0;
            if (forcedPack != null)
            {   // ?ui=lakes_downloading | lakes_failed | lakes_offline : the mockup states on Saltwhite (03b-03d), Cedar Bend installed
                size = id == "saltwhite" ? 6800000 : 8000000;
                if (id == "skiview") return PackState.Included; if (id == "cedarbend" && forcedPack != "lakes") return PackState.Installed;
                if (forcedPack == "lakes_offline") return PackState.Offline;
                if (id == "saltwhite" && forcedPack == "lakes_downloading") { pct = 0.62f; got = 4200000; return PackState.Downloading; }
                if (id == "saltwhite" && forcedPack == "lakes_failed") return PackState.Failed;
                if (forcedPack == "lakes" && id == "cedarbend") size = 7800000;
                return PackState.NotDownloaded;
            }
            if (id == "skiview") return PackState.Included;
            if (!SkiViewBoot.Active) return id == game?.lakeId ? PackState.Installed : PackState.Unknown;
            var p = SkiViewBoot.PackInfo(id); size = p.size; pct = p.progress; got = p.got;
            if (p.downloading) return PackState.Downloading; if (p.failed) return PackState.Failed;
            if (p.size < 0) return PackState.Unknown; if (p.size == 0) return PackState.Installed;
            return Application.internetReachability == NetworkReachability.NotReachable ? PackState.Offline : PackState.NotDownloaded;
        }
        static string Mb(long b) => (b / 1000000f).ToString("0.0") + " MB";

        void BuildLakes()
        {
            lakes = Menu(out var safe, "STEP 2 OF 3 · " + "SLALOM", "SELECT LAKE", NavBack);
            Btn("TITLE", "sv-btn sv-btn--ghost", lakes.Q(className: "sv-header"), ToTitle, "back");   // b9: always a way to the title
            var body = El("sv-body", safe);
            var listPlate = El("sv-lakes19__list", body); var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; listPlate.Add(sv); lakeList = sv.contentContainer;
            for (int i = 0; i < LAKES.Length; i++)
            {
                int k = i; var l = LAKES[i];
                var r = new Button(() => { lakeSel = k; RefreshLakes(false); }); r.AddToClassList("sv-row"); r.AddToClassList("sv-lakes19__row"); lakeList.Add(r); lakeRows.Add(r);
                Lb((i + 1).ToString("00"), "t-micro sv-lakes19__num", r).pickingMode = PickingMode.Ignore; Livery(l, r);
                var th = El("sv-lakes19__thumb", r); th.pickingMode = PickingMode.Ignore; var tt = Resources.Load<Texture2D>("SkiViewUI/Lakes/" + l.id); if (tt != null) th.style.backgroundImage = new StyleBackground(tt);   // batch 5 (03 v5): real in-game thumbnail per row
                Lb(l.name, "t-label sv-lakes19__name", r).pickingMode = PickingMode.Ignore;
                var chip = El("sv-lakes19__chip", r); chip.pickingMode = PickingMode.Ignore; chip.name = "chip";
            }
            lakeCard = El("sv-plate sv-plate--card sv-lakes19__card", body);
        }
        void RefreshLakes(bool resetSel)
        {
            if (lakes == null) return;
            if (resetSel) { int c = System.Array.FindIndex(LAKES, l => l.id == (forcedPack != null ? (forcedPack == "lakes" ? "cedarbend" : "saltwhite") : game?.lakeId)); lakeSel = c < 0 ? 0 : c; }
            for (int i = 0; i < lakeRows.Count; i++)
            {
                var r = lakeRows[i]; r.EnableInClassList("sv-row--selected", i == lakeSel);
                var st = State(LAKES[i].id, out float pct, out long size, out _);
                r.EnableInClassList("sv-lakes19__row--dim", st == PackState.Offline);
                var chip = r.Q("chip"); chip.Clear();
                if (st == PackState.Included) Lb("INCLUDED", "sv-lakes19__tag", chip);
                else if (st == PackState.Installed) Key(chip, null, "check").AddToClassList("sv-lakes19__ok");
                else if (st == PackState.Downloading) Lb(Mathf.RoundToInt(pct * 100f) + "%", "t-micro sv-lakes19__pct", chip);
                else if (st == PackState.Failed) Lb("!", "sv-lakes19__fail", chip);
                else if (st == PackState.Offline) Key(chip, null, "offline").AddToClassList("sv-lakes19__ok");
                else if (st == PackState.NotDownloaded) { Key(chip, null, "download").AddToClassList("sv-lakes19__ok"); Lb(Mb(size), "t-micro", chip); }
                else Lb("…", "t-micro", chip);
            }
            var l = LAKES[lakeSel]; lakeCard.Clear(); var s = State(l.id, out float p0, out long sz, out long got);
            var img = El("sv-lakes19__img", lakeCard); var tex = Resources.Load<Texture2D>("SkiViewUI/Lakes/" + l.id); if (tex != null) img.style.backgroundImage = new StyleBackground(tex);
            var chips = El("sv-lakes19__chips", img); Lb("SLALOM", "sv-lakes19__ev", chips); Lb(!l.jump ? "NO JUMP RAMP" : l.id == "gulfwind" ? "JUMP MODE · NORTH BULGE" : "JUMP", "sv-lakes19__ev", chips);
            var lv = Livery(l, img); lv.AddToClassList("sv-lakes19__flag");
            var txt = El("sv-lakes19__txt", lakeCard);
            Lb($"LAKE {lakeSel + 1:00} OF {LAKES.Length} · {l.region}", "t-micro", txt); Lb(l.name, "t-h1", txt); Lb(l.blurb, "t-body sv-lakes19__blurb", txt);
            var stat = El("sv-lakes19__status", txt); var act = El("sv-lakes19__act", txt);
            string id = l.id;
            switch (s)
            {
                case PackState.Included: case PackState.Installed: case PackState.Unknown:
                    Lb(s == PackState.Included ? "INCLUDED WITH THE GAME" : s == PackState.Installed ? "INSTALLED · PLAYS OFFLINE" : "CHECKING PACK…", "t-label", stat);
                    var sel = Btn(game != null && id == game.lakeId ? "SKIING HERE · RESUME" : "SELECT", "sv-btn sv-btn--primary sv-lakes19__go", act, () => Select(id), "play"); break;
                case PackState.NotDownloaded:
                    Lb("NOT DOWNLOADED · " + Mb(sz), "t-label", stat); Lb("Downloads once, then plays offline. You can keep skiing at Ski View while it finishes.", "t-caption", stat);
                    Btn("DOWNLOAD · " + Mb(sz), "sv-btn sv-btn--primary sv-lakes19__go", act, () => { SkiViewBoot.Download(id); RefreshLakes(false); }, "download"); break;
                case PackState.Downloading:
                    Lb($"DOWNLOADING… {Mb(got)} / {Mb(sz)} · {Mathf.RoundToInt(p0 * 100f)}%", "t-label", stat);
                    var bar = El("sv-lakes19__bar", stat); var fill = El("sv-lakes19__fill", bar); fill.style.width = Length.Percent(p0 * 100f);
                    Lb("You can keep skiing at Ski View while this finishes.", "t-caption", stat);
                    Btn("CANCEL", "sv-btn sv-btn--ghost sv-lakes19__go", act, () => { SkiViewBoot.CancelDownload(id); RefreshLakes(false); }); break;
                case PackState.Failed:
                    stat.AddToClassList("sv-lakes19__status--fail"); Lb("DOWNLOAD DIDN'T FINISH", "t-label", stat); Lb("Check your connection and try again.", "t-caption", stat);
                    Btn("RETRY · " + Mb(sz), "sv-btn sv-btn--primary sv-lakes19__go", act, () => { SkiViewBoot.Download(id); RefreshLakes(false); }, "retry"); break;
                case PackState.Offline:
                    Lb("YOU'RE OFFLINE", "t-label", stat); Lb($"Connect to the internet to download {Title(l.name)}. Lakes you've already downloaded still work.", "t-caption", stat);
                    var b = Btn("UNAVAILABLE OFFLINE", "sv-btn sv-lakes19__go", act, null, "offline"); b.SetEnabled(false); break;
            }
        }
        static string Title(string n) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.ToLowerInvariant());
        public void OpenLakes() { lakesFromSetup = TitleFlow; Open(lakes); }
        void Select(string id)
        {
            CloseMenus();
            if (game != null && id == game.lakeId) return;
            skipTitleOnce = true;   // b9: a lake picked from the menus starts the run, not the title again
            if (SkiViewBoot.Active) SkiViewBoot.Load(SkiViewBoot.SceneKey(id)); else game?.NextLake();
        }

        // ---------------- pause (05 v4) ----------------
        Label pauseCrumb, camStep, tpRope, tpSpeed, tpBuoys, tpPb; VisualElement tpPips;
        void BuildPause()
        {
            pause = El("sv-screen sv-menu", root); El("sv-scrim-full", pause); menus.Add(pause);
            var safe = El("sv-safe", pause); var body = El("sv-pause19__body", safe);
            var col = El("sv-pause19", body);
            pauseCrumb = Lb("", "t-micro", col); Lb("PAUSED", "t-h1", col);
            Btn("RESUME", "sv-btn sv-btn--primary", col, CloseMenus, "play");
            Btn("RESTART PASS", "sv-btn", col, () => { CloseMenus(); game?.RetryPass(); }, "retry");
            var cam = El("sv-btn sv-pause19__cam", col); var ci = El("sv-icon", cam); ci.style.backgroundImage = new StyleBackground(Resources.Load<VectorImage>("SkiViewUI/Icons/camera"));
            Lb("CAMERA", "sv-pause19__lab", cam); var stp = El("sv-pause19__step", cam);
            Btn(null, "sv-btn sv-btn--icon sv-pause19__arrow", stp, () => StepCam(-1), "arrow-left"); camStep = Lb("", "t-label sv-pause19__view", stp); Btn(null, "sv-btn sv-btn--icon sv-pause19__arrow", stp, () => StepCam(1), "arrow-right");
            Btn("SETTINGS", "sv-btn", col, () => { pauseReturn = true; Open(settings); }, "settings");
            Btn("CONTROLS HELP", "sv-btn", col, () => { pauseReturn = true; Open(help); }, "controls");
            pauseJump = Btn("JUMP MODE: OFF", "sv-btn", col, () => { game?.ToggleJumpMode(); CloseMenus(); }, "jump-distance");
            Btn("CHANGE LAKE", "sv-btn", col, () => { lakesFromSetup = false; Open(lakes); }, "back");
            Btn("WORLD RECORDS", "sv-btn", col, () =>
            {
                pauseReturn = true; titleReturn = false;
                var d = SkiViewRecords.Discipline.Slalom;
                if (game != null && game.trickMode) d = SkiViewRecords.Discipline.Trick;
                else if (game != null && game.jumpMode) d = SkiViewRecords.Discipline.Jump;
                OpenRecords(d, "PAUSED");
            }, "trophy");
            Btn("MAIN MENU", "sv-btn sv-btn--ghost", col, ToTitle, "back");   // b9: the title screen, not lake select
            var tp = El("sv-plate sv-plate--card sv-pause19__this", body); El("sv-strip", tp); Lb("THIS PASS", "t-label", tp);
            Label KV(string k, string icon) { var r = El("sv-pause19__kv", tp); var ic = El("sv-key19__icon", r); ic.style.backgroundImage = new StyleBackground(Resources.Load<VectorImage>("SkiViewUI/Icons/" + icon)); Lb(k, "t-micro sv-pause19__k", r); return Lb("", "t-label", r); }
            tpRope = KV("ROPE", "rope"); tpSpeed = KV("SPEED", "speed"); tpBuoys = KV("BUOYS SO FAR", "buoy"); tpPb = KV("PERSONAL BEST", "trophy"); tpPb.AddToClassList("t-gold");
            tpPips = El("sv-pause19__pips", tp); for (int i = 0; i < 7; i++) { if (i == 0) GatePill(tpPips, true); else El("sv-pip", tpPips); };
        }
        public void OpenPause() { Open(pause); }
        void StepCam(int d) { if (game == null) return; int i = (CycleIndex(game.camMode) + d + 4) % 4; game.SetCamera(FromCycle(i)); ShowCamToast(game.camMode); if (camStep != null) camStep.text = ViewNames[i]; }
        void RefreshPause()
        {
            if (pauseJump == null || game == null) return;
            bool can = game.jumpRamp != null && !game.jumpOnly;
            pauseJump.style.display = can ? DisplayStyle.Flex : DisplayStyle.None;
            var l = pauseJump.Q<Label>(); if (l != null) l.text = game.jumpMode ? "JUMP MODE: ON" : "JUMP MODE: OFF";
            var sim = game.sim; int ri = sim != null ? sim.ropeIndex : 0;
            pauseCrumb.text = game.trickMode
                ? $"TRICKS · {game.lakeName.ToUpperInvariant()} · PASS {Mathf.Max(1, game.trickPass)}/2 · {game.trickBoatKmh:0} km/h · {game.trickRopeM:0.0} m"
                : $"{(game.jumpMode ? "JUMP" : "SLALOM")} · {game.lakeName.ToUpperInvariant()} · PASS {game.passes + 1} · {SkierSim.ROPE_LABEL[ri]}";
            camStep.text = ViewNames[CycleIndex(game.camMode)];
            tpRope.text = $"{SkierSim.ROPE_LABEL[ri]} · {SkierSim.ROPE_M[ri]:0.00} M"; tpSpeed.text = SpeedMph(game != null ? game.RunMph : 34f).ToUpperInvariant();
            float b = sim != null && game.mode == SkiViewGame.Mode.Pass ? sim.passBuoys : 0f; tpBuoys.text = b.ToString("0.#");
            tpPb.text = PbText(game.lakeId);
            int k = 0; foreach (var e in tpPips.Children()) { if (k > 0) { e.EnableInClassList("sv-pip--done", b >= k); e.EnableInClassList("sv-pip--half", b > k - 1 && b < k); } k++; }
        }
        // personal best per lake: score = rope index * 6 + buoys (PlayerPrefs sv.pb.<lake>)
        static float Pb(string lake) => PlayerPrefs.GetFloat("sv.pb." + lake, -1f);
        static string PbFmt(float sc) => sc < 0f ? "—" : $"{(sc % 6f < 0.01f && sc > 0f ? 6f : sc % 6f):0.#} @ {SkierSim.ROPE_LABEL[Mathf.Clamp(Mathf.FloorToInt((sc - 0.01f) / 6f), 0, 8)]}";
        static string PbText(string lake) => PbFmt(Pb(lake));

        Label trickTbdChip;
        void EnsureTrickTbdChip(VisualElement host)
        {
            if (trickTbdChip == null)
            {
                trickTbdChip = Lb("TBD · Holly post–1 Nov", "sv-tbd-chip", host);
                trickTbdChip.pickingMode = PickingMode.Ignore;
            }
            else if (trickTbdChip.parent != host)
            {
                trickTbdChip.RemoveFromHierarchy();
                // after score row (PASS COMPLETE + row + …): keep near score
                host.Insert(Mathf.Min(3, host.childCount), trickTbdChip);
            }
            trickTbdChip.style.display = DisplayStyle.Flex;
            trickTbdChip.text = "TBD · Holly post–1 Nov";
        }

        // ---------------- results (07 / 07b v4) ----------------
        VisualElement results; Label rsBanner, rsScore, rsSuffix, rsRope, rsPb, rsNextCap; Button rsMain, rsAlt, rsTitle; SkiViewGame.Mode rsModePrev; int rsRopeIdx; float rsBuoysLast;
        void BuildResults(VisualElement safe)
        {
            results = El("sv-plate sv-plate--card sv-results19", safe); El("sv-strip", results);
            rsBanner = Lb("NEW PERSONAL BEST", "sv-results19__pb", results);
            Lb("PASS COMPLETE", "t-micro", results);
            var row = El("sv-results19__row", results); rsScore = Lb("", "t-num-xl sv-results19__num", row); rsSuffix = Lb("", "t-h2", row);
            rsRope = Lb("", "t-label", results); rsPb = Lb("", "t-caption", results);
            rsRecordBox = El("sv-results19__records", results);
            rsRecordHead = Lb("", "t-micro", rsRecordBox);
            var rsScroll = new ScrollView(ScrollViewMode.Vertical); rsScroll.AddToClassList("sv-results19__recscroll"); rsRecordBox.Add(rsScroll); rsRecordLines = rsScroll.contentContainer;
            rsRecordBox.style.display = DisplayStyle.None;
            var det = El("sv-results19__details", results); Lb("DETAILS", "t-micro sv-results19__dk", det); rsDetails = Lb("", "t-caption sv-results19__dv", det);
            var act = El("sv-results19__act", results);
            // b10.27 T24 Gap A: NEXT continues drop (SAME if rope choice still open); TITLE matches Jump/Tricks
            rsMain = Btn("NEXT PASS", "sv-btn sv-btn--primary", act, () => { if (game != null && game.DropPhase >= 2 && game.RopeChoicePending) game.ChooseRope(false); results.style.display = DisplayStyle.None; }, "play");   // b9.1: the boat tows him round the island to the next pass (skip pill still jumps ahead)
            rsNextCap = Lb("", "t-caption", results);
            rsAlt = Btn("RETRY PASS", "sv-btn", act, () => { results.style.display = DisplayStyle.None; game?.RetryPass(); }, "retry");
            Btn("RECORDS", "sv-btn sv-btn--ghost", act, OpenRecordsFromResults, "trophy");
            rsTitle = Btn("TITLE", "sv-btn sv-btn--ghost", act, () => { results.style.display = DisplayStyle.None; ToTitle(); }, "back");
            results.style.display = DisplayStyle.None;
            // b9.1a: rope choice while he floats and the boat circles back (touch here; keys: left/right + Enter, S = shorten)
            choice = El("sv-plate sv-plate--card sv-results19", safe); El("sv-strip", choice);
            Lb("NEXT PASS", "t-micro", choice); chTitle = Lb("SAME LENGTH OR SHORTEN?", "t-label", choice);
            var cr = El("sv-results19__act", choice);
            chSame = Btn("SAME", "sv-btn", cr, () => game?.ChooseRope(false), "retry");
            chShort = Btn("SHORTEN", "sv-btn sv-btn--primary", cr, () => game?.ChooseRope(true), "play");
            // b9.1c (Scott): one label per button (the Btn label child, never Button.text on top of it), icon beside the text
            foreach (var b in new[] { chSame, chShort })
            {
                b.style.flexDirection = FlexDirection.Row; b.style.justifyContent = Justify.Center; b.style.alignItems = Align.Center;
                var ic = b.Q(className: "sv-icon"); if (ic != null) { ic.style.position = Position.Relative; ic.style.left = StyleKeyword.Auto; ic.style.right = StyleKeyword.Auto; ic.style.flexShrink = 0; ic.style.marginRight = 10; }
            }
            chSameL = chSame.Q<Label>(); chShortL = chShort.Q<Label>();
            // b10.27 T24 Gap B: Esc cancels choice → results plate (never soft-lock)
            var hint = Lb(Application.isMobilePlatform ? "Tap to choose · Back cancels" : "Arrows · Enter · S shortens · Esc cancels to results", "t-caption", choice);
            hint.style.whiteSpace = WhiteSpace.Normal; hint.style.flexShrink = 1; hint.style.marginLeft = 0; hint.style.paddingLeft = 0; hint.style.unityTextAlign = TextAnchor.MiddleLeft; hint.style.maxWidth = Length.Percent(100);
            choice.style.display = DisplayStyle.None;
        }
        VisualElement choice; Label chTitle, chSameL, chShortL; Button chSame, chShort;
        void TickChoice()
        {
            if (choice == null || game == null) return;
            bool open = game.RopeChoiceOpen;
            choice.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                // Gap B: after Esc cancel, keep results plate visible while still in the drop
                if (game.RopeChoiceEscaped && game.DropPhase >= 2 && game.mode == SkiViewGame.Mode.Sink && results != null
                    && !game.jumpMode && !game.JumpResultsOpen && !game.trickMode && !SkiViewClinic.Active)
                    results.style.display = DisplayStyle.Flex;
                return;
            }
            results.style.display = DisplayStyle.None;
            chSameL.text = "SAME · " + game.SameLabel; chShortL.text = game.CanShorten ? "SHORTEN · " + game.ShortLabel : "AT SHORTEST";
            chShort.SetEnabled(game.CanShorten);
            chShort.EnableInClassList("sv-btn--primary", game.ShortenHighlighted); chSame.EnableInClassList("sv-btn--primary", !game.ShortenHighlighted);
        }
        void TickResults()
        {
            if (results == null || game == null || game.sim == null) return;
            TickChoice();
            TickClinicResults();
            if (SkiViewClinic.Active) { /* Clinic owns results plate */ if (SkiViewClinic.ResultsOpen) { results.style.display = DisplayStyle.None; rsModePrev = game.mode; return; } }
            var m = game.mode; var sim = game.sim;
            // b10.14 Jump Phase A results (T15 / T31 31c)
            if (game.jumpMode && game.JumpResultsOpen)
            {
                if (!results.ClassListContains("sv-results19--jump"))
                {
                    results.AddToClassList("sv-results19--jump");
                    results.EnableInClassList("sv-results19--pb", false); rsBanner.style.display = DisplayStyle.None;
                    if (Kmh) { rsScore.text = game.JumpBest.ToString("0.0"); rsSuffix.text = " m"; }
                    else { rsScore.text = Mathf.FloorToInt(game.JumpBest / 0.3048f + 0.5f).ToString(); rsSuffix.text = " ft"; }
                    // T32: setup line + live best; primary "JUMP AGAIN"
                    float mph = game.jumpBoatKmh / 1.609344f;
                    rsRope.text = $"{game.jumpBoatKmh:0} km/h · {mph:0.0} mph · {game.rampHeight:0.00} m ramp · 23 m rope";
                    rsDetails.text = game.JumpResultsSummary();
                    rsPb.text = game.JumpBest > 0f ? "LONGEST OF 3" : "3 PASSES COMPLETE";
                    rsMain.style.display = DisplayStyle.Flex; rsNextCap.style.display = DisplayStyle.None;
                    rsMain.text = "JUMP AGAIN"; rsAlt.text = "TITLE"; rsAlt.EnableInClassList("sv-btn--primary", false);
                    rsMain.EnableInClassList("sv-btn--primary", true);
                    if (rsTitle != null) rsTitle.style.display = DisplayStyle.None;
                    rsMain.clickable = new Clickable(() => { results.style.display = DisplayStyle.None; results.RemoveFromClassList("sv-results19--jump"); recordListSeq = int.MinValue; recordListShown = false; game.BeginJumpRound(); });
                    rsAlt.clickable = new Clickable(() => { results.style.display = DisplayStyle.None; results.RemoveFromClassList("sv-results19--jump"); recordListSeq = int.MinValue; recordListShown = false; game.CloseJumpResults(); if (game.jumpMode) game.ToggleJumpMode(); OpenTitle(); });
                    results.style.display = DisplayStyle.Flex;
                }
                ShowRecordList(SkiViewRecords.Discipline.Jump);
                rsModePrev = m;
                return;
            }
            if (results.ClassListContains("sv-results19--jump"))
            {
                results.RemoveFromClassList("sv-results19--jump");
                results.style.display = DisplayStyle.None;
                rsMain.text = "NEXT PASS"; rsAlt.text = "RETRY PASS";
                if (rsTitle != null) rsTitle.style.display = DisplayStyle.Flex;
                rsMain.clickable = new Clickable(() => { if (game != null && game.DropPhase >= 2 && game.RopeChoicePending) game.ChooseRope(false); results.style.display = DisplayStyle.None; });
                rsAlt.clickable = new Clickable(() => { results.style.display = DisplayStyle.None; game?.RetryPass(); });
            }

            // b10.11 Tricks Phase A results (T23)
            if (game.trickMode && game.TrickResultsOpen)
            {
                if (!results.ClassListContains("sv-results19--trick"))
                {
                    results.AddToClassList("sv-results19--trick");
                    results.EnableInClassList("sv-results19--pb", false); rsBanner.style.display = DisplayStyle.None;
                    rsScore.text = game.TrickScore.ToString(); rsSuffix.text = " PTS";
                    // b10.26 T17b/T29b: real Chart ints on details; hide legacy TBD chip
                    if (trickTbdChip != null) trickTbdChip.style.display = DisplayStyle.None;
                    rsRope.text = $"{SpeedKmh(game.trickBoatKmh)} · {game.trickRopeM:0.0} m · 2 × 20 s";
                    rsDetails.text = game.TrickResultsSummary();
                    rsPb.text = game.TrickRoundOver ? "ROUND OVER · FALL IN PASS 1" : "BOTH PASSES COMPLETE";
                    rsMain.style.display = DisplayStyle.Flex; rsNextCap.style.display = DisplayStyle.None;
                    rsMain.text = "TRICK AGAIN"; rsAlt.text = "TITLE"; rsAlt.EnableInClassList("sv-btn--primary", false);
                    rsMain.EnableInClassList("sv-btn--primary", true);
                    if (rsTitle != null) rsTitle.style.display = DisplayStyle.None;
                    rsMain.clickable = new Clickable(() => { results.style.display = DisplayStyle.None; results.RemoveFromClassList("sv-results19--trick"); recordListSeq = int.MinValue; recordListShown = false; game.BeginTrickRound(); });
                    rsAlt.clickable = new Clickable(() => { results.style.display = DisplayStyle.None; results.RemoveFromClassList("sv-results19--trick"); recordListSeq = int.MinValue; recordListShown = false; game.CloseTrickResults(); OpenTitle(); });
                    results.style.display = DisplayStyle.Flex;
                }
                ShowRecordList(SkiViewRecords.Discipline.Trick);
                rsModePrev = m;
                return;
            }
            if (results.ClassListContains("sv-results19--trick"))
            {
                results.RemoveFromClassList("sv-results19--trick");
                results.style.display = DisplayStyle.None;
                if (trickTbdChip != null) trickTbdChip.style.display = DisplayStyle.None;
                // restore slalom button labels
                rsMain.text = "NEXT PASS"; rsAlt.text = "RETRY PASS";
                if (rsTitle != null) rsTitle.style.display = DisplayStyle.Flex;
                rsMain.clickable = new Clickable(() => { if (game != null && game.DropPhase >= 2 && game.RopeChoicePending) game.ChooseRope(false); results.style.display = DisplayStyle.None; });
                rsAlt.clickable = new Clickable(() => { results.style.display = DisplayStyle.None; game?.RetryPass(); });
            }
            if (game.trickMode) { rsModePrev = m; return; }

            if ((m == SkiViewGame.Mode.Sink || m == SkiViewGame.Mode.Fallen || m == SkiViewGame.Mode.Turn) && rsModePrev == SkiViewGame.Mode.Pass && !game.jumpMode && !game.JumpResultsOpen && !SkiViewClinic.Active && tutStep < 0)
            {
                var res = sim.last; rsRopeIdx = res.rope; float b = res.buoys; bool full = res.clean; float sc = rsRopeIdx * 6f + b;
                float pb0 = Pb(game.lakeId); bool newPb = sc > pb0 + 0.01f; if (newPb) { PlayerPrefs.SetFloat("sv.pb." + game.lakeId, sc); PlayerPrefs.Save(); }
                rsScore.text = SkierSim.Fmt(b); rsSuffix.text = " @ " + SkierSim.ROPE_LABEL[rsRopeIdx];
                rsRope.text = $"{RopeOff(rsRopeIdx)} | {SpeedMph(game != null ? game.RunMph : 34f)} | Pass {Mathf.Max(1, game.passes)}";
                rsDetails.text = $"{sim.Breakdown(res)} · rope {SkierSim.ROPE_M[rsRopeIdx]:0.00} m · {SpeedMph(game != null ? game.RunMph : 34f)}";
                rsPb.text = newPb ? $"PERSONAL BEST {PbFmt(sc)}" + (pb0 >= 0f ? $" · WAS {PbFmt(pb0)}" : "") : $"PERSONAL BEST {PbFmt(Mathf.Max(pb0, sc))}";
                bool wr = game.LastRecord.attempted && game.LastRecord.discipline == SkiViewRecords.Discipline.Slalom && game.LastRecord.qualified && game.LastRecord.rank == 1;
                rsBanner.text = wr ? "NEW WORLD RECORD" : "NEW PERSONAL BEST";
                results.EnableInClassList("sv-results19--pb", newPb || wr); rsBanner.style.display = (newPb || wr) ? DisplayStyle.Flex : DisplayStyle.None;
                ShowRecordList(SkiViewRecords.Discipline.Slalom);
                int next = Mathf.Min(rsRopeIdx + 1, SkierSim.ROPE_M.Length - 1);
                // NEXT PASS only after a full pass (the rope shortens only then); after a partial pass RETRY is the main action
                rsMain.style.display = full ? DisplayStyle.Flex : DisplayStyle.None; rsNextCap.style.display = full ? DisplayStyle.Flex : DisplayStyle.None;
                rsNextCap.text = $"Next: same length or shorten to {SkierSim.ROPE_LABEL[next].ToLowerInvariant()} ({SkierSim.ROPE_M[next]:0.0} m)";
                rsAlt.EnableInClassList("sv-btn--primary", !full);
                results.style.display = DisplayStyle.Flex; results.schedule.Execute(() => results.style.display = DisplayStyle.None).StartingIn(9000);
            }
            if (m == SkiViewGame.Mode.Pass) { results.style.display = DisplayStyle.None; recordListSeq = int.MinValue; recordListShown = false; }
            rsModePrev = m;
        }

        // ---------------- controls help (14) ----------------
        void BuildHelp()
        {
            help = Menu(out var safe, "PAUSE", "CONTROLS", NavBack);
            var hd = safe.Q(className: "sv-header"); var right = El("sv-header__right", hd);
            Btn("REPLAY TUTORIAL", "sv-btn sv-btn--sm", right, () => { CloseMenus(); StartTutorial(0); }, "retry");
            safe.style.left = 0; safe.style.top = 0; safe.style.width = Length.Percent(100); safe.style.height = Length.Percent(100);   // the card grid has no fixed-width column to size the menu (SafeAreaRoot only insets the first .sv-safe)
            var body = El("sv-body", safe);
            // Phase 1 review #5 (Julia 20e, T20 spec): card grid; binding chips wrap instead of truncating. 1 column at 16:9/4:3, 2 at >= 20:9 and on desktop
            var plate = El("sv-plate sv-plate--card sv-panel sv-help19__table", body);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; plate.Add(sv); var c = sv.contentContainer;
            var grid = El("sv-ctl-grid", c); bool touchDev = Application.isMobilePlatform;   // headless desktop Chrome reports touchSupported, so the platform decides; .input-touch still swaps the highlight on a real touch grid.EnableInClassList("sv-ctl-grid--touch", touchDev);
            void Chip(VisualElement parent, string cls, string icon, System.Action<VisualElement> fill)
            {
                var ch = El("sv-ctl-chip " + cls, parent); ch.pickingMode = PickingMode.Ignore;
                var i = El("sv-ctl-chip__icon", ch); var vi = icon != null ? Resources.Load<VectorImage>("SkiViewUI/Icons/" + icon) : null; if (vi != null) i.style.backgroundImage = new StyleBackground(vi); else i.style.display = DisplayStyle.None;
                fill(ch);
            }
            void R(string a, string touch, System.Action<VisualElement> kb, string pad)
            {
                if (grid.childCount == 0 || grid[grid.childCount - 1].childCount == 2) El("sv-ctl-pair", grid);
                var r = El("sv-ctl-row", grid[grid.childCount - 1]); Lb(a, "t-label sv-ctl-row__a", r);
                var chips = El("sv-ctl-row__chips", r);
                Chip(chips, "sv-ctl-chip--touch", "touch", ch => Lb(touch, "sv-ctl-chip__t", ch));
                Chip(chips, "sv-ctl-chip--kb", "keyboard", kb);
                Chip(chips, "sv-ctl-chip--pad", null, ch => Lb(pad, "sv-ctl-chip__t", ch));
            }
            R("LEAN / CUT LEFT", "HOLD LEFT HALF", k => { Key(k, "A"); Key(k, null, "arrow-left"); }, "L-STICK LEFT");
            R("LEAN / CUT RIGHT", "HOLD RIGHT HALF", k => { Key(k, "D"); Key(k, null, "arrow-right"); }, "L-STICK RIGHT");
            R("JUMP: START THE CUT", "TAP", k => { Key(k, "SPACE"); Key(k, "CLICK"); }, "A");
            R("JUMP: POP ON THE RAMP", "TAP", k => { Key(k, "SPACE"); Key(k, "CLICK"); }, "A");
            R("SKIP THE TURNAROUND", "TAP", k => Key(k, "SPACE"), "A");
            R("CHANGE CAMERA", "CAM BUTTON", k => Key(k, "C"), "RB");
            R("1ST PERSON: LOOK DOWN", "\u2014", k => { Key(k, "S"); Key(k, null, "arrow-down"); }, "R-STICK DOWN");
            R("PAUSE", "PAUSE BUTTON", k => Key(k, "ESC"), "MENU");
            help.RegisterCallback<GeometryChangedEvent>(e => { var w = help.resolvedStyle.width; var h = help.resolvedStyle.height; grid.EnableInClassList("sv-ctl-grid--2", h > 0f && (!touchDev || w / h >= 2.1f)); });
            // T5: buoy legend, "Reading the HUD"
            Lb("READING THE HUD", "t-micro sv-legend19__h", c);
            Legend(c);
        }
        static VisualElement GatePill(VisualElement parent, bool cleared)
        {
            var p = El("sv-gate-pill " + (cleared ? "is-cleared" : "is-pending"), parent); p.pickingMode = PickingMode.Ignore;
            var i = El("sv-gate-pill__icon", p); var vi = Resources.Load<VectorImage>("SkiViewUI/Icons/check"); if (vi != null) i.style.backgroundImage = new StyleBackground(vi);
            Lb("GATE", "sv-gate-pill__t", p); return p;
        }
        static void Legend(VisualElement c)
        {
            void L(string pipCls, string pipLab, string title, string text)
            {
                var r = El("sv-legend19__row", c); var sw = El("sv-legend19__sw", r); var p = El(pipCls, sw); if (pipLab != null) Lb(pipLab, "sv-bug__lab", p);
                var col = El(null, r); col.style.flexShrink = 1; Lb(title, "t-label", col); Lb(text, "t-caption", col);
            }
            { var r = El("sv-legend19__row", c); var sw = El("sv-legend19__sw sv-legend19__sw--pill", r); GatePill(sw, true); var col = El(null, r); col.style.flexShrink = 1; Lb("ENTRY GATES CLEARED", "t-label", col); Lb("The pill turns green once you pass through the entry gates.", "t-caption", col); }
            { var r = El("sv-legend19__row", c); var sw = El("sv-legend19__sw sv-legend19__sw--pill", r); GatePill(sw, false); var col = El(null, r); col.style.flexShrink = 1; Lb("ENTRY GATES NOT YET CLEARED", "t-label", col); Lb("Outlined until the gates count.", "t-caption", col); }
            L("sv-pip sv-pip--done", null, "ORANGE DOT", "Buoy rounded: a full buoy.");
            L("sv-pip sv-pip--half", null, "HALF ORANGE", "Reached the buoy line but not all the way round: half a buoy.");
            L("sv-pip sv-pip--next", null, "WHITE RING", "Next buoy.");
            L("sv-pip", null, "GREY RING", "Still to come in this pass.");
        }

        bool OpenFromArg(string v)
        {
            if (v == "pause") { root.schedule.Execute(() => Open(pause)).StartingIn(300); return true; }
            if (v == "records" || v == "worldrecords") { root.schedule.Execute(() => OpenRecords(SkiViewRecords.Discipline.Slalom, "TITLE")).StartingIn(300); return true; }
            if (v == "help") { root.schedule.Execute(() => Open(help)).StartingIn(300); return true; }
            if (v == "setup" || v == "ski") { root.schedule.Execute(() => OpenSetupCapture()).StartingIn(300); return true; }
            if (v == "clinic" || v == "clinic_list") { root.schedule.Execute(OpenClinicList).StartingIn(300); return true; }
            if (v == "clinic_plan") { root.schedule.Execute(() => OpenClinicPlan("stance")).StartingIn(300); return true; }
            if (v.StartsWith("lakes")) { forcedPack = v; root.schedule.Execute(() => Open(lakes)).StartingIn(300); return true; }
            return false;
        }

        // ---------------- kit HUD ----------------
        void BuildHud19(VisualElement safe)
        {
            bug = El("sv-bug sv-bug19", safe); bug.pickingMode = PickingMode.Ignore;
            var id = El("sv-bug__id", bug); Lb(SkiViewBranding.Badge, "t-h3", id);   // fallback text badge (SV)
            {   // Scott Oct 24 ~11:54: hex SWLM Holdings logo in HUD bug (where Wade logo was); not Wade-gated
                var lp = SkiViewBranding.CompanyLogoPath("icon") ?? SkiViewBranding.CompanyLogoPath("primary") ?? "Branding/slots/icon";
                var tex = !string.IsNullOrEmpty(lp) ? Resources.Load<Texture2D>(lp) : null;
                if (tex != null) { id.Clear(); var im = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore }; im.style.width = 56; im.style.height = 56; id.Add(im); }
            }
            VisualElement Cell(string lab, out Label val, out Label labEl) { var c = El("sv-bug__cell", bug); labEl = Lb(lab, "sv-bug__lab", c); val = Lb("", "t-hud-val", c); return c; }
            Cell("ROPE", out bugRope, out bugRopeLab); Cell("SPEED", out bugSpeed, out bugSpeedLab); Cell("PASS", out bugPass, out var bugPassLab);
            bugRopeLab.style.display = bugSpeedLab.style.display = bugPassLab.style.display = DisplayStyle.None;   // T25: one line "28 off | 34 mph | Pass 2"; the logo cell stays top-left
            foreach (var e in bug.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            pips = El("sv-pips19", safe); pips.pickingMode = PickingMode.Ignore;
            // Phase 1 review #5 (Julia 20f): "✓ GATE" pill replaces the G badge; restores pipEls.Add(gate) (dropped in T5)
            var gate = GatePill(pips, true); pipEls.Add(gate);
            for (int i = 0; i < 6; i++) pipEls.Add(El("sv-pip", pips));
            pipCount = Lb("Buoys 0 / 6", "sv-pips19__n", pips);
            foreach (var e in pipEls) e.pickingMode = PickingMode.Ignore;
            call = El("sv-call sv-call19", safe); call.pickingMode = PickingMode.Ignore; callText = Lb("", "sv-call19__t", call); callText.pickingMode = PickingMode.Ignore;
            coach = Lb("", "t-caption sv-coach19", safe); coach.pickingMode = PickingMode.Ignore;   // one-time coach line (tutorial-script 5b)
            // T5: every HUD button sits in a transparent .sv-hit parent (144 px tall = 48 dp down to a 360 dp short side); the visual ignores picking
            Button Hit(string mod, System.Action a) { var h = new Button(a) { focusable = false }; h.AddToClassList("sv-hit"); h.AddToClassList(mod); safe.Add(h); return h; }
            var skHit = Hit("sv-hit--skip", () => game?.SkipToSetup());
            var sk = new Button(() => game?.SkipToSetup()) { focusable = false }; sk.AddToClassList("sv-skip19"); skHit.Add(sk); sk.pickingMode = PickingMode.Ignore; skipPill = skHit;
            Lb(Application.isMobilePlatform ? "TAP TO SKIP" : "SPACE TO SKIP", "sv-skip19__t", sk).pickingMode = PickingMode.Ignore;
            var pHit = Hit("sv-hit--pause", OpenPause);
            pauseBtn = Btn(null, "sv-btn sv-btn--icon sv-pause-btn19", pHit, OpenPause, "pause"); pauseBtn.focusable = false; pauseBtn.pickingMode = PickingMode.Ignore;
            var tHit = Hit("sv-hit--tutskip", EndTutorial);
            var tsk = Btn("SKIP TUTORIAL", "sv-btn sv-btn--sm sv-tutskip19", tHit, EndTutorial); tsk.focusable = false; tsk.pickingMode = PickingMode.Ignore; tutSkip = tHit;
            if (!Application.isMobilePlatform)
            {   // T5: desktop key strip (hidden under .input-touch); P (debug autopilot) deliberately not shown
                var ks = El("sv-keys19", safe); ks.pickingMode = PickingMode.Ignore; keyStrip = ks;
                VisualElement G() { var g = El("sv-keys19__g", ks); g.pickingMode = PickingMode.Ignore; return g; }
                void Sep() => El("sv-keys19__sep", ks).pickingMode = PickingMode.Ignore;
                void T(VisualElement g, string t) => Lb(t, "sv-keys19__t", g).pickingMode = PickingMode.Ignore;
                var g1 = G(); Key(g1, "A"); Key(g1, null, "arrow-left"); T(g1, "LEAN LEFT"); Key(g1, "D"); Key(g1, null, "arrow-right"); T(g1, "LEAN RIGHT"); Sep();
                var g2 = G(); Key(g2, "C"); T(g2, "CAMERA"); Sep();
                var g3 = G(); Key(g3, "ESC"); T(g3, "PAUSE");
                keysJump = new List<VisualElement> { El("sv-keys19__sep", ks) }; var g4 = G(); Key(g4, "SPACE"); T(g4, "CUT · POP"); keysJump.Add(g4);
                foreach (var e in keysJump) e.pickingMode = PickingMode.Ignore;
            }
            if (Application.isMobilePlatform)
            {
                steerL = Lb("◂ HOLD · LEAN LEFT", "sv-steer-hint sv-steer19--left", safe); steerR = Lb("HOLD · LEAN RIGHT ▸", "sv-steer-hint sv-steer-hint--right", safe);
                steerL.pickingMode = steerR.pickingMode = PickingMode.Ignore;
            }
            // T26: jump timing bar (cut window on the approach, pop window on the ramp)
            jbar = El("sv-jbar", safe); jbar.pickingMode = PickingMode.Ignore; jbarLab = Lb("", "sv-jbar__lab", jbar);
            var tr = El("sv-jbar__track", jbar); jbarZone = El("sv-jbar__zone", tr); jbarMark = El("sv-jbar__mark", tr);
            foreach (var e in jbar.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore; jbar.style.display = DisplayStyle.None;
            BuildTutorial(safe); BuildResults(safe);
            if (SkiViewGame.HudHidden) root.style.display = DisplayStyle.None;
        }

        void Tick19()
        {
            if (game == null || bug == null) return;
            var sim = game.sim; bool jump = game.jumpMode || game.mode == SkiViewGame.Mode.Jump;
            bool trick = game.trickMode || game.mode == SkiViewGame.Mode.Trick;
            bugRopeLab.text = jump ? "RAMP" : "ROPE";
            if (keysJump != null) foreach (var e in keysJump) e.style.display = jump ? DisplayStyle.Flex : DisplayStyle.None;
            if (trick)
            {
                bugRope.text = $"{game.trickRopeM:0.0} m";
                bugSpeed.text = SpeedKmh(game.trickBoatKmh);
                bugPass.text = game.tphase == SkiViewGame.TPhase.Active || game.tphase == SkiViewGame.TPhase.Armed
                    ? $"P{game.trickPass}/2 · {game.TrickClock:0.00} · {game.TrickScore}"
                    : game.tphase == SkiViewGame.TPhase.Results ? $"TOTAL {game.TrickScore}"
                    : $"PASS {Mathf.Max(1, game.trickPass)}/2";
            }
            else
            {
                bugRope.text = jump ? game.RampRatio.ToString("0.000") + " ramp" : (sim != null ? RopeOff(sim.ropeIndex) : "");
                bugSpeed.text = jump ? SpeedKmh(game.jumpBoatKmh) : SpeedMph(game != null ? game.RunMph : 34f);
                bugPass.text = jump
                    ? (game.JumpResultsOpen
                        ? (Kmh ? $"BEST {game.JumpBest:0.0} m" : $"BEST {Mathf.FloorToInt(game.JumpBest / 0.3048f + 0.5f)} ft")
                        : (game.JumpDistance > 0f
                            ? (Kmh ? game.JumpDistance.ToString("0.0") + " m" : (game.JumpDistance * 3.28084f).ToString("0") + " ft")
                            : $"Jump {Mathf.Min(SkiViewGame.JUMP_PASSES, game.JumpPassCount + 1)}/{SkiViewGame.JUMP_PASSES}"))
                    : "Pass " + Mathf.Max(1, game.passes + (game.mode == SkiViewGame.Mode.Pass ? 1 : 0));
            }
            if (keyStrip != null) keyStrip.style.display = Hints ? DisplayStyle.Flex : DisplayStyle.None;
            bool inPass = !trick && game.mode == SkiViewGame.Mode.Pass && sim != null;
            pips.style.display = inPass ? DisplayStyle.Flex : DisplayStyle.None;
            if (inPass)
            {
                pipEls[0].EnableInClassList("is-cleared", sim.gatesCleared); pipEls[0].EnableInClassList("is-pending", !sim.gatesCleared);
                for (int i = 0; i < 6; i++)
                {
                    var e = pipEls[i + 1]; float got = sim.passBuoys - i;
                    e.EnableInClassList("sv-pip--done", got >= 1f); e.EnableInClassList("sv-pip--half", got > 0f && got < 1f); e.EnableInClassList("sv-pip--next", got <= 0f && sim.nextBuoy == i && sim.gatesCleared);
                }
                pipCount.text = $"Buoys {SkierSim.Fmt(sim.passBuoys)} / 6";
            }
            string c = game.bannerT > 0f && !string.IsNullOrEmpty(game.banner) ? game.banner.ToUpperInvariant() : game.HudCall;
            callText.text = c; call.style.display = string.IsNullOrEmpty(c) ? DisplayStyle.None : DisplayStyle.Flex;
            coach.text = game.CoachOn ? game.CoachLine : ""; coach.style.display = game.CoachOn ? DisplayStyle.Flex : DisplayStyle.None;
            skipPill.style.display = game.InTurnaround ? DisplayStyle.Flex : DisplayStyle.None;
            tutSkip.style.display = tutStep >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (steerL != null) { bool show = game.mode == SkiViewGame.Mode.Pass || game.mode == SkiViewGame.Mode.Turn; steerL.style.display = steerR.style.display = show ? DisplayStyle.Flex : DisplayStyle.None; }
            if (lakes != null && lakes.ClassListContains("sv-menu--open") && Time.frameCount % 15 == 0 && forcedPack == null) RefreshLakes(false);
            TickJumpBar(); TickTutorial(); TickResults();
        }
        void TickJumpBar()
        {
            if (jbar == null) return;
            bool appr = game.mode == SkiViewGame.Mode.Jump && game.jphase == SkiViewGame.JPhase.Approach && !game.CutOn && game.JumpRunT > SkiViewGame.CUT_OPEN - 1.5f;
            bool ramp = game.mode == SkiViewGame.Mode.Jump && game.jphase == SkiViewGame.JPhase.Ramp && !game.Popped;
            bool show = JumpBar && (appr || ramp);
            jbar.style.display = show ? DisplayStyle.Flex : DisplayStyle.None; if (!show) return;
            float lo, hi, v;
            if (appr) { float a = SkiViewGame.CUT_OPEN - 1.5f, b = SkiViewGame.CUT_LATE; v = Mathf.InverseLerp(a, b, game.JumpRunT); lo = Mathf.InverseLerp(a, b, SkiViewGame.CUT_OPEN); hi = 1f; jbarLab.text = game.JumpRunT >= SkiViewGame.CUT_OPEN ? "CUT NOW" : "WAIT WIDE"; }
            else { v = game.JumpRampTau; lo = 0.78f; hi = 0.95f; jbarLab.text = v >= lo ? "POP NOW" : "RAMP"; }
            jbarZone.style.left = Length.Percent(lo * 100f); jbarZone.style.width = Length.Percent((hi - lo) * 100f);
            jbarMark.style.left = Length.Percent(Mathf.Clamp01(v) * 100f);
            jbar.EnableInClassList("sv-jbar--go", v >= lo && v <= hi);
        }

        // ---------------- tutorial (13-13d, tutorial-script.md) ----------------
        int tutStep = -1; float tutT; int tutCross0, tutCam0; SkiViewGame.CamMode tutCamLast; const string TUT_PREF = "sv.tutorial.done";
        public bool TutorialRunning => tutStep >= 0;
        struct TutCopy { public string title, touch, keys; }
        static readonly TutCopy[] TUT =
        {
            new TutCopy { title = "Welcome to Ski View", touch = "Thirty seconds of basics, then the lake is yours.", keys = "Thirty seconds of basics, then the lake is yours." },
            new TutCopy { title = "HANG ON", touch = "Sit back, tips up. The boat does the work.", keys = "Sit back, tips up. The boat does the work." },
            new TutCopy { title = "LEAN TO CUT", touch = "Hold the left side of the screen to lean left. Hold the right side to lean right. Let go to ride flat.", keys = "Hold A / ← or D / → to lean. Let go to ride flat." },
            new TutCopy { title = "GATES, THEN SIX BUOYS", touch = "Go through the gate, then round the outside of each orange buoy. Half a buoy still counts.", keys = "Go through the gate, then round the outside of each orange buoy. Half a buoy still counts." },
            new TutCopy { title = "CUT EARLIER", touch = "Start your lean before the wakes, not at the buoy.", keys = "Start your lean before the wakes, not at the buoy." },
            new TutCopy { title = "TURNAROUND", touch = "The boat idles and you settle in, still holding the handle. Then it's a deep-water start and a pull round the island, while the rope shortens for the next pass.", keys = "The boat idles and you settle in, still holding the handle. Then it's a deep-water start and a pull round the island, while the rope shortens for the next pass." },
            new TutCopy { title = "CHANGE THE VIEW", touch = "Tap CAM to switch the camera: HIGH, CLOSE, 1ST, BOAT. Your choice is remembered.", keys = "Press C to switch the camera: HIGH, CLOSE, 1ST, BOAT. Your choice is remembered." },
            new TutCopy { title = "WAIT WIDE", touch = "Hold wide on the far side of the boat. When it says CUT NOW, tap once to start your cut to the ramp.", keys = "Hold wide on the far side of the boat. When it says CUT NOW, press Space or click once to start your cut to the ramp." },
            new TutCopy { title = "POP AT THE TOP", touch = "Tap again on the ramp to POP. Best near the top: about the last fifth before the lip.", keys = "Press Space again on the ramp to POP. Best near the top: about the last fifth before the lip." },
            new TutCopy { title = "NICE. KEEP GOING", touch = "Late cuts are faster and steeper. Early pops fall back; late pops go out the front.", keys = "Late cuts are faster and steeper. Early pops fall back; late pops go out the front." },
            new TutCopy { title = "YOU'RE SET", touch = "Controls are always in Pause › Controls.", keys = "Controls are always in Pause › Controls." },
        };
        void BuildTutorial(VisualElement safe)
        {
            tutCard = El("sv-plate sv-plate--card sv-tut19", safe); El("sv-strip", tutCard);
            var top = El("sv-tut19__top", tutCard); tutCrumb = Lb("", "t-micro", top); tutDots = El("sv-tut19__dots", top);
            for (int i = 0; i < 10; i++) El("sv-tut19__dot", tutDots);
            tutTitle = Lb("", "t-h2", tutCard); tutText = Lb("", "t-body sv-tut19__text", tutCard);
            tutKeys = El("sv-tut19__keys", tutCard); tutProg = Lb("", "t-micro sv-tut19__prog", tutCard);
            foreach (var e in tutCard.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            tutCard.style.display = DisplayStyle.None;
            int forced = SkiViewArgs.I("tutorial", -1);
            if (forced >= 0) root.schedule.Execute(() => StartTutorial(forced)).StartingIn(200);
            else if (PlayerPrefs.GetInt(TUT_PREF, 0) == 0 && SkiViewPlayer.HasName && !SkiViewGame.HudHidden && !SkiViewArgs.Has("at") && !SkiViewArgs.Has("ui") && Application.isPlaying)
                root.schedule.Execute(() => StartTutorial(0)).StartingIn(400);
        }
        public void StartTutorial(int step) { if (game == null) return; tutStep = step; ShowStep(); }
        public void EndTutorial() { tutStep = -1; if (tutCard != null) tutCard.style.display = DisplayStyle.None; PlayerPrefs.SetInt(TUT_PREF, 1); PlayerPrefs.Save(); }
        void Next(int to = -1)
        {
            tutStep = to >= 0 ? to : tutStep + 1;
            if (!game.jumpMode && tutStep >= 7 && tutStep <= 9) tutStep = 10;   // jump steps only on a jump run
            if (tutStep > 10) { EndTutorial(); return; }
            ShowStep();
        }
        void ShowStep()
        {
            var t = TUT[tutStep]; bool touch = Application.isMobilePlatform; tutT = 0f;
            tutCard.style.display = DisplayStyle.Flex;
            tutCrumb.text = tutStep == 0 ? "TUTORIAL" : $"TUTORIAL · STEP {tutStep} OF 10";
            int di = 0; foreach (var d in tutDots.Children()) { d.EnableInClassList("sv-tut19__dot--on", di == tutStep - 1); d.EnableInClassList("sv-tut19__dot--done", di < tutStep - 1); di++; }
            string who = SkiViewPlayer.Name;
            tutTitle.text = tutStep == 0 && !string.IsNullOrEmpty(who) ? "Welcome to Ski View, " + who : t.title;
            tutText.text = touch ? t.touch : t.keys;
            tutKeys.Clear();
            tutProg.text = "";
            if (tutStep == 2)
            {
                if (touch) { Lb("HOLD LEFT HALF", "t-label", tutKeys); Lb("·", "t-label", tutKeys); Lb("HOLD RIGHT HALF", "t-label", tutKeys); }
                else { Key(tutKeys, "A"); Key(tutKeys, null, "arrow-left"); Lb("LEAN LEFT", "t-micro sv-tut19__kl", tutKeys); Key(tutKeys, "D"); Key(tutKeys, null, "arrow-right"); Lb("LEAN RIGHT", "t-micro sv-tut19__kl", tutKeys); }
                tutCross0 = game.sim != null ? game.sim.wakeCrossings : 0;
            }
            else if (tutStep == 5 || tutStep == 7 || tutStep == 8) { if (touch) Lb("TAP", "sv-key", tutKeys); else Key(tutKeys, "SPACE"); Lb(tutStep == 5 ? "TO SKIP" : tutStep == 7 ? "TO CUT" : "TO POP", "t-micro sv-tut19__kl", tutKeys); }
            else if (tutStep == 3) { var lg = El("sv-tut19__legend", tutKeys); lg.style.flexDirection = FlexDirection.Column; lg.style.flexGrow = 1; Legend(lg); }   // T5: legend in tutorial step 3
            else if (tutStep == 6) { if (touch) Lb("CAM", "sv-key", tutKeys); else { Key(tutKeys, "C"); } Lb("HIGH → CLOSE → 1ST → BOAT", "t-micro sv-tut19__kl", tutKeys); tutCamLast = game.camMode; }
            if (tutStep == 8) tutProg.text = "FELL BACK < 0.6 · EARLY · BEST 0.78–0.95 · LATE · OUT THE FRONT";
            foreach (var e in tutCard.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
        }
        void TickTutorial()
        {
            if (tutStep < 0 || game == null || MenuOpen) return;
            tutT += Time.unscaledDeltaTime; var m = game.mode; var sim = game.sim;
            if (SkiViewArgs.Has("tutorial") && SkiViewArgs.I("tutorial", 0) > 1) return;   // a single forced step stays up for the capture
            switch (tutStep)
            {
                case 0: if (tutT > 4f || Input.GetKeyDown(KeyCode.Return) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)) Next(m == SkiViewGame.Mode.DeepStart ? 1 : 2); break;
                case 1: if (m != SkiViewGame.Mode.DeepStart && tutT > 1.5f) Next(game.jumpMode ? 7 : 2); break;
                case 2:
                    int got = sim != null ? sim.wakeCrossings - tutCross0 : 0;
                    tutProg.text = $"WAKE CROSSINGS  {Mathf.Min(got, 2)} / 2";
                    if (got >= 2 || m == SkiViewGame.Mode.Pass) Next(3); break;
                case 3: if (m == SkiViewGame.Mode.Pass && tutT > 3f) { tutCard.style.display = DisplayStyle.None; tutStep = 31; } break;
                case 31: if (m == SkiViewGame.Mode.Sink || m == SkiViewGame.Mode.Fallen) Next(5); break;
                case 5: if (m == SkiViewGame.Mode.Pass) Next(6); break;
                case 6: if (game.camMode != tutCamLast || tutT > 6f) Next(game.jumpMode ? 7 : 10); break;
                case 7: if (game.CutOn || game.jphase == SkiViewGame.JPhase.Ramp) Next(8); break;
                case 8: if (game.jphase == SkiViewGame.JPhase.Flight || game.jphase == SkiViewGame.JPhase.Landing || m != SkiViewGame.Mode.Jump) Next(9); break;
                case 9: if (tutT > 4f) Next(10); break;
                case 10: if (tutT > 4f) EndTutorial(); break;
            }
        }
    }
}

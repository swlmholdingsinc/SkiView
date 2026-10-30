using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>
    /// Pass 18: UI Toolkit overlay built from Julia's kit (sv-tokens.uss / sv-components.uss, Barlow fonts, icons, PanelSettings
    /// Scale With Screen Size 1920x1080 Expand, SafeAreaRoot). Holds what the kit's notes §8 asks for (CAM button on touch, C / RB
    /// hint on desktop, the 1.5 s view toast, a toast at the start of each run) plus the Settings screen (Graphics: Camera motion
    /// Full / Reduced · Audio: master volume · Controls: Default camera + the camera binding row) and the Credits screen (CC BY sound
    /// lines verbatim from audio/LICENSES.md, CC0 thanks, Barlow OFL notice + full license). Everything is loaded from
    /// Resources/SkiViewUI so the lake packs carry no UI references. The IMGUI top bar stays for the rest of the HUD for now.
    /// </summary>
    public partial class SkiViewUI : MonoBehaviour
    {
        public static SkiViewUI Instance { get; private set; }
        public SkiViewGame game;
        UIDocument doc; VisualElement root, hud, toast, camBtn, camHint, settings, credits, license, panelBody;
        Label toastView, hintView; readonly List<VisualElement> dots = new List<VisualElement>();
        IVisualElementScheduledItem hideJob; float lastCamPress = -1f; int tab = 1; float timeScale0 = 1f;
        readonly List<VisualElement> menus = new List<VisualElement>();   // pass 19: every full-screen menu (settings, credits, license, pause, lakes, help)
        public bool MenuOpen { get { foreach (var m in menus) if (m != null && m.ClassListContains("sv-menu--open")) return true; return false; } }
        public const string VOL_PREF = "skiview.volume";

        public static SkiViewUI Ensure(SkiViewGame g)
        {
            if (Instance != null) { Instance.game = g; return Instance; }
            var ps = Resources.Load<PanelSettings>("SkiViewUI/SV_Panel");
            if (ps == null) { Debug.LogWarning("[SkiView] UI: Resources/SkiViewUI/SV_Panel missing, UI Toolkit overlay off"); return null; }
            var go = new GameObject("SkiViewUI"); DontDestroyOnLoad(go);
            var d = go.AddComponent<UIDocument>(); d.panelSettings = ps; d.sortingOrder = 10;
            var ui = go.AddComponent<SkiViewUI>(); ui.game = g; ui.doc = d;
            ui.Build(); go.AddComponent<SafeAreaRoot>();
            return ui;
        }

        void Awake() { Instance = this; AudioListener.volume = PlayerPrefs.GetFloat(VOL_PREF, 1f); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        static VisualElement El(string cls = null, VisualElement parent = null, string name = null)
        {
            var e = new VisualElement(); if (cls != null) foreach (var c in cls.Split(' ')) e.AddToClassList(c);
            if (name != null) e.name = name; parent?.Add(e); return e;
        }
        static Label Lb(string text, string cls, VisualElement parent) { var l = new Label(text); foreach (var c in cls.Split(' ')) l.AddToClassList(c); parent.Add(l); return l; }
        static Button Btn(string text, string cls, VisualElement parent, System.Action onClick, string icon = null)
        {
            var b = new Button(onClick); foreach (var c in cls.Split(' ')) b.AddToClassList(c);
            if (icon != null) { var i = El("sv-icon", b); var vi = Resources.Load<VectorImage>("SkiViewUI/Icons/" + icon); if (vi != null) i.style.backgroundImage = new StyleBackground(vi); }
            if (!string.IsNullOrEmpty(text)) { var l = new Label(text); l.pickingMode = PickingMode.Ignore; b.Add(l); }
            El("sv-halo", b).pickingMode = PickingMode.Ignore; parent.Add(b); return b;
        }

        void Build()
        {
            root = doc.rootVisualElement; root.pickingMode = PickingMode.Ignore;
            foreach (var n in new[] { "sv-tokens", "sv-components", "sv-game", "sv-hud-t5" }) { var ss = Resources.Load<StyleSheet>("SkiViewUI/" + n); if (ss != null) root.styleSheets.Add(ss); }
            // HUD (picking ignored except the CAM button)
            hud = El("sv-screen sv-hud", root); hud.pickingMode = PickingMode.Ignore;
            El("sv-fp-scrim", hud).pickingMode = PickingMode.Ignore;
            var safe = El("sv-safe", hud); safe.pickingMode = PickingMode.Ignore;
            toast = El("sv-toast", safe, "camToast"); toast.pickingMode = PickingMode.Ignore;
            Lb("CAMERA", "sv-toast__label", toast).pickingMode = PickingMode.Ignore;
            toastView = Lb("HIGH CHASE", "sv-toast__view", toast); toastView.pickingMode = PickingMode.Ignore;
            var dw = El("sv-toast__dots", toast); dw.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 4; i++) { var dd = El("sv-toast__dot", dw); dd.pickingMode = PickingMode.Ignore; dots.Add(dd); }
            if (Application.isMobilePlatform)
            {
                var camHit = new Button(CamPressed) { focusable = false }; camHit.AddToClassList("sv-hit"); camHit.AddToClassList("sv-hit--cam"); safe.Add(camHit);   // T5: 144 px tap area
                camBtn = Btn("CAM", "sv-cam-btn", camHit, CamPressed, null); camBtn.name = "cam"; camBtn.focusable = false; camBtn.pickingMode = PickingMode.Ignore;
                var ic = new VisualElement(); ic.AddToClassList("sv-icon"); ic.pickingMode = PickingMode.Ignore; camBtn.Insert(0, ic);
            }
            else
            {   // desktop / WebGL: the C · RB hint with the current view (clickable too, for mouse-only players)
                var h = new Button(CamPressed) { focusable = false }; h.AddToClassList("sv-cam-hint"); safe.Add(h); camHint = h;
                Lb("C", "sv-key", h).pickingMode = PickingMode.Ignore; Lb("RB", "sv-key", h).pickingMode = PickingMode.Ignore;
                El("sv-icon", h).pickingMode = PickingMode.Ignore;
                var col = El(null, h); col.pickingMode = PickingMode.Ignore;
                Lb("CAMERA", "t-micro", col).pickingMode = PickingMode.Ignore; hintView = Lb("HIGH CHASE", "sv-cam-hint__view", col); hintView.pickingMode = PickingMode.Ignore;
            }
            root.EnableInClassList("sv-motion-reduced", SkiViewGame.ReducedMotion);
            BuildHud19(safe);
            BuildSettings(); BuildCredits(); BuildLicense(); BuildPause(); BuildLakes(); BuildHelp(); BuildClinic(); BuildRecords(); BuildTitle();
            ImportLegacyLakeBests();
            // capture / review: ?ui=graphics|audio|controls|credits|license opens that screen (WebGL)
            string url = Application.absoluteURL ?? ""; int q = url.IndexOf("ui=");
            if (q >= 0)
            {
                string v = url.Substring(q + 3); int amp = v.IndexOf('&'); if (amp >= 0) v = v.Substring(0, amp);
                if (OpenFromArg(v)) { }
                else if (v == "credits") root.schedule.Execute(() => Open(credits)).StartingIn(300);
                else if (v == "license") root.schedule.Execute(() => Open(license)).StartingIn(300);
                else { int t = v == "graphics" ? 0 : v == "audio" ? 1 : v == "controls" ? 2 : -1; if (t >= 0) { tab = t; root.schedule.Execute(() => { Open(settings); }).StartingIn(300); } }
            }
        }

        // ---------------- camera control (§8) ----------------
        static readonly SkiViewGame.CamMode[] Cycle = { SkiViewGame.CamMode.SkierChase, SkiViewGame.CamMode.CloseChase, SkiViewGame.CamMode.FirstPerson, SkiViewGame.CamMode.Broadcast };
        public static readonly string[] ViewNames = { "HIGH CHASE", "CLOSE CHASE", "FIRST PERSON", "BOAT CAM" };
        public static int CycleIndex(SkiViewGame.CamMode m) => Mathf.Max(0, System.Array.IndexOf(Cycle, m));
        public static SkiViewGame.CamMode FromCycle(int i) => Cycle[Mathf.Clamp(i, 0, 3)];
        void CamPressed()
        {
            if (Time.unscaledTime - lastCamPress < 0.15f || game == null || MenuOpen) return;
            lastCamPress = Time.unscaledTime; game.CycleCamera();
        }
        /// <summary>Show the 2.0 s view toast (restarts on a new press, never stacks) and update the desktop hint / FP scrim.</summary>
        public void ShowCamToast(SkiViewGame.CamMode m)
        {
            if (toast == null) return;
            int idx = CycleIndex(m); toastView.text = ViewNames[idx];
            for (int i = 0; i < dots.Count; i++) dots[i].EnableInClassList("sv-toast__dot--on", i == idx);
            if (hintView != null) hintView.text = ViewNames[idx];
            hud.EnableInClassList("sv-hud--fp", m == SkiViewGame.CamMode.FirstPerson);
            root.EnableInClassList("sv-motion-reduced", SkiViewGame.ReducedMotion);   // T5: reduced motion = opacity-only pill
            toast.AddToClassList("sv-toast--show");
            hideJob?.Pause();
            hideJob = toast.schedule.Execute(() => toast.RemoveFromClassList("sv-toast--show")).StartingIn(1500);   // T26: camera name low on screen for 1.5 s
        }
        public void SetHudVisible(bool on) { if (hud != null) hud.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; }

        /// <summary>True when the screen point (Input space, bottom-left origin) is over a pickable UI Toolkit control or a menu is open,
        /// so the game's touch / mouse loop doesn't also read it as steering or a tap (kit notes §8).</summary>
        public static bool Blocks(Vector2 screenPos)
        {
            var ui = Instance; if (ui == null || ui.root?.panel == null) return false;
            if (ui.MenuOpen) return true;
            var p = RuntimePanelUtils.ScreenToPanel(ui.root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var ve = ui.root.panel.Pick(p);
            return ve != null && ve.pickingMode == PickingMode.Position && ve != ui.root;
        }

        // ---------------- settings ----------------
        VisualElement Menu(out VisualElement safe, string crumb, string title, System.Action back)
        {
            var m = El("sv-screen sv-menu", root); El("sv-scrim-full", m); menus.Add(m);
            safe = El("sv-safe", m);
            var hd = El("sv-header", safe);
            Btn(null, "sv-btn sv-btn--icon", hd, back, "back");
            var tt = El(null, hd); Lb(crumb, "t-micro", tt); Lb(title, "t-h1", tt);
            return m;
        }
        void BuildSettings()
        {
            settings = Menu(out var safe, "OPTIONS", "SETTINGS", NavBack);
            var body = El("sv-body", safe); var tabs = El("sv-tabs", body);
            string[] tn = { "GRAPHICS", "AUDIO", "CONTROLS" }; string[] ti = { "graphics", "audio", "controls" };
            var tabBtns = new List<Button>();
            for (int i = 0; i < 3; i++) { int k = i; tabBtns.Add(Btn(tn[i], "sv-tab", tabs, () => { tab = k; for (int j = 0; j < 3; j++) tabBtns[j].EnableInClassList("sv-tab--on", j == k); FillPanel(); }, ti[i])); }
            tabBtns[tab].AddToClassList("sv-tab--on");
            Btn("CREDITS", "sv-tab", tabs, () => Open(credits), "info");
            var plate = El("sv-plate sv-plate--card sv-panel", body); El("sv-strip", plate);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; plate.Add(sv); panelBody = sv.contentContainer;
            FillPanel();
        }
        VisualElement Row(string label, string caption)
        {
            var r = El("srow", panelBody); r.focusable = true; var t = El("srow__txt", r);
            Lb(label, "t-label", t); if (caption != null) Lb(caption, "t-caption", t); return r;
        }
        void Seg(VisualElement row, string[] items, int on, System.Action<int> pick)
        {
            var s = El("sv-seg", row); var bs = new List<Button>();
            for (int i = 0; i < items.Length; i++)
            {
                int k = i; var b = new Button(() => { for (int j = 0; j < bs.Count; j++) bs[j].EnableInClassList("sv-seg__item--on", j == k); pick(k); }) { text = items[i] };
                b.AddToClassList("sv-seg__item"); if (i == on) b.AddToClassList("sv-seg__item--on"); s.Add(b); bs.Add(b);
            }
        }
        void FillPanel()
        {
            panelBody.Clear();
            SkiViewGame.LoadCameraSettings();
            if (tab == 0)
            {
                Seg(Row("CAMERA MOTION", "REDUCED DAMPS FIRST-PERSON ROLL AND BOAT-CAM BUZZ, SLOWER FOLLOW, LONGER BLENDS"), new[] { "FULL", "REDUCED" },
                    SkiViewGame.ReducedMotion ? 1 : 0, k => SkiViewGame.SaveCameraSettings(SkiViewGame.DefaultCamera, k == 1));
                Seg(Row("QUALITY", "AUTO PICKS MOBILE ON PHONES, PC ON DESKTOP"), new[] { "MOBILE", "PC" }, Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 1),
                    k => QualitySettings.SetQualityLevel(Mathf.Clamp(k, 0, QualitySettings.names.Length - 1), true));
                // T6: presets on top of the platform tier (High = the current look); both persist between sessions
                Seg(Row("PRESET", "LOW AND MED LOWER REFLECTION RESOLUTION, FAR-TREE DENSITY AND SPRAY · TREES UPDATE ON THE NEXT LAKE LOAD"), new[] { "LOW", "MED", "HIGH" },
                    (int)SkiViewQuality.Current, k => SkiViewQuality.SetPreset((SkiViewQuality.Preset)k));
                Seg(Row("WATER REFLECTIONS", null), new[] { "OFF", "ON" }, SkiViewQuality.Reflections ? 1 : 0, k => SkiViewQuality.SetReflections(k == 1));
            }
            else if (tab == 1)
            {
                var r = Row("MASTER VOLUME", null);
                var sl = new Slider(0f, 1f) { value = AudioListener.volume }; sl.AddToClassList("sv-slider"); sl.style.width = 520;
                sl.RegisterValueChangedCallback(e => { AudioListener.volume = e.newValue; PlayerPrefs.SetFloat(VOL_PREF, e.newValue); PlayerPrefs.Save(); });
                r.Add(sl);
            }
            else
            {
                int cur = SkiViewGame.DefaultCamera >= 0 ? CycleIndex((SkiViewGame.CamMode)SkiViewGame.DefaultCamera) : 0;
                Seg(Row("DEFAULT CAMERA", "EVERY RUN STARTS IN THIS VIEW; CAM CHANGES IT FOR THE CURRENT RUN ONLY"), new[] { "HIGH CHASE", "CLOSE CHASE", "FIRST PERSON", "BOAT CAM" },
                    cur, k => SkiViewGame.SaveCameraSettings((int)FromCycle(k), SkiViewGame.ReducedMotion));
                Seg(Row("SPEED UNITS", "HUD, PAUSE, RESULTS AND THE JUMP DISTANCE"), new[] { "MPH", "KM/H" }, SkiViewUI.Kmh ? 1 : 0, k => { PlayerPrefs.SetInt(UNITS_PREF, k); PlayerPrefs.Save(); });   // T25
                Seg(Row("JUMP TIMING BAR", "CUT AND POP WINDOWS ON A JUMP RUN"), new[] { "OFF", "ON" }, JumpBar ? 1 : 0, k => { PlayerPrefs.SetInt(JUMPBAR_PREF, k); PlayerPrefs.Save(); });   // T26
                Seg(Row("TOURNAMENT JUMP BUOYS", "ALL 13 JUMP-COURSE BUOYS (OFF: TIMING PAIRS + 180 M MARKER)"), new[] { "OFF", "ON" }, SkiViewGame.TournamentJumpBuoys ? 1 : 0, k => { PlayerPrefs.SetInt(SkiViewGame.TOURN_JUMP_PREF, k); PlayerPrefs.Save(); });
                if (!Application.isMobilePlatform) Seg(Row("KEY HINTS", "THE KEY STRIP ALONG THE BOTTOM OF THE SCREEN (DESKTOP)"), new[] { "OFF", "ON" }, Hints ? 1 : 0, k => { PlayerPrefs.SetInt(HINTS_PREF, k); PlayerPrefs.Save(); });
                var b = Row("CAMERA (NEXT VIEW)", null); var keys = El("sv-seg", b);
                foreach (var k in new[] { "C", "RB", "CAM BUTTON" }) Lb(k, "sv-key", keys);
                var st = Row("STEER", null); var k2 = El("sv-seg", st); Key(k2, "A"); Key(k2, "D"); Key(k2, null, "arrow-left"); Key(k2, null, "arrow-right"); Lb("HOLD LEFT / RIGHT HALF", "sv-key", k2);
                var tp = Row("ACTION / SKIP TURN", null); var k3 = El("sv-seg", tp); foreach (var k in new[] { "SPACE", "TAP", "CLICK" }) Lb(k, "sv-key", k3);
                var ld = Row("LOOK DOWN (FIRST PERSON)", null); var k4 = El("sv-seg", ld); Key(k4, "S"); Key(k4, null, "arrow-down");
                var hp = Row("CONTROLS HELP", "EVERY INPUT ON TOUCH, KEYBOARD AND MOUSE"); Btn("OPEN", "sv-btn sv-btn--sm", hp, () => Open(help), "controls");
                var rt = Row("REPLAY TUTORIAL", null); Btn("REPLAY", "sv-btn sv-btn--sm", rt, () => { CloseMenus(); StartTutorial(0); }, "retry");
            }
        }

        // ---------------- credits ----------------
        static readonly string[] CC_BY =
        {
            "\"acceleration\" by sanian (https://freesound.org/s/535608/), licensed CC BY 4.0. Trimmed, mixed and loudness-normalised for {TITLE}.",
            "\"Lake shore waves from boat wake.wav\" by juskiddink (https://freesound.org/s/102809/), licensed CC BY 4.0. Trimmed, mixed and loudness-normalised for {TITLE}.",
            "\"water sea spray H06.wav\" by volivieri (https://freesound.org/s/634611/), licensed CC BY 4.0. Trimmed, mixed and loudness-normalised for {TITLE}.",
            "\"WATRSplsh_Water Splash, Big, Dive Into Water, Person or Animal_Nicholas Judy_TDC\" by designerschoice (recorded by Nicholas Judy) (https://freesound.org/s/811439/), licensed CC BY 4.0. Trimmed, mixed and loudness-normalised for {TITLE}.",
            "\"WWS StationaryEngine8211running\" by Work With Sounds / Technical Museum of Slovenia (https://commons.wikimedia.org/wiki/File:WWS_StationaryEngine8211running.ogg), licensed CC BY 4.0. Trimmed, mixed and loudness-normalised for {TITLE}.",
            "\"WWS StationaryEngine\" by Work With Sounds / barbara.grilc (https://commons.wikimedia.org/wiki/File:WWS_StationaryEngine.ogg), licensed CC BY 4.0. Trimmed, mixed and loudness-normalised for {TITLE}.",
            "\"Wind in forest (Gravity Sound)\" by Gravity Sound (https://commons.wikimedia.org/wiki/File:Wind_in_forest_(Gravity_Sound).wav), licensed CC BY 4.0. Trimmed, mixed and loudness-normalised for {TITLE}.",
        };
        void BuildCredits()
        {
            credits = Menu(out var safe, "OPTIONS", "CREDITS", () => Open(settings));
            var plate = El("sv-plate sv-plate--card sv-panel", safe); plate.style.flexGrow = 1; El("sv-strip", plate);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; plate.Add(sv); var c = sv.contentContainer;
            void H(string t) => Lb(t, "sv-credits-h", c);
            void T(string t) => Lb(t, "sv-credits-text", c);
            H(SkiViewBranding.Title); T("A game by SWLM Software"); T("\u00a9 2026 SWLM Holdings Inc. All rights reserved.");
            H("SOUND"); T("Licensed CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/):"); foreach (var l in CC_BY) T(l.Replace("{TITLE}", SkiViewBranding.Title));
            H("PUBLIC DOMAIN"); T("Additional sound effects, textures and skies are public domain (CC0). Thank you to everyone who shares their work.");
            H("FONTS");
            T("Barlow and Barlow Condensed: Copyright 2017 The Barlow Project Authors (https://github.com/jpt/barlow).");
            T("Licensed under the SIL Open Font License, Version 1.1 (https://openfontlicense.org).");
            T("\"SV Tab Condensed\" is a modified version of Barlow Condensed (tabular figures), distributed under the same license.");
            var row = El(null, c); row.style.flexDirection = FlexDirection.Row;
            Btn("VIEW FONT LICENSE", "sv-btn sv-btn--sm", row, () => Open(license));
            H("THANKS"); T("To everyone who drives the boat at dawn.");
        }
        void BuildLicense()
        {
            license = Menu(out var safe, "CREDITS", "FONT LICENSE", () => Open(credits));
            var plate = El("sv-plate sv-plate--card sv-panel", safe); plate.style.flexGrow = 1;
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; plate.Add(sv);
            var ta = Resources.Load<TextAsset>("SkiViewUI/OFL-Barlow");
            var l = new Label(ta != null ? ta.text : "OFL-Barlow.txt missing"); l.AddToClassList("sv-credits-text"); l.style.fontSize = 20; sv.Add(l);
        }

        // ---------------- open / close ----------------
        public void OpenSettings() { Open(settings); }
        public void OpenCredits() { Open(credits); }
        void Open(VisualElement m)
        {
            if (!MenuOpen) { timeScale0 = Time.timeScale; Time.timeScale = 0f; }
            foreach (var x in menus) x?.EnableInClassList("sv-menu--open", x == m);
            if (m == lakes) RefreshLakes(true); if (m == pause) RefreshPause(); if (m == recordsMenu) FillRecords();
            if (m == settings) { FillPanel(); int i = 0; settings.Query<Button>(className: "sv-tab").ForEach(b => { if (i < 3) b.EnableInClassList("sv-tab--on", i == tab); i++; }); }
            SetHudVisible(false);
        }
        public void CloseMenus()
        {
            foreach (var x in menus) x?.RemoveFromClassList("sv-menu--open");
            if (titleReturn) { titleReturn = false; OpenTitle(); return; }   // T22: Settings / Credits opened from the title go back to it
            Time.timeScale = timeScale0 <= 0f ? 1f : timeScale0; SetHudVisible(true);
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !TitleEsc())
            {
                // b10.27 T24 Gap B: rope-choice Esc → results plate (not a soft-lock / not Pause-only)
                if (game != null && game.RopeChoiceOpen) { game.CancelRopeChoice(); return; }
                if (MenuOpen) NavBack(); else OpenPause();
            }   // Esc / Android back
            if (Input.GetKeyDown(KeyCode.Tab) && tutStep >= 0) EndTutorial();
            Tick19();
            if (!MenuOpen && Input.GetKeyDown(KeyCode.JoystickButton5)) CamPressed();
        }
    }
}

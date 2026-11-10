using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>T22 main / title screen (Julia's 22_final: sv-title-t22.uxml/.uss + 9-slices in Resources/SkiViewUI/Title).
    /// Shown once at startup over the paused game. SLALOM / JUMP / TRICKS open the event panel (Quick start / Setup).
    /// b10.20 T29: Tricks enabled; SKI VIEW wordmark; no Wade logos on title. INTERNAL watermark until licence signed.
    /// Skipped for captures / deep links: ?clean, ?title=0, ?ui=, ?mode=, ?at=.</summary>
    public partial class SkiViewUI
    {
        VisualElement title, titlePanel, soonToast; Label soonText, panelName, lastSetup; bool titleReturn, panelJump, panelTricks, panelChallenge;
        IVisualElementScheduledItem soonJob; static bool skipTitleOnce;
        public bool TitleOpen => title != null && title.ClassListContains("sv-menu--open");

        static bool SkipTitle() => SkiViewArgs.Get("clean") != null || SkiViewArgs.Get("title") == "0" || SkiViewArgs.Get("ui") != null
                                   || SkiViewArgs.Get("mode") != null || SkiViewArgs.Get("at") != null || !Application.isPlaying;

        void BuildTitle()
        {
            var vta = Resources.Load<VisualTreeAsset>("SkiViewUI/Title/sv-title-t22");
            if (vta == null) { Debug.LogWarning("[SkiView] T22 title UXML missing"); return; }
            title = El("sv-screen sv-menu", root, "title"); menus.Add(title);
            vta.CloneTree(title);
            var tr = title.Q("title-root"); tr.style.position = Position.Absolute; tr.style.left = tr.style.right = tr.style.top = tr.style.bottom = 0;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            bool phone = Application.isMobilePlatform, four3 = aspect <= 1.45f;
            tr.EnableInClassList("sv-title--phone", phone); tr.EnableInClassList("sv-title--4x3", four3); tr.EnableInClassList("sv-title--phone-4x3", phone && four3);
            tr.EnableInClassList("sv-reduced-motion", SkiViewGame.ReducedMotion);

            var hero = title.Q("hero"); string hp = SkiViewBranding.TitleHeroPath(aspect);
            var ht = hp != null ? Resources.Load<Texture2D>(hp) : null;
            if (ht != null) hero.style.backgroundImage = new StyleBackground(ht); else hero.style.display = DisplayStyle.None;
            // Title mark is the SWLM hexagon plus the company name. No crest and no tagline.
            title.Q("rights-tag").style.display = DisplayStyle.None;
            hero.style.display = DisplayStyle.None;
            title.Q("watermark").style.display = SkiViewBranding.Flag.wadeLicenseSigned ? DisplayStyle.None : DisplayStyle.Flex;
            var logoEl = title.Q("logo");
            var mark = Resources.Load<Texture2D>("Branding/SWLMLogoMark");
            if (logoEl != null)
            {
                logoEl.AddToClassList("sv-swlm-mark");
                logoEl.style.width = 63;
                logoEl.style.height = 80;
                if (mark != null)
                {
                    logoEl.style.backgroundImage = new StyleBackground(mark);
                    logoEl.style.display = DisplayStyle.Flex;
                }
                else logoEl.style.display = DisplayStyle.None;
            }
            var company = title.Q<Label>("swlm-name");
            if (company == null)
            {
                company = new Label("SWLM Holdings Inc.");
                company.name = "swlm-name";
                company.AddToClassList("sv-swlm-name");
                title.Q("title-root").Add(company);
            }
            company.text = "SWLM Holdings Inc.";
            var copy = title.Q<Label>("copyright");
            if (copy != null) copy.text = "\u00a9 2026 SWLM Holdings Inc.";
            HideTitleCrest(title);
            var wm = title.Q<Label>("ski-view-wordmark");
            if (wm != null) wm.style.display = DisplayStyle.None;

            titlePanel = title.Q("event-panel"); var pl = titlePanel.Query<Label>().ToList(); panelName = pl[0]; lastSetup = title.Q<Label>("last-setup");
            soonToast = title.Q("soon-toast"); soonText = soonToast.Q<Label>();
            title.Q<Button>("ev-slalom").clicked += () => OpenEventPanel(false, false);
            title.Q<Button>("ev-jump").clicked += () => OpenEventPanel(true, false);
            title.Q<Button>("ev-tricks").clicked += () => OpenEventPanel(false, true);
            // b10.17 Clinic Phase A: enable Clinic; Beat Wade stays Coming soon
            var clinicBtn = title.Q<Button>("sec-clinic");
            if (clinicBtn != null)
            {
                clinicBtn.text = SkiViewClinic.ModeName().ToUpperInvariant();
                clinicBtn.clicked += () => OpenClinicList();
                clinicBtn.SetEnabled(true);
            }
            title.Q<Button>("sec-beat").clicked += () => OpenEventPanel(false, false, true);
            title.Q<Button>("sec-settings").clicked += () => { titleReturn = true; Open(settings); };
            title.Q<Button>("sec-credits").clicked += () => { titleReturn = true; Open(credits); };
            var recordsBtn = title.Q<Button>("sec-records");
            if (recordsBtn != null) recordsBtn.clicked += () => { titleReturn = true; pauseReturn = false; OpenRecords(SkiViewRecords.Discipline.Slalom, "TITLE"); };
            title.Q<Button>("quick-start").clicked += QuickStart;
            title.Q<Button>("setup").clicked += () => { StartEvent(); OpenRunSetup(); };   // b9: ski / rope / speed first
            var choose = title.Q<Button>("choose-lake");
            if (choose != null) choose.clicked += ChooseLakeFromEvent;
            if (game != null && game.jumpRamp == null) title.Q("ev-jump").SetEnabled(false);
            // b10.11 Phase A: enable Tricks (remove Coming soon)
            var trBtn = title.Q("ev-tricks");
            if (trBtn != null)
            {
                trBtn.RemoveFromClassList("sv-event--disabled");
                trBtn.SetEnabled(true);
                var soon = trBtn.Q(className: "sv-tag-soon"); if (soon != null) soon.style.display = DisplayStyle.None;
                var sub = trBtn.Q(className: "sv-event__sub"); if (sub is Label sl) sl.text = "20 s · two passes · 1 ski";
            }
            // b10.14 / T31: Jump subtitle
            var jBtn = title.Q("ev-jump");
            if (jBtn != null)
            {
                var jsub = jBtn.Q(className: "sv-event__sub"); if (jsub is Label jl) jl.text = "3 passes · longest jump";
            }
            if (!SkipTitle() && !skipTitleOnce) root.schedule.Execute(OpenTitle).StartingIn(50);
            skipTitleOnce = false;
        }

        public void OpenTitle()
        {
            game?.FlushSlalomSet();
            if (title == null) return;
            titleReturn = false; Open(title); titlePanel.RemoveFromClassList("sv-event-panel--open");
            var tr = title.Q("title-root"); tr.RemoveFromClassList("sv-title--in");
            int i = 0; title.Query(className: "sv-event").ForEach(e => { e.style.transitionDelay = new System.Collections.Generic.List<TimeValue> { new TimeValue(SkiViewGame.ReducedMotion ? 0 : 50 * i++, TimeUnit.Millisecond) }; });
            tr.schedule.Execute(() => tr.AddToClassList("sv-title--in")).StartingIn(30);
            title.Q("ev-slalom").Focus();
        }

        void OpenEventPanel(bool jump, bool tricks = false, bool challenge = false)
        {
            panelChallenge = challenge;
            panelJump = jump && !tricks && !challenge;
            panelTricks = tricks && !challenge;
            title.Q("ev-slalom").EnableInClassList("sv-event--selected", !jump && !tricks && !challenge);
            title.Q("ev-jump").EnableInClassList("sv-event--selected", jump && !tricks && !challenge);
            title.Q("ev-tricks").EnableInClassList("sv-event--selected", tricks && !challenge);
            var beat = title.Q("sec-beat"); if (beat != null) beat.EnableInClassList("sv-event--selected", challenge);
            panelName.text = challenge ? "CHALLENGE" : tricks ? "TRICKS" : jump ? "JUMP" : "SLALOM";
            lastSetup.text = tricks ? SetupSummaryTricks() : SetupSummary(jump);
            titlePanel.AddToClassList("sv-event-panel--open");
            var choose = title.Q<Button>("choose-lake");
            if (choose != null) choose.Focus(); else title.Q("quick-start").Focus();
        }

        void ChooseLakeFromEvent()
        {
            StartEvent();
            lakesFromSetup = false;
            lakesFromEvent = true;
            Open(lakes);
        }

        static void HideTitleCrest(VisualElement root)
        {
            if (root == null) return;
            root.Query(className: "sv-crest").ForEach(e => e.style.display = DisplayStyle.None);
            root.Query(className: "sv-crest-tag").ForEach(e => e.style.display = DisplayStyle.None);
            foreach (var name in new[] { "crest", "crest-tag", "seeley-crest", "seeley-tagline" })
            {
                var e = root.Q(name);
                if (e != null) e.style.display = DisplayStyle.None;
            }
            root.Query<Label>().ForEach(l =>
            {
                if (l == null || string.IsNullOrEmpty(l.text)) return;
                if (l.name == "swlm-name" || l.name == "copyright") return;
                if (l.text.IndexOf("Seeley", System.StringComparison.Ordinal) >= 0 || l.text.IndexOf("Strive for Perfection", System.StringComparison.Ordinal) >= 0)
                    l.style.display = DisplayStyle.None;
            });
        }

        void StartEvent()
        {
            if (game == null) return;
            if (panelTricks)
            {
                if (game.jumpMode) game.ToggleJumpMode();
                game.trickMode = true;
                game.SetTrickGear(true);
                return;
            }
            if (game.trickMode) game.ToggleTrickMode();
            if (game.jumpRamp != null && game.jumpMode != panelJump) game.ToggleJumpMode();
        }
        void QuickStart() { StartEvent(); game?.ApplySetup(); if (panelTricks) game?.BeginTrickRound(); else if (panelJump) game?.BeginJumpRound(); titleReturn = false; CloseMenus(); }   // b9 / b10.11 / b10.14

        void SoonToast(string text, VisualElement beside)
        {
            soonText.text = text;
            var wb = beside.worldBound; var tb = title.Q("title-root").worldBound;
            soonToast.style.left = wb.xMax - tb.xMin + 24; soonToast.style.top = wb.yMin - tb.yMin + (wb.height - 88) * 0.5f;
            soonToast.AddToClassList("sv-soon-toast--show");
            soonJob?.Pause(); soonJob = soonToast.schedule.Execute(() => soonToast.RemoveFromClassList("sv-soon-toast--show")).StartingIn(2000);
        }

        /// <summary>Esc on the title: close the event panel; nothing else (no quit prompt on WebGL).</summary>
        bool TitleEsc()
        {
            if (!TitleOpen) return false;
            if (titlePanel.ClassListContains("sv-event-panel--open")) { titlePanel.RemoveFromClassList("sv-event-panel--open"); title.Q(panelTricks ? "ev-tricks" : panelJump ? "ev-jump" : "ev-slalom").Focus(); }
            return true;
        }
    }
}

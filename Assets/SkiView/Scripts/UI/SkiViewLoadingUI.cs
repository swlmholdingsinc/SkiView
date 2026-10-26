using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>Pass 19 (Julia batch 5): loading screen (16: full-bleed lake art, name, region, blurb, progress, rotating tip from
    /// copy/loading-tips.md) and the full-screen pack states (16b downloading with speed / ETA, 16c failed, 16d offline).
    /// Lives on the boot object so it survives the scene swap. Preview: ?ui=loading | pack | pack_failed | pack_offline.</summary>
    public class SkiViewLoadingUI : MonoBehaviour
    {
        public static bool Ready { get; private set; }
        UIDocument doc; VisualElement root, screen, art, bar, fill; Label crumb, name, region, blurb, stat, tip, sub; VisualElement acts;
        string shownKey, mode; float tipAt; int tipIdx = -1; readonly System.Collections.Generic.List<int> bag = new System.Collections.Generic.List<int>();
        long lastGot; float lastT, speed; string preview;

        static readonly string[,] TIPS =
        {
            { "Start your lean before the wakes, not at the buoy.", "Start your lean before the wakes, not at the buoy." },
            { "Round the outside of each orange buoy. Half a buoy still counts.", "Round the outside of each orange buoy. Half a buoy still counts." },
            { "Let go to ride flat. Hold left or right to lean.", "Let go to ride flat. Hold A / D or the arrow keys to lean." },
            { "Tap CAM to switch views: HIGH, CLOSE, 1ST, BOAT. Your choice is remembered.", "Press C to switch views: HIGH, CLOSE, 1ST, BOAT. Your choice is remembered." },
            { "In first person, the handle and your hands show your lean.", "In first person, press S or the down arrow to look down at your skis." },
            { "Jump: wait wide, tap on CUT NOW, then tap again to POP near the top of the ramp.", "Jump: wait wide, press Space on CUT NOW, then again to POP near the top." },
            { "Late cuts arrive faster and steeper. Early pops fall back; late pops go out the front.", "Late cuts arrive faster and steeper. Early pops fall back; late pops go out the front." },
            { "No cut by about 2.6 s before the ramp means you miss it.", "No cut by about 2.6 s before the ramp means you miss it." },
            { "Each pass you clear shortens the rope for the next one.", "Each pass you clear shortens the rope for the next one." },
            { "Tap during the turnaround to skip straight to set-up.", "Press Space during the turnaround to skip to set-up." },
            { "Downloaded lakes play offline.", "Downloaded lakes play offline." },
            { "Controls are always in Pause > Controls.", "Controls are always in Pause > Controls." },
            { "Camera motion too much? Settings > Graphics > Camera motion > Reduced.", "Camera motion too much? Settings > Graphics > Camera motion > Reduced." },
        };

        void Start()
        {
            var ps = Resources.Load<PanelSettings>("SkiViewUI/SV_Panel"); if (ps == null) return;
            doc = gameObject.AddComponent<UIDocument>(); doc.panelSettings = ps; doc.sortingOrder = 50;
            root = doc.rootVisualElement; root.pickingMode = PickingMode.Ignore;
            foreach (var n in new[] { "sv-tokens", "sv-components", "sv-game" }) { var ss = Resources.Load<StyleSheet>("SkiViewUI/" + n); if (ss != null) root.styleSheets.Add(ss); }
            screen = E("sv-screen sv-load19", root); art = E("sv-load19__art", screen); E("sv-scrim-full sv-load19__scrim", screen);
            var safe = E("sv-safe sv-load19__safe", screen);
            var col = E("sv-plate sv-load19__plate", safe);
            crumb = L("", "t-micro", col); name = L("", "t-h1", col); region = L("", "t-label sv-load19__region", col); blurb = L("", "t-body sv-load19__blurb", col);
            stat = L("", "t-label sv-load19__stat", col); bar = E("sv-lakes19__bar sv-load19__bar", col); fill = E("sv-lakes19__fill", bar);
            sub = L("", "t-caption sv-load19__sub", col); acts = E("sv-load19__acts", col);
            tip = L("", "t-body sv-load19__tip", safe);
            screen.style.display = DisplayStyle.None; Ready = true;
            preview = SkiViewArgs.Get("ui"); if (preview != null && !(preview == "loading" || preview.StartsWith("pack"))) preview = null;
        }
        void OnDestroy() { Ready = false; }
        VisualElement E(string cls, VisualElement p) { var e = new VisualElement(); foreach (var c in cls.Split(' ')) e.AddToClassList(c); e.pickingMode = PickingMode.Ignore; p.Add(e); return e; }
        Label L(string t, string cls, VisualElement p) { var l = new Label(t); foreach (var c in cls.Split(' ')) l.AddToClassList(c); l.pickingMode = PickingMode.Ignore; p.Add(l); return l; }
        void B(string t, bool primary, System.Action a)
        {
            var b = new Button(a); b.AddToClassList("sv-btn"); b.AddToClassList(primary ? "sv-btn--primary" : "sv-btn--ghost"); b.AddToClassList("sv-load19__btn");
            var l = new Label(t); l.pickingMode = PickingMode.Ignore; b.Add(l); acts.Add(b); acts.pickingMode = PickingMode.Position; b.pickingMode = PickingMode.Position;
        }
        static string IdOf(string key) => string.IsNullOrEmpty(key) || !key.StartsWith("SkiViewLake_") ? "skiview" : key.Substring(12);
        static string Mb(long b) => (b / 1000000f).ToString("0.0") + " MB";

        void Update()
        {
            if (!Ready) return;
            string key = SkiViewBoot.LoadingKey; bool busy = SkiViewBoot.Busy;
            string m = SkiViewBoot.DlFailed && key != null ? (SkiViewBoot.Offline ? "offline" : "failed") : busy ? (SkiViewBoot.Downloading ? "pack" : "loading") : null;
            float pct = SkiViewBoot.Progress; long got = SkiViewBoot.DlGot, tot = SkiViewBoot.DlTotal;
            if (preview != null)
            {   // mockup states on Harrow Pond / Tall Pine
                key = preview == "loading" ? "SkiViewLake_tallpine" : "SkiViewLake_harrowpond"; m = preview == "loading" ? "loading" : preview == "pack" ? "pack" : preview == "pack_failed" ? "failed" : "offline";
                tot = 8000000; got = m == "pack" ? 4900000 : m == "failed" ? 4900000 : 0; pct = m == "loading" ? 0.58f : got / (float)tot; speed = 1.2e6f;
            }
            if (m == null) { if (screen.style.display != DisplayStyle.None) screen.style.display = DisplayStyle.None; shownKey = null; mode = null; return; }
            screen.style.display = DisplayStyle.Flex; screen.pickingMode = m == "loading" ? PickingMode.Ignore : PickingMode.Position;
            string id = IdOf(key); var lc = System.Array.Find(SkiViewUI.LAKES, x => x.id == id);
            if (key != shownKey || m != mode)
            {
                shownKey = key; mode = m; var tex = Resources.Load<Texture2D>("SkiViewUI/Lakes/" + id); art.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
                name.text = lc.name ?? id.ToUpperInvariant(); region.text = lc.region ?? ""; blurb.text = lc.blurb ?? "";
                acts.Clear(); acts.pickingMode = PickingMode.Ignore;
                bar.EnableInClassList("sv-load19__bar--fail", m == "failed");
                crumb.text = m == "loading" ? "LOADING" : m == "pack" ? "DOWNLOADING LAKE PACK" : m == "failed" ? "LAKE PACK" : "YOU'RE OFFLINE";
                if (m == "pack") { B("SKI AT SKI VIEW INSTEAD", false, () => { SkiViewBoot.CancelLoad(); }); B("CANCEL", false, () => SkiViewBoot.CancelLoad()); }
                if (m == "failed") { B("RETRY", true, () => { SkiViewBoot.ClearFailed(); SkiViewBoot.Load(key); }); B("BACK TO LAKES", false, () => { SkiViewBoot.ClearFailed(); SkiViewUI.Instance?.OpenLakes(); }); }
                if (m == "offline") { B("SKI AT SKI VIEW", true, () => { SkiViewBoot.ClearFailed(); SkiViewBoot.Load(SkiViewBoot.SceneKey("skiview")); }); B("BACK TO LAKES", false, () => { SkiViewBoot.ClearFailed(); SkiViewUI.Instance?.OpenLakes(); }); }
                tipAt = 0f; lastGot = got; lastT = Time.unscaledTime;
            }
            float dt = Time.unscaledTime - lastT; if (preview == null && dt > 0.5f) { speed = Mathf.Lerp(speed, (got - lastGot) / dt, 0.5f); lastGot = got; lastT = Time.unscaledTime; }
            fill.style.width = Length.Percent(Mathf.Clamp01(pct) * 100f);
            if (m == "loading") { stat.text = "LOADING " + (lc.name ?? "") + " · " + Mathf.RoundToInt(pct * 100f) + "%"; sub.text = ""; }
            else if (m == "pack")
            {
                stat.text = $"{Mb(got)} / {Mb(tot)} · {Mathf.RoundToInt(pct * 100f)}%";
                float eta = speed > 1000f ? (tot - got) / speed : -1f;
                sub.text = (speed > 1000f ? $"{speed / 1e6f:0.0} MB/s" : "") + (eta >= 0f ? $" · ABOUT {Mathf.CeilToInt(eta)} S LEFT" : "") + "\nDownloads once, then plays offline.";
            }
            else if (m == "failed") { stat.text = "DOWNLOAD DIDN'T FINISH · STOPPED AT " + Mb(got); sub.text = "Check your connection and try again."; }
            else { stat.text = "CAN'T DOWNLOAD " + (lc.name ?? "") + " RIGHT NOW"; sub.text = "Connect to the internet to download it. Lakes you've already downloaded still work, and Ski View is always included."; }
            bar.style.display = m == "offline" ? DisplayStyle.None : DisplayStyle.Flex;
            // rotating tip: every 6 s, random order, no repeats until all have shown; jump tips only on ramp lakes
            if (Time.unscaledTime >= tipAt)
            {
                tipAt = Time.unscaledTime + 6f;
                for (int guard = 0; guard < 30; guard++)
                {
                    if (bag.Count == 0) for (int i = 0; i < TIPS.GetLength(0); i++) bag.Add(i);
                    int k = bag[Random.Range(0, bag.Count)]; bag.Remove(k);
                    if ((k == 5 || k == 7) && !lc.jump) continue;
                    tipIdx = k; break;
                }
                if (tipIdx >= 0) tip.text = "TIP  ·  " + TIPS[tipIdx, Application.isMobilePlatform ? 0 : 1];
            }
        }
    }
}

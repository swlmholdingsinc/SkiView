using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>Pass 19: capture / review parameters for James (audio) and Julia (UI). WebGL reads the page URL query (?a=1&b=2),
    /// players and the editor read "-a 1" command-line pairs. Every key is optional; unknown keys are ignored.
    ///   lake=&lt;id&gt;            start lake (SkiViewBoot)
    ///   cam=high|close|fp|boat  start camera for this run (doesn't touch the saved default)
    ///   mode=slalom|jump        start in jump mode where the lake has a ramp
    ///   at=pass|sink|dws|turn|setup|ramp|flight   jump the run to that moment (sim fast-forwards, no UI clicks needed)
    ///   auto=1                  autopilot steering (hands-free captures)
    ///   rope=0..8               start rope index (15 off .. 43 off)
    ///   speed=0.25..4           sim time scale
    ///   hud=0                   hide every overlay (clean audio/video frames)
    ///   tier=0|1|2              force the PC / WebGL / phone detail tier
    ///   mute=boat,skier,amb,ui,music   comma list of groups to silence;  solo=&lt;group&gt;  only that group
    ///   music=1                 play the menu music loop over the run (level check);  sfx=&lt;id&gt; play one bank sound on start (repeat=s to loop it)
    ///   vol=0..1                master volume for this session
    ///   ui=graphics|audio|controls|credits|license|help|pause|setup|ski|lakes|lakes_downloading|lakes_failed|lakes_offline
    ///   tutorial=0..10          show that tutorial step (tutorial=1 runs the whole flow from the start)
    ///   seed=&lt;int&gt;             Random seed (audio jitter, crane / osprey timers)</summary>
    public static class SkiViewArgs
    {
        static Dictionary<string, string> map;
        static Dictionary<string, string> Map
        {
            get
            {
                if (map != null) return map;
                map = new Dictionary<string, string>();
                string url = Application.absoluteURL ?? ""; int q = url.IndexOf('?');
                if (q >= 0)
                {
                    string qs = url.Substring(q + 1); int h = qs.IndexOf('#'); if (h >= 0) qs = qs.Substring(0, h);
                    foreach (var kv in qs.Split('&')) { if (kv.Length == 0) continue; int e = kv.IndexOf('='); string k = e < 0 ? kv : kv.Substring(0, e), v = e < 0 ? "1" : kv.Substring(e + 1); map[UnEsc(k).ToLowerInvariant()] = UnEsc(v); }
                }
                var a = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < a.Length; i++)
                    if (a[i].StartsWith("-") && a[i].Length > 1 && !a[i].StartsWith("--"))
                    {
                        string k = a[i].Substring(1).ToLowerInvariant(); if (k == "batchmode" || k == "executemethod" || k == "projectpath" || k == "logfile") continue;
                        if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) { if (!map.ContainsKey(k)) map[k] = a[i + 1]; i++; }
                    }
                return map;
            }
        }
        static string UnEsc(string s) { try { return System.Uri.UnescapeDataString(s.Replace('+', ' ')); } catch { return s; } }
        public static string Get(string key, string def = null) => Map.TryGetValue(key, out var v) ? v : def;
        public static bool Has(string key) => Map.ContainsKey(key);
        public static float F(string key, float def) => float.TryParse(Get(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def;
        public static int I(string key, int def) => int.TryParse(Get(key), out var i) ? i : def;
        public static bool On(string key) { var v = Get(key); return v == "1" || v == "true" || v == "on" || v == "yes"; }
        public static bool Listed(string key, string item) { var v = Get(key); if (v == null) return false; foreach (var s in v.Split(',')) if (s.Trim().ToLowerInvariant() == item) return true; return false; }
        /// <summary>For tests and the editor capture scripts.</summary>
        public static void Set(string key, string value) { var m = Map; if (value == null) m.Remove(key); else m[key] = value; }
        public static string Summary() { var l = new List<string>(); foreach (var kv in Map) l.Add(kv.Key + "=" + kv.Value); return string.Join(" ", l); }
    }
}

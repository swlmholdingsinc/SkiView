using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>b10.18 Clinic: CLV2 + tip bank, unlock ladder, lessons 1–7 playable (T16b). Shell over slalom Pass / DeepStart.</summary>
    public static class SkiViewClinic
    {
        public const string PrefPrefix = "sv.clinic.";
        public static readonly string[] Ladder = { "stance", "wide_pull", "gate_first_ball", "preturn", "wake_crossing", "rope_progression" };
        public const float SoftWidthTargetM = 8f; // T16b [DA] until tuning

        [Serializable] public class PendText { public string text; public string status; }
        [Serializable] public class Setup { public string lake; public int ropeIndex; public string ropeLabel; public int boatSpeedMph; }
        [Serializable] public class Criteria { public bool noFall; public float minFractionHandleLow; public float minBuoys; public bool gatesMade; public bool deepWaterStartSuccess; public int maxSlackEvents; public PendText description; }
        [Serializable] public class FocusCue { public string id; public PendText text; }
        [Serializable] public class Feedback { public string cue; public string when; public PendText text; }
        [Serializable] public class Lesson
        {
            public string id; public int order; public bool optional;
            public PendText title, goal; public Setup setup; public Criteria pass_criteria;
            public FocusCue[] focus_cues; public Feedback[] feedback; public string status;
        }
        [Serializable] public class PlanStep { public PendText title, prompt; }
        [Serializable] public class File { public string status; public PlanStep plan_step; public Lesson[] lessons; }

        [Serializable] public class TipLesson { public string id; public string status; public PendText plan_tip, success_note, fail_note; }
        [Serializable] public class TipFile { public string status; public TipLesson[] lessons; }

        static File _data; static TipFile _tips;
        public static File Data { get { Ensure(); return _data; } }
        public static string ActiveLessonId, ActiveCueId;
        public static bool Active, ResultsOpen, LastFell, LastGates, LastComplete;
        public static float LastBuoys, LastHandleLow = -1f;
        public static string WantLake;
        public static bool SkipLakeLoad; // capture / editor smoke: stay on current lake

        public static void Ensure()
        {
            if (_data != null) return;
            var ta = Resources.Load<TextAsset>("Clinic/clinic_lessons_v2");
            if (ta == null) { Debug.LogError("[SkiView] Clinic CLV2 missing"); _data = new File { lessons = Array.Empty<Lesson>() }; }
            else { _data = JsonUtility.FromJson<File>(ta.text); if (_data.lessons == null) _data.lessons = Array.Empty<Lesson>(); }
            var tb = Resources.Load<TextAsset>("Clinic/clinic_tips_v1");
            if (tb != null) { _tips = JsonUtility.FromJson<TipFile>(tb.text); if (_tips.lessons == null) _tips.lessons = Array.Empty<TipLesson>(); }
            else _tips = new TipFile { lessons = Array.Empty<TipLesson>() };
            Debug.Log($"[SkiView] Clinic CLV2 lessons={_data.lessons.Length} tips={_tips.lessons.Length}");
        }

        public static string ModeName() => SkiViewBranding.ClinicModeName;
        public static Lesson Get(string id) { Ensure(); foreach (var L in _data.lessons) if (L.id == id) return L; return null; }
        public static TipLesson GetTip(string id) { Ensure(); foreach (var t in _tips.lessons) if (t.id == id) return t; return null; }
        public static string PlanTip(string id) { var t = GetTip(id); return t?.plan_tip?.text; }

        public static bool IsComplete(string id) => PlayerPrefs.GetInt(PrefPrefix + id + ".done", 0) == 1;
        public static float BestBuoys(string id) => PlayerPrefs.GetFloat(PrefPrefix + id + ".bestBuoys", 0f);
        public static int RopeProgMax() => Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "rope_progression.maxUnlockedIndex", 1), 1, 8);

        public static void MarkComplete(string id, float buoys)
        {
            PlayerPrefs.SetInt(PrefPrefix + id + ".done", 1);
            if (buoys > BestBuoys(id)) PlayerPrefs.SetFloat(PrefPrefix + id + ".bestBuoys", buoys);
            PlayerPrefs.Save();
        }

        public static bool IsUnlocked(string id)
        {
            Ensure();
            if (Get(id) == null) return false;
            if (id == "stance" || id == "deep_water_start") return true; // optional parallel never gates
            int idx = Array.IndexOf(Ladder, id);
            if (idx <= 0) return id == "stance";
            return IsComplete(Ladder[idx - 1]);
        }

        public static bool IsPlayable(string id) => IsUnlocked(id); // b10.18: all unlocked lessons playable

        public static int RemapMph(int clv2Mph) => clv2Mph <= 28 ? 34 : (clv2Mph >= 36 ? 36 : 34);

        public static void BeginLesson(SkiViewGame game, string lessonId, string cueId)
        {
            Ensure();
            var L = Get(lessonId);
            if (L == null || game == null) return;
            Active = true; ResultsOpen = false; ActiveLessonId = lessonId; ActiveCueId = cueId;
            LastFell = false; LastBuoys = 0f; LastHandleLow = -1f; LastGates = false; LastComplete = false;
            if (game.jumpMode) game.ToggleJumpMode();
            if (game.trickMode) game.ToggleTrickMode();
            int mph = RemapMph(L.setup != null ? L.setup.boatSpeedMph : 34);
            int rope = L.setup != null ? Mathf.Clamp(L.setup.ropeIndex, 0, SkierSim.ROPE_M.Length - 1) : 0;
            if (lessonId == "rope_progression")
            {
                rope = RopeProgMax(); // persist unlock; first = 1 (22 OFF)
                mph = 36;
            }
            SkiViewGame.SaveSetup(SkiViewGame.SetupSki, SkiViewGame.SetupBinding, rope, mph);
            game.ApplySetup();
            WantLake = L.setup != null ? L.setup.lake : "saltwhite";
            if (!SkipLakeLoad && !string.IsNullOrEmpty(WantLake) && game.lakeId != WantLake)
            {
                PlayerPrefs.SetString(PrefPrefix + "pending.lesson", lessonId);
                PlayerPrefs.SetString(PrefPrefix + "pending.cue", cueId);
                PlayerPrefs.Save();
                string key = SkiViewBoot.SceneKey(WantLake);
                Debug.Log($"[SkiView] Clinic load lake={WantLake} key={key}");
                if (!SkiViewBoot.Load(key))
                    Debug.LogWarning($"[SkiView] Clinic lake load busy — stay on {game.lakeId}");
                else return; // resume after load via TryResumePending
            }
            StartOnLake(game, L, cueId, rope, mph);
        }

        static void StartOnLake(SkiViewGame game, Lesson L, string cueId, int rope, int mph)
        {
            string cueTxt = "";
            if (L.focus_cues != null)
                foreach (var c in L.focus_cues) if (c.id == cueId && c.text != null) { cueTxt = c.text.text; break; }
            game.banner = $"{ModeName().ToUpperInvariant()} · {L.title?.text ?? L.id} · {SkierSim.ROPE_LABEL[rope]} · {mph} MPH";
            if (mph == 34 && L.setup != null && L.setup.boatSpeedMph == 28) game.banner += " · (28→34)";
            if (!string.IsNullOrEmpty(WantLake) && game.lakeId != WantLake) game.banner += " · want " + WantLake;
            game.bannerT = 3.5f;
            SkiAudio.Instance?.ClinicLessonStart();
            if (!string.IsNullOrEmpty(cueTxt)) { game.SetCoach(cueTxt, 6f); SkiAudio.Instance?.ClinicTip(); }
            if (L.id == "deep_water_start") game.BeginClinicDeepStart();
            Debug.Log($"[SkiView] Clinic begin lesson={L.id} cue={cueId} mph={mph} rope={rope} lake={game.lakeId}");
        }

        public static void TryResumePending(SkiViewGame game)
        {
            if (game == null) return;
            string lid = PlayerPrefs.GetString(PrefPrefix + "pending.lesson", "");
            string cue = PlayerPrefs.GetString(PrefPrefix + "pending.cue", "");
            if (string.IsNullOrEmpty(lid)) return;
            PlayerPrefs.DeleteKey(PrefPrefix + "pending.lesson");
            PlayerPrefs.DeleteKey(PrefPrefix + "pending.cue");
            PlayerPrefs.Save();
            Ensure();
            var L = Get(lid);
            if (L == null) return;
            Active = true; ActiveLessonId = lid; ActiveCueId = cue; ResultsOpen = false;
            int mph = RemapMph(L.setup != null ? L.setup.boatSpeedMph : 34);
            int rope = lid == "rope_progression" ? RopeProgMax() : (L.setup != null ? L.setup.ropeIndex : 0);
            if (lid == "rope_progression") mph = 36;
            SkiViewGame.SaveSetup(SkiViewGame.SetupSki, SkiViewGame.SetupBinding, rope, mph);
            game.ApplySetup();
            WantLake = L.setup != null ? L.setup.lake : "";
            StartOnLake(game, L, cue, rope, mph);
        }

        public static void NotifyDeepStartOutcome(SkiViewGame game, bool success)
        {
            if (!Active || ActiveLessonId != "deep_water_start" || ResultsOpen) return;
            LastFell = !success; LastBuoys = 0f; LastGates = false;
            LastComplete = success;
            if (success) MarkComplete("deep_water_start", 0f);
            if (success) SkiAudio.Instance?.ClinicPass(); else SkiAudio.Instance?.ClinicFail();
            ResultsOpen = true;
            Debug.Log($"[SkiView] Clinic DWS success={success}");
        }

        public static void EndPass(SkiViewGame game, bool fell, float buoys, bool gatesCleared = false)
        {
            if (!Active) return;
            if (ActiveLessonId == "deep_water_start") return; // handled by NotifyDeepStartOutcome
            LastFell = fell; LastBuoys = buoys;
            LastGates = gatesCleared || (game != null && game.sim != null && (game.sim.last.exitGate || game.sim.last.clean));
            if (!LastGates && buoys >= 1f) LastGates = true; // buoy1 implies gates in our course
            bool ok = EvaluateComplete(ActiveLessonId, fell, buoys, LastGates, game);
            LastComplete = ok;
            if (ok)
            {
                MarkComplete(ActiveLessonId, buoys);
                if (ActiveLessonId == "rope_progression" && buoys >= 5.99f)
                {
                    int cur = game != null && game.sim != null ? game.sim.ropeIndex : RopeProgMax();
                    int next = Mathf.Min(cur + 1, 8);
                    if (next > RopeProgMax()) { PlayerPrefs.SetInt(PrefPrefix + "rope_progression.maxUnlockedIndex", next); PlayerPrefs.Save(); }
                }
                SkiAudio.Instance?.ClinicPass();
            }
            else SkiAudio.Instance?.ClinicFail();
            ResultsOpen = true;
            Debug.Log($"[SkiView] Clinic end lesson={ActiveLessonId} fell={fell} buoys={buoys:0.#} gates={LastGates} complete={ok}");
        }

        /// <summary>T16b [DA] hard criteria by lesson id. Soft/unrecorded never fails.</summary>
        public static bool EvaluateComplete(string id, bool fell, float buoys, bool gates, SkiViewGame game)
        {
            if (fell) return false;
            switch (id)
            {
                case "stance": return true; // noFall hard (already !fell)
                case "wide_pull": return buoys >= 4f;
                case "gate_first_ball": return gates && buoys >= 1f;
                case "preturn": return buoys >= 5f; // turn timing soft/unrecorded
                case "wake_crossing": return true; // noFall; slack soft/unrecorded
                case "rope_progression": return buoys >= 5.99f; // full 6
                case "deep_water_start": return false; // via NotifyDeepStartOutcome
                default: return !fell;
            }
        }

        public static string DataReadout(SkiViewGame game)
        {
            string hl = LastHandleLow < 0f ? "— (not recorded yet)" : $"{LastHandleLow * 100f:0}%";
            string lake = game != null ? game.lakeName : "?";
            string want = !string.IsNullOrEmpty(WantLake) ? WantLake : "—";
            string width = "— (not recorded yet)";
            string slack = "— (not recorded yet)";
            string turn = "— (not recorded yet)";
            string rope = game?.sim != null ? SkierSim.ROPE_LABEL[game.sim.ropeIndex] : "—";
            switch (ActiveLessonId)
            {
                case "stance":
                    return $"Fell: {(LastFell ? "yes" : "no")}\nBuoys: {LastBuoys:0.#}\nHandle low: {hl}\nLake: {lake} (want {want})";
                case "wide_pull":
                    return $"Fell: {(LastFell ? "yes" : "no")}\nBuoys: {LastBuoys:0.#}\nWidth: {width}\nLake: {lake}";
                case "gate_first_ball":
                    return $"Gates: {(LastGates ? "made" : "missed")}\nBuoy 1: {(LastBuoys >= 1f ? "cleared" : "no")}\nBuoys: {LastBuoys:0.#}\nFell: {(LastFell ? "yes" : "no")}\nTurn-in offset: {turn}";
                case "preturn":
                    return $"Buoys: {LastBuoys:0.#}\nFell: {(LastFell ? "yes" : "no")}\nTurn timing: {turn}\nSlack: {slack}";
                case "wake_crossing":
                    return $"Fell: {(LastFell ? "yes" : "no")}\nBuoys: {LastBuoys:0.#}\nSlack: {slack}";
                case "rope_progression":
                    return $"Buoys: {LastBuoys:0.#}\nFell: {(LastFell ? "yes" : "no")}\nRope: {rope}\nNext unlocked: {SkierSim.ROPE_LABEL[Mathf.Clamp(RopeProgMax(), 0, SkierSim.ROPE_LABEL.Length - 1)]}\nWidth: {width}";
                case "deep_water_start":
                    return $"Deep-water start: {(LastComplete ? "success" : (LastFell ? "fall" : "not recorded yet"))}\nStable stance: — (not recorded yet)\nLake: {lake}";
                default:
                    return $"Fell: {(LastFell ? "yes" : "no")}\nBuoys: {LastBuoys:0.#}";
            }
        }

        public static List<string> FeedbackLines()
        {
            var lines = new List<string>();
            var tip = GetTip(ActiveLessonId);
            if (LastComplete && tip?.success_note != null && !string.IsNullOrEmpty(tip.success_note.text))
                lines.Add(tip.success_note.text);
            if (!LastComplete && tip?.fail_note != null && !string.IsNullOrEmpty(tip.fail_note.text))
                lines.Add(tip.fail_note.text);

            var L = Get(ActiveLessonId);
            if (L?.feedback != null)
            {
                foreach (var f in L.feedback)
                {
                    if (f?.text == null) continue;
                    string w = f.when ?? "";
                    bool match = false;
                    if (w.Contains("fell==true")) match = LastFell;
                    else if (w.Contains("fell==false")) match = !LastFell;
                    else if (w.Contains("handle_low_fraction>=")) match = LastHandleLow < 0f || LastHandleLow >= 0.7f;
                    else if (w.Contains("handle_low_fraction<")) match = LastHandleLow >= 0f && LastHandleLow < 0.7f;
                    else if (w.Contains("buoys_cleared>="))
                    {
                        float need = 6f;
                        int ge = w.IndexOf(">="); if (ge >= 0) float.TryParse(w.Substring(ge + 2).Trim().TrimEnd(')'), out need);
                        match = LastBuoys + 1e-3f >= need;
                    }
                    else if (w.Contains("buoys_cleared<"))
                    {
                        float need = 6f;
                        int lt = w.IndexOf('<'); if (lt >= 0) float.TryParse(w.Substring(lt + 1).Trim().TrimEnd(')'), out need);
                        match = LastBuoys < need;
                    }
                    else if (w.Contains("gates_made==true")) match = LastGates;
                    else if (w.Contains("gates_made==false")) match = !LastGates;
                    else if (w.Contains("buoy1_cleared==true")) match = LastBuoys >= 1f;
                    else if (w.Contains("deep_water_start_success==true")) match = LastComplete;
                    else if (w.Contains("deep_water_start_success==false")) match = !LastComplete;
                    else if (w.Contains("avg_max_lateral") || w.Contains("avg_turn_offset") || w.Contains("slack_events") || w.Contains("lean_"))
                        continue; // soft unrecorded — skip inventing
                    else match = f.cue == ActiveCueId || f.cue == "*";

                    if (f.cue != "*" && f.cue != ActiveCueId && !(w.Contains("fell") || w.Contains("buoys") || w.Contains("gates") || w.Contains("deep_water")))
                        continue;
                    if (match && !lines.Contains(f.text.text)) lines.Add(f.text.text);
                }
            }
            if (lines.Count == 0)
                lines.Add("Pass complete. Keep the same focus next run.");
            return lines;
        }

        public static void ClearActive() { Active = false; ResultsOpen = false; ActiveLessonId = null; ActiveCueId = null; WantLake = null; }
        public static void CloseResults() { ResultsOpen = false; }
    }
}

using System.Collections;
using UnityEngine;

namespace SkiView
{
    /// <summary>Pass 19: SkiViewArgs (URL / command-line) applied to a run - camera, mode, rope, autopilot, sim speed, detail tier,
    /// HUD off and "at=" fast-forward to a moment of the run, for James's audio captures and Julia's UI reviews.</summary>
    public partial class SkiViewGame
    {
        /// <summary>Call-plate text for the kit HUD (tutorial-script.md wording): turnaround calls, jump prompts, fall messages.</summary>
        public string HudCall
        {
            get
            {
                if (mode == Mode.Jump)
                {
                    if (jphase == JPhase.Approach && !cutOn) return runT > -4.6f ? "CUT NOW" : "WAIT WIDE";
                    if (jphase == JPhase.Ramp && !popped) return "POP";
                    return "";
                }
                string setUp = "SET UP · " + (jumpMode ? "JUMP" : SkierSim.ROPE_LABEL[sim.ropeIndex]);
                if (spinCall && turnaround)
                {   // pass 19 spin turn: SPIN TURN while the boat pivots, then SET UP · <next rope> OFF
                    if (mode == Mode.Sink) return spinSeen && !path.InSpin(boatS) ? setUp : "SPIN TURN";
                    return mode == Mode.Turn && passes > 0 && sim.ropeM <= ropeTarget + 0.01f ? setUp : mode == Mode.DeepStart ? "DEEP-WATER START" : "SPIN TURN";
                }
                if (mode == Mode.Turn && turnaround && passes > 0 && path.spins != null && path.SpinAhead(boatS, 220f)) return "SPIN TURN";
                if (DropPullingOut) return "PULL OUT WIDE";
                if (dropPh >= 2 && mode == Mode.Sink) return RopeChoiceOpen ? (CanShorten ? "SAME LENGTH OR SHORTEN?" : "SAME LENGTH") : dropPh >= 3 ? "GRAB THE HANDLE" : "LET GO · BOAT CIRCLING BACK";
                if (mode == Mode.Sink) return "SETTLING IN";
                if (mode == Mode.DeepStart) return "DEEP-WATER START";
                if (mode == Mode.Fallen) return string.IsNullOrEmpty(sim.message) ? "FALL" : sim.message.ToUpperInvariant();
                if (mode == Mode.Turn && passes > 0) return sim.ropeM > ropeTarget + 0.01f || !turnaroundSetUp ? "AROUND THE ISLAND" : "SET UP · " + (jumpMode ? "JUMP" : SkierSim.ROPE_LABEL[sim.ropeIndex]);
                return "";
            }
        }
        bool turnaroundSetUp => !turnaround || (path.Progress(boatS) > -250f && path.Progress(boatS) < 0f);   // b9.1: towed turn -> SET UP on the run-in
        public bool InTurnaround => turnaround && (mode == Mode.Sink || mode == Mode.DeepStart || mode == Mode.Turn);
        public bool CutOn => cutOn; public bool Popped => popped;
        public static bool HudHidden => SkiViewArgs.Get("hud") == "0";
        bool argsApplied, argsHeld;
        /// <summary>Apply URL / command-line args that were held until the player entered a name.</summary>
        public void ReleaseHeldArgs()
        {
            if (!argsHeld) return;
            argsHeld = false;
            ApplyArgs();
        }
        void ApplyArgs()
        {
            if (argsApplied || !Application.isPlaying) return;
            if (SkiViewPlayer.BlocksRun) { argsHeld = true; return; }
            argsApplied = true;
            string c = SkiViewArgs.Get("cam");
            if (c != null) { var m = c == "boat" ? CamMode.Broadcast : c == "fp" || c == "first" || c == "1st" ? CamMode.FirstPerson : c == "close" ? CamMode.CloseChase : CamMode.SkierChase; SetCamera(m); camModeLast = m; SkiView.UI.SkiViewUI.Instance?.ShowCamToast(m); }   // Phase 2 fix: the run-start toast ran before args, so the pill kept the old view name
            if (SkiViewArgs.Get("mode") == "jump" && jumpRamp != null && !jumpMode) ToggleJumpMode();
            int r = SkiViewArgs.I("rope", -1);
            if (r >= 0 && !jumpMode) { sim.ropeIndex = Mathf.Clamp(r, 0, SkierSim.ROPE_M.Length - 1); sim.ropeM = SkierSim.ROPE_M[sim.ropeIndex]; ropeTarget = sim.ropeM; }
            if (SkiViewArgs.On("auto")) autopilot = true;
            if (SkiViewArgs.On("gates")) BuildGateDebug();
            float sp = SkiViewArgs.F("speed", 1f); if (sp != 1f) timeScale = Mathf.Clamp(sp, 0.25f, 4f);
            int tier = SkiViewArgs.I("tier", -1);
            if (tier >= 0) { var det = FindFirstObjectByType<LakeDetail>(); if (det != null) { det.forceTier = tier; det.Apply(tier); } }
            if (SkiViewArgs.Has("seed")) Random.InitState(SkiViewArgs.I("seed", 0));
            if (SkiViewArgs.Has("vol")) AudioListener.volume = Mathf.Clamp01(SkiViewArgs.F("vol", 1f));
            string at = SkiViewArgs.Get("at"); if (!string.IsNullOrEmpty(at)) StartCoroutine(FastForward(at));
        }

        /// <summary>Run the sim (autopilot on, no rendering cost beyond a frame per 10 sim seconds) until the named moment.</summary>
        IEnumerator FastForward(string at)
        {
            yield return null;
            bool auto0 = autopilot; autopilot = true;
            if ((at == "ramp" || at == "flight") && jumpRamp != null && !jumpMode) ToggleJumpMode();
            System.Func<bool> done = at switch
            {
                "pass" => () => mode == Mode.Pass,
                "sink" => () => mode == Mode.Sink,
                "dws" => () => mode == Mode.DeepStart && passes > 0,
                "turn" => () => mode == Mode.Turn && passes > 0,
                "setup" => () => mode == Mode.Sink,
                "choice" => () => RopeChoiceOpen,
                "ramp" => () => jphase == JPhase.Ramp,
                "flight" => () => jphase == JPhase.Flight,
                _ => () => true
            };
            float simmed = 0f;
            while (!done() && simmed < 400f)
            {
                for (int i = 0; i < 600 && !done(); i++) { FixedStep(FIXED); simmed += FIXED; }
                ApplyVisuals(true); yield return null;
            }
            if (at == "setup" && mode == Mode.Sink) SkipToSetup();
            autopilot = auto0 || SkiViewArgs.On("auto");
            Debug.Log($"[SkiView] args: at={at} reached={done()} after {simmed:0} s sim ({SkiViewArgs.Summary()})");
        }
    }
}

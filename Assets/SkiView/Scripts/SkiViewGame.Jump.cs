using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>Pass 13: first working tournament jump (roadmap/JUMP-TRICKS-SPEC.md section 1; AWSA 2026 rule 9, IWWF 10.02).
    /// Two jump skis, helmet, 23 m rope, boat at 54 km/h in the jump lane 17 m from the ramp centreline.
    /// The approach is a scripted rope pendulum (pull out wide on the ramp side, counter-cut across both wakes, turn on the far
    /// side, then a progressive double-wake cut to the ramp), so the speed at the ramp emerges from the rope geometry
    /// (~1.45x boat speed, ~15 deg across the ramp axis). Then: ramp contact (gravity + waxed friction along the 14.8 deg surface,
    /// leg spring compressed at the toe, pop in the last third), flight (point mass with posture-driven lift and drag and a
    /// slack-capable rope, spec 1.4.3), touchdown (landing window: ski pitch) and ride-out, or an out-the-front crash.</summary>
    public partial class SkiViewGame
    {
        public enum JPhase { None, Approach, Ramp, Flight, Landing, RideOut }
        [Header("Jump (pass 13)")]
        public bool jumpMode;
        public bool jumpOnly;   // pass 18: jump-mode-only lake (Heron Marsh): always in jump mode, no slalom
        public bool rampJumpModeOnly;   // T26: the ramp is only on the water in jump mode (Gulfwind: north side of the course B bulge)
        public Vector2[] playBox;   // pass 19 (Marrowind): 4 corners of the playable water box; soft wall outside it
        bool boxWarned;
        /// <summary>Metres the point lies outside the convex play box (0 inside).</summary>
        public float OutsidePlayBox(Vector3 p)
        {
            if (playBox == null || playBox.Length < 3) return 0f;
            Vector2 c = Vector2.zero; foreach (var q in playBox) c += q; c /= playBox.Length;
            float o = 0f; Vector2 pp = new Vector2(p.x, p.z);
            for (int i = 0; i < playBox.Length; i++)
            {
                Vector2 a = playBox[i], b = playBox[(i + 1) % playBox.Length], e = (b - a).normalized, n = new Vector2(e.y, -e.x);
                if (Vector2.Dot(c - a, n) > 0) n = -n;          // outward normal
                o = Mathf.Max(o, Vector2.Dot(pp - a, n));
            }
            return o;
        }
        /// <summary>Soft wall: past the box edge the skier's swing is damped back toward the boat line (no hard stop), with a banner.</summary>
        void PlayBoxWall(float dt)
        {
            float o = OutsidePlayBox(skierPos);
            if (o <= 0f) { boxWarned = false; return; }
            float k = Mathf.Min(o, 12f) * 0.35f;
            sim.theta *= Mathf.Exp(-k * dt); sim.omega *= Mathf.Exp(-k * 1.5f * dt);
            if (!boxWarned) { banner = "EDGE OF THE SKI AREA"; bannerT = 1.6f; boxWarned = true; }
        }
        public bool jumpCrashNext;                  // capture / debug: tips down at touchdown -> out-the-front fall
        public Transform jumpSkiL, jumpSkiR;        // two jump skis (origin at the tail, +Z to the tip)
        public GameObject helmet;
        public SkiSpray jumpSprayL, jumpSprayR;
        public float jumpSkiAnkleZ = 0.95f, jumpStanceHalf = 0.16f;
        public float jumpRopeM = 23f, jumpBoatKmh = 54f, jumpPopTau = 0.82f, jumpPopDv = 0.6f, jumpClA = 0.42f, jumpCdA = 0.27f;
        public JPhase jphase = JPhase.None;
        // pass 14: player control. CUT = tap (Space / click / touch) on the far side starts the progressive cut to the ramp: a later,
        // harder cut means more speed and a steeper crossing angle; no cut by -2.6 s = missed the ramp. POP = tap on the ramp:
        // best in the last ~third (0.78-0.95), early = tips high (sat back), late = tips low (out the front).
        public float jumpCutAt = float.NaN, jumpPopAt = float.NaN;   // capture / debug: run time of the cut press, ramp fraction of the pop press
        public bool jumpCam = true; public static bool jumpDebug = false;                                  // shore / side camera for the ramp, flight and ride-out
        public float JumpCutT { get; private set; } = float.NaN;
        public float JumpPopT { get; private set; } = -1f;
        public float JumpCrossDeg { get; private set; }
        public float JumpToeSpeed { get; private set; }
        public float JumpLandLat { get; private set; }
        public string JumpResult { get; private set; } = "";
        bool jumpPress, cutOn, jMissed; Vector3 cutA, cutB; float landPitchWant = 7f, popDv;
        public const float CUT_OPEN = -6.4f, CUT_LATE = -2.6f, AUTO_CUT = -4.3f, AUTO_POP = 0.86f;
        public float JumpRampTau => rampLenOut > 0f ? Mathf.Clamp01(rampS / rampLenOut) : 0f;   // T26: timing bar
        /// <summary>Jump button (Space, tap / click below the HUD strip): the cut on the approach, the pop on the ramp.</summary>
        public void JumpPress() { if (mode == Mode.Jump) jumpPress = true; }
        /// <summary>Jump mode, press before the approach has started: cut at the ideal moment of the coming run.</summary>
        public void JumpArm() { if (jumpMode) cutArmed = true; }
        bool cutArmed; const float ASSIST_CUT = -3.9f;
        public float JumpT => jT;
        public float JumpRunT => runT;
        public float JumpDistance { get; private set; }
        public float JumpLipSpeed { get; private set; }
        public float JumpAirTime { get; private set; }
        public float JumpApex { get; private set; }
        public Vector3 JumpTouchdown { get; private set; }
        public bool JumpGearOn { get; private set; }
        /// <summary>T15 Phase A: 3-pass round; longest scored counts.</summary>
        public const int JUMP_PASSES = 3;
        public struct JumpPassRec { public string kind; public float dist; } // SCORED | BALK | FALL
        readonly JumpPassRec[] jumpRound = new JumpPassRec[JUMP_PASSES];
        public int JumpPassCount { get; private set; }   // registered 0..3
        public float JumpBest { get; private set; }
        public bool JumpResultsOpen { get; private set; }
        public IReadOnlyList<JumpPassRec> JumpRoundLog => jumpRound;
        public float RampProgress => rampLenOut > 0f ? rampS / rampLenOut : 0f;
        public Vector3 JumpSide => jSide;
        public Vector3 JumpVel => jVel;

        const float KM = 82f, RHO = 1.2f, MU = 0.03f, ROPE_K = 4000f, ARM = 0.6f;
        // approach keys: (t relative to the ramp toe [s], rope angle toward the ramp side [deg], rate [deg/s]); key 0 = current state
        static readonly Vector3[] JKEYS = { new Vector3(-11f, 0f, 0f), new Vector3(-7.8f, 36f, 0f), new Vector3(-5.9f, 5f, -22f), new Vector3(-4.3f, -34f, 0f), new Vector3(0f, 42f, 14f) };
        Vector3[] jkeys;
        float jT, runT, jR, popDvApplied, jPost, rampS, rampV, rampLat, rampLatV, landT, skiPitchFly, flyMaxY;
        bool popped, jCrash;
        Vector3 jPos, jVel, jSide, jLip, jAxis, jRight, jToe, jFwd;
        float boatLatOff, boatLatTarget, jT0Along;
        float jumpCrouch = -1f, jumpPitch = 0f, jumpHandleIn = 0f, jumpTip = 4f, jumpVYaw = 0f, jumpSink = 0.03f, jumpVisualTip = 4f;
        SkierPoses PoseTbl => rig != null && rig.poses != null ? rig.poses : new SkierPoses();

        Vector3 SideOfBoat() { Vector3 r = Vector3.Cross(Vector3.up, boatFwd).normalized; return Vector3.Dot(r, jSide) >= 0f ? r : -r; }
        float RampSinA => Mathf.Clamp(RampRatio, 0.05f, 0.5f);

        public void SetJumpGear(bool on)
        {
            JumpGearOn = on;
            if (on && trickMode) { trickMode = false; SetTrickGear(false); }
            if (ski != null) ski.gameObject.SetActive(!on);
            if (jumpSkiL != null) jumpSkiL.gameObject.SetActive(on);
            if (jumpSkiR != null) jumpSkiR.gameObject.SetActive(on);
            if (helmet != null) helmet.SetActive(on);
            if (jumpCourse != null) jumpCourse.SetActive(on);
            if (rampJumpModeOnly && jumpRamp != null) jumpRamp.gameObject.SetActive(on);   // T26
            if (rig != null) { rig.jumpOn = on; rig.jumpBootL = on && jumpSkiL != null ? jumpSkiL.Find("Boot") : null; rig.jumpBootR = on && jumpSkiR != null ? jumpSkiR.Find("Boot") : null; }
            ropeTarget = on ? jumpRopeM : SkierSim.ROPE_M[sim.ropeIndex];
            SkiAudio.Instance?.TournamentJumpAmbience(on);   // James Jump Phase A stub
        }
        static float RopeHorizFor(float ropeM) { float dy = SkierSim.PYLON_Y - SkierSim.HANDLE_Y; return Mathf.Sqrt(ropeM * ropeM - dy * dy); }
        static float RopeMForRadius(float radius) { float h = radius - SkierSim.ARM_REACH, dy = SkierSim.PYLON_Y - SkierSim.HANDLE_Y; return Mathf.Sqrt(h * h + dy * dy); }

        void JumpGeometry()
        {
            if (jumpRamp == null) return;
            jFwd = path.CourseFwd(jumpDir);
            jLip = jumpRamp.position; jLip.y = 0f;
            Vector3 lat = jLip - jFwd * Vector3.Dot(jLip, jFwd); jSide = lat.normalized;
            jAxis = jumpRamp.forward; jAxis.y = 0f; jAxis.Normalize(); jRight = Vector3.Cross(Vector3.up, jAxis);
            float cosA = Mathf.Sqrt(1f - RampSinA * RampSinA);
            jToe = jLip - jAxis * (rampLenOut * cosA);
            // jump lane: X axis 15 m from the ramp centreline (toward the course), boat at y = -2 -> 17 m from the ramp
            boatLatTarget = lat.magnitude - 17f;
            jR = RopeHorizFor(jumpRopeM) + SkierSim.ARM_REACH;   // 23 m line: skier ~23.9 m from the pylon
        }

        public void ResetJumpRound()
        {
            JumpPassCount = 0; JumpBest = 0f; JumpResultsOpen = false;
            for (int i = 0; i < JUMP_PASSES; i++) jumpRound[i] = default;
        }

        public void CloseJumpResults() { JumpResultsOpen = false; }

        /// <summary>T15/T31: start a fresh 3-pass jump round from current setup.</summary>
        public void BeginJumpRound()
        {
            if (!inited) Init();
            if (!jumpMode) { jumpMode = true; SetJumpGear(true); }
            jumpBoatKmh = JUMP_KMH[SetupJumpKmhIdx];
            rampHeight = JUMP_RAMP_M[SetupJumpRampIdx];
            jumpRopeM = 23f;
            ResetJumpRound();
            JumpGeometry();
            if (sim != null) { sim.ropeM = jumpRopeM; ropeTarget = jumpRopeM; }
            ApplySki(SetupSki);
            banner = $"JUMP · 3 PASSES · {rampHeight:0.00} m · {SkiView.UI.SkiViewUI.SpeedKmh(jumpBoatKmh)} · 23 m"; bannerT = 3f;
        }

        void RegisterJumpPass(string kind, float dist)
        {
            if (JumpPassCount >= JUMP_PASSES) return;
            jumpRound[JumpPassCount] = new JumpPassRec { kind = kind, dist = dist };
            if (kind == "SCORED" && dist > JumpBest) JumpBest = dist;
            JumpPassCount++;
            passes = JumpPassCount;   // HUD reuse
            if (JumpPassCount >= JUMP_PASSES)
            {
                JumpResultsOpen = true;
                NoteJumpRecord();
                banner = SkiView.UI.SkiViewUI.Kmh
                    ? $"ROUND · BEST {JumpBest:0.0} m"
                    : $"ROUND · BEST {Mathf.FloorToInt(JumpBest / 0.3048f + 0.5f)} ft";
                bannerT = 4f;
            }
        }

        public string JumpResultsSummary()
        {
            var sb = new System.Text.StringBuilder();
            bool kmh = SkiView.UI.SkiViewUI.Kmh;
            for (int i = 0; i < JumpPassCount; i++)
            {
                var r = jumpRound[i];
                bool best = r.kind == "SCORED" && r.dist >= JumpBest - 0.01f && JumpBest > 0f;
                string line;
                if (r.kind == "BALK") line = $"Pass {i + 1}: — Balk";
                else if (r.kind == "FALL") line = $"Pass {i + 1}: — Fall";
                else
                {
                    int ft = Mathf.FloorToInt(r.dist / 0.3048f + 0.5f);
                    line = kmh
                        ? $"Pass {i + 1}: {r.dist:0.0} m / {ft} ft"
                        : $"Pass {i + 1}: {ft} ft / {r.dist:0.0} m";
                    if (best) line += "  Best";
                }
                if (i > 0) sb.Append('\n');
                sb.Append(line);
            }
            return sb.ToString();
        }

        public string JumpDistFmt(float m)
        {
            int ft = Mathf.FloorToInt(m / 0.3048f + 0.5f);
            return SkiView.UI.SkiViewUI.Kmh ? $"{m:0.0} m · {ft} ft" : $"{ft} ft · {m:0.0} m";
        }

        /// <summary>Capture / debug: put the boat on the southbound line 11 s before the ramp and start a jump run now.</summary>
        public void BeginJumpRun(bool crash = false)
        {
            if (!inited) Init();
            SetJumpGear(true);
            jumpMode = true; jumpCrashNext = crash;
            JumpGeometry();
            float vb = jumpBoatKmh / 3.6f;
            float startAlong = PlanT0Along() - vb * (-JKEYS[0].x);
            boatS = path.FindS(jumpDir, startAlong);
            boatLatOff = boatLatTarget;
            boatPos = path.Position(boatS) + jSide * boatLatOff; boatFwd = path.Tangent(boatS); prevHeading = Heading(boatFwd);
            sim.ropeM = jumpRopeM; sim.theta = 0f; sim.omega = 0f; throttle = 1f; ropeTarget = sim.ropeM;
            mode = Mode.Turn; PlaceSkier(); skierPrev = skierPos;
            StartApproach();
        }

        float PlanT0Along()
        {
            // boat along-course position when the skier reaches the toe at the planned entry (1 m to the course side of centre)
            Vector3 entry = jToe - jSide * 0.5f;   // pass 14: aimed 0.5 m to the course side; the real path lands ~1 m further out (+1.5 m), the diagonal carries him to the middle
            float latEntry = Vector3.Dot(entry - jFwd * Vector3.Dot(entry, jFwd), jSide) - boatLatTarget;
            float phiToe = Mathf.Asin(Mathf.Clamp(latEntry / jR, -0.95f, 0.95f));
            JKEYS[4].y = phiToe * Mathf.Rad2Deg;
            return Vector3.Dot(entry, jFwd) + PYLON_BACK + jR * Mathf.Cos(phiToe);
        }

        void StartApproach()
        {
            mode = Mode.Jump; jphase = JPhase.Approach; jT = 0f; popped = false; jCrash = jumpCrashNext; JumpDistance = 0f; JumpAirTime = 0f; JumpApex = 0f;
            bool armed = cutArmed; jumpPress = false; cutOn = false; jMissed = false; cutArmed = armed; skipTurn = false; JumpCutT = float.NaN; JumpPopT = -1f; popDv = 0f; landPitchWant = 7f; JumpResult = ""; jcActive = false;
            jT0Along = PlanT0Along();
            // key 0 = where the skier is now on the rope
            Vector3 rel = skierPos - Pylon(); rel.y = 0f; Vector3 sd = SideOfBoat();
            float phi0 = Mathf.Asin(Mathf.Clamp(Vector3.Dot(rel, sd) / Mathf.Max(1f, rel.magnitude), -0.95f, 0.95f)) * Mathf.Rad2Deg;
            jkeys = (Vector3[])JKEYS.Clone();
            jkeys[0] = new Vector3(Mathf.Min(RunTime(), -8.6f), phi0, 0f);
            banner = $"JUMP · {RampRatio:0.000} RAMP · {rampHeight:0.00} m · {SkiView.UI.SkiViewUI.SpeedKmh(jumpBoatKmh)} · 23 m ROPE"; bannerT = 3f;
        }

        float RunTime() => (Vector3.Dot(boatPos, jFwd) - jT0Along) / (jumpBoatKmh / 3.6f);

        static float Hermite(Vector3 a, Vector3 b, float t, out float rate)
        {
            float T = Mathf.Max(0.01f, b.x - a.x), u = Mathf.Clamp01((t - a.x) / T);
            float u2 = u * u, u3 = u2 * u;
            float h00 = 2 * u3 - 3 * u2 + 1, h10 = u3 - 2 * u2 + u, h01 = -2 * u3 + 3 * u2, h11 = u3 - u2;
            float p = h00 * a.y + h10 * T * a.z + h01 * b.y + h11 * T * b.z;
            float d00 = 6 * u2 - 6 * u, d10 = 3 * u2 - 4 * u + 1, d01 = -6 * u2 + 6 * u, d11 = 3 * u2 - 2 * u;
            rate = (d00 * a.y + d10 * T * a.z + d01 * b.y + d11 * T * b.z) / T;
            return p;
        }
        float Phi(float t, out float rate)
        {
            if (cutOn && t >= cutB.x) { rate = cutB.z; return cutB.y + rate * (t - cutB.x); }   // past the toe key: keep the end rate (the step that crosses the toe)
            if (cutOn && t >= cutA.x) return Hermite(cutA, cutB, t, out rate);
            // before the cut: pull out, counter-cut, turn on the far side (key 3) and hold there until the player cuts
            for (int i = 0; i < 3; i++) if (t <= jkeys[i + 1].x) return Hermite(jkeys[i], jkeys[i + 1], t, out rate);
            rate = 0f; return jkeys[3].y;
        }

        /// <summary>The cut: from the current rope angle and rate to the toe angle at t = 0. The end rate (how hard he is still
        /// accelerating through the wakes at the toe) follows the average rate needed, so a late cut is a harder, faster, steeper one.</summary>
        void BeginCut(float t)
        {
            float phi = Phi(t, out float rate); float toe = jkeys[4].y;
            float avg = (toe - phi) / Mathf.Max(0.5f, -t);
            float endRate = Mathf.Clamp(0.8f * avg, 8f, 19f);
            cutA = new Vector3(t, phi, rate); cutB = new Vector3(0f, toe, endRate); cutOn = true; JumpCutT = t;
            Debug.Log($"[SkiView] jump: CUT at {t:0.00} s (rope angle {phi:0} deg, rate {rate:0}) -> toe {toe:0} deg at {endRate:0} deg/s");
        }

        /// <summary>Pop timing (ramp fraction at the press; &lt; 0 = no pop) -> extra vertical speed and the landing ski pitch.</summary>
        void PopEffect(float tau, out float dv, out float pitch)
        {
            if (tau < 0f) { dv = 0f; pitch = 5f; return; }                 // rode off without a pop: short but safe
            if (tau > 1f) { dv = 0.15f; pitch = -1f; return; }              // popped after the lip: thrown over the front
            if (tau < 0.6f) { dv = 0.15f; pitch = 7f + (0.78f - tau) * 50f; return; }   // far too early: tips high, sits back
            if (tau < 0.78f) { dv = jumpPopDv * Mathf.Lerp(0.35f, 0.9f, (tau - 0.6f) / 0.18f); pitch = 7f + (0.78f - tau) * 30f; return; }
            if (tau <= 0.95f) { dv = jumpPopDv * (1f - Mathf.Abs(tau - 0.86f) * 1.5f); pitch = 7f; return; }
            dv = jumpPopDv * 0.8f; pitch = 7f - (tau - 0.95f) * 130f;        // late: tips go down
        }

        void UpdateHeadRate(float dt)
        {
            Vector3 vh = skierVel; vh.y = 0f;
            if (vh.magnitude > 2f)
            {
                float hd = Mathf.Atan2(vh.x, vh.z);
                if (!float.IsNaN(headPrev)) { float d = Mathf.DeltaAngle(headPrev * Mathf.Rad2Deg, hd * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt; headRate = Mathf.Lerp(headRate, Mathf.Clamp(d, -4f, 4f), 1f - Mathf.Exp(-dt / 0.12f)); }
                headPrev = hd;
            }
        }

        /// <summary>Rope force on the skier (slack-capable spring, critically damped; spec 1.4.3).</summary>
        Vector3 RopeForce(Vector3 p, Vector3 v)
        {
            Vector3 pyl = Pylon() + Vector3.up * SkierSim.PYLON_Y, hand = p + Vector3.up * 0.95f;
            Vector3 d = hand - pyl; float L = d.magnitude; if (L <= jR) return Vector3.zero;
            Vector3 n = d / L; Vector3 vb = boatFwd * (jumpBoatKmh / 3.6f);
            float vr = Vector3.Dot(v - vb, n), c = 2f * Mathf.Sqrt(ROPE_K * KM);
            return -n * Mathf.Max(0f, ROPE_K * (L - jR) + c * vr);
        }

        void JumpStep(float dt)
        {
            jT += dt; runT = RunTime();
            float g = 9.81f, sinA = RampSinA, cosA = Mathf.Sqrt(1f - sinA * sinA);
            skierPrev = skierPos;
            switch (jphase)
            {
                case JPhase.Approach:
                {
                    bool press = jumpPress; jumpPress = false;
                    // a press before the cut window opens is kept (it used to be thrown away, so an early tap meant a miss), and
                    // the cut goes at the ideal moment; no press at all -> assisted cut a little after the ideal moment, so the skier
                    // always makes the double-wake cut to the ramp
                    if (press && !cutOn && runT < CUT_OPEN) { cutArmed = true; press = false; }
                    if (cutArmed && runT >= AUTO_CUT) press = true;
                    if (!float.IsNaN(jumpCutAt)) press = runT >= jumpCutAt; else if (autopilot) press = runT >= AUTO_CUT;
                    else if (runT >= ASSIST_CUT) press = true;
                    if (!cutOn && press && runT >= CUT_OPEN && runT < CUT_LATE) BeginCut(runT);
                    if (!cutOn && runT >= CUT_LATE)
                    {
                        // never cut: swing back in behind the boat, past the ramp
                        float p0 = Phi(runT, out float r0); cutA = new Vector3(runT, p0, r0); cutB = new Vector3(runT + 3.5f, 0f, 0f); cutOn = true; jMissed = true;
                        JumpResult = "MISSED"; banner = "NO CUT · MISSED THE RAMP"; bannerT = 3f;
                        Debug.Log("[SkiView] jump: no cut by -2.6 s -> missed the ramp");
                    }
                    if (jMissed && runT > cutB.x + 1.5f) { EndJumpRun(); break; }
                    float phi = Phi(runT, out float rate) * Mathf.Deg2Rad;
                    Vector3 sd = SideOfBoat();
                    skierPos = Pylon() - boatFwd * (jR * Mathf.Cos(phi)) + sd * (jR * Mathf.Sin(phi)); skierPos.y = 0f;
                    skierVel = (skierPos - skierPrev) / dt; UpdateHeadRate(dt);
                    if (runT > -0.25f && jumpDebug) Debug.Log($"[SkiView] jdbg runT {runT:0.000} phi {phi * Mathf.Rad2Deg:0.00} rate {rate:0.0} vel {skierVel.magnitude:0.0} dt {dt:0.0000} boat {boatFwd} cutOn {cutOn} cutA {cutA} cutB {cutB}");
                    // compression and edge: knees soften through the wakes, hips drop and the body stacks against the line in the cut
                    var ap = PoseTbl;
                    jumpCrouch = SkierPoses.CrouchForKnee(ap.approachKnee); jumpPitch = 0f; jumpHandleIn = ap.approachHandleIn;
                    jumpTip = ap.approachTip; jumpVisualTip = ap.approachTip; jumpVYaw = 0f; jumpSink = 0.03f;
                    if (runT >= 0f && !jMissed)
                    {
                        // ramp contact at the toe: start the ramp phase from the actual entry point
                        jphase = JPhase.Ramp; jT = 0f;
                        Vector3 rel = skierPos - jToe; rampS = Vector3.Dot(rel, jAxis) / cosA; rampLat = Vector3.Dot(rel, jRight);
                        Vector3 vh = skierVel; vh.y = 0f;
                        rampV = Vector3.Dot(vh, jAxis) / cosA; rampLatV = Vector3.Dot(vh, jRight);
                        float hwR = rampWidth * 0.5f - 0.3f;
                        if (Mathf.Abs(rampLat) > hwR) { Debug.Log($"[SkiView] jump: entry {rampLat:+0.00;-0.00} m clamped to the ramp ({hwR:0.00})"); rampLat = Mathf.Clamp(rampLat, -hwR, hwR); }
                        JumpLipSpeed = vh.magnitude; JumpToeSpeed = vh.magnitude; JumpCrossDeg = Mathf.Abs(Vector3.SignedAngle(jAxis, vh, Vector3.up));
                        Debug.Log($"[SkiView] jump: ramp toe at {simTime:0.00}s speed {vh.magnitude:0.0} m/s ({vh.magnitude / (jumpBoatKmh / 3.6f):0.00}x boat) across the ramp axis {Vector3.SignedAngle(jAxis, vh, Vector3.up):0.0} deg, entry {rampLat:+0.00;-0.00} m from the centreline");
                        EmitBurst(crashDrops, skierPos + Vector3.up * 0.1f, 140, skierVel * 0.25f, 1.6f, 1.0f, 3.0f, 0.8f, fxRnd);
                        EmitBurst(crashSheet, skierPos, 60, skierVel * 0.1f, 1.2f, 0.6f, 1.8f, 0.7f, fxRnd);
                        EmitBurst(crashMist, skierPos + Vector3.up * 0.3f, 25, skierVel * 0.2f, 1.8f, 0.3f, 1.2f, 0.7f, fxRnd);
                    }
                    break;
                }
                case JPhase.Ramp:
                {
                    // along the waxed, wet surface: gravity component + friction; the rope still pulls at ~45 deg (small, ignored)
                    rampV -= (g * sinA + MU * g * cosA) * dt;
                    rampS += rampV * dt; rampLat += rampLatV * dt;
                    float tau = Mathf.Clamp01(rampS / rampLenOut);
                    bool pp = jumpPress; jumpPress = false;
                    if (!float.IsNaN(jumpPopAt)) pp = tau >= jumpPopAt; else if (autopilot) pp = tau >= AUTO_POP;
                    if (!popped && pp) { popped = true; JumpPopT = tau; }
                    var tp = PoseTbl;
                    float extend = Mathf.InverseLerp(0.55f, 1f, tau);   // tall the whole ramp, a little taller through the lip — no squat
                    float kneeT = Mathf.Lerp(tp.takeoffKneeIn, tp.takeoffKneeOut, extend);
                    jumpCrouch = SkierPoses.CrouchForKnee(kneeT); jumpPitch = tp.takeoffChestPitch; jumpHandleIn = tp.takeoffHandleIn;
                    jumpTip = 0f; jumpVisualTip = 0f; jumpVYaw = 0f; jumpSink = 0f;
                    skierPos = jToe + jAxis * (rampS * cosA) + jRight * rampLat + Vector3.up * (rampS * sinA);
                    skierVel = (skierPos - skierPrev) / dt;
                    if (rampS >= rampLenOut)
                    {
                        float tbi = Vector3.Dot(skierPos - jToe, jAxis) - rampLenOut * cosA;   // overshoot past the lip (horizontal)
                        PopEffect(popped ? JumpPopT : -1f, out popDv, out landPitchWant);
                        landPitchWant -= Mathf.Max(0f, JumpCrossDeg - 19f) * 0.8f;   // a very steep crossing twists him at the lip
                        if (jCrash) landPitchWant = -4f;
                        jPos = skierPos; jVel = jAxis * (rampV * cosA) + Vector3.up * (rampV * sinA + popDv) + jRight * rampLatV;
                        jphase = JPhase.Flight; jT = 0f; jPost = 0f; flyMaxY = jPos.y; skiPitchFly = Mathf.Asin(sinA) * Mathf.Rad2Deg + 3f;
                        JumpLipSpeed = jVel.magnitude;
                        Debug.Log($"[SkiView] jump: off the top edge at {jVel.magnitude:0.0} m/s ({jVel.magnitude * 3.6f:0} km/h), vertical {jVel.y:0.00} m/s (pop {popDv:0.00} at {(popped ? JumpPopT.ToString("0.00") : "none")}), overshoot {tbi:0.00} m, landing pitch {landPitchWant:0.0}");
                        EmitBurst(crashDrops, skierPos, 160, jVel * 0.8f, 0.8f, -1.5f, 0.5f, 0.7f, fxRnd);   // a skirt of droplets falling off the skis
                    }
                    break;
                }
                case JPhase.Flight:
                {
                    // posture p -> 1 (the "bird": body forward over the skis, skis in a V, handle at the hips)
                    jPost = Mathf.MoveTowards(jPost, jT < 0.12f ? 0f : 1f, 2.5f * dt);
                    {
                        bool lp = jumpPress; jumpPress = false;
                        if (!float.IsNaN(jumpPopAt) && jumpPopAt > 1f) lp = jT >= (jumpPopAt - 1f) * 0.3f;
                        if (!popped && lp && jT < 0.15f) { popped = true; JumpPopT = 1f + jT / 0.3f; PopEffect(JumpPopT, out float ldv, out landPitchWant); jVel.y += ldv; if (jCrash) landPitchWant = -4f; Debug.Log($"[SkiView] jump: late pop {jT:0.00} s after the lip -> landing pitch {landPitchWant:0.0}"); }
                    }
                    int sub = 4; float h = dt / sub;
                    for (int i = 0; i < sub; i++)
                    {
                        Vector3 v = jVel; float sp = v.magnitude;
                        Vector3 vh = new Vector3(v.x, 0, v.z); float vhm = vh.magnitude;
                        Vector3 n = vhm > 0.1f ? (Vector3.up * vhm - vh / vhm * v.y) / Mathf.Max(0.1f, sp) : Vector3.up;
                        float cl = Mathf.Lerp(0.5f, 1f, jPost) * jumpClA, cd = Mathf.Lerp(0.5f, 1f, jPost) * jumpCdA;
                        Vector3 F = 0.5f * RHO * sp * sp * (cl * n - cd * (sp > 0.1f ? v / sp : Vector3.zero)) + RopeForce(jPos, v);
                        jVel += (F / KM + Vector3.down * g) * h;
                        jPos += jVel * h;
                    }
                    flyMaxY = Mathf.Max(flyMaxY, jPos.y);
                    skierPos = jPos; skierVel = jVel;
                    // ski pitch: tips up off the ramp, held up through the apex, flattening to +7 deg (good) or tips down (crash) for touchdown
                    float fall = Mathf.InverseLerp(0.4f, -0.6f, jVel.y / Mathf.Max(1f, jVel.magnitude) * 10f);
                    float pitchWant = Mathf.Lerp(Mathf.Max(18f, landPitchWant + 4f), landPitchWant, fall);
                    skiPitchFly = Mathf.MoveTowards(skiPitchFly, pitchWant, 30f * dt);
                    float land = Mathf.Clamp01((0.9f - jPos.y) / 0.9f) * (jVel.y < 0 ? 1f : 0f);   // close the V and level the skis before the water
                    var fp = PoseTbl;
                    fp.FlightPose(jPost, land, out float bodyFwd, out float tipUp, out float flightKnee, out float vHalf);
                    jumpCrouch = SkierPoses.CrouchForKnee(flightKnee);
                    jumpVisualTip = tipUp;
                    jumpPitch = tipUp + bodyFwd;   // one hinge: cancels the ski tip and leaves the body `bodyFwd` degrees forward of vertical
                    jumpHandleIn = fp.flightHandleIn; jumpTip = 0f; jumpVYaw = vHalf; jumpSink = 0f;
                    if (jPos.y <= 0f && jVel.y < 0f)
                    {
                        jPos.y = 0f; skierPos = jPos;
                        JumpAirTime = jT; JumpApex = flyMaxY; JumpTouchdown = jPos;
                        Vector3 lipC = jumpRamp.position; Vector3 d = jPos - lipC; d.y = 0f;
                        // T15 §2 / IWWF 10.13: official distance = impact from top-edge centre + 2.1 m, 0.1 m steps
                        JumpDistance = Mathf.Round((d.magnitude + 2.1f) * 10f) / 10f;
                        int ft = Mathf.FloorToInt(JumpDistance / 0.3048f + 0.5f);
                        float latSl = Vector3.Dot(jPos - jFwd * Vector3.Dot(jPos, jFwd), jSide); JumpLandLat = latSl;
                        Debug.Log($"[SkiView] jump: touchdown {latSl:0.0} m from the slalom line (ramp centre {Vector3.Dot(jLip - jFwd * Vector3.Dot(jLip, jFwd), jSide):0.0} m), along {Vector3.Dot(jPos, jFwd):0.0} m");
                        Debug.Log($"[SkiView] jump: touchdown after {jT:0.00} s, apex {flyMaxY:0.0} m, distance {JumpDistance:0.0} m / {ft} ft (+2.1), ski pitch {skiPitchFly:0.0} deg, sink {-jVel.y:0.0} m/s -> {(skiPitchFly < 2f ? "OUT THE FRONT" : skiPitchFly > 16f ? "FELL BACK" : "ride out")}");
                        Vector3 hv = jVel; hv.y = 0f;
                        if (skiPitchFly < 2f || skiPitchFly > 16f)
                        {
                            // tips caught: over the front (or tips far too high: sat back and fell). Straight into the impact, both skis come off
                            string why = skiPitchFly < 2f ? "OUT THE FRONT" : "FELL BACK"; JumpResult = why;
                            sim.Fall(why); skierVel = hv * 0.55f;
                            mode = Mode.Fallen; impactDone = false; fallFace = hv.sqrMagnitude > 1f ? hv.normalized : boatFwd;
                            actionT = fallImpactT - 0.02f; fallT = fallDuration - actionT; jphase = JPhase.None;
                            RegisterJumpPass("FALL", 0f);
                            banner = $"{why} · {JumpDistance:0.0} m · {ft} ft · NO SCORE"; bannerT = 4f;
                            return;
                        }
                        // James: set sink speed before Landing so HeavyLanding can use TouchdownVz
                        if (SkiAudio.Instance != null) SkiAudio.Instance.TouchdownVz = -jVel.y;
                        jphase = JPhase.Landing; jT = 0f; jVel = hv; JumpResult = "RIDE OUT";
                        splash[5] = new Vector4(jPos.x, jPos.z, simTime, 0.75f);
                        EmitBurst(crashSheet, jPos + hv.normalized * 0.6f, 85, hv * 0.45f, 1.8f, 1.2f, 3.0f, 0.9f, fxRnd);   // pass 15: 150 -> 85, mist 55 -> 32 (softer core)
                        EmitBurst(crashDrops, jPos + hv.normalized * 0.4f, 260, hv * 0.55f, 2.4f, 1.5f, 4.0f, 0.9f, fxRnd);
                        EmitBurst(crashMist, jPos + hv.normalized * 0.8f + Vector3.up * 0.4f, 32, hv * 0.35f, 2.4f, 0.4f, 1.6f, 1f, fxRnd);
                        banner = $"{JumpDistance:0.0} m · {ft} ft · {RampRatio:0.000} · {jumpBoatKmh:0} km/h"; bannerT = 4f;
                    }
                    break;
                }
                case JPhase.Landing:
                case JPhase.RideOut:
                {
                    // on the water: planing drag back toward boat speed, the rope comes tight and swings the skier in behind the boat
                    float vb = jumpBoatKmh / 3.6f; Vector3 v = jVel; float sp = v.magnitude;
                    Vector3 F = RopeForce(jPos, v) - (sp > 0.1f ? v / sp : Vector3.zero) * KM * 1.6f * Mathf.Max(0f, sp - vb * 0.85f);   // planing drag: he drops back until the line is tight again
                    F.y = 0f; jVel += F / KM * dt; jVel.y = 0f;
                    // the skis bite: the sideways drift off the ramp diagonal dies out and he rides back in behind the boat
                    { Vector3 sdb = SideOfBoat(); float vs = Vector3.Dot(jVel, sdb); jVel -= sdb * (vs * (1f - Mathf.Exp(-dt / 0.6f))); jVel -= sdb * (Mathf.Clamp(Vector3.Dot(jPos - Pylon(), sdb), -1f, 1f) * 1.2f * dt); }
                    jPos += jVel * dt; jPos.y = 0f;
                    skierPos = jPos; skierVel = jVel; UpdateHeadRate(dt);
                    var lp = PoseTbl;
                    if (jphase == JPhase.Landing)
                    {
                        float into = Mathf.Clamp01(jT / 0.22f);
                        float back = Mathf.Clamp01((jT - 0.22f) / Mathf.Max(0.2f, lp.landRecoverSec - 0.22f));
                        float kneeL = Mathf.Lerp(Mathf.Lerp(lp.preLandKnee, lp.landKnee, into), lp.glideKnee, back);
                        float torso = Mathf.Lerp(Mathf.Lerp(lp.preLandLean, lp.landTorso, into), 0f, back);
                        jumpVisualTip = Mathf.Lerp(lp.preLandTip, lp.landTip, Mathf.Clamp01(jT / 0.2f));
                        jumpCrouch = SkierPoses.CrouchForKnee(kneeL);
                        jumpPitch = jumpVisualTip + torso;
                        jumpHandleIn = Mathf.Lerp(lp.flightHandleIn, lp.landHandleIn, into);
                    }
                    else
                    {
                        float ru = Mathf.Clamp01(jT / 0.4f);
                        jumpCrouch = ru < 1f ? SkierPoses.CrouchForKnee(lp.glideKnee) : -1f;
                        jumpPitch = Mathf.Lerp(lp.landTip, 0f, ru);
                        jumpVisualTip = lp.waterTip;
                        jumpHandleIn = Mathf.Lerp(lp.landHandleIn, 0.05f, ru);
                    }
                    jumpTip = lp.waterTip; jumpVYaw = 0f; jumpSink = 0.03f;
                    if (jphase == JPhase.Landing && jT > lp.landRecoverSec) { jphase = JPhase.RideOut; jT = 0f; }
                    if (jphase == JPhase.RideOut && Mathf.Abs(jT - 2.0f) < dt * 0.5f)
                    {
                        JumpResult = "SCORED";
                        banner = SkiView.UI.SkiViewUI.Kmh ? $"SCORED · {JumpDistance:0.0} m" : $"SCORED · {Mathf.FloorToInt(JumpDistance / 0.3048f + 0.5f)} ft";
                        bannerT = 3f;
                        if (JumpDistance >= 40f) SkiAudio.Instance?.SpectatorReaction();   // James optional [DA ≥ 40 m]
                    }
                    if (jphase == JPhase.RideOut && jT > 4f) EndJumpRun();
                    break;
                }
            }
        }

        // ---------- pass 14: playable jump camera (shore / side), from just before the toe through the ride-out (and a crash)
        bool jcActive; Vector3 jcPos, jcLook, jcLookVel; float jcFov, jcFovVel;
        bool JumpCamWanted => jumpCam && jumpRamp != null && ((mode == Mode.Jump && !jMissed && (jphase != JPhase.Approach || runT > -0.6f) && jphase != JPhase.None) || (mode == Mode.Fallen && jumpMode && jcActive));
        /// <summary>Returns true when the jump camera drove the view this frame.</summary>
        bool UpdateJumpCamera(bool snap)
        {
            if (!JumpCamWanted) { jcActive = false; return false; }
            Vector3 tgt = skierPos + Vector3.up * (mode == Mode.Fallen ? 0.3f : 0.9f) + skierVel * 0.08f;
            bool hard = snap || !Application.isPlaying || !jcActive;
            if (!jcActive)
            {
                // on a low stand at the shore on the ramp side, a little downstream of the lip: sees the ramp face, the flight and the landing
                jcActive = true; jcPos = jLip + jSide * 17f + jFwd * 7f; jcPos.y = 2.4f;
            }
            float d = Vector3.Distance(jcPos, tgt);
            float wantFov = Mathf.Clamp(2f * Mathf.Atan(1.9f / 0.40f * 0.5f / Mathf.Max(5f, d)) * Mathf.Rad2Deg, 9f, 45f);   // skier ~1/3 of the frame height
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            if (hard) { jcLook = tgt; jcLookVel = Vector3.zero; jcFov = wantFov; jcFovVel = 0f; }
            else { jcLook = Vector3.SmoothDamp(jcLook, tgt, ref jcLookVel, 0.10f, Mathf.Infinity, dt); jcFov = Mathf.SmoothDamp(jcFov, wantFov, ref jcFovVel, 0.35f, Mathf.Infinity, dt); }
            cam.transform.position = jcPos; cam.transform.rotation = Quaternion.LookRotation(jcLook - jcPos, Vector3.up);
            cam.fieldOfView = jcFov; cam.nearClipPlane = 0.3f;
            return true;
        }

        string JumpPrompt()
        {
            if (mode != Mode.Jump) return "";
            if (jphase == JPhase.Approach && !cutOn) return runT > -4.6f ? "   ·   CUT NOW! (tap)" : "   ·   wait on the far side, tap to CUT";
            if (jphase == JPhase.Ramp && !popped) return "   ·   POP! (tap)";
            return "";
        }

        void EndJumpRun()
        {
            // hand the skier back to the rope pendulum (island turn next); the rope stays at jump length while jump mode is on
            Vector3 rel = skierPos - Pylon(); rel.y = 0f;
            sim.ropeM = jumpMode ? jumpRopeM : RopeMForRadius(Mathf.Max(6f, rel.magnitude));
            Vector3 right = Vector3.Cross(Vector3.up, boatFwd);
            sim.theta = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(rel, right) / sim.Radius, -0.99f, 0.99f)); sim.omega = 0f;
            cutArmed = false; jphase = JPhase.None; mode = Mode.Turn; bcFov = 0f; jcActive = false;   // boat cam snaps back
            // T15: balk counts as a pass (no distance); scored ride-out records distance
            if (jMissed) RegisterJumpPass("BALK", 0f);
            else if (JumpResult == "SCORED" || JumpResult == "RIDE OUT") RegisterJumpPass("SCORED", JumpDistance);
            if (!jumpMode) SetJumpGear(false);
        }

        /// <summary>Called every FixedStep after the boat moved: jump-lane offset and the run trigger.</summary>
        void JumpBoatAndTrigger(float dt, bool onLine, int dir)
        {
            if (jumpRamp == null) return;
            bool inRun = mode == Mode.Jump || (mode == Mode.Fallen && jumpMode);
            if (jumpMode && jFwd == Vector3.zero) JumpGeometry();
            float want = (jumpMode && (inRun || (dir == jumpDir && onLine))) ? boatLatTarget : 0f;
            boatLatOff = Mathf.MoveTowards(boatLatOff, want, 0.6f * dt);
            if (boatLatOff != 0f) boatPos += (jSide == Vector3.zero ? Vector3.zero : jSide) * boatLatOff;
            if (mode == Mode.Turn && jumpMode && dir == jumpDir && onLine && !JumpResultsOpen && JumpPassCount < JUMP_PASSES)
            {
                float t = RunTime();
                if (t > -11.2f && t < -9f) StartApproach();
            }
        }

        /// <summary>Jump skis: both boots ride on their skis; in the air the skis open into a V and pitch with the posture.</summary>
        void PoseJumpSkis()
        {
            if (!JumpGearOn || jumpSkiL == null || jumpSkiR == null) return;
            bool air = mode == Mode.Jump && (jphase == JPhase.Flight || jphase == JPhase.Ramp);
            // Ramp, flight and landing: the root rotation is the ski pitch. Everywhere else, undo the water-stance nose-down and add a little tip.
            bool rootIsSki = mode == Mode.Jump && (jphase == JPhase.Ramp || jphase == JPhase.Flight || jphase == JPhase.Landing);
            bool rideBlend = mode == Mode.Jump && jphase == JPhase.RideOut && jT < 0.35f;
            float tip = rootIsSki ? 0f : rideBlend ? Mathf.Lerp(0f, 3f + jumpTip, jT / 0.35f) : (mode == Mode.Jump ? 3f + jumpTip : (WaterPose ? 0f : 4f));
            float vy = mode == Mode.Jump ? jumpVYaw : 0f, sink = mode == Mode.Jump ? jumpSink : 0.03f;
            if (mode == Mode.Fallen) return;   // popped free (WaterFx floats them)
            foreach (var (t, sg) in new[] { (jumpSkiL, -1f), (jumpSkiR, 1f) })
            {
                var R = Quaternion.Euler(-tip, sg * vy, 0f);
                Vector3 ankle = new Vector3(sg * jumpStanceHalf, air ? 0f : -sink, 0f);
                t.localRotation = R; t.localPosition = ankle - R * new Vector3(0, 0, jumpSkiAnkleZ);
            }
        }

        /// <summary>Root pose while on the ramp / in the air (other phases use the normal stance and lean).</summary>
        bool JumpRootPose()
        {
            if (mode != Mode.Jump || skier == null) return false;
            Vector3 vh = skierVel; vh.y = 0f; Vector3 face = vh.sqrMagnitude > 1f ? vh.normalized : boatFwd;
            if (jphase == JPhase.Ramp)
            {
                float sA = RampSinA, cA = Mathf.Sqrt(1f - sA * sA);
                Vector3 nrm = Vector3.Cross(jAxis * cA + Vector3.up * sA, jRight).normalized;   // ramp surface normal
                Vector3 f = skierVel - Vector3.Dot(skierVel, nrm) * nrm; if (f.sqrMagnitude < 1f) f = jAxis * cA + Vector3.up * sA;
                skier.SetPositionAndRotation(skierPos, Quaternion.LookRotation(f.normalized, nrm));
                return true;
            }
            if (jphase == JPhase.Flight)
            {
                float vd = lastVisT < 0f ? 0.016f : Mathf.Clamp(simTime - lastVisT, 0.016f, 0.25f);
                leanRightDeg = Mathf.MoveTowards(leanRightDeg, 0f, 120f * vd);
                skier.SetPositionAndRotation(skierPos, Quaternion.LookRotation(face) * Quaternion.Euler(-jumpVisualTip, 0f, 0f));
                return true;
            }
            if (jphase == JPhase.Landing)
            {
                leanRightDeg = Mathf.MoveTowards(leanRightDeg, 0f, 90f * Mathf.Max(0.016f, lastVisT < 0f ? 0.016f : Mathf.Clamp(simTime - lastVisT, 0f, 0.25f)));
                skier.SetPositionAndRotation(skierPos, Quaternion.LookRotation(face) * Quaternion.Euler(-jumpVisualTip, 0f, 0f));
                return true;
            }
            if (jphase == JPhase.RideOut && jT < 0.35f)
            {
                float u = Mathf.Clamp01(jT / 0.35f);
                float rootX = Mathf.Lerp(-PoseTbl.landTip, 3f, u);
                skier.SetPositionAndRotation(skierPos, Quaternion.LookRotation(face) * Quaternion.Euler(rootX, 0f, 0f));
                return true;
            }
            return false;
        }

        float JumpSlack()
        {
            // slack line in the air (the skier flies faster than the boat at first), tight again in the ride-out
            if (jphase == JPhase.Flight) { Vector3 d = skierPos + Vector3.up * 0.95f - (Pylon() + Vector3.up * SkierSim.PYLON_Y); return Mathf.Clamp01((jR - d.magnitude) / 1.2f + 0.3f); }
            if (jphase == JPhase.Ramp) return 0.25f;
            return 0f;
        }

        /// <summary>After a jump crash both skis come off: one pops up and over, the other skids off to the side.</summary>
        void JumpSkisFloat(Vector3 flat)
        {
            if (jumpSkiL == null || jumpSkiR == null) return;
            float tt = actionT - fallImpactT; Vector3 side = Vector3.Cross(Vector3.up, fallFace);
            int k = 0;
            foreach (var t in new[] { jumpSkiL, jumpSkiR })
            {
                float sg = k == 0 ? -1f : 1f, hop = k == 0 ? 1.4f : 0.5f, dur = k == 0 ? 0.55f : 0.4f;
                Vector3 dirF = (fallFace + side * sg * (k == 0 ? 0.5f : 0.9f)).normalized;
                Vector3 rest = flat + fallFace * (k == 0 ? 2.6f : 1.4f) + side * sg * (k == 0 ? 1.2f : 2.0f) + dirF * (0.4f * (1f - Mathf.Exp(-tt * 0.6f)));
                rest.y = 0.02f + Mathf.Sin(simTime * 2.3f + k) * 0.012f;
                float pop = Mathf.Clamp01(tt / dur);
                Vector3 p = Vector3.Lerp(flat + Vector3.up * 0.2f, rest, pop) + Vector3.up * (Mathf.Sin(pop * Mathf.PI) * hop);
                Quaternion spin = Quaternion.LookRotation(dirF) * Quaternion.Euler(-50f + 50f * pop, pop * (k == 0 ? 260f : 120f), sg * 40f * (1f - pop));
                Quaternion floatRot = Quaternion.LookRotation(dirF) * Quaternion.Euler(Mathf.Sin(simTime * 1.7f + k) * 2f, 0f, 4f * sg + Mathf.Sin(simTime * 2.3f) * 3f);
                // rotate about the ski centre, not the tail
                Quaternion q = pop < 1f ? spin : floatRot;
                t.SetPositionAndRotation(p - q * new Vector3(0, 0, 1.0f), q);
                if (tt >= dur && k == 0 && splash[2].w == 0f) splash[2] = new Vector4(rest.x, rest.z, simTime, 0.3f);
                k++;
            }
        }

        public void ToggleJumpMode()
        {
            if (jumpRamp == null && !jumpMode) return;   // pass 15: slalom-only lakes (no ramp)
            if (jumpOnly && jumpMode) return;            // pass 18: jump-only lake
            if (!jumpMode) FlushSlalomSet();
            if (jumpMode) SkiAudio.Instance?.UiBack(); else SkiAudio.Instance?.UiConfirm();
            jumpMode = !jumpMode; SetJumpGear(jumpMode);
            if (jumpMode)
            {
                jumpBoatKmh = JUMP_KMH[SetupJumpKmhIdx];
                rampHeight = JUMP_RAMP_M[SetupJumpRampIdx];
                jumpRopeM = 23f;
                JumpGeometry(); ResetJumpRound();
            }
            banner = jumpMode ? $"JUMP · 3 PASSES · LONGEST · {Bound(path.CourseFwd(jumpDir))}" : "SLALOM MODE"; bannerT = 2.5f;
        }

        void JumpRigParams()
        {
            if (rig == null) return;
            bool j = mode == Mode.Jump;
            rig.jumpCrouch = j ? jumpCrouch : -1f; rig.jumpPitchDeg = j ? jumpPitch : 0f; rig.jumpHandleIn = j ? jumpHandleIn : 0f;
            rig.jumpPose = !j ? 0 : jphase == JPhase.Ramp ? 1 : jphase == JPhase.Flight ? 2 : jphase == JPhase.Landing ? 3 : 0;
        }

        void JumpSpray(float sdt, Vector3 face)
        {
            if (!JumpGearOn) return;
            bool onWater = mode == Mode.Turn || mode == Mode.Pass || (mode == Mode.Jump && (jphase == JPhase.Approach || jphase == JPhase.Landing || jphase == JPhase.RideOut));
            Vector3 su = skier.up; Vector3 leanH = su - Vector3.Dot(su, Vector3.up) * Vector3.up;
            Vector3 away = leanH.sqrMagnitude > 1e-4f ? -leanH.normalized : -skier.right;
            float edge = Mathf.Clamp01(Vector3.Angle(su, Vector3.up) / 55f);
            float sp = skierVel.magnitude;
            if (jumpSprayL != null) jumpSprayL.Drive(sp, edge, Mathf.Clamp01(Mathf.Abs(headRate) / 1.2f), away, face, sdt, onWater);
            if (jumpSprayR != null) jumpSprayR.Drive(sp, edge * 0.8f, Mathf.Clamp01(Mathf.Abs(headRate) / 1.2f) * 0.8f, away, face, sdt, onWater);
        }
    }
}

using System;

namespace SkiView
{
    /// <summary>C# port of the slalom pendulum + scoring from cutline-app/src/game/sim.ts and course.ts (v1.2.0).
    /// Frame (same as the TS sim, "TS frame"): boat travels -Z, entrance gate at Z=0, buoys at Z=-(27+41i),
    /// exit gate at Z=-259, +X = skier's right. theta = rope angle from straight behind (+ = skier left).</summary>
    [Serializable]
    public class SkierSim
    {
        public const float BUOY_OFFSET = 11.5f, BUOY_SPACING = 41f, FIRST_BUOY = 27f, COURSE_LENGTH = 259f;
        public const float GATE_Z = 0f, EXIT_Z = -COURSE_LENGTH;
        public const float PYLON_Y = 1.285f /* SkiBoat.fbx TowPoint: tow ball above the waterline */, HANDLE_Y = 0.92f, ARM_REACH = 0.9f, REACH = 1.85f;
        public static readonly float[] ROPE_M = { 18.25f, 16.0f, 14.25f, 13.0f, 12.0f, 11.25f, 10.75f, 10.25f, 9.75f };
        public static readonly string[] ROPE_LABEL = { "15 OFF", "22 OFF", "28 OFF", "32 OFF", "35 OFF", "38 OFF", "39.5 OFF", "41 OFF", "43 OFF" };

        // Difficulty (cutline "amateur" tuning, run at 34 mph for N1)
        public float leanPower = 23f, maxOmega = 2.45f, gateHalf = 2.5f, buoySlack = 1.9f;
        public bool missGatesEnds = true;
        // skier stats from TOP-TIER-PLAN §10.3
        public float cut = 1.06f, hold = 1.06f, edge = 1.08f, reach = 1.04f;
        // b10.21 short-line difficulty (Scott Oct 23 + polish/slalom-pass-rates.md).
        // Goal: a little easier from 22 off (not much); short line still gets hard; stepped curve with 38 cliff.
        // Evidence: BoS M1 scrape ~1/9 @35, ~1/20 @38; recreational estimates in that brief. Tables not wild guesses.
        // Indices match ROPE_LABEL: 15, 22, 28, 32, 35, 38, 39.5, 41, 43.
        static readonly float[] ROPE_PHYS_MUL = {
            1.00f,  // 15  — baseline (tutorial)
            1.07f,  // 22  — b10.6 was 1.05; +2% more ease (Scott: little easier from 22)
            1.04f,  // 28  — step down from 22 (was 1.06 flat mid-band)
            1.01f,  // 32  — advanced; almost baseline (was 1.05)
            0.98f,  // 35  — rare (~11% M1); slight physics tax (was 1.02 ease)
            0.94f,  // 38  — cliff: rope < buoy offset; first hard filter (~5% M1)
            0.92f,  // 39.5
            0.90f,  // 41
            0.88f,  // 43  — WR territory
        };
        static readonly float[] ROPE_GATE_EXTRA_M = {
            0.22f,  // 15 welcoming
            0.20f,  // 22 (was 0.18 for 22–32 flat)
            0.12f,  // 28
            0.08f,  // 32
            0.04f,  // 35
            0f, 0f, 0f, 0f  // 38+ tournament-strict
        };
        static readonly float[] ROPE_BUOY_GRACE_M = {
            0.22f,  // 15 wide
            0.18f,  // 22 (was 0.15 for 22–32)
            0.12f,  // 28
            0.08f,  // 32
            0.04f,  // 35
            0f, 0f, 0f, 0f  // 38+ full 11.5 m — must stretch
        };

        /// <summary>b10.21: physics mul by rope (torque / maxOmega help). &gt;1 easier, &lt;1 short-line tax.</summary>
        public float RopePhysicsMul()
        {
            int i = ropeIndex < 0 ? 0 : (ropeIndex >= ROPE_PHYS_MUL.Length ? ROPE_PHYS_MUL.Length - 1 : ropeIndex);
            return ROPE_PHYS_MUL[i];
        }
        public float GateExtraM()
        {
            int i = ropeIndex < 0 ? 0 : (ropeIndex >= ROPE_GATE_EXTRA_M.Length ? ROPE_GATE_EXTRA_M.Length - 1 : ropeIndex);
            return ROPE_GATE_EXTRA_M[i];
        }
        /// <summary>|x| on the ball side that still counts as rounded. 38+ = full BUOY_OFFSET (no grace).</summary>
        public float BuoyClearNeed()
        {
            int i = ropeIndex < 0 ? 0 : (ropeIndex >= ROPE_BUOY_GRACE_M.Length ? ROPE_BUOY_GRACE_M.Length - 1 : ropeIndex);
            return BUOY_OFFSET - ROPE_BUOY_GRACE_M[i];
        }

        public float theta = 0.3f, omega = 0f, lean = 0f, prevTheta;
        public float ropeM = 18.25f;
        public int ropeIndex = 0;

        // pass state
        public bool scoring;
        public bool gatesCleared, awaitingExit, fallen, passDone;
        public int nextBuoy;
        public float passBuoys, pendingHalf;
        public int roundedBalls;   // balls rounded this pass (each worth 0.5 on the round + 0.5 on the cross / exit gate for ball 6)
        /// <summary>The one pass result: written once by Finish() when a pass ends, read by the banner, the results screen and progression.</summary>
        public struct PassResult { public float buoys; public int rope, rounded, id; public bool clean, exitGate; public string why; }
        public PassResult last;
        public PassResult Finish(bool clean)
        {
            last = new PassResult { buoys = Math.Min(6f, passBuoys), rope = ropeIndex, rounded = roundedBalls, clean = clean && passBuoys >= 5.99f,
                exitGate = clean, why = message, id = last.id + 1 };
            return last;
        }
        public static string Fmt(float b) { int w = (int)Math.Floor(b + 1e-3f); return (b - w >= 0.49f ? $"{w}.5" : w.ToString()); }
        public string Breakdown(PassResult r) => r.clean ? "6 buoys + exit gate" : $"{Fmt(r.buoys)} buoys · {r.why}";
        public string message = "";
        public float x, z, prevZ; // skier position in TS frame (only meaningful while scoring)

        public static float BuoyX(int i) => i % 2 == 0 ? BUOY_OFFSET : -BUOY_OFFSET;
        public static float BuoyZ(int i) => -(FIRST_BUOY + i * BUOY_SPACING);

        static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        static int Sign(float v) => v > 0 ? 1 : (v < 0 ? -1 : 0);

        public float RopeHoriz => (float)Math.Sqrt(Math.Max(4f, ropeM * ropeM - (PYLON_Y - HANDLE_Y) * (PYLON_Y - HANDLE_Y)));
        public float Radius => RopeHoriz + ARM_REACH + Stretch;
        /// <summary>b9 (Scott Oct 23): the body / ski stretch at the ball. Leaning out with the outside hand released, the skier gets
        /// long and low and the ski carves out past the handle by up to STRETCH_M. It moves the skier's real path (skierPos), so the
        /// buoy judge sees it; there is no scoring tolerance. The rig reads Stretch01 for the reach pose.</summary>
        public const float STRETCH_M = 1.0f;
        public float Stretch => STRETCH_M * Stretch01;
        public float Stretch01 => stretchS;
        /// <summary>reach v2: the stretch eases in / out at STRETCH_RATE per second (it used to snap off in ~3 frames when the lean
        /// flipped for the cut, a 1 m radius step = a ~60 deg heading jump at 41 off).</summary>
        public const float STRETCH_RATE = 2f; public float stretchS;
        float StretchTarget => Math.Abs(theta) > 0.6f && Sign(lean) == Sign(theta) ? Clamp((Math.Abs(theta) - 0.6f) / 0.6f, 0f, 1f) * Clamp(Math.Abs(lean), 0f, 1f) : 0f;
        /// <summary>Skier lateral offset in the boat frame (+ = right).</summary>
        public float Lateral => -Radius * (float)Math.Sin(theta);
        public float LateralVel => -Radius * omega * (float)Math.Cos(theta);
        public float Behind => Radius * (float)Math.Cos(theta);
        /// <summary>Pass 18 audio: wake crossings so far and abs(omega)/maxOmega at the last one.</summary>
        public int wakeCrossings; public float crossOmega01;

        public void BeginPass()
        {
            scoring = true; gatesCleared = false; awaitingExit = false; passDone = false;
            nextBuoy = 0; passBuoys = 0; pendingHalf = 0; roundedBalls = 0; message = "THROUGH THE GATES"; entryMin = exitMin = float.MaxValue; entryAng = exitAng = 0f;
        }

        /// <summary>One fixed step of the pendulum (sim.ts fixed()). steer: +1 = left. power scales lean torque.</summary>
        public void Step(float dt, float steer, float power = 1f)
        {
            if (fallen) return;
            prevTheta = theta;
            float k = steer == 0 ? 4.2f : 22f;
            lean += (steer - lean) * (1f - (float)Math.Exp(-k * dt));
            float L = ropeM;
            float ease = RopePhysicsMul();   // b10.21: table — ease mid-line, tax 35+
            float torque = lean * leanPower * cut * power * ease / Math.Max(8f, L);
            bool cuttingIn = Math.Abs(theta) > 0.22f && Sign(lean) != 0 && Sign(lean) != Sign(theta);
            bool goingOut = Math.Abs(theta) > 0.15f && Sign(lean) == Sign(theta) && lean != 0;
            if (cuttingIn) torque *= 1.55f;
            if (goingOut) torque *= 1.22f;
            if (Math.Abs(theta) < 0.18f && Math.Abs(omega) > 0.6f) torque *= 1.12f;
            float drag = 1.02f + Math.Abs(lean) * 0.22f;
            omega += (torque - drag * omega) * dt;
            float maxO = maxOmega * hold * (1f + (ease - 1f) * 0.5f);
            omega = Clamp(omega, -maxO, maxO);
            theta += omega * dt;
            if (Math.Abs(theta) > 1.62f && omega * theta > 0f) omega = 0f;   // reach v2: pinned at full width the swing stops (outward omega kept running = no reach at the ball, late cut)
            theta = Clamp(theta, -1.62f, 1.62f);
            { float st = StretchTarget, d = STRETCH_RATE * dt; stretchS += Clamp(st - stretchS, -d, d); }
            if (prevTheta * theta < 0) { omega *= 1.06f; wakeCrossings++; crossOmega01 = Math.Abs(omega) / Math.Max(0.01f, maxOmega); } // wake crossing pop (pass 18: counted for the EdgeChange sound)
            if (scoring)
            {
                bool blow = Math.Abs(omega) > maxO * (0.96f + edge * 0.04f) && Math.Abs(lean) > 0.92f && Math.Abs(theta) > 1.48f;
                if (blow) Fall("Caught an edge");
            }
        }

        float Width() => Math.Abs(x) + Math.Abs(lean) * REACH * reach;

        /// <summary>sim.ts checkCourse(). Call after Step with the skier's TS-frame position.</summary>
        public void CheckCourse(float sx, float sz)
        {
            float px = x; prevZ = z; x = sx; z = sz;
            if (!scoring || fallen || passDone) return;
            // entry gates: a timing cue on the run-in, then judged over a short window either side of the gate line (closest
            // approach to the centreline, from the segment between steps), so a skier who visibly goes through is never called out
            if (!gatesCleared)
            {
                if (z > GATE_Z) message = z > GATE_CUE_M ? $"LINE UP · GATES IN {z:0} m" : "CUT NOW ▸ THROUGH THE GATES";
                if (z <= GATE_Z + GATE_WIN && prevZ > GATE_Z - GATE_WIN) { entryMin = Math.Min(entryMin, SegMinAbsX(px, prevZ, x, z, GATE_Z + GATE_WIN, GATE_Z - GATE_WIN)); entryAng = Math.Max(entryAng, CrossAngle(px, prevZ, x, z)); }
                if (prevZ >= GATE_Z - GATE_WIN && z < GATE_Z - GATE_WIN)
                {
                    float tolE = GateTol(GATE_ENTRY_HALF + GateExtraM(), entryAng); bool ok = entryMin <= tolE;
                    GateLog?.Invoke($"entry gate: closest {entryMin:0.00} m from centre (tolerance {tolE:0.00}, crossing {entryAng:0}°, gate balls ±{GATE_BALL:0.00} at z {GATE_Z:0}) skier now x {x:0.00} z {z:0.00} -> {(ok ? "THROUGH" : "MISSED")}");
                    if (ok) { gatesCleared = true; message = "BUOY 1 · RIGHT"; }
                    else if (missGatesEnds) { Fall("Missed the entrance gates"); return; }
                    else gatesCleared = true;
                }
            }
            if (gatesCleared && nextBuoy < 6)
            {
                int i = nextBuoy; float bz = BuoyZ(i), bx = BuoyX(i);
                float dist = (float)Math.Sqrt((x - bx) * (x - bx) + (z - bz) * (z - bz));
                int side = Sign(bx);
                float need = BuoyClearNeed();   // b10.21: stepped grace; 38+ full 11.5 m
                bool outsideNow = x * side >= need;
                if (dist < 0.28f && !outsideNow) { Fall($"Hit buoy {i + 1}"); return; }
                if (prevZ >= bz && z < bz)
                {
                    // b9 / b10.21: credit when path is outside the ball. Grace from ROPE_BUOY_GRACE_M; 38+ cliff = full offset.
                    bool outside = x * side >= need;
                    lastBallX = x * side;
                    GateLog?.Invoke($"buoy {i + 1}: ball x {bx:0.0} z {bz:0}, skier x {x:0.00} -> {(outside ? "ROUNDED" : "MISSED")}, balls {passBuoys + (outside ? 0.5f : 0f):0.0}");
                    if (outside)
                    {
                        passBuoys += 0.5f; pendingHalf = side; nextBuoy = i + 1; roundedBalls++;
                        if (nextBuoy >= 6) { awaitingExit = true; message = "EXIT GATES"; }
                        else message = nextBuoy % 2 == 0 ? $"BUOY {nextBuoy + 1} · RIGHT" : $"BUOY {nextBuoy + 1} · LEFT";
                    }
                    else { Fall($"Missed buoy {i + 1}"); return; }
                }
            }
            if (pendingHalf != 0 && x * pendingHalf < 0)   // IWWF 8.09: the second half of a buoy is earned on the cut back across the course (for ball 6, before the exit gate)
             { passBuoys += 0.5f; pendingHalf = 0; }
            // exit gates: same window judgement; a full pass is 6 balls + the exit = 6.0 (the half for ball 6 is paid here)
            if (awaitingExit)
            {
                if (z <= EXIT_Z + GATE_WIN && prevZ > EXIT_Z - GATE_WIN) { exitMin = Math.Min(exitMin, SegMinAbsX(px, prevZ, x, z, EXIT_Z + GATE_WIN, EXIT_Z - GATE_WIN)); exitAng = Math.Max(exitAng, CrossAngle(px, prevZ, x, z)); }
                if (prevZ >= EXIT_Z - GATE_WIN && z < EXIT_Z - GATE_WIN)
                {
                    float tolX = GateTol(GATE_EXIT_HALF + GateExtraM(), exitAng); bool ok = exitMin <= tolX;
                    GateLog?.Invoke($"exit gate: closest {exitMin:0.00} m from centre (tolerance {tolX:0.00}, crossing {exitAng:0}°, gate balls ±{GATE_BALL:0.00} at z {EXIT_Z:0}) skier now x {x:0.00} z {z:0.00} -> {(ok ? "THROUGH" : "MISSED")}, balls {passBuoys + (ok && pendingHalf != 0 ? 0.5f : 0f):0.0}");
                    if (ok)
                    {
                        if (pendingHalf != 0) { passBuoys += 0.5f; pendingHalf = 0; }
                        awaitingExit = false; passDone = true; scoring = false;
                        message = "FULL PASS!";
                    }
                    else Fall(x > 0 ? "Exit gate missed right" : "Exit gate missed left");   // 8.09b: right side keeps 6, left side (never crossed back) stays 5.5
                }
            }
        }

        // gate judging. The gate balls sit ±1.25 m off the centreline; the window is ±4 m along the course either side of the gate line.
        public const float GATE_BALL = 1.25f, GATE_WIN = 4f, GATE_ENTRY_HALF = GATE_BALL, GATE_EXIT_HALF = GATE_BALL, GATE_CUE_M = 14f;   // b9: between the gate balls (was ±3.6 m)
        public float lastBallX;
        float entryMin = float.MaxValue, exitMin = float.MaxValue, entryAng, exitAng;
        // b9: the skier must pass between the gate balls; an angled crossing gets at most 0.5 m extra.
        public const float GATE_ANGLED_HALF = GATE_BALL + 0.5f, GATE_ANGLE_DEG = 15f;   // b9 (Scott): ±0.5 m extra on a real angled crossing only (was ±4.5 m: missed gates scored)
        static float CrossAngle(float x0, float z0, float x1, float z1) { float dz = Math.Abs(z1 - z0); return dz < 1e-4f ? 90f : (float)(Math.Atan(Math.Abs(x1 - x0) / dz) * 180.0 / Math.PI); }
        /// <summary>b10.6: angled crossing keeps +0.5 m over the (possibly eased) straight window.</summary>
        public static float GateTol(float straight, float angDeg) => angDeg >= GATE_ANGLE_DEG ? Math.Max(GATE_ANGLED_HALF, straight + 0.5f) : straight;
        public static Action<string> GateLog;
        public float testExitX = float.NaN;
        /// <summary>Smallest |x| on the segment (x0,z0)-(x1,z1) clipped to zHi >= z >= zLo.</summary>
        static float SegMinAbsX(float x0, float z0, float x1, float z1, float zHi, float zLo)
        {
            float t0 = 0f, t1 = 1f, dz = z1 - z0;
            if (Math.Abs(dz) > 1e-6f)
            {
                float ta = (zHi - z0) / dz, tb = (zLo - z0) / dz; float lo = Math.Min(ta, tb), hi = Math.Max(ta, tb);
                t0 = Math.Max(t0, lo); t1 = Math.Min(t1, hi); if (t0 > t1) return float.MaxValue;
            }
            float a = x0 + (x1 - x0) * t0, b = x0 + (x1 - x0) * t1;
            return a * b <= 0f ? 0f : Math.Min(Math.Abs(a), Math.Abs(b));
        }

        public void Fall(string why)
        {
            if (fallen) return;
            fallen = true; scoring = false; message = why;
        }

        public void Recover()
        {
            fallen = false; theta = 0.05f; omega = 0; lean = 0; stretchS = 0f;
        }

        /// <summary>Autopilot tuned in Tools/ai_tune.py (clears 6 balls + exit at 15–32 off, 34 mph).
        /// Returns steer (+1 = left).</summary>
        public float AutoSteer(bool inPass)
        {
            // b9 retune (Tools/ai_tune_b9.py) for the strict ball judge: a gate shot (swing out on the ball-2 side, cut 10 m before
            // the gate line so the skier crosses the gate centre on an angle toward ball 1), then aim 13.5 m out at each ball.
            // Clears 15 .. 41 off at 34 mph; at 41 off the skier passes ~0.18 m outside the balls (game theta limit 1.62 in the course, reach v2).
            float target = 0f;
            if (inPass && !gatesCleared && scoring) target = (z > GATE_Z + 10f ? -0.8f : 1f) * 13.5f * 0.7f * Sign(BuoyX(0));
            if (inPass && gatesCleared && nextBuoy < 6) target = BuoyX(nextBuoy) / BUOY_OFFSET * 13.5f;
            if (inPass && awaitingExit && !float.IsNaN(testExitX)) target = testExitX;   // capture/test: deliberately wide exit
            float xp = Lateral + LateralVel * 0.3f;
            return Clamp(-0.3f * (target - xp), -1f, 1f);
        }

        public string Score() => Fmt(last.buoys) + " @ " + ROPE_LABEL[last.rope];
    }
}

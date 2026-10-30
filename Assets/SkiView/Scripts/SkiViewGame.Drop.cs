using UnityEngine;

namespace SkiView
{
    /// <summary>b10.7/b10.25 turnaround after a COMPLETED (clean) pass (Scott Oct 23): 1) boat reaches the MOUTH of the turn island still approaching,
    /// 2) skier pulls out wide to the TREE side while the boat HOLDS at the mouth, 3) lets go and floats, 4) boat idles a tree-side
    /// circle in open water (never into the island / beach) while SAME LENGTH / SHORTEN, 5) deep-water start from float (no teleport),
    /// 6) boat tows him round the island in the channel into the next pass the other way. Spin-turn lakes
    /// and jump mode keep their own turn; a missed / failed pass keeps the normal fall and restart.</summary>
    public partial class SkiViewGame
    {
        [Header("b9.1a drop turnaround")] public bool dropTurnaround = true;
        public float dropCircleMinR = 12f, dropIdleSpeed = 4.5f, dropPullOutM = 9f, dropPassAbeamM = 10f, dropSlackS = 1.6f, dropChoiceAtS = 3f;   // James turnaround spec (estimates tuned short)
        int dropPh;                  // 0 none, 1 pull-out, 2 floating while the boat circles, 3 boat back, waiting for the choice, 4 slack coming out
        float dropR = 12f, dropSlackT; bool dropWait; float dropWaitS0; Vector3 dropF0;
        // b10.7 (Scott): drop at the MOUTH — open water just before the turn island, NOT mid-channel (path hugs ~26–33 m).
        // Ski View let-go targets ~55–60 m from island centre. Mouth band = (channelM, mouthM]; freeze boatS during pull-out.
        public float dropIslandTurnDeg = 18f, dropIslandMaxM = 120f, dropIslandMouthM = 62f, dropIslandChannelM = 50f;
        Vector3 dwsResid; float floatYaw; bool floatYawSet;
        public float dwsResidS = 2.6f, slackMps = 2.0f, slackMaxS = 6f;
        float dropT, dropSide = 1f, dropPhi, dwsBlendT = -1f;
        Vector3 dropP0, dropF, dropC, dropU, floatPos, floatVel;
        int dropPrevRope, dropShortRope; bool dropChosen, dropShorten = true, dropChoiceEscaped;
        public int DropPhase => dropPh;
        /// <summary>The SAME LENGTH / SHORTEN choice is open (touch buttons or keys).</summary>
        public bool RopeChoiceOpen => dropPh >= 2 && !dropChosen && !dropChoiceEscaped && mode == Mode.Sink && actionT > dropChoiceAtS;
        /// <summary>b10.27 T24 Gap B: Esc cancelled the choice UI back to the results plate (still need ChooseRope to continue).</summary>
        public bool RopeChoiceEscaped => dropChoiceEscaped;
        /// <summary>Drop is past let-go and the rope length is not settled yet (open or Esc-cancelled).</summary>
        public bool RopeChoicePending => dropPh >= 2 && !dropChosen && mode == Mode.Sink;
        public bool ShortenHighlighted => dropShorten;
        public bool CanShorten => dropShortRope != dropPrevRope;
        public string SameLabel => SkierSim.ROPE_LABEL[dropPrevRope];
        public string ShortLabel => SkierSim.ROPE_LABEL[dropShortRope];
        public void HighlightRope(bool shorten) { if (RopeChoiceOpen) dropShorten = shorten && CanShorten; }
        /// <summary>b10.27 T24 Gap B: Esc / Back leaves the choice UI → results plate (never soft-lock).</summary>
        public void CancelRopeChoice()
        {
            if (dropPh < 2 || dropChosen || mode != Mode.Sink) return;
            dropChoiceEscaped = true;
            banner = "CHOOSE NEXT · SAME OR SHORTEN FROM RESULTS"; bannerT = 2.5f;
        }
        public void ChooseRope(bool shorten)
        {
            if (dropPh < 2 || dropChosen) return;
            dropChosen = true; dropChoiceEscaped = false; dropShorten = shorten && CanShorten;
            sim.ropeIndex = dropShorten ? dropShortRope : dropPrevRope; ropeTarget = SkierSim.ROPE_M[sim.ropeIndex];
            banner = (dropShorten ? "SHORTEN · " : "SAME LENGTH · ") + SkierSim.ROPE_LABEL[sim.ropeIndex] + " · " + MphTag; bannerT = 2.5f;
            skipTurn = true;   // the rest of the circle runs at x3; the skip pill jumps to the pick-up
        }

        bool DropSupported => dropTurnaround && !jumpMode && !path.SpinAhead(boatS, 220f);

        /// <summary>Called at a completed pass instead of the plain towed turn. Returns false when this lake / mode keeps the old turn.</summary>
        bool BeginDrop(bool clean)
        {
            if (!clean || !DropSupported) return false;
            dropPrevRope = sim.last.rope; dropShortRope = sim.ropeIndex;   // EndPass already advanced it on a clean pass: undo until he chooses
            sim.ropeIndex = dropPrevRope; ropeTarget = SkierSim.ROPE_M[dropPrevRope];
            float turn = 0f;
            for (int k = 1; k <= 20; k++) turn += Vector3.Cross(boatFwd, path.Tangent(boatS + k * 12f)).y;
            dropSide = turn >= 0f ? 1f : -1f;   // + = the boat turns right round the island: the trees are on the right
            dropPh = 1; dropT = 0f; dropWait = true; dropWaitS0 = boatS; dropF0 = path.Tangent(boatS); dropChosen = false; dropChoiceEscaped = false; dropShorten = CanShorten; turnaround = true;
            Debug.Log($"[SkiView] drop: pull out to the {(dropSide > 0 ? "right" : "left")} (tree side) at boat {boatPos}");
            return true;
        }

        /// <summary>Pull-out steering toward the tree side.</summary>
        public bool DropPullingOut => dropPh == 1 && !dropWait;
        float DropSteer() => Mathf.Clamp(-0.5f * (dropSide * dropPullOutM - sim.Lateral), -1f, 1f);

        void DropTick(float dt)
        {
            if (dropPh != 1) return;
            if (dropWait)
            {
                // b10.7: release in the mouth band. If EndPass already put us inside the channel, walk boatS BACK to ~60 m.
                float bend = Vector3.Angle(dropF0, path.Tangent(boatS + 12f));
                float gone = boatS - dropWaitS0; if (gone < 0f) gone += path.Length;
                float islandDist = DropIslandAheadM();
                bool atMouth = islandDist <= dropIslandMouthM && islandDist > dropIslandChannelM && (gone > 12f || islandDist <= dropIslandMouthM);
                bool turnStarting = bend >= dropIslandTurnDeg && islandDist <= dropIslandMouthM + 20f && islandDist > dropIslandChannelM && gone > 12f;
                bool tooFar = gone >= dropIslandMaxM;
                bool pastChannel = islandDist <= dropIslandChannelM;
                if (!atMouth && !turnStarting && !tooFar && !pastChannel) return;
                // b10.25: land exactly in the mouth band. pastChannel → rewind; still outside mouth (tooFar
                // used to freeze ~85 m out when BeginDrop was ~145 m ahead) → walk FORWARD to ~61 m.
                if (pastChannel) DropRewindToMouth();
                else if (islandDist > dropIslandMouthM) DropAdvanceToMouth();
                dropWait = false;
                dropP0 = path.Position(boatS); dropF = path.Tangent(boatS); dropF.y = 0f; dropF.Normalize();
                boatPos = dropP0; boatFwd = dropF;   // lock pose at the mouth (boatS frozen for pull-out + idle circle)
                islandDist = DropIslandAheadM();
                Debug.Log($"[SkiView] drop: pull-out at mouth gone={gone:0}m bend={bend:0}° island={islandDist:0.0}m boat={boatPos}");
            }
            dropT += dt;
            // during pull-out the boat stays frozen at the mouth so we do not drive into the island
            if (dropSide * sim.Lateral >= dropPullOutM - 0.5f || dropT > 2.8f)
            {
                // let go: he glides on and settles; the boat idles a TREE-SIDE circle in open water (never into the island)
                dropPh = 2; mode = Mode.Sink; actionT = 0f; spinSink = false; floatYawSet = false;
                floatPos = skierPos; floatVel = skierVel; floatVel.y = 0f;
                dropP0 = boatPos; dropF = boatFwd; dropF.y = 0f; dropF.Normalize();
                Vector3 r = Vector3.Cross(Vector3.up, dropF);   // boat's right
                // Tree-side circle: centre toward trees (dropSide) so the loop turns AWAY from the island.
                // (James port circle at a right-hand island turn would cut into the island / then get nudged onto the beach.)
                float skTree = Vector3.Dot(floatPos + floatVel / 2.5f - dropP0, r * dropSide);
                dropR = Mathf.Clamp(Mathf.Max(dropCircleMinR, (skTree + dropPassAbeamM) * 0.45f), dropCircleMinR, 20f);
                dropU = -r * dropSide;                 // from centre to release point
                dropC = dropP0 - dropU * dropR;        // = dropP0 + r*dropSide*dropR
                dropPhi = 0f;
                DropKeepCircleInChannel();
                sim.omega = 0f; sim.lean = 0f;
                banner = "LET GO · " + (CanShorten ? "SAME LENGTH OR SHORTEN?" : "SAME LENGTH"); bannerT = 2.5f;
            }
        }

        /// <summary>Boat on its idle circle back to the release point (boatS stays frozen there). True while it owns the boat.</summary>
        bool DropBoatStep(float dt)
        {
            if (dropPh < 1) return false;
            if (dropPh == 1)
            {
                if (dropWait) return false;   // still approaching the mouth on the baked loop
                // b10.7: freeze at the mouth while he pulls out to the tree side
                boatPos = dropP0; boatFwd = dropF; return true;
            }
            float ramp = Mathf.Clamp01(actionT / 1.5f);   // comes off the throttle first
            float v = Mathf.Lerp(path.Speed(boatS) * 0.5f, dropIdleSpeed, ramp);
            if ((boatPos - floatPos).magnitude < 16f) v *= 0.6f;   // neutral as it passes him
            if (dropPh == 2)
            {
                dropPhi += v * dt / Mathf.Max(4f, dropR);
                if (dropPhi >= Mathf.PI * 2f) { dropPhi = Mathf.PI * 2f; dropPh = 3; }
            }
            if (dropPh == 4)
            {
                if (dropSlackT > 0.3f) boatS = path.Wrap(boatS + slackMps * dt);   // idles away at ~2.5 mph to take up the slack
                boatPos = path.Position(boatS); boatFwd = path.Tangent(boatS); return true;
            }
            float c = Mathf.Cos(dropPhi), s = Mathf.Sin(dropPhi);
            boatPos = dropC + dropR * (dropU * c + dropF * s);
            boatFwd = (-dropU * s + dropF * c).normalized;
            return true;
        }

        void DropFloatStep(float dt)
        {
            actionT += dt;
            floatVel *= Mathf.Exp(-2.5f * dt);
            floatPos += floatVel * dt; floatPos.y = 0f;
            skierPrev = skierPos; skierPos = floatPos; skierVel = (skierPos - skierPrev) / dt;
            if (autopilot && !dropChosen && actionT > dropChoiceAtS + 1f) ChooseRope(dropShorten);   // headless / demo: the default
            if (dropPh == 3 && dropChosen) { dropPh = 4; dropSlackT = 0f; banner = "HANDLE IN · SLACK OUT"; bannerT = 1.5f; }
            if (dropPh == 4) dropSlackT += dt;
            bool taut = false; float behindAng = 9f;
            if (dropPh == 4)
            {
                // James P5: the line comes tight, then he is towed slowly (tractrix: the taut line drags him in behind the boat)
                Vector3 pyl = Pylon(); pyl.y = 0f; Vector3 d = floatPos - pyl; d.y = 0f; float R = RopeHorizFor(ropeTarget) + SkierSim.ARM_REACH;
                if (d.magnitude >= R - 0.4f) { taut = true; if (d.magnitude > R) { floatPos = pyl + d.normalized * R; skierPos = floatPos; } }
                behindAng = Vector3.Angle(d, -boatFwd) * Mathf.Deg2Rad;
            }
            if (dropPh == 4 && dropSlackT >= dropSlackS && ((taut && behindAng < 0.7f) || dropSlackT >= slackMaxS))
            {
                // James P5: the boat has idled straight on along its line until the slack is out; hit it from there
                sim.Recover(); sim.ropeM = ropeTarget;
                Vector3 vec = floatPos - Pylon(); vec.y = 0f; Vector3 right = Vector3.Cross(Vector3.up, boatFwd);
                sim.theta = Mathf.Clamp(Mathf.Atan2(-Vector3.Dot(vec, right), Vector3.Dot(vec, -boatFwd)), -1.0f, 1.0f); sim.omega = 0f;
                dropPh = 0; dwsBlendT = 0f; skipTurn = false;
                mode = Mode.DeepStart; dwsFrom = 0.1f; throttle = 0.1f; actionT = 0f; dwsPatchDone = false; banner = "DEEP-WATER START"; bannerT = 2f;
            }
        }

        /// <summary>During the DWS the skier eases from where he floated onto the towed line (no snap).</summary>
        void DwsBlend(float dt)
        {
            if (dwsBlendT < 0f) return;
            // the gap between where he floats and the towed line decays (the rope swings him in behind the boat while he plows);
            // the towed motion itself is never blended, so there is no sideways slide against the pull
            if (dwsBlendT == 0f) { dwsResid = floatPos - skierPos; dwsResid.y = 0f; }
            dwsBlendT += dt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dwsBlendT / dwsResidS));
            skierPos += dwsResid * (1f - k); skierPos.y = 0f;
            if (k >= 1f) dwsBlendT = -1f;
        }

        /// <summary>James P2/P3: extra depth while floating after a drop: dunk to head-under at ~1 s, one 15 cm vest overshoot,
        /// settled by ~3 s, then a +-3 cm bob at 0.4 Hz. Metres, + = deeper.</summary>
        float FloatBobM(float t)
        {
            float dunk = 0.30f * Mathf.Exp(-Mathf.Pow((t - 1.0f) / 0.35f, 2f));
            float over = -0.15f * Mathf.Exp(-Mathf.Pow((t - 1.9f) / 0.45f, 2f));
            float amb = 0.03f * Mathf.Sin(2f * Mathf.PI * 0.4f * t) * Mathf.Clamp01((t - 2.2f) / 1f);
            return dunk + over + amb;
        }
        /// <summary>Facing while floating: turns slowly (40 deg/s) toward the boat, with a +-10 deg drift over 8 s.</summary>
        Vector3 FloatFacing(Vector3 want, float t, float dt)
        {
            float w = Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg + 10f * Mathf.Sin(2f * Mathf.PI * t / 8f);
            if (!floatYawSet) { floatYaw = w; floatYawSet = true; }
            floatYaw = Mathf.MoveTowardsAngle(floatYaw, w, 40f * dt);
            return Quaternion.Euler(0f, floatYaw, 0f) * Vector3.forward;
        }
        public bool InDropFloat => dropPh >= 2 && mode == Mode.Sink;

        /// <summary>RETRY PASS / RESTART: always the plain set-up, even mid-turnaround.</summary>
        public void RetryPass() { FlushSlalomSet(); DropReset(); SkipToSetup(); }
        void DropReset() { dropPh = 0; dwsBlendT = -1f; floatYawSet = false; }
        /// <summary>Skip pill after the rope choice: jump the boat to the pick-up (no teleport of the skier). False = not in a drop.</summary>
        bool DropSkip()
        {
            if (dropPh < 2) return false;
            if (!dropChosen) ChooseRope(dropShorten);
            if (dropPh == 2) { dropPhi = Mathf.PI * 2f; dropPh = 3; boatPos = dropP0; boatFwd = dropF; snapCamNext = true; }
            return true;
        }
        bool DropRopeHidden => dropPh >= 2 && dropPh < 4;   // he let go: no line between the boat and his hands while it circles


        /// <summary>b10.7/b10.25: mouth target ~61 m from the ahead island centre (open water, not channel ~26–33 m).</summary>
        float DropMouthTargetM => dropIslandMouthM - 1f;

        /// <summary>b10.7: if the pass ended already inside the channel, walk boatS backward along the baked path until we are
        /// ~61 m from the nearest ahead island (open water at the mouth). No-op when centres are unknown.</summary>
        void DropRewindToMouth()
        {
            float target = DropMouthTargetM;
            float bestS = boatS; float bestAbs = Mathf.Abs(DropIslandAheadM() - target);
            for (int i = 1; i <= 40; i++)
            {
                float s = path.Wrap(boatS - i * 3f);
                Vector3 pos = path.Position(s); Vector3 fwd = path.Tangent(s);
                float d = DropIslandDistFrom(pos, fwd);
                float err = Mathf.Abs(d - target);
                if (err < bestAbs) { bestAbs = err; bestS = s; }
                if (d >= dropIslandMouthM + 5f) break;   // walked back past the mouth band
            }
            boatS = bestS;
            Debug.Log($"[SkiView] drop: rewound to mouth island≈{DropIslandDistFrom(path.Position(boatS), path.Tangent(boatS)):0.0}m");
        }

        /// <summary>b10.25: when BeginDrop is still well outside the mouth (tooFar used to freeze ~85 m out), walk boatS
        /// FORWARD along the baked path to ~61 m from the ahead island. Stops before the channel band.</summary>
        void DropAdvanceToMouth()
        {
            float target = DropMouthTargetM;
            float bestS = boatS; float bestAbs = Mathf.Abs(DropIslandAheadM() - target);
            for (int i = 1; i <= 50; i++)
            {
                float s = path.Wrap(boatS + i * 3f);
                Vector3 pos = path.Position(s); Vector3 fwd = path.Tangent(s);
                float d = DropIslandDistFrom(pos, fwd);
                float err = Mathf.Abs(d - target);
                if (err < bestAbs) { bestAbs = err; bestS = s; }
                if (d <= dropIslandChannelM) break;   // entered channel — keep the best open-water sample
            }
            boatS = bestS;
            Debug.Log($"[SkiView] drop: advanced to mouth island≈{DropIslandDistFrom(path.Position(boatS), path.Tangent(boatS)):0.0}m");
        }
        float DropIslandDistFrom(Vector3 pos, Vector3 fwd)
        {
            float best = 999f;
            if (islandCentres == null) return best;
            for (int i = 0; i + 1 < islandCentres.Length; i += 2)
            {
                var ic = new Vector3(islandCentres[i], 0f, islandCentres[i + 1]);
                Vector3 d = ic - pos; d.y = 0f;
                if (Vector3.Dot(d, fwd) < -10f) continue;
                best = Mathf.Min(best, d.magnitude);
            }
            return best;
        }
        /// <summary>Metres from the boat to the nearest turn-island centre ahead on the loop (or a path-curvature estimate).</summary>
        float DropIslandAheadM()
        {
            float best = 999f;
            if (islandCentres != null)
                for (int i = 0; i + 1 < islandCentres.Length; i += 2)
                {
                    var ic = new Vector3(islandCentres[i], 0f, islandCentres[i + 1]);
                    Vector3 d = ic - boatPos; d.y = 0f;
                    if (Vector3.Dot(d, boatFwd) < -10f) continue;
                    best = Mathf.Min(best, d.magnitude);
                }
            if (best < 900f) return best;
            Vector3 a = path.Position(boatS), b = path.Position(boatS + 40f), c = path.Position(boatS + 80f);
            Vector3 ab = b - a, bc = c - b; ab.y = 0f; bc.y = 0f;
            if (ab.sqrMagnitude < 1f || bc.sqrMagnitude < 1f) return 999f;
            float cross = Vector3.Cross(ab.normalized, bc.normalized).y;
            if (Mathf.Abs(cross) < 0.08f) return 999f;
            Vector3 mid = (a + c) * 0.5f; Vector3 inside = Vector3.Cross(Vector3.up, (c - a).normalized) * Mathf.Sign(cross);
            var est = mid + inside * 35f;
            return Vector3.Distance(new Vector3(boatPos.x, 0f, boatPos.z), new Vector3(est.x, 0f, est.z));
        }
        /// <summary>b10.7: keep the idle circle in open water on the tree side — clear of every turn island AND of dry ground.
        /// Never shove the centre across the channel onto the outer beach (that was the b10.5 beach failure).</summary>
        void DropKeepCircleInChannel()
        {
            float needFromIsland = 68f + dropR;   // path hugs islands at ~26–33 m; circle centre stays well outside
            if (islandCentres != null)
                for (int i = 0; i + 1 < islandCentres.Length; i += 2)
                {
                    var ic = new Vector3(islandCentres[i], 0f, islandCentres[i + 1]);
                    Vector3 toC = dropC - ic; toC.y = 0f;
                    if (toC.magnitude >= needFromIsland || toC.sqrMagnitude < 0.01f) continue;
                    // Push away from the island, but prefer the tree-side / upstream open water — not across onto the outer bank.
                    Vector3 tree = Vector3.Cross(Vector3.up, dropF) * dropSide;
                    Vector3 away = (toC.normalized * 0.55f + tree.normalized * 0.35f - dropF * 0.25f).normalized;
                    dropC = ic + away * needFromIsland;
                    DropRelockCircleFromRelease();
                    Debug.Log($"[SkiView] drop: circle nudged clear of island → R={dropR:0.0} C={dropC}");
                }
            // Shrink if any sample on the circle sits on land (GroundY > -WATER_MIN).
            for (int guard = 0; guard < 6; guard++)
            {
                bool land = false;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    Vector3 p = dropC + dropR * (dropU * Mathf.Cos(a) + dropF * Mathf.Sin(a));
                    if (GroundY(p) > -WATER_MIN) { land = true; break; }
                }
                if (!land) break;
                dropR = Mathf.Max(8f, dropR * 0.82f);
                Vector3 tree = Vector3.Cross(Vector3.up, dropF) * dropSide;
                dropC = dropP0 + tree.normalized * dropR;   // pull centre toward trees / release
                DropRelockCircleFromRelease();
                Debug.Log($"[SkiView] drop: circle shrunk off land → R={dropR:0.0} C={dropC}");
            }
        }
        void DropRelockCircleFromRelease()
        {
            Vector3 radial = dropP0 - dropC; radial.y = 0f;
            if (radial.sqrMagnitude < 1f) return;
            dropR = Mathf.Clamp(radial.magnitude, 8f, 22f);
            dropU = radial.normalized;
            dropC = dropP0 - dropU * dropR;
            Vector3 wantF = Vector3.Cross(dropU, Vector3.up).normalized;
            if (Vector3.Dot(wantF, dropF) < 0f) wantF = -wantF;
            dropF = wantF;
        }

        void DropKeys()
        {
            if (!RopeChoiceOpen) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.L)) HighlightRope(false);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Alpha2)) HighlightRope(true);
            if (Input.GetKeyDown(KeyCode.S)) ChooseRope(true);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) ChooseRope(dropShorten);
        }
    }
}

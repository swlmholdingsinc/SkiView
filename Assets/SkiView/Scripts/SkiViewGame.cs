using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkiView
{
    /// <summary>N1 greybox game loop: boat on the baked lake loop at 34 mph, one skier (Wade Cox) on the rope,
    /// scored slalom pass, island turnaround with rope shortening, reverse-direction pass, repeat.</summary>
    [ExecuteAlways]
    public partial class SkiViewGame : MonoBehaviour
    {
        public enum Mode { Turn, Pass, Fallen, DeepStart, Jump, Sink, Trick }

        [Header("Scene refs (wired by SkiViewBuilder)")]
        public BoatPath path;
        public float[] islandCentres;   // b10.5: turn-island xz pairs from venue (drop mouth / circle clearance)
        void EnsureIslandCentres()
        {
            if (islandCentres != null && islandCentres.Length >= 2) return;
            var ta = Resources.Load<TextAsset>("SkiViewIslandCentres");
            if (ta == null) { Debug.LogWarning("[SkiView] SkiViewIslandCentres.json missing from Resources"); return; }
            string key = string.IsNullOrEmpty(lakeId) ? "skiview" : lakeId;
            islandCentres = ParseIslandCentresJson(ta.text, key);
            if (islandCentres == null || islandCentres.Length < 2)
                Debug.LogWarning("[SkiView] no islandCentres for lakeId=" + key);
            else
                Debug.Log($"[SkiView] islandCentres loaded for {key}: {islandCentres.Length / 2} islands");
        }
        static float[] ParseIslandCentresJson(string json, string key)
        {
            // Tiny extractor: "key": [n,n,...]  (Resources file is a flat object of float arrays)
            string needle = "\"" + key + "\":";
            int i = json.IndexOf("\"" + key + "\":");
            if (i < 0) return null;
            int lb = json.IndexOf('[', i); int rb = json.IndexOf(']', lb);
            if (lb < 0 || rb < 0) return null;
            var parts = json.Substring(lb + 1, rb - lb - 1).Split(',');
            var list = new System.Collections.Generic.List<float>();
            foreach (var p in parts)
            {
                var s = p.Trim(); if (s.Length == 0) continue;
                if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) list.Add(v);
            }
            return list.Count >= 2 ? list.ToArray() : null;
        }
        public Transform boat, skier, skierBody, ski, handle;
        public Transform boatTow, boatWake, boatSprayPoint;   // SkiBoat.fbx empties
        public ParticleSystem boatSpray, boatSheet; float lastBoatSprayT = -1f;   // pass 6: droplets + thin transom sheet (streaks, no puffs)
        public SkierRig rig;
        SkiSpray spray; float lastSprayT = -1f;
        // pass 4: whole-body lean from the force balance on the ski edge (one line from the edge to the shoulders)
        float headPrev = float.NaN, headRate, leanRightDeg, rollLimit = 70f;
        public float LeanDeg => leanRightDeg;       // + = leaning toward the skier's right
        public float HeadingRate => headRate;        // rad/s, + = turning right
        public float turnGain = 1.25f, ropePullG = 1.6f, ropeSideFull = 0.45f, maxLeanDeg = 70f;            // Mixamo rider (null = capsule placeholder)
        Vector3 handleBar = Vector3.right;
        float lastVisT = -1f;
        // pass 12: water reacting to falls and deep-water starts. Crash sheet + droplets, plow spray off the ski tip and body,
        // and shader splash sources (_SVSplash: x, z, start time, strength; strength > 0 = foam patch + ring ripples,
        // < 0 = continuous small ripples around someone floating)
        public ParticleSystem crashSheet, crashDrops, plowSpray;
        public ParticleSystem crashMist;   // pass 14: fine mist hanging over a crash / landing
        public float fallImpactT = 0.85f;
        readonly Vector4[] splash = new Vector4[6];
        float lastFxT = -1f, plowAcc;
        bool impactDone, dwsPatchDone;
        Vector3 impactPos, skiFloatPos, skiFloatDir;
        static readonly int ID_Splash = Shader.PropertyToID("_SVSplash");
        public LineRenderer rope;
        public Camera cam;

        [Header("Tuning")]
        public string lakeId = "skiview", lakeName = "Ski View";   // pass 15: per-lake scenes
        public string[] lakeScenes, lakeNames;
        public float startProgress = -232f;
        /// <summary>b9 (Scott Oct 23): at Ski View slalom runs start at the south end (starting dock in front of the warming hut) and the
        /// first pass heads north (-1). Jump runs keep the southbound approach (Holly's layout); other lakes unchanged.</summary>
        int StartDir => lakeId == "skiview" && !jumpOnly && SkiViewArgs.Get("mode") != "jump" ? -1 : 1;   // spawn just out of the north island turn, southbound (session 2 lake)
        public bool autopilot = false;
        public float timeScale = 1f;

        [Header("Runtime (read-only)")]
        public Mode mode = Mode.Turn;
        public float boatS;
        public int passDir = 1;
        public int passes, cleanPasses;
        public float bestBuoys;
        public SkierSim sim = new SkierSim();
        public string banner = "";
        public float bannerT;
        public bool skipTurn;
        public float simTime;

        const float FIXED = 1f / 60f;
        const float PYLON_BACK = -0.36f;   // SkiBoat.fbx TowPoint is 0.36 m forward of the pivot (CG)
        float acc, prevHeading, fallT, ropeTarget, wakeT, skiT;
        // pass 11: deep-water start + fall sequence (Mixamo batch 3). actionT = seconds into the current fall / start;
        // throttle scales the boat speed (it idles back after a fall and accelerates through the start)
        float actionT, throttle = 1f;
        public float dwsDuration = 3.2f, fallDuration = 3.4f;
        public float ActionT => actionT;
        Vector3 fallFace = Vector3.forward;
        void EnterFall()
        {
            if (SkiViewClinic.Active && SkiViewClinic.ActiveLessonId == "deep_water_start") { SkiViewClinic.NotifyDeepStartOutcome(this, false); }

            DropReset(); EndPass(false); mode = Mode.Fallen; fallT = fallDuration; actionT = 0f; impactDone = false;
            Vector3 vh = skierVel; vh.y = 0f; fallFace = vh.sqrMagnitude > 1f ? vh.normalized : boatFwd;
        }
        /// <summary>Capture / debug: make the skier fall now (plays the fall, treading water, then the deep-water start).</summary>
        public void BeginFall(string why) { sim.Fall(why); EnterFall(); }
        // pass 18 turnaround (after a full pass): the boat idles back, the skier settles and sinks still holding the handle, then
        // the boat powers up into a deep-water start and pulls him round the turn island and into the next pass.
        [Header("Turnaround")] public float sinkDuration = 3.4f, sinkIdleThrottle = 0.12f;
        bool spinSink, spinSeen;   // pass 19: spin turn (Holly v2): float through the boat's spin, deep-water start once the line is tight
        public bool SpinTurn => spinSink;
        bool spinCall; public string CoachLine { get; private set; } float coachT; public bool CoachOn => coachT > 0f;
        public void SetCoach(string line, float secs = 6f) { CoachLine = line ?? ""; coachT = secs; }
        /// <summary>b10.18 Clinic deep_water_start: drop into DeepStart from current ski position.</summary>
        public void BeginClinicDeepStart()
        {
            DropReset(); mode = Mode.DeepStart; dwsFrom = 0.25f; throttle = 0.25f; actionT = 0f; dwsPatchDone = false;
            PlaceSkier(); dwsBlendT = 0f; DwsBlend(1e-4f); skierPrev = skierPos;
            banner = "CLINIC · DEEP-WATER START"; bannerT = 2.5f;
        }
        const float SPIN_IDLE = 2.2f;
        public bool turnaround;   // inside a post-pass turnaround (sink -> DWS -> island turn -> set-up); a tap skips to the set-up
        float dwsFrom = 0.25f; bool snapCamNext;
        float SinkDepth => mode == Mode.Sink ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 2.5f, actionT)) : 0f;
        /// <summary>Depth for the in-water pose: the sink before a DWS, then the DWS itself.</summary>
        float WaterDepth => mode == Mode.Sink ? SinkDepth : DwsDepth;
        bool WaterPose => mode == Mode.Sink || mode == Mode.DeepStart;
        float DwsDepth => mode == Mode.DeepStart ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, dwsDuration, actionT)) : 0f;
        bool inited;
        Vector3 skierPos, skierPrev, skierVel, boatPos, boatFwd;
        readonly Vector4[] wake = new Vector4[40];
        readonly Vector4[] skiTrail = new Vector4[24];
        int wakeN, skiN;
        Vector3 camVel;
        public bool cameraFollow = true;
        float ropeSag, ropeSagVel, lastRopeT = -1f;

        static readonly int ID_Time = Shader.PropertyToID("_SVTime"), ID_Wake = Shader.PropertyToID("_WakePts"),
            ID_WakeN = Shader.PropertyToID("_WakeCount"), ID_Ski = Shader.PropertyToID("_SkiPts"),
            ID_SkiN = Shader.PropertyToID("_SkiCount"), ID_Boat = Shader.PropertyToID("_BoatPos"), ID_BoatFwd = Shader.PropertyToID("_BoatFwd");

        void OnEnable() { if (Application.isPlaying) Init(); }

        public void Init()
        {
            SkierSim.GateLog = m => Debug.Log("[SkiView] gates: " + m);
            if (path == null || path.Count == 0) return;
            EnsureIslandCentres();   // b10.7: scenes predate the field; load from Resources so mouth/channel math works in WebGL
            LoadCameraPref();   // pass 17
            boatS = path.FindS(StartDir, startProgress);
            boatPos = path.Position(boatS); boatFwd = path.Tangent(boatS);
            prevHeading = Heading(boatFwd);
            sim = new SkierSim { ropeIndex = 0, ropeM = SkierSim.ROPE_M[0], theta = 0.25f };
            ropeTarget = sim.ropeM;
            ApplySetup();   // b9: saved ski / starting rope / speed
            SkiViewClinic.TryResumePending(this);
            mode = Mode.Turn; passes = 0; cleanPasses = 0; bestBuoys = 0; simTime = 0; wakeN = 0; skiN = 0;
            actionT = 0f; throttle = 1f; DropReset();
            PlaceSkier(); skierPrev = skierPos;
            if (Application.isPlaying) { mode = Mode.DeepStart; throttle = 0.25f; dwsPatchDone = false; }
            if (jumpOnly && jumpRamp != null) { jumpMode = true; SetJumpGear(true); JumpGeometry(); }
            if (rampJumpModeOnly && jumpRamp != null && !jumpMode) jumpRamp.gameObject.SetActive(false);   // T26
            for (int i = 0; i < splash.Length; i++) splash[i] = Vector4.zero;   // the game opens with a deep-water start (captures skip it)
            banner = lakeName.ToUpperInvariant() + " · " + SkiViewBranding.SkierName + " · " + SkierSim.ROPE_LABEL[sim.ropeIndex] + " · " + SkiView.UI.SkiViewUI.SpeedMph(RunMph).ToUpperInvariant(); bannerT = 4f;
            if (Application.isPlaying) { SkiView.UI.SkiViewUI.Ensure(this)?.ShowCamToast(camMode); SkiAudio.Ensure(this); }   // pass 18: UI Toolkit overlay + run-start view toast
            inited = true;
            ApplyArgs();   // pass 19: URL / command-line capture params
            ApplyVisuals(true);
        }

        static float Heading(Vector3 f) => Mathf.Atan2(f.x, f.z);

        void Update()
        {
            if (!Application.isPlaying) return;
            if (!inited) Init();
            ReadInputAndStep(Time.deltaTime);
            ApplyVisuals(false);
        }

        float steerInput; readonly System.Collections.Generic.HashSet<int> uiFingers = new System.Collections.Generic.HashSet<int>(); bool mouseOnUi;
        void ReadInputAndStep(float dt)
        {
            float s = 0;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) s += 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) s -= 1;
            // pass 19 touch guard: a finger that lands on a kit button (CAM, pause, skip pill, menus) is owned by the UI until it lifts -
            // it never steers or taps, even when it slides off the button. Without the kit, the old top strip stays the HUD.
            bool kit = SkiView.UI.SkiViewUI.Instance != null; float strip = kit ? 1.01f : 0.82f;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && (t.position.y > Screen.height * strip || SkiView.UI.SkiViewUI.Blocks(t.position))) uiFingers.Add(t.fingerId);
                bool owned = uiFingers.Contains(t.fingerId);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) uiFingers.Remove(t.fingerId);
                if (owned || SkiView.UI.SkiViewUI.Blocks(t.position)) continue;
                s += t.position.x < Screen.width * 0.5f ? 1 : -1;
            }
            if (Input.GetMouseButtonDown(0)) mouseOnUi = Input.mousePosition.y > Screen.height * strip || SkiView.UI.SkiViewUI.Blocks(Input.mousePosition);
            if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) mouseOnUi = false;
            bool mouseFree = !mouseOnUi && !SkiView.UI.SkiViewUI.Blocks(Input.mousePosition);
            if (Input.touchCount == 0 && Input.GetMouseButton(0) && Input.mousePosition.y < Screen.height * strip && mouseFree)
                s += Input.mousePosition.x < Screen.width * 0.5f ? 1 : -1;
            DropKeys();
            if (Input.GetKeyDown(KeyCode.P)) autopilot = !autopilot;
            if (Input.GetKeyDown(KeyCode.C) && !(SkiView.UI.SkiViewUI.Instance?.MenuOpen ?? false)) CycleCamera();
            // pass 14: jump button = Space or a tap / click below the HUD strip (cut on the approach, pop on the ramp)
            bool menu = SkiView.UI.SkiViewUI.Instance?.MenuOpen ?? false;
            bool tap = !menu && !RopeChoiceOpen && (Input.GetKeyDown(KeyCode.Space) || (Input.touchCount == 0 && Input.GetMouseButtonDown(0) && mouseFree));
            for (int i = 0; i < Input.touchCount; i++) { var t = Input.GetTouch(i); if (t.phase == TouchPhase.Began && !uiFingers.Contains(t.fingerId) && !menu) tap = true; }
            if (tap && mode == Mode.Jump) JumpPress();
            // b9.1: a tap / steer no longer skips the island turn (it teleported the boat to the run-in); only the SKIP pill does
            else if (tap && jumpMode && mode == Mode.Turn) JumpArm();   // jump mode: an early press is kept for the next approach (no skip, which blocked the run)
            else if (Input.GetKeyDown(KeyCode.Space) && mode == Mode.Turn) skipTurn = true;
            steerInput = Mathf.Clamp(s, -1, 1);
            Advance(dt);
        }

        /// <summary>Advance the simulation by dt seconds of real time (fixed 60 Hz substeps).</summary>
        public void Advance(float dt)
        {
            float scale = timeScale * (mode != Mode.Pass && mode != Mode.Jump && skipTurn ? 3f : 1f);
            acc += Mathf.Min(dt, 0.1f) * scale;
            while (acc >= FIXED) { FixedStep(FIXED); acc -= FIXED; }
        }

        /// <summary>Used by the headless capture script.</summary>
        public void SimulateFor(float seconds)
        {
            if (!inited) Init();
            int n = Mathf.RoundToInt(seconds / FIXED);
            for (int i = 0; i < n; i++) FixedStep(FIXED);
            ApplyVisuals(true);
        }

        void FixedStep(float dt)
        {
            simTime += dt;
            bannerT -= dt; coachT -= dt;
            // throttle: idles back after a fall, ramps up through the deep-water start
            if (mode == Mode.DeepStart) throttle = Mathf.Lerp(dwsFrom, 1f, Mathf.SmoothStep(0f, 1f, actionT / dwsDuration));
            else if (mode == Mode.Sink) throttle = Mathf.Lerp(throttle, sinkIdleThrottle, 1f - Mathf.Exp(-dt / 0.55f));   // off the throttle
            else throttle = Mathf.MoveTowards(throttle, mode == Mode.Fallen ? 0.3f : 1f, 0.4f * dt);
            float v = path.Speed(boatS) * SpeedMul * throttle;
            if (mode == Mode.Sink && spinSink) v = spinSeen && !path.InSpin(boatS) ? Mathf.Min(path.Speed(boatS), SPIN_IDLE) : path.Speed(boatS);
            if (jumpMode && path.Speed(boatS) > 14f) v = jumpBoatKmh / 3.6f * throttle;   // jump: 54 km/h on the straights
            if (trickMode && path.Speed(boatS) > 8f) v = trickBoatKmh / 3.6f * throttle;   // tricks: skier-chosen km/h
            if (!DropBoatStep(dt)) { boatS = path.Wrap(boatS + v * dt); boatPos = path.Position(boatS); boatFwd = path.Tangent(boatS); }   // b9.1a: the boat circles back off the loop
            float h = Heading(boatFwd);
            float dpsi = Mathf.DeltaAngle(prevHeading * Mathf.Rad2Deg, h * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            prevHeading = h;

            float prog = path.Progress(boatS);
            int dir = path.Dir(boatS);
            Vector3 cf = path.CourseFwd(dir);
            float lat = Vector3.Cross(cf, boatPos).y;
            bool onLine = Mathf.Abs(lat) < 6f && Vector3.Dot(boatFwd, cf) > 0.97f;
            JumpBoatAndTrigger(dt, onLine, dir);   // pass 13: jump lane offset + jump-run trigger (jump mode only)
            TrickBoatAndTrigger(dt, onLine, dir);  // b10.11 tricks Phase A

            // start a pass when the boat is lined up and at speed on the run-in to the pre-gates (184.5 m from centre).
            // Session 2 lake is 670 m tip to tip, so the straight run-in is ~30 m (was 55 m on the 840 m trace).
            if (!jumpMode && !trickMode && mode == Mode.Turn && onLine && prog > -214f && prog < -passWindowLate)
            {
                passDir = dir; mode = Mode.Pass; skipTurn = false; turnaround = false;
                sim.ropeM = ropeTarget;
                sim.BeginPass();
                banner = $"PASS {passes + 1} · {SkierSim.ROPE_LABEL[sim.ropeIndex]} · {MphTag} · {Bound(cf)}"; bannerT = 2.5f;
            }

            // spin-turn end: the boat comes off the plane into the idle stretch -> the skier lets the boat settle and sinks
            if (mode == Mode.Turn && path.spins != null && path.Speed(boatS) < 6f && path.SpinAhead(boatS, 20f) && !path.InSpin(boatS - 25f))
            { BeginSink(); spinSink = true; spinSeen = false; spinCall = true;   // pass 19 (tutorial-script 5b/5c): call plate SPIN TURN -> SET UP · <rope> OFF + the skip pill, no banner
              if (PlayerPrefs.GetInt("sv.coach.spin", 0) == 0) { CoachLine = "The boat swings round and takes you back down the course."; coachT = 6f; PlayerPrefs.SetInt("sv.coach.spin", 1); PlayerPrefs.Save(); } }

            DropTick(dt);
            if (dropPh >= 2 && mode == Mode.Sink) DropFloatStep(dt);
            else if (mode == Mode.Sink && spinSink)
            {
                // spin turn: floats on the slack line (a rope can only pull) while the boat pivots round him, then deep-water starts
                actionT += dt; sim.omega = 0f; sim.lean = 0f;
                if (path.InSpin(boatS)) spinSeen = true;
                skierPrev = skierPos;
                Vector3 pyl = Pylon(); Vector3 vec = skierPos - pyl; vec.y = 0f; float dist = vec.magnitude;
                if (actionT < 1.2f) { sim.theta *= Mathf.Exp(-1.6f * dt); PlaceSkier(); }
                else
                {
                    if (dist > sim.Radius) { vec *= sim.Radius / dist; skierPos = pyl + vec; skierPos.y = 0f; }
                    Vector3 right = Vector3.Cross(Vector3.up, boatFwd);
                    sim.theta = Mathf.Atan2(-Vector3.Dot(vec, right), Vector3.Dot(vec, -boatFwd));
                }
                skierVel = (skierPos - skierPrev) / dt;
                bool taut = dist >= sim.Radius * 0.9f && Mathf.Abs(sim.theta) < 1.2f;
                if ((actionT >= sinkDuration && spinSeen && !path.InSpin(boatS) && taut) || actionT > 75f)
                {
                    sim.theta = Mathf.Clamp(sim.theta, -1.2f, 1.2f); spinSink = false;
                    mode = Mode.DeepStart; dwsFrom = Mathf.Clamp01(v / Mathf.Max(0.5f, path.Speed(boatS))); actionT = 0f; dwsPatchDone = false; banner = "DEEP-WATER START"; bannerT = 2f;
                }
            }
            else if (mode == Mode.Sink)
            {
                // glide and sink: the line goes slack as the boat comes off the plane, he drifts in straight behind and settles
                // into the water in the start crouch, handle in hand
                actionT += dt;
                sim.theta *= Mathf.Exp(-1.6f * dt); sim.omega = 0f; sim.lean = 0f;
                skierPrev = skierPos; PlaceSkier(); KeepSkierOnWater(); skierVel = (skierPos - skierPrev) / dt;
                if (actionT >= sinkDuration) { mode = Mode.DeepStart; dwsFrom = throttle; actionT = 0f; dwsPatchDone = false; banner = "DEEP-WATER START"; bannerT = 2f; }
            }
            else if (mode == Mode.DeepStart)
            {
                // deep-water start: the rope comes tight and the skier is dragged up straight behind the boat
                actionT += dt;
                skierPrev = skierPos; PlaceSkier(); DwsBlend(dt); KeepSkierOnWater(); skierVel = (skierPos - skierPrev) / dt;
                if (actionT >= dwsDuration) {
                    if (SkiViewClinic.Active && SkiViewClinic.ActiveLessonId == "deep_water_start")
                        SkiViewClinic.NotifyDeepStartOutcome(this, true);
                    mode = Mode.Turn; banner = "UP! · NEXT PASS AFTER THE TURN"; bannerT = 2f; }
            }
            else if (mode == Mode.Jump) JumpStep(dt);
            else if (mode == Mode.Trick) { sim.theta = Mathf.Clamp(sim.theta - dpsi, -1.5f, 1.5f); TrickStep(dt); }
            else if (mode != Mode.Fallen)
            {
                // the skier lags the boat's heading change (rope frame rotates under him)
                bool inPass = mode == Mode.Pass;
                float thMax = inPass ? 1.62f : 1.4f;   // reach v2: in the course use the sim's own limit (1.4 capped 41 off at 11.38 m, inside the 11.5 m balls)
                sim.theta = Mathf.Clamp(sim.theta - dpsi, -thMax, thMax);
                float auto = sim.AutoSteer(inPass);
                float steer;
                if (inPass) steer = autopilot ? auto : steerInput;
                else steer = (steerInput != 0 && !autopilot) ? Mathf.Clamp(steerInput * 0.6f + auto * 0.4f, -1, 1) : auto;
                if (DropPullingOut) steer = DropSteer();   // b9.1a: pull out wide to the tree side
                sim.Step(dt, steer, inPass ? 1f : 0.8f);
                PlayBoxWall(dt);   // pass 19
                skierPrev = skierPos;
                PlaceSkier();
                if (!inPass) KeepSkierOnWater();
                skierVel = (skierPos - skierPrev) / dt;
                {
                    Vector3 vh = skierVel; vh.y = 0f;
                    if (vh.magnitude > 2f)
                    {
                        float hd = Mathf.Atan2(vh.x, vh.z);
                        if (!float.IsNaN(headPrev)) { float d = Mathf.DeltaAngle(headPrev * Mathf.Rad2Deg, hd * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt; headRate = Mathf.Lerp(headRate, Mathf.Clamp(d, -4f, 4f), 1f - Mathf.Exp(-dt / 0.12f)); }
                        headPrev = hd;
                    }
                }
                if (inPass)
                {
                    Vector3 pcf = path.CourseFwd(passDir);
                    Vector3 right = Vector3.Cross(Vector3.up, pcf);
                    float a = Vector3.Dot(skierPos, pcf);
                    sim.CheckCourse(Vector3.Dot(skierPos, right), -(a + SkierSim.COURSE_LENGTH * 0.5f));
                    if (sim.passDone) { EndPass(true); if (!SkiViewClinic.Active && !BeginDrop(sim.last.clean) && (path.SpinAhead(boatS, 220f) || !jumpMode)) turnaround = true; }   // b9.1 / b10.17: Clinic skips rope-choice drop
                    else if (sim.fallen) EnterFall();
                    else if (path.Progress(boatS) > 250f || Vector3.Dot(boatFwd, pcf) < 0.5f) { sim.Fall("Ran out of course"); EnterFall(); }
                }
            }
            else
            {
                fallT -= dt; actionT += dt;
                skierVel *= Mathf.Exp(-2.5f * dt);
                skierPos += skierVel * dt;
                // the boat comes back round and the skier gets up with a deep-water start
                if (fallT <= 0)
                {
                    // b10.2: get up from where he fell - the rope swings him in behind the boat during the DWS (was a 14 m snap onto the line)
                    sim.Recover(); floatPos = skierPos; floatPos.y = 0f;
                    { Vector3 vec = floatPos - Pylon(); vec.y = 0f; Vector3 rr = Vector3.Cross(Vector3.up, boatFwd); sim.theta = Mathf.Clamp(Mathf.Atan2(-Vector3.Dot(vec, rr), Vector3.Dot(vec, -boatFwd)), -1.0f, 1.0f); sim.omega = 0f; }
                    mode = Mode.DeepStart; dwsFrom = 0.25f; actionT = 0f; dwsPatchDone = false; PlaceSkier(); dwsBlendT = 0f; DwsBlend(1e-4f); skierPrev = skierPos; banner = "DEEP-WATER START"; bannerT = 2f; }
            }

            // rope shortening while the boat is slow in the turn (1.5 s reel-in)
            if ((mode == Mode.Turn || mode == Mode.Sink) && Mathf.Abs(sim.ropeM - ropeTarget) > 0.001f && v < 10f)
                sim.ropeM = Mathf.MoveTowards(sim.ropeM, ropeTarget, Mathf.Abs(SkierSim.ROPE_M[0] - SkierSim.ROPE_M[1]) / 1.5f * dt);

            // wake + spray trails for the water shader
            wakeT -= dt; skiT -= dt;
            if (wakeT <= 0)
            {
                wakeT = 0.25f;
                Vector3 stern = boatPos + boatFwd * (boatWake != null ? boatWake.localPosition.z : -3.0f);   // WakeEmitter (transom, waterline)
                Push(wake, ref wakeN, new Vector4(stern.x, v, stern.z, simTime));
            }
            if (skiT <= 0 && mode != Mode.Fallen)
            {
                skiT = 0.1f;
                Push(skiTrail, ref skiN, new Vector4(skierPos.x, Mathf.Min(1f, skierVel.magnitude / 20f), skierPos.z, simTime));
            }
        }

        static void Push(Vector4[] arr, ref int n, Vector4 p)
        {
            for (int i = arr.Length - 1; i > 0; i--) arr[i] = arr[i - 1];
            arr[0] = p; n = Mathf.Min(n + 1, arr.Length);
        }

        static void EmitBurst(ParticleSystem ps, Vector3 at, int n, Vector3 baseVel, float spreadH, float upMin, float upMax, float sizeMul, System.Random rnd)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                // pass 15: softer core - births spread over a 0.6 m disc and fewer near-zero lateral speeds (was a 0.25 m point
                // with sqrt-distributed spread, which stacked many translucent sheets into one bright blob)
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, h = Mathf.Lerp(0.35f, 1f, Mathf.Sqrt((float)rnd.NextDouble())) * spreadH;
                ep.position = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.6f * Mathf.Sqrt((float)rnd.NextDouble());
                ep.velocity = baseVel + new Vector3(Mathf.Cos(a) * h, Mathf.Lerp(upMin, upMax, (float)rnd.NextDouble()), Mathf.Sin(a) * h);
                ep.startSize = ps.main.startSize.constantMax * Mathf.Lerp(0.5f, 1f, (float)rnd.NextDouble()) * sizeMul;
                ps.Emit(ep, 1);
            }
        }
        readonly System.Random fxRnd = new System.Random(1234);

        /// <summary>Pass 12: splash sources for the water shader, crash / plow particles, and the ski floating free after a fall.</summary>
        void WaterFx()
        {
            float bdt = lastFxT < 0f ? 0f : Mathf.Clamp(simTime - lastFxT, 0f, 1.5f); lastFxT = simTime;
            RampWaterFx(bdt);
            for (int i = 0; i < splash.Length; i++) if (splash[i].w > 0f && simTime - splash[i].z > 8f) splash[i] = Vector4.zero;
            splash[1] = Vector4.zero; splash[3] = Vector4.zero;   // continuous sources are re-set every frame
            Vector3 flat = skierPos; flat.y = 0f;
            if (mode == Mode.Fallen)
            {
                if (!impactDone && actionT >= fallImpactT)
                {
                    impactDone = true;
                    impactPos = flat + fallFace * 0.9f;
                    splash[0] = new Vector4(impactPos.x, impactPos.z, simTime, 1f); splash[2] = Vector4.zero;
                    // the crash: a sheet thrown up and forward, and droplets everywhere
                    EmitBurst(crashSheet, impactPos, 260, fallFace * 2.2f, 2.4f, 2.0f, 4.5f, 1f, fxRnd);
                    EmitBurst(crashDrops, impactPos, 420, fallFace * 2.8f, 4.0f, 2.5f, 6.5f, 1f, fxRnd);
                    EmitBurst(crashMist, impactPos + Vector3.up * 0.4f, 45, fallFace * 1.4f, 2.6f, 0.6f, 2.2f, 1f, fxRnd);
                    // the ski pops free and floats off to one side
                    Vector3 side = Vector3.Cross(Vector3.up, fallFace);
                    skiFloatPos = impactPos - side * 1.5f - fallFace * 0.2f; skiFloatDir = (fallFace - side * 0.6f).normalized;
                }
                if (actionT > 1.6f) splash[1] = new Vector4(flat.x, flat.z, simTime - (actionT - 1.6f), -1f);   // treading: rings around him
                if (impactDone && JumpGearOn) JumpSkisFloat(flat);
                if (impactDone && ski != null && !JumpGearOn)
                {
                    float tt = actionT - fallImpactT;
                    Vector3 drift = skiFloatDir * (0.35f * (1f - Mathf.Exp(-tt * 0.6f)));
                    Vector3 rest = skiFloatPos + drift + Vector3.up * (0.02f + Mathf.Sin(simTime * 2.3f) * 0.012f);
                    float pop = Mathf.Clamp01(tt / 0.45f);
                    Vector3 from = flat + Vector3.up * 0.3f;
                    Vector3 p = Vector3.Lerp(from, rest, pop) + Vector3.up * (Mathf.Sin(pop * Mathf.PI) * 1.1f);
                    Quaternion floatRot = Quaternion.LookRotation(skiFloatDir) * Quaternion.Euler(Mathf.Sin(simTime * 1.7f) * 2f, 0f, 4f + Mathf.Sin(simTime * 2.3f) * 3f);
                    Quaternion spin = Quaternion.LookRotation(skiFloatDir) * Quaternion.Euler(-60f + 60f * pop, pop * 220f, 0f);
                    ski.SetPositionAndRotation(p, pop < 1f ? spin : floatRot);
                    if (tt >= 0.45f && splash[2].w == 0f) splash[2] = new Vector4(rest.x, rest.z, simTime, 0.35f);   // the ski lands
                }
            }
            if (mode == Mode.Sink && actionT > 1.6f) splash[3] = new Vector4(flat.x, flat.z, simTime - (actionT - 1.6f), -0.8f);   // settled, floating
            if (mode == Mode.DeepStart)
            {
                float dd = DwsDepth;
                if (actionT < 0.9f) splash[3] = new Vector4(flat.x, flat.z, simTime - actionT, -0.8f);   // floating, waiting for the pull
                if (!dwsPatchDone && actionT >= 0.9f) { dwsPatchDone = true; splash[4] = new Vector4(flat.x, flat.z, simTime, 0.7f); }   // churned patch where he sat
                // plowing: the ski and body push a bow wave until the ski planes
                float plow = Mathf.Clamp01(Mathf.InverseLerp(0.5f, 1.2f, actionT)) * Mathf.Clamp01(dd * 1.6f) * Mathf.Clamp01(skierVel.magnitude / 4f);
                plowAcc += plow * 900f * bdt * (fpShown ? 0.35f : 1f);   // first person: the eye is right over the chest spray
                int n = Mathf.FloorToInt(plowAcc); plowAcc -= n;
                if (n > 0)
                {
                    Vector3 fwd = Flat(skierVel.sqrMagnitude > 0.5f ? skierVel : boatFwd);
                    Vector3 tip = ski != null ? ski.position + ski.forward * 0.75f : flat + fwd * 0.9f; tip.y = 0.05f;
                    Vector3 vel = skierVel * 0.7f; vel.y = 0f;
                    EmitBurst(plowSpray, tip, n * 2 / 3, vel + fwd * 1.2f, 2.6f, 1.2f, 3.2f, 1f, fxRnd);           // off the ski tip
                    if (!fpShown) EmitBurst(plowSpray, flat + fwd * 0.2f + Vector3.up * 0.05f, n / 3, vel, 2.0f, 0.8f, 2.2f, 1.2f, fxRnd);   // off the chest and knees (not under a first-person eye)
                }
            }
            if (!Application.isPlaying && bdt > 0f)
            {
                int k = Mathf.CeilToInt(bdt * 60f);
                for (int i = 0; i < k; i++) { if (crashSheet) crashSheet.Simulate(bdt / k, false, false, false); if (crashDrops) crashDrops.Simulate(bdt / k, false, false, false); if (crashMist) crashMist.Simulate(bdt / k, false, false, false); if (plowSpray) plowSpray.Simulate(bdt / k, false, false, false); }
            }
        }
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        void EndPass(bool clean)
        {
            passes++;
            var res = sim.Finish(clean);   // single source of truth for banner, results screen and progression
            bestBuoys = Mathf.Max(bestBuoys, res.buoys + res.rope * 6);
            string score = sim.Score() + " (" + sim.Breakdown(res) + ")";
            if (SkiViewClinic.Active)
            {
                SkiViewClinic.EndPass(this, !clean, res.buoys, sim.gatesCleared);
                banner = (res.clean || clean ? "CLINIC · PASS" : "CLINIC · FALL") + " · " + score;
                bannerT = 4f;
                if (mode == Mode.Pass) mode = Mode.Turn;
                return;   // no rope shorten in Clinic submode
            }
            NoteSlalomRecord(res);
            if (res.clean)
            {
                cleanPasses++;
                sim.ropeIndex = Mathf.Min(sim.ropeIndex + 1, SkierSim.ROPE_M.Length - 1);
                banner = $"FULL PASS · {score} · Next: {SkierSim.ROPE_LABEL[sim.ropeIndex]} · {MphTag}";
            }
            else banner = $"{sim.message} · {score} · Next: {SkierSim.ROPE_LABEL[sim.ropeIndex]} · {MphTag}";
            bannerT = 4f;
            ropeTarget = SkierSim.ROPE_M[sim.ropeIndex];
            if (mode == Mode.Pass) mode = Mode.Turn;
        }

        /// <summary>Full pass done: start the turnaround (boat idles back, skier sinks holding the handle).</summary>
        void BeginSink()
        {
            mode = Mode.Sink; actionT = 0f; turnaround = true; skipTurn = false;
            banner += "   ·   tap to skip the turn"; bannerT = 4f;
        }

        /// <summary>Skip the turnaround: put the boat on the run-in for the next pass (opposite direction), skier up and set up behind.</summary>
        public void SkipToSetup()
        {
            if (DropSkip()) { SkipCount++; return; }
            int nd = -passDir; if (nd == 0) nd = 1; SkipCount++; DropReset();
            mode = Mode.Turn; actionT = 0f; throttle = 1f; skipTurn = false; turnaround = false; spinSink = false; spinCall = false;
            sim.Recover(); sim.ropeM = ropeTarget; sim.theta = 0.05f;
            boatS = path.FindS(nd, Mathf.Max(-212f, -(passWindowLate + 22f)));
            boatPos = path.Position(boatS); boatFwd = path.Tangent(boatS); prevHeading = Heading(boatFwd);
            PlaceSkier(); skierPrev = skierPos - boatFwd * (path.Speed(boatS) * FIXED); skierVel = boatFwd * path.Speed(boatS);
            headPrev = float.NaN; headRate = 0f; wakeN = 0; skiN = 0; lastRopeT = -1f;
            snapCamNext = true;
            banner = $"SET UP · {SkierSim.ROPE_LABEL[sim.ropeIndex]} · {MphTag}"; bannerT = 2f;
        }

        Vector3 Pylon() => boatPos - boatFwd * PYLON_BACK;

        // Boat heel from the real lateral acceleration (v^2 * curvature), a fraction of the coordinated-turn angle, capped at 9 deg
        // and eased in time. The old -curv*60 (curv = sine of the heading change over 12 m) heeled the hull ~40 deg in the island
        // turns, so the gunwale dipped under the water plane and the boat looked like it was sinking.
        const float BANK_MAX = 9f, BANK_GAIN = 0.35f, BANK_TAU = 0.45f;
        float bankDeg, bankT = -1f;
        float BoatBank(float curv)
        {
            float k = Mathf.Asin(Mathf.Clamp(curv, -1f, 1f)) / 12f;   // 1/m, signed
            float v = path.Speed(boatS) * SpeedMul * throttle;
            float want = -Mathf.Clamp(Mathf.Atan(v * v * Mathf.Abs(k) / 9.81f) * Mathf.Rad2Deg * BANK_GAIN, 0f, BANK_MAX) * Mathf.Sign(k);
            bool first = bankT < 0f || simTime < bankT; float dt = first ? 0f : Mathf.Min(simTime - bankT, 0.5f); bankT = simTime;
            bankDeg = first ? want : Mathf.Lerp(bankDeg, want, 1f - Mathf.Exp(-dt / BANK_TAU));
            return bankDeg;
        }

        // The skier stays on open water: if the rope swings him over the shallows (shore band / beach), he is pulled back toward the
        // boat's own track (the trail point straight behind the pylon is always water) until there is at least WATER_MIN of depth.
        const float WATER_MIN = 1.0f;
        Terrain waterTerrain; bool waterTerrainLooked;
        float GroundY(Vector3 p)
        {
            if (!waterTerrainLooked || waterTerrain == null) { waterTerrain = Terrain.activeTerrain; waterTerrainLooked = true; }
            return waterTerrain == null ? -10f : waterTerrain.SampleHeight(p) + waterTerrain.transform.position.y;
        }
        void KeepSkierOnWater()
        {
            if (GroundY(skierPos) <= -WATER_MIN) return;
            Vector3 trail = path.Position(path.Wrap(boatS - PYLON_BACK - sim.Radius)); trail.y = 0f;
            Vector3 from = skierPos; float lo = 0f, hi = 1f;
            if (GroundY(trail) > -WATER_MIN) { trail = Pylon() - boatFwd * sim.Behind; trail.y = 0f; }
            for (int i = 0; i < 8; i++) { float m = (lo + hi) * 0.5f; if (GroundY(Vector3.Lerp(from, trail, m)) > -WATER_MIN) lo = m; else hi = m; }
            skierPos = Vector3.Lerp(from, trail, hi); skierPos.y = 0f;
            if (mode == Mode.Turn || mode == Mode.Pass)
            {
                // re-derive the rope angle so the pendulum carries on from the clamped spot
                Vector3 rel = skierPos - Pylon(); rel.y = 0f; Vector3 right = Vector3.Cross(Vector3.up, boatFwd);
                float th = Mathf.Atan2(-Vector3.Dot(rel, right), -Vector3.Dot(rel, boatFwd));   // SkierSim: Lateral = -R sin(theta)
                if (Mathf.Abs(th) < Mathf.Abs(sim.theta)) { sim.theta = th; sim.omega = 0f; }
            }
        }

        void PlaceSkier()
        {
            Vector3 right = Vector3.Cross(Vector3.up, boatFwd);
            skierPos = Pylon() - boatFwd * sim.Behind + right * sim.Lateral;
            skierPos.y = 0;
        }

        public void ApplyVisuals(bool snapCamera)
        {
            TickGateDebug(); TickBuoyVis();
            snapCamera |= snapCamNext; snapCamNext = false;
            if (path == null || boat == null) return;
            // boat: slight bow-up trim, banked into turns
            float curv = Vector3.Cross(path.Tangent(boatS - 6f), path.Tangent(boatS + 6f)).y;
            float bob = Mathf.Sin(simTime * 2.1f) * 0.02f;
            // pivot = static waterline under the CG: lifts a few cm and trims bow-up as it gets on plane
            float plane = Mathf.InverseLerp(4f, 15.2f, path.Speed(boatS));
            boat.SetPositionAndRotation(boatPos + Vector3.up * (0.06f * plane + bob),
                Quaternion.LookRotation(boatFwd) * Quaternion.Euler(-(1.0f + 2.5f * plane) + Mathf.Sin(simTime * 1.3f) * 0.3f, 0, BoatBank(curv)));
            if (boatSpray != null && boatSprayPoint != null)
            {
                float bdt = lastBoatSprayT < 0f ? 0f : Mathf.Clamp(simTime - lastBoatSprayT, 0f, 1.5f); lastBoatSprayT = simTime;
                // droplets: fine streaked spray kicked up and back from the prop wash
                boatSpray.transform.SetPositionAndRotation(boatSprayPoint.position, Quaternion.LookRotation((Vector3.up * 0.55f - boatFwd).normalized, Vector3.up));
                var bm = boatSpray.main; bm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f + 1.5f * plane, 2.2f + 3.2f * plane);
                var bsh = boatSpray.shape; bsh.shapeType = ParticleSystemShapeType.Cone; bsh.angle = 16f; bsh.radius = 0.35f; bsh.radiusThickness = 1f;
                var bem = boatSpray.emission; bem.rateOverTime = 650f * plane * SkiViewQuality.Spray;   // T6: preset spray scale
                // thin sheet: a narrow curtain of streaks peeling off the full transom width, arcing back into the water
                if (boatSheet != null)
                {
                    boatSheet.transform.SetPositionAndRotation(boatSprayPoint.position + Vector3.up * 0.02f, Quaternion.LookRotation((Vector3.up * 0.28f - boatFwd).normalized, Vector3.up));
                    var sm2 = boatSheet.main; sm2.startSpeed = new ParticleSystem.MinMaxCurve(2.0f + 2.0f * plane, 3.0f + 3.0f * plane);
                    var ssh2 = boatSheet.shape; ssh2.shapeType = ParticleSystemShapeType.Box; ssh2.scale = new Vector3(1.9f, 0.02f, 0.04f); ssh2.randomDirectionAmount = 0.04f;
                    var sem2 = boatSheet.emission; sem2.rateOverTime = 1400f * plane * SkiViewQuality.Spray;
                }
                if (!Application.isPlaying && bdt > 0f)
                {
                    int n = Mathf.CeilToInt(bdt * 60f);
                    for (int i = 0; i < n; i++) { boatSpray.Simulate(bdt / n, false, false, false); if (boatSheet != null) boatSheet.Simulate(bdt / n, false, false, false); }
                }
            }

            Vector3 pyl = boatTow != null ? boatTow.position : Pylon() + Vector3.up * SkierSim.PYLON_Y;   // rope on the TowPoint empty
            Vector3 vel = skierVel; vel.y = 0;
            Vector3 face = vel.sqrMagnitude > 1f ? vel.normalized : boatFwd;
            if (skier != null)
            {
                if (JumpRootPose()) { }
                else if (mode == Mode.Fallen)
                {
                    // pass 11: the fall clip plays upright on the water, then the skier sinks to treading depth (head and shoulders out)
                    float sink = Mathf.Lerp(0.05f, 1.32f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1.9f, actionT)));
                    skier.SetPositionAndRotation(skierPos + Vector3.down * sink, Quaternion.LookRotation(fallFace));
                }
                else if (WaterPose)
                {
                    // sitting back in the water facing the boat, ski tip up; rises and comes upright as the boat pulls (DWS), or
                    // the reverse as he settles in after a pass (sink): from skiing upright to the cannonball, handle held
                    float dd = WaterDepth;
                    Vector3 tb = pyl - skierPos; tb.y = 0; tb = tb.sqrMagnitude > 1e-4f ? tb.normalized : boatFwd;
                    if (mode == Mode.Sink) { Vector3 ff = Flat(face); tb = Vector3.Slerp(ff, tb, Mathf.Clamp01(actionT / 0.8f)).normalized; }
                    float bobM = 0f;
                    if (InDropFloat) { bobM = FloatBobM(actionT); if (actionT > 0.8f) tb = FloatFacing(tb, actionT, Mathf.Clamp(simTime - lastVisT, 0f, 0.1f)); }   // James P2/P3 float
                    skier.SetPositionAndRotation(skierPos + Vector3.down * (0.57f * dd + bobM), Quaternion.LookRotation(tb) * Quaternion.Euler(Mathf.Lerp(3f, -45f, dd), 0, 0));   // ski ~45 deg, the "cannonball"
                }
                else
                {
                    Vector3 toBoat = (pyl - skierPos); toBoat.y = 0; toBoat.Normalize();
                    Vector3 r = Vector3.Cross(Vector3.up, face);
                    float side = Vector3.Dot(toBoat, r); // >0 boat to the skier's right
                    // Force balance on the body (Docs/SLALOM-SKIER-LOOK.md, pass 4): the ski pushes the body along
                    //   F = m(a_h - T_h/m) + m g  ->  lean = atan(|a_perp - T_perp/m| / g), toward (a_perp - T_perp/m).
                    // Pull: rope to one side, little curvature -> lean away from the boat (30-45 deg).
                    // Buoy: rope slack, hard curvature -> lean into the turn (50-70 deg).
                    float vlean = lastVisT < 0f ? 0f : Mathf.Clamp(simTime - lastVisT, 0f, 0.25f);
                    float loadV = Mathf.InverseLerp(5f, 14f, path.Speed(boatS));
                    float reachL = mode == Mode.Pass ? Mathf.InverseLerp(0.72f, 0.95f, Mathf.Abs(sim.Lateral) / SkierSim.BUOY_OFFSET) * Mathf.InverseLerp(1.2f, 0.3f, Mathf.Abs(sim.omega)) : 0f;
                    float pull = loadV * (1f - reachL);
                    float aRight = vel.magnitude * headRate;
                    float latF = aRight * turnGain - Mathf.Sign(side) * Mathf.Min(1f, Mathf.Abs(side) / ropeSideFull) * ropePullG * 9.81f * pull;
                    float leanT = Mathf.Clamp(Mathf.Atan2(latF, 9.81f) * Mathf.Rad2Deg, -Mathf.Min(maxLeanDeg, rollLimit), Mathf.Min(maxLeanDeg, rollLimit));
                    leanRightDeg = vlean <= 0f ? leanT : Mathf.MoveTowards(leanRightDeg, Mathf.Lerp(leanRightDeg, leanT, 1f - Mathf.Exp(-vlean / 0.12f)), Mathf.Lerp(rollRateLongDegS, rollRateShortDegS, Mathf.InverseLerp(SkierSim.ROPE_M[0], SkierSim.ROPE_M[7], sim.ropeM)) * vlean);   // feel/skier-pose: 240 -> 300 deg/s at short line   // b9.1: <= 4 deg/frame at 60 fps (Holly)
                    // keep every body part out of the water: the rig reports its lowest clearance, back the limit off
                    if (rig != null) rollLimit = rig.WaterClearance < 0.03f ? Mathf.Max(30f, Mathf.Abs(leanRightDeg) - 4f) : Mathf.MoveTowards(rollLimit, maxLeanDeg, 30f * Mathf.Max(vlean, 0.016f));
                    float roll = -leanRightDeg;   // positive Euler z tips the body to the skier's left
                    float pitch = rig != null ? 3f : -12f; // rig: slight tip-up (the stance IK handles hips/shoulders)
                    skier.SetPositionAndRotation(skierPos, Quaternion.LookRotation(face) * Quaternion.Euler(pitch, 0, roll));
                }
            }
            PoseJumpSkis();
            JumpRigParams();
            if (rig != null)
            {
                float vdt = lastVisT < 0f ? 0f : Mathf.Clamp(simTime - lastVisT, 0f, 0.25f);
                lastVisT = simTime;
                float load = Mathf.InverseLerp(5f, 14f, path.Speed(boatS));
                float reachT = mode == Mode.Pass ? Mathf.InverseLerp(0.72f, 0.95f, Mathf.Abs(sim.Lateral) / SkierSim.BUOY_OFFSET) * Mathf.InverseLerp(1.2f, 0.3f, Mathf.Abs(sim.omega)) : 0f;
                var act = mode == Mode.Fallen ? SkierRig.RiderAction.Fall : WaterPose ? SkierRig.RiderAction.DeepStart : SkierRig.RiderAction.None;
                if (mode == Mode.DeepStart) load = Mathf.Lerp(load, 1f, DwsDepth);   // arms straight on the rope from the first tug
                if (mode == Mode.Sink) load = Mathf.Lerp(0.3f, 0.6f, SinkDepth);     // slack line, handle held in at the waist
                rig.rope01 = Mathf.InverseLerp(SkierSim.ROPE_M[0], SkierSim.ROPE_M[7], sim.ropeM);   // b9.1: reach / apex lean by rope length
                rig.Drive(load, mode == Mode.Jump ? Mathf.Abs(headRate) / 1.5f : Mathf.Abs(sim.omega) / 2.45f, mode == Mode.Fallen, vdt, pyl, reachT, act, mode == Mode.Sink ? 0f : actionT, WaterDepth);
            }
            if (skier != null)
            {
                if (spray == null) spray = ski != null ? ski.GetComponent<SkiSpray>() : skier.GetComponentInChildren<SkiSpray>();
                float sdt = lastSprayT < 0f ? 0f : Mathf.Clamp(simTime - lastSprayT, 0f, 1.5f);
                lastSprayT = simTime;
                JumpSpray(sdt, face);
                if (spray != null && !JumpGearOn)
                {
                    Vector3 su = skier.up; Vector3 leanH = su - Vector3.Dot(su, Vector3.up) * Vector3.up;
                    Vector3 away = leanH.sqrMagnitude > 1e-4f ? -leanH.normalized : -skier.right;
                    float edge = Mathf.Clamp01(Vector3.Angle(su, Vector3.up) / 55f); SprayEdge = edge;
                    spray.Drive(skierVel.magnitude, edge, Mathf.Clamp01(Mathf.Abs(sim.omega) / 1.6f), away, face, sdt, mode != Mode.Fallen && WaterDepth < 0.4f);
                }
            }
            if (handle != null)
            {
                Vector3 hp = skier != null ? skier.TransformPoint(new Vector3(0, 1.05f, 0.35f)) : skierPos;
                Vector3 toP = (pyl - hp); toP.y = 0; toP = toP.sqrMagnitude > 1e-4f ? toP.normalized : boatFwd;
                Vector3 bar = Vector3.Cross(Vector3.up, toP).normalized;
                if (rig != null)
                {
                    // handle follows the hands; with one hand released it hangs from the other
                    if (rig.ReleaseL > 0.5f) hp = rig.HandR - bar * 0.075f;
                    else if (rig.ReleaseR > 0.5f) hp = rig.HandL + bar * 0.075f;
                    else hp = (rig.HandL + rig.HandR) * 0.5f;
                }
                handle.position = hp;
                handle.rotation = Quaternion.FromToRotation(Vector3.up, bar);
                handleBar = bar;
            }
            if (rope != null) rope.enabled = !DropRopeHidden;
            if (handle != null) foreach (var hr in handle.GetComponentsInChildren<Renderer>()) hr.enabled = !DropRopeHidden;
            if (rope != null && !DropRopeHidden)
            {
                // main line to a V bridle on the 13 in handle bar
                Vector3 hc = handle != null ? handle.position : skierPos + Vector3.up;
                Vector3 toP = (pyl - hc).normalized;
                Vector3 apex = hc + toP * 0.45f; ropePyl = pyl; ropeApex = apex;
                // pass 7: sag + bounce. Under full load the line is nearly straight (a few cm of sag); it goes slack as the skier
                // reaches out at the buoy, in the slow turns, and after a fall. The sag is a lightly damped spring, so it bounces
                // when the load comes back on, plus a small hum from the boat.
                float rdt = lastRopeT < 0f ? 0f : Mathf.Clamp(simTime - lastRopeT, 0f, 0.25f); lastRopeT = simTime;
                float L = Vector3.Distance(pyl, apex);
                float loadR = Mathf.InverseLerp(5f, 14f, path.Speed(boatS));
                float reachR = mode == Mode.Pass ? Mathf.InverseLerp(0.72f, 0.95f, Mathf.Abs(sim.Lateral) / SkierSim.BUOY_OFFSET) * Mathf.InverseLerp(1.2f, 0.3f, Mathf.Abs(sim.omega)) : 0f;
                float slack = mode == Mode.Fallen ? 1f : Mathf.Clamp01(1f - loadR * (1f - 0.8f * reachR));
                if (mode == Mode.Sink) slack = Mathf.Max(slack, Mathf.Clamp01(actionT / 1.2f) * 0.85f);   // boat off the plane: the line sags
                if (mode == Mode.Jump) slack = Mathf.Max(slack, JumpSlack());
                ApplyTrickYawPose();
                float sagTarget = L * Mathf.Lerp(0.004f, 0.045f, slack * slack) + (mode == Mode.Fallen ? 0.8f : 0f);
                if (rdt <= 0f) { ropeSag = sagTarget; ropeSagVel = 0f; }
                else
                {
                    int sub = Mathf.CeilToInt(rdt / (1f / 120f)); float h = rdt / sub;
                    for (int i = 0; i < sub; i++) { float a = 55f * (sagTarget - ropeSag) - 2.2f * ropeSagVel; ropeSagVel += a * h; ropeSag += ropeSagVel * h; }
                    ropeSag = Mathf.Max(0f, ropeSag);
                }
                float hum = 0.012f * loadR * Mathf.Sin(simTime * 23f) + 0.008f * loadR * Mathf.Sin(simTime * 37f + 1.3f);
                Vector3 sideR = Vector3.Cross(Vector3.up, toP).normalized;
                const int N = 16;
                rope.positionCount = N + 4;
                for (int i = 0; i <= N; i++)
                {
                    float t = i / (float)N, bell = 4f * t * (1f - t);
                    Vector3 q = Vector3.Lerp(pyl, apex, t) + Vector3.down * (ropeSag * bell) + Vector3.down * (hum * Mathf.Sin(Mathf.PI * 2f * t))
                                + sideR * (ropeSagVel * 0.04f * Mathf.Sin(Mathf.PI * 2f * t));
                    if (q.y < 0.02f) q.y = 0.02f;   // a slack line lies on the water, never through it
                    rope.SetPosition(i, q);
                }
                rope.SetPosition(N + 1, hc - handleBar * 0.16f); rope.SetPosition(N + 2, hc + handleBar * 0.16f); rope.SetPosition(N + 3, apex);
            }

            WaterFx();
            // water shader globals
            Shader.SetGlobalFloat(ID_Time, simTime);
            Shader.SetGlobalVectorArray(ID_Splash, splash);
            Shader.SetGlobalVectorArray(ID_Wake, wake);
            Shader.SetGlobalFloat(ID_WakeN, wakeN);
            Shader.SetGlobalVectorArray(ID_Ski, skiTrail);
            Shader.SetGlobalFloat(ID_SkiN, skiN);
            Shader.SetGlobalVector(ID_Boat, new Vector4(boatPos.x, 0, boatPos.z, path.Speed(boatS)));
            Shader.SetGlobalVector(ID_BoatFwd, new Vector4(boatFwd.x, boatFwd.y, boatFwd.z, boatWake != null ? boatWake.localPosition.z : -3.0f));   // w: WakeEmitter z (stern)

            if (cam != null && cameraFollow) { UpdateCamera(snapCamera); FirstPersonRig(); }
        }

        public enum CamMode { Broadcast, SkierChase, FirstPerson, CloseChase }   // pass 17: SkierChase = the higher chase view (player default)
        [Header("Camera (pass 7)")]
        public CamMode camMode = CamMode.Broadcast;
        // pass 8/9: low in the boat ahead of the pylon (at the windshield), on the rope line extended, looking straight back down the rope
        // (TWBC boat cam). Pass 9: ~16 deg lens, skier ~25% of the frame, so the pylon post and the stern deck edge fill the bottom
        // of the frame like the reference; 2.2 m from the pylon so the ball isn't a giant blob
        public float bcExtraRise = 0.2f, bcBehindPylon = 2.2f, bcPylonTopAboveTow = 0.04f, bcPylonInFrame = 0.42f, bcSkierH = 1.35f, bcSkierFrac = 0.22f, bcMinFov = 14f, bcMaxFov = 34f,
                     bcLookSmooth = 0.12f, bcFovSmooth = 0.4f, bcDirSmooth = 0.25f, bcMaxYaw = 50f, bcNearClip = 1.3f, bcTiltDown = 1.3f;
        Vector3 bcLook, bcLookVel, bcDir, bcDirVel; float bcFov, bcFovVel, bcRise, bcRiseVel;
        void UpdateCamera(bool snap)
        {
            // pass 17: switchable cameras with smooth blends; the jump shore camera still takes over unless first person is on
            if (DebugTopCam()) return;   // b9.1e: cam=top debug overhead
            if (camMode != camModeLast) { BeginCamBlend(); camModeLast = camMode; }
            bool jc = camMode != CamMode.FirstPerson && UpdateJumpCamera(snap);   // pass 14: shore camera through the ramp, flight and ride-out
            if (jc != jcLast) { if (!jc) BeginCamBlend(); jcLast = jc; }
            if (!jc)
            {
                if (camMode == CamMode.Broadcast) UpdateBroadcastCamera(snap);
                else if (camMode == CamMode.FirstPerson) UpdateFirstPersonCamera(snap);
                else if (camMode == CamMode.CloseChase) UpdateCloseChaseCamera(snap);
                else UpdateHighChaseCamera(snap);
            }
            ApplyCamBlend(snap);
        }

        void UpdateHighChaseCamera(bool snap)
        {
            // legacy chase: behind and above the skier, looking up the rope toward the boat
            bool turn = mode != Mode.Pass;
            Vector3 back = -boatFwd;
            Vector3 want = skierPos + back * (turn ? 14f : 9.5f) + Vector3.up * (turn ? 7.5f : 3.6f)
                           + Vector3.Cross(Vector3.up, boatFwd) * sim.Lateral * 0.3f;
            Vector3 look = Vector3.Lerp(skierPos, boatPos, 0.38f) + Vector3.up * 1.2f;
            cam.fieldOfView = 52f; cam.nearClipPlane = 0.2f;
            if (snap || !Application.isPlaying) cam.transform.position = want;
            else cam.transform.position = Vector3.SmoothDamp(cam.transform.position, want, ref camVel, 0.35f);
            cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position);
        }

        /// <summary>Broadcast boat camera, in the TWBC tournament style (reference/twbc, study only). It is mounted low in the boat just ahead of
        /// and above the pylon, on the rope line, and looks back down the rope at the skier, so the pylon and rope enter from the bottom centre and the
        /// wake fills the foreground. The camera is gimbal-stabilised: the boat's pitch and roll are ignored, so the horizon stays level.
        /// Pan and tilt smoothly follow the skier with a little lead, and the lens zooms with the distance so the skier keeps a
        /// constant size (~38% of the frame height). It goes wider in the slow island turns.</summary>
        void UpdateBroadcastCamera(bool snap)
        {
            Vector3 tow = boatTow != null ? boatTow.position : Pylon() + Vector3.up * SkierSim.PYLON_Y;
            Vector3 pylonTop = tow + Vector3.up * bcPylonTopAboveTow;
            // look at the skier's middle (handle height), so the rope runs from the pylon top at bottom centre straight up to the hands
            Vector3 target = skierPos + Vector3.up * (mode == Mode.Fallen ? 0.3f : 0.8f);
            // the mount slides round the pylon so it stays on the rope line (pylon top directly under the skier, the rope line of sight
            // clears the seats); at wide angles it sits a little outboard of the gunwale, like a jib arm (never in shot)
            Vector3 toSkier = target - tow; toSkier.y = 0f;
            Vector3 aft = -boatFwd; aft.y = 0f; aft.Normalize();
            Vector3 dir = toSkier.sqrMagnitude > 1f ? toSkier.normalized : aft;
            float yaw = Vector3.SignedAngle(aft, dir, Vector3.up);
            float maxYaw = mode == Mode.Jump ? 72f : bcMaxYaw;   // pass 14: the jump approach swings much wider than a slalom pass
            dir = Quaternion.AngleAxis(Mathf.Clamp(yaw, -maxYaw, maxYaw), Vector3.up) * aft;
            float D = toSkier.magnitude + bcBehindPylon;
            // zoom so the skier (~1.35 m visible above the water, crouched) fills ~38% of the frame height (the real boat cam is a telephoto: a 50-60 deg lens
            // would make the skier ~7-10% tall at 18-23 m); wider in the turns
            float wantFov = 2f * Mathf.Atan(bcSkierH / bcSkierFrac * 0.5f / Mathf.Max(6f, D)) * Mathf.Rad2Deg * (mode == Mode.Pass ? 1f : mode == Mode.Jump ? 0.72f : 1.6f);   // pass 14: jump approach: tighter than a pass (skier ~30% of the frame), no turn widening
            float aspect = cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
            wantFov = Mathf.Clamp(wantFov, mode == Mode.Jump ? 10f : bcMinFov, bcMaxFov) * Mathf.Lerp(1.25f, 1f, Mathf.InverseLerp(1.3f, 1.7f, aspect));   // 4:3 tablets get a little wider; 16:9 and up as specified
            bool hard = snap || !Application.isPlaying || bcFov <= 0f;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            if (hard) { bcDir = dir; bcDirVel = Vector3.zero; bcFov = wantFov; bcFovVel = 0f; }
            else
            {
                bcDir = Vector3.SmoothDamp(bcDir, dir, ref bcDirVel, bcDirSmooth, Mathf.Infinity, dt);
                bcFov = Mathf.SmoothDamp(bcFov, wantFov, ref bcFovVel, bcFovSmooth, Mathf.Infinity, dt);
            }
            Vector3 bd = bcDir; bd.y = 0f; bd = bd.sqrMagnitude > 1e-4f ? bd.normalized : aft;
            // camera height: just enough above the pylon top that the ball pokes into the bottom ~22% of the lower half-frame
            float half = 0.5f * bcFov * Mathf.Deg2Rad, rise = 0.1f;
            for (int it = 0; it < 4; it++)
            {
                float axisDown = Mathf.Atan2(pylonTop.y + rise - target.y, D) + bcTiltDown * Mathf.Deg2Rad;
                rise = Mathf.Clamp(bcBehindPylon * Mathf.Tan(axisDown + bcPylonInFrame * half), 0.02f, 0.6f);
            }
            if (hard) { bcRise = rise; bcRiseVel = 0f; } else bcRise = Mathf.SmoothDamp(bcRise, rise, ref bcRiseVel, bcFovSmooth, Mathf.Infinity, dt);
            Vector3 mount = pylonTop - bd * bcBehindPylon + Vector3.up * (bcRise + bcExtraRise);   // Scott Oct 23: +0.2 m so the pylon ball and rope don't block the skier
            if (hard) { bcLook = target; bcLookVel = Vector3.zero; }
            else bcLook = Vector3.SmoothDamp(bcLook, target, ref bcLookVel, bcLookSmooth, Mathf.Infinity, dt);
            float plane = Mathf.InverseLerp(4f, 15.2f, path.Speed(boatS));
            float shake = ReducedMotion ? 0f : 0.05f * plane;   // stabilised head: only a little hull buzz gets through
            cam.transform.position = mount;
            // tilted a touch below the skier (skier sits just above centre) so the stern deck edge crosses the bottom of the frame
            cam.transform.rotation = Quaternion.LookRotation(bcLook - mount, Vector3.up) * Quaternion.Euler(bcTiltDown + shake * Mathf.Sin(simTime * 41f), shake * 0.6f * Mathf.Sin(simTime * 29f + 0.7f), 0f);
            cam.fieldOfView = bcFov;
            cam.nearClipPlane = bcNearClip;   // clips the windshield frame just behind the mount (the camera looks aft)
        }

        /// <summary>Pass 15: load the next lake scene (each lake has its own terrain, shore, trees, buildings, course, sky and boat colours).</summary>
        public void NextLake()
        {
            if (lakeScenes == null || lakeScenes.Length < 2) return;
            FlushSlalomSet();
            SkiAudio.Instance?.UiConfirm();
            int i = System.Array.IndexOf(lakeScenes, SceneManager.GetActiveScene().name);
            string next = lakeScenes[(i + 1 + lakeScenes.Length) % lakeScenes.Length];
            if (SkiViewBoot.Active) SkiViewBoot.Load(next);                       // pass 16: Addressables (per-lake bundles / asset packs)
            else if (Application.CanStreamedLevelBeLoaded(next)) SceneManager.LoadScene(next);
        }
        public Vector3 SkierPos => skierPos;
        public Vector3 BoatPos => boatPos;
        // pass 18 audio taps (read by SkiAudio each frame)
        public float Throttle => throttle;
        public float BoatSpeed => path != null && path.Count > 0 ? path.Speed(boatS) * SpeedMul * throttle : 0f;
        public float SkierSpeed => skierVel.magnitude;
        public float SprayEdge { get; private set; }
        public float SkierDepth => mode == Mode.Sink || mode == Mode.DeepStart ? WaterDepth : 0f;
        public int SkipCount { get; private set; }
        [Header("Audio (pass 18)")] public AudioClip ambientClip;   // per-lake baked ambient bed (lives in the lake's pack)
        public Vector3 BoatFwd => boatFwd;
        public float BoatS => boatS;

        // ---------- HUD ----------
        GUIStyle big, small, btn;
        void OnGUI()
        {
            if (!Application.isPlaying) return;
            var svui = SkiView.UI.SkiViewUI.Instance; if (svui != null || HudHidden) return;   // pass 19: the whole HUD is on Julia's kit now (IMGUI only without the UI resources)
            float k = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            // pass 17: keep text and buttons inside the safe area (notch / cutout on either side in both landscape directions)
            Rect sa = Screen.safeArea;
            float insL = sa.xMin / k, insR = (Screen.width - sa.xMax) / k, insT = (Screen.height - sa.yMax) / k;
            float Wfull = Screen.width / k;
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                btn = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
                big.normal.textColor = Color.white; small.normal.textColor = Color.white;
            }
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Wfull, 64 + insT), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.TRS(new Vector3(insL * k, insT * k, 0), Quaternion.identity, new Vector3(k, k, 1));
            float W = Wfull - insL - insR;   // everything below is laid out in the safe width
            string title = lakeId == "skiview" ? SkiViewBranding.Title + "  " + SkiViewBranding.GreyboxTag : lakeName.ToUpperInvariant();   // pass 17: just the lake name away from home
            string left = $"{title}    {SkierSim.ROPE_LABEL[sim.ropeIndex]} · {MphTag}    Pass {passes + (mode == Mode.Pass ? 1 : 0)}";
            if (jumpMode || mode == Mode.Jump) left = $"{title}    JUMP · {RampRatio:0.000} · {jumpBoatKmh:0} km/h · 23 m    Jumps {passes}";
            GUI.Label(new Rect(16, 6, W * 0.6f, 28), left, small);
            string sc = mode == Mode.Jump ? $"Jump run · {jphase}" + (JumpDistance > 0f ? $"   {JumpDistance:0.0} m" : "") + JumpPrompt() : mode == Mode.Pass ? $"Buoys {sim.passBuoys:0.#}   {sim.message}" : (mode == Mode.Fallen ? sim.message : mode == Mode.DeepStart ? "Deep-water start" : mode == Mode.Sink ? "Settling in · tap to skip the turn" : "Island turn  ·  " + (sim.ropeM > ropeTarget + 0.01f ? "shortening rope" : $"Next: {SkierSim.ROPE_LABEL[sim.ropeIndex]} · {MphTag}"));
            GUI.Label(new Rect(16, 32, W * 0.7f, 28), sc, small);
            if (mode == Mode.Turn && jumpRamp != null && !jumpOnly && GUI.Button(new Rect(W - 570, 10, 130, 44), jumpMode ? "JUMP: ON" : "JUMP: OFF", btn)) ToggleJumpMode();
            if (mode == Mode.Turn && lakeScenes != null && lakeScenes.Length > 1 && GUI.Button(new Rect(W - 770, 10, 190, 44), "LAKE: " + lakeName.ToUpperInvariant(), btn)) NextLake();
            if (GUI.Button(new Rect(W - 290, 10, 130, 44), autopilot ? "AUTO: ON" : "AUTO: OFF", btn)) autopilot = !autopilot;
            if (svui != null) { if (GUI.Button(new Rect(W - 430, 10, 130, 44), "SETTINGS", btn)) svui.OpenSettings(); }   // pass 18: CAM moved to the UI Toolkit button / hint
            else if (GUI.Button(new Rect(W - 430, 10, 130, 44), CamLabel, btn)) CycleCamera();
            if (mode != Mode.Pass && GUI.Button(new Rect(W - 150, 10, 136, 44), turnaround ? "Skip turn >>" : (skipTurn ? ">> x3" : "Fast >>"), btn)) { if (turnaround) SkipToSetup(); else skipTurn = !skipTurn; }
            if (bannerT > 0) GUI.Label(new Rect(0, 90, W, 44), banner, big);
            GUI.Label(new Rect(16, 720 - 36, W - 200, 30), jumpMode ? "Jump: tap (or Space) on the far side to CUT - later = faster and steeper, too late = miss  ·  tap on the ramp to POP (last third)  ·  P autopilot"
                                                       : "Hold left / right side of the screen (or arrow keys, A / D) to cut  ·  P autopilot  ·  C camera  ·  Space skip turn", small);
        }
    }
}

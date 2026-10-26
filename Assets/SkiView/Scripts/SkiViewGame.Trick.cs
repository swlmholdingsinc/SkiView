using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>b10.11 Tricks Phase A — T14-trick-first-slice.md.
    /// Two 20 s passes opposite directions; starter set S/B/F/O/BB/WB/WF; Simple touch+keyboard;
    /// clock at first trick motion after entry buoy; south-dock start; trick-v1 finless ski.</summary>
    public partial class SkiViewGame
    {
        public enum TPhase { Off, Approach, Armed, Active, Turnaround, Results }
        public enum TrickCode { S, B, F, O, BB, WB, WF }

        [Header("Tricks (b10.11 Phase A)")]
        public bool trickMode;
        public TPhase tphase = TPhase.Off;
        public float trickBoatKmh = 31f, trickRopeM = 12.5f;
        public int trickPass = 0;   // 1 or 2 while skiing
        public float TrickClock { get; private set; } = 20f;   // remaining
        public int TrickScore { get; private set; }
        public string TrickToast { get; private set; } = "";
        public float TrickToastT { get; private set; }
        public bool TrickRoundOver { get; private set; }   // fall in pass 1
        public bool TrickResultsOpen => tphase == TPhase.Results;
        public IReadOnlyList<TrickEntry> TrickLog => trickLog;

        public struct TrickEntry
        {
            public string code, name, reason;
            public int pts;
            public bool credited, reverse;
            public int pass;
        }

        public const string TRICK_KMH_PREF = "sv.setup.trick.kmh", TRICK_ROPE_PREF = "sv.setup.trick.rope";
        public static readonly float[] TRICK_KMH = { 27f, 29f, 31f, 33f };
        public static readonly float[] TRICK_ROPE_M = { 11.5f, 12.0f, 12.5f, 13.0f, 13.5f };
        public static readonly string[] TRICK_KMH_LABEL = { "27 km/h\n16.8 mph", "29 km/h\n18.0 mph", "31 km/h\n19.3 mph", "33 km/h\n20.5 mph" };
        public static readonly string[] TRICK_ROPE_LABEL = { "11.5 m", "12.0 m", "12.5 m", "13.0 m", "13.5 m" };

        public static int SetupTrickKmhIdx
        {
            get
            {
                float v = PlayerPrefs.GetFloat(TRICK_KMH_PREF, 31f);
                int best = 2;
                for (int i = 0; i < TRICK_KMH.Length; i++) if (Mathf.Approximately(TRICK_KMH[i], v)) return i;
                return best;
            }
        }
        public static int SetupTrickRopeIdx
        {
            get
            {
                float v = PlayerPrefs.GetFloat(TRICK_ROPE_PREF, 12.5f);
                for (int i = 0; i < TRICK_ROPE_M.Length; i++) if (Mathf.Approximately(TRICK_ROPE_M[i], v)) return i;
                return 2;
            }
        }
        public static void SaveTrickSetup(int kmhIdx, int ropeIdx)
        {
            kmhIdx = Mathf.Clamp(kmhIdx, 0, TRICK_KMH.Length - 1);
            ropeIdx = Mathf.Clamp(ropeIdx, 0, TRICK_ROPE_M.Length - 1);
            PlayerPrefs.SetFloat(TRICK_KMH_PREF, TRICK_KMH[kmhIdx]);
            PlayerPrefs.SetFloat(TRICK_ROPE_PREF, TRICK_ROPE_M[ropeIdx]);
            PlayerPrefs.Save();
        }

        // chart # → BAS/REV (1-ski) from T14 S2
        static readonly Dictionary<TrickCode, (int chart, int bas, int rev, string name, bool wake)> Chart = new Dictionary<TrickCode, (int, int, int, string, bool)>
        {
            { TrickCode.S,  (1, 40, 40, "Side slide", false) },
            { TrickCode.B,  (3, 60, 60, "Surface 180 F→B", false) },
            { TrickCode.F,  (4, 60, 60, "Surface 180 B→F", false) },
            { TrickCode.O,  (5, 90, 90, "Surface 360 F→F", false) },
            { TrickCode.BB, (5, 90, 90, "Surface 360 B→B", false) }, // chart 5b shares #5 with O for no-repeat [DA: treat BB as chart 5b distinct via code]
            { TrickCode.WB, (14, 80, 80, "Wake 180 F→B", true) },
            { TrickCode.WF, (15, 80, 80, "Wake 180 B→F", true) },
        };

        // Phase A: O and BB share chart family for no-repeat of 360 face — credit each code once + reverse once
        readonly List<TrickEntry> trickLog = new List<TrickEntry>();
        readonly HashSet<string> creditedKeys = new HashSet<string>(); // "pass:code" or "pass:Rcode"
        GameObject trickCourse;
        Transform trickSkiInst;
        float[] trickBuoyAlong; // boat progress along course for two entry buoys
        float trickYaw;         // skier facing yaw offset deg (0 = forward / F)
        float trickSpinRemain;  // degrees left in current spin (signed: + CCW / left)
        float trickSpinSpeed = 420f;
        float trickSpinTarget;
        TrickCode? trickSpinCode;
        bool trickSpinWake, trickSpinRev, trickFacingBack;
        float trickSlideT, trickEdgeHold, trickCrestWindow;
        float trickLastCreditT = -99f;
        string trickLastBasicKey;
        bool trickClockStarted, trickFallThisPass;
        int trickBeepPrev = 99;
        float trickApproachAlong;
        Vector2 trickSwipe0; float trickSwipeT; int trickSwipeCount; float trickSwipeDir;
        float trickQChainT; int trickQChain; // keyboard Q/E chain for 360
        bool trickPopArmed;

        public bool TrickGearOn { get; private set; }
        public float TrickYaw => trickYaw;
        public bool TrickFacingBack => trickFacingBack;
        public bool TrickSpinning => Mathf.Abs(trickSpinRemain) > 0.5f;
        public string TrickHudLine => tphase == TPhase.Active || tphase == TPhase.Armed
            ? $"PASS {trickPass}/2 · {TrickClock:0.00} · {TrickScore}"
            : tphase == TPhase.Approach ? $"PASS {trickPass}/2 · APPROACH" : "";

        public void SetTrickGear(bool on)
        {
            TrickGearOn = on;
            if (on && jumpMode) { jumpMode = false; SetJumpGear(false); }
            ApplyTrickSki(on);
            EnsureTrickCourse(on);
            // hide jump ramp/course when tricks on
            if (jumpCourse != null) jumpCourse.SetActive(false);
            if (rampJumpModeOnly && jumpRamp != null) jumpRamp.gameObject.SetActive(false);
            else if (jumpRamp != null && on) jumpRamp.gameObject.SetActive(false);
            if (on)
            {
                trickBoatKmh = TRICK_KMH[SetupTrickKmhIdx];
                trickRopeM = TRICK_ROPE_M[SetupTrickRopeIdx];
                ropeTarget = trickRopeM;
                if (sim != null) { sim.ropeM = trickRopeM; }
            }
            else if (sim != null)
            {
                ropeTarget = SkierSim.ROPE_M[sim.ropeIndex];
            }
        }

        public void ToggleTrickMode()
        {
            if (trickMode) SkiAudio.Instance?.UiBack(); else SkiAudio.Instance?.UiConfirm();
            trickMode = !trickMode;
            if (trickMode && jumpMode) { jumpMode = false; SetJumpGear(false); }
            SetTrickGear(trickMode);
            if (trickMode) BeginTrickRound();
            else { tphase = TPhase.Off; mode = Mode.Turn; }
            banner = trickMode ? $"TRICKS · {trickBoatKmh:0} km/h · {trickRopeM:0.0} m · 2 × 20 s" : "SLALOM MODE";
            bannerT = 2.5f;
        }

        public void BeginTrickRound()
        {
            if (!inited) Init();
            if (!trickMode) { trickMode = true; SetTrickGear(true); }
            trickLog.Clear(); creditedKeys.Clear(); TrickScore = 0; TrickRoundOver = false;
            trickPass = 1; trickYaw = 0f; trickFacingBack = false; trickSpinRemain = 0f; trickSpinCode = null;
            trickClockStarted = false; TrickClock = 20f; trickFallThisPass = false; trickBeepPrev = 99;
            trickLastBasicKey = null; trickLastCreditT = -99f; trickSlideT = 0f;
            tphase = TPhase.Approach;
            // south dock / first pass northbound on Ski View (StartDir = -1)
            passDir = StartDir;
            boatS = path.FindS(passDir, startProgress);
            boatPos = path.Position(boatS); boatFwd = path.Tangent(boatS); prevHeading = Heading(boatFwd);
            sim.ropeM = trickRopeM; ropeTarget = trickRopeM; sim.theta = 0.2f; sim.omega = 0f; throttle = 1f;
            mode = Mode.DeepStart; actionT = 0f; dwsPatchDone = false; turnaround = false;
            PlaceSkier(); skierPrev = skierPos;
            PlaceTrickBuoys();
            banner = $"TRICKS · PASS 1 · {trickBoatKmh:0} km/h · {trickRopeM:0.0} m"; bannerT = 3f;
        }

        void ApplyTrickSki(bool on)
        {
            if (ski == null) return;
            // hide slalom colourways / legacy body when tricks on
            foreach (var nm in new[] { "SlalomSki_V3_ToePlate", "SlalomSki_V3_DoubleBoot", "SlalomSki_V2", "SkiBody", "Fin", "FinWing", "FinBlock" })
            {
                var ch = ski.Find(nm); if (ch != null) ch.gameObject.SetActive(false);
            }
            if (!on)
            {
                if (trickSkiInst != null) trickSkiInst.gameObject.SetActive(false);
                ApplySki(SetupSki); // restore colourway
                return;
            }
            if (trickSkiInst == null)
            {
                var pf = Resources.Load<GameObject>("Skis/TrickSki_V1");
                if (pf == null) { Debug.LogWarning("[SkiView] Resources/Skis/TrickSki_V1 missing — import TrickSki first"); return; }
                trickSkiInst = Object.Instantiate(pf, ski, false).transform;
                trickSkiInst.name = "TrickSki_V1";
                float mid = 0.671f; // same ankle-mid offset as V3
                trickSkiInst.localPosition = new Vector3(0, 0, mid);
                trickSkiInst.localRotation = Quaternion.identity;
                foreach (var r in trickSkiInst.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            trickSkiInst.gameObject.SetActive(true);
            // kit boot meshes stay hidden; IK targets remain kit FrontBoot/RearBoot
            foreach (var bn in new[] { "FrontBoot", "RearBoot" })
            {
                var b = ski.Find(bn); if (b == null) continue;
                foreach (var r in b.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            var spray = ski.GetComponentInChildren<SkiSpray>(true);
            var em = FindDeep(trickSkiInst, "SprayEmitter");
            if (em != null && spray != null) spray.tailZ = ski.InverseTransformPoint(em.position).z;
        }

        void EnsureTrickCourse(bool on)
        {
            if (!on)
            {
                if (trickCourse != null) trickCourse.SetActive(false);
                return;
            }
            if (trickCourse == null)
            {
                trickCourse = new GameObject("TrickCourse");
                trickCourse.transform.SetParent(transform, false);
                // two orange entry balls
                for (int i = 0; i < 2; i++)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    b.name = i == 0 ? "TrickEntryA" : "TrickEntryB";
                    b.transform.SetParent(trickCourse.transform, false);
                    b.transform.localScale = Vector3.one * 0.23f;
                    if (Application.isPlaying) Object.Destroy(b.GetComponent<Collider>()); else Object.DestroyImmediate(b.GetComponent<Collider>());
                    var r = b.GetComponent<MeshRenderer>();
                    var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    m.SetColor("_BaseColor", new Color(1f, 0.40f, 0.02f));
                    if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(1f, 0.40f, 0.02f));
                    m.SetFloat("_Smoothness", 0.72f);
                    r.sharedMaterial = m; r.allowOcclusionWhenDynamic = false;
                }
            }
            trickCourse.SetActive(true);
            PlaceTrickBuoys();
        }

        void PlaceTrickBuoys()
        {
            if (trickCourse == null || path == null) return;
            // ±115 m from lake midpoint along slalom centreline [DA] — T14 §1.3
            float mid = 0f;
            Vector3 c0 = path.Position(path.FindS(-1, mid - 115f));
            Vector3 c1 = path.Position(path.FindS(-1, mid + 115f));
            // auto lake-safe side: offset slightly off centreline toward +cross
            Vector3 f = path.CourseFwd(-1); Vector3 side = Vector3.Cross(Vector3.up, f).normalized;
            float sideOff = 4f; // [DA] boat may pass either side
            var a = trickCourse.transform.Find("TrickEntryA");
            var b = trickCourse.transform.Find("TrickEntryB");
            if (a != null) a.position = new Vector3(c0.x, 0.09f, c0.z) + side * sideOff;
            if (b != null) b.position = new Vector3(c1.x, 0.09f, c1.z) + side * sideOff;
            trickBuoyAlong = new[] { mid - 115f, mid + 115f };
        }

        /// <summary>Fixed-step boat speed override + pass arming for tricks.</summary>
        void TrickBoatAndTrigger(float dt, bool onLine, int dir)
        {
            if (!trickMode) return;
            // approach → armed when boat past the entry buoy for this direction
            if (tphase == TPhase.Approach && mode == Mode.Turn && onLine)
            {
                float prog = path.Progress(boatS);
                float entry = EntryAlongForDir(dir);
                // boat approaching entry from before the course
                if (dir < 0 && prog > entry - 40f && prog < entry + 5f) { ArmTrickPass(dir); }
                if (dir > 0 && prog < entry + 40f && prog > entry - 5f) { ArmTrickPass(dir); }
            }
            if ((tphase == TPhase.Armed || tphase == TPhase.Active) && mode == Mode.Turn)
                mode = Mode.Trick;
        }

        float EntryAlongForDir(int dir)
        {
            // pass 1 northbound (dir=-1): enter at +115 (south buoy) heading north toward -115
            // pass 2 southbound (dir=+1): enter at -115 heading south toward +115
            if (trickBuoyAlong == null || trickBuoyAlong.Length < 2) return dir < 0 ? 115f : -115f;
            return dir < 0 ? Mathf.Max(trickBuoyAlong[0], trickBuoyAlong[1]) : Mathf.Min(trickBuoyAlong[0], trickBuoyAlong[1]);
        }

        void ArmTrickPass(int dir)
        {
            passDir = dir; trickClockStarted = false; TrickClock = 20f; trickFallThisPass = false; trickBeepPrev = 99;
            tphase = TPhase.Armed; mode = Mode.Trick;
            skipTurn = false; turnaround = false;
            sim.ropeM = trickRopeM; ropeTarget = trickRopeM;
            banner = $"PASS {trickPass} · ARMED · FIRST SPIN STARTS THE CLOCK"; bannerT = 2f;
        }

        void TrickStep(float dt)
        {
            // skier rope pendulum (reuse slalom) with light edge input
            float h = Heading(boatFwd);
            float dpsi = 0f; // already applied in FixedStep before this? We call from Mode.Trick branch after dpsi applied
            bool inPass = tphase == TPhase.Armed || tphase == TPhase.Active;
            float thMax = 1.5f;
            sim.theta = Mathf.Clamp(sim.theta, -thMax, thMax);
            float auto = sim.AutoSteer(false);
            float steer = (steerInput != 0 && !autopilot) ? Mathf.Clamp(steerInput * 0.7f + auto * 0.3f, -1, 1) : auto * 0.5f;
            // hold edge for wake cut
            if (Mathf.Abs(steerInput) > 0.4f) trickEdgeHold = Mathf.MoveTowards(trickEdgeHold, Mathf.Sign(steerInput), dt * 2f);
            else trickEdgeHold = Mathf.MoveTowards(trickEdgeHold, 0f, dt * 1.5f);
            // crest window: near wake (small |theta|)
            if (Mathf.Abs(sim.theta) < 0.25f && Mathf.Abs(trickEdgeHold) > 0.3f) trickCrestWindow = 0.40f;
            else trickCrestWindow = Mathf.Max(0f, trickCrestWindow - dt);

            sim.Step(dt, steer, 0.85f);
            skierPrev = skierPos; PlaceSkier(); KeepSkierOnWater(); skierVel = (skierPos - skierPrev) / Mathf.Max(1e-4f, dt);

            ReadTrickInput(dt);
            AdvanceTrickSpin(dt);

            // auto-start clock if past buoy by 15 m with no trick [DA]
            if (tphase == TPhase.Armed)
            {
                float prog = path.Progress(boatS);
                float entry = EntryAlongForDir(passDir);
                float past = passDir < 0 ? (entry - prog) : (prog - entry);
                if (past > 15f) StartTrickClock("running out of lake");
            }

            if (tphase == TPhase.Active)
            {
                TrickClock -= dt;
                SkiAudio.Instance?.TrickTimer(TrickClock);
                SkiAudio.Instance?.TrickSlide(trickSlideT > 0f);
                if (TrickClock <= 0f) EndTrickPass(false, "time");
            }

            // side-slide hold
            if (trickSlideT > 0f)
            {
                trickSlideT -= dt;
                trickYaw = Mathf.MoveTowardsAngle(trickYaw, trickFacingBack ? 180f : 90f * Mathf.Sign(trickSpinRemain == 0 ? 1 : 0) + (trickFacingBack ? 180f : 0f), 720f * dt);
                // keep ~90° for slide
                float slideTarget = trickFacingBack ? 180f : 90f;
                if (Mathf.Abs(Mathf.DeltaAngle(trickYaw, 90f)) < 20f || Mathf.Abs(Mathf.DeltaAngle(trickYaw, -90f)) < 20f)
                    trickYaw = Mathf.DeltaAngle(0f, trickYaw) >= 0f ? 90f : -90f;
                if (trickSlideT <= 0f) FinishSideSlide();
            }
        }

        void StartTrickClock(string why)
        {
            if (trickClockStarted) return;
            trickClockStarted = true; tphase = TPhase.Active; TrickClock = 20f; trickBeepPrev = 99;
            banner = $"20.00 · PASS {trickPass}"; bannerT = 1.2f;
            Debug.Log("[SkiView] trick clock start: " + why);
        }

        void ReadTrickInput(float dt)
        {
            if (tphase != TPhase.Armed && tphase != TPhase.Active) return;
            if (TrickSpinning || trickSlideT > 0f) return;

            // keyboard
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
            {
                int dir = Input.GetKeyDown(KeyCode.Q) ? 1 : -1; // Q = CCW (+)
                if (Time.time - trickQChainT < 0.30f && trickQChain == dir) { BeginSpin(360f, dir, false); trickQChain = 0; }
                else { trickQChain = dir; trickQChainT = Time.time; BeginSpin(180f, dir, false); }
            }
            if (Input.GetKeyDown(KeyCode.Z)) BeginSideSlide(1);
            if (Input.GetKeyDown(KeyCode.C)) BeginSideSlide(-1);
            if (Input.GetKeyDown(KeyCode.Space)) trickPopArmed = true; // b10.13 SFX A: arm only; TrickWakePop fires once in BeginSpin
            if (Input.GetKeyDown(KeyCode.R) && trickLastBasicKey != null && Time.time - trickLastCreditT < 1.2f)
                TryReversePrompt();

            // touch: swipe L/R = 180; double-swipe = 360; flick back = side slide
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) { trickSwipe0 = t.position; trickSwipeT = Time.time; }
                else if (t.phase == TouchPhase.Ended)
                {
                    Vector2 d = t.position - trickSwipe0;
                    float dur = Time.time - trickSwipeT;
                    if (d.magnitude > 80f)
                    {
                        float dir = d.x < 0f ? 1f : -1f; // swipe left → CCW
                        if (dur < 0.35f && Mathf.Abs(d.x) < 120f) BeginSideSlide(dir > 0 ? 1 : -1);
                        else if (Time.time - trickSwipeT < 0.5f && trickSwipeCount == 1 && Mathf.Sign(trickSwipeDir) == Mathf.Sign(dir) && Time.time - trickLastCreditT < 0.5f)
                        { BeginSpin(360f, dir, false); trickSwipeCount = 0; }
                        else
                        {
                            bool wake = trickCrestWindow > 0f || trickPopArmed;
                            BeginSpin(180f, dir, wake); trickSwipeCount = 1; trickSwipeDir = dir; trickPopArmed = false;
                        }
                    }
                }
            }
        }

        void TryReversePrompt()
        {
            // reverse = opposite rotation of last basic
            if (string.IsNullOrEmpty(trickLastBasicKey)) return;
            // map last code to reverse spin
            if (trickLastBasicKey.EndsWith(":B")) BeginSpin(180f, trickFacingBack ? -1 : 1, false, true);
            else if (trickLastBasicKey.EndsWith(":F")) BeginSpin(180f, trickFacingBack ? 1 : -1, false, true);
            else if (trickLastBasicKey.EndsWith(":O")) BeginSpin(360f, 1, false, true);
            else if (trickLastBasicKey.EndsWith(":BB")) BeginSpin(360f, -1, false, true);
            else if (trickLastBasicKey.EndsWith(":WB")) BeginSpin(180f, 1, true, true);
            else if (trickLastBasicKey.EndsWith(":WF")) BeginSpin(180f, -1, true, true);
            else if (trickLastBasicKey.EndsWith(":S")) BeginSideSlide(1, true);
        }

        void BeginSideSlide(int dir, bool asReverse = false)
        {
            if (tphase == TPhase.Armed) StartTrickClock("side slide");
            if (tphase != TPhase.Active) return;
            trickSpinRemain = 0f; trickSlideT = 0.85f; trickSpinCode = TrickCode.S; trickSpinRev = asReverse;
            trickYaw = dir >= 0 ? 90f : -90f;
            SkiAudio.Instance?.TrickCarve();
            SkiAudio.Instance?.TrickSlide(true);
        }

        void FinishSideSlide()
        {
            SkiAudio.Instance?.TrickSlide(false);
            // return to skiing position (0 or 180)
            trickYaw = trickFacingBack ? 180f : 0f;
            CreditTrick(TrickCode.S, trickSpinRev, false, true);
            trickSpinCode = null;
        }

        void BeginSpin(float degrees, float dirSign, bool wake, bool asReverse = false)
        {
            if (tphase == TPhase.Armed) StartTrickClock("spin");
            if (tphase != TPhase.Active && tphase != TPhase.Armed) return;
            if (tphase == TPhase.Armed) StartTrickClock("spin");
            wake = wake || trickPopArmed || trickCrestWindow > 0f;
            trickPopArmed = false;
            float d = degrees * Mathf.Sign(dirSign == 0 ? 1 : dirSign);
            trickSpinRemain = d; trickSpinTarget = d; trickSpinWake = wake; trickSpinRev = asReverse;
            // classify
            bool back = trickFacingBack;
            if (Mathf.Abs(degrees - 360f) < 1f)
                trickSpinCode = back ? TrickCode.BB : TrickCode.O;
            else
            {
                // 180: F→B or B→F
                if (wake) trickSpinCode = back ? TrickCode.WF : TrickCode.WB;
                else trickSpinCode = back ? TrickCode.F : TrickCode.B;
            }
            SkiAudio.Instance?.TrickSpin(Mathf.RoundToInt(degrees));
            if (wake) SkiAudio.Instance?.TrickWakePop();
        }

        void AdvanceTrickSpin(float dt)
        {
            if (Mathf.Abs(trickSpinRemain) < 0.5f) { if (trickSpinCode != null && trickSlideT <= 0f && trickSpinTarget != 0f) CompleteSpin(); return; }
            float step = Mathf.Sign(trickSpinRemain) * trickSpinSpeed * dt;
            if (Mathf.Abs(step) > Mathf.Abs(trickSpinRemain)) step = trickSpinRemain;
            trickSpinRemain -= step;
            trickYaw += step;
            // fall check: roll while spinning [DA]
            if (Mathf.Abs(leanRightDeg) > 55f) { FailTrick("Fell"); return; }
        }

        void CompleteSpin()
        {
            if (trickSpinCode == null) { trickSpinTarget = 0f; return; }
            var code = trickSpinCode.Value;
            bool wake = trickSpinWake;
            // update facing for 180s
            if (code == TrickCode.B || code == TrickCode.WB) trickFacingBack = true;
            if (code == TrickCode.F || code == TrickCode.WF) trickFacingBack = false;
            // snap yaw to 0/180
            trickYaw = trickFacingBack ? 180f : 0f;
            // wake must have been airborne/pop — Phase A: require crest window or pop
            bool wakeOk = !wake || trickCrestWindow > -0.01f; // already consumed; trust BeginSpin flag
            // Actually wake denial: if classified wake but edge load was weak
            if (Chart[code].wake)
            {
                // re-check: if we didn't have crest/pop at start, BeginSpin still may have set wake from crest — OK
                // if wake trick but skier never edged: deny
            }
            if (wake) SkiAudio.Instance?.TrickWakeLand();
            // yaw error recover
            float err = Mathf.Abs(Mathf.DeltaAngle(trickYaw, trickFacingBack ? 180f : 0f));
            if (err > 45f) { FailTrick("Fell"); return; }
            bool ok = err <= 25f;
            CreditTrick(code, trickSpinRev, Chart[code].wake, ok);
            trickSpinCode = null; trickSpinTarget = 0f; trickSpinRemain = 0f;
        }

        void CreditTrick(TrickCode code, bool reverse, bool wake, bool rotationOk)
        {
            if (tphase != TPhase.Active) return;
            if (TrickClock <= 0f) { LogDenied(code, reverse, 0, "Out of time"); return; }
            if (!rotationOk) { LogDenied(code, reverse, 0, "Rotation incomplete"); return; }
            if (Chart[code].wake && !wake && !trickSpinWake)
            {
                // wake not airborne — 0, not surface downgrade
                LogDenied(code, reverse, 0, "Rotation incomplete"); return;
            }
            string key = $"{trickPass}:{(reverse ? "R" : "")}{code}";
            string basicKey = $"{trickPass}:{code}";
            if (creditedKeys.Contains(key) || (!reverse && creditedKeys.Contains(basicKey)))
            {
                ShowTrickToast("↻ Repeat · 0", false);
                trickLog.Add(new TrickEntry { code = (reverse ? "R" : "") + code, name = Chart[code].name, pts = 0, credited = false, reason = "Repeat", reverse = reverse, pass = trickPass });
                return;
            }
            if (reverse)
            {
                // reverse must follow basic in same pass
                if (trickLastBasicKey != basicKey.Replace("R", "") && trickLastBasicKey != $"{trickPass}:{code}")
                {
                    // still allow if last basic was this code
                    if (trickLastBasicKey == null || !trickLastBasicKey.EndsWith(":" + code) || Time.time - trickLastCreditT > 1.2f)
                    {
                        // treat as basic instead per rules (reverse becomes basic if basic not credited)
                        reverse = false; key = basicKey;
                    }
                }
            }
            int pts = reverse ? Chart[code].rev : Chart[code].bas;
            creditedKeys.Add(key);
            if (!reverse) { creditedKeys.Add(basicKey); trickLastBasicKey = basicKey; }
            trickLastCreditT = Time.time;
            TrickScore += pts;
            trickLog.Add(new TrickEntry { code = (reverse ? "R" : "") + code, name = Chart[code].name, pts = pts, credited = true, reason = "", reverse = reverse, pass = trickPass });
            ShowTrickToast($"✓ Landed +{pts}", true); // b10.26 T17b/T29b: close TBD chip
            SkiAudio.Instance?.TrickCredited(pts, false);
        }

        void LogDenied(TrickCode code, bool reverse, int pts, string reason)
        {
            trickLog.Add(new TrickEntry { code = (reverse ? "R" : "") + code, name = Chart[code].name, pts = 0, credited = false, reason = reason, reverse = reverse, pass = trickPass });
            ShowTrickToast(string.IsNullOrEmpty(reason) ? "✕ Denied" : $"✕ Denied · {reason}", false);
        }

        void ShowTrickToast(string msg, bool ok)
        {
            TrickToast = msg; TrickToastT = 0.9f;
            banner = msg; bannerT = 0.9f;
        }

        void FailTrick(string reason)
        {
            if (trickSpinCode != null)
                LogDenied(trickSpinCode.Value, trickSpinRev, 0, reason);
            trickSpinRemain = 0f; trickSpinCode = null; trickSlideT = 0f;
            SkiAudio.Instance?.TrickFall();
            trickFallThisPass = true;
            EndTrickPass(true, reason);
        }

        void EndTrickPass(bool fell, string why)
        {
            SkiAudio.Instance?.TrickSlide(false);
            // b10.13 SFX B: final horn only on clean end — falls already play splash
            if (!fell) SkiAudio.Instance?.TrickTimer(0f);
            if (fell && trickPass == 1)
            {
                TrickRoundOver = true;
                tphase = TPhase.Results; mode = Mode.Turn;
                banner = "ROUND OVER · FALL IN PASS 1"; bannerT = 4f;
                SkiAudio.Instance?.TrickRunResult();
                return;
            }
            if (trickPass == 1)
            {
                // turnaround then pass 2 opposite direction
                trickPass = 2; tphase = TPhase.Turnaround; mode = Mode.Turn; turnaround = true;
                banner = "PASS 1 DONE · TURNAROUND · PASS 2 OPPOSITE"; bannerT = 3f;
                // flip direction for pass 2
                // after turnaround the boat will be on opposite course; Approach will re-arm
                tphase = TPhase.Approach;
                BeginSink(); // reuse sink→DWS→turn feel
            }
            else
            {
                tphase = TPhase.Results; mode = Mode.Turn;
                banner = $"TRICKS TOTAL · {TrickScore}"; bannerT = 5f;
                SkiAudio.Instance?.TrickRunResult();
            }
        }

        /// <summary>Called from ApplyVisuals to yaw the ski/skier for trick rotation.</summary>
        void ApplyTrickYawPose()
        {
            if (!trickMode || skier == null) return;
            if (tphase != TPhase.Active && tphase != TPhase.Armed && trickSlideT <= 0f && !TrickSpinning) return;
            // extra yaw on ski root about up
            if (ski != null)
            {
                var e = ski.localEulerAngles;
                ski.localRotation = Quaternion.Euler(e.x, trickYaw, e.z);
            }
        }

        public void CloseTrickResults()
        {
            if (tphase != TPhase.Results) return;
            tphase = TPhase.Off;
            // stay in trick mode for another round or return to title — leave gear on
        }

        public string TrickResultsSummary()
        {
            var sb = new System.Text.StringBuilder();
            int p1 = 0, p2 = 0;
            foreach (var e in trickLog)
            {
                if (e.credited) { if (e.pass == 1) p1 += e.pts; else p2 += e.pts; }
                string mark = e.credited ? "✓" : "✕";
                string tail = e.credited ? $" +{e.pts}" : (string.IsNullOrEmpty(e.reason) ? " 0" : $" 0 · {e.reason}");
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"{mark} P{e.pass} {e.code} {e.name}{tail}");
            }
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"Pass 1: {p1} · Pass 2: {p2} · Total {TrickScore}");
            if (TrickRoundOver) sb.Append(" · Round over (fall pass 1)");
            return sb.ToString();
        }
    }
}

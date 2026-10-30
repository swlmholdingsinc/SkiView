using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>
    /// Pass 18: game audio after James's UNITY-AUDIO-SPEC.md, the practical subset. Groups are script faders (no AudioMixer, so the
    /// same code works on Web where the mixer is volume-only): Default Boat −3 · Skier 0 · Ambient −6 · UI −4 · Music −2 dB, plus the
    /// per-camera offsets (§0.4) and scripted ducks (§5: Results, Crash, Jump flight, Boat-over-ambient). Engine idle / cruise / wake /
    /// hull slap follow the §2.1 speed curves (rpm leads the hull, off-throttle pitch drop at the sink, load bump out of the turn);
    /// the onboard idle replaces the outside one on the boat cam. Events are read from the game state each frame (mode / jump phase
    /// changes, SkierSim wake-crossing and buoy counters, SkipCount): the turnaround (sink plunge, float lap, throttle-up, rope tug,
    /// plow), falls / big crashes, jump ramp hit / run-up hiss / lip pop / whoosh / flight wind / landing, buoy and gate ticks,
    /// result stings, shore wash. Ambient = the lake's baked bed (SkiViewGame.ambientClip, in the lake pack) on two 2D sources
    /// crossfading 1 s before the end (the Web AAC loop-click fix). On WebGL nothing starts before the first tap / click / key
    /// (browser autoplay rule); the game is already in its opening deep-water start then, so the throttle-up plays on that input.
    /// </summary>
    public class SkiAudio : MonoBehaviour
    {
        public static SkiAudio Instance { get; private set; }
        SkiViewGame game; SkiAudioBank bank;
        bool started;
        AudioSource idle, idleOn, idleDrv, cruise, rpmLo, rpmHi, rpmPres, trickSlide, title, wake, hull, floatLap, wind, ambA, ambB, music; float birdAt = -1f; float ambSwapAt = -1f; bool ambOnA = true;
        readonly List<AudioSource> pool3 = new List<AudioSource>(), pool2 = new List<AudioSource>();
        float rpm01, rpmVel, lastSpeed, loadBump, offThrottle, camBoat, camSkier, camAmb, duckBoat, duckSkier, duckAmb, duckMusic;
        float duckUntil, duckRelease = 1.2f, dBoat, dSkier, dAmb, dMusic; float sprayPrev; int ui = 0; float plowT = -1f, sinkPlungeAt = -1f, washAt = -1f, throttleUpT = -1f;
        AudioSource throttleSrc, plowSrc;
        SkiViewGame.Mode modePrev; SkiViewGame.JPhase jPrev; int crossPrev, skipPrev; float buoysPrev; bool gatesPrev; string lakePrev;
        readonly Dictionary<string, float> cool = new Dictionary<string, float>();
        // audio passes 4-8 (T13 §6-§10): RPM layers, title loop, jump-miss sting, wake thumps, rope slap, trick set
        bool rpmLayers; bool titlePrev; string jResPrev = ""; float altStingAt = -1f, slapAt = -1f, planingAt = -1f, chimeAt = -1f; string chimeId; float cutPrev = float.NaN; int beepPrev = 99;

        public static SkiAudio Ensure(SkiViewGame g)
        {
            if (Instance == null)
            {
                var b = Resources.Load<SkiAudioBank>("SkiViewAudio/Bank");
                if (b == null) { Debug.LogWarning("[SkiView] audio: Resources/SkiViewAudio/Bank missing, audio off"); return null; }
                var go = new GameObject("SkiAudio"); DontDestroyOnLoad(go);
                Instance = go.AddComponent<SkiAudio>(); Instance.bank = b; Instance.Setup();
            }
            Instance.Attach(g);
            return Instance;
        }
        static float Db(float db) => db <= -79f ? 0f : Mathf.Pow(10f, db / 20f);
        AudioSource Src(string name, bool is3D, float min, float max, float doppler, int pri)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = is3D ? 1f : 0f; s.minDistance = min; s.maxDistance = max;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.dopplerLevel = doppler; s.priority = pri; return s;
        }
        AudioSource Loop(string name, string id, bool is3D, float min, float max, float doppler, int pri)
        {
            var s = Src(name, is3D, min, max, doppler, pri); var e = bank.Get(id); if (e == null) return s;
            s.clip = e.clip; s.loop = true; s.volume = 0f; return s;
        }
        void Setup()
        {
            idle = Loop("EngineIdle", "boat_idle_loop", true, 6, 150, 0.5f, 20);
            idleOn = Loop("EngineIdleOnboard", "boat_idle_onboard_loop", true, 2, 60, 0f, 20); idleOn.spatialBlend = 0.3f;
            idleDrv = Loop("EngineIdleDriver", "boat_engine_idle_driver_loop", true, 2, 60, 0f, 20); idleDrv.spatialBlend = 0.3f;   // pass 4: layer under the onboard idle (boat cam)
            // pass 8 (T13 §10.1): three RPM layers replace EngineCruise; falls back to the v8 cruise loop if the bank was not rebuilt
            rpmLo = Loop("EngineRpmLow", "boat_engine_rpm_low_loop", true, 8, 250, 1f, 15);
            cruise = Loop("EngineRpmMid", "boat_engine_rpm_mid_loop", true, 8, 250, 1f, 15);
            rpmHi = Loop("EngineRpmHigh", "boat_engine_rpm_high_loop", true, 8, 250, 1f, 15);
            rpmPres = Loop("EngineRpmPresence", "boat_engine_rpm_high_presence_loop", true, 8, 250, 1f, 16);   // pass 10: brightness for the slowed high layer, 24-30 mph
            rpmLayers = rpmLo.clip != null && cruise.clip != null && rpmHi.clip != null;
            if (!rpmLayers) { Debug.LogWarning("[SkiView] audio: rpm layers missing from Bank, using boat_cruise_v8_loop"); cruise = Loop("EngineCruise", "boat_cruise_v8_loop", true, 8, 250, 1f, 15); }
            trickSlide = Loop("TrickSlide", "skier_trick_slide_loop", true, 3, 60, 0f, 40);
            wake = Loop("WakeHiss", "boat_wake_loop", true, 4, 60, 0.5f, 40);
            hull = Loop("HullSlap", "boat_hull_slap_loop", true, 3, 30, 0f, 60);
            floatLap = Loop("FloatLap", "skier_float_lap_loop", true, 2, 25, 0f, 60);
            wind = Loop("SpeedWind", "skier_flight_wind_loop", false, 1, 10, 0f, 70);
            music = Loop("MenuMusic", "music_menu_loop", false, 1, 10, 0, 10); music.ignoreListenerPause = true;   // pass 19: title / pause / lake select / results bed
            title = Loop("TitleMusic", "music_title_loop", false, 1, 10, 0, 10); title.ignoreListenerPause = true;   // pass 7: new main-menu loop (Src -4)
            ambA = Src("AmbientA", false, 1, 10, 0, 50); ambB = Src("AmbientB", false, 1, 10, 0, 50);
            for (int i = 0; i < 10; i++) pool3.Add(Src("Sfx3D_" + i, true, 4, 120, 0, 30));
            for (int i = 0; i < 5; i++) { var s = Src("Sfx2D_" + i, false, 1, 10, 0, 128); s.ignoreListenerPause = true; pool2.Add(s); }
            started = Application.platform != RuntimePlatform.WebGLPlayer;
        }
        void Attach(SkiViewGame g)
        {
            game = g; modePrev = g.mode; jPrev = g.jphase; crossPrev = g.sim != null ? g.sim.wakeCrossings : 0; skipPrev = g.SkipCount;
            buoysPrev = 0f; gatesPrev = false;
            if (lakePrev != g.lakeId)
            {
                bool loadIn = !string.IsNullOrEmpty(lakePrev); lakePrev = g.lakeId; StartAmbient(g.ambientClip);
                shoreDetail = null; shoreDetailLake = null; reedAt = lapAt = -1f;
                // NextLake load-in: distant boat pass-by (BoatPassDistant) timed from the camera's distance to the boat
                if (loadIn && started && Camera.main != null) { var bp = g.BoatPos; BoatPassDistant(bp, Vector3.Distance(Camera.main.transform.position, bp) / Mathf.Max(g.BoatSpeed, 5f)); }
            }
            if (started && g.mode == SkiViewGame.Mode.DeepStart) ThrottleUp();
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnApplicationPause(bool p) { AudioListener.pause = p; }

        // ---------- one-shots ----------
        bool Cool(string id, float s) { float t; if (cool.TryGetValue(id, out t) && Time.unscaledTime < t) return false; cool[id] = Time.unscaledTime + s; return true; }
        AudioSource Free(List<AudioSource> pool) { AudioSource best = pool[0]; foreach (var s in pool) { if (!s.isPlaying) return s; if (s.time > best.time) best = s; } return best; }
        public AudioSource Play(string id, Vector3? at, float srcDb, float group, float pitch = 1f, float jitter = 0f, float startTime = 0f, float cooldown = 0f)
        {
            if (!started || bank == null) return null;
            if (cooldown > 0f && !Cool(id, cooldown)) return null;
            var e = bank.Get(id); if (e == null || e.clip == null) return null;
            var s = Free(at.HasValue ? pool3 : pool2);
            s.clip = e.clip; s.loop = false; s.pitch = pitch * (1f + Random.Range(-jitter, jitter));
            s.volume = Mathf.Clamp01(Db(srcDb + group)); if (at.HasValue) s.transform.position = at.Value;
            s.time = Mathf.Clamp(startTime, 0f, e.clip.length - 0.05f); s.Play(); return s;
        }
        // pass 19: ?mute=boat,skier,amb,ui,music and ?solo=<group> (James's captures)
        static float M(string g) => (SkiViewArgs.Has("solo") && SkiViewArgs.Get("solo") != g) || SkiViewArgs.Listed("mute", g) || SkiViewArgs.Listed("mute", "all") ? -80f : 0f;
        float GBoat => -3f + camBoat + dBoat + M("boat"); float GSkier => camSkier + dSkier + M("skier"); float GAmb => -6f + camAmb + dAmb + M("amb"); float GUI => -4f + M("ui"); float GMusic => -2f + dMusic + M("music");
        public void UiClick() { string[] c = { "ui_click_01", "ui_click_02", "ui_click_03", "ui_click_04" }; ui = (ui + 1 + Random.Range(0, 3)) % 4; Play(c[ui], null, -10f, GUI, 1f, 0.03f, 0f, 0.05f); }
        public void UiConfirm() => Play("ui_confirm", null, -9f, GUI, 1f, 0.02f, 0f, 0.15f);
        public void UiBack() => Play("ui_back", null, -10f, GUI, 1f, 0f, 0f, 0.15f);
        void Duck(float boat, float skier, float amb, float music, float hold, float release)
        {
            dBoat = Mathf.Min(dBoat, boat); dSkier = Mathf.Min(dSkier, skier); dAmb = Mathf.Min(dAmb, amb); dMusic = Mathf.Min(dMusic, music);
            duckBoat = boat; duckSkier = skier; duckAmb = amb; duckMusic = music; duckUntil = Time.time + hold; duckRelease = release;
        }

        void ThrottleUp()
        {
            bool onboard = game.camMode == SkiViewGame.CamMode.Broadcast;
            throttleSrc = onboard ? Play("boat_throttle_up_onboard", game.BoatPos, -1.2f, GBoat, 1f, 0.02f, 3.0f, 2f) : Play("boat_throttle_up", game.BoatPos, 0f, GBoat, 1f, 0.02f, 0f, 2f);
            throttleUpT = Time.time; rpm01 = Mathf.Max(rpm01, 0.6f);
            Play("boat_gear_clunk", game.BoatPos, -4f, GBoat, 1f, 0.03f, 0f, 1f);
            Play("skier_rope_tug", game.SkierPos, -4f, GSkier, 1f, 0.05f, 0f, 0.5f);
            Play("skier_dws_rope_tension", game.SkierPos, 4f, GSkier, 1f, 0.04f, 0f, 0.5f);   // pass 4: +4 dB source boost
            Play("skier_dws_splash", game.SkierPos, 0f, GSkier, 1f, 0.04f, 0f, 0.5f);
            plowT = Time.time + 0.9f; planingAt = Time.time + 1.8f;
        }

        // ---------- per frame ----------
        void LateUpdate()
        {
            if (game == null || bank == null) return;
            if (!started)
            {
                if (Input.anyKeyDown || Input.touchCount > 0 || Input.GetMouseButtonDown(0))
                { started = true; StartAmbient(game.ambientClip); if (game.mode == SkiViewGame.Mode.DeepStart || game.mode == SkiViewGame.Mode.Turn) ThrottleUp(); }
                else return;
            }
            float dt = Time.deltaTime; Vector3 bp = game.BoatPos, sp = game.SkierPos;
            foreach (var s in new[] { idle, idleOn, idleDrv, cruise, rpmLo, rpmHi, rpmPres, hull }) s.transform.position = bp;
            trickSlide.transform.position = sp;
            wake.transform.position = bp - game.BoatFwd * 2.5f; floatLap.transform.position = sp;
            // camera offsets (§0.4), blended
            var cm = game.camMode; float tb = 0, ts = 0, ta = 0;
            if (cm == SkiViewGame.CamMode.CloseChase) { tb = -1; ts = 2; } else if (cm == SkiViewGame.CamMode.FirstPerson) { tb = -3; ts = 4; ta = -2; } else if (cm == SkiViewGame.CamMode.Broadcast) { tb = 3; ts = -3; ta = -2; }
            float kc = 1f - Mathf.Exp(-dt / 0.35f); camBoat += (tb - camBoat) * kc; camSkier += (ts - camSkier) * kc; camAmb += (ta - camAmb) * kc;
            // ducks release
            if (Time.time > duckUntil) { float kr = dt / Mathf.Max(0.1f, duckRelease) * 8f; dBoat = Mathf.MoveTowards(dBoat, 0, kr); dSkier = Mathf.MoveTowards(dSkier, 0, kr); dAmb = Mathf.MoveTowards(dAmb, 0, kr); dMusic = Mathf.MoveTowards(dMusic, 0, kr); }
            Events(sp, bp);
            Engine(dt, cm);
            Skier(dt, sp);
            Ambient(dt);
            Music(dt); Birds(); SfxArg(); PassBy(); ShowPA(); ShoreEmitters(); Pending();
        }

        // ---------- pass 19: menu music, shore birds, tournament PA / scoreboard / applause ----------
        void Music(float dt)
        {
            if (music == null || music.clip == null) return;
            bool titleOpen = SkiView.UI.SkiViewUI.Instance?.TitleOpen ?? false;
            if (title != null && title.clip != null)
            {   // pass 7: the title screen gets music_title_loop (Src -4), every other menu keeps music_menu_loop (Src -5.3)
                float tt = titleOpen ? Db(-4f + GMusic) : 0f;
                title.volume = Mathf.MoveTowards(title.volume, tt, Time.unscaledDeltaTime / (titleOpen ? 1.0f : 0.8f) * Mathf.Max(Db(-4f + GMusic), 0.05f));
                if (title.volume > 0f && !title.isPlaying) title.Play(); else if (title.volume <= 0f && title.isPlaying && !titleOpen) title.Pause();
            }
            else titleOpen = false;
            bool want = (!titleOpen && (SkiView.UI.SkiViewUI.Instance?.MenuOpen ?? false)) || SkiViewArgs.On("music");
            float target = want ? Db(-5.3f + GMusic) : 0f;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime / (want ? 1.0f : 0.8f) * Mathf.Max(Db(-5.3f + GMusic), 0.05f));
            if (music.volume > 0f && !music.isPlaying) music.Play(); else if (music.volume <= 0f && music.isPlaying && !want) music.Pause();
        }
        static readonly Dictionary<string, string[]> BIRDS = new Dictionary<string, string[]>
        {
            { "marrowind", new[] { "amb_bird_sandhill_crane", "amb_bird_osprey", "amb_bird_anhinga" } },
            { "heronmarsh", new[] { "amb_bird_sandhill_crane", "amb_bird_osprey", "amb_bird_anhinga" } },
            { "villaserena", new[] { "amb_bird_sandhill_crane" } }, { "harrowpond", new[] { "amb_bird_osprey" } },
        };
        static readonly Dictionary<string, float> BIRD_DB = new Dictionary<string, float> { { "amb_bird_sandhill_crane", -9.1f }, { "amb_bird_osprey", -15f }, { "amb_bird_anhinga", -17f } };
        void Birds()
        {
            if (game == null || !BIRDS.TryGetValue(game.lakeId, out var ids)) return;
            if (birdAt < 0f) { birdAt = Time.time + Random.Range(20f, 60f); return; }
            if (Time.time < birdAt) return; birdAt = Time.time + Random.Range(45f, 120f);
            var cam = Camera.main; if (cam == null) return;
            float a = Random.Range(0f, Mathf.PI * 2f); Vector3 at = cam.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(90f, 180f); at.y = 6f;
            string id = ids[Random.Range(0, ids.Length)]; var s = Play(id, at, BIRD_DB[id], GAmb, 1f, 0.03f);
            if (s != null) { s.minDistance = 20f; s.maxDistance = 400f; }
        }
        bool sfxDone; float sfxNext;
        void SfxArg()
        {   // ?sfx=<bank id>[&repeat=<s>]: audition one bank sound (2D, its Target level) for level checks
            string id = SkiViewArgs.Get("sfx"); if (id == null || (sfxDone && SkiViewArgs.F("repeat", 0f) <= 0f) || Time.time < sfxNext) return;
            var e = bank.Get(id); if (e == null) { sfxDone = true; return; }
            Play(id, null, Mathf.Min(0f, -20f - e.lufs), 0f); sfxDone = true; sfxNext = Time.time + Mathf.Max(0.5f, SkiViewArgs.F("repeat", 0f));
        }
        const float EdgeTrim = 5f;   // James: skier_edge_change sits +5 dB over the skier group
        float paAt = -1f;
        void ShowPA()
        {   // audio pass 3: distant show-PA announcement now and then at the tournament / show lakes
            if (game == null || !(Tournament || game.lakeId == "marrowind")) return;
            if (paAt < 0f) { paAt = Time.time + Random.Range(40f, 90f); return; }
            if (Time.time < paAt) return; paAt = Time.time + Random.Range(90f, 180f);
            Play("amb_show_pa_announce", null, -14f, GAmb, 1f, 0f, 0f, 60f);
        }
        bool jcPrev; float passbyCool = -1f;
        void PassBy()
        {   // §1: BoatPassClose - the jump shore camera cuts in, the boat roars past it (baked Doppler; peak ~22 s into the clip)
            if (game == null) return; bool on = game.JumpCamOn;
            if (on && !jcPrev && Time.time > passbyCool && Camera.main != null)
            {
                var cam = Camera.main.transform.position; var bp = game.BoatPos; float d = Vector3.Distance(cam, bp), v = Mathf.Max(game.BoatSpeed, 5f);
                var s = Play("boat_passby_fast", cam + (bp - cam).normalized * 10f, -0.3f, GBoat, 1f, 0f, Mathf.Max(0f, 22f - d / v), 30f);   // pass 8/9: Src -6.1 -> -0.3 (pass-9 file, CPA loudness matched)
                if (s != null) { s.minDistance = 10f; s.maxDistance = 250f; s.dopplerLevel = 0f; }
                passbyCool = Time.time + 30f;
            }
            jcPrev = on;
            // pass 8: BoatPassDistant when the title screen opens (fixed, distant camera)
            bool tOpen = SkiView.UI.SkiViewUI.Instance?.TitleOpen ?? false;
            if (tOpen && !titlePrev && Camera.main != null) { var cam = Camera.main.transform.position; var bp = game.BoatPos; BoatPassDistant(bp, Vector3.Distance(cam, bp) / Mathf.Max(game.BoatSpeed, 5f)); }
            titlePrev = tOpen;
        }
        /// <summary>Pass 8 (§1 BoatPassDistant): distant boat passby, Src -6.1 (pass 9 file), baked Doppler (closest approach ~20 s into the clip).
        /// Called on NextLake load-in (Attach) and when the title opens. TODO caller: a dedicated lake intro flyover does not exist yet; it should call this with the boat's closest-approach point and the seconds until it.</summary>
        public void BoatPassDistant(Vector3 cpaPoint, float tCPA)
        {
            var s = Play("boat_passby", cpaPoint, -6.1f, GBoat, 1f, 0f, Mathf.Max(0f, 20f - tCPA), 30f);
            if (s != null) { s.minDistance = 15f; s.maxDistance = 400f; s.dopplerLevel = 0f; }
        }
        bool Tournament => game != null && game.lakeId == "saltwhite";

        static float Lerp(float[] xs, float[] ys, float x)
        {
            if (x <= xs[0]) return ys[0]; for (int i = 1; i < xs.Length; i++) if (x <= xs[i]) return Mathf.Lerp(ys[i - 1], ys[i], (x - xs[i - 1]) / (xs[i] - xs[i - 1])); return ys[ys.Length - 1];
        }
        static readonly float[] MPH = { 0, 3, 6, 10, 14, 20, 30, 34, 36 };
        static readonly float[] IDLE_V = { 0, 0, 0, -6, -40, -40, -40, -40, -40 }, IDLE_P = { 0.95f, 1.0f, 1.05f, 1.12f, 1.15f, 1.15f, 1.15f, 1.15f, 1.15f };
        static readonly float[] CRU_V = { -40, -40, -40, -12, -4, -1, 0, 0, 0 }, CRU_P = { 0.74f, 0.74f, 0.74f, 0.78f, 0.84f, 0.90f, 0.97f, 1.0f, 1.02f };
        // pass 9 (T13 §10.1 rev 9): one rpm curve drives all layers, pitch = rpm / layer native rpm (low 2696, mid 3370, high 4811),
        // so the firing orders line up through the crossfades; the new high layer is its own high-load V8, mid hands over 24-34 mph
        static readonly float[] RMPH = { 0, 3, 6, 10, 14, 18, 20, 24, 26, 30, 34, 36 };
        static readonly float[] RPM = { 2793, 2793, 2793, 2944, 3170, 3321, 3397, 3502, 3650, 4100, 4650, 4900 };
        const float NAT_LO = 2696f, NAT_MID = 3370f, NAT_HI = 4811f;
        static readonly float[] LO_V = { -40, -40, -20, -3, 0, -2, -6, -40, -40, -40, -40, -40 };
        static readonly float[] MID_V = { -40, -40, -40, -40, -14, -3, 0, 0, -0.5f, -5, -22, -40 };
        static readonly float[] HI_V = { -40, -40, -40, -40, -40, -40, -40, -13, -4, 0, 1, 1.5f };   // pass 10: +1 / +1.5 at 34 / 36 mph
        // pass 10: presence layer (excited 2-9 kHz top of the high layer) at the high-layer pitch, only where the high layer plays slow
        static readonly float[] PRES_V = { -40, -40, -40, -40, -40, -40, -40, -24, -15, -19, -40, -40 };
        static readonly float[] WAKE_V = { -40, -40, -40, -20, -12, -4, -1, 0, 0 }, WAKE_P = { 0.88f, 0.88f, 0.88f, 0.88f, 0.92f, 0.95f, 0.98f, 1.0f, 1.01f };
        static readonly float[] HULL_V = { 0, -3, -12, -40, -40, -40, -40, -40, -40 };
        void Engine(float dt, SkiViewGame.CamMode cm)
        {
            float v = game.BoatSpeed, mph = v * 2.237f;
            bool dws = game.mode == SkiViewGame.Mode.DeepStart; bool sink = game.mode == SkiViewGame.Mode.Sink;
            float demand = dws ? 1f : sink ? 0.12f : 0f;
            float want = Mathf.Max(mph / 34f, demand);
            rpm01 = Mathf.SmoothDamp(rpm01, want, ref rpmVel, want > rpm01 ? 0.15f : 0.6f);
            float accel = (v - lastSpeed) / Mathf.Max(1e-3f, dt); lastSpeed = v;
            loadBump = Mathf.MoveTowards(loadBump, accel > 1f && game.mode == SkiViewGame.Mode.Turn ? 1f : 0f, dt / 0.8f);
            offThrottle = Mathf.MoveTowards(offThrottle, sink ? 1f : 0f, dt / 0.3f);
            float m = rpm01 * 34f; bool onboard = cm == SkiViewGame.CamMode.Broadcast;
            float idleDb = Lerp(MPH, IDLE_V, m), idleP = Lerp(MPH, IDLE_P, m);
            if (throttleUpT > 0f && Time.time - throttleUpT < 0.8f) idleDb = Mathf.Lerp(idleDb, -40f, (Time.time - throttleUpT) / 0.8f);
            idle.volume = Mathf.MoveTowards(idle.volume, onboard ? 0f : Db(-6f + idleDb + GBoat), dt / 0.7f); idle.pitch = idleP;
            idleOn.volume = Mathf.MoveTowards(idleOn.volume, onboard ? Db(-4f + idleDb + GBoat) : 0f, dt / 0.7f); idleOn.pitch = idleP;
            idleDrv.volume = Mathf.MoveTowards(idleDrv.volume, onboard ? Db(-10f + idleDb + GBoat) : 0f, dt / 0.7f); idleDrv.pitch = idleP;
            float bend = 0.04f * loadBump - 0.06f * offThrottle, rpm = Lerp(RMPH, RPM, m);
            if (rpmLayers)
            {
                rpmLo.volume = Db(-4f + Lerp(RMPH, LO_V, m) + 2f * loadBump + GBoat); rpmLo.pitch = rpm / NAT_LO + bend;
                cruise.volume = Db(-4f + Lerp(RMPH, MID_V, m) + 2f * loadBump + GBoat); cruise.pitch = rpm / NAT_MID + bend;
                rpmHi.volume = Db(-4f + Lerp(RMPH, HI_V, m) + 2f * loadBump + GBoat); rpmHi.pitch = rpm / NAT_HI + bend;
                if (rpmPres.clip != null) { rpmPres.volume = Db(-4f + Lerp(RMPH, PRES_V, m) + 2f * loadBump + GBoat); rpmPres.pitch = rpmHi.pitch; }
            }
            else { cruise.volume = Db(-4f + Lerp(MPH, CRU_V, m) + 2f * loadBump + GBoat); cruise.pitch = Lerp(MPH, CRU_P, m) + bend; }
            wake.volume = Db(-8f + Lerp(MPH, WAKE_V, mph) + GBoat); wake.pitch = Lerp(MPH, WAKE_P, mph);
            hull.volume = Db(-8.9f + Lerp(MPH, HULL_V, mph) + (onboard ? 4f : 0f) + GBoat);
            foreach (var s in new[] { idle, idleOn, idleDrv, cruise, rpmLo, rpmHi, rpmPres, wake, hull }) if (!s.isPlaying && s.clip != null) { s.time = Random.Range(0f, s.clip.length * 0.9f); s.Play(); }
            // the throttle-up one-shot fades out 3.0 -> 4.5 s after its trigger (onboard 3.5 -> 5.0)
            if (throttleSrc != null && throttleSrc.isPlaying && throttleUpT > 0f)
            {
                float a = Time.time - throttleUpT, f0 = onboard ? 3.5f : 3.0f; if (a > f0) throttleSrc.volume = Mathf.Max(0f, throttleSrc.volume - dt / 1.5f * Db(GBoat)); throttleSrc.transform.position = game.BoatPos;
            }
        }
        static readonly float[] SHEET = { 0, 0.10f, 0.25f, 0.40f, 0.60f, 0.85f }, SHEET_V = { -40, -22, -14, -8, -4, 0 };
        static readonly float[] SKMPH = { 15, 25, 34, 45, 55 }, SKP = { 0.90f, 0.95f, 1.0f, 1.04f, 1.08f };
        void Skier(float dt, Vector3 sp)
        {
            float speed = game.SkierSpeed, sp01 = Mathf.InverseLerp(4f, 16f, speed);
            float carve = game.sim != null ? Mathf.Clamp01(Mathf.Abs(game.sim.omega) / 1.6f) : 0f;
            float sheet = sp01 * Mathf.Clamp01(Mathf.Pow(game.SprayEdge, 1.5f) * 0.6f + carve * 0.9f);
            bool sprayOn = !game.JumpGearOn && game.mode != SkiViewGame.Mode.Fallen && game.SkierDepth < 0.4f && game.mode != SkiViewGame.Mode.Jump;
            if (sprayOn && sprayPrev < 0.25f && sheet >= 0.25f)
                Play("skier_carve_spray", sp, Lerp(SHEET, SHEET_V, sheet), GSkier, Lerp(SKMPH, SKP, speed * 2.237f), 0.05f, new[] { 0f, 0.15f, 0.3f }[Random.Range(0, 3)], 0.6f);
            sprayPrev = sheet;
            // floating skier lap (sink / fallen after 1.6 s)
            bool floating = (game.mode == SkiViewGame.Mode.Sink && game.SkierDepth > 0.4f) || (game.mode == SkiViewGame.Mode.Fallen);
            floatLap.volume = Mathf.MoveTowards(floatLap.volume, floating ? Db(-10.2f + GSkier) : 0f, dt / (floating ? 1f : 0.5f));
            if (floatLap.volume > 0f && !floatLap.isPlaying && floatLap.clip != null) floatLap.Play(); else if (floatLap.volume <= 0f && floatLap.isPlaying) floatLap.Stop();
            // speed wind: jump flight, and first person at speed
            bool flight = game.jphase == SkiViewGame.JPhase.Flight; float windDb = flight ? -14f : game.camMode == SkiViewGame.CamMode.FirstPerson ? -14f - 18f * (1f - sp01) : -80f;
            wind.volume = Mathf.MoveTowards(wind.volume, Db(windDb + GSkier), dt / 0.3f);
            if (wind.volume > 0f && !wind.isPlaying && wind.clip != null) wind.Play(); else if (wind.volume <= 0f && wind.isPlaying) wind.Stop();
            // plow rush in the DWS rise
            if (plowT > 0f && Time.time >= plowT) { plowT = -1f; plowSrc = Play("skier_plow_rush", sp, -2.4f, GSkier, 1f, 0.03f); }
            if (plowSrc != null && plowSrc.isPlaying) { plowSrc.transform.position = sp; if (game.mode != SkiViewGame.Mode.DeepStart) plowSrc.volume = Mathf.Max(0f, plowSrc.volume - dt / 0.3f); }
            if (sinkPlungeAt > 0f && Time.time >= sinkPlungeAt) { sinkPlungeAt = -1f; Play("skier_splash_fall", sp, -10.6f, GSkier, 0.9f, 0.02f); }
            if (washAt > 0f && Time.time >= washAt) { washAt = -1f; if (game.camMode != SkiViewGame.CamMode.FirstPerson) Play("boat_wake_shore_wash", Camera.main != null ? Camera.main.transform.position + Camera.main.transform.right * 30f : sp, -8.4f, GBoat, 1f, 0.04f, 0f, 25f); }
        }

        void Events(Vector3 sp, Vector3 bp)
        {
            var m = game.mode; var j = game.jphase; var sim = game.sim;
            if (m != modePrev)
            {
                if (m == SkiViewGame.Mode.Pass && Tournament) Play("amb_pa_chime", null, -12.5f, GAmb, 1f, 0f, 0f, 20f);
                if (m == SkiViewGame.Mode.Sink)
                {
                    if (Tournament) { Play("ui_scoreboard_beep", null, -12f, GUI, 1f, 0f, 0f, 2f); }
                    if ((Tournament || game.lakeId == "marrowind") && sim != null && buoysPrev >= 5.99f) Play("amb_crowd_applause_cheer", null, -3.8f, GAmb, 1f, 0.02f, 0f, 10f);   // full pass: results sting + duck, throttle chopped, sink plunge at 0.6 s, decel layer, shore wash 8 s later
                    Play("ui_results_sting", null, -5.1f, GMusic, 1f, 0f, 0f, 3f); Duck(-8, -6, -6, 0, 1.6f, 1.2f);
                    Play("boat_engine_decel_v8", bp, -1.9f, GBoat, 1f, 0.02f, 0f, 2f); Play("boat_gear_clunk", bp, -4f, GBoat, 0.95f, 0.02f, 0f, 1f);
                    sinkPlungeAt = Time.time + 0.6f; washAt = Time.time + 8f;
                }
                else if (m == SkiViewGame.Mode.DeepStart && (modePrev == SkiViewGame.Mode.Sink || modePrev == SkiViewGame.Mode.Fallen)) ThrottleUp();
                else if (m == SkiViewGame.Mode.Fallen)
                {
                    bool big = modePrev == SkiViewGame.Mode.Jump || game.SkierSpeed > 12f || (sim != null && sim.message != null && (sim.message.Contains("edge") || sim.message.Contains("Hit buoy")));
                    if (big) Play("skier_big_crash", sp, 0f, GSkier + 4f, 1f, 0.03f, modePrev == SkiViewGame.Mode.Jump ? 0.2f : 0f, 1f);
                    else Play("skier_splash_fall", sp, -0.6f, GSkier, 1f, 0.04f, 0f, 1f);
                    Duck(-6, 0, -4, -4, 0.8f, 1.5f);
                    if (modePrev == SkiViewGame.Mode.Pass) Play("ui_results_sting_fail", null, -8.6f, GMusic, 1f, 0f, 0f, 3f);
                    washAt = Time.time + 8f; slapAt = Time.time + 0.5f;   // pass 8: the loose handle slaps the water
                }
                else if (m == SkiViewGame.Mode.Turn && modePrev == SkiViewGame.Mode.Jump) washAt = Time.time + 8f;
                modePrev = m;
            }
            if (j != jPrev)
            {
                if (j == SkiViewGame.JPhase.Ramp) { Play("skier_jump_ramp_hit", sp, game.JumpToeSpeed < 1f ? -3f : 0f, GSkier + 4f, 1f, 0.03f); Play("skier_jump_runup_hiss", sp, -5.7f, GSkier, 1f, 0.03f); }
                else if (j == SkiViewGame.JPhase.Flight) { Play("skier_jump_lip_pop", sp, 0f, GSkier, 1f, 0.03f); Play("skier_jump_whoosh", sp, -5f, GSkier, 1f, 0.03f); Duck(-4, 1, -3, 0, 2.5f, 0.3f); }
                else if (j == SkiViewGame.JPhase.Landing) { if (HeavyLanding) Play("skier_jump_landing_heavy", sp, 0f, GSkier + 4f, 1f, 0.03f); else Play("skier_ski_landing", sp, 0f, GSkier + 4f, 1f, 0.03f, 0.1f); TouchdownVz = float.NaN; Play("ui_results_sting", null, -5.1f, GMusic, 1f, 0f, 0f, 3f); duckUntil = Time.time; Duck(-8, -6, -6, 0, 1.6f, 1.2f); }
                jPrev = j;
            }
            if (sim != null)
            {
                if (sim.wakeCrossings != crossPrev)
                {
                    crossPrev = sim.wakeCrossings; float o = sim.crossOmega01;
                    if (o > 0.15f && game.SkierDepth < 0.4f) Play("skier_edge_change", sp, (o >= 0.8f ? 0f : o >= 0.5f ? Mathf.Lerp(-6f, 0f, (o - 0.5f) / 0.3f) : Mathf.Lerp(-12f, -6f, (o - 0.2f) / 0.3f)), GSkier + EdgeTrim, game.SkierSpeed * 2.237f > 45f ? 1.04f : 1f, 0.06f, 0f, 0.35f);
                    if (game.SkierDepth < 0.4f && m != SkiViewGame.Mode.Jump)
                    {   // pass 8: wake thump (light < 25 mph, heavy >= 25 mph on Impacts +4); edge chatter on hard edges
                        float smph = game.SkierSpeed * 2.237f;
                        if (smph < 25f) Play("skier_wake_thump_light", sp, 0f, GSkier, 1f, 0.05f, 0f, 0.3f); else Play("skier_wake_thump_heavy", sp, 0f, GSkier + 4f, 1f, 0.05f, 0f, 0.3f);
                        if (o > 0.5f) Play(Random.value < 0.5f ? "skier_edge_chatter_01" : "skier_edge_chatter_02", sp, 0f, GSkier + EdgeTrim, 1f, 0.05f, 0f, 0.35f);
                    }
                }
                if (sim.passBuoys > buoysPrev + 0.01f && m == SkiViewGame.Mode.Pass)
                    Play("skier_buoy_pass", sp, -5f, GSkier, (sim.nextBuoy % 2 == 1) ? 1.0f : 1.04f, 0.03f, 0f, 0.5f);
                buoysPrev = sim.passBuoys;
                if (sim.gatesCleared && !gatesPrev && m == SkiViewGame.Mode.Pass) Play("ui_gate_tick", null, -10f, GUI);
                gatesPrev = sim.gatesCleared;
            }
            // pass 8: rope slap at the jump cut; jump MISSED / crash -> ui_results_sting_alt (T13 decision H)
            if (!float.IsNaN(game.JumpCutT) && float.IsNaN(cutPrev)) Play("skier_rope_slap_01", sp, -2f, GSkier, 1f, 0.05f, 0f, 0.5f);
            cutPrev = game.JumpCutT;
            string jr = game.JumpResult ?? "";
            if (jr != jResPrev) { if (jr == "MISSED" || jr == "OUT THE FRONT" || jr == "FELL BACK") altStingAt = Time.time + (jr == "MISSED" ? 0.3f : 1.2f); jResPrev = jr; }
            if (game.SkipCount != skipPrev)
            {   // tap-to-skip: confirm, fade one-shots, snap the engine to cruise
                skipPrev = game.SkipCount; UiConfirm();
                foreach (var s in pool3) if (s.isPlaying) s.volume *= 0.2f;
                rpm01 = 1f; throttleUpT = -1f; plowT = -1f; sinkPlungeAt = -1f; cruise.dopplerLevel = 0f; rpmLo.dopplerLevel = 0f; rpmHi.dopplerLevel = 0f; rpmPres.dopplerLevel = 0f; Invoke(nameof(RestoreDoppler), 0.1f);
            }
        }
        void RestoreDoppler() { foreach (var s in new[] { cruise, rpmLo, rpmHi, rpmPres }) if (s != null) s.dopplerLevel = 1f; }

        void StartAmbient(AudioClip c)
        {
            if (!started) return;
            foreach (var a in new[] { ambA, ambB }) a.Stop();
            if (c == null) return;
            ambA.clip = c; ambB.clip = c; ambA.loop = ambB.loop = false;
            ambA.time = Random.Range(0f, c.length * 0.5f); ambA.volume = 0f; ambA.Play(); ambOnA = true;
            ambSwapAt = Time.time + (c.length - ambA.time) - 1f;
        }
        void Ambient(float dt)
        {
            if (ambA.clip == null) return;
            float boatMph = game.BoatSpeed * 2.237f, side = boatMph > 20f ? -3f : boatMph < 10f ? 0f : -3f * (boatMph - 10f) / 10f;   // boat over ambient
            float target = Db((ambA.clip.name.StartsWith("amb_bed_") ? -6f : -14f) + GAmb + side);   // pass 5/8: amb_bed_* beds at Src -6 (Master only), old amb_lake_* at -14
            var on = ambOnA ? ambA : ambB; var off = ambOnA ? ambB : ambA;
            if (ambSwapAt > 0f && Time.time >= ambSwapAt) { off.time = 0f; off.volume = 0f; off.Play(); ambOnA = !ambOnA; var t = on; on = off; off = t; ambSwapAt = Time.time + on.clip.length - 1f; }
            on.volume = Mathf.MoveTowards(on.volume, target, dt / 1.0f * Mathf.Max(target, 0.05f));
            if (off.isPlaying) { off.volume = Mathf.MoveTowards(off.volume, 0f, dt / 1.0f * Mathf.Max(target, 0.05f)); if (off.volume <= 0f) off.Stop(); }
        }

        // ---------- audio passes 4-8: scheduled one-shots ----------
        /// <summary>Touchdown vertical speed in m/s (positive = downward), NaN when unknown.
        /// Set by SkiViewGame.Jump at touchdown (b10.14) before jphase = Landing.</summary>
        public float TouchdownVz { get; set; } = float.NaN;
        /// <summary>Heavy landing (T13 §10): touchdown vertical speed over 7 m/s; falls back to a 30 m+ jump while the speed isn't provided.</summary>
        bool HeavyLanding => game != null && (!float.IsNaN(TouchdownVz) ? TouchdownVz > 7f : game.JumpDistance >= 30f);
        void Pending()
        {
            Vector3 sp = game.SkierPos;
            if (planingAt > 0f && Time.time >= planingAt) { planingAt = -1f; if (game.mode == SkiViewGame.Mode.DeepStart || game.SkierDepth < 0.4f) Play("skier_dws_planing", sp, 0f, GSkier, 1f, 0.03f); }
            if (slapAt > 0f && Time.time >= slapAt) { slapAt = -1f; Play("skier_rope_slap_02", sp, -2f, GSkier, 1f, 0.05f, 0f, 0.5f); }
            if (altStingAt > 0f && Time.time >= altStingAt) { altStingAt = -1f; Play("ui_results_sting_alt", null, -7.3f, GMusic, 1f, 0f, 0f, 3f); }
            if (chimeAt > 0f && Time.unscaledTime >= chimeAt) { chimeAt = -1f; Play(chimeId, null, 0f, GMusic, 1f, 0f, 0f, 0.1f); }
            if (trickSlide.volume > 0f && !trickSlide.isPlaying && trickSlide.clip != null) trickSlide.Play(); else if (trickSlide.volume <= 0f && trickSlide.isPlaying) trickSlide.Stop();
        }

        // ---------- pass 4: Clinic / Beat Wade stingers (Music), tip chime (UI) ----------
        // Clinic cues are called from SkiViewClinic (lesson start / tip / pass / fail). TODO caller: Beat Wade mode does not exist yet;
        // ui_beatwade_near_miss.ogg and ui_beatwade_win.ogg are also not in the audio bank (only ui_beatwade_rung_unlock.ogg is).
        public void ClinicLessonStart() => Play("ui_clinic_lesson_start", null, -5f, GMusic, 1f, 0f, 0f, 1f);
        public void ClinicPass() => Play("ui_clinic_pass", null, -5f, GMusic, 1f, 0f, 0f, 1f);
        public void ClinicFail() => Play("ui_clinic_fail", null, -5f, GMusic, 1f, 0f, 0f, 1f);
        public void ClinicTip() => Play("ui_clinic_tip_chime", null, -4f, GUI, 1f, 0.02f, 0f, 0.5f);
        public void BeatWadeRungUnlock() => Play("ui_beatwade_rung_unlock", null, -5f, GMusic, 1f, 0f, 0f, 1f);
        public void BeatWadeNearMiss() => Play("ui_beatwade_near_miss", null, -5f, GMusic, 1f, 0f, 0f, 1f);
        public void BeatWadeWin() => Play("ui_beatwade_win", null, -2f, GMusic, 1f, 0f, 0f, 2f);   // +3 dB vs the other stingers
        // ---------- pass 5: unlock / personal best (Music) ----------
        // PersonalBest is called from SkiViewGame.RegisterJumpPass (persistent per-lake jump PB). TODO caller: SkiUnlock - there is no ski unlock system yet.
        public void SkiUnlock() => Play("ui_ski_unlock", null, -5f, GMusic, 1f, 0f, 0f, 1f);
        public void PersonalBest() => Play("ui_personal_best", null, -5f, GMusic, 1f, 0f, 0f, 1f);

        // ---------- pass 6: shoreline one-shots (Ambient group, Master only, 3D, random variation, pitch +-5%) ----------
        /// <summary>kind = "reed" | "bank_lap" | "dock_creak". Reeds / bank laps are driven by ShoreEmitters (every 3-8 s, on the
        /// LakeDetail.lakeBounds edge nearest the listener). TODO caller: dock_creak needs per-lake dock positions (none in the scenes' data yet).</summary>
        public AudioSource Shoreline(string kind, Vector3 at)
        {
            string id = kind == "reed" ? "amb_reed_rustle_0" : kind == "bank_lap" ? "amb_bank_lap_0" : kind == "dock_creak" ? "amb_dock_creak_0" : null;
            if (id == null) return null;
            var s = Play(id + Random.Range(1, 4), at, kind == "dock_creak" ? -8f : -6f, GAmb, 1f, 0.05f);
            if (s != null) { s.minDistance = 4f; s.maxDistance = 80f; }
            return s;
        }

        LakeDetail shoreDetail; string shoreDetailLake; float reedAt = -1f, lapAt = -1f;
        /// <summary>Shoreline one-shot emitters: a point on the nearest lakeBounds edge (+-25 m along it), only when within 70 m of the listener.</summary>
        void ShoreEmitters()
        {
            if (game == null || !started || Camera.main == null) return;
            if (shoreDetailLake != game.lakeId) { shoreDetailLake = game.lakeId; shoreDetail = FindFirstObjectByType<LakeDetail>(); }
            if (shoreDetail == null) return;
            var b = shoreDetail.lakeBounds; if (b.y <= b.x || b.w <= b.z) return;
            if (reedAt < 0f) reedAt = Time.time + Random.Range(3f, 8f);
            if (lapAt < 0f) lapAt = Time.time + Random.Range(3f, 8f);
            bool reed = Time.time >= reedAt, lap = Time.time >= lapAt; if (!reed && !lap) return;
            if (reed) reedAt = Time.time + Random.Range(3f, 8f); if (lap) lapAt = Time.time + Random.Range(3f, 8f);
            var c = Camera.main.transform.position; float wy = game.SkierPos.y;
            float dx0 = Mathf.Abs(c.x - b.x), dx1 = Mathf.Abs(b.y - c.x), dz0 = Mathf.Abs(c.z - b.z), dz1 = Mathf.Abs(b.w - c.z);
            float m = Mathf.Min(Mathf.Min(dx0, dx1), Mathf.Min(dz0, dz1)); Vector3 p;
            if (m == dx0 || m == dx1) p = new Vector3(m == dx0 ? b.x : b.y, wy, Mathf.Clamp(c.z + Random.Range(-25f, 25f), b.z, b.w));
            else p = new Vector3(Mathf.Clamp(c.x + Random.Range(-25f, 25f), b.x, b.y), wy, m == dz0 ? b.z : b.w);
            if ((p - c).sqrMagnitude > 70f * 70f) return;
            if (reed) Shoreline("reed", p);
            if (lap) Shoreline("bank_lap", p);
        }

        // ---------- pass 7 / b10.11 Phase A: TRICK event set ----------
        // Callers wired from SkiViewGame.Trick.cs (Phase A). Flip/toe APIs remain unused until later.
        static string Pick(string stem, int n) => stem + "_0" + Random.Range(1, n + 1);
        public void TrickCarve() => Play(Pick("skier_trick_carve", 3), game != null ? game.SkierPos : (Vector3?)null, 3f, GSkier, 1f, 0.04f, 0f, 0.15f);
        /// <summary>Surface-turn slide loop, on while the ski is sliding (Src -2.5); call every frame or on change.</summary>
        public void TrickSlide(bool on) { if (trickSlide != null) trickSlide.volume = Mathf.MoveTowards(trickSlide.volume, on ? Db(-2.5f + GSkier) : 0f, Time.deltaTime / (on ? 0.08f : 0.15f)); }
        public void TrickSpin(int degrees) => Play(Pick(degrees >= 360 ? "skier_trick_spin_360" : "skier_trick_spin_180", 2), game != null ? game.SkierPos : (Vector3?)null, 0f, GSkier, 1f, 0.03f);
        public void TrickWakePop() => Play(Pick("skier_trick_wake_pop", 2), game != null ? game.SkierPos : (Vector3?)null, 0f, GSkier, 1f, 0.03f);
        public void TrickWakeLand() => Play(Pick("skier_trick_wake_land", 2), game != null ? game.SkierPos : (Vector3?)null, 0f, GSkier, 1f, 0.03f);
        public void TrickToeStrapOn() => Play("skier_trick_toestrap_on", game != null ? game.SkierPos : (Vector3?)null, 4f, GSkier, 1f, 0.03f, 0f, 0.5f);
        public void TrickToeHold() => Play(Pick("skier_trick_toehold_rope", 2), game != null ? game.SkierPos : (Vector3?)null, 4f, GSkier, 1f, 0.03f, 0f, 0.3f);
        public void TrickFlipWhoosh() => Play(Pick("skier_trick_flip_whoosh", 2), game != null ? game.SkierPos : (Vector3?)null, 0f, GSkier, 1f, 0.03f);
        public void TrickFlipLand() => Play("skier_trick_flip_land_splash", game != null ? game.SkierPos : (Vector3?)null, 0f, GSkier + 4f, 1f, 0.03f);
        public void TrickFall() { TrickSlide(false); Play(Pick("skier_trick_fall_splash", 2), game != null ? game.SkierPos : (Vector3?)null, 0f, GSkier, 1f, 0.03f, 0f, 1f); }
        /// <summary>A trick was credited: tally tick now, score chime ~80 ms later (big chime at 300+ points or any flip).</summary>
        public void TrickCredited(int points, bool flip)
        {
            Play("ui_trick_tally_tick", null, -2f, GUI, 1f, 0.02f);
            chimeId = points >= 300 || flip ? "ui_trick_score_chime_big" : "ui_trick_score_chime"; chimeAt = Time.unscaledTime + 0.08f;
        }
        /// <summary>Call every frame during a trick pass with the seconds left: beeps at 5, 4, 3, 2, 1 s and the final tone at 0.</summary>
        public void TrickTimer(float remaining)
        {
            int c = Mathf.CeilToInt(remaining);
            if (remaining > 5f) { beepPrev = 99; return; }
            if (c < beepPrev) { beepPrev = c; if (c >= 1) Play("ui_trick_timer_beep", null, 0f, GUI); else Play("ui_trick_timer_final", null, 0f, GUI, 1f, 0f, 0f, 1f); }
        }
        // ---------- pass 10: tournament jump-event ambience (no speech) ----------
        bool tourneyBed;
        /// <summary>Swap the lake bed for amb_bed_tournament_jump_loop (Ambient group, Master only, Src -6 via the amb_bed_ rule) while on;
        /// off restores the lake's own SkiViewGame.ambientClip. Called from SkiViewGame.SetJumpGear (jump gear on / off).</summary>
        public void TournamentJumpAmbience(bool on)
        {
            if (on == tourneyBed || game == null) return; tourneyBed = on;
            var e = on ? bank.Get("amb_bed_tournament_jump_loop") : null;
            StartAmbient(on && e != null ? e.clip : game.ambientClip);
        }
        /// <summary>Distant shoreline spectators react to a big jump (2D, Ambient group, Src -6, random of 2).
        /// Called from the SkiViewGame.Jump scored landing at 40 m+ (placeholder threshold; Karthik to tune).</summary>
        public void SpectatorReaction() => Play(Random.value < 0.5f ? "amb_crowd_react_big_01" : "amb_crowd_react_big_02", null, -6f, GAmb, 1f, 0.03f, 0f, 4f);
        public void TrickRunResult() { TrickSlide(false); Play("ui_trick_result_sting", null, 1f, GMusic, 1f, 0f, 0f, 3f); }
    }
}

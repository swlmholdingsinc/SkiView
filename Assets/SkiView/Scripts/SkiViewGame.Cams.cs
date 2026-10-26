using UnityEngine;

namespace SkiView
{
    /// <summary>
    /// Pass 17: player cameras, cycled with the CAM button or C and remembered between sessions (PlayerPrefs "skiview.cam"):
    /// high chase (behind and above, the default), close chase (shoulder height, ~5 m back), first person (from the skier's eyes:
    /// handle and hands, rope and boat ahead, ski tip at the bottom edge; lean roll damped and limited so it doesn't make
    /// anyone sick), and the TV boat camera. Switching blends over 0.7 s; the jump shore camera still takes over unless first
    /// person is on. No camera goes below 0.3 m over the water or inside the skier.
    /// </summary>
    public partial class SkiViewGame
    {
        const string CAM_PREF = "skiview.cam", CAM_DEFAULT_PREF = "skiview.camdefault", CAM_MOTION_PREF = "skiview.cammotion";
        /// <summary>Pass 18: Settings - camera motion Reduced (no first-person roll, slower follow, no boat-cam buzz, longer blends).</summary>
        public static bool ReducedMotion;
        /// <summary>Pass 18: Settings - camera at session start (-1 = the last one used).</summary>
        public static int DefaultCamera = -1;
        public static void LoadCameraSettings() { DefaultCamera = PlayerPrefs.GetInt(CAM_DEFAULT_PREF, (int)CamMode.SkierChase); ReducedMotion = PlayerPrefs.GetInt(CAM_MOTION_PREF, 0) == 1; }   // kit §8: default High chase
        public static void SaveCameraSettings(int defaultCam, bool reduced)
        {
            DefaultCamera = defaultCam; ReducedMotion = reduced;
            PlayerPrefs.SetInt(CAM_DEFAULT_PREF, defaultCam); PlayerPrefs.SetInt(CAM_MOTION_PREF, reduced ? 1 : 0); PlayerPrefs.Save();
        }
        [Header("First-person rig (pass 18)")] public Transform fpRig, fpHandle, fpApex;
        [Header("First-person arms (pass 19)")] public Transform fpForearmL, fpForearmR, fpUpperL, fpUpperR, fpGloveL, fpGloveR;
        public float fpForearmLen = 0.265f, fpUpperLen = 0.30f, fpShoulderW = 0.19f, fpShoulderDrop = 0.25f, fpShoulderFwd = 0.05f;
        public Vector3 fpWristLocal = new Vector3(0f, -0.012f, -0.088f);
        float fpLeanX, fpLeanXVel, fpEdgePitch, fpEdgePitchVel, fpYawFix, fpYawFixVel;
        public Vector3 fpHandleLocal = new Vector3(0f, -0.30f, 0.56f);
        ParticleSystemRenderer plowR; float plowMax = 0.5f;
        float fpPitchCur = 24f, fpPitchVel, fpLookDown, fpLookDownVel; Vector3 skiTipLocal; bool skiTipSet;
        /// <summary>Front tip of the slalom ski in the ski's local frame (from its mesh bounds, cached).</summary>
        Vector3 SkiTipLocal()
        {
            if (skiTipSet || ski == null) return skiTipLocal;
            skiTipSet = true; float best = float.MinValue;
            foreach (var mf in ski.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.gameObject.layer != 0) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var l = ski.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (l.z > best) { best = l.z; skiTipLocal = new Vector3(0f, l.y, l.z); }
                }
            }
            return skiTipLocal;
        }
        Vector3 ropePyl, ropeApex; float fpTilt, fpTiltVel, fpReach, fpReachVel; bool fpShown;
        static readonly CamMode[] CamCycle = { CamMode.SkierChase, CamMode.CloseChase, CamMode.FirstPerson, CamMode.Broadcast };
        CamMode camModeLast = (CamMode)(-1); bool jcLast; public bool JumpCamOn => jcLast;   // pass 19: audio hook (boat_passby_fast)
        float camBlend = 1f; Vector3 camFromPos; Quaternion camFromRot; float camFromFov = 52f, camFromNear = 0.2f;
        Vector3 fpDir, fpDirVel, ccFwd, ccFwdVel, ccVel; float fpRoll, fpRollVel, fpEyeY, fpEyeYVel;
        Transform headBone; bool headLooked;
        [Header("Cameras (pass 17)")] public float camBlendTime = 0.7f, fpRollScale = 0.3f, fpRollMax = 9f, fpRollSmooth = 0.5f, fpFov = 80f, fpPitch = 24f, fpPitchMax = 40f, fpSkiYawShare = 0.35f, fpEyeBack = 0.14f, fpLookDownDeg = 26f,
                                       ccBack = 5.0f, ccHeight = 1.55f, ccFov = 62f;

        public string CamLabel => camMode == CamMode.Broadcast ? "CAM: BOAT" : camMode == CamMode.FirstPerson ? "CAM: 1ST" : camMode == CamMode.CloseChase ? "CAM: CLOSE" : "CAM: HIGH";

        void LoadCameraPref()
        {
            if (!Application.isPlaying) return;   // editor captures keep the scene's camera
            LoadCameraSettings();
            int c = DefaultCamera >= 0 ? DefaultCamera : PlayerPrefs.GetInt(CAM_PREF, (int)CamMode.SkierChase);
            camMode = System.Enum.IsDefined(typeof(CamMode), c) ? (CamMode)c : CamMode.SkierChase;
            camModeLast = camMode;   // no blend on load
        }

        public void CycleCamera()
        {
            int i = System.Array.IndexOf(CamCycle, camMode);
            SetCamera(CamCycle[(i + 1) % CamCycle.Length]);
            SkiAudio.Instance?.UiClick();
            var ui = SkiView.UI.SkiViewUI.Instance;
            if (ui != null) ui.ShowCamToast(camMode); else { banner = CamLabel.Replace("CAM: ", "CAMERA: "); bannerT = 1.2f; }
        }

        public void SetCamera(CamMode m)
        {
            camMode = m;
            // pass 18 (kit §8): the CAM button changes the view for the current run only; runs start in Settings > Default camera
        }

        void BeginCamBlend()
        {
            if (cam == null || !Application.isPlaying) { camBlend = 1f; return; }
            camFromPos = cam.transform.position; camFromRot = cam.transform.rotation; camFromFov = cam.fieldOfView; camFromNear = cam.nearClipPlane;
            camBlend = 0f;
        }

        void ApplyCamBlend(bool snap)
        {
            if (snap || !Application.isPlaying) { camBlend = 1f; return; }
            if (camBlend >= 1f) return;
            camBlend = Mathf.Min(1f, camBlend + Time.deltaTime / Mathf.Max(0.05f, camBlendTime * (ReducedMotion ? 1.5f : 1f)));
            float t = camBlend * camBlend * (3f - 2f * camBlend);
            Vector3 p = Vector3.Lerp(camFromPos, cam.transform.position, t);
            p.y = Mathf.Max(p.y, WaterY(p) + 0.3f);
            cam.transform.SetPositionAndRotation(p, Quaternion.Slerp(camFromRot, cam.transform.rotation, t));
            cam.fieldOfView = Mathf.Lerp(camFromFov, cam.fieldOfView, t);
            cam.nearClipPlane = Mathf.Min(camFromNear, cam.nearClipPlane);   // never clip the near subject mid-blend
        }

        static float WaterY(Vector3 p) => 0f;

        Transform HeadBone()
        {
            if (!headLooked || headBone == null)
            {
                headLooked = true;
                var anim = rig != null ? rig.GetComponentInChildren<Animator>() : null;
                headBone = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            }
            return headBone;
        }

        /// <summary>First person: from just in front of the skier's eyes, looking up the rope at the handle, hands and boat with the
        /// ski tip at the bottom of the frame. Yaw follows the rope with a short lag; roll follows the lean at 30%, max 9 deg, slowly.</summary>
        void UpdateFirstPersonCamera(bool snap)
        {
            var hb = HeadBone();
            Vector3 tow = boatTow != null ? boatTow.position : boatPos + Vector3.up * SkierSim.PYLON_Y;
            Vector3 eye = hb != null ? hb.position + Vector3.up * 0.08f : skierPos + Vector3.up * 1.6f;
            bool fallen = mode == Mode.Fallen;
            Vector3 toBoat = tow - eye; toBoat.y = 0f;
            Vector3 dir = toBoat.sqrMagnitude > 1f ? toBoat.normalized : Flat(boatFwd);
            // pass 18: look a quarter of the way toward where the ski points (the tip comes into view in the cuts; boat stays in frame)
            if (ski != null && !fallen) { Vector3 sf = ski.forward; sf.y = 0f; if (sf.sqrMagnitude > 1e-3f) { sf.Normalize(); if (Vector3.Dot(sf, dir) > -0.2f) dir = Vector3.Slerp(dir, sf, fpSkiYawShare).normalized; } }
            bool hard = snap || !Application.isPlaying || fpDir == Vector3.zero;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            // pass 19: hard cuts - if the ski tip would sit within 9 deg of the frame's side edge, turn toward it by the excess
            if (ski != null && !fallen)
            {
                Vector3 tipW = ski.TransformPoint(SkiTipLocal()) - eye; tipW.y = 0f;
                float hHalf = Mathf.Atan(Mathf.Tan(fpFov * 0.5f * Mathf.Deg2Rad) * Mathf.Max(1f, cam.aspect)) * Mathf.Rad2Deg;
                float ang = tipW.sqrMagnitude > 0.01f ? Vector3.SignedAngle(dir, tipW, Vector3.up) : 0f;
                float lim = hHalf - 9f, want = Mathf.Abs(ang) > lim ? (Mathf.Abs(ang) - lim) * Mathf.Sign(ang) : 0f;
                want = Mathf.Clamp(want, ReducedMotion ? -10f : -18f, ReducedMotion ? 10f : 18f);
                fpYawFix = hard ? want : Mathf.SmoothDamp(fpYawFix, want, ref fpYawFixVel, ReducedMotion ? 0.35f : 0.15f, Mathf.Infinity, dt);
                dir = Quaternion.AngleAxis(fpYawFix, Vector3.up) * dir;
            }
            if (hard) { fpDir = dir; fpDirVel = Vector3.zero; fpRoll = 0f; fpRollVel = 0f; fpEyeY = eye.y; fpEyeYVel = 0f; }
            else
            {
                fpDir = Vector3.SmoothDamp(fpDir, dir, ref fpDirVel, ReducedMotion ? 0.3f : 0.12f, Mathf.Infinity, dt);
                fpEyeY = Mathf.SmoothDamp(fpEyeY, eye.y, ref fpEyeYVel, ReducedMotion ? 0.2f : 0.08f, Mathf.Infinity, dt);   // damp head bob
            }
            Vector3 d = fpDir; d.y = 0f; d = d.sqrMagnitude > 1e-4f ? d.normalized : dir;
            eye.y = fpEyeY;
            eye -= d * fpEyeBack;                               // helmet-cam: a little behind the eyes (the body layer is hidden from this camera)
            eye.y = Mathf.Max(eye.y, WaterY(eye) + (fallen ? 0.35f : 0.6f));
            float rollWant = fallen || ReducedMotion ? 0f : Mathf.Clamp(-LeanDeg * fpRollScale, -fpRollMax, fpRollMax);
            fpRoll = hard ? rollWant : Mathf.SmoothDamp(fpRoll, rollWant, ref fpRollVel, fpRollSmooth, 30f, dt);
            // pitch: at least fpPitch; tilt further (up to fpPitchMax) to bring the ski tip into the bottom of the frame, never
            // so far that the boat leaves the top. Smoothed (slower with Reduced motion).
            float pitchWant = fpPitch;
            if (!fallen && ski != null)
            {
                Vector3 tip = ski.TransformPoint(SkiTipLocal()); Vector3 v = tip - eye; float hz = new Vector2(v.x, v.z).magnitude;
                float tipDown = Mathf.Atan2(-v.y, Mathf.Max(0.05f, hz)) * Mathf.Rad2Deg;
                Vector3 vb = tow - eye; float boatDown = Mathf.Atan2(-vb.y, Mathf.Max(1f, new Vector2(vb.x, vb.z).magnitude)) * Mathf.Rad2Deg;
                float half = fpFov * 0.5f;
                // pass 19: the tip wins over the boat (the boat may ride up to the top edge), plus a small edge-driven pitch toward the
                // ski in hard cuts (Reduced motion: half of it, slower)
                float edge = Mathf.Clamp01((Mathf.Abs(LeanDeg) - 15f) / 30f) * (ReducedMotion ? 3f : 6f);
                fpEdgePitch = hard ? edge : Mathf.SmoothDamp(fpEdgePitch, edge, ref fpEdgePitchVel, ReducedMotion ? 0.6f : 0.25f, Mathf.Infinity, dt);
                float maxP = ReducedMotion ? fpPitchMax - 4f : fpPitchMax + 4f;
                pitchWant = Mathf.Clamp(Mathf.Max(tipDown - half + 8f, fpPitch + fpEdgePitch), fpPitch, Mathf.Min(maxP, Mathf.Max(fpPitch + fpEdgePitch, boatDown + half - 1f)));
            }
            if (hard || fallen) { fpPitchCur = fallen ? 4f : pitchWant; fpPitchVel = 0f; }
            else fpPitchCur = Mathf.SmoothDamp(fpPitchCur, pitchWant, ref fpPitchVel, ReducedMotion ? 0.8f : 0.4f, Mathf.Infinity, dt);
            // look down (S / down arrow held): eases the view down to the ski, and back up on release
            float ldWant = Application.isPlaying && (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? fpLookDownDeg : 0f;
            fpLookDown = hard ? ldWant : Mathf.SmoothDamp(fpLookDown, ldWant, ref fpLookDownVel, ReducedMotion ? 0.35f : 0.18f, Mathf.Infinity, dt);
            float pitch = fpPitchCur + fpLookDown;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(pitch, 0f, fpRoll);
            cam.fieldOfView = fpFov; cam.nearClipPlane = 0.06f;
        }

        /// <summary>Close chase: right behind the skier on the rope line at shoulder height, ~5 m back, looking past the skier at the boat.</summary>
        void UpdateCloseChaseCamera(bool snap)
        {
            Vector3 tow = boatTow != null ? boatTow.position : boatPos;
            Vector3 f = tow - skierPos; f.y = 0f; f = f.sqrMagnitude > 1f ? f.normalized : Flat(boatFwd);
            bool hard = snap || !Application.isPlaying || ccFwd == Vector3.zero;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            if (hard) { ccFwd = f; ccFwdVel = Vector3.zero; } else ccFwd = Vector3.SmoothDamp(ccFwd, f, ref ccFwdVel, (mode == Mode.Pass ? 0.25f : 0.45f) * (ReducedMotion ? 1.6f : 1f), Mathf.Infinity, dt);
            Vector3 cf = ccFwd; cf.y = 0f; cf = cf.sqrMagnitude > 1e-4f ? cf.normalized : f;
            bool fallen = mode == Mode.Fallen;
            float back = fallen ? ccBack + 2f : ccBack, hgt = fallen ? 2.2f : ccHeight;
            Vector3 want = skierPos - cf * back + Vector3.up * hgt;
            want.y = Mathf.Max(want.y, WaterY(want) + 0.6f);
            Vector3 cur = (hard) ? want : Vector3.SmoothDamp(cam.transform.position, want, ref ccVel, 0.14f, Mathf.Infinity, dt);
            // keep out of the skier: at least 3.5 m from the body centre
            Vector3 body = skierPos + Vector3.up * 1.0f, off = cur - body;
            if (off.magnitude < 3.5f) cur = body + (off.sqrMagnitude > 1e-4f ? off.normalized : -cf) * 3.5f;
            cur.y = Mathf.Max(cur.y, WaterY(cur) + 0.6f);
            Vector3 look = skierPos + Vector3.up * (fallen ? 0.4f : 1.15f) + cf * (fallen ? 1f : 7f);
            cam.transform.position = cur;
            cam.transform.rotation = Quaternion.LookRotation(look - cur, Vector3.up);
            cam.fieldOfView = ccFov; cam.nearClipPlane = 0.15f;
        }
        /// <summary>Pass 18: first-person forearms, gloves and handle (camera-space rig, never reflected). The handle sits low in the
        /// frame, the bridle points up the real rope, the bar tilts with the real handle, and it reaches forward as the line slackens
        /// at the ball. The rope's near end is moved onto the rig's bridle apex, and the world handle is hidden while it shows.</summary>
        void FirstPersonRig()
        {
            bool on = fpRig != null && camMode == CamMode.FirstPerson && !jcLast && mode != Mode.Fallen && camBlend >= 0.6f;
            if (fpRig != null && fpRig.gameObject.activeSelf != on) fpRig.gameObject.SetActive(on);
            if (handle != null) { var hr = handle.GetComponent<Renderer>(); if (hr != null) hr.enabled = !on; }
            fpShown = on;
            // DWS plow spray starts at the chest, right under a first-person eye: cap its on-screen size so drops near the lens
            // read as droplets, not blobs filling the frame
            if (plowSpray != null) { if (plowR == null) { plowR = plowSpray.GetComponent<ParticleSystemRenderer>(); plowMax = plowR.maxParticleSize; } plowR.maxParticleSize = camMode == CamMode.FirstPerson ? 0.018f : plowMax; }
            // the skier's body, boots, helmet and the world handle are on the body layer: hidden from the first-person eye (the
            // forearm rig stands in for the arms); the ski stays visible. The reflection camera has its own mask, so the
            // reflection still shows the skier.
            if (cam != null) cam.cullingMask = on ? ~(1 << PlanarReflection.SKIER_BODY_LAYER) : ~0;
            if (!on || fpHandle == null) return;
            float dt = Application.isPlaying ? Mathf.Max(1e-4f, Time.deltaTime) : 0f;
            Transform ct = cam.transform;
            // reach: the handle comes forward / down a little when the line goes slack at the ball, back in under load
            float reachWant = mode == Mode.Pass ? Mathf.InverseLerp(0.72f, 0.97f, Mathf.Abs(sim.Lateral) / SkierSim.BUOY_OFFSET) * Mathf.InverseLerp(1.2f, 0.3f, Mathf.Abs(sim.omega)) : 0f;
            fpReach = dt > 0f ? Mathf.SmoothDamp(fpReach, reachWant, ref fpReachVel, 0.18f, Mathf.Infinity, dt) : reachWant;
            // pass 19: lean-driven motion - the handle stays at the inside hip as the body tips (shifts across the frame toward the
            // rope side), and comes in a little under load; Reduced motion halves it
            float leanN = Mathf.Clamp(-LeanDeg / 45f, -1f, 1f) * (ReducedMotion ? 0.5f : 1f);
            fpLeanX = dt > 0f ? Mathf.SmoothDamp(fpLeanX, leanN, ref fpLeanXVel, 0.2f, Mathf.Infinity, dt) : leanN;
            float load = mode == Mode.Pass || mode == Mode.Turn ? Mathf.Clamp01(Mathf.Abs(LeanDeg) / 50f) : 0f;
            Vector3 lp = fpHandleLocal + new Vector3(0.055f * fpLeanX, -0.03f * fpReach - 0.02f * load, 0.07f * fpReach - 0.03f * load);
            fpHandle.localPosition = lp;
            // bridle up the rope (camera space), bar tilt from the real handle, limited
            Vector3 toP = ct.InverseTransformDirection((ropePyl - fpHandle.position).normalized);
            Vector3 bar = ct.InverseTransformDirection(handleBar.sqrMagnitude > 1e-4f ? handleBar : ct.right);
            if (bar.x < 0f) bar = -bar;
            float tiltWant = Mathf.Clamp(Mathf.Atan2(bar.y, Mathf.Max(1e-3f, bar.x)) * Mathf.Rad2Deg, -18f, 18f);
            fpTilt = dt > 0f ? Mathf.SmoothDamp(fpTilt, tiltWant, ref fpTiltVel, 0.15f, Mathf.Infinity, dt) : tiltWant;
            Vector3 fw = Vector3.Slerp(Vector3.forward, toP, 0.75f).normalized;
            fpHandle.localRotation = Quaternion.LookRotation(fw, Quaternion.AngleAxis(fpTilt, fw) * Vector3.up);
            PoseFpArms(ct);
            // rope: bend the near end onto the rig's bridle apex
            if (rope != null && fpApex != null && rope.positionCount >= 20)
            {
                Vector3 a = fpApex.position, dA = a - ropeApex; const int N = 16;
                for (int i = 0; i <= N; i++) { float t = i / (float)N; rope.SetPosition(i, rope.GetPosition(i) + dA * (t * t * t)); }
                rope.SetPosition(N + 1, a); rope.SetPosition(N + 2, a); rope.SetPosition(N + 3, a);
            }
        }
    
        /// <summary>Pass 19: two-bone IK for each arm from a fixed shoulder (camera position, yaw and roll; not the look pitch) to the
        /// glove's wrist. The elbow bends out and down; the forearm aims wrist -> elbow, the upper arm elbow -> shoulder.</summary>
        void PoseFpArms(Transform ct)
        {
            if (fpForearmL == null || fpGloveL == null) return;
            Vector3 fw = ct.forward; fw.y = 0f; fw = fw.sqrMagnitude > 1e-4f ? fw.normalized : Vector3.forward;
            Vector3 rt = Vector3.Cross(Vector3.up, fw), upv = Vector3.up;
            float roll = ct.eulerAngles.z; rt = Quaternion.AngleAxis(roll, fw) * rt; upv = Quaternion.AngleAxis(roll, fw) * upv;
            for (int k = -1; k <= 1; k += 2)
            {
                var g = k < 0 ? fpGloveL : fpGloveR; var fa = k < 0 ? fpForearmL : fpForearmR; var ua = k < 0 ? fpUpperL : fpUpperR;
                if (g == null || fa == null || ua == null) continue;
                Vector3 W = g.TransformPoint(fpWristLocal);
                Vector3 S = ct.position + fw * fpShoulderFwd - upv * fpShoulderDrop + rt * (k * fpShoulderW);
                float a = fpUpperLen, b = fpForearmLen; Vector3 sw = W - S; float d = Mathf.Clamp(sw.magnitude, 0.05f, (a + b) * 0.999f); Vector3 dn = sw.normalized;
                float x = (a * a - b * b + d * d) / (2f * d), h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
                Vector3 pole = rt * k * 1.0f - upv * 0.7f - fw * 0.25f; Vector3 pp = Vector3.ProjectOnPlane(pole, dn).normalized;
                Vector3 E = S + dn * x + pp * h;
                fa.position = W; fa.rotation = Quaternion.LookRotation(E - W, upv + fw * 0.3f);
                ua.position = E; ua.rotation = Quaternion.LookRotation(S - E, fw - upv * 0.2f);
            }
        }
}
}

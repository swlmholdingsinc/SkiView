using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SkiView
{
    /// <summary>Rigged slalom skier (Mixamo Remy, Humanoid). Layers, in order:
    /// 1. A manual PlayableGraph of upper-body clips (idle breathing, a bit of "Pulling A Rope"), or "Treading Water" after a fall.
    /// 2. Stance from SkierPoses (footage targets): a straight sideways board on the pull, knees and handle per phase.
    ///    Feet IK to the FrontBoot / RearBoot attaches, so the rear boot's 12° yaw is the rear foot's aim.
    /// 3. Arms to a handle at the hip. On the reach, every buoy reaches toward the boat and down the course; odd/even buoys
    ///    alternate only the course side and which hand holds the handle (the outside hand releases).
    /// Manual time, so it also evaluates in the headless editor capture.</summary>
    [ExecuteAlways]
    public class SkierRig : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip idle, pull, edge, fallen;
        public AnimationClip leanL, leanR;   // Christopher Oct 23: SlalomStance_LeanLeft/_LeanRight, blended on the turn side
        public AnimationClip start, fall, standUp;     // pass 11 (Mixamo batch 3): deep-water start load-up, fall, get-up (one-shots)
        public enum RiderAction { None, DeepStart, Fall }
        const int NCLIP = 9;                           // 0 idle, 1 pull, 2 edge, 3 treading water, 4 start, 5 fall, 6 stand up, 7/8 slalom lean left/right
        Vector3 baseLocal; bool baseSet;
        public Transform frontBoot, rearBoot;          // boot roots on the ski (heel at origin, +Z toward the toes)
        public float ankleAboveSole = 0.075f;          // measured from the bind pose by the builder
        [Header("Stance")]
        public float pelvisFwd = 0.0f;                // pelvis ahead of the ankle midpoint (over the front foot)
        public float crouchPull = 0.72f, crouchGlide = 0.76f;   // Scott Oct 23: low athletic stance, knees bent (was 0.76 / 0.80, read as standing)   // pelvis-to-ankle height as a fraction of leg length
        public float leanTotalPullDeg = 45f, leanTotalApexDeg = 58f, leanBodyMaxDeg = 35f;   // b10.8 Scott: 58 deg sideways apex
        public float leanTotalApexShortDeg = 36f;   // b9.1: Holly's apex lean by rope, ~36 deg at 15 off up to ~60 deg at 41 off
        public float rope01 = 0f;                    // 0 = 15 off .. 1 = 41 off, set by the game every frame
        public float reachArmM = 0.10f;              // b9.1: inside arm extends along the rope at the apex (more at long line, lean does it at short line)
        // reach v2 (Scott Oct 23): a REAL stretch at short line. At 38-41 off, at the ball: free hand off the handle, inside arm
        // straight along the rope from the shoulder, shoulders open to the boat, hips low and back, body long (ref 39/41 off footage:
        // ankle-hip-shoulder near one straight line, ~60-65 deg off vertical, inside arm in line with the rope, chest square to the boat)
        public float stretchFrom01 = 0.85f, stretchFull01 = 0.95f;   // rope01: 0.875 = 38 off, 0.94 = 39.5 off, 1 = 41 off
        public float stretchLeanDeg = 58f, stretchLeanExtraDeg = 0f, stretchBodyMaxDeg = 75f, stretchHipDrop = 0.05f, stretchHipBack = 0.04f, stretchCrouchUp = 0.14f, stretchBodyLongW = 0.9f;   // b10.8: 58 deg stretch lean; body-long weight: higher pulled the skinned hair off the scalp
        public float Stretch01 { get; private set; }
        float reachS;
        public float LeanTotalDeg { get; private set; }
        public float leanFwdShare = 0.08f, leanSideRate = 3.5f; float leanSide, leanSideT, edgeSigned;   // b10.8: lean sideways not forward; b10.3: max forward component of the lean direction
        public float leanGlideDeg = 0f, leanPullDeg = 0f, chestExtraLeanDeg = 0f;   // stacked lean away from the pull (refs: 40-50 deg total with the ski edge)
        public float shoulderBackDeg = 8f, upperYawShare = 0.55f, hipYawShare = 0.25f;
        public float handleFwd = 0.36f, handleDrop = 0.12f, handHalf = 0.075f;
        public float stanceDebug = -1f;
        public int forceClip = -1;
        PlayableGraph graph; AnimationMixerPlayable mix;
        readonly float[] w = new float[NCLIP];
        bool built; float releaseW, releaseSide = 1f, stanceW = 1f;
        public float skiTipUpDeg = 9f, skiSinkAtSpeed = -0.03f,   // Karthik note Oct 23: ski V2 root (deck top) 0.03 m above the water, tail buried by the tip-up pitch (was 5 deg / 3.5 cm)
                skiCarveRollDeg = 5f;
        public float hipDropAtEdge = 0.05f, headTiltWithBody = 0.25f;
        public float dwsCrouch = 0.34f; float dwsDepth;
        // pass 13: jump stance on two skis. The game sets these each frame: jumpCrouch >= 0 overrides the crouch, jumpPitchDeg tips the
        // ankle-hip-shoulder line forward over the skis (the "bird" in the air), jumpHandleIn pulls the handle in to the hips
        public bool jumpOn; public Transform jumpBootL, jumpBootR;
        public float jumpCrouch = -1f, jumpPitchDeg = 0f, jumpHandleIn = 0f;
        /// <summary>0 water or approach, 1 ramp, 2 flight, 3 landing. Flight and landing stay on a neutral clip so a slalom lean cannot fold the torso.</summary>
        public int jumpPose;
        public SkierPoses poses = new SkierPoses();
        float shoulderYaw, kneeS = 148f, armS = 0.9f;
        int reachTowardBoat; float latS, inbS;
        public float WaterClearance { get; private set; } = 1f;
        public string LowestBone { get; private set; } = ""; public string Debug_ { get; private set; } = ""; public float RootY { get; private set; }   // lowest body point above the water (m), read by SkiViewGame   // pass 3: ski buried tail-down, extra edge in carves
        public float headPitchDownDeg = 6f, gazeToBoat = 0.2f;
        Transform ski; Vector3 skiBasePos; bool skiBaseSet; Transform neck, head;
        Transform hips, spine, chest, lUL, lLL, lF, lT, rUL, rLL, rF, rT, lUA, lLA, lH, rUA, rLA, rH;
        public Vector3 HandL { get; private set; }
        public Vector3 HandR { get; private set; }
        public float ReleaseL { get; private set; }
        public float ReleaseR { get; private set; }

        Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
        void Build()
        {
            if (built && graph.IsValid()) return;
            if (animator == null) return;
            graph = PlayableGraph.Create("SkierRig");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "skier", animator);
            mix = AnimationMixerPlayable.Create(graph, NCLIP);
            var clips = new[] { idle, pull, edge, fallen, start, fall, standUp, leanL, leanR };
            for (int i = 0; i < NCLIP; i++)
            {
                if (clips[i] == null) continue;
                var cp = AnimationClipPlayable.Create(graph, clips[i]);
                cp.SetApplyFootIK(false); cp.SetTime(i < 4 ? i * 0.37 : 0); if (i >= 7) cp.Pause();
                if (i >= 4 && i < 7) cp.Pause();   // one-shots: time is set explicitly from the action clock
                graph.Connect(cp, 0, mix, i);
            }
            if (!baseSet) { baseLocal = transform.localPosition; baseSet = true; }
            output.SetSourcePlayable(mix);
            hips = B(HumanBodyBones.Hips); spine = B(HumanBodyBones.Spine); chest = B(HumanBodyBones.Chest) ?? spine;
            lUL = B(HumanBodyBones.LeftUpperLeg); lLL = B(HumanBodyBones.LeftLowerLeg); lF = B(HumanBodyBones.LeftFoot); lT = B(HumanBodyBones.LeftToes);
            rUL = B(HumanBodyBones.RightUpperLeg); rLL = B(HumanBodyBones.RightLowerLeg); rF = B(HumanBodyBones.RightFoot); rT = B(HumanBodyBones.RightToes);
            lUA = B(HumanBodyBones.LeftUpperArm); lLA = B(HumanBodyBones.LeftLowerArm); lH = B(HumanBodyBones.LeftHand);
            rUA = B(HumanBodyBones.RightUpperArm); rLA = B(HumanBodyBones.RightLowerArm); rH = B(HumanBodyBones.RightHand);
            neck = B(HumanBodyBones.Neck); head = B(HumanBodyBones.Head);
            built = true;
        }

        /// <param name="load">0 = slack/slow boat, 1 = full pull</param>
        /// <param name="carve">0..1 carve rate</param>
        /// <param name="pylon">tow point (world)</param>
        /// <param name="turnReach">0..1: how far into the buoy turn (outside hand releases)</param>
        /// <param name="action">pass 11: DeepStart (stance IK on, deep crouch, "Pull Heavy Object Start" as the upper-body base) or
        /// Fall (fall clip, then treading water; no stance IK)</param>
        /// <param name="actionT">seconds since the action began</param>
        /// <param name="depth">DeepStart only: 1 = sitting back in the water, 0 = up on the ski</param>
        public void Drive(float load, float carve, bool isFallen, float dt, Vector3 pylon, float turnReach,
            RiderAction action = RiderAction.None, float actionT = 0f, float depth = 0f,
            int buoy = 0, int reachToward = 0, float lateral01 = 0f, float inbound = 0f)
        {
            Build();
            if (!built) return;
            if (poses == null) poses = new SkierPoses();
            reachTowardBoat = reachToward;
            latS = lateral01;
            inbS = dt <= 0f ? inbound : Mathf.Lerp(inbS, inbound, 1f - Mathf.Exp(-6f * dt));
            reachS = dt <= 0f ? turnReach : Mathf.Lerp(reachS, turnReach, 1f - Mathf.Exp(-8f * dt)); turnReach = reachS;   // reach v2: at 41 off the reach cue can step 0 -> 1 when the swing pins at full width
            var root0 = transform.parent != null ? transform.parent : transform;
            if (baseSet) transform.localPosition = baseLocal;   // PlantFeet used to leave its offset behind after a fall
            if (isFallen && action == RiderAction.None) action = RiderAction.Fall;
            var target = new float[NCLIP];
            if (action == RiderAction.Fall)
            {
                bool inClip = fall != null && actionT < fall.length - 0.05f;
                if (inClip) target[5] = 1f; else target[3] = 1f;
                if (inClip) dt = Mathf.Min(dt, 0.05f);   // quick blend into the fall
            }
            TrackEdge(root0, dt);
            // Ramp / flight / landing are posed by IK. A slalom lean clip under that IK folds the torso: the clip was
            // chosen from which side of the boat he is still on, and a cut flips the edge before the centreline.
            bool neutralPose = action != RiderAction.Fall && jumpOn;
            bool dropLeans = neutralPose;
            if (action != RiderAction.Fall && jumpPose == 2 && jumpOn)
                target[0] = 1f;
            else if (action != RiderAction.Fall)
            {
                target[0] = 1f - 0.3f * load; target[1] = 0.3f * load;
                if (pull != null && pull.name.Contains("SlalomStance")) { target[0] = 0.15f * (1f - load); target[1] = 1f - target[0]; }
                if (!dropLeans && leanL != null && leanR != null && root0 != null)
                {
                    // Lean clip follows the ski edge, and only once the eased lean has caught that edge.
                    // The previous side's full-body pose under the new edge is what folded the cut.
                    float edgeMag = Mathf.Clamp01(Mathf.Abs(edgeSigned) / 0.28f);
                    float clipSide = edgeSigned >= 0f ? 1f : -1f;
                    bool agree = leanSide * clipSide > 0.35f && edgeMag > 0.2f;
                    float lw = agree ? Mathf.Clamp01(load) * edgeMag * 0.35f : 0f;
                    target[clipSide > 0f ? 8 : 7] = lw; target[1] *= 1f - lw; target[0] *= 1f - lw;
                    dropLeans = !agree && Mathf.Abs(edgeSigned) > 0.09f;
                }
                if (action == RiderAction.DeepStart && start != null)
                {
                    float dd = Mathf.Clamp01(depth);
                    for (int i = 0; i < 4; i++) target[i] *= 1f - dd;
                    target[4] = dd;
                }
            }
            if (forceClip >= 0) for (int i = 0; i < NCLIP; i++) target[i] = i == forceClip ? 1f : 0f;
            float k = dt <= 0f ? 1f : 1f - Mathf.Exp(-8f * dt);
            float kLean = dropLeans && dt > 0f ? 1f - Mathf.Exp(-18f * dt) : k;
            if (start != null && mix.GetInput(4).IsValid()) mix.GetInput(4).SetTime(Mathf.Min(actionT * 0.8f, start.length - 0.02f));
            if (fall != null && mix.GetInput(5).IsValid()) mix.GetInput(5).SetTime(Mathf.Min(actionT, fall.length - 0.02f));
            for (int i = 0; i < NCLIP; i++)
            {
                float kk = (i == 7 || i == 8) ? kLean : k;
                w[i] = Mathf.Lerp(w[i], target[i], kk);
                if (mix.GetInput(i).IsValid()) mix.SetInputWeight(i, w[i]);
            }
            graph.Evaluate(Mathf.Max(0f, dt));
            var root = transform.parent != null ? transform.parent : transform;   // skier root (on the water, +Z = ski direction)
            if (action == RiderAction.Fall || frontBoot == null || rearBoot == null) { HandsFromBones(); MeasureClearance(root); return; }
            dwsDepth = action == RiderAction.DeepStart ? Mathf.Clamp01(depth) : 0f;

            stanceW = Mathf.Lerp(stanceW, Mathf.Clamp01(load), k);
            float s = stanceDebug >= 0f ? stanceDebug : stanceW;
            Vector3 up = root.up, fwd = root.forward, right = root.right;
            Vector3 toBoat = pylon - root.position; toBoat -= Vector3.Dot(toBoat, up) * up;
            float yawToBoat = Vector3.SignedAngle(fwd, toBoat, up);
            yawToBoat = Mathf.Clamp(yawToBoat, -75f, 75f);

            bool J = jumpOn && jumpBootL != null && jumpBootR != null;
            Transform fb = J ? jumpBootL : frontBoot, rb = J ? jumpBootR : rearBoot;
            if (!J) PoseSki(root, s, carve);
            float sw = J ? 0f : Mathf.Clamp01(Mathf.InverseLerp(stretchFrom01, stretchFull01, rope01)) * Mathf.SmoothStep(0f, 1f, releaseW) * s;
            Stretch01 = sw;
            float edge01n = Mathf.Clamp01(Mathf.Abs(edgeSigned) / 0.35f);
            var ph = (!J && buoy > 0) ? poses.EvaluateSlalom(latS, inbS, Mathf.Clamp01(carve), edge01n, reachTowardBoat) : poses.EvaluateSlalom(Mathf.Lerp(0.15f, 0.45f, s), s, Mathf.Clamp01(carve), edge01n, 1);
            if (J) ph.free = 0f;
            kneeS = Mathf.Lerp(kneeS, ph.knee, k);
            armS = Mathf.Lerp(armS, ph.arm, k);
            // Feet key off the boot attaches (rear boot is yawed 12°). Ankle targets are in that boot's local space.
            Vector3 aF = AnkleTarget(fb), aR = AnkleTarget(rb);
            float legLen = (Vector3.Distance(lUL.position, lLL.position) + Vector3.Distance(lLL.position, lF.position));
            float crouch = SkierPoses.CrouchForKnee(kneeS);
            if (J && jumpCrouch >= 0f) crouch = jumpCrouch;
            crouch = Mathf.Lerp(crouch, dwsCrouch, dwsDepth);
            Vector3 mid = (aF + aR) * 0.5f;
            // sideways stack on the ski. The root roll is the big lean; only a few degrees of extra hinge, so the pull stays one board.
            Vector3 rightH = new Vector3(right.x, 0f, right.z); rightH = rightH.sqrMagnitude > 1e-4f ? rightH.normalized : right;
            Vector3 fwdH = Vector3.Cross(rightH, Vector3.up);
            float sideS = leanSide;
            float fwdShare = (edge01n < 0.25f) ? 0f : leanFwdShare;
            Vector3 Lat(Vector3 d) { float fd = Mathf.Clamp(Vector3.Dot(d, fwdH) / Mathf.Max(0.2f, Mathf.Abs(Vector3.Dot(d, rightH))), -1f, 1f); return rightH * sideS + fwdH * (fd * fwdShare * Mathf.Abs(sideS)); }
            Vector3 away = Lat(toBoat.sqrMagnitude > 1e-4f ? -toBoat.normalized : -right);
            away -= Vector3.Dot(away, up) * up;
            float rootRoll = Vector3.Angle(up, Vector3.up);
            bool airborne = J && (jumpPose == 1 || jumpPose == 2);
            float extra = airborne ? 0f : Mathf.Clamp(ph.lean - rootRoll, 0f, poses.bodyExtraDeg);
            float lean = extra * Mathf.Deg2Rad;
            LeanTotalDeg = rootRoll + extra;
            Vector3 leanUp = (up * Mathf.Cos(lean) + away * Mathf.Sin(lean)).normalized;
            if (J && jumpPitchDeg != 0f) leanUp = Quaternion.AngleAxis(jumpPitchDeg, right) * leanUp;
            // shoulders open toward the boat on the pull; on the reach they open toward the boat and down the course at every buoy (odd/even only flips the side) and finish last
            float open = Mathf.Clamp(ph.shoulderToBoat, -1f, 1f);
            float yawWant = (open >= 0f ? yawToBoat : -yawToBoat) * Mathf.Abs(open);
            shoulderYaw = dt <= 0f ? yawWant : Mathf.MoveTowards(shoulderYaw, yawWant, poses.shoulderYawRate * dt);
            hips.rotation = Quaternion.FromToRotation(up, leanUp) * Quaternion.AngleAxis(shoulderYaw * 0.2f, up) * hips.rotation;
            Vector3 hipJointMid = (lUL.position + rUL.position) * 0.5f;
            Vector3 pelvisWant = mid + fwd * pelvisFwd + leanUp * (legLen * crouch);
            hips.position += pelvisWant - hipJointMid;
            Vector3 twistAx = Vector3.Lerp(up, leanUp, 0.5f).normalized;
            Vector3 chestRight = Quaternion.AngleAxis(shoulderYaw, up) * right;
            if (spine != null) spine.rotation = Quaternion.AngleAxis(shoulderYaw * 0.4f, twistAx) * spine.rotation;
            if (chest != null && chest != spine) chest.rotation = Quaternion.AngleAxis(shoulderYaw * 0.4f, twistAx) * chest.rotation;
            if (!J && neck != null && spine != null && ph.straight > 0.05f)
            {
                Vector3 cur = (neck.position - hips.position).normalized;
                if (cur.sqrMagnitude > 1e-6f) spine.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, leanUp), ph.straight * 0.85f) * spine.rotation;
            }

            Vector3 BootFwd(Transform boot)
            {
                Vector3 bf = boot != null ? boot.forward : fwd;
                bf -= Vector3.Dot(bf, up) * up;
                return bf.sqrMagnitude > 1e-6f ? bf.normalized : fwd;
            }
            Vector3 highSide = (Vector3.up - up) * 0.5f;
            if (J)
            {
                Vector3 lift = jumpPose == 3 ? up * 0.12f : Vector3.zero;
                TwoBone(lUL, lLL, lF, aF, (lUL.position + aF) * 0.5f + BootFwd(fb) * 0.45f - right * 0.05f + highSide + lift);
                TwoBone(rUL, rLL, rF, aR, (rUL.position + aR) * 0.5f + BootFwd(rb) * 0.45f + right * 0.05f + highSide + lift);
            }
            else
            {
                TwoBone(lUL, lLL, lF, aF, (lUL.position + aF) * 0.5f + BootFwd(fb) * 0.5f + highSide);
                TwoBone(rUL, rLL, rF, aR, (rUL.position + aR) * 0.5f + BootFwd(rb) * 0.5f + highSide);
            }
            AimFoot(lF, lT, fb); AimFoot(rF, rT, rb);

            Vector3 sh = (lUA.position + rUA.position) * 0.5f;
            Vector3 dirP = pylon - sh; dirP -= Vector3.Dot(dirP, up) * up;
            if (dirP.sqrMagnitude < 1e-6f) dirP = fwd; else dirP.Normalize();
            float armFrac = armS;
            if (J)
            {
                if (jumpPose == 2) armFrac = poses.flightArm;
                else if (jumpPose == 1) armFrac = poses.takeoffArm;
                else if (jumpPose == 3) armFrac = poses.landArm;
                else armFrac = poses.approachArm;
            }
            float drop = J ? poses.jumpHandleDrop : ph.handleDrop;
            float fwdD = J ? poses.jumpHandleFwd : poses.handleFwd;
            Vector3 hipC = (lUL.position + rUL.position) * 0.5f;
            Vector3 waist = hipC + dirP * fwdD - up * drop;
            float armLen = Vector3.Distance(lUA.position, lLA.position) + Vector3.Distance(lLA.position, lH.position);
            Vector3 aim = waist - sh; if (aim.sqrMagnitude < 1e-6f) aim = dirP;
            float reachFrac = Mathf.Clamp(aim.magnitude / Mathf.Max(0.05f, armLen), 0.78f, armFrac);
            Vector3 handleC = sh + aim.normalized * (armLen * reachFrac);
            if (!J && reachTowardBoat > 0 && ph.free > 0.4f) handleC += dirP * (poses.reachArm * 0.08f);
            Vector3 bar = Vector3.Cross(up, dirP); if (bar.sqrMagnitude < 1e-6f) bar = right; else bar.Normalize();
            Vector3 tL = handleC - bar * handHalf, tR = handleC + bar * handHalf;
            Vector3 boatH = toBoat; boatH.y = 0f; if (boatH.sqrMagnitude > 1e-6f) boatH.Normalize(); else boatH = fwd;
            Vector3 reachDir = boatH;   // #125: handle arm reaches inward toward the boat at every buoy
            // #125: the OUTSIDE hand (away from the boat) is the one released, consistently; the free arm goes out over the buoy side
            float wantSide = -Mathf.Sign(Vector3.Dot(boatH, right));
            if (Mathf.Abs(wantSide) < 0.05f) wantSide = releaseSide >= 0f ? 1f : -1f;
            float wantRel = J ? 0f : (buoy > 0 ? ph.free : Mathf.Clamp01((turnReach - 0.25f) / 0.5f));
            releaseW = Mathf.Lerp(releaseW, wantRel, dt <= 0f ? 1f : 1f - Mathf.Exp(-6f * dt));
            if (wantRel > 0.05f && releaseW < 0.12f) releaseSide = wantSide;
            Vector3 fH = new Vector3(fwd.x, 0f, fwd.z); if (fH.sqrMagnitude > 1e-6f) fH.Normalize(); else fH = fwd;
            Transform frUA = releaseSide > 0f ? rUA : lUA;
            Vector3 free = frUA.position + (reachDir * poses.freeArmOut + fH * poses.freeArmFwd - up * 0.06f).normalized * (armLen * 0.92f);
            ReleaseL = releaseSide < 0f ? releaseW : 0f; ReleaseR = releaseSide > 0f ? releaseW : 0f;
            tL = Vector3.Lerp(tL, free, ReleaseL); tR = Vector3.Lerp(tR, free, ReleaseR);
            float pw = Mathf.Clamp01(Mathf.Max(releaseW, 1f - armFrac));
            Vector3 pL = Vector3.Lerp(lLA.position, (lUA.position + tL) * 0.5f, pw);
            Vector3 pR = Vector3.Lerp(rLA.position, (rUA.position + tR) * 0.5f, pw);
            float returning = Mathf.Clamp01(releaseW) * (1f - wantRel);
            Vector3 tuckHip = releaseSide > 0f ? rUL.position : lUL.position;
            if (releaseSide < 0f) pL = Vector3.Lerp(pL, tuckHip + up * 0.08f, returning);
            else pR = Vector3.Lerp(pR, tuckHip + up * 0.08f, returning);
            TwoBone(lUA, lLA, lH, tL, pL - up * 0.25f - chestRight * 0.15f);
            TwoBone(rUA, rLA, rH, tR, pR - up * 0.25f + chestRight * 0.15f);
            headTiltWithBody = poses.headTiltWithBody; headPitchDownDeg = poses.headPitchDownDeg; gazeToBoat = poses.gazeToBoat;
            LevelHead(up, fwd, reachTowardBoat < 0 ? fwd : toBoat);
            HandsFromBones();
            MeasureClearance(root);
        }

        void TrackEdge(Transform root0, float dt)
        {
            if (root0 == null) { edgeSigned = 0f; return; }
            Vector3 rightH = new Vector3(root0.right.x, 0f, root0.right.z);
            rightH = rightH.sqrMagnitude > 1e-4f ? rightH.normalized : root0.right;
            edgeSigned = Vector3.Dot(root0.up, rightH);
            if (Mathf.Abs(edgeSigned) > 0.09f || Mathf.Abs(leanSideT) < 0.01f) leanSideT = edgeSigned >= 0f ? 1f : -1f;
            leanSide = dt <= 0f ? leanSideT : Mathf.MoveTowards(leanSide, leanSideT, leanSideRate * dt);
        }

        /// Ski sits in the water at speed: tail buried, tip up, rolled further onto its edge in a carve.
        /// Pivot = skier origin (ankle midpoint); the boots ride on the ski so the leg IK follows.
        void PoseSki(Transform root, float s, float carve)
        {
            if (ski == null && frontBoot != null) ski = frontBoot.parent;
            if (ski == null) return;
            if (!skiBaseSet) { skiBasePos = ski.localPosition; skiBaseSet = true; }
            Vector3 hR = Vector3.Cross(Vector3.up, root.forward); float rollSign = hR.sqrMagnitude > 1e-6f ? -Mathf.Sign(Vector3.Dot(root.up, hR.normalized)) : 0f;
            float extraRoll = rollSign * Mathf.Clamp01(carve * 1.6f) * skiCarveRollDeg;
            // the root already pitches 3 deg nose-down for the body; undo that on the ski and add the tip-up
            var R = Quaternion.Euler(-(3f + skiTipUpDeg * Mathf.Lerp(0.5f, 1f, s)), 0f, extraRoll);
            ski.localRotation = R;
            ski.localPosition = R * skiBasePos + Vector3.down * (skiSinkAtSpeed * Mathf.Lerp(0.3f, 1f, s));
        }

        // b10.2: the competition vest is a rigid merged mesh on the skier root, fitted to the upright torso. With the rig's 50-60 deg
        // lean it stayed upright round the belly (the grey ball in side views); it now rides on the Spine bone.

        /// Head level, eyes forward (a little toward the boat/next buoy), neck not hyperextended.
        void LevelHead(Vector3 up, Vector3 fwd, Vector3 toBoat)
        {
            if (head == null) return;
            if (neck != null)
            {
                Vector3 d = (head.position - neck.position).normalized;
                neck.rotation = Quaternion.FromToRotation(d, Vector3.Slerp(d, Vector3.up, 0.5f)) * neck.rotation;
            }
            Vector3 g = Vector3.Slerp(new Vector3(fwd.x, 0, fwd.z).normalized, toBoat.sqrMagnitude > 1e-4f ? new Vector3(toBoat.x, 0, toBoat.z).normalized : fwd, gazeToBoat);
            if (chest != null)
            {
                Vector3 cf = chest.forward; cf.y = 0;
                if (cf.sqrMagnitude > 1e-4f) { cf.Normalize(); float a = Vector3.SignedAngle(cf, g, Vector3.up); g = Quaternion.AngleAxis(Mathf.Clamp(a, -55f, 55f), Vector3.up) * cf; }
            }
            Vector3 right = Vector3.Cross(Vector3.up, g).normalized;
            g = Quaternion.AngleAxis(headPitchDownDeg, right) * g;
            head.rotation = Quaternion.LookRotation(g, Vector3.Slerp(Vector3.up, up, headTiltWithBody));   // eyes ~level with the horizon
        }

        void MeasureClearance(Transform root)
        {
            float wy = 0f, m = 9f;   // lake surface is y = 0 (waves +-5 cm)
            // bone centre minus a rough flesh radius (knees/hands are thin, torso and head are not)
            var bones = new[] { hips, spine, chest, neck, head, lLA, rLA, lH, rH, lLL, rLL };
            var rad = new[] { 0.12f, 0.13f, 0.15f, 0.07f, 0.12f, 0.045f, 0.045f, 0.04f, 0.04f, 0.055f, 0.055f };
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && bones[i].position.y - rad[i] - wy < m) { m = bones[i].position.y - rad[i] - wy; LowestBone = bones[i].name; }
            RootY = root.position.y;
            Debug_ = $"hipsY={hips.position.y:F2} kneeL={lLL.position.y:F2} kneeR={rLL.position.y:F2} footL={lF.position.y:F2} footR={rF.position.y:F2} handL={lH.position.y:F2} handR={rH.position.y:F2} headY={(head ? head.position.y : 0):F2} legLen={(Vector3.Distance(lUL.position, lLL.position) + Vector3.Distance(lLL.position, lF.position)):F2}";
            WaterClearance = m;
        }

        // Offsets are on the boot attach (heel at the origin, +Z to the toes), not on the plate mesh.
        // The rear boot's 12° yaw is in that transform, so the ankle and the sole follow it.
        Vector3 AnkleTarget(Transform boot) => boot.TransformPoint(new Vector3(0, 0.012f + ankleAboveSole, 0.068f));

        void AimFoot(Transform foot, Transform toes, Transform boot)
        {
            if (foot == null || boot == null) return;
            Vector3 cur = toes != null ? toes.position - foot.position : foot.forward;
            if (cur.sqrMagnitude < 1e-8f) return;
            var align = Quaternion.LookRotation(boot.forward, boot.up) * Quaternion.Inverse(Quaternion.LookRotation(cur.normalized, foot.up));
            foot.rotation = align * foot.rotation;
        }

        static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            if (a == null || b == null || c == null) return;
            float la = Vector3.Distance(a.position, b.position), lb = Vector3.Distance(b.position, c.position);
            Vector3 ap = a.position, t = target - ap;
            float d = Mathf.Clamp(t.magnitude, 0.02f, (la + lb) * 0.999f);
            Vector3 dir = t.normalized;
            Vector3 pp = pole - ap; Vector3 bend = pp - Vector3.Dot(pp, dir) * dir;
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.Cross(dir, Vector3.right);
            bend.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2 * la * d), -1f, 1f);
            Vector3 mid = ap + dir * (la * cosA) + bend * (la * Mathf.Sqrt(1 - cosA * cosA));
            a.rotation = Quaternion.FromToRotation(b.position - ap, mid - ap) * a.rotation;
            Vector3 end = ap + dir * d;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, end - b.position) * b.rotation;
        }

        void HandsFromBones()
        {
            HandL = lH != null ? lH.position : transform.position + Vector3.up;
            HandR = rH != null ? rH.position : transform.position + Vector3.up;
        }

        /// <summary>Back-compat: midpoint of the hands.</summary>
        public bool HandPoint(out Vector3 p) { p = (HandL + HandR) * 0.5f; return lH != null; }

        void OnDisable() { if (graph.IsValid()) graph.Destroy(); built = false; }
        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); built = false; }
    }
}

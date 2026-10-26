using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SkiView
{
    /// <summary>Rigged slalom skier (Mixamo Remy, Humanoid). Layers, in order:
    /// 1. A manual PlayableGraph of upper-body clips (idle breathing, a bit of "Pulling A Rope"), or "Treading Water" after a fall.
    /// 2. Stance (Docs/SLALOM-SKIER-LOOK.md): pelvis up and forward over the front foot, two-bone leg IK into the
    ///    tandem high-wrap boots (front knee bent more), feet aimed down the ski, shoulders back, upper body yawed toward
    ///    the boat (counter-rotation against the ski).
    /// 3. Arms: two-bone IK, straight arms to the handle low in front of the hips. The outside hand releases and reaches
    ///    toward the buoy in the turn.
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
        public float leanFwdShare = 0.08f, leanSideRate = 3.5f; float leanSide, leanSideT;   // b10.8: lean sideways not forward; b10.3: max forward component of the lean direction
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
            RiderAction action = RiderAction.None, float actionT = 0f, float depth = 0f)
        {
            Build();
            if (!built) return;
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
            else
            {
                target[0] = 1f - 0.3f * load; target[1] = 0.3f * load;
                if (pull != null && pull.name.Contains("SlalomStance")) { target[0] = 0.15f * (1f - load); target[1] = 1f - target[0]; }
                if (leanL != null && leanR != null && root0 != null)
                {
                    // lean poses on the turn side: leaning away from the boat, more of it as the load builds
                    Vector3 tb = pylon - root0.position; float side = -Vector3.Dot(tb, root0.right);   // >0 = leaning right
                    float lw = Mathf.Clamp01(load) * Mathf.Clamp01(Mathf.Abs(side) / 3f) * 0.85f;
                    target[side > 0 ? 8 : 7] = lw; target[1] *= 1f - lw; target[0] *= 1f - lw;
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
            if (start != null && mix.GetInput(4).IsValid()) mix.GetInput(4).SetTime(Mathf.Min(actionT * 0.8f, start.length - 0.02f));
            if (fall != null && mix.GetInput(5).IsValid()) mix.GetInput(5).SetTime(Mathf.Min(actionT, fall.length - 0.02f));
            for (int i = 0; i < NCLIP; i++) { w[i] = Mathf.Lerp(w[i], target[i], k); if (mix.GetInput(i).IsValid()) mix.SetInputWeight(i, w[i]); }
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
            // --- pelvis: up and forward over the front foot
            Vector3 aF = AnkleTarget(fb), aR = AnkleTarget(rb);
            float legLen = (Vector3.Distance(lUL.position, lLL.position) + Vector3.Distance(lLL.position, lF.position));
            float edge01 = Mathf.Clamp01(Vector3.Angle(up, Vector3.up) / 65f);
            float crouch = Mathf.Lerp(crouchGlide, crouchPull, s) - hipDropAtEdge * edge01;   // hips low and close to the water at the buoy
            crouch = Mathf.Lerp(crouch, Mathf.Min(0.9f, crouch + stretchCrouchUp), sw) - stretchHipDrop * sw;   // reach v2: long legs, the lean puts the hips down
            if (J && jumpCrouch >= 0f) crouch = jumpCrouch;
            crouch = Mathf.Lerp(crouch, dwsCrouch, dwsDepth);   // deep-water start: knees to the chest behind the ski
            Vector3 mid = (aF + aR) * 0.5f;
            Vector3 hipJointMid = (lUL.position + rUL.position) * 0.5f;
            // stacked lean: ankle->hip->shoulder line tilts away from the boat, feet stay in the boots
            // b10.3 (Scott): the lean goes to the SIDE, in the ski's lateral plane (ski on edge, body stretched sideways round the ball),
            // not toward the tips or the boat; only a small natural forward share (leanFwdShare) is kept
            Vector3 rightH = new Vector3(right.x, 0f, right.z); rightH = rightH.sqrMagnitude > 1e-4f ? rightH.normalized : right;
            Vector3 fwdH = Vector3.Cross(rightH, Vector3.up);
            float edgeD = Vector3.Dot(up, rightH); if (Mathf.Abs(edgeD) > 0.09f || leanSideT == 0f) leanSideT = edgeD >= 0f ? 1f : -1f; float sideT = leanSideT;   // full side once the ski is edged >5 deg; holds through a flat moment
            leanSide = dt <= 0f ? sideT : Mathf.MoveTowards(leanSide, sideT, leanSideRate * dt); float sideS = leanSide;   // eased so the edge change can't snap the lean across   // the side the ski is edged toward (inside of the turn, toward the ball at the apex); continuous through the edge change
            Vector3 Lat(Vector3 d) { float fd = Mathf.Clamp(Vector3.Dot(d, fwdH) / Mathf.Max(0.2f, Mathf.Abs(Vector3.Dot(d, rightH))), -1f, 1f); return rightH * sideS + fwdH * (fd * leanFwdShare * Mathf.Abs(sideS)); }   // not normalised: a flat ski fades the lean out instead of flipping it
            Vector3 away = Lat(toBoat.sqrMagnitude > 1e-4f ? -toBoat.normalized : -right);
            away -= Vector3.Dot(away, up) * up;
            float leanWant = sw * stretchLeanExtraDeg + Mathf.Lerp(leanTotalPullDeg, Mathf.Lerp(leanTotalApexShortDeg, leanTotalApexDeg, Mathf.Clamp01(rope01)), Mathf.Clamp01(turnReach)) * s;   // Holly: 40-50 deg on the pull, 50-65 at the apex (ski edge + body)
            float rootRoll = Vector3.Angle(up, Vector3.up);
            float lean = (Mathf.Lerp(leanGlideDeg, leanPullDeg, s) + (J ? 0f : Mathf.Clamp(leanWant - rootRoll, 0f, Mathf.Lerp(leanBodyMaxDeg, stretchBodyMaxDeg, sw)))) * Mathf.Deg2Rad;
            LeanTotalDeg = rootRoll + lean * Mathf.Rad2Deg;
            Vector3 leanUp = (up * Mathf.Cos(lean) + away * Mathf.Sin(lean)).normalized;
            if (sw > 0f)
            {
                // reach v2/b10.3: at the ball the stretch leans INTO the turn, sideways toward the ball (the long line ski -> hips -> shoulders -> inside
                // arm -> rope), in world terms so the root roll can't cancel it, capped relative to the root
                Vector3 tbH = Lat(toBoat.sqrMagnitude > 1e-4f ? toBoat : right);   // b10.3: sideways toward the ball, not toward the boat/tips
                float a = stretchLeanDeg * Mathf.Deg2Rad;
                Vector3 wantW = Vector3.up * Mathf.Cos(a) + tbH * Mathf.Sin(a);
                wantW = Vector3.RotateTowards(up, wantW.normalized, stretchBodyMaxDeg * Mathf.Deg2Rad, 0f);
                leanUp = Vector3.Slerp(leanUp, wantW, sw).normalized;
                LeanTotalDeg = Mathf.Lerp(LeanTotalDeg, Vector3.Angle(leanUp, Vector3.up), sw);
            }
            if (J && jumpPitchDeg != 0f) leanUp = Quaternion.AngleAxis(jumpPitchDeg, right) * leanUp;
            hips.rotation = Quaternion.FromToRotation(up, leanUp) * Quaternion.AngleAxis(yawToBoat * hipYawShare, up) * hips.rotation;
            hipJointMid = (lUL.position + rUL.position) * 0.5f;
            Vector3 pelvisWant = mid + fwd * (pelvisFwd - stretchHipBack * sw) + leanUp * (legLen * crouch);
            hips.position += pelvisWant - hipJointMid;
            if (chest != null) { float e = chestExtraLeanDeg * s * Mathf.Deg2Rad; chest.rotation = Quaternion.FromToRotation(leanUp, (leanUp * Mathf.Cos(e) + away * Mathf.Sin(e)).normalized) * chest.rotation; }
            // upper body: counter-rotate toward the boat, shoulders back against the pull
            float upShare = Mathf.Lerp(upperYawShare, 1f, sw);   // reach v2: shoulders open to the boat
            Vector3 chestRight = Quaternion.AngleAxis(yawToBoat * upShare, up) * right;
            float jBack = J ? Mathf.Clamp01(1f - jumpPitchDeg / 30f) : 1f;   // pass 14: no shoulders-back against the pull in the air
            Vector3 twistAx = Vector3.Lerp(up, leanUp, sw).normalized;   // reach v2: open the shoulders about the leaned torso axis (about root-up it swung the lean away)
            if (spine != null) spine.rotation = Quaternion.AngleAxis(yawToBoat * (upShare - hipYawShare) * 0.5f, twistAx) * Quaternion.AngleAxis(-shoulderBackDeg * 0.5f * jBack, right) * spine.rotation;
            if (chest != null && chest != spine) chest.rotation = Quaternion.AngleAxis(yawToBoat * (upShare - hipYawShare) * 0.5f, twistAx) * Quaternion.AngleAxis(-shoulderBackDeg * 0.5f * jBack, right) * chest.rotation;
            // pass 14: in the air the clip's leaning-back upper body folds forward over the skis (the bird), so the side view isn't upright
            if (J && jumpPitchDeg > 0f) { float jf = jumpPitchDeg * 0.45f; if (spine != null) spine.rotation = Quaternion.AngleAxis(jf, right) * spine.rotation; if (chest != null && chest != spine) chest.rotation = Quaternion.AngleAxis(jf, right) * chest.rotation; }

            // reach v2: body long - the hip-to-neck line straightened onto the leaned leg line (no break at the waist)
            if (sw > 0f && neck != null && spine != null)
            {
                Vector3 cur = (neck.position - hips.position).normalized;
                spine.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, leanUp), sw * stretchBodyLongW) * spine.rotation;
            }
            // --- legs into the boots (left foot forward); knees point down the ski, slightly out
            // knee poles from the hip-ankle midpoint (not the clip's knee, which dragged the rear knee down onto the ski):
            // front knee forward over the toes, rear knee forward and a little up so it tucks behind the front calf
            Vector3 highSide = (Vector3.up - up) * 0.5f;   // when the body is rolled, keep the knees on the high side, off the water
            if (J)
            {
                // two skis side by side: both knees forward over the toes, a little apart
                TwoBone(lUL, lLL, lF, aF, (lUL.position + aF) * 0.5f + fwd * 0.6f - right * 0.05f + highSide);
                TwoBone(rUL, rLL, rF, aR, (rUL.position + aR) * 0.5f + fwd * 0.6f + right * 0.05f + highSide);
            }
            else
            {
                TwoBone(lUL, lLL, lF, aF, (lUL.position + aF) * 0.5f + fwd * 0.6f - right * 0.06f + highSide);
                TwoBone(rUL, rLL, rF, aR, (rUL.position + aR) * 0.5f + fwd * 0.6f + up * 0.25f + right * 0.08f + highSide * 1.4f);
            }
            AimFoot(lF, lT, fb); AimFoot(rF, rT, rb);

            // --- arms: straight to the handle, low in front of the hips, in the rope direction
            Vector3 sh = (lUA.position + rUA.position) * 0.5f;
            Vector3 dirP = (pylon - sh); dirP -= Vector3.Dot(dirP, up) * up; dirP.Normalize();
            float hin = J ? Mathf.Clamp01(jumpHandleIn) : 0f;
            Vector3 handleC = (lUL.position + rUL.position) * 0.5f + dirP * Mathf.Lerp(handleFwd, 0.10f, hin) - up * Mathf.Lerp(handleDrop, 0.02f, hin);
            float armLen = Vector3.Distance(lUA.position, lLA.position) + Vector3.Distance(lLA.position, lH.position);
            Vector3 toH = handleC - sh; handleC = sh + toH.normalized * Mathf.Min(toH.magnitude * (hin > 0f ? 1f : 99f), armLen * Mathf.Lerp(Mathf.Lerp(0.9f, 0.97f, s), 0.995f, releaseW));   // arms long in the pull; handle in to the hips in the air
            handleC += dirP * (reachArmM * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(rope01)) * releaseW);   // b9.1: reach on the inside arm toward the boat
            Vector3 bar = Vector3.Cross(up, dirP).normalized;   // handle bar axis (skier's right when facing the boat)
            Vector3 tL = handleC - bar * handHalf, tR = handleC + bar * handHalf;
            // turn: release the outside hand (the one away from the boat) and reach forward/out toward the buoy
            float outside = -Mathf.Sign(Vector3.Dot(toBoat, right));
            float wantRel = Mathf.Clamp01((turnReach - 0.25f) / 0.5f);   // only around the buoy, never on the pull
            releaseW = Mathf.Lerp(releaseW, wantRel, dt <= 0 ? 1f : 1f - Mathf.Exp(-6f * dt));
            if (wantRel > 0.05f && releaseW < 0.1f) releaseSide = outside;   // b9.1: latch the side for the whole release (re-latching mid-turn swapped hands = buoy pop)
            Vector3 fH = new Vector3(fwd.x, 0, fwd.z).normalized, rH2 = Vector3.Cross(Vector3.up, fH);
            // Holly: both hands on the handle on the pulls; around the buoy only, the free hand tucks to the vest (chest, a little forward)
            Vector3 vest = (chest != null ? chest.position : sh - up * 0.2f) + fH * 0.16f + rH2 * releaseSide * 0.08f - up * 0.06f;
            Vector3 reach = vest;
            ReleaseL = releaseSide < 0 ? releaseW : 0f; ReleaseR = releaseSide > 0 ? releaseW : 0f;
            if (sw > 0f)
            {
                // inside hand: the arm in line with the rope, fully extended from its shoulder (toward the pylon, rope height included)
                Transform inUA = releaseSide < 0 ? rUA : lUA;
                Vector3 ropeDir = (pylon - inUA.position).normalized;
                Vector3 grip = inUA.position + ropeDir * (armLen * 0.985f);
                if (releaseSide < 0) tR = Vector3.Lerp(tR, grip, sw); else tL = Vector3.Lerp(tL, grip, sw);
                // free hand: off the handle and clear of it, low and out behind on the outside (balance), not tucked at the vest
                Transform frUA = releaseSide < 0 ? lUA : rUA;
                Vector3 awayH = new Vector3(away.x, 0f, away.z).normalized;
                Vector3 free = frUA.position + (awayH * 0.55f + Vector3.down * 0.65f - fH * 0.25f).normalized * (armLen * 0.98f);   // near-straight arm (a hair of bend keeps the elbow plane stable)
                reach = Vector3.Lerp(vest, free, sw);
            }
            tL = Vector3.Lerp(tL, reach, ReleaseL); tR = Vector3.Lerp(tR, reach, ReleaseR);
            // b10.2: around the ball the elbow pole comes from the shoulder-target line, not the clip's elbow - near-straight arms took the
            // clip elbow as the bend plane and flipped 20-60 deg/frame as the stretch let go
            float pw = Mathf.Max(releaseW, sw);
            Vector3 pL = Vector3.Lerp(lLA.position, (lUA.position + tL) * 0.5f, pw), pR = Vector3.Lerp(rLA.position, (rUA.position + tR) * 0.5f, pw);
            TwoBone(lUA, lLA, lH, tL, pL - up * 0.3f - chestRight * 0.2f);
            TwoBone(rUA, rLA, rH, tR, pR - up * 0.3f + chestRight * 0.2f);
            LevelHead(up, fwd, toBoat);
            HandsFromBones();
            MeasureClearance(root);
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

        Vector3 AnkleTarget(Transform boot) => boot.TransformPoint(new Vector3(0, 0.012f + ankleAboveSole, 0.068f));

        void AimFoot(Transform foot, Transform toes, Transform boot)
        {
            if (foot == null || toes == null) return;
            Vector3 want = boot.TransformPoint(new Vector3(0, 0.012f + 0.03f, 0.21f));
            Vector3 cur = toes.position - foot.position, des = want - foot.position;
            if (cur.sqrMagnitude > 1e-6f && des.sqrMagnitude > 1e-6f) foot.rotation = Quaternion.FromToRotation(cur, des) * foot.rotation;
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

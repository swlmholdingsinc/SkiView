using UnityEngine;

namespace SkiView
{
    /// <summary>
    /// Slalom ski spray (pass 3). Three world-space particle systems on the ski:
    ///  - Sheet: the spray wall thrown off the ski's base side in a carve (opposite the lean). Scales with edge angle and speed.
    ///  - Rooster: tail plume up and back. Scales with speed, and grows with edge.
    ///  - Foam: flat white churn at the tail that trails behind as wake foam.
    /// Driven from SkiViewGame.ApplyVisuals. In edit mode (headless capture) the systems are stepped with Simulate().
    /// </summary>
    public class SkiSpray : MonoBehaviour
    {
        public ParticleSystem sheet, rooster, foam, legs;   // pass 7: legs = spray thrown up the shins
        public ParticleSystem mist;                          // pass 14: fine mist hanging around the sheet and rooster tail
        public float tailZ = 0.05f, sheetZ = 0.55f, bootsZ = 0.85f;   // local z on the ski (origin at the tail)

        /// <summary>Pass 14: layered spray. drop = droplets / motion streaks, sheet = torn translucent sheet, mist = fine mist, foam = flat foam.</summary>
        public static SkiSpray Create(Transform ski, Material drop, Material sheetMat = null, Material mistMat = null, Material foamMat = null)
        {
            var sp = ski.gameObject.AddComponent<SkiSpray>();
            sheetMat = sheetMat ?? drop; mistMat = mistMat ?? drop; foamMat = foamMat ?? mistMat;
            sp.sheet = Make("Spray_Sheet", ski, sheetMat, 2200, 0.6f, 1.0f, new Vector2(0.06f, 0.16f), 0.55f, 1.7f, true);
            { var r = sp.sheet.GetComponent<ParticleSystemRenderer>(); r.velocityScale = 0.05f; r.lengthScale = 1.5f; }
            sp.rooster = Make("Spray_Rooster", ski, drop, 1800, 0.75f, 1.0f, new Vector2(0.025f, 0.07f), 0.8f, 1.2f, true);
            sp.foam = Make("Spray_TailFoam", ski, foamMat, 1500, 2.2f, 0f, new Vector2(0.22f, 0.50f), 0.55f, 2.6f, false, true);
            sp.legs = Make("Spray_Legs", ski, drop, 800, 0.32f, 1.0f, new Vector2(0.010f, 0.028f), 0.8f, 1.1f, true);
            sp.mist = Make("Spray_Mist", ski, mistMat, 900, 1.6f, 0.05f, new Vector2(0.2f, 0.5f), 0.09f, 3.2f, false);
            { var lv = sp.mist.limitVelocityOverLifetime; lv.enabled = true; lv.dampen = 0.1f; lv.limit = 0.5f; }
            return sp;
        }

        public static ParticleSystem MakeSystem(string name, Transform parent, Material mat, int max, float life, float gravity, Vector2 size, float alpha, float grow, bool streak = false)
            => Make(name, parent, mat, max, life, gravity, size, alpha, grow, streak);
        /// <summary>Pass 13: flat (horizontal billboard) foam on the water.</summary>
        public static ParticleSystem MakeFlatSystem(string name, Transform parent, Material mat, int max, float life, Vector2 size, float alpha, float grow)
            => Make(name, parent, mat, max, life, 0f, size, alpha, grow, false, true);

        static ParticleSystem Make(string name, Transform parent, Material mat, int max, float life, float gravity, Vector2 size, float alpha, float grow, bool streak = false, bool flat = false)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.duration = 1f; m.loop = true; m.playOnAwake = true; m.maxParticles = max;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new Color(0.93f, 0.97f, 1f, alpha);
            m.gravityModifier = gravity; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.06f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, grow));
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = streak ? ParticleSystemRenderMode.Stretch : flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            if (streak) { r.velocityScale = 0.035f; r.lengthScale = 1.2f; } r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.maxParticleSize = 2f;
            return ps;
        }

        /// <param name="speed">skier speed, m/s</param>
        /// <param name="edge">0..1 edge angle (roll / max)</param>
        /// <param name="carve">0..1 carve rate</param>
        /// <param name="awayFromLean">world horizontal direction the ski base faces (spray throw side)</param>
        public void Drive(float speed, float edge, float carve, Vector3 awayFromLean, Vector3 skiFwd, float dt, bool active)
        {
            if (sheet == null) return;
            float sp01 = Mathf.InverseLerp(4f, 16f, speed);
            float e = Mathf.Clamp01(edge), c = Mathf.Clamp01(carve);
            float sheetAmt = active ? sp01 * Mathf.Clamp01(Mathf.Pow(e, 1.5f) * 0.6f + c * 0.9f) : 0f;
            float roosterAmt = active ? sp01 * (0.35f + 0.65f * Mathf.Max(e, c)) : 0f;
            float foamAmt = active ? sp01 : 0f;
            Vector3 up = Vector3.up, back = -skiFwd;

            // sheet: emitted along the base-side edge of the forebody, thrown out and up (a wall), a little back
            sheet.transform.position = transform.TransformPoint(new Vector3(0, 0f, sheetZ)) + awayFromLean * 0.07f;
            sheet.transform.rotation = Quaternion.LookRotation((awayFromLean * 1.0f + up * (0.45f + 0.45f * e) + back * 0.5f).normalized, up);
            var sm = sheet.main; sm.startSpeed = new ParticleSystem.MinMaxCurve(2f + 5f * sheetAmt, 3.5f + 9f * sheetAmt);
            var ssh = sheet.shape; ssh.shapeType = ParticleSystemShapeType.Cone; ssh.angle = 22f; ssh.radius = 0.30f; ssh.radiusThickness = 1f;
            var sem = sheet.emission; sem.rateOverTime = 5200f * sheetAmt * SkiViewQuality.Spray;   // T6: preset spray scale

            // rooster tail: straight up and back from the tail
            rooster.transform.position = transform.TransformPoint(new Vector3(0, 0f, tailZ));
            rooster.transform.rotation = Quaternion.LookRotation((up * 1.0f + back * 0.55f + awayFromLean * 0.25f * e).normalized, up);
            var rm = rooster.main; rm.startSpeed = new ParticleSystem.MinMaxCurve(1.8f + 3f * roosterAmt, 3f + 4.5f * roosterAmt);
            var rsh = rooster.shape; rsh.shapeType = ParticleSystemShapeType.Cone; rsh.angle = 10f + 8f * e; rsh.radius = 0.05f;
            var rem = rooster.emission; rem.rateOverTime = 1600f * roosterAmt * SkiViewQuality.Spray;

            // tail foam: flat churn left behind on the water
            foam.transform.position = transform.TransformPoint(new Vector3(0, 0f, tailZ + 0.1f)) + Vector3.up * 0.02f;
            foam.transform.rotation = Quaternion.LookRotation(back, up);
            var fm = foam.main; fm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
            var fsh = foam.shape; fsh.shapeType = ParticleSystemShapeType.Box; fsh.scale = new Vector3(0.25f + 0.3f * e, 0.01f, 0.3f);
            var fem = foam.emission; fem.rateOverTime = 260f * foamAmt * SkiViewQuality.Spray;

            // legs: fine droplets kicked up off the ski between the boots, splashing up the shins on the spray side
            if (legs != null)
            {
                float legAmt = active ? sp01 * (0.35f + 0.65f * Mathf.Max(e, c)) : 0f;
                legs.transform.position = transform.TransformPoint(new Vector3(0, 0.03f, bootsZ)) + awayFromLean * 0.06f;
                legs.transform.rotation = Quaternion.LookRotation((up * 1.0f + awayFromLean * 0.30f + back * 0.35f).normalized, up);
                var lm = legs.main; lm.startSpeed = new ParticleSystem.MinMaxCurve(1.4f + 1.2f * legAmt, 2.6f + 2.4f * legAmt);
                var lsh = legs.shape; lsh.shapeType = ParticleSystemShapeType.Cone; lsh.angle = 14f; lsh.radius = 0.16f; lsh.radiusThickness = 1f;
                var lem = legs.emission; lem.rateOverTime = 1100f * legAmt * SkiViewQuality.Spray;
            }

            // mist: rises off the sheet and the rooster tail, drifts back and hangs
            if (mist != null)
            {
                float mAmt = active ? sp01 * (0.25f * roosterAmt + 0.9f * sheetAmt) : 0f;
                mist.transform.position = Vector3.Lerp(sheet.transform.position + awayFromLean * 0.5f, rooster.transform.position, 0.4f) + up * 0.3f;
                mist.transform.rotation = Quaternion.LookRotation((up * 0.6f + awayFromLean * 0.6f + back * 0.6f).normalized, up);
                var mm = mist.main; mm.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f + 3f * mAmt);
                var msh = mist.shape; msh.shapeType = ParticleSystemShapeType.Cone; msh.angle = 35f; msh.radius = 0.35f;
                var mem = mist.emission; mem.rateOverTime = 420f * mAmt * SkiViewQuality.Spray;
            }
            if (!Application.isPlaying && dt > 0f)
            {
                // headless capture: step in small slices so emission spreads along the path
                float t = Mathf.Min(dt, 1.5f); int n = Mathf.CeilToInt(t / (1f / 60f));
                for (int i = 0; i < n; i++) { float h = t / n; sheet.Simulate(h, false, false, false); rooster.Simulate(h, false, false, false); foam.Simulate(h, false, false, false); if (legs != null) legs.Simulate(h, false, false, false); if (mist != null) mist.Simulate(h, false, false, false); }
            }
        }
    }
}

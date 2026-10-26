using UnityEngine;

namespace SkiView
{
    /// <summary>Pass 13: the tournament jump ramp's water (film sheeting down the waxed surface, drips off the top edge,
    /// droplets at the foot, foam along the waterline). Scene refs are wired by SkiViewBuilder.</summary>
    public partial class SkiViewGame
    {
        [Header("Jump ramp (pass 13)")]
        public Transform jumpRamp;
        [Tooltip("Pass 17: boat-path direction of the jump run (+1 = the baked 'southbound' pass, -1 = the return pass).")] public int jumpDir = 1;
        [Tooltip("Pass 17: a pass may start this far (m) before the course centre at the latest (186 = the home lake; short run-ins use less)")]
        public float passWindowLate = 186f;
        /// <summary>Compass word for a course direction ("SOUTHBOUND", "NORTHWESTBOUND", ...).</summary>
        public static string Bound(Vector3 d)
        {
            string[] n = { "NORTH", "NORTHEAST", "EAST", "SOUTHEAST", "SOUTH", "SOUTHWEST", "WEST", "NORTHWEST" };
            float az = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
            return n[Mathf.RoundToInt(az / 45f) % 8] + "BOUND";
        }
        public GameObject jumpCourse;
        public float rampHeight = 1.65f, rampLenOut = 6.85f, rampWidth = 4.22f, rampUnder = 0.6f;
        public ParticleSystem[] rampFx;
        public ParticleSystem rampFoam;
        public Vector3[] rampWaterline;
        float rampFoamAcc;
        bool rampBaseSet; Vector3 rampBasePos; Quaternion rampBaseRot;
        public float RampRatio => rampHeight / Mathf.Max(0.1f, rampLenOut);

        void RampWaterFx(float bdt)
        {
            if (jumpRamp == null) return;
            // pass 14: the ramp rides on its floats - a slow, small heave and pitch from the lake (anchored, so only cm)
            if (!rampBaseSet) { rampBasePos = jumpRamp.position; rampBaseRot = jumpRamp.rotation; rampBaseSet = true; }
            float heave = 0.010f * Mathf.Sin(simTime * 1.9f) + 0.005f * Mathf.Sin(simTime * 3.1f + 1.2f);
            float pitch = 0.10f * Mathf.Sin(simTime * 1.6f + 0.4f), roll = 0.07f * Mathf.Sin(simTime * 1.2f + 2.0f);
            if (mode == Mode.Jump && jphase == JPhase.Ramp) { heave -= 0.025f; pitch += 0.15f; }   // settles a touch as the skier runs up it
            jumpRamp.SetPositionAndRotation(rampBasePos + Vector3.up * heave, rampBaseRot * Quaternion.Euler(pitch, 0f, roll));
            if (rampFoam != null && rampWaterline != null && rampWaterline.Length > 1 && bdt > 0f)
            {
                // foam where the lake laps the toe, the aprons and the front skirt
                float total = 0f; for (int i = 1; i < rampWaterline.Length; i++) total += Vector3.Distance(rampWaterline[i - 1], rampWaterline[i]);
                rampFoamAcc += total * 9f * bdt;
                int n = Mathf.FloorToInt(rampFoamAcc); rampFoamAcc -= n;
                var ep = new ParticleSystem.EmitParams();
                for (int k = 0; k < n; k++)
                {
                    float d = (float)fxRnd.NextDouble() * total;
                    for (int i = 1; i < rampWaterline.Length; i++)
                    {
                        float L = Vector3.Distance(rampWaterline[i - 1], rampWaterline[i]);
                        if (d > L) { d -= L; continue; }
                        Vector3 p = Vector3.Lerp(rampWaterline[i - 1], rampWaterline[i], d / Mathf.Max(L, 1e-3f));
                        Vector3 outw = Vector3.Cross(rampWaterline[i] - rampWaterline[i - 1], Vector3.up).normalized;
                        ep.position = p + outw * (0.05f + 0.35f * (float)fxRnd.NextDouble()) + Vector3.up * 0.015f;
                        ep.velocity = outw * (0.05f + 0.15f * (float)fxRnd.NextDouble());
                        rampFoam.Emit(ep, 1); break;
                    }
                }
            }
            if (!Application.isPlaying && bdt > 0f)
            {
                int k = Mathf.CeilToInt(Mathf.Min(bdt, 1.5f) * 30f);
                for (int i = 0; i < k; i++)
                {
                    float h = Mathf.Min(bdt, 1.5f) / k;
                    if (rampFx != null) foreach (var ps in rampFx) if (ps != null) ps.Simulate(h, false, false, false);
                    if (rampFoam != null) rampFoam.Simulate(h, false, false, false);
                }
            }
        }
    }
}

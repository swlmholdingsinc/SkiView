using UnityEngine;

namespace SkiView
{
    /// <summary>Closed boat loop baked by Tools/gen_venue.py: southbound pass, south-island turn (east gap in,
    /// west gap out), northbound pass, north-island turn. Samples every 1 m with target speed and course coords.</summary>
    public class BoatPath : MonoBehaviour
    {
        public float step = 1f;
        public float[] xs, zs, speeds, along;
        public int[] dirs;
        public int[] spins;   // pass 19: 1 = spin-turn idle stretch (Holly turnaround v2): the skier floats while the boat spins round
        public Vector2 courseFwdSouth = new Vector2(-0.1646f, -0.9864f); // unity x,z

        // Runtime loop (not serialized): the baked trace re-sampled to exact 1 m arc length and, in the island turns only
        // (|along| > TURN_A0, i.e. past the pre-gates), Gaussian-smoothed in position and speed. The Ski View home-lake
        // trace has 0.05-4.65 m sample gaps and per-sample heading kinks up to 168 deg, and the index-based sampling
        // below turned those into speed surges (x4.6), heading flips and a jittery bank round the turn islands.
        // The course straights (buoys, gates, run-ins) keep their line (< 0.5 m).
        [System.NonSerialized] float[] rx, rz, rv, ra; [System.NonSerialized] int[] rd, rs; [System.NonSerialized] bool built;
        public const float TURN_A0 = 215f, TURN_A1 = 235f, POS_SIGMA = 5f, SPEED_SIGMA = 10f;

        public int Count { get { Build(); return rx == null ? 0 : rx.Length; } }
        public float Length => Count * step;

        /// <summary>Rebuild the runtime loop from the baked arrays (call after changing them).</summary>
        public void Rebuild() { built = false; Build(); }

        void Build()
        {
            if (built) return;
            built = true; rx = null;
            if (xs == null || xs.Length < 8 || zs == null || speeds == null || along == null || dirs == null) return;
            int n = xs.Length; bool hasSpin = spins != null && spins.Length == n;
            float[] fv = new float[n], fa = new float[n]; int[] fd = new int[n], fsp = new int[n];
            for (int i = 0; i < n; i++) { fv[i] = speeds[i]; fa[i] = along[i]; fd[i] = dirs[i]; fsp[i] = hasSpin ? spins[i] : 0; }
            float[] px = (float[])xs.Clone(), pz = (float[])zs.Clone();
            Resample(ref px, ref pz, ref fv, ref fa, ref fd, ref fsp);
            n = px.Length;
            float[] w = new float[n];
            for (int i = 0; i < n; i++) { float t = Mathf.Clamp01((Mathf.Abs(fa[i]) - TURN_A0) / (TURN_A1 - TURN_A0)); w[i] = t * t * (3f - 2f * t); }
            for (int it = 0; it < 2; it++)
            {
                float[] gx = Gauss(px, POS_SIGMA), gz = Gauss(pz, POS_SIGMA);
                for (int i = 0; i < n; i++) { px[i] += w[i] * (gx[i] - px[i]); pz[i] += w[i] * (gz[i] - pz[i]); }
            }
            float[] gv = Gauss(fv, SPEED_SIGMA);
            for (int i = 0; i < n; i++) fv[i] += w[i] * (gv[i] - fv[i]);
            Resample(ref px, ref pz, ref fv, ref fa, ref fd, ref fsp);
            rx = px; rz = pz; rv = fv; ra = fa; rd = fd; rs = hasSpin ? fsp : null;
        }

        // closed-loop re-sample to uniform `step` arc length (floats lerped, ints nearest)
        void Resample(ref float[] x, ref float[] z, ref float[] v, ref float[] a, ref int[] d, ref int[] sp)
        {
            int n = x.Length; var c = new float[n + 1];
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; c[i + 1] = c[i] + Mathf.Sqrt((x[j] - x[i]) * (x[j] - x[i]) + (z[j] - z[i]) * (z[j] - z[i])); }
            float L = c[n]; int m = Mathf.Max(8, Mathf.RoundToInt(L / step)); float h = L / m;
            float[] nx = new float[m], nz = new float[m], nv = new float[m], na = new float[m]; int[] nd = new int[m], ns = new int[m];
            int k = 0;
            for (int q = 0; q < m; q++)
            {
                float s = q * h; while (k < n - 1 && c[k + 1] <= s) k++;
                int j = (k + 1) % n; float seg = c[k + 1] - c[k]; float t = seg > 1e-6f ? (s - c[k]) / seg : 0f;
                nx[q] = Mathf.Lerp(x[k], x[j], t); nz[q] = Mathf.Lerp(z[k], z[j], t); nv[q] = Mathf.Lerp(v[k], v[j], t);
                na[q] = Mathf.Abs(a[j] - a[k]) < 20f ? Mathf.Lerp(a[k], a[j], t) : (t < 0.5f ? a[k] : a[j]);
                int nn = t < 0.5f ? k : j; nd[q] = d[nn]; ns[q] = sp[nn];
            }
            x = nx; z = nz; v = nv; a = na; d = nd; sp = ns;
        }

        static float[] Gauss(float[] src, float sigma)
        {
            int n = src.Length, r = Mathf.CeilToInt(3f * sigma); var k = new float[2 * r + 1]; float sum = 0f;
            for (int i = -r; i <= r; i++) { k[i + r] = Mathf.Exp(-0.5f * i * i / (sigma * sigma)); sum += k[i + r]; }
            var o = new float[n];
            for (int i = 0; i < n; i++) { float acc = 0f; for (int j = -r; j <= r; j++) acc += src[((i + j) % n + n) % n] * k[j + r]; o[i] = acc / sum; }
            return o;
        }

        public float Wrap(float s)
        {
            float L = Length;
            s %= L; if (s < 0) s += L;
            return s;
        }

        public Vector3 Position(float s)
        {
            s = Wrap(s) / step;
            int i = Mathf.FloorToInt(s); float t = s - i;
            int j = (i + 1) % Count;
            return new Vector3(Mathf.Lerp(rx[i], rx[j], t), 0f, Mathf.Lerp(rz[i], rz[j], t));
        }

        public Vector3 Tangent(float s)
        {
            Vector3 a = Position(s - 2f), b = Position(s + 2f);
            Vector3 d = b - a; d.y = 0;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        public float Speed(float s)
        {
            s = Wrap(s) / step;
            int i = Mathf.FloorToInt(s); float t = s - i;
            return Mathf.Lerp(rv[i], rv[(i + 1) % Count], t);
        }

        public int Dir(float s) => rd[Mathf.FloorToInt(Wrap(s) / step) % Count];

        /// <summary>Signed distance along the slalom course in the travel direction of the pass (entrance gate = -129.5).</summary>
        public float Progress(float s)
        {
            int i = Mathf.FloorToInt(Wrap(s) / step) % Count;
            return ra[i] * rd[i];
        }

        public bool InSpin(float s) => Count > 0 && rs != null && rs[Mathf.FloorToInt(Wrap(s) / step) % Count] != 0;

        /// <summary>True if a spin stretch starts within the next <paramref name="dist"/> metres of the loop.</summary>
        public bool SpinAhead(float s, float dist)
        {
            if (Count == 0 || rs == null) return false;
            for (float d = 0f; d <= dist; d += step) if (InSpin(s + d)) return true;
            return false;
        }

        public Vector3 CourseFwd(int dir) => new Vector3(courseFwdSouth.x, 0, courseFwdSouth.y) * dir;

        /// <summary>First sample where the boat runs dir and is |progress| from the course centre (used for spawning).</summary>
        public float FindS(int dir, float progress)
        {
            float best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                if (rd[i] != dir) continue;
                Vector3 p = new Vector3(rx[i], 0, rz[i]);
                Vector3 f = CourseFwd(dir);
                float lat = Vector3.Cross(f, p).y;
                if (Mathf.Abs(lat) > 6f) continue;
                float d = Mathf.Abs(ra[i] * rd[i] - progress);
                if (d < bd) { bd = d; best = i * step; }
            }
            return best;
        }
    }
}

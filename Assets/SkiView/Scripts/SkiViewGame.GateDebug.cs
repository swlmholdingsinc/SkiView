using UnityEngine;

namespace SkiView
{
    /// <summary>?gates=1 : draws the gate hit areas exactly as the scoring code judges them (same frame as CheckCourse): a green box
    /// ±GATE_ENTRY_HALF wide and ±GATE_WIN deep at each end of the course (cyan outer box: ±4.5 m for angled crossings ≥15°), the gate line itself, the 9.6 m buoy rounding lines,
    /// and a marker on the point the scoring uses for the skier (the sim position, not the boat).</summary>
    public partial class SkiViewGame
    {
        Transform gateDbgSkier;
        void BuildGateDebug()
        {
            var root = new GameObject("GateDebug").transform;
            var course = GameObject.Find("SlalomCourse"); float y = (course != null ? course.transform.position.y : 0f) + 0.06f;
            Material Mat(Color c) { var m = rope != null ? new Material(rope.sharedMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit")); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c); return m; }
            Vector3 cf = path.CourseFwd(1), rt = Vector3.Cross(Vector3.up, cf);
            Vector3 P(float x, float zTs) { float a = -(zTs + SkierSim.COURSE_LENGTH * 0.5f); return cf * a + rt * x + Vector3.up * y; }
            void Line(Color c, float w, params Vector3[] pts)
            {
                var lr = new GameObject("l").AddComponent<LineRenderer>(); lr.transform.SetParent(root, false);
                lr.sharedMaterial = Mat(c); lr.startColor = lr.endColor = c; lr.widthMultiplier = w; lr.positionCount = pts.Length; lr.SetPositions(pts); lr.loop = false;
            }
            foreach (float gz in new[] { SkierSim.GATE_Z, SkierSim.EXIT_Z })
            {
                float h = SkierSim.GATE_ENTRY_HALF, wv = SkierSim.GATE_WIN;
                Line(new Color(0.1f, 1f, 0.3f, 0.9f), 0.15f, P(-h, gz + wv), P(h, gz + wv), P(h, gz - wv), P(-h, gz - wv), P(-h, gz + wv));
                float h2 = SkierSim.GATE_ANGLED_HALF;   // dashed-looking outer box: the hit area for an angled crossing
                Line(new Color(0.1f, 0.8f, 1f, 0.8f), 0.08f, P(-h2, gz + wv), P(h2, gz + wv), P(h2, gz - wv), P(-h2, gz - wv), P(-h2, gz + wv));
                Line(new Color(1f, 1f, 1f, 0.9f), 0.08f, P(-h - 2f, gz), P(h + 2f, gz));
                Line(new Color(1f, 0.3f, 0.1f, 0.9f), 0.25f, P(-SkierSim.GATE_BALL, gz), P(SkierSim.GATE_BALL, gz));   // the drawn gate balls span
            }
            for (int i = 0; i < 6; i++)
            {
                float bz = SkierSim.BuoyZ(i), side = SkierSim.BuoyX(i) > 0 ? 1f : -1f;
                Line(new Color(1f, 0.85f, 0.1f, 0.9f), 0.12f, P(side * 9.6f, bz), P(side * 16f, bz));   // round outside here
            }
            gateDbgSkier = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; DestroyImmediate(gateDbgSkier.GetComponent<Collider>());
            gateDbgSkier.localScale = Vector3.one * 0.6f; gateDbgSkier.GetComponent<Renderer>().sharedMaterial = Mat(Color.magenta);
            gateDbgSkier.SetParent(root, true);
        }
        void TickGateDebug() { if (gateDbgSkier != null) gateDbgSkier.position = skierPos + Vector3.up * 0.3f; }
    }
}

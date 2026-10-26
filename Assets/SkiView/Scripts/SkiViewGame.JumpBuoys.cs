using UnityEngine;

namespace SkiView
{
    /// <summary>Ski View jump course from Holly's skiview_jump_course_v1.json (IWWF 10.16 / AWSA p.94), layout metres (x, z).
    /// Default 9 buoys: the four timing/end-course pairs plus the 180 m entry marker. Settings "Tournament jump buoys" shows all 13.</summary>
    public partial class SkiViewGame
    {
        public const string TOURN_JUMP_PREF = "sv.jumpbuoys.tournament";
        public static bool TournamentJumpBuoys => SkiViewArgs.Get("tjb") != null ? SkiViewArgs.On("tjb") : PlayerPrefs.GetInt(TOURN_JUMP_PREF, 0) == 1;   // b9.1e: tjb=1|0 debug override
        static readonly (string id, float x, float z, char c, bool tournamentOnly)[] JUMP_BUOYS =
        {
            ("15PG", 2.03f, -12.14f, 'G', true),
            ("19PG", -1.92f, -12.8f, 'G', true),
            ("210m", 16.83f, -9.67f, 'G', true),
            ("180m", 11.89f, 19.92f, 'R', false),
            ("150m", 6.95f, 49.51f, 'Y', true),
            ("15ST", -20.68f, 123.98f, 'R', false),
            ("19ST", -24.63f, 123.32f, 'R', false),
            ("15MT", -34.18f, 204.86f, 'Y', false),
            ("19MT", -38.13f, 204.21f, 'Y', false),
            ("15ET", -40.93f, 245.31f, 'R', false),
            ("19ET", -44.88f, 244.65f, 'R', false),
            ("15EC", -49.0f, 293.64f, 'Y', false),
            ("19EC", -52.94f, 292.98f, 'Y', false)
        };
        const float JB_OFF_X = 28.53f, JB_OFF_Z = 149.74f;
        GameObject jbRoot; GameObject[] jbTourn; int jbState = -1;

        void BuildJumpBuoys()
        {
            if (jbRoot != null || lakeId != "skiview") return;
            jbRoot = new GameObject("JumpCourse_v1"); var list = new System.Collections.Generic.List<GameObject>();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); var mesh = tmp.GetComponent<MeshFilter>().sharedMesh; var baseM = tmp.GetComponent<MeshRenderer>().sharedMaterial;
            // b9.1e: the primitive's default material has no shader in the WebGL build (pink balls); base them on the slalom buoy material
            var sc = GameObject.Find("SlalomCourse");
            if (sc != null) foreach (var mr in sc.GetComponentsInChildren<MeshRenderer>(true)) { foreach (var m in mr.sharedMaterials) if (m != null && m.name.StartsWith("BuoyTurn")) { baseM = m; break; } if (baseM != null && baseM.name.StartsWith("BuoyTurn")) break; }
            if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
            Material M(Color c) { var m = new Material(baseM) { name = "JumpBuoy" }; m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c); m.SetFloat("_Smoothness", 0.7f); return m; }
            Material red = M(new Color(0.95f, 0.12f, 0.1f)), yel = M(new Color(1f, 0.85f, 0.1f)), grn = M(new Color(0.2f, 0.85f, 0.3f));
            float y = 0f;
            foreach (var b in JUMP_BUOYS)
            {
                var go = new GameObject("JB_" + b.id); go.transform.SetParent(jbRoot.transform, false);
                go.transform.position = new Vector3(b.x + JB_OFF_X, y + 0.025f, JB_OFF_Z - b.z);   // layout (+y south) -> scene (+z north); checked: ramp top edge (-17.74,197.47) -> (10.79,-47.73), 15MT/ST/ET/EC land on the builder's balls go.transform.localScale = Vector3.one * 0.23f;   // 23 cm, ~9 cm freeboard like the slalom balls
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.allowOcclusionWhenDynamic = false;
                r.sharedMaterial = b.c == 'R' ? red : b.c == 'Y' ? yel : grn;
                if (b.tournamentOnly) list.Add(go);
            }
            jbTourn = list.ToArray(); jbState = -1;
        }

        /// <summary>Per frame: slalom course hidden whenever the jump gear is on (Holly: 26 + 13 = 39 buoys before), jump buoys per the setting.</summary>
        void TickJumpBuoys()
        {
            if (lakeId != "skiview") return;
            if (JumpGearOn) BuildJumpBuoys(); if (jbRoot == null) return;
            if (jumpCourse != null && JumpGearOn && jumpCourse.activeSelf) jumpCourse.SetActive(false);   // the old builder-merged 13-ball course
            int st = !JumpGearOn ? 0 : TournamentJumpBuoys ? 2 : 1; if (st == jbState) return; jbState = st;
            jbRoot.SetActive(st > 0); foreach (var g in jbTourn) g.SetActive(st == 2);
        }
    }
}

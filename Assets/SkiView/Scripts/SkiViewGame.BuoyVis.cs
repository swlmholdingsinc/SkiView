using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>Buoys (Scott 5:54: plain, realistic buoys; nothing that looks like UI).
    /// Turn and gate balls: saturated tournament orange, lit PBR (URP Lit clone of the builder's BuoyTurn material), slightly glossy,
    /// a touch brighter than the original. Guide and pre-gate balls keep their original builder materials and colours.
    /// Only help: on the phone tier, turn and gate balls are drawn 1.25x (same XZ, same freeboard ratio; scoring uses the real 23 cm
    /// positions), and every course ball is its own renderer outside any LOD group and never occlusion-culled, so far balls don't vanish.</summary>
    public partial class SkiViewGame
    {
        class Vis { public Transform t; public Vector3 basePos, baseScale; public float wl; public bool big; }
        List<Vis> buoyVis; bool buoyVisTried; float buoyVisScale = -1f; LakeDetail lakeDetail; bool slalomHidden; GameObject slalomCourse;
        public static bool BuoyVisOff; public static float BuoyScreenH = -1f;   // capture hooks (BuoyScreenH no longer used for sizing)
        static readonly Color BUOY_ORANGE = new Color(1f, 0.40f, 0.02f);
        public const float PHONE_BUOY_SCALE = 1.25f;

        void InitBuoyVis()
        {
            buoyVisTried = true;
            var course = GameObject.Find("SlalomCourse"); if (course == null) return;
            // the builder merges the course balls into one mesh per material (MergeRig); the named ball transforms stay as markers.
            // Draw each ball as its own sphere (so it can carry its own scale and sits in no LOD group) with the original materials.
            Material orig(string n) { foreach (var mr in course.GetComponentsInChildren<MeshRenderer>(true)) foreach (var m in mr.sharedMaterials) if (m != null && m.name.StartsWith(n)) return m; return null; }
            Material turnO = orig("BuoyTurn"), gateO = orig("BuoyGate"), guideM = orig("BuoyGuide"), preM = orig("BuoyPreGate");
            if (turnO == null) { Debug.LogWarning("[SkiView] buoys: BuoyTurn material not found, leaving the builder's course as is"); return; }
            var orange = new Material(turnO) { name = "BuoyTournamentOrange" };
            orange.SetColor("_BaseColor", BUOY_ORANGE); if (orange.HasProperty("_Color")) orange.SetColor("_Color", BUOY_ORANGE);
            orange.SetFloat("_Smoothness", 0.72f); if (orange.HasProperty("_Metallic")) orange.SetFloat("_Metallic", 0f);
            if (guideM == null) guideM = turnO; if (preM == null) preM = turnO;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); var sphere = tmp.GetComponent<MeshFilter>().sharedMesh; if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
            var balls = new List<Transform>(course.GetComponentsInChildren<Transform>(true));
            buoyVis = new List<Vis>();
            foreach (Transform t in balls)
            {
                bool turn = t.name.StartsWith("Turn"), gate = t.name.StartsWith("Gate"), guide = t.name.StartsWith("Guide"), pre = t.name.StartsWith("PreGate");
                if (!turn && !gate && !guide && !pre) continue;
                if (turn && t.name.Length > 5) continue;
                float d0 = Mathf.Abs(t.lossyScale.x); if (d0 < 0.15f || d0 > 0.35f) d0 = 0.23f;   // b9: a marker without its 23 cm scale (stale lake scenes) drew a 1 m ball
                var go = new GameObject(t.name + "_vis"); go.layer = t.gameObject.layer; go.transform.SetParent(course.transform, true);
                go.transform.position = t.position; go.transform.localScale = Vector3.one * d0;
                go.AddComponent<MeshFilter>().sharedMesh = sphere;
                var r = go.AddComponent<MeshRenderer>(); r.allowOcclusionWhenDynamic = false;
                r.sharedMaterial = (turn || gate) ? orange : guide ? guideM : preM;
                buoyVis.Add(new Vis { t = go.transform, basePos = t.position, baseScale = Vector3.one * d0, big = turn || gate, wl = t.position.y + d0 * 0.5f - (turn ? 0.09f : 0.115f) });
            }
            foreach (var mr in course.GetComponentsInChildren<MeshRenderer>(true)) if (!mr.name.EndsWith("_vis")) mr.enabled = false;
        }

        void TickBuoyVis()
        {
            if (BuoyVisOff) return; if (!buoyVisTried) InitBuoyVis();
            // Scott 9:31 / Holly: SetJumpGear never hid SlalomCourse - hide every slalom buoy, gate and guide whenever the jump gear is on
            bool hide = JumpGearOn || TrickGearOn; TickJumpBuoys();
            if (hide != slalomHidden) { if (slalomCourse == null) slalomCourse = GameObject.Find("SlalomCourse"); if (slalomCourse != null) { slalomCourse.SetActive(!hide); slalomHidden = hide; } }
            if (buoyVis == null) return;
            var det = lakeDetail != null ? lakeDetail : (lakeDetail = FindFirstObjectByType<LakeDetail>());
            float s = (det != null ? det.Tier : LakeDetail.AutoTier) >= 2 ? PHONE_BUOY_SCALE : 1f;
            if (Mathf.Approximately(s, buoyVisScale)) return; buoyVisScale = s;
            foreach (var v in buoyVis)
            {
                float k = v.big ? s : 1f;
                v.t.localScale = v.baseScale * k;
                v.t.position = new Vector3(v.basePos.x, v.wl + (v.basePos.y - v.wl) * k, v.basePos.z);
            }
        }
    }
}

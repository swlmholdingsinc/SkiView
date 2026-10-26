using UnityEngine;
namespace SkiView
{
    public partial class SkiViewGame
    {
        // b9.1e: debug overhead camera (URL arg cam=top). Frames the jump buoys + ramp in jump mode, else the skier area; logs the visible jump buoy count once.
        bool topLogged;
        bool DebugTopCam()
        {
            if (cam == null || SkiViewArgs.Get("cam") != "top") return false;
            Bounds b = new Bounds(skierPos, Vector3.one * 60f); int n = 0;
            if (jbRoot != null && jbRoot.activeInHierarchy)
                foreach (var r in jbRoot.GetComponentsInChildren<Renderer>()) { if (n == 0) b = r.bounds; else b.Encapsulate(r.bounds); n++; }
            if (n > 0 && jumpRamp != null) b.Encapsulate(jumpRamp.transform.position);
            float span = Mathf.Max(b.size.x, b.size.z) * 0.6f + 15f;
            cam.fieldOfView = 50f; cam.farClipPlane = Mathf.Max(cam.farClipPlane, span * 4f);
            float h = span / Mathf.Tan(25f * Mathf.Deg2Rad);
            cam.transform.SetPositionAndRotation(new Vector3(b.center.x, h, b.center.z), Quaternion.Euler(90f, 0f, 0f));
            if (!topLogged && n > 0) { topLogged = true; foreach (Transform t in jbRoot.transform) t.localScale = Vector3.one * 1.6f;   // debug only: 23 cm balls are sub-pixel from overhead
                 Debug.Log($"[SkiView] topcam: jump buoy renderers={n} visibleRoots={CountJumpBuoys()} span={span:0}m"); }
            return true;
        }
        int CountJumpBuoys() { int k = 0; if (jbRoot != null) foreach (Transform t in jbRoot.transform) if (t.gameObject.activeInHierarchy) k++; return k; }
    }
}

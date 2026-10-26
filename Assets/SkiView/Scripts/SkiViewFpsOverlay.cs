using UnityEngine;

namespace SkiView
{
    /// <summary>Test builds: tiny frame-time readout for real-device numbers. On with ?fps=1 in the URL, or toggle anytime with a
    /// three-finger tap (touch) or F3 (keyboard). Shows fps, mean / p95 frame ms over the last 2 s, detail tier and screen size.
    /// Cost when off: one Update with an early-out.</summary>
    public class SkiViewFpsOverlay : MonoBehaviour
    {
        static SkiViewFpsOverlay inst;
        readonly float[] buf = new float[240]; int n, head; bool on; float acc; string text = "";
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst != null) return;
            var go = new GameObject("SV_FpsOverlay"); DontDestroyOnLoad(go); go.hideFlags = HideFlags.HideInHierarchy;
            inst = go.AddComponent<SkiViewFpsOverlay>(); inst.on = SkiViewArgs.Get("fps") == "1";
        }

        void Update()
        {
            bool tog = Input.touchCount == 3 && Input.GetTouch(2).phase == TouchPhase.Began;
            if (Input.GetKeyDown(KeyCode.F3)) tog = true;
            if (tog) { on = !on; n = 0; }
            if (!on) return;
            float dt = Time.unscaledDeltaTime * 1000f; buf[head] = dt; head = (head + 1) % buf.Length; if (n < buf.Length) n++;
            acc += Time.unscaledDeltaTime; if (acc < 0.5f) return; acc = 0f;
            float sum = 0f, total = 0f; int cnt = 0; var tmp = new float[n];
            for (int i = 0; i < n && total < 2000f; i++) { float v = buf[(head - 1 - i + buf.Length) % buf.Length]; tmp[cnt++] = v; sum += v; total += v; }
            System.Array.Sort(tmp, 0, cnt); float mean = sum / Mathf.Max(1, cnt), p95 = cnt > 0 ? tmp[Mathf.Min(cnt - 1, (int)(cnt * 0.95f))] : 0f;
            text = $"{1000f / Mathf.Max(0.01f, mean):0} fps  {mean:0.0} ms  p95 {p95:0.0}  T{LakeDetail.AutoTier}  {Screen.width}x{Screen.height}";
        }

        void OnGUI()
        {
            if (!on) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 40), normal = { textColor = Color.white } };
            var r = new Rect(Screen.width * 0.5f - Screen.width * 0.2f, 4, Screen.width * 0.4f, style.fontSize * 1.6f);
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 6, r.y + 2, r.width, r.height), text, style);
        }
    }
}

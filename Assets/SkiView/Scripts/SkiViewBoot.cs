using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;

namespace SkiView
{
    /// <summary>Pass 16: per-lake content split. The player build holds only this boot scene + code; every lake scene (home
    /// included) is an Addressables scene. Ski View + the shared kit ship with the app (local groups; Android install-time),
    /// each other lake is its own bundle group (WebGL: separate bundle fetched on demand; Android AAB: on-demand Play Asset
    /// Delivery pack). Start a lake directly with index.html?lake=&lt;id&gt; (WebGL) or -lake &lt;id&gt; (command line).</summary>
    public class SkiViewBoot : MonoBehaviour
    {
        public string homeScene = "SkiViewLake";
        static SkiViewBoot inst;
        string status = "Loading..."; float progress; bool busy;
        // pass 19 (Julia 16 / 16b-d): state read by SkiViewLoadingUI
        public static string LoadingKey { get; private set; }
        public static float Progress => inst != null ? inst.progress : 0f;
        public static long DlGot { get; private set; }
        public static long DlTotal { get; private set; }
        public static bool DlFailed { get; private set; }
        public static bool Downloading { get; private set; }
        public static bool Offline => Application.internetReachability == NetworkReachability.NotReachable;
        static bool cancelDl;
        public static void CancelLoad() { cancelDl = true; }
        public static void ClearFailed() { DlFailed = false; LoadingKey = null; }

        public static bool Active => inst != null;

        void Awake()
        {
            if (inst != null) { Destroy(gameObject); return; }
            inst = this; DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<SkiView.UI.SkiViewLoadingUI>();   // pass 19: Julia's loading / pack screens (replaces the IMGUI bar)
            // pass 17: landscape either way round (matches PlayerSettings; also set at runtime in case a device starts in portrait)
            Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        IEnumerator Start()
        {
            var init = Addressables.InitializeAsync(); yield return init;
            string lake = StartLakeArg();
            yield return LoadRoutine(string.IsNullOrEmpty(lake) || lake == "skiview" ? homeScene : "SkiViewLake_" + lake);
        }

        // ---------- pass 19: lake packs for the lake-select screen (Julia's 03 / 03b / 03c / 03d) ----------
        public class Pack { public string key; public long size = -1; public float progress; public long got; public bool sizing, downloading, failed; public AsyncOperationHandle handle; }
        static readonly System.Collections.Generic.Dictionary<string, Pack> packs = new System.Collections.Generic.Dictionary<string, Pack>();
        public static string SceneKey(string lakeId) => string.IsNullOrEmpty(lakeId) || lakeId == "skiview" ? (inst != null ? inst.homeScene : "SkiViewLake") : "SkiViewLake_" + lakeId;
        /// <summary>Pack state for a lake (size 0 = installed / included; -1 = still asking). Starts the size query on first call.</summary>
        public static Pack PackInfo(string lakeId)
        {
            string key = SceneKey(lakeId);
            if (!packs.TryGetValue(key, out var p)) { p = new Pack { key = key }; packs[key] = p; }
            if (p.size < 0 && !p.sizing && inst != null) { p.sizing = true; inst.StartCoroutine(inst.SizeRoutine(p)); }
            return p;
        }
        IEnumerator SizeRoutine(Pack p)
        {
            var sz = Addressables.GetDownloadSizeAsync(p.key); yield return sz;
            p.size = sz.Status == AsyncOperationStatus.Succeeded ? sz.Result : 0; Addressables.Release(sz); p.sizing = false;
        }
        public static void Download(string lakeId)
        {
            var p = PackInfo(lakeId); if (p.downloading || inst == null) return;
            p.failed = false; p.downloading = true; p.progress = 0f; inst.StartCoroutine(inst.DownloadRoutine(p));
        }
        public static void CancelDownload(string lakeId)
        {
            var p = PackInfo(lakeId); if (!p.downloading) return;
            p.downloading = false; p.progress = 0f; if (p.handle.IsValid()) Addressables.Release(p.handle);
        }
        IEnumerator DownloadRoutine(Pack p)
        {
            p.handle = Addressables.DownloadDependenciesAsync(p.key, false);
            while (p.handle.IsValid() && !p.handle.IsDone && p.downloading) { var ds = p.handle.GetDownloadStatus(); p.progress = ds.Percent; p.got = ds.DownloadedBytes; yield return null; }
            if (!p.downloading) yield break;   // cancelled
            bool ok = p.handle.IsValid() && p.handle.Status == AsyncOperationStatus.Succeeded;
            if (p.handle.IsValid()) Addressables.Release(p.handle);
            p.downloading = false; p.failed = !ok; if (ok) { p.size = 0; p.progress = 1f; }
            Debug.Log($"[SkiView] pack {p.key}: download {(ok ? "done" : "FAILED")}");
        }
        public static bool Busy => inst != null && inst.busy;

        static string StartLakeArg()
        {
            var lk = SkiViewArgs.Get("lake"); if (!string.IsNullOrEmpty(lk)) return lk;   // pass 19: shared URL / command-line parser
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-lake") return a[i + 1];
            return null;
        }

        /// <summary>Load a lake scene by its address (= scene name). Called by the HUD LAKE button.</summary>
        public static bool Load(string sceneName)
        {
            if (inst == null || inst.busy) return false;
            inst.StartCoroutine(inst.LoadRoutine(sceneName)); return true;
        }

        IEnumerator LoadRoutine(string key)
        {
            busy = true; progress = 0; status = key == homeScene ? "Loading Ski View" : "Loading lake";
            float t0 = Time.realtimeSinceStartup;
            string from = "";
            var locs = Addressables.LoadResourceLocationsAsync(key); yield return locs;
            if (locs.Status == AsyncOperationStatus.Succeeded && locs.Result.Count > 0)
            {
                var l = locs.Result[0];
                from = string.Join(" + ", (l.Dependencies ?? new System.Collections.Generic.List<IResourceLocation>()).Select(d => System.IO.Path.GetFileName(d.InternalId)).Take(4));
            }
            Addressables.Release(locs);
            var sz = Addressables.GetDownloadSizeAsync(key); yield return sz;
            long bytes = sz.Status == AsyncOperationStatus.Succeeded ? sz.Result : 0; Addressables.Release(sz);
            Debug.Log($"[SkiView] lake load start {key}: bundles {from}; download {bytes / 1048576f:0.0} MB");
            LoadingKey = key; DlFailed = false; DlGot = 0; DlTotal = bytes; cancelDl = false;
            if (bytes > 0)
            {   // pass 19: fetch the pack first (cancellable, its own screen), then load the scene from the cache
                if (Offline) { DlFailed = true; busy = false; Debug.Log($"[SkiView] pack {key}: offline"); yield break; }
                Downloading = true;
                var dl = Addressables.DownloadDependenciesAsync(key, false);
                while (!dl.IsDone && !cancelDl) { var ds0 = dl.GetDownloadStatus(); DlGot = ds0.DownloadedBytes; if (ds0.TotalBytes > 0) DlTotal = ds0.TotalBytes; progress = ds0.Percent; yield return null; }
                bool ok = !cancelDl && dl.Status == AsyncOperationStatus.Succeeded; Addressables.Release(dl); Downloading = false;
                if (cancelDl) { busy = false; LoadingKey = null; Debug.Log($"[SkiView] pack {key}: cancelled"); yield break; }
                if (!ok) { DlFailed = true; busy = false; Debug.Log($"[SkiView] pack {key}: download FAILED"); yield break; }
                var pk = packs.Values.FirstOrDefault(x => x.key == key); if (pk != null) pk.size = 0;
            }
            var h = Addressables.LoadSceneAsync(key, LoadSceneMode.Single);
            while (!h.IsDone)
            {
                progress = h.PercentComplete;
                var ds = h.GetDownloadStatus();
                if (ds.TotalBytes > 0) status = $"Downloading lake  {ds.DownloadedBytes / 1048576f:0.0} / {ds.TotalBytes / 1048576f:0.0} MB";
                yield return null;
            }
            if (h.Status == AsyncOperationStatus.Succeeded)
                Debug.Log($"[SkiView] LAKE READY {key} (Addressables) in {Time.realtimeSinceStartup - t0:0.0}s from {from}");
            else Debug.LogError($"[SkiView] lake load FAILED {key}: {h.OperationException}");
            busy = false; LoadingKey = null;
        }

        void OnGUI()
        {
            if (!busy || SkiView.UI.SkiViewLoadingUI.Ready) return;   // pass 19: the kit loading screen draws instead
            float w = Screen.width, hh = Screen.height;
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(0, 0, w, hh), Texture2D.whiteTexture); GUI.color = Color.white;
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(hh / 28f), alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, hh * 0.42f, w, hh * 0.08f), status, st);
            GUI.DrawTexture(new Rect(w * 0.3f, hh * 0.52f, w * 0.4f * Mathf.Clamp01(progress), 6), Texture2D.whiteTexture);
        }
    }
}

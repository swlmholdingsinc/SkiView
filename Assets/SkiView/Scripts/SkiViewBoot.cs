using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
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
        float phaseSince; bool stallMarked; Camera bootCam;
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
        /// <summary>Last boot step. Stays on screen until the lake scene is ready and no fault is set.</summary>
        public static string Phase { get; private set; } = "awake";
        public static string Fault { get; private set; }

        public static string BootLine(string phase, string scene, bool busy, int width, int height, string fault)
        {
            string line = "[BOOT] phase=" + phase + " scene=" + (string.IsNullOrEmpty(scene) ? "(none)" : scene)
                + " busy=" + (busy ? "1" : "0") + " screen=" + width + "x" + height;
            if (!string.IsNullOrEmpty(fault)) line += "\n" + fault;
            return line;
        }

        public static void Note(string phase, string fault = null) { SetPhase(phase, fault); }

        static void SetPhase(string phase, string fault = null)
        {
            Phase = phase;
            if (inst != null) inst.phaseSince = Time.realtimeSinceStartup;
            if (fault != null) Fault = fault;
            string line = BootLine(phase, SceneManager.GetActiveScene().name, inst != null && inst.busy, Screen.width, Screen.height, Fault);
            if (string.IsNullOrEmpty(fault)) Debug.Log("[SkiView] " + line);
            else Debug.LogError("[SkiView] " + line);
            Page(line);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SkiViewBootMark(string message);
        static void Page(string message) { try { SkiViewBootMark(message); } catch (Exception) { } }
#else
        static void Page(string message) { if (message == null) return; }
#endif

        /// <summary>
        /// Parameterless InitializeAsync releases its handle before a coroutine resumes, so IsValid is false after a successful init.
        /// Call InitializeAsync(false), then this. A real failure stays failed.
        /// </summary>
        public static bool InitAllowsLoad(bool handleValid, bool succeeded, string exceptionMessage, out string fault)
        {
            if (!handleValid)
            {
                fault = "invalid handle. InitializeAsync was released before this step read it. No catalog request is proof that this step did not run.";
                return false;
            }
            if (!succeeded)
            {
                fault = string.IsNullOrEmpty(exceptionMessage) ? "init failed" : exceptionMessage;
                return false;
            }
            fault = null;
            return true;
        }

        void Awake()
        {
            if (inst != null) { Destroy(gameObject); return; }
            inst = this; DontDestroyOnLoad(gameObject);
            phaseSince = Time.realtimeSinceStartup;
            EnsureBootCamera();
            SetPhase("awake");
            try { gameObject.AddComponent<SkiView.UI.SkiViewLoadingUI>(); }
            catch (Exception ex) { SetPhase("loading-ui-threw", ex.GetType().Name + ": " + ex.Message); }
            if (Application.isMobilePlatform) { Application.targetFrameRate = 60; Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false; Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true; Screen.orientation = ScreenOrientation.AutoRotation; }
        }

        void EnsureBootCamera()
        {
            if (bootCam != null) return;
            var found = Camera.main;
            if (found == null)
            {
                var go = new GameObject("BootCamera");
                go.transform.SetParent(transform, false);
                bootCam = go.AddComponent<Camera>();
                bootCam.clearFlags = CameraClearFlags.SolidColor;
                bootCam.backgroundColor = new Color(0.05f, 0.07f, 0.09f);
                bootCam.depth = -100f;
                go.tag = "MainCamera";
            }
            else bootCam = found;
        }

        void Update()
        {
            if (stallMarked) return;
            if (!SkiViewBootContract.Stalled(Phase, Time.realtimeSinceStartup - phaseSince)) return;
            stallMarked = true;
            Fault = SkiViewBootContract.StallFault(Phase);
            Debug.LogError("[SkiView] " + Fault);
            Page(BootLine(Phase, SceneManager.GetActiveScene().name, busy, Screen.width, Screen.height, Fault));
        }

        IEnumerator Start()
        {
            var body = BootBody();
            while (true)
            {
                bool more;
                try { more = body.MoveNext(); }
                catch (Exception ex)
                {
                    SetPhase("boot-threw", ex.GetType().Name + ": " + ex.Message);
                    yield break;
                }
                if (!more) yield break;
                yield return body.Current;
            }
        }

        IEnumerator BootBody()
        {
            SetPhase("addressables-init");
            var init = Addressables.InitializeAsync(false);
            yield return init;
            bool valid = init.IsValid();
            bool succeeded = valid && init.Status == AsyncOperationStatus.Succeeded;
            string exceptionMessage = valid && init.OperationException != null ? init.OperationException.Message : null;
            if (valid) Addressables.Release(init);
            string fault;
            if (!InitAllowsLoad(valid, succeeded, exceptionMessage, out fault))
            {
                SetPhase("init-failed", fault);
                yield break;
            }
            SetPhase("init-ok");
            string lake = StartLakeArg();
            SetPhase("load-begin lake=" + (string.IsNullOrEmpty(lake) ? "(home)" : lake));
            yield return LoadRoutine(string.IsNullOrEmpty(lake) || lake == "skiview" ? homeScene : "SkiViewLake_" + lake);
        }

        public class Pack { public string key; public long size = -1; public float progress; public long got; public bool sizing, downloading, failed; public AsyncOperationHandle handle; }
        static readonly System.Collections.Generic.Dictionary<string, Pack> packs = new System.Collections.Generic.Dictionary<string, Pack>();
        public static string SceneKey(string lakeId) => string.IsNullOrEmpty(lakeId) || lakeId == "skiview" ? (inst != null ? inst.homeScene : "SkiViewLake") : "SkiViewLake_" + lakeId;
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
            if (!p.downloading) yield break;
            bool ok = p.handle.IsValid() && p.handle.Status == AsyncOperationStatus.Succeeded;
            if (p.handle.IsValid()) Addressables.Release(p.handle);
            p.downloading = false; p.failed = !ok; if (ok) { p.size = 0; p.progress = 1f; }
            Debug.Log($"[SkiView] pack {p.key}: download {(ok ? "done" : "FAILED")}");
        }
        public static bool Busy => inst != null && inst.busy;

        static string StartLakeArg()
        {
            var lk = SkiViewArgs.Get("lake"); if (!string.IsNullOrEmpty(lk)) return lk;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-lake") return a[i + 1];
            return null;
        }

        public static bool Load(string sceneName)
        {
            if (inst == null || inst.busy) return false;
            inst.StartCoroutine(inst.LoadRoutine(sceneName)); return true;
        }

        IEnumerator LoadRoutine(string key)
        {
            busy = true; progress = 0; status = key == homeScene ? "Loading Ski View" : "Loading lake";
            SetPhase("locations " + key);
            float t0 = Time.realtimeSinceStartup;
            string from = "";
            var locs = Addressables.LoadResourceLocationsAsync(key); yield return locs;
            if (!locs.IsValid() || locs.Status != AsyncOperationStatus.Succeeded)
                SetPhase("locations-failed " + key, locs.IsValid() && locs.OperationException != null ? locs.OperationException.Message : "invalid handle");
            else if (locs.Result.Count == 0)
                SetPhase("locations-empty " + key, "no resource location");
            if (locs.IsValid() && locs.Status == AsyncOperationStatus.Succeeded && locs.Result.Count > 0)
            {
                var l = locs.Result[0];
                from = string.Join(" + ", (l.Dependencies ?? new System.Collections.Generic.List<IResourceLocation>()).Select(d => System.IO.Path.GetFileName(d.InternalId)).Take(4));
            }
            Addressables.Release(locs);
            SetPhase("size " + key);
            var sz = Addressables.GetDownloadSizeAsync(key); yield return sz;
            long bytes = sz.Status == AsyncOperationStatus.Succeeded ? sz.Result : 0; Addressables.Release(sz);
            Debug.Log($"[SkiView] lake load start {key}: bundles {from}; download {bytes / 1048576f:0.0} MB");
            LoadingKey = key; DlFailed = false; DlGot = 0; DlTotal = bytes; cancelDl = false;
            SetPhase("bytes " + key + " " + bytes);
            if (bytes > 0)
            {
                if (Offline) { DlFailed = true; busy = false; SetPhase("offline " + key, "internet not reachable"); yield break; }
                Downloading = true;
                var dl = Addressables.DownloadDependenciesAsync(key, false);
                while (!dl.IsDone && !cancelDl) { var ds0 = dl.GetDownloadStatus(); DlGot = ds0.DownloadedBytes; if (ds0.TotalBytes > 0) DlTotal = ds0.TotalBytes; progress = ds0.Percent; yield return null; }
                bool ok = !cancelDl && dl.Status == AsyncOperationStatus.Succeeded; Addressables.Release(dl); Downloading = false;
                if (cancelDl) { busy = false; LoadingKey = null; SetPhase("download-cancelled " + key); yield break; }
                if (!ok) { DlFailed = true; busy = false; SetPhase("download-failed " + key, "dependency download failed"); yield break; }
                var pk = packs.Values.FirstOrDefault(x => x.key == key); if (pk != null) pk.size = 0;
            }
            SetPhase("load-scene " + key);
            var h = Addressables.LoadSceneAsync(key, LoadSceneMode.Single);
            while (!h.IsDone)
            {
                progress = h.PercentComplete;
                var ds = h.GetDownloadStatus();
                if (ds.TotalBytes > 0) status = $"Downloading lake  {ds.DownloadedBytes / 1048576f:0.0} / {ds.TotalBytes / 1048576f:0.0} MB";
                yield return null;
            }
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                SetPhase("lake-ready " + key);
                Debug.Log($"[SkiView] LAKE READY {key} (Addressables) in {Time.realtimeSinceStartup - t0:0.0}s from {from}");
            }
            else
            {
                string why = h.OperationException != null ? h.OperationException.ToString() : "load scene failed";
                SetPhase("load-failed " + key, why);
            }
            busy = false; LoadingKey = null;
        }

        void OnGUI()
        {
            var scene = SceneManager.GetActiveScene();
            bool ready = Phase != null && Phase.IndexOf("lake-ready", StringComparison.Ordinal) >= 0 && string.IsNullOrEmpty(Fault);
            if (!ready)
            {
                float boxW = Mathf.Max(320f, Screen.width * 0.72f);
                GUI.color = new Color(0f, 0f, 0f, 0.82f);
                GUI.DrawTexture(new Rect(12f, 12f, boxW, 150f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var line = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, normal = { textColor = Color.white } };
                GUI.Label(new Rect(24f, 20f, boxW - 24f, 134f), BootLine(Phase, scene.name, busy, Screen.width, Screen.height, Fault), line);
            }
            if (!busy || SkiView.UI.SkiViewLoadingUI.Ready) return;
            float sw = Screen.width, hh = Screen.height;
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(0, 0, sw, hh), Texture2D.whiteTexture); GUI.color = Color.white;
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(hh / 28f), alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, hh * 0.42f, sw, hh * 0.08f), status, st);
            GUI.DrawTexture(new Rect(sw * 0.3f, hh * 0.52f, sw * 0.4f * Mathf.Clamp01(progress), 6), Texture2D.whiteTexture);
        }
    }
}

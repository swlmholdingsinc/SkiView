using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkiView
{
    /// <summary>
    /// Opt-in collar QA. Created only when the URL asks for it, so a normal run never replaces materials or poses.
    /// It names the renderer and submesh that are actually enabled, and can show them unlit, as normals, or as tangents.
    /// </summary>
    public class SkiViewVestQa : MonoBehaviour
    {
        struct Live
        {
            public SkiViewVestQaLogic.Entry entry;
            public Renderer renderer;
        }

        class Snap
        {
            public Material[] materials;
            public bool enabled;
            public string materialNames;
        }

        readonly List<Live> live = new List<Live>();
        readonly Dictionary<int, Snap> snaps = new Dictionary<int, Snap>();
        readonly List<Material> spawned = new List<Material>();
        string appliedKey = "";
        string vestKey = "";
        float nextScan;
        bool framed;
        bool seeded;
        string shade = "lit";
        bool isolate;
        int isolateInst;
        bool scopeAll;
        float poseRoll = float.NaN;
        bool faceCam;
        int selectedInst;
        bool showOverlap;
        Vector2 scroll;
        string state = "init";
        string scanError = "";
        static SkiViewVestQa inst;
        static string loadedScene = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            var active = SceneManager.GetActiveScene();
            if (active.IsValid()) loadedScene = active.name;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            loadedScene = scene.name;
            if (inst != null) inst.Rebind(scene.name);
            Ensure();
        }

        static void Ensure()
        {
            if (!Application.isPlaying) return;
            var args = ReadArgs();
            if (!SkiViewVestQaLogic.Requested(Arg(args, "vestqa"), Arg(args, "vestshade"), Arg(args, "pose"), Arg(args, "vestid"), Arg(args, "vestsub")))
                return;
            if (inst != null) return;
            var go = new GameObject("SkiViewVestQa");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SkiViewVestQa>();
            Debug.Log("[VESTQA] persistent host scene=" + loadedScene);
        }

        void Awake()
        {
            if (inst != null && inst != this) { Destroy(gameObject); return; }
            inst = this;
            state = "awake";
        }

        void Rebind(string sceneName)
        {
            RestoreAll();
            live.Clear();
            snaps.Clear();
            appliedKey = "";
            vestKey = "";
            nextScan = 0f;
            framed = false;
            scanError = "";
            state = "rebind " + sceneName;
        }

        static Dictionary<string, string> ReadArgs()
        {
            var fromUrl = SkiViewVestQaLogic.ArgsFromUrl(Application.absoluteURL ?? "");
            foreach (var key in new[] { "vestqa", "vestshade", "pose", "vestid", "vestsub", "vestscope", "cam", "skier" })
            {
                string v = SkiViewArgs.Get(key);
                if (v != null) fromUrl[key] = v;
            }
            return fromUrl;
        }

        static string Arg(Dictionary<string, string> args, string key)
        {
            return args.TryGetValue(key, out var v) ? v : null;
        }

        void OnDestroy()
        {
            for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) Destroy(spawned[i]);
            spawned.Clear();
            if (inst == this) inst = null;
        }

        void LateUpdate()
        {
            var args = ReadArgs();
            Seed(args);
            var game = FindFirstObjectByType<SkiViewGame>();
            if (game == null) { state = "waiting for game"; return; }
            if (game.skier == null) { state = "waiting for skier"; return; }
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.5f; Scan(game); }
            ApplyPose(game);
            ApplyShade();
        }

        void Seed(Dictionary<string, string> args)
        {
            if (seeded) return;
            seeded = true;
            shade = SkiViewVestQaLogic.Shade(Arg(args, "vestshade"));
            poseRoll = SkiViewVestQaLogic.PoseRollDegrees(Arg(args, "pose"));
            scopeAll = Arg(args, "vestscope") == "all" || Arg(args, "vestqa") == "all";
            string camArg = Arg(args, "cam");
            faceCam = camArg != null && camArg.Trim().ToLowerInvariant() == "face";
        }

        void Scan(SkiViewGame game)
        {
            try
            {
                ScanBody(game);
                scanError = "";
                state = "scanned";
            }
            catch (Exception ex)
            {
                scanError = ex.GetType().Name + ": " + ex.Message;
                state = "scan error";
                Debug.LogError("[VESTQA] " + scanError);
            }
        }

        static void AddRoot(Transform root, List<Renderer> found, HashSet<int> seen)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !seen.Add(r.GetInstanceID())) continue;
                found.Add(r);
            }
        }

        void ScanBody(SkiViewGame game)
        {
            var found = new List<Renderer>();
            var seen = new HashSet<int>();
            AddRoot(game.skier, found, seen);
            if (game.rig != null) AddRoot(game.rig.transform, found, seen);
            if (game.rig != null && game.rig.animator != null) AddRoot(game.rig.animator.transform, found, seen);

            Transform chest = Chest(game);
            var raw = new List<SkiViewVestQaLogic.Entry>();
            var renderers = new List<Renderer>();
            foreach (var r in found)
            {
                int id = r.GetInstanceID();
                Remember(id, r);
                Snap snap;
                snaps.TryGetValue(id, out snap);
                string mats = snap != null ? snap.materialNames : GameplayMatNames(r);
                var mesh = MeshOf(r);
                string role = SkiViewVestQaLogic.Role(r.name, mesh != null ? mesh.name : "", mats);
                bool overlap = role == "other" && OverlapsCollar(r, chest);
                if (role != "vest" && !overlap) continue;
                if (overlap && role == "other") role = "overlap";
                int tris = -1;
                int subs = 1;
                bool readable = false;
                if (mesh != null)
                {
                    subs = Mathf.Max(1, mesh.subMeshCount);
                    readable = mesh.isReadable;
                    if (readable)
                    {
                        tris = 0;
                        for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)mesh.GetIndexCount(s) / 3;
                    }
                }
                var lod = r.GetComponentInParent<LODGroup>();
                raw.Add(new SkiViewVestQaLogic.Entry
                {
                    id = id,
                    role = role,
                    path = PathOf(r.transform) + "/" + r.GetType().Name,
                    mesh = mesh != null ? mesh.name : "",
                    submeshes = subs,
                    materials = mats,
                    triangles = tris,
                    enabled = r.enabled,
                    active = r.gameObject.activeInHierarchy,
                    visible = r.isVisible,
                    readable = readable,
                    haveBaseline = snap != null,
                    baselineEnabled = snap != null && snap.enabled,
                    lod = lod == null ? "none" : (lod.enabled ? "on" : "off")
                });
                renderers.Add(r);
            }
            var ordered = SkiViewVestQaLogic.OrderForDisplay(raw);
            var byId = new Dictionary<int, Renderer>();
            for (int i = 0; i < raw.Count; i++) byId[raw[i].id] = renderers[i];
            live.Clear();
            var vestIds = new StringBuilder();
            int overlaps = 0;
            foreach (var e in ordered)
            {
                Renderer rr;
                byId.TryGetValue(e.id, out rr);
                live.Add(new Live { entry = e, renderer = rr });
                if (e.role == "vest") vestIds.Append(e.id).Append(',');
                else overlaps++;
            }
            string key = vestIds.ToString();
            if (key != vestKey)
            {
                vestKey = key;
                var log = new StringBuilder();
                log.Append("[VESTQA] vestInstances=");
                int vestN = 0;
                for (int i = 0; i < live.Count; i++)
                {
                    if (live[i].entry.role != "vest") continue;
                    vestN++;
                    log.Append('\n').Append(SkiViewVestQaLogic.LegendLine(live[i].entry));
                }
                log.Append("\noverlap=").Append(overlaps).Append(" vest=").Append(vestN);
                Debug.Log(log.ToString());
            }
        }

        void Remember(int id, Renderer r)
        {
            if (snaps.ContainsKey(id) || r == null) return;
            var ms = r.sharedMaterials;
            if (ms != null)
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && SkiViewVestQaLogic.IsQaMaterialName(ms[i].name)) return;
            snaps[id] = new Snap
            {
                materials = ms == null ? new Material[0] : (Material[])ms.Clone(),
                enabled = r.enabled,
                materialNames = GameplayMatNames(r)
            };
        }

        void RestoreAll()
        {
            foreach (var kv in snaps)
            {
                var r = RendererById(kv.Key);
                if (r == null) continue;
                r.sharedMaterials = kv.Value.materials;
                r.enabled = kv.Value.enabled;
            }
            for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) Destroy(spawned[i]);
            spawned.Clear();
        }

        Renderer RendererById(int id)
        {
            for (int i = 0; i < live.Count; i++)
                if (live[i].renderer != null && live[i].renderer.GetInstanceID() == id) return live[i].renderer;
            return null;
        }

        void ApplyPose(SkiViewGame game)
        {
            float roll = poseRoll;
            if (float.IsNaN(roll)) return;
            Transform body = game.skier;
            body.rotation = body.rotation * Quaternion.AngleAxis(roll, body.forward);
            if (faceCam) Reframe(game);
        }

        void Reframe(SkiViewGame game)
        {
            if (game == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            Transform head = Head(game);
            Vector3 aim = head != null ? head.position : game.skier.position + Vector3.up * 1.58f;
            Vector3 faceDir = head != null ? head.forward : game.skier.forward;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 1e-4f) faceDir = Vector3.forward;
            faceDir.Normalize();
            Vector3 toCam = cam.transform.position - aim;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.01f && Vector3.Dot(faceDir, toCam.normalized) < 0f) faceDir = -faceDir;
            Vector3 portrait = aim - Vector3.up * 0.055f;
            Vector3 pos = portrait + faceDir * 0.94f + Vector3.up * 0.01f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(portrait - pos, Vector3.up));
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            if (!framed) { framed = true; Debug.Log("[VESTQA] face camera re-aimed after pose"); }
        }

        void ApplyShade()
        {
            string key = shade + "|" + (isolate ? isolateInst.ToString() : "") + "|" + scopeAll + "|" + vestKey;
            if (key != appliedKey)
            {
                RestoreAll();
                appliedKey = key;
                if (shade != "lit")
                {
                    Shader shader = LoadShader();
                    if (shader == null) Debug.LogError("[VESTQA] debug shader missing; materials left unchanged");
                    else Paint(shader);
                }
            }
            if (!isolate) return;
            for (int i = 0; i < live.Count; i++)
            {
                var item = live[i];
                if (item.renderer == null) continue;
                bool affect = item.entry.role == "vest" || (scopeAll && item.entry.role == "overlap");
                if (!affect) continue;
                item.renderer.enabled = item.entry.id == isolateInst;
            }
        }

        void Paint(Shader shader)
        {
            for (int i = 0; i < live.Count; i++)
            {
                var item = live[i];
                var r = item.renderer;
                if (r == null) continue;
                bool affect = item.entry.role == "vest" || (scopeAll && item.entry.role == "overlap");
                if (!affect) continue;
                if (isolate && item.entry.id != isolateInst) continue;
                int subs = Mathf.Max(1, item.entry.submeshes);
                var mats = new Material[subs];
                for (int s = 0; s < subs; s++)
                {
                    var m = new Material(shader) { name = "QaShade_" + item.entry.id + "_" + s };
                    Color c = shade == "unlit" ? new Color(0.55f, 0.55f, 0.52f, 1f) : IdColor(item.entry.id, s);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    string modeShade = shade == "ids" || shade == "unlit" ? "unlit" : shade;
                    if (m.HasProperty("_Mode")) m.SetFloat("_Mode", SkiViewVestQaLogic.ShadeMode(modeShade));
                    mats[s] = m;
                    spawned.Add(m);
                }
                r.sharedMaterials = mats;
            }
        }

        static Shader LoadShader()
        {
            var s = Resources.Load<Shader>("VestQa");
            if (s != null) return s;
            return Shader.Find("Hidden/SkiView/VestQa");
        }

        static Color IdColor(int id, int submesh)
        {
            uint n = (uint)id * 8u + (uint)submesh + 1u;
            float r = ((n * 67u) % 255u) / 255f;
            float g = ((n * 131u) % 255u) / 255f;
            float b = ((n * 193u) % 255u) / 255f;
            return new Color(0.25f + 0.75f * r, 0.25f + 0.75f * g, 0.25f + 0.75f * b, 1f);
        }

        void OnGUI()
        {
            int vestN = 0, overlapN = 0;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].entry.role == "vest") vestN++;
                else overlapN++;
            }
            const float w = 440f;
            float x = Mathf.Max(8f, Screen.width - w - 8f);
            float y = 64f;
            float panelH = Mathf.Min(420f, Mathf.Max(180f, Screen.height - 150f));
            GUI.Box(new Rect(x, y, w, panelH), GUIContent.none);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = Color.white } };
            var small = new GUIStyle(GUI.skin.button) { fontSize = 11 };
            string head = SkiViewVestQaLogic.Heartbeat(vestN, loadedScene, state, scanError)
                + " shade=" + shade
                + " overlap=" + overlapN
                + (isolate ? " isolate=" + isolateInst : "")
                + (float.IsNaN(poseRoll) ? "" : " poseRoll=" + poseRoll.ToString("0"));
            GUI.Label(new Rect(x + 6f, y + 4f, w - 12f, 36f), head, label);
            float by = y + 40f;
            if (GUI.Button(new Rect(x + 6f, by, 64f, 22f), "Lit", small)) SetShade("lit");
            if (GUI.Button(new Rect(x + 74f, by, 64f, 22f), "Unlit", small)) SetShade("unlit");
            if (GUI.Button(new Rect(x + 142f, by, 72f, 22f), "Normals", small)) SetShade("normals");
            if (GUI.Button(new Rect(x + 218f, by, 78f, 22f), "Tangents", small)) SetShade("tangents");
            if (GUI.Button(new Rect(x + 300f, by, 50f, 22f), "Ids", small)) SetShade("ids");
            by += 26f;
            if (GUI.Button(new Rect(x + 6f, by, 80f, 22f), "Isolate", small)) IsolateSelected();
            if (GUI.Button(new Rect(x + 90f, by, 80f, 22f), "Show all", small)) ShowAll();
            if (GUI.Button(new Rect(x + 174f, by, 52f, 22f), "Pose L", small)) poseRoll = SkiViewVestQaLogic.PoseDegrees;
            if (GUI.Button(new Rect(x + 230f, by, 52f, 22f), "Pose C", small)) poseRoll = 0f;
            if (GUI.Button(new Rect(x + 286f, by, 52f, 22f), "Pose R", small)) poseRoll = -SkiViewVestQaLogic.PoseDegrees;
            if (GUI.Button(new Rect(x + 342f, by, 90f, 22f), showOverlap ? "Hide rest" : "Overlap", small)) showOverlap = !showOverlap;
            by += 26f;
            var shown = new List<Live>();
            for (int i = 0; i < live.Count; i++)
                if (live[i].entry.role == "vest" || showOverlap) shown.Add(live[i]);
            float rowH = 22f;
            float viewH = panelH - (by - y) - 48f;
            if (viewH < 60f) viewH = 60f;
            var view = new Rect(x + 6f, by, w - 12f, viewH);
            var content = new Rect(0f, 0f, w - 28f, Mathf.Max(viewH, shown.Count * rowH));
            scroll = GUI.BeginScrollView(view, scroll, content);
            if (shown.Count == 0) GUI.Label(new Rect(0f, 0f, content.width, rowH), "renderers=0", label);
            for (int i = 0; i < shown.Count; i++)
            {
                string text = (shown[i].entry.id == selectedInst ? "> " : "") + SkiViewVestQaLogic.CompactLine(shown[i].entry);
                if (GUI.Button(new Rect(0f, i * rowH, content.width, rowH - 2f), text, small))
                    selectedInst = shown[i].entry.id;
            }
            GUI.EndScrollView();
            string detail = "select a row";
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].entry.id != selectedInst) continue;
                detail = "inst=" + live[i].entry.id + " " + live[i].entry.path + " mats=" + live[i].entry.materials;
                break;
            }
            GUI.Label(new Rect(x + 6f, y + panelH - 42f, w - 12f, 38f), detail, label);
        }

        void SetShade(string next)
        {
            shade = next;
            appliedKey = "";
        }

        void IsolateSelected()
        {
            if (selectedInst == 0) return;
            isolate = true;
            isolateInst = selectedInst;
            appliedKey = "";
        }

        void ShowAll()
        {
            isolate = false;
            isolateInst = 0;
            appliedKey = "";
        }

        static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer sm) return sm.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        static string GameplayMatNames(Renderer r)
        {
            var ms = r.sharedMaterials;
            if (ms == null || ms.Length == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < ms.Length; i++)
            {
                if (i > 0) sb.Append('|');
                if (ms[i] == null) sb.Append("null");
                else sb.Append(ms[i].name).Append('/').Append(ms[i].shader != null ? ms[i].shader.name : "");
            }
            return sb.ToString();
        }

        static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        static bool OverlapsCollar(Renderer r, Transform chest)
        {
            if (chest == null || r == null) return false;
            Bounds b = r.bounds;
            Vector3 c = chest.position;
            Vector3 closest = b.ClosestPoint(c);
            return (closest - c).sqrMagnitude < 0.35f * 0.35f;
        }

        static Transform Chest(SkiViewGame game)
        {
            var anim = game.rig != null ? game.rig.animator : null;
            if (anim != null && anim.isHuman)
                return anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Neck);
            return game.skier;
        }

        static Transform Head(SkiViewGame game)
        {
            var anim = game.rig != null ? game.rig.animator : null;
            if (anim != null && anim.isHuman) return anim.GetBoneTransform(HumanBodyBones.Head);
            if (anim == null) return null;
            foreach (var t in anim.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("Head")) return t;
            return null;
        }
    }
}

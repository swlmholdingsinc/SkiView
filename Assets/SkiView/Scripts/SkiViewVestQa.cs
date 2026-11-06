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

        readonly List<Live> live = new List<Live>();
        string signature = "";
        string appliedKey = "";
        float nextScan;
        bool framed;
        string state = "init";
        string scanError = "";
        readonly List<Material> spawned = new List<Material>();
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
            live.Clear();
            signature = "";
            appliedKey = "";
            nextScan = 0f;
            framed = false;
            scanError = "";
            state = "rebind " + sceneName;
            for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) Destroy(spawned[i]);
            spawned.Clear();
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
            var game = FindFirstObjectByType<SkiViewGame>();
            if (game == null) { state = "waiting for game"; return; }
            if (game.skier == null) { state = "waiting for skier"; return; }
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.5f; Scan(game, args); }
            ApplyPose(game, args);
            ApplyShade(args);
        }

        void Scan(SkiViewGame game, Dictionary<string, string> args)
        {
            try
            {
                ScanBody(game, args);
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

        void ScanBody(SkiViewGame game, Dictionary<string, string> args)
        {
            var found = new List<Renderer>();
            var seen = new HashSet<int>();
            void AddRoot(Transform root)
            {
                if (root == null) return;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !seen.Add(r.GetInstanceID())) continue;
                    found.Add(r);
                }
            }
            AddRoot(game.skier);
            if (game.rig != null) AddRoot(game.rig.transform);
            if (game.rig != null && game.rig.animator != null) AddRoot(game.rig.animator.transform);

            Transform chest = Chest(game);
            bool scopeAll = Arg(args, "vestscope") == "all" || Arg(args, "vestqa") == "all";
            var raw = new List<SkiViewVestQaLogic.Entry>();
            var renderers = new List<Renderer>();
            foreach (var r in found)
            {
                var mesh = MeshOf(r);
                string mats = MatNames(r);
                string role = SkiViewVestQaLogic.Role(r.name, mesh != null ? mesh.name : "", mats);
                bool overlap = role == "other" && OverlapsCollar(r, chest);
                if (role != "vest" && !(overlap && scopeAll) && !overlap) continue;
                if (overlap && role == "other") role = "overlap";
                if (role == "overlap" && !scopeAll)
                {
                    // Logged so a body facet is not invisible, and left on its gameplay material.
                }
                int tris = 0;
                int subs = 1;
                bool readable = false;
                if (mesh != null)
                {
                    subs = Mathf.Max(1, mesh.subMeshCount);
                    readable = mesh.isReadable;
                    if (readable) for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)mesh.GetIndexCount(s) / 3;
                }
                raw.Add(new SkiViewVestQaLogic.Entry
                {
                    role = role,
                    path = PathOf(r.transform) + "/" + r.GetType().Name,
                    mesh = mesh != null ? mesh.name : "",
                    submeshes = subs,
                    materials = mats,
                    triangles = tris,
                    enabled = r.enabled && r.gameObject.activeInHierarchy,
                    readable = readable
                });
                renderers.Add(r);
            }
            var ordered = SkiViewVestQaLogic.AssignIds(raw);
            // AssignIds sorts a copy; match renderers back by path. Paths can repeat if two renderers share a name chain.
            var byPath = new Dictionary<string, Queue<Renderer>>();
            for (int i = 0; i < raw.Count; i++)
            {
                if (!byPath.TryGetValue(raw[i].path, out var q)) { q = new Queue<Renderer>(); byPath[raw[i].path] = q; }
                q.Enqueue(renderers[i]);
            }
            live.Clear();
            var sig = new StringBuilder();
            foreach (var e in ordered)
            {
                Renderer rr = null;
                if (byPath.TryGetValue(e.path, out var q) && q.Count > 0) rr = q.Dequeue();
                live.Add(new Live { entry = e, renderer = rr });
                sig.Append(SkiViewVestQaLogic.LegendLine(e)).Append('\n');
            }
            string text = sig.ToString();
            if (text != signature)
            {
                signature = text;
                Debug.Log("[VESTQA] count=" + live.Count + " shade=" + SkiViewVestQaLogic.Shade(Arg(args, "vestshade"))
                    + " pose=" + (Arg(args, "pose") ?? "") + "\n" + text);
            }
        }

        void ApplyPose(SkiViewGame game, Dictionary<string, string> args)
        {
            float roll = SkiViewVestQaLogic.PoseRollDegrees(Arg(args, "pose"));
            if (float.IsNaN(roll)) return;
            Transform body = game.skier;
            body.rotation = body.rotation * Quaternion.AngleAxis(roll, body.forward);
            string cam = Arg(args, "cam");
            if (cam != null && cam.Trim().ToLowerInvariant() == "face")
                Reframe(game);
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

        void ApplyShade(Dictionary<string, string> args)
        {
            string shade = SkiViewVestQaLogic.Shade(Arg(args, "vestshade"));
            int solo = SkiViewVestQaLogic.ParseId(Arg(args, "vestid"));
            bool hasSub = SkiViewVestQaLogic.ParseSub(Arg(args, "vestsub"), out int subRenderer, out int subIndex);
            bool scopeAll = Arg(args, "vestscope") == "all" || Arg(args, "vestqa") == "all";
            bool isolate = solo >= 0 || hasSub;
            for (int i = 0; i < live.Count; i++)
            {
                var item = live[i];
                if (item.renderer == null) continue;
                bool affect = item.entry.role == "vest" || (scopeAll && item.entry.role == "overlap");
                if (!affect || !isolate) continue;
                bool keep = solo < 0 || item.entry.id == solo;
                if (hasSub) keep = item.entry.id == subRenderer;
                item.renderer.enabled = keep;
            }
            string key = shade + "|" + solo + "|" + (hasSub ? subRenderer + "." + subIndex : "") + "|" + scopeAll + "|" + signature;
            if (shade == "lit" || key == appliedKey) return;
            Shader shader = LoadShader();
            if (shader == null)
            {
                Debug.LogError("[VESTQA] debug shader missing; materials left unchanged");
                return;
            }
            for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) Destroy(spawned[i]);
            spawned.Clear();
            appliedKey = key;
            for (int i = 0; i < live.Count; i++)
            {
                var item = live[i];
                var r = item.renderer;
                if (r == null) continue;
                bool affect = item.entry.role == "vest" || (scopeAll && item.entry.role == "overlap");
                if (!affect) continue;
                if (isolate && !(solo < 0 || item.entry.id == solo) && !(hasSub && item.entry.id == subRenderer)) continue;
                int subs = Mathf.Max(1, item.entry.submeshes);
                var mats = new Material[subs];
                for (int s = 0; s < subs; s++)
                {
                    bool dim = hasSub && item.entry.id == subRenderer && s != subIndex;
                    var m = new Material(shader) { name = "VestQa_" + item.entry.id + "_" + s };
                    Color c = shade == "unlit" ? new Color(0.55f, 0.55f, 0.52f, 1f) : IdColor(item.entry.id, s);
                    if (dim) c = new Color(0.08f, 0.08f, 0.08f, 1f);
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
            int n = id * 8 + submesh + 1;
            float r = ((n * 67) % 255) / 255f;
            float g = ((n * 131) % 255) / 255f;
            float b = ((n * 193) % 255) / 255f;
            return new Color(0.25f + 0.75f * r, 0.25f + 0.75f * g, 0.25f + 0.75f * b, 1f);
        }

        void OnGUI()
        {
            var args = ReadArgs();
            float roll = SkiViewVestQaLogic.PoseRollDegrees(Arg(args, "pose"));
            var sb = new StringBuilder();
            sb.Append(SkiViewVestQaLogic.Heartbeat(live.Count, loadedScene, state, scanError));
            sb.Append(" shade=").Append(SkiViewVestQaLogic.Shade(Arg(args, "vestshade")));
            if (!float.IsNaN(roll)) sb.Append(" poseRoll=").Append(roll.ToString("0"));
            sb.Append('\n');
            if (live.Count == 0) sb.Append("renderers=0\n");
            for (int i = 0; i < live.Count; i++) sb.Append(SkiViewVestQaLogic.LegendLine(live[i].entry)).Append('\n');
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = Color.white } };
            const float w = 460f;
            float h = style.CalcHeight(new GUIContent(sb.ToString()), w);
            var area = new Rect(Mathf.Max(12f, Screen.width - w - 12f), 118f, w, h + 8f);
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x + 6f, area.y + 4f, w - 12f, h), sb.ToString(), style);
        }

        static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer sm) return sm.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        static string MatNames(Renderer r)
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

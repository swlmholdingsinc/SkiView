using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SkiView;

/// <summary>
/// Edit-mode inventory of the staged skier after the same SetAthlete call the 24-shot capture uses.
/// Does not call UpdateGear125, does not run Drive, and does not save the scene.
/// Same-pose lit frames: authored shadows, shadows off, and one hide per enabled duplicate mesh.
/// Player visibility is not observed.
///
/// Unity.exe -batchmode -projectPath &lt;staged&gt; -executeMethod SkiViewRendererInventory125.Run -logFile renderer-inventory.log -quit
/// </summary>
public static class SkiViewRendererInventory125
{
    const string ScenePath = "Assets/SkiView/Scenes/SkiViewLake.unity";

    public static void Run()
    {
        int code = 0;
        try { code = Capture(); }
        catch (Exception ex)
        {
            Debug.LogError("[RendInv] " + ex);
            code = 5;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int Capture()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[RendInv] no graphics device. Do not pass -nographics.");
            return 2;
        }
        bool allowOther = Environment.GetEnvironmentVariable("SV_CAPTURE_ALLOW_NON_D3D11") == "1";
        if (!allowOther && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
        {
            Debug.LogError("[RendInv] graphicsDeviceType=" + SystemInfo.graphicsDeviceType);
            return 2;
        }
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null || pipeline.GetType().Name.IndexOf("Universal", StringComparison.Ordinal) < 0)
        {
            Debug.LogError("[RendInv] URP is not active.");
            return 2;
        }
        if (!File.Exists(ScenePath)) { Debug.LogError("[RendInv] missing scene"); return 3; }
        EditorSceneManager.OpenScene(ScenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null || game.skier == null || game.rig == null || game.rig.animator == null)
        {
            Debug.LogError("[RendInv] no skier rig");
            return 3;
        }
        var setAthlete = game.GetType().GetMethod("SetAthlete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        if (setAthlete == null) { Debug.LogError("[RendInv] SetAthlete missing"); return 3; }
        Light sun = null;
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light != null && light.type == LightType.Directional && light.enabled) { sun = light; break; }
        if (sun == null) { Debug.LogError("[RendInv] no directional light"); return 3; }

        string dir = Environment.GetEnvironmentVariable("SV_INVENTORY_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/renderer125"));
        Directory.CreateDirectory(dir);

        var before = Collect(game);
        setAthlete.Invoke(game, new object[] { 0 });
        var after = Collect(game);
        var beforeIds = new HashSet<int>();
        for (int i = 0; i < before.Count; i++) beforeIds.Add(before[i].id);

        int n = after.Count;
        var ids = new int[n];
        var meshes = new string[n];
        var enabled = new bool[n];
        for (int i = 0; i < n; i++) { ids[i] = after[i].id; meshes[i] = after[i].mesh; enabled[i] = after[i].enabled; }
        var dups = SkiViewCharacterCapture125Logic.EnabledDuplicateIds(ids, meshes, enabled);

        var head = game.rig.animator.GetBoneTransform(HumanBodyBones.Head);
        var camGo = new GameObject("RendererInventoryCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.aspect = 1280f / 720f;
        if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
            camGo.AddComponent<UniversalAdditionalCameraData>();
        var savedEnabled = new Dictionary<int, bool>();
        var savedCast = new Dictionary<int, ShadowCastingMode>();
        var renderers = AllRenderers(game);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            savedEnabled[r.GetInstanceID()] = r.enabled;
            savedCast[r.GetInstanceID()] = r.shadowCastingMode;
        }
        var sunShadows = sun.shadows;
        var shots = new StringBuilder();
        try
        {
            if (head != null) SkiViewCharacterCapture125.Frame(cam, head);
            Shot(cam, dir, "baseline_shadows_authored", shots);
            sun.shadows = LightShadows.None;
            foreach (var r in renderers) if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            Shot(cam, dir, "shadows_off", shots);
            sun.shadows = sunShadows;
            foreach (var r in renderers) if (r != null && savedCast.ContainsKey(r.GetInstanceID())) r.shadowCastingMode = savedCast[r.GetInstanceID()];
            for (int i = 0; i < dups.Count; i++)
            {
                Renderer hide = null;
                foreach (var r in renderers) if (r != null && r.GetInstanceID() == dups[i]) hide = r;
                if (hide == null) continue;
                hide.enabled = false;
                Shot(cam, dir, "hide_" + dups[i], shots);
                hide.enabled = savedEnabled[dups[i]];
            }
        }
        finally
        {
            sun.shadows = sunShadows;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                int id = r.GetInstanceID();
                if (savedEnabled.ContainsKey(id)) r.enabled = savedEnabled[id];
                if (savedCast.ContainsKey(id)) r.shadowCastingMode = savedCast[id];
            }
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"playMode\": false,\n");
        sb.Append("  \"playerVisibility\": \"not observed\",\n");
        sb.Append("  \"init\": \"edit-mode SetAthlete(0), the same call SkiViewCharacterCapture125.Run uses\",\n");
        sb.Append("  \"updateGear125Invoked\": false,\n");
        sb.Append("  \"driveInvoked\": false,\n");
        sb.Append("  \"staticCaptureOrientation\": true,\n");
        sb.Append("  \"clearance\": \"not evaluated\",\n");
        sb.Append("  \"sceneSaved\": false,\n");
        sb.Append("  \"beforeSetAthlete\": ");
        WriteRows(sb, before, null);
        sb.Append(",\n  \"afterSetAthlete\": ");
        WriteRows(sb, after, beforeIds);
        sb.Append(",\n  \"enabledDuplicateIds\": [");
        for (int i = 0; i < dups.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(dups[i]); }
        sb.Append("],\n  \"shots\": [\n");
        sb.Append(shots);
        sb.Append("\n  ]\n}\n");
        File.WriteAllText(Path.Combine(dir, "inventory.json"), sb.ToString());
        Debug.Log("[RendInv] " + dir + " renderers=" + after.Count + " enabledDuplicates=" + dups.Count);
        return 0;
    }

    static void Shot(Camera cam, string dir, string name, StringBuilder shots)
    {
        uint hash; float mean, stdev; int bytes;
        string err = SkiViewCharacterCapture125.Render(cam, Path.Combine(dir, name + ".png"), out hash, out mean, out stdev, out bytes);
        if (shots.Length > 0) shots.Append(",\n");
        shots.Append("    {\"name\": \"").Append(name).Append("\", \"bytes\": ").Append(bytes)
            .Append(", \"error\": \"").Append(err ?? "").Append("\"}");
    }

    struct Row
    {
        public int id;
        public string name, mesh, path, role;
        public bool enabled, active, receive;
        public int verts, shapes;
        public ShadowCastingMode cast;
        public string materials;
    }

    static List<Row> Collect(SkiViewGame game)
    {
        var list = new List<Row>();
        var seen = new HashSet<int>();
        Add(game.skier, list, seen);
        if (game.rig != null && game.rig.animator != null) Add(game.rig.animator.transform, list, seen);
        return list;
    }

    static void Add(Transform root, List<Row> list, HashSet<int> seen)
    {
        if (root == null) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !seen.Add(r.GetInstanceID())) continue;
            var sm = r as SkinnedMeshRenderer;
            string mesh = sm != null && sm.sharedMesh != null ? sm.sharedMesh.name : "";
            var mats = r.sharedMaterials;
            var mb = new StringBuilder();
            if (mats != null)
                for (int i = 0; i < mats.Length; i++)
                {
                    if (i > 0) mb.Append("|");
                    mb.Append(mats[i] != null ? mats[i].name : "");
                }
            list.Add(new Row
            {
                id = r.GetInstanceID(),
                name = r.name,
                mesh = mesh,
                path = PathOf(r.transform),
                role = SkiViewCharacterCapture125Logic.MeshRole(r.name, mesh),
                enabled = r.enabled,
                active = r.gameObject.activeInHierarchy,
                receive = r.receiveShadows,
                verts = sm != null && sm.sharedMesh != null ? sm.sharedMesh.vertexCount : 0,
                shapes = sm != null && sm.sharedMesh != null ? sm.sharedMesh.blendShapeCount : 0,
                cast = r.shadowCastingMode,
                materials = mb.ToString()
            });
        }
    }

    static List<Renderer> AllRenderers(SkiViewGame game)
    {
        var list = new List<Renderer>();
        var seen = new HashSet<int>();
        void Take(Transform root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null && seen.Add(r.GetInstanceID())) list.Add(r);
        }
        Take(game.skier);
        if (game.rig != null && game.rig.animator != null) Take(game.rig.animator.transform);
        return list;
    }

    static void WriteRows(StringBuilder sb, List<Row> rows, HashSet<int> before)
    {
        sb.Append("[\n");
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(",\n");
            var r = rows[i];
            sb.Append("    {\"id\": ").Append(r.id);
            sb.Append(", \"name\": \"").Append(Esc(r.name)).Append("\"");
            sb.Append(", \"mesh\": \"").Append(Esc(r.mesh)).Append("\"");
            sb.Append(", \"role\": \"").Append(r.role).Append("\"");
            sb.Append(", \"path\": \"").Append(Esc(r.path)).Append("\"");
            sb.Append(", \"enabled\": ").Append(r.enabled ? "true" : "false");
            sb.Append(", \"active\": ").Append(r.active ? "true" : "false");
            sb.Append(", \"shadowCasting\": \"").Append(r.cast).Append("\"");
            sb.Append(", \"receiveShadows\": ").Append(r.receive ? "true" : "false");
            sb.Append(", \"verts\": ").Append(r.verts);
            sb.Append(", \"shapes\": ").Append(r.shapes);
            sb.Append(", \"materials\": \"").Append(Esc(r.materials)).Append("\"");
            if (before != null) sb.Append(", \"createdBySetAthlete\": ").Append(before.Contains(r.id) ? "false" : "true");
            sb.Append("}");
        }
        sb.Append("\n  ]");
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder();
        while (t != null)
        {
            if (sb.Length > 0) sb.Insert(0, "/");
            sb.Insert(0, t.name);
            t = t.parent;
        }
        return sb.ToString();
    }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "'");
    }
}

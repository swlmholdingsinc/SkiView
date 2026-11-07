using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SkiView;

/// <summary>
/// Same editor step as SkiViewPoseEvidence125 (seed 125, autopilot, SimulateFor, then UpdateGear125 if it exists).
/// Writes the 39 core frames only, with every blendshape weight that was read, the body, and baked imported vertices.
/// Does not decide clearance. Does not save the scene. The 24-shot command is a different method.
///
/// Unity.exe -batchmode -projectPath &lt;staged&gt; -executeMethod SkiViewNativeEvidence125.Core -logFile native-evidence.log -quit
/// </summary>
public static class SkiViewNativeEvidence125
{
    const string ScenePath = "Assets/SkiView/Scenes/SkiViewLake.unity";
    const string BodyFbx = "Assets/SkiView/Skier/INTERNAL_ONLY_Felderpuss_V3_WadeCox.fbx";
    const string VestFbx = "Assets/SkiView/Resources/Gear125/SkiView_Gear_Slalom_125.fbx";

    public static void Core()
    {
        int code = 0;
        try { code = Run(); }
        catch (Exception ex)
        {
            Debug.LogError("[NativeEv] " + ex);
            code = 5;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int Run()
    {
        if (!File.Exists(ScenePath)) { Debug.LogError("[NativeEv] missing scene"); return 3; }
        EditorSceneManager.OpenScene(ScenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null || game.path == null || game.path.Count == 0 || game.rig == null || game.rig.animator == null || game.skier == null)
        {
            Debug.LogError("[NativeEv] SkiViewLake has no humanoid rig on a path.");
            return 3;
        }
        string dir = Environment.GetEnvironmentVariable("SV_EVIDENCE_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/native-evidence125"));
        Directory.CreateDirectory(dir);

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var gear = game.GetType().GetMethod("UpdateGear125", flags, null, Type.EmptyTypes, null);
        var ensure = game.GetType().GetMethod("EnsureGear125", flags, null, Type.EmptyTypes, null);
        var setGear = game.GetType().GetMethod("SetGear125", flags, null, new[] { typeof(bool) }, null);
        string driverError = "";
        game.autopilot = true;
        UnityEngine.Random.InitState(125);
        game.Init();
        Invoke(ensure, game, null, ref driverError);
        if (setGear != null) Invoke(setGear, game, new object[] { game.jumpMode }, ref driverError);

        int last = SkiViewCharacterCapture125Logic.LastCoreFrame();
        var frames = new StringBuilder();
        int written = 0;
        var baked = new Mesh { name = "NativeEvBake" };
        for (int i = 0; i <= last; i++)
        {
            game.SimulateFor(1f / 60f);
            if (gear != null) Invoke(gear, game, null, ref driverError);
            if (!SkiViewCharacterCapture125Logic.IsCoreFrame(i)) continue;
            if (written > 0) frames.Append('\n');
            frames.Append(Frame(game, i, gear != null, baked));
            written++;
        }
        UnityEngine.Object.DestroyImmediate(baked);
        File.WriteAllText(Path.Combine(dir, "core.jsonl"), frames.ToString());
        File.WriteAllText(Path.Combine(dir, "core-header.json"), Header(game, gear != null, driverError, written));
        Debug.Log("[NativeEv] " + dir + " core=" + written + " driver=" + (gear != null) + " sceneSaved=false clearance=not-evaluated");
        return 0;
    }

    static string Header(SkiViewGame game, bool driver, string driverError, int written)
    {
        var anim = game.rig.animator.transform;
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"evaluation\": \"SkiViewGame.SimulateFor -> ApplyVisuals -> SkierRig.Drive\",\n");
        sb.Append("  \"updateGear125InvokedByExport\": ").Append(driver ? "true" : "false").Append(",\n");
        sb.Append("  \"applyVisualsInThisRepoCallsUpdateGear125\": false,\n");
        sb.Append("  \"playMode\": false,\n");
        sb.Append("  \"playerVisibility\": \"not observed\",\n");
        sb.Append("  \"clearance\": \"not evaluated\",\n");
        sb.Append("  \"seed\": 125,\n");
        sb.Append("  \"coreFrames\": ").Append(written).Append(",\n");
        sb.Append("  \"armatureObjectOffsetZ\": ").Append(G(SkiViewCharacterCapture125Logic.ArmatureObjectOffsetZ)).Append(",\n");
        sb.Append("  \"headCanonicalY\": ").Append(G(SkiViewCharacterCapture125Logic.HeadCanonicalY)).Append(",\n");
        sb.Append("  \"neckCanonicalY\": ").Append(G(SkiViewCharacterCapture125Logic.NeckCanonicalY)).Append(",\n");
        sb.Append("  \"bakeUseScale\": false,\n");
        sb.Append("  \"bakeSpace\": \"").Append(SkiViewCharacterCapture125Logic.BakeSpace).Append("\",\n");
        sb.Append("  \"matrixLayout\": \"Unity Matrix4x4 indexer, column-major, 16 floats\",\n");
        sb.Append("  \"skinWeights\": ").Append((int)QualitySettings.skinWeights).Append(",\n");
        sb.Append("  \"animatorLossyScale\": "); Vec(sb, anim.lossyScale); sb.Append(",\n");
        sb.Append("  \"skierLossyScale\": "); Vec(sb, game.skier.lossyScale); sb.Append(",\n");
        sb.Append("  \"bodySha256\": \"").Append(ShaFile(BodyFbx)).Append("\",\n");
        sb.Append("  \"originalVestSha256\": \"").Append(ShaFile(VestFbx)).Append("\",\n");
        sb.Append("  \"r6Files\": ");
        R6Files(sb);
        sb.Append(",\n  \"driverError\": \"").Append(Esc(driverError)).Append("\",\n");
        sb.Append("  \"sceneSaved\": false\n}\n");
        return sb.ToString();
    }

    static string Frame(SkiViewGame game, int index, bool driver, Mesh baked)
    {
        var anim = game.rig.animator;
        var sb = new StringBuilder();
        sb.Append("{\"i\": ").Append(index);
        sb.Append(", \"t\": ").Append(G(index / 60f));
        sb.Append(", \"mode\": \"").Append(game.mode).Append("\"");
        sb.Append(", \"gearDriverInvoked\": ").Append(driver ? "true" : "false");
        sb.Append(", \"playMode\": false");
        sb.Append(", \"animatorLossyScale\": "); Vec(sb, anim.transform.lossyScale);
        sb.Append(", \"meshes\": [");
        var seen = new HashSet<int>();
        bool any = false;
        any = AppendMeshes(sb, game.skier, baked, seen, any);
        if (game.rig.animator != null) any = AppendMeshes(sb, game.rig.animator.transform, baked, seen, any);
        sb.Append("]}");
        return sb.ToString();
    }

    static bool AppendMeshes(StringBuilder sb, Transform root, Mesh baked, HashSet<int> seen, bool any)
    {
        if (root == null) return any;
        foreach (var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (sm == null || sm.sharedMesh == null || !seen.Add(sm.GetInstanceID())) continue;
            string role = SkiViewCharacterCapture125Logic.MeshRole(sm.name, sm.sharedMesh.name);
            if (role == "other" || role == "otherVest") continue;
            if (any) sb.Append(", ");
            any = true;
            WriteMesh(sb, sm, role, baked);
        }
        return any;
    }

    static void WriteMesh(StringBuilder sb, SkinnedMeshRenderer sm, string role, Mesh baked)
    {
        var mesh = sm.sharedMesh;
        bool bake = SkiViewCharacterCapture125Logic.BakeRole(role, sm.name, mesh.name);
        sb.Append("{\"id\": ").Append(sm.GetInstanceID());
        sb.Append(", \"role\": \"").Append(role).Append("\"");
        sb.Append(", \"name\": \"").Append(Esc(sm.name)).Append("\"");
        sb.Append(", \"mesh\": \"").Append(Esc(mesh.name)).Append("\"");
        sb.Append(", \"enabled\": ").Append(sm.enabled ? "true" : "false");
        sb.Append(", \"active\": ").Append(sm.gameObject.activeInHierarchy ? "true" : "false");
        sb.Append(", \"shadowCasting\": \"").Append(sm.shadowCastingMode).Append("\"");
        sb.Append(", \"receiveShadows\": ").Append(sm.receiveShadows ? "true" : "false");
        sb.Append(", \"importedVertexCount\": ").Append(mesh.vertexCount);
        sb.Append(", \"blendShapeCount\": ").Append(mesh.blendShapeCount);
        sb.Append(", \"boneCount\": ").Append(sm.bones != null ? sm.bones.Length : 0);
        sb.Append(", \"rootBone\": \"").Append(sm.rootBone != null ? Esc(sm.rootBone.name) : "").Append("\"");
        sb.Append(", \"localScale\": "); Vec(sb, sm.transform.localScale);
        sb.Append(", \"lossyScale\": "); Vec(sb, sm.transform.lossyScale);
        sb.Append(", \"localToWorld\": "); Mat(sb, sm.localToWorldMatrix);
        sb.Append(", \"worldToLocal\": "); Mat(sb, sm.worldToLocalMatrix);
        if (sm.rootBone != null) { sb.Append(", \"rootBoneLocalToWorld\": "); Mat(sb, sm.rootBone.localToWorldMatrix); }
        sb.Append(", \"bindposes\": [");
        var bp = mesh.bindposes;
        for (int i = 0; i < bp.Length; i++) { if (i > 0) sb.Append(", "); Mat(sb, bp[i]); }
        sb.Append("], \"weights\": [");
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (!SkiViewCharacterCapture125Logic.IncludeReadWeight(true)) continue;
            if (i > 0) sb.Append(", ");
            float w = sm.GetBlendShapeWeight(i);
            sb.Append("{\"name\": \"").Append(Esc(mesh.GetBlendShapeName(i))).Append("\", \"weight\": ").Append(G(w)).Append(", \"read\": true}");
        }
        sb.Append("], \"bones\": [");
        var bones = sm.bones;
        if (bones != null)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                var b = bones[i];
                sb.Append("{\"name\": \"").Append(b != null ? Esc(b.name) : "").Append("\"");
                if (b != null)
                {
                    sb.Append(", \"localPos\": "); Vec(sb, b.localPosition);
                    sb.Append(", \"localRot\": "); Quat(sb, b.localRotation);
                    sb.Append(", \"localScale\": "); Vec(sb, b.localScale);
                    sb.Append(", \"lossyScale\": "); Vec(sb, b.lossyScale);
                    sb.Append(", \"localToWorld\": "); Mat(sb, b.localToWorldMatrix);
                }
                sb.Append("}");
            }
        }
        sb.Append("]");
        if (!bake) { sb.Append(", \"baked\": false}"); return; }
        baked.Clear();
        sm.BakeMesh(baked, SkiViewCharacterCapture125Logic.BakeUseScale);
        sb.Append(", \"baked\": true, \"bakeUseScale\": false, \"bakeSpace\": \"").Append(SkiViewCharacterCapture125Logic.BakeSpace).Append("\"");
        sb.Append(", \"bakedVertexCount\": ").Append(baked.vertexCount);
        var verts = baked.vertices;
        var norms = baked.normals;
        sb.Append(", \"vertices\": [");
        for (int i = 0; i < verts.Length; i++) { if (i > 0) sb.Append(", "); Vec(sb, verts[i]); }
        sb.Append("], \"normals\": [");
        for (int i = 0; i < norms.Length; i++) { if (i > 0) sb.Append(", "); Vec(sb, norms[i]); }
        sb.Append("]}");
    }

    static void R6Files(StringBuilder sb)
    {
        sb.Append("[");
        bool any = false;
        string[] guids = AssetDatabase.FindAssets("r6");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path)) continue;
            string low = path.ToLowerInvariant();
            if (low.IndexOf("r6") < 0) continue;
            if (!low.EndsWith(".fbx") && !low.EndsWith(".asset")) continue;
            if (any) sb.Append(", ");
            any = true;
            sb.Append("{\"path\": \"").Append(Esc(path)).Append("\", \"sha256\": \"").Append(ShaFile(path)).Append("\"}");
        }
        sb.Append("]");
    }

    static bool Invoke(MethodInfo method, object target, object[] args, ref string error)
    {
        if (method == null) return false;
        try { method.Invoke(target, args); return true; }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            if (error.Length == 0) error = inner.GetType().Name + ": " + inner.Message;
            return false;
        }
    }

    static string ShaFile(string assetPath)
    {
        string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        if (!File.Exists(full)) return "";
        using (var sha = SHA256.Create())
        using (var fs = File.OpenRead(full))
        {
            var bytes = sha.ComputeHash(fs);
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }

    static void Mat(StringBuilder sb, Matrix4x4 m)
    {
        sb.Append("[");
        for (int i = 0; i < 16; i++) { if (i > 0) sb.Append(","); sb.Append(G(m[i])); }
        sb.Append("]");
    }

    static void Vec(StringBuilder sb, Vector3 v)
    {
        sb.Append("[").Append(G(v.x)).Append(",").Append(G(v.y)).Append(",").Append(G(v.z)).Append("]");
    }

    static void Quat(StringBuilder sb, Quaternion q)
    {
        sb.Append("[").Append(G(q.x)).Append(",").Append(G(q.y)).Append(",").Append(G(q.z)).Append(",").Append(G(q.w)).Append("]");
    }

    static string G(float v) { return v.ToString("G9", CultureInfo.InvariantCulture); }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "'");
    }
}

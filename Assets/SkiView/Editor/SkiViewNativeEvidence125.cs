using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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

    /// <summary>
    /// Static imported-mesh triangles for body LODs and vests. Does not simulate or bake the 39 poses and does not write core.jsonl.
    /// Edit mode does not select a player LOD.
    /// Unity.exe -batchmode -projectPath &lt;staged&gt; -executeMethod SkiViewNativeEvidence125.Topology -logFile native-topology.log -quit
    /// </summary>
    public static void Topology()
    {
        int code = 0;
        try { code = WriteTopology(); }
        catch (Exception ex)
        {
            Debug.LogError("[NativeEv] " + ex);
            code = 5;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int WriteTopology()
    {
        if (!File.Exists(ScenePath)) { Debug.LogError("[NativeEv] missing scene"); return 3; }
        EditorSceneManager.OpenScene(ScenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null || game.skier == null || game.rig == null || game.rig.animator == null)
        {
            Debug.LogError("[NativeEv] no skier rig");
            return 3;
        }
        string dir = Environment.GetEnvironmentVariable("SV_TOPOLOGY_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/native-evidence125"));
        Directory.CreateDirectory(dir);
        var setAthlete = game.GetType().GetMethod("SetAthlete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        var imported = new Dictionary<int, Mesh>();
        RememberImported(game, imported);
        if (setAthlete != null) setAthlete.Invoke(game, new object[] { 0 });
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"playMode\": false,\n");
        sb.Append("  \"playerLodSelection\": \"unknown\",\n");
        sb.Append("  \"note\": \"Edit mode does not choose a LOD. Slots below are membership only. Indices are the imported mesh, not an FBX control-point map and not a baked pose.\",\n");
        sb.Append("  \"baked\": false,\n");
        sb.Append("  \"posesSimulated\": false,\n");
        sb.Append("  \"coreJsonl\": \"not written\",\n");
        sb.Append("  \"cameras\": ");
        WriteCameras(sb);
        sb.Append(",\n  \"lodGroups\": ");
        WriteTopologyLods(sb, game);
        sb.Append(",\n  \"meshes\": [\n");
        var written = new HashSet<int>();
        bool any = false;
        any = WriteImported(sb, game, imported, written, any);
        sb.Append("\n  ]\n}\n");
        File.WriteAllText(Path.Combine(dir, "topology.json"), sb.ToString());
        Debug.Log("[NativeEv] topology " + dir + " meshes=" + written.Count + " playerLod=unknown sceneSaved=false");
        return 0;
    }

    static void RememberImported(SkiViewGame game, Dictionary<int, Mesh> imported)
    {
        void Take(Transform root)
        {
            if (root == null) return;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (!WantTopology(smr)) continue;
                imported[smr.GetInstanceID()] = smr.sharedMesh;
            }
        }
        Take(game.skier);
        Take(game.rig.animator.transform);
    }

    static bool WantTopology(SkinnedMeshRenderer smr)
    {
        string role = SkiViewCharacterCapture125Logic.MeshRole(smr.name, smr.sharedMesh.name);
        return role == "body" || role == "originalVest" || role == "otherVest";
    }

    static bool WriteImported(StringBuilder sb, SkiViewGame game, Dictionary<int, Mesh> imported, HashSet<int> written, bool any)
    {
        var meshes = new Dictionary<int, Mesh>();
        var owners = new Dictionary<int, List<SkinnedMeshRenderer>>();
        void Take(Transform root)
        {
            if (root == null) return;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null || !WantTopology(smr)) continue;
                Mesh mesh;
                if (!imported.TryGetValue(smr.GetInstanceID(), out mesh) || mesh == null) mesh = smr.sharedMesh;
                if (mesh.name.EndsWith(SkiViewBodyHeadCoverage.ClippedSuffix, StringComparison.Ordinal)) continue;
                int id = mesh.GetInstanceID();
                meshes[id] = mesh;
                List<SkinnedMeshRenderer> list;
                if (!owners.TryGetValue(id, out list))
                {
                    list = new List<SkinnedMeshRenderer>();
                    owners[id] = list;
                }
                bool seenRenderer = false;
                for (int i = 0; i < list.Count; i++) if (list[i].GetInstanceID() == smr.GetInstanceID()) { seenRenderer = true; break; }
                if (!seenRenderer) list.Add(smr);
            }
        }
        Take(game.skier);
        Take(game.rig.animator.transform);
        foreach (var pair in meshes)
        {
            if (!written.Add(pair.Key)) continue;
            if (any) sb.Append(",\n");
            any = true;
            WriteOneMesh(sb, owners[pair.Key], pair.Value);
        }
        return any;
    }

    static void WriteOneMesh(StringBuilder sb, List<SkinnedMeshRenderer> renderers, Mesh mesh)
    {
        var first = renderers[0];
        string path = AssetDatabase.GetAssetPath(mesh);
        string sha = ShaFile(path);
        string role = SkiViewCharacterCapture125Logic.MeshRole(first.name, mesh.name);
        sb.Append("    {\"meshId\": ").Append(mesh.GetInstanceID());
        sb.Append(", \"mesh\": \"").Append(Esc(mesh.name)).Append("\"");
        sb.Append(", \"role\": \"").Append(role).Append("\"");
        if (role == "originalVest" || role == "otherVest")
            sb.Append(", \"contentLabel\": \"").Append(SkiViewCharacterCapture125Logic.VestContentLabel(sha)).Append("\"");
        sb.Append(", \"sourcePath\": \"").Append(Esc(path)).Append("\"");
        sb.Append(", \"sourceSha256\": \"").Append(sha).Append("\"");
        sb.Append(", \"persistent\": ").Append(EditorUtility.IsPersistent(mesh) ? "true" : "false");
        string suffix = NameSuffix(mesh.name, first.name);
        sb.Append(", \"producer\": \"").Append(Esc(string.IsNullOrEmpty(path) ? (string.IsNullOrEmpty(suffix) ? "runtime mesh, asset path empty" : suffix) : "imported asset")).Append("\"");
        if (string.IsNullOrEmpty(path))
            sb.Append(", \"sourceSha256Reason\": \"AssetDatabase.GetAssetPath is empty. contentSha256 hashes positions in mesh vertex order plus indices. No FBX path is inferred.\"");
        sb.Append(", \"vertexCount\": ").Append(mesh.vertexCount);
        if (role == "body")
        {
            sb.Append(", \"vertexOrder\": \"mesh vertex index, the order the triangle indices address\"");
            sb.Append(", \"contentSha256\": \"").Append(ContentSha(mesh)).Append("\"");
            sb.Append(", \"positions\": [");
            WritePositions(sb, mesh);
            sb.Append("]");
        }
        sb.Append(", \"renderers\": [");
        for (int i = 0; i < renderers.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var smr = renderers[i];
            sb.Append("{\"id\": ").Append(smr.GetInstanceID());
            sb.Append(", \"name\": \"").Append(Esc(smr.name)).Append("\"");
            sb.Append(", \"enabled\": ").Append(smr.enabled ? "true" : "false").Append("}");
        }
        sb.Append("], \"submeshes\": [");
        WriteSubmeshes(sb, mesh);
        sb.Append("]}");
    }

    static void WriteSubmeshes(StringBuilder sb, Mesh mesh)
    {
        Mesh.MeshDataArray read = Mesh.AcquireReadOnlyMeshData(mesh);
        try
        {
            var data = read[0];
            bool wide = data.indexFormat == IndexFormat.UInt32;
            for (int s = 0; s < data.subMeshCount; s++)
            {
                if (s > 0) sb.Append(", ");
                var desc = data.GetSubMesh(s);
                int count = desc.indexCount;
                int tris = desc.topology == MeshTopology.Triangles ? count / 3 : 0;
                sb.Append("{\"submesh\": ").Append(s);
                sb.Append(", \"topology\": \"").Append(desc.topology).Append("\"");
                sb.Append(", \"indexCount\": ").Append(count);
                sb.Append(", \"triangles\": ").Append(tris);
                sb.Append(", \"baseVertex\": ").Append(desc.baseVertex);
                sb.Append(", \"indices\": [");
                int begin = desc.indexStart;
                if (wide)
                {
                    var raw = data.GetIndexData<int>();
                    for (int i = 0; i < count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append(raw[begin + i] + desc.baseVertex);
                    }
                }
                else
                {
                    var raw = data.GetIndexData<ushort>();
                    for (int i = 0; i < count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append(raw[begin + i] + desc.baseVertex);
                    }
                }
                sb.Append("]}");
            }
        }
        finally { read.Dispose(); }
    }

    static void WriteCameras(StringBuilder sb)
    {
        sb.Append("[");
        var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c == null) continue;
            if (i > 0) sb.Append(", ");
            sb.Append("{\"name\": \"").Append(Esc(c.name)).Append("\"");
            sb.Append(", \"enabled\": ").Append(c.enabled ? "true" : "false");
            sb.Append(", \"fov\": ").Append(G(c.fieldOfView));
            sb.Append(", \"near\": ").Append(G(c.nearClipPlane));
            sb.Append(", \"far\": ").Append(G(c.farClipPlane));
            sb.Append(", \"pos\": "); Vec(sb, c.transform.position);
            sb.Append(", \"lodSelection\": \"unknown\"}");
        }
        sb.Append("]");
    }

    static void WriteTopologyLods(StringBuilder sb, SkiViewGame game)
    {
        sb.Append("[");
        var seen = new HashSet<int>();
        bool any = false;
        void One(Transform root)
        {
            if (root == null) return;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
            {
                if (lg == null || !seen.Add(lg.GetInstanceID())) continue;
                if (any) sb.Append(", ");
                any = true;
                var lods = lg.GetLODs();
                sb.Append("{\"name\": \"").Append(Esc(lg.name)).Append("\", \"enabled\": ").Append(lg.enabled ? "true" : "false");
                sb.Append(", \"selectedLod\": \"unknown\", \"slots\": [");
                if (lods != null)
                {
                    for (int i = 0; i < lods.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append("{\"lod\": ").Append(i).Append(", \"renderers\": [");
                        var rs = lods[i].renderers;
                        if (rs != null)
                            for (int k = 0; k < rs.Length; k++)
                            {
                                if (k > 0) sb.Append(", ");
                                var r = rs[k];
                                sb.Append("{\"id\": ").Append(r != null ? r.GetInstanceID() : 0);
                                sb.Append(", \"name\": \"").Append(r != null ? Esc(r.name) : "").Append("\"}");
                            }
                        sb.Append("]}");
                    }
                }
                sb.Append("]}");
            }
        }
        One(game.skier);
        One(game.rig.animator.transform);
        sb.Append("]");
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
        string vestLabel = SkiViewCharacterCapture125Logic.VestContentLabel(ShaFile(VestFbx));
        var frames = new StringBuilder();
        int written = 0;
        var baked = new Mesh { name = "NativeEvBake" };
        for (int i = 0; i <= last; i++)
        {
            game.SimulateFor(1f / 60f);
            if (gear != null) Invoke(gear, game, null, ref driverError);
            if (!SkiViewCharacterCapture125Logic.IsCoreFrame(i)) continue;
            if (written > 0) frames.Append('\n');
            frames.Append(Frame(game, i, gear != null, baked, vestLabel));
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
        string vestSha = ShaFile(VestFbx);
        string vestLabel = SkiViewCharacterCapture125Logic.VestContentLabel(vestSha);
        sb.Append("  \"bodySha256\": \"").Append(ShaFile(BodyFbx)).Append("\",\n");
        sb.Append("  \"vestSha256\": \"").Append(vestSha).Append("\",\n");
        sb.Append("  \"vestSlotPath\": \"").Append(Esc(VestFbx)).Append("\",\n");
        sb.Append("  \"vestSlotLabel\": \"").Append(vestLabel).Append("\",\n");
        sb.Append("  \"expectedOriginalVestSha256\": \"").Append(SkiViewCharacterCapture125Logic.ExpectedOriginalVestSha256).Append("\",\n");
        sb.Append("  \"expectedR6Sha256\": \"").Append(SkiViewCharacterCapture125Logic.ExpectedR6Sha256Prefix).Append("...").Append(SkiViewCharacterCapture125Logic.ExpectedR6Sha256Suffix).Append("\",\n");
        sb.Append("  \"originalVestSha256\": \"").Append(vestSha).Append("\",\n");
        sb.Append("  \"originalVestSha256Meaning\": \"same bytes as vestSha256. vestSlotLabel is the hash identity, not the file name\",\n");
        sb.Append("  \"driverError\": \"").Append(Esc(driverError)).Append("\",\n");
        sb.Append("  \"sceneSaved\": false\n}\n");
        return sb.ToString();
    }

    static string Frame(SkiViewGame game, int index, bool driver, Mesh baked, string vestLabel)
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
        any = AppendMeshes(sb, game.skier, baked, seen, any, vestLabel);
        if (game.rig.animator != null) any = AppendMeshes(sb, game.rig.animator.transform, baked, seen, any, vestLabel);
        sb.Append("]}");
        return sb.ToString();
    }

    static bool AppendMeshes(StringBuilder sb, Transform root, Mesh baked, HashSet<int> seen, bool any, string vestLabel)
    {
        if (root == null) return any;
        foreach (var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (sm == null || sm.sharedMesh == null || !seen.Add(sm.GetInstanceID())) continue;
            string role = SkiViewCharacterCapture125Logic.MeshRole(sm.name, sm.sharedMesh.name);
            if (role == "other" || role == "otherVest") continue;
            if (any) sb.Append(", ");
            any = true;
            WriteMesh(sb, sm, role, baked, vestLabel);
        }
        return any;
    }

    static void WriteMesh(StringBuilder sb, SkinnedMeshRenderer sm, string role, Mesh baked, string vestLabel)
    {
        var mesh = sm.sharedMesh;
        bool bake = SkiViewCharacterCapture125Logic.BakeRole(role, sm.name, mesh.name);
        sb.Append("{\"id\": ").Append(sm.GetInstanceID());
        sb.Append(", \"role\": \"").Append(role).Append("\"");
        if (role == "originalVest" || role == "otherVest") sb.Append(", \"contentLabel\": \"").Append(vestLabel).Append("\"");
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

    static string NameSuffix(string meshName, string rendererName)
    {
        if (string.IsNullOrEmpty(meshName) || string.IsNullOrEmpty(rendererName)) return "";
        if (!meshName.StartsWith(rendererName, StringComparison.Ordinal) || meshName.Length <= rendererName.Length) return "";
        return meshName.Substring(rendererName.Length).TrimStart('_');
    }

    static string ContentSha(Mesh mesh)
    {
        Mesh.MeshDataArray read = Mesh.AcquireReadOnlyMeshData(mesh);
        try
        {
            var data = read[0];
            using (var ms = new MemoryStream())
            using (var sha = SHA256.Create())
            {
                int vc = data.vertexCount;
                var pos = new NativeArray<Vector3>(vc, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                data.GetVertices(pos);
                for (int i = 0; i < vc; i++)
                {
                    ms.Write(BitConverter.GetBytes(pos[i].x), 0, 4);
                    ms.Write(BitConverter.GetBytes(pos[i].y), 0, 4);
                    ms.Write(BitConverter.GetBytes(pos[i].z), 0, 4);
                }
                pos.Dispose();
                for (int s = 0; s < data.subMeshCount; s++)
                {
                    var desc = data.GetSubMesh(s);
                    int begin = desc.indexStart;
                    if (data.indexFormat == IndexFormat.UInt32)
                    {
                        var raw = data.GetIndexData<int>();
                        for (int i = 0; i < desc.indexCount; i++) ms.Write(BitConverter.GetBytes(raw[begin + i] + desc.baseVertex), 0, 4);
                    }
                    else
                    {
                        var raw = data.GetIndexData<ushort>();
                        for (int i = 0; i < desc.indexCount; i++) ms.Write(BitConverter.GetBytes(raw[begin + i] + desc.baseVertex), 0, 4);
                    }
                }
                ms.Position = 0;
                var hash = sha.ComputeHash(ms);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
        finally { read.Dispose(); }
    }

    static void WritePositions(StringBuilder sb, Mesh mesh)
    {
        Mesh.MeshDataArray read = Mesh.AcquireReadOnlyMeshData(mesh);
        try
        {
            var data = read[0];
            int vc = data.vertexCount;
            var pos = new NativeArray<Vector3>(vc, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            data.GetVertices(pos);
            for (int i = 0; i < vc; i++)
            {
                if (i > 0) sb.Append(",");
                Vec(sb, pos[i]);
            }
            pos.Dispose();
        }
        finally { read.Dispose(); }
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

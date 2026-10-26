using System.Collections.Generic;
using System.Linq;
using SkiView;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 18: draw-call reduction for phones (static/rigid merges, no-reflect tagging) and the first-person arms rig.</summary>
public static partial class SkiViewBuilder
{
    static string meshTag = "home";

    /// <summary>Pass 18: LakeDetail on the home lake (terrain trees: plant prototypes are the understory kinds).</summary>
    static void HomeLakeDetail(Transform root, Venue venue)
    {
        var terrain = root.GetComponentInChildren<Terrain>(); if (terrain == null) return;
        var water = root.Find("Water"); var b = water != null ? water.GetComponent<Renderer>().bounds : new Bounds(Vector3.zero, new Vector3(800, 10, 800));
        var det = root.gameObject.GetComponent<LakeDetail>() ?? root.gameObject.AddComponent<LakeDetail>();
        det.terrain = terrain; det.lakeBounds = new Vector4(b.min.x, b.max.x, b.min.z, b.max.z);
        var protos = terrain.terrainData.treePrototypes; var small = new List<int>();
        for (int i = 0; i < protos.Length; i++) { var pf = protos[i].prefab; if (pf == null) continue; var rr = pf.GetComponentsInChildren<Renderer>(); if (rr.Length == 0) continue; var bb = rr[0].bounds; foreach (var r in rr) bb.Encapsulate(r.bounds); if (bb.size.y < 2.5f) small.Add(i); }
        det.plantPrototypes = small.ToArray();
        Debug.Log($"[SkiView] home LakeDetail: bounds {det.lakeBounds}, {small.Count} small plant prototypes of {protos.Length}");
    }

    /// <summary>Merge every MeshRenderer under root into one mesh per (anchor, material, shadow mode, layer). The anchor of a renderer is
    /// the nearest ancestor (or itself) in anchors (bones, toggled roots, moving roots); root is always an anchor. Renderers under
    /// a LODGroup, with a mirrored transform or listed in skip are left alone. GameObjects and colliders stay; only the
    /// MeshRenderer + MeshFilter of merged pieces go. Returns (before, after) draw items.</summary>
    static bool KitSkiPart(Transform t) { for (; t != null; t = t.parent) if (t.name == "SkiBody" || t.name == "Fin" || t.name == "FinWing" || t.name == "FinBlock") return true; return false; }

    public static (int, int) MergeRig(Transform root, IEnumerable<Transform> anchors, string prefix, ICollection<Transform> skip = null)
    {
        var aset = new HashSet<Transform>(anchors.Where(a => a != null)) { root };
        var groups = new Dictionary<(Transform, Material, ShadowCastingMode, int), List<CombineInstance>>();
        var done = new List<MeshRenderer>(); int before = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (skip != null && skip.Contains(r.transform)) continue;
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || r.GetComponentInParent<LODGroup>(true) != null) continue;
            if (r.GetComponent<ParticleSystem>() != null || !r.enabled) continue;
            if (!r.gameObject.activeInHierarchy && KitSkiPart(r.transform)) continue;   // b9: the inactive CLASSIC kit ski stays separate (runtime toggle)
            Transform a = r.transform; while (a != null && !aset.Contains(a)) a = a.parent; if (a == null) continue;
            var m = a.worldToLocalMatrix * r.transform.localToWorldMatrix; if (m.determinant <= 0f) continue;
            var mats = r.sharedMaterials; int n = Mathf.Min(mats.Length, mf.sharedMesh.subMeshCount); before += n;
            for (int s = 0; s < n; s++)
            {
                var key = (a, mats[s], r.shadowCastingMode, r.gameObject.layer);
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<CombineInstance>();
                l.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = m });
            }
            done.Add(r);
        }
        int gi = 0;
        foreach (var kv in groups)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = $"{prefix}_{gi}" };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true); mesh.RecalculateBounds();
            var go = new GameObject("Merged_" + (kv.Key.Item2 != null ? kv.Key.Item2.name : "none")); go.transform.SetParent(kv.Key.Item1, false);
            go.layer = kv.Key.Item4;
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, mesh.name);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = kv.Key.Item2; mr.shadowCastingMode = kv.Key.Item3;
            gi++;
        }
        foreach (var r in done) { var mf = r.GetComponent<MeshFilter>(); Object.DestroyImmediate(r); if (mf != null) Object.DestroyImmediate(mf); }
        return (before, gi);
    }

    /// <summary>Small pieces (bounds diagonal under maxSize) go on the no-reflect layer, so the planar reflection skips them.
    /// Buoys and names in keep stay reflected.</summary>
    public static int TagNoReflect(Transform root, float maxSize = 0.9f)
    {
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.gameObject.layer != 0) continue;
            string nm = r.name.ToLowerInvariant(); if (nm.Contains("buoy") || nm.Contains("ball")) continue;
            if (r.bounds.size.magnitude < maxSize) { r.gameObject.layer = PlanarReflection.NO_REFLECT_LAYER; n++; }
        }
        return n;
    }

    /// <summary>Skier + rope-side draw merges, run on the home scene once the skier, jump skis and helmet exist (lakes reuse the rig).</summary>
    static void MergeSkier(SkiViewGame game)
    {
        var bones = new List<Transform>();
        foreach (var smr in game.skier.GetComponentsInChildren<SkinnedMeshRenderer>(true)) bones.AddRange(smr.bones);
        var anim = game.rig != null ? game.rig.animator : null;
        if (anim != null) foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones))) { if (b == HumanBodyBones.LastBone) continue; var t = anim.GetBoneTransform(b); if (t != null) bones.Add(t); }
        // pass 18: body layer (hidden from the first-person eye): everything on the skier except the ski itself (boots included)
        int BODY = PlanarReflection.SKIER_BODY_LAYER;
        foreach (var t in game.skier.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = BODY;
        if (game.handle != null) game.handle.gameObject.layer = BODY;
        var bootRoots = new List<Transform>();
        foreach (var sk in new[] { game.ski, game.jumpSkiL, game.jumpSkiR })
        {
            if (sk == null) continue;
            foreach (var bn in new[] { "FrontBoot", "RearBoot", "Boot" }) { var b = sk.Find(bn); if (b != null) bootRoots.Add(b); }
            foreach (var t in sk.GetComponentsInChildren<Transform>(true)) { bool boot = false; for (var u = t; u != null && u != sk; u = u.parent) if (bootRoots.Contains(u)) { boot = true; break; } if (!boot) t.gameObject.layer = 0; }
        }
        var extra = new List<Transform>(bones) { game.ski, game.jumpSkiL, game.jumpSkiR, game.helmet != null ? game.helmet.transform : null };
        var (b0, a0) = MergeRig(game.skier, extra, "skier_merge");
        Debug.Log($"[SkiView] skier merged: {b0} -> {a0} draw items (bones, ski, jump skis, helmet kept as anchors)");
    }

}

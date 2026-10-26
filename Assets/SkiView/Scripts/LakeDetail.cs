using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkiView
{
    /// <summary>
    /// Pass 17: plant density / LOD scaling for the dense lake forests (Cedar Bend has ~93k terrain tree instances).
    /// Tier 0 = desktop (everything), 1 = desktop browser (WebGL), 2 = phone. On tiers 1-2 a runtime copy of the TerrainData
    /// keeps every tree near the water (inside the lake bounds + nearBand), thins the far forest to farKeep (survivors are
    /// scaled up a little so the canopy stays closed from the boat), drops small plants (ferns / grass / reeds / brambles)
    /// further than plantBand from the water, and lowers the tree draw distance and LOD bias. The reflection camera renders
    /// at an extra-low LOD bias on phones. The source asset is never modified.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class LakeDetail : MonoBehaviour
    {
        public Terrain terrain;
        public Vector4 lakeBounds;                 // xmin, xmax, zmin, zmax (world)
        public int[] plantPrototypes = new int[0];  // small plants (culled first)
        public Renderer[] smallObjects = new Renderer[0];   // pass 18: decorative meshes dropped on phones (Spanish moss)
        public float nearBand = 140f;
        [Header("Tier 1 (WebGL desktop)")] public float webFarKeep = 0.6f, webPlantBand = 90f, webTreeDistance = 2000f, webLodBias = 0.8f;
        [Header("Tier 2 (phone)")] public float mobileFarKeep = 0.3f, mobilePlantBand = 40f, mobileTreeDistance = 1300f, mobileLodBias = 0.55f, mobileReflLodBias = 0.3f, mobileShadowDistance = 70f;
        public int mobileShadowCascades = 1;
        public bool foliageA2C;                   // T2: alpha-to-coverage foliage edges (SkiView/VegWind) on this lake, desktop + browser tiers only
        public GameObject[] phonePrototypes = new GameObject[0];   // T2: per tree prototype, the non-cross-fade prefab the phone tier swaps in (null = keep)
        public int forceTier = -1;                 // captures / perf stats: -1 = auto
        public static int LastKept, LastTotal;
        float savedLod = -1f, savedShadowDist = -1f; int savedCascades = -1;

        public static int AutoTier => Application.isMobilePlatform ? 2 : (Application.platform == RuntimePlatform.WebGLPlayer ? 1 : 0);
        public int Tier => forceTier >= 0 ? forceTier : AutoTier;

        void Awake() { if (Application.isPlaying) Apply(Tier); }

        // pass 19: global wind for the vegetation shader (dir xz, sway m at R = 1, flutter m); phones skip it (SV_WIND_OFF)
        public Vector4 wind = new Vector4(0.8f, 0.6f, 0.3f, 0.035f);
        public Vector4 foliageRim;                // lake P1: SkiView/VegWind warm fresnel rim on sun-facing foliage (rgb, strength); 0 = off
        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeginCam; RenderPipelineManager.endCameraRendering += EndCam; Shader.SetGlobalVector("_SVWind", wind); Shader.DisableKeyword("SV_WIND_OFF"); Shader.SetGlobalFloat("_SV_A2C", foliageA2C && Tier < 2 ? 1f : 0f); Shader.SetGlobalVector("_SV_FoliageRim", foliageRim); }
        void OnDestroy()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp != null && savedShadowDist >= 0f) { urp.shadowDistance = savedShadowDist; urp.shadowCascadeCount = savedCascades; savedShadowDist = -1f; }
        }
        void OnDisable() { Shader.SetGlobalFloat("_SV_A2C", 0f); Shader.SetGlobalVector("_SV_FoliageRim", Vector4.zero); RenderPipelineManager.beginCameraRendering -= BeginCam; RenderPipelineManager.endCameraRendering -= EndCam; }
        void BeginCam(ScriptableRenderContext c, Camera cam)
        {
            if (Tier < 2 || cam.name != "PlanarReflectionCamera") return;
            savedLod = QualitySettings.lodBias; QualitySettings.lodBias = mobileReflLodBias;
        }
        void EndCam(ScriptableRenderContext c, Camera cam) { if (savedLod >= 0f) { QualitySettings.lodBias = savedLod; savedLod = -1f; } }

        /// <summary>Apply a tier (0 restores nothing: call on a fresh scene). Works on a runtime copy of the TerrainData.</summary>
        public void Apply(int tier)
        {
            if (terrain == null) terrain = GetComponentInChildren<Terrain>();
            float densT6 = SkiViewQuality.TreeKeep;   // T6: Low/Med thin the far trees further (High = the tier as before)
            if (terrain == null || (tier <= 0 && densT6 >= 0.999f)) return;
            if (smallObjects != null) foreach (var r in smallObjects) if (r != null) r.enabled = tier < 2;
            float farKeep = (tier <= 0 ? 1f : tier == 1 ? webFarKeep : mobileFarKeep) * densT6, plantBand = tier <= 0 ? float.MaxValue : tier == 1 ? webPlantBand : mobilePlantBand;
            var src = terrain.terrainData;
            var td = Instantiate(src); td.name = src.name + " (tier " + tier + ")";
            Vector3 tp = terrain.transform.position, size = td.size;
            var plants = new HashSet<int>(plantPrototypes);
            var all = td.treeInstances; var keep = new List<TreeInstance>(all.Length);
            float grow = 1f / Mathf.Sqrt(Mathf.Max(0.2f, farKeep));   // survivors cover the same canopy area
            grow = Mathf.Min(grow, 1.35f);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                float x = tp.x + t.position.x * size.x, z = tp.z + t.position.z * size.z;
                float dx = Mathf.Max(0f, Mathf.Max(lakeBounds.x - x, x - lakeBounds.y)), dz = Mathf.Max(0f, Mathf.Max(lakeBounds.z - z, z - lakeBounds.w));
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (plants.Contains(t.prototypeIndex)) { if (d > plantBand) continue; keep.Add(t); continue; }
                if (d <= nearBand * SkiViewQuality.NearBand) { keep.Add(t); continue; }   // T6: Low/Med narrow the keep-all shore band
                uint h = (uint)(i * 2654435761u); h ^= h >> 15; float r = (h & 0xFFFF) / 65535f;
                float k = Mathf.Lerp(1f, farKeep, Mathf.Clamp01((d - nearBand * SkiViewQuality.NearBand) / 120f));   // fade the thinning in over 120 m
                if (r > k) continue;
                if (k < 0.999f) { float g = Mathf.Lerp(1f, grow, 1f - k); t.heightScale *= Mathf.Lerp(1f, g, 0.5f); t.widthScale *= g; }
                keep.Add(t);
            }
            td.SetTreeInstances(keep.ToArray(), false);
            if (tier >= 2 && phonePrototypes != null && phonePrototypes.Length > 0)   // T2: phones keep plain LOD switching (no cross-fade double draw)
            {
                var pp = td.treePrototypes;
                for (int k = 0; k < pp.Length && k < phonePrototypes.Length; k++) if (phonePrototypes[k] != null) pp[k].prefab = phonePrototypes[k];
                td.treePrototypes = pp; td.RefreshPrototypes();
            }
            terrain.terrainData = td;
            var col = terrain.GetComponent<TerrainCollider>(); if (col != null) col.terrainData = td;
            if (tier <= 0) { LastKept = keep.Count; LastTotal = all.Length; return; }   // T6: desktop Low/Med only thin; draw distance and LOD stay
            terrain.treeDistance = tier == 1 ? webTreeDistance : mobileTreeDistance;
            terrain.treeLODBiasMultiplier = tier == 1 ? webLodBias : mobileLodBias;
            if (tier == 2) Shader.EnableKeyword("SV_WIND_OFF");
            if (tier == 2)
            {   // phones: shadows only near the skier/boat (the far forest shadows were the biggest extra pass), one cascade
                var urp = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (urp != null && savedShadowDist < 0f) { savedShadowDist = urp.shadowDistance; savedCascades = urp.shadowCascadeCount; urp.shadowDistance = Mathf.Min(urp.shadowDistance, mobileShadowDistance); urp.shadowCascadeCount = mobileShadowCascades; }
            }
            LastKept = keep.Count; LastTotal = all.Length;
            Debug.Log($"[SkiView] lake detail tier {tier}: {keep.Count}/{all.Length} plant instances kept (far keep {farKeep:0.00}, plants within {plantBand:0} m)");
        }
    }
}

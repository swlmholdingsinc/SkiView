using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SkiView
{
    /// <summary>
    /// Mobile-friendly planar reflection for the lake (y = waterY). A second camera renders the mirrored view into a
    /// reduced-resolution RT (1/4 res on the Mobile tier, 1/2 otherwise) that the water shader samples in screen space.
    /// The reflection camera uses a proper rotation (no negative-scale matrix, so no culling flip): its image is the mirror
    /// image flipped horizontally, which the shader undoes by sampling u' = 1 - u. An oblique near plane clips everything
    /// below the waterline. Water is on layer 4 ("Water") and excluded from the reflection.
    /// </summary>
    [ExecuteAlways, DefaultExecutionOrder(1000)]
    public class PlanarReflection : MonoBehaviour
    {
        /// <summary>Pass 18: small pieces (no reflection) and the first-person arms rig.</summary>
        public const int NO_REFLECT_LAYER = 30, FP_LAYER = 31, SKIER_BODY_LAYER = 29;
        public Camera source;
        public float waterY = 0f, clipOffset = 0.05f;
        [Range(0.1f, 1f)] public float resolutionScale = 0.5f, mobileResolutionScale = 0.25f;
        public LayerMask reflectLayers = ~(1 << 4);
        public float farClip = 1600f;
        [Tooltip("Manual vertical flip of the reflection lookup, for on-device checks if a GLES/Vulkan/Metal device shows the mirror upside down. " +
                 "The shader already auto-flips when the water draws straight to the back buffer on top-left-origin APIs. Untested on a device.")]
        public bool forceFlipY = false;
        Camera reflCam; RenderTexture rt;
        static readonly int ID_Tex = Shader.PropertyToID("_PlanarReflectionTex"), ID_On = Shader.PropertyToID("_PlanarReflectionOn"), ID_Flip = Shader.PropertyToID("_PlanarReflectionFlipY");

        void OnDisable()
        {
            Shader.SetGlobalFloat(ID_On, 0f);
            if (rt != null) { rt.Release(); DestroyImmediate(rt); rt = null; }
            if (reflCam != null) DestroyImmediate(reflCam.gameObject);
        }

        bool IsMobileTier => Application.isMobilePlatform || QualitySettings.GetQualityLevel() == 0;

        void Ensure()
        {
            if (source == null) source = Camera.main;
            if (source == null) return;
            // pass 17: desktop browsers (WebGL) get the half-res reflection; quarter res only on phones (WebGL at 1/4 read blocky)
            float sc = Application.isMobilePlatform ? mobileResolutionScale : (Application.platform == RuntimePlatform.WebGLPlayer ? Mathf.Max(0.5f, resolutionScale) : (IsMobileTier ? mobileResolutionScale : resolutionScale));
            sc *= SkiViewQuality.ReflectionScale;   // T6: Low 0.5x, Med 0.75x, High 1x of the tier scale
            int w = Mathf.Max(64, (int)(source.pixelWidth * sc)), h = Mathf.Max(64, (int)(source.pixelHeight * sc));
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { rt.Release(); DestroyImmediate(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "PlanarReflection", useMipMap = false, antiAliasing = 1, hideFlags = HideFlags.DontSave };
                rt.Create();
            }
            if (reflCam == null)
            {
                var go = new GameObject("PlanarReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
                reflCam = go.AddComponent<Camera>();
                var d = go.AddComponent<UniversalAdditionalCameraData>();
                d.renderPostProcessing = false; d.renderShadows = !IsMobileTier; d.requiresDepthTexture = false; d.requiresColorTexture = false;
                d.antialiasing = AntialiasingMode.None;
                reflCam.enabled = false;   // rendered explicitly (SubmitRenderRequest) each frame / each capture
            }
        }

        public void UpdatePose()
        {
            Ensure();
            if (reflCam == null || source == null) return;
            reflCam.CopyFrom(source);
            reflCam.enabled = false; reflCam.targetTexture = rt; reflCam.cullingMask = reflectLayers & ~((1 << NO_REFLECT_LAYER) | (1 << FP_LAYER)); reflCam.farClipPlane = farClip;
            reflCam.allowMSAA = false; reflCam.allowHDR = true; reflCam.depth = source.depth - 10;
            Transform s = source.transform;
            Vector3 p = s.position; p.y = 2f * waterY - p.y;
            Vector3 f = s.forward; f.y = -f.y; Vector3 u = s.up; u.y = -u.y;
            reflCam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(f, u));
            reflCam.ResetProjectionMatrix();
            // Projection-flip notes: the mirror is a proper rotation (no negative-scale matrix), so culling/winding never inverts.
            // The oblique matrix below is the API-neutral (GL-style) projection; URP converts it with GL.GetGPUProjectionMatrix
            // per target (render-into-texture flip on D3D/Vulkan/Metal), so we must NOT pre-flip it here. The shader compensates
            // for the back-buffer case (see SkiViewWater.shader, UNITY_UV_STARTS_AT_TOP).
            // oblique near plane at the water surface (keep what's above it)
            Matrix4x4 w2c = reflCam.worldToCameraMatrix;
            Vector3 cp = w2c.MultiplyPoint(new Vector3(0, waterY + clipOffset, 0));
            Vector3 cn = w2c.MultiplyVector(Vector3.up).normalized;
            reflCam.projectionMatrix = reflCam.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn)));
        }

        /// <summary>Render the reflection now (also used by the headless capture before each shot).</summary>
        public void RenderNow()
        {
            UpdatePose();
            if (reflCam == null || rt == null) return;
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(reflCam, req)) RenderPipeline.SubmitRenderRequest(reflCam, req);
            Shader.SetGlobalTexture(ID_Tex, rt); Shader.SetGlobalFloat(ID_On, 1f); Shader.SetGlobalFloat(ID_Flip, forceFlipY ? 1f : 0f);
        }

        void LateUpdate()
        {
            // play mode: once per frame after the game camera has moved (execution order 1000), outside the render loop
            if (!Application.isPlaying) return;
            if (!SkiViewQuality.Reflections) { Shader.SetGlobalFloat(ID_On, 0f); return; }   // T6: water-reflections switch (persisted)
            RenderNow();
        }
    }
}

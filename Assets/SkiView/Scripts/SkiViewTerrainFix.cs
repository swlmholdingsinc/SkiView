using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkiView
{
    /// <summary>Ground fix: beyond basemapDistance URP terrain draws a runtime basemap that goes white/grey
    /// (banks read as snow/water). Keep full splat at all distances. b10.21: always disable drawInstanced
    /// (b10.7 was WebGL-only; Scott still saw washed banks). Runs on every scene load incl. Addressables.</summary>
    public static class SkiViewTerrainFix
    {
        public const float BASEMAP_DISTANCE = 20000f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; Apply(); }

        static void OnLoaded(Scene s, LoadSceneMode m) => Apply();

        public static void Apply()
        {
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.basemapDistance < BASEMAP_DISTANCE) t.basemapDistance = BASEMAP_DISTANCE;
                if (t.drawInstanced) t.drawInstanced = false;   // b10.21: always off
            }
        }
    }
}

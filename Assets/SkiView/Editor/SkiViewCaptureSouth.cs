using System;
using System.IO;
using SkiView;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>South-end aerials (lot 17, lot 18, community basin and warming hut): a top-down and an oblique view,
/// written to Screens/southend/aerial_&lt;tag&gt;_{top,oblique,hut}.png. Tag from env SV_TAG (before / after).</summary>
public static partial class SkiViewCapture
{
    public static void AerialSouth()
    {
        string tag = Environment.GetEnvironmentVariable("SV_TAG") ?? "after";
        Directory.CreateDirectory(Path.Combine(OutDir, "southend"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var g = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); g.Init(); g.SimulateFor(0.5f);
        var cam = g.cam; var c = new Vector3(5f, 0f, -300f);
        Shot(cam, g, $"southend/aerial_{tag}_top", () => { cam.fieldOfView = 50f; cam.farClipPlane = 3000f; cam.transform.position = c + Vector3.up * 150f; cam.transform.rotation = Quaternion.Euler(89.5f, 0f, 0f); });
        Shot(cam, g, $"southend/aerial_{tag}_oblique", () => { cam.fieldOfView = 50f; cam.transform.position = new Vector3(-95f, 70f, -230f); cam.transform.LookAt(new Vector3(0f, 0f, -310f)); });
        Shot(cam, g, $"southend/aerial_{tag}_hut", () => { cam.fieldOfView = 45f; cam.transform.position = new Vector3(-60f, 22f, -275f); cam.transform.LookAt(new Vector3(-20f, 2f, -318f)); });
        Shot(cam, g, $"southend/aerial_{tag}_lifts", () => { cam.fieldOfView = 40f; cam.transform.position = new Vector3(-30f, 14f, -330f); cam.transform.LookAt(new Vector3(-50f, 1f, -347f)); });
        var se = GameObject.Find("SouthEnd_Basin_Hut_Dock");
        if (se != null) foreach (var r in se.GetComponentsInChildren<MeshRenderer>(true)) if (r.name == "Boat" || r.name == "Canopy") { var mf = r.GetComponent<MeshFilter>(); Debug.Log($"[SkiView] south probe {r.transform.parent.name}/{r.name} en={r.enabled} act={r.gameObject.activeInHierarchy} mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name + " v" + mf.sharedMesh.vertexCount : "NULL")} b={r.bounds.center}"); }
        Debug.Log("[SkiView] south aerials done " + tag);
    }
}

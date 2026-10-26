using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Ski with Wade T3: placeholder brand slots are ship:false. A release build (SV_RELEASE=1, non-development)
/// fails while Resources/Branding/slots/slots.json still lists any ship:false slot, so no placeholder ships by accident.</summary>
public class SkiViewBrandGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        bool release = System.Environment.GetEnvironmentVariable("SV_RELEASE") == "1" && (report.summary.options & UnityEditor.BuildOptions.Development) == 0;
        // T8 (13) + owner decision Oct 23: Wade branding/likeness are always on, so every release build fails until the owner confirms the license is signed
        var fl = SkiView.SkiViewBranding.Flag;
        if (release && (fl.wadeLikenessEnabled || fl.wadeBrandingEnabled) && !fl.wadeLicenseSigned)
            throw new BuildFailedException("Wade likeness/branding is on and branding.json wadeLicenseSigned is false. Builds stay INTERNAL until the owner confirms the license is signed.");
        UnityEditor.PlayerSettings.productName = SkiView.SkiViewBranding.ProductName;   // app/window name from branding.json in every build
        string p = Path.Combine(Application.dataPath, "SkiView/Resources/Branding/slots/slots.json");
        if (!File.Exists(p)) return;
        bool placeholders = File.ReadAllText(p).Replace(" ", "").Contains("\"ship\":false");
        if (placeholders && release) throw new BuildFailedException("Brand slots still hold DO-NOT-SHIP placeholders (slots.json ship:false). Replace them before a release build.");
        if (placeholders) Debug.Log("[brand-guard] placeholder brand slots present (internal build OK)");
    }
}

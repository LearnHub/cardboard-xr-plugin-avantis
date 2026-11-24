#if UNITY_EDITOR


using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management.Metadata;
using System.Globalization;

namespace Google.XR.Cardboard.Editor
{
    public class CardboardXRSettingsBaker : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Without the Cardboard loader the baked values are never read.
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(report.summary.platform);
            if (!XRPackageMetadataStore.IsLoaderAssigned(typeof(XRLoader).FullName, targetGroup))
            {
                return;
            }

            // Pull current values from XR Plugin Management UI
            if (!EditorBuildSettings.TryGetConfigObject("Google.XR.Cardboard.XRSettings", out XRSettings s) || s == null)
            {
                // Safe defaults if not configured
                WriteBakedClass(false, 0.5f, 0.24f, false, true);
                return;
            }

            WriteBakedClass(s.isSPI, s.resolutionScale, s.sharpeningValue, s.reprojection, s.tiltToSpin);
        }

        private static void WriteBakedClass(bool isSPI, float scale, float sharpen, bool reprojection, bool tiltToSpin)
        {
            // Find the existing runtime file
            string[] guids = AssetDatabase.FindAssets("BakedXRSettings t:Script");
            if (guids.Length == 0)
            {
                throw new FileNotFoundException("BakedXRSettings.cs not found in project.");
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);

            var src =
        $@"namespace Google.XR.Cardboard.Internal
{{
    // Auto-generated at build time. Do not edit.
    internal static class BakedXRSettings
    {{
        public const bool SPI = {(isSPI ? "true" : "false")};
        public const float EyeInternalScale = {(scale/100f).ToString(CultureInfo.InvariantCulture)}f;
        public const float SharpeningValue  = {sharpen.ToString(CultureInfo.InvariantCulture)}f;
        public const bool Reprojection = {(reprojection ? "true" : "false")};
        public const bool TiltToSpin = {(tiltToSpin ? "true" : "false")};
    }}
}}";

            // Importing queues a script compile, which a failed build doesn't absorb: the Editor
            // then reloads and can clear the build errors from the console. Line endings are
            // ignored, as they vary with how the package was checked out.
            if (File.Exists(path) && NormaliseLineEndings(File.ReadAllText(path)) == NormaliseLineEndings(src))
            {
                return;
            }

            File.WriteAllText(path, src);
            AssetDatabase.ImportAsset(path);
        }

        private static string NormaliseLineEndings(string text)
        {
            return text.Replace("\r\n", "\n");
        }
    }
}
#endif

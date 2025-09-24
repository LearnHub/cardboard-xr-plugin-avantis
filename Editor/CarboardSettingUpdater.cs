#if UNITY_EDITOR


using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Google.XR.Cardboard.Editor
{
    public class CardboardXRSettingsBaker : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Pull current values from XR Plugin Management UI
            if (!EditorBuildSettings.TryGetConfigObject("Google.XR.Cardboard.XRSettings", out XRSettings s) || s == null)
            {
                // Safe defaults if not configured
                WriteBakedClass(false, 0.5f, 0.24f);
                return;
            }

            WriteBakedClass(s.isSPI, s.resolutionScale, s.sharpeningValue);
        }

        private static void WriteBakedClass(bool isSPI, float scale, float sharpen)
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
        public const float EyeInternalScale = {scale/100}f;
        public const float SharpeningValue  = {sharpen}f;
    }}
}}";

            File.WriteAllText(path, src);
            AssetDatabase.ImportAsset(path);
        }
    }
}
#endif

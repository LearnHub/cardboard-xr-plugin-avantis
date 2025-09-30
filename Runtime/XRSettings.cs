using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Management;
using static Google.XR.Cardboard.XRSettings;
using static UnityEngine.GraphicsBuffer;
using UnityEditor;

namespace Google.XR.Cardboard
{
    [XRConfigurationData("Cardboard XR", "Google.XR.Cardboard.XRSettings")]
    [System.Serializable]
    public class XRSettings : ScriptableObject
    {
        [TextArea(3, 21)]
        [Tooltip("Resolution Settings")]
        public string description = "Single Stereo Pass Support:\n" +
        "- Requires OpenGLES 3.x Renderer (Vulkan is not supported)\n" +
        "- Works with Unity's Universal Render Pipeline Shaders\n" +
        "- Works with Unity's Built-in Render Pipeline Shaders\n" +
        "- Universal Render Pipeline (URP)Shader Graph shaders are supported automatically.\n" +
        "- For Built-in Render Pipeline, Shader Graph shader need to be coded with Multiview extension support\n\n" +
        "Resolution Preset: Recommended value is Balanced. (Above, the fragment cost and fillrate is exponentially higher for dimminishing returns.)\n" +
        "Available Presets:\n" +
        "- Full - 100% Resolution\n" +
        "- Quality - 66% Resolution\n" +
        "- Balanced - 58% Resolution\n" +
        "- Performance - 50% Resolution\n" +
        "- Ultra Performance - 33% Resolution\n" +
        "- Manual - Allows Custom Resolution Scale\n\n" +
        "Sharpenning:\n" +
        "- The optimal sharpenning value is defined for each preset. It can also be manually modified.";

       
        public enum RenderingPreset 
        {
            SinglePass = 1,
            MultiPass = 0
        }

        [Tooltip("Rendering Method")]
        public RenderingPreset renderingMode = RenderingPreset.SinglePass;
        [HideInInspector]
        public bool isSPI = false;

        public enum ResolutionPreset
        {
            Full = 100,
            Quality = 66,
            Balanced = 58,
            Performance = 50,
            UltraPerformance = 33,
            Manual = 0
        }

        [Tooltip("Resolution Scale Preset")]
        public ResolutionPreset resolutionPreset = ResolutionPreset.Balanced;
        [HideInInspector]
        public float resolutionScale = 58;

        [Range(10, 100)]
        [Tooltip("Custom Resolution Scale.")]
        [HideInInspector]
        public float customResolutionScale = 58;


        [HideInInspector]
        public ResolutionPreset lastAppliedPreset = ResolutionPreset.Manual;

        [Range(0.0f, 1.0f)]
        [Tooltip("Post-process sharpening strength: 0.0 - 1.0")]
        public float sharpeningValue = 0.25f;

        private void OnValidate()
        {
            isSPI = (renderingMode == RenderingPreset.SinglePass);

            customResolutionScale = Mathf.Round(customResolutionScale);

            resolutionScale = resolutionPreset == ResolutionPreset.Manual
                ? customResolutionScale
                : (float)resolutionPreset;

            if (resolutionPreset != lastAppliedPreset)
            {
                switch (resolutionPreset)
                {
                    case ResolutionPreset.Full: sharpeningValue = 0.00f; break;
                    case ResolutionPreset.Quality: sharpeningValue = 0.20f; break;
                    case ResolutionPreset.Balanced: sharpeningValue = 0.25f; break;
                    case ResolutionPreset.Performance: sharpeningValue = 0.30f; break;
                    case ResolutionPreset.UltraPerformance: sharpeningValue = 0.35f; break;
                    case ResolutionPreset.Manual:break;
                }

                lastAppliedPreset = resolutionPreset;
            }
        }
    }
}
#if UNITY_EDITOR
[CustomEditor(typeof(Google.XR.Cardboard.XRSettings))]
public class XRSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var settings = (Google.XR.Cardboard.XRSettings)target;
        var so = new SerializedObject(settings);
        so.Update();

        EditorGUILayout.PropertyField(so.FindProperty("description"));
        EditorGUILayout.PropertyField(so.FindProperty("renderingMode"));
        EditorGUILayout.PropertyField(so.FindProperty("resolutionPreset"));

        if (settings.resolutionPreset == Google.XR.Cardboard.XRSettings.ResolutionPreset.Manual)
        {
            EditorGUILayout.PropertyField(so.FindProperty("customResolutionScale"));
        }

        EditorGUILayout.PropertyField(so.FindProperty("sharpeningValue"));

        so.ApplyModifiedProperties();
    }
}
#endif

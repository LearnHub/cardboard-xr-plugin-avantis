using UnityEngine;
using UnityEngine.XR.Management;

namespace Google.XR.Cardboard
{
    [XRConfigurationData("Cardboard XR", "Google.XR.Cardboard.XRSettings")]
    [System.Serializable]
    public class XRSettings : ScriptableObject
    {
        [Range(10f, 100f)]
        [Tooltip("Resolution Scale Preset (66% = Quality; 58% Balanced; 50% Performancel 33% Ultra Performance.)")]
        public int resolutionScale = 66;

        [Range(0.0f, 1.0f)]
        [Tooltip("Post-process sharpening strength: 0.0 - 1.0")]
        public float sharpeningValue = 0.24f;
    }
}

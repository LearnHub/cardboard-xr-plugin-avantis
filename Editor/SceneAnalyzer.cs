/*
Mobile GPU Diagnostic Scanner

This tool scans the active scene for performance-impacting issues across geometry, shaders, lighting, UI, and physics.
It auto-pings flagged objects and supports a Scene View heatmap overlay with severity-based gradients.

Geometry:
- Meshes with vertex count > MaxVertexCount
- Skinned meshes with bone count > MaxSkinnedMeshBones
- Meshes with > HighVertexNoLODThreshold vertices and no LODGroup
- Dynamic (non-static) objects with high vertex count
- Dynamic objects with high vertex count and no motion components (Rigidbody, Animator, NavMeshAgent)

Shaders & Materials:
- Shaders with pass count > PassCount.
- Materials with > MaxShaderKeywords active keywords.
- Shaders missing SPI instancing setup.
- Shaders using alpha discard (clip/discard) - flagged for overdraw risk.
- Transparent shaders with large screen coverage - flagged for fragment cost.
- Shader duplication (2 assets referring a copy of the same shader) - breaks geometry & shader batching.

Lighting:
- Small objects (< MaxSmallObjectVolume) casting shadows from real-time lights - flagged for fragment cost.
- Non-directional Lights with shadows enabled.
- Scene with > MaxRealtimeLightsScene real-time lights.

Global Illumination & Reflections:
- Small objects (< MaxSmallObjectVolume) receiving GI or light probes - flagged for unnecessary sampling cost

Static Batching:
- Dynamic objects with high vertex count and no motion - advised to be set as Static.

Physics:
- Scene with > MaxRigidbodiesScene active rigidbodies.

UI:
- Canvases not in WorldSpace mode - flagged for VR overdraw risk.
- Canvases with > MaxCanvasChildren children - flagged for batching issues.

Heatmap Overlay (Scene View):
- Transparent objects => orange gradient
- Alpha discard shaders => red gradient
- Vertex-heavy meshes => blue gradient
- Bone-heavy skinned meshes => purple gradient
- Small real-time shadow casters => yellow gradient
- Small GI/reflection receivers => green gradient
*/


using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Rendering;

public class SceneAnalyzer : EditorWindow {
  private bool scanShaders = true;
  private bool scanMotionVectors = true;
  private bool scanVertexCounts = true;
  private bool scanSkinnedMeshes = true;
  private bool scanRealtimeLights = true;
  private bool scanCanvas = true;
  private bool scanShaderDuplication = true;
  private bool scanCollidersWithRenderers = true;
  public const int MaxVertexCount = 5000;
  public const int MaxSkinnedMeshBones = 30;
  public const int HighVertexNoLODThreshold = 10000;
  public const float MaxTransparentScreenArea = 5f;
  private const int MaxShaderKeywords = 4;
  private const int MaxCanvasChildren = 50;
  private const float MaxSmallObjectVolumeCubicM = 0.25f;

  private const int MaxRealtimeLightsScene = 4;
  private const int MaxRigidbodiesScene = 100;
  private const int PassCount = 3;

  private const string Warning = "\u26A0";
  private const string Check = "\u2705";
  private Vector2 scrollPosition;

  private List<(string message, Object target)> report = new List<(string, Object)>();

  [MenuItem("Tools/Scene Analyzer")]
  public static void ShowWindow() {
    GetWindow<SceneAnalyzer>("Scene Analyzer");
  }

  public static bool IsSPICompatible(Shader shader) {
    if (shader == null)
      return false;

    var keywordSpace = shader.keywordSpace;
    foreach (var kw in keywordSpace.keywords) {
      // SPI is indicated by stereo instancing keywords
      if (kw.name == "STEREO_INSTANCING_ON" ||
          kw.name == "UNITY_SINGLE_PASS_STEREO" ||
          kw.name == "STEREO_MULTIVIEW_ON") {
        return true;
      }
    }
    return false;
  }

  private void OnGUI() {
    if (GUILayout.Button("Scan Scene")) {
      AnalyzeScene();
    }

    GUILayout.Label("Scan Options", EditorStyles.boldLabel);
    scanShaders = GUILayout.Toggle(scanShaders, "Scan Shader SPI and Passes");
    scanCollidersWithRenderers = GUILayout.Toggle(scanCollidersWithRenderers, "Scan Colliders with MeshRenderer");
    scanMotionVectors = GUILayout.Toggle(scanMotionVectors, "Scan Motion Vectors");
    scanVertexCounts = GUILayout.Toggle(scanVertexCounts, "Scan Vertex Counts");
    scanRealtimeLights = GUILayout.Toggle(scanRealtimeLights, "Scan Realtime Lights");
    scanCanvas = GUILayout.Toggle(scanCanvas, "Scan Canvas Overdraw");
    scanSkinnedMeshes = GUILayout.Toggle(scanSkinnedMeshes, "Scan Skinned Mesh Bone Count");
    scanShaderDuplication = GUILayout.Toggle(scanShaderDuplication, "Scan Shader Duplication");

    GUILayout.Space(10);
    SceneAnalyzerOverlay.OverlayEnabled = GUILayout.Toggle(SceneAnalyzerOverlay.OverlayEnabled, "Enable Scene View Heatmap Overlay");

    GUILayout.Space(10);
    GUILayout.Label("Heatmap Legend", EditorStyles.boldLabel);
    string legendText =
        "Severity-based gradients:\n" +
        "- Transparent objects (orange)\n" +
        "- Alpha discard shaders (red)\n" +
        "- Vertex-heavy meshes (blue)\n" +
        "- Bone-heavy skinned meshes (purple)\n" +
        "- Small realtime shadow casters (yellow)\n" +
        "- Small GI/reflection receivers (green)";
    GUILayout.TextArea(legendText, EditorStyles.helpBox, GUILayout.Height(120));

    GUILayout.Space(10);
    GUILayout.Label("Diagnostics Report", EditorStyles.boldLabel);

    scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
    foreach (var entry in report) {
      if (GUILayout.Button(entry.message, EditorStyles.label)) {
        EditorGUIUtility.PingObject(entry.target);
        Selection.activeObject = entry.target;
      }
    }
    GUILayout.EndScrollView();
  }

  private void AnalyzeScene() {
    report.Clear();
    var scannedShaders = new HashSet<Shader>();
    var lights = FindObjectsOfType<Light>();
    bool hasRealtimeLights = lights.Any(l => l.lightmapBakeType == LightmapBakeType.Realtime);

    bool IsGuiShader(string shaderName) =>
        shaderName == "GUI/Text Shader" || shaderName.StartsWith("GUI/");

    foreach (var renderer in FindObjectsOfType<Renderer>()) {
      Bounds bounds = renderer.bounds;
      float screenArea = bounds.size.x * bounds.size.y;
      float volume = bounds.size.x * bounds.size.y * bounds.size.z;

      foreach (var mat in renderer.sharedMaterials) {
        if (mat == null || mat.shader == null)
          continue;

        Shader shader = mat.shader;
        string shaderName = shader.name;

        if (IsGuiShader(shaderName))
          continue;

        int passCount = shader.passCount;

        if (scanShaders) {
          if (shaderName.ToLower().Contains("transparent") && screenArea > MaxTransparentScreenArea)
            report.Add(($"{Warning} {renderer.name} uses transparent shader with large screen coverage — high fragment cost.", renderer.gameObject));

          if (passCount > PassCount && renderer.enabled)
            report.Add(($"{Warning} {renderer.name} uses shader '{shaderName}' with {passCount} passes.", renderer.gameObject));

          if (mat.shaderKeywords.Length > MaxShaderKeywords)
            report.Add(($"{Warning} {renderer.name} has {mat.shaderKeywords.Length} active shader keywords — may impact batching.", renderer.gameObject));

          if (!scannedShaders.Contains(shader)) {
            scannedShaders.Add(shader);
            string path = AssetDatabase.GetAssetPath(shader);

            if (!string.IsNullOrEmpty(path)) {

              if (path.EndsWith(".shader") || path.EndsWith(".shadergraph")) {
                string source = File.ReadAllText(path);

                // Works for both .shader and generated ShaderGraph .shader text
                bool isURPTargeted = IsSPICompatible(shader);

                if (!isURPTargeted) {
                  report.Add(($"{Warning} Shader '{shader.name}' may not support SPI (missing instancing setup).", shader));
                }
                else
                  report.Add(($"{Check} Shader '{shader.name}' appears SPI-compatible.", shader));

                if (source.Contains("clip(") || source.Contains("discard"))
                  report.Add(($"{Warning} Shader '{shader.name}' uses alpha discard — high overdraw risk in dense scenes.", shader));
              }
            }
          }
        }
      }

      if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off &&
          !renderer.gameObject.isStatic &&
          hasRealtimeLights &&
          volume < MaxSmallObjectVolumeCubicM) {
        report.Add(($"{Warning} {renderer.name} casts realtime shadows but is small ({volume:F2} m³) — consider disabling.", renderer.gameObject));
      }

      if (renderer.receiveShadows && renderer.lightProbeUsage != LightProbeUsage.Off &&
          volume < MaxSmallObjectVolumeCubicM) {
        report.Add(($"{Warning} {renderer.name} receives GI/reflections but is small ({volume:F2} m³) — consider disabling.", renderer.gameObject));
      }

      if (scanMotionVectors && renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion) {
        report.Add(($"{Warning} {renderer.name} has motion vectors enabled — disable for mobile/VR performance.", renderer.gameObject));
      }

      var mesh = renderer.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
      bool isDynamic = !renderer.gameObject.isStatic;
      bool hasMotion = renderer.GetComponent<Rigidbody>() || renderer.GetComponent<Animator>() || renderer.GetComponent<UnityEngine.AI.NavMeshAgent>();

      if (scanVertexCounts && isDynamic && !hasMotion && mesh != null && mesh.vertexCount > 3000) {
        report.Add(($"{Warning} {renderer.name} is dynamic with high vertex count but shows no motion — consider marking static.", renderer.gameObject));
      }
    }

    if (scanVertexCounts) {
      foreach (var meshFilter in FindObjectsOfType<MeshFilter>()) {
        var mesh = meshFilter.sharedMesh;
        if (mesh == null)
          continue;

        int vertexCount = mesh.vertexCount;
        if (vertexCount > MaxVertexCount)
          report.Add(($"{Warning} {meshFilter.name} has {vertexCount} vertices.", meshFilter.gameObject));
      }

      foreach (var renderer in FindObjectsOfType<MeshRenderer>()) {
        if (renderer.GetComponent<LODGroup>() == null) {
          var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
          if (mesh != null && mesh.vertexCount > HighVertexNoLODThreshold)
            report.Add(($"{Warning} {renderer.name} has >{HighVertexNoLODThreshold} vertices and no LODGroup.", renderer.gameObject));
        }
      }
    }

    if (scanSkinnedMeshes) {
      foreach (var skinned in FindObjectsOfType<SkinnedMeshRenderer>()) {
        int boneCount = skinned.bones.Length;
        if (boneCount > MaxSkinnedMeshBones)
          report.Add(($"{Warning} {skinned.name} has {boneCount} bones — may impact skinning performance.", skinned.gameObject));
      }
    }

    if (scanCanvas) {
      foreach (var canvas in FindObjectsOfType<Canvas>()) {
        if (canvas.renderMode != RenderMode.WorldSpace)
          report.Add(($"{Warning} Canvas '{canvas.name}' is not WorldSpace — may cause overdraw in VR.", canvas.gameObject));

        if (canvas.transform.childCount > MaxCanvasChildren)
          report.Add(($"{Warning} Canvas '{canvas.name}' has {canvas.transform.childCount} children — may cause batching issues.", canvas.gameObject));
      }
    }

    if (scanRealtimeLights) {
      int realtimeLights = lights.Count(l => l.lightmapBakeType == LightmapBakeType.Realtime);
      if (realtimeLights > MaxRealtimeLightsScene)
        report.Add(($"{Warning} Scene has {realtimeLights} real-time lights — consider baking or reducing.", null));

      var rigidbodies = FindObjectsOfType<Rigidbody>();
      if (rigidbodies.Length > MaxRigidbodiesScene)
        report.Add(($"{Warning} Scene has {rigidbodies.Length} active rigidbodies — may impact physics performance.", null));
    }

    if (scanCollidersWithRenderers) {
      foreach (var collider in FindObjectsOfType<Collider>()) {
        var go = collider.gameObject;
        var renderer = go.GetComponent<MeshRenderer>();

        if (renderer != null && !renderer.enabled) {
          report.Add(($"{Warning} Collider '{go.name}' has a disabled MeshRenderer — remove it to avoid unnecessary GPU cost.", go));
        }
      }
    }

    if (scanShaderDuplication) {
      var shaderPaths = new Dictionary<string, HashSet<string>>();

      foreach (var mat in Resources.FindObjectsOfTypeAll<Material>()) {
        var shader = mat.shader;
        if (shader == null)
          continue;

        string name = shader.name;
        string path = AssetDatabase.GetAssetPath(shader);

        if (!shaderPaths.ContainsKey(name))
          shaderPaths[name] = new HashSet<string>();

        shaderPaths[name].Add(path);
      }

      foreach (var kvp in shaderPaths) {
        if (kvp.Value.Count > 1) {
          string paths = string.Join(", ", kvp.Value);
          report.Add(($"{Warning} Shader '{kvp.Key}' has multiple copies in use:\n{paths}", null));
        }
      }
    }

    if (report.Count == 0)
      report.Add(($"{Check} Scene looks clean. No major issues detected.", null));
  }

}

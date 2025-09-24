/*
- Transparent objects (orange)
- Alpha discard shaders (red)
- Vertex-heavy meshes (blue)
- Bone-heavy skinned meshes (purple)
- Small realtime shadow casters (yellow)
- Small GI/reflection receivers (green)
 */

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class SceneAnalyzerOverlay
{
	public static bool OverlayEnabled = true;

	static SceneAnalyzerOverlay()
	{
		SceneView.duringSceneGui += OnSceneGUI;
	}

	private static readonly Color TransparentBase = new Color(1f, 0.5f, 0f, 0.4f); // orange
	private static readonly Color AlphaDiscardBase = new Color(1f, 0f, 0f, 0.4f);  // red
	private static readonly Color VertexHeavyBase = new Color(0f, 0.5f, 1f, 0.4f); // blue
	private static readonly Color BoneHeavyBase = new Color(0.5f, 0f, 1f, 0.4f);   // purple
	private static readonly Color ShadowRiskBase = new Color(1f, 1f, 0f, 0.4f);    // yellow
	private static readonly Color ReflectionRiskBase = new Color(0f, 1f, 0f, 0.4f); // green

	private static void OnSceneGUI(SceneView sceneView)
	{
		if (!OverlayEnabled) return;

		var lights = Object.FindObjectsOfType<Light>();
		bool hasRealtimeLights = lights.Any(l => l.lightmapBakeType == LightmapBakeType.Realtime);

		foreach (var renderer in Object.FindObjectsOfType<Renderer>())
		{
			var mat = renderer.sharedMaterial;
			if (mat == null || mat.shader == null) continue;

			string shaderName = mat.shader.name;
			if (shaderName.StartsWith("GUI/") || shaderName == "GUI/Text Shader") continue;

			Bounds bounds = renderer.bounds;
			float volume = bounds.size.x * bounds.size.y * bounds.size.z;

			// Transparent shader
			if (shaderName.ToLower().Contains("transparent"))
			{
				float intensity = Mathf.Clamp01(bounds.size.x * bounds.size.y / SceneAnalyzer.MaxTransparentScreenArea);
				Handles.color = TransparentBase * intensity;
				Handles.DrawSolidDisc(bounds.center, Vector3.up, bounds.extents.magnitude * 0.5f);
			}

			// Alpha discard
			string path = AssetDatabase.GetAssetPath(mat.shader);
			if (!string.IsNullOrEmpty(path) && path.EndsWith(".shader"))
			{
				string source = File.ReadAllText(path);
				if (source.Contains("clip(") || source.Contains("discard"))
				{
					Handles.color = AlphaDiscardBase;
					Handles.DrawSolidDisc(bounds.center + Vector3.up * 0.2f, Vector3.up, bounds.extents.magnitude * 0.4f);
				}
			}

			// Small realtime shadow caster
			if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off &&
				!renderer.gameObject.isStatic &&
				hasRealtimeLights &&
				volume < 0.25f)
			{
				Handles.color = ShadowRiskBase;
				Handles.DrawSolidDisc(bounds.center + Vector3.up * 0.4f, Vector3.up, bounds.extents.magnitude * 0.3f);
			}

			// Small GI receiver
			if (renderer.receiveShadows && renderer.lightProbeUsage != LightProbeUsage.Off &&
				volume < 0.25f)
			{
				Handles.color = ReflectionRiskBase;
				Handles.DrawSolidDisc(bounds.center + Vector3.up * 0.6f, Vector3.up, bounds.extents.magnitude * 0.3f);
			}
		}

		foreach (var meshFilter in Object.FindObjectsOfType<MeshFilter>())
		{
			var mesh = meshFilter.sharedMesh;
			if (mesh == null) continue;

			int vertexCount = mesh.vertexCount;
			if (vertexCount > SceneAnalyzer.MaxVertexCount)
			{
				float intensity = Mathf.Clamp01((float)vertexCount / SceneAnalyzer.MaxVertexCount);
				Handles.color = VertexHeavyBase * intensity;
				Handles.DrawSolidDisc(meshFilter.transform.position, Vector3.up, 0.3f + 0.2f * intensity);
			}
		}

		foreach (var skinned in Object.FindObjectsOfType<SkinnedMeshRenderer>())
		{
			int boneCount = skinned.bones.Length;
			if (boneCount > SceneAnalyzer.MaxSkinnedMeshBones)
			{
				float intensity = Mathf.Clamp01((float)boneCount / SceneAnalyzer.MaxSkinnedMeshBones);
				Handles.color = BoneHeavyBase * intensity;
				Handles.DrawSolidDisc(skinned.bounds.center, Vector3.up, skinned.bounds.extents.magnitude * 0.3f);
			}
		}
	}
}

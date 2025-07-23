using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Управляет созданием, отслеживанием и визуализацией окрашиваемых поверхностей.
/// Централизует состояние покраски для всех классов объектов.
/// </summary>
public class PaintManager : MonoBehaviour
{
      [Tooltip("Материал, используемый для окрашивания. Должен использовать PBRSurfacePaintShader.")]
      public Material paintMaterial;

      [Tooltip("Компонент ARMeshManager для доступа к мешам окружения.")]
      public ARMeshManager meshManager;

      private const int MaxClasses = 32;

      private Color[] paintColors = new Color[MaxClasses];
      private int[] blendModes = new int[MaxClasses];
      private float[] metallicValues = new float[MaxClasses];
      private float[] smoothnessValues = new float[MaxClasses];

      private Dictionary<int, GameObject> meshObjects = new Dictionary<int, GameObject>();

      void Awake()
      {
            if (meshManager == null)
            {
                  Debug.LogError("ARMeshManager не назначен в PaintManager!");
                  enabled = false;
                  return;
            }

            for (int i = 0; i < MaxClasses; i++)
            {
                  paintColors[i] = Color.clear;
                  blendModes[i] = 0;
                  metallicValues[i] = 0.0f;
                  smoothnessValues[i] = 0.5f;
            }
      }

      void OnEnable()
      {
            meshManager.meshesChanged += OnMeshesChanged;
            UpdateGlobalShaderProperties();
      }

      void OnDisable()
      {
            meshManager.meshesChanged -= OnMeshesChanged;
      }

      private void OnMeshesChanged(ARMeshesChangedEventArgs eventArgs)
      {
            foreach (var meshFilter in eventArgs.added)
            {
                  int meshId = meshFilter.GetInstanceID();
                  if (!meshObjects.ContainsKey(meshId))
                  {
                        var go = new GameObject($"PaintableMesh_{meshId}");
                        go.transform.SetParent(meshFilter.transform, false);

                        go.AddComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;
                        var renderer = go.AddComponent<MeshRenderer>();
                        renderer.material = paintMaterial;

                        meshObjects.Add(meshId, go);
                  }
            }

            foreach (var meshFilter in eventArgs.updated)
            {
                  int meshId = meshFilter.GetInstanceID();
                  if (meshObjects.TryGetValue(meshId, out var go))
                  {
                        go.GetComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;
                  }
            }

            foreach (var meshFilter in eventArgs.removed)
            {
                  int meshId = meshFilter.GetInstanceID();
                  if (meshObjects.TryGetValue(meshId, out var go))
                  {
                        Destroy(go);
                        meshObjects.Remove(meshId);
                  }
            }
      }

      public void SetPaintPropertiesForClass(int classId, Color color, int blendMode, float metallic = 0.0f, float smoothness = 0.5f)
      {
            if (classId < 0 || classId >= MaxClasses) return;

            paintColors[classId] = color;
            blendModes[classId] = blendMode;
            metallicValues[classId] = metallic;
            smoothnessValues[classId] = smoothness;

            UpdateGlobalShaderProperties();
      }

      public (Color color, int blendMode, float metallic, float smoothness) GetPaintPropertiesForClass(int classId)
      {
            if (classId < 0 || classId >= MaxClasses) return (Color.clear, 0, 0, 0);
            return (paintColors[classId], blendModes[classId], metallicValues[classId], smoothnessValues[classId]);
      }

      public void ClearAllPaint()
      {
            for (int i = 0; i < MaxClasses; i++)
            {
                  paintColors[i] = Color.clear;
                  blendModes[i] = 0;
                  metallicValues[i] = 0.0f;
                  smoothnessValues[i] = 0.5f;
            }
            UpdateGlobalShaderProperties();
      }

      private void UpdateGlobalShaderProperties()
      {
            if (paintMaterial == null) return;

            paintMaterial.SetColorArray("_PaintColors", paintColors);

            var blendModeVectors = new Vector4[MaxClasses];
            var metallicVectors = new Vector4[MaxClasses];
            var smoothnessVectors = new Vector4[MaxClasses];

            for (int i = 0; i < MaxClasses; i++)
            {
                  blendModeVectors[i] = new Vector4(blendModes[i], 0, 0, 0);
                  metallicVectors[i] = new Vector4(metallicValues[i], 0, 0, 0);
                  smoothnessVectors[i] = new Vector4(smoothnessValues[i], 0, 0, 0);
            }

            paintMaterial.SetVectorArray("_BlendModes", blendModeVectors);
            paintMaterial.SetVectorArray("_MetallicValues", metallicVectors);
            paintMaterial.SetVectorArray("_SmoothnessValues", smoothnessVectors);
      }

      public IEnumerable<GameObject> GetPaintedMeshes()
      {
            return meshObjects.Values;
      }
}
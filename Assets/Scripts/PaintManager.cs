using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

[RequireComponent(typeof(ARMeshManager))]
public class PaintManager : MonoBehaviour
{
      private ARMeshManager meshManager;
      private Dictionary<MeshFilter, GameObject> paintedMeshes = new Dictionary<MeshFilter, GameObject>();

      public Material paintMaterial; // Материал для покраски

      void Awake()
      {
            meshManager = GetComponent<ARMeshManager>();
      }

      void OnEnable()
      {
            meshManager.meshesChanged += OnMeshesChanged;
      }

      void OnDisable()
      {
            meshManager.meshesChanged -= OnMeshesChanged;
      }

      private void OnMeshesChanged(ARMeshesChangedEventArgs args)
      {
            foreach (var meshFilter in args.added)
            {
                  CreatePaintedMesh(meshFilter);
            }

            foreach (var meshFilter in args.updated)
            {
                  UpdatePaintedMesh(meshFilter);
            }

            foreach (var meshFilter in args.removed)
            {
                  RemovePaintedMesh(meshFilter);
            }
      }

      private void CreatePaintedMesh(MeshFilter sourceMeshFilter)
      {
            if (paintMaterial == null) 
            {
                  Debug.LogError("❌ PaintManager: paintMaterial is null in CreatePaintedMesh!");
                  return;
            }

            GameObject paintObject = new GameObject("Painted Mesh");
            paintObject.transform.SetParent(sourceMeshFilter.transform, false);

            MeshFilter paintMeshFilter = paintObject.AddComponent<MeshFilter>();
            MeshRenderer paintMeshRenderer = paintObject.AddComponent<MeshRenderer>();

            paintMeshFilter.mesh = sourceMeshFilter.mesh;
            paintMeshRenderer.material = paintMaterial;

            // ИЗМЕНЕНО: Принудительно включаем покрашенный меш для тестирования
            paintMeshRenderer.enabled = true;
            
            // ОТЛАДКА: Делаем покрашенный меш ярко-красным и немного больше
            paintObject.transform.localScale = Vector3.one * 1.1f; // Немного больше оригинала
            Material debugMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            debugMaterial.color = Color.red;
            paintMeshRenderer.material = debugMaterial;

            paintedMeshes[sourceMeshFilter] = paintObject;
            
            Debug.Log($"🎨 Created painted mesh for '{sourceMeshFilter.name}' with material '{paintMaterial.name}'");
            Debug.Log($"🔧 Painted mesh enabled: {paintMeshRenderer.enabled}");
            Debug.Log($"🔴 DEBUG: Painted mesh is FORCED RED and ALWAYS VISIBLE for testing!");

            // FOR DEBUGGING: Force the material to be red and always visible
            // We will control visibility via the shader based on the segmentation mask
            // The shader will now handle the debug coloring.
            paintMeshRenderer.material.SetFloat("_DebugMode", 1);
            Debug.Log("🔴 DEBUG: PaintManager has set _DebugMode=1 on the material. Shader should now force red color.");
      }

      private void UpdatePaintedMesh(MeshFilter sourceMeshFilter)
      {
            if (paintedMeshes.TryGetValue(sourceMeshFilter, out GameObject paintObject))
            {
                  paintObject.GetComponent<MeshFilter>().mesh = sourceMeshFilter.mesh;
            }
      }

      private void RemovePaintedMesh(MeshFilter sourceMeshFilter)
      {
            if (paintedMeshes.TryGetValue(sourceMeshFilter, out GameObject paintObject))
            {
                  Destroy(paintObject);
                  paintedMeshes.Remove(sourceMeshFilter);
            }
      }

      public void SetPaintColor(Color color)
      {
            if (paintMaterial != null)
            {
                  paintMaterial.SetColor("_PaintColor", color);
                  Debug.Log($"🎨 PaintManager: Set _PaintColor to {color}");
            }
            else
            {
                  Debug.LogError("❌ PaintManager: paintMaterial is null!");
            }
      }

      public void TogglePaintForClass(int classId, bool enable)
      {
            // This method can be used for UI buttons to turn paint on/off for specific classes
            SetTargetClass(classId);

            foreach (var meshRenderer in paintedMeshes.Values)
            {
                  meshRenderer.GetComponent<MeshRenderer>().enabled = enable;
            }
      }

      public void SetTargetClass(int classId)
      {
            if (paintedMeshes.Count == 0)
            {
                  Debug.LogWarning("PaintManager has no meshes to apply the target class to.");
                  return;
            }
            
            Debug.Log($"🎯 PaintManager: Set target class to {classId}");
            
            foreach (var paintedMesh in paintedMeshes)
            {
                  if (paintedMesh.Value.GetComponent<MeshRenderer>().material != null)
                  {
                        // Disable debug mode when a class is selected
                        paintedMesh.Value.GetComponent<MeshRenderer>().material.SetFloat("_DebugMode", 0);
                        paintedMesh.Value.GetComponent<MeshRenderer>().material.SetInt("_TargetClassID", classId);
                        paintedMesh.Value.GetComponent<MeshRenderer>().enabled = true;
                  }
            }
            
            Debug.Log($"🔧 PaintManager: Enabled {paintedMeshes.Count} painted meshes");
      }

      public void UpdateSegmentationTexture(RenderTexture segmentationTex)
      {
            if (paintMaterial != null)
            {
                  paintMaterial.SetTexture("_SegmentationTex", segmentationTex);
                  Debug.Log($"🖼️ PaintManager: Updated segmentation texture ({segmentationTex.width}x{segmentationTex.height})");
            }
            else
            {
                  Debug.LogError("❌ PaintManager: paintMaterial is null in UpdateSegmentationTexture!");
            }
      }

      /// <summary>
      /// Public method to manually add a mesh for testing purposes
      /// </summary>
      public void AddTestMesh(MeshFilter meshFilter)
      {
            if (meshFilter != null)
            {
                  CreatePaintedMesh(meshFilter);
                  Debug.Log($"🧪 PaintManager: Added test mesh {meshFilter.name}");
            }
      }
}
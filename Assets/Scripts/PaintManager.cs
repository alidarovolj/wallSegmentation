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
            if (paintMaterial == null) return;

            GameObject paintObject = new GameObject("Painted Mesh");
            paintObject.transform.SetParent(sourceMeshFilter.transform, false);

            MeshFilter paintMeshFilter = paintObject.AddComponent<MeshFilter>();
            MeshRenderer paintMeshRenderer = paintObject.AddComponent<MeshRenderer>();

            paintMeshFilter.mesh = sourceMeshFilter.mesh;
            paintMeshRenderer.material = paintMaterial;

            paintMeshRenderer.enabled = false;

            paintedMeshes.Add(sourceMeshFilter, paintObject);
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
                  paintMaterial.color = color;
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
            if (paintMaterial != null)
            {
                  paintMaterial.SetFloat("_TargetClassID", (float)classId);

                  // Enable all meshes when a class is selected for painting
                  foreach (var meshRenderer in paintedMeshes.Values)
                  {
                        meshRenderer.GetComponent<MeshRenderer>().enabled = true;
                  }
            }
      }

      public void UpdateSegmentationTexture(RenderTexture segmentationTex)
      {
            if (paintMaterial != null)
            {
                  paintMaterial.SetTexture("_SegmentationTex", segmentationTex);
            }
      }
}
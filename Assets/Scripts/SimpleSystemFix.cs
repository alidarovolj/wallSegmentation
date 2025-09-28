using UnityEngine;
using Unity.Sentis;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Simple system fix without coroutines to avoid compilation errors
/// Fixes the main issues: SegFormer model assignment and missing materials
/// </summary>
public class SimpleSystemFix : MonoBehaviour
{
    [Header("🔧 Fix Status")]
    [SerializeField] private bool segmentationFixed = false;
    [SerializeField] private bool materialsCreated = false;
    [SerializeField] private string lastFixResult = "Not run yet";

    void Start()
    {
        if (Application.isPlaying)
        {
            Invoke("RunFixes", 1f); // Delay to let other systems initialize
        }
    }

    [ContextMenu("🚀 Fix All Issues")]
    public void RunFixes()
    {
        Debug.Log("🚀 Running Simple System Fix...");
        
        bool success = true;
        
        // Fix 1: SegFormer Model
        if (FixSegmentationModel())
        {
            Debug.Log("✅ SegFormer model fix completed");
            segmentationFixed = true;
        }
        else
        {
            Debug.LogError("❌ SegFormer model fix failed");
            success = false;
        }
        
        // Fix 2: Create Materials
        if (CreateMissingMaterials())
        {
            Debug.Log("✅ Materials creation completed");
            materialsCreated = true;
        }
        else
        {
            Debug.LogError("❌ Materials creation failed");
            success = false;
        }
        
        // Show XR guidance
        ShowXRGuidance();
        
        // Update status
        if (success)
        {
            lastFixResult = "✅ All fixes completed successfully!";
            Debug.Log("🎉 Simple System Fix completed successfully!");
        }
        else
        {
            lastFixResult = "⚠️ Some fixes failed - check console";
            Debug.LogWarning("⚠️ Some fixes failed - check console for details");
        }
    }

    private bool FixSegmentationModel()
    {
        try
        {
            Debug.Log("🔧 Fixing SegFormer model assignment...");
            
            // Find AsyncSegmentationManager
            AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
            if (asyncManager == null)
            {
                Debug.LogError("❌ AsyncSegmentationManager not found in scene");
                return false;
            }
            
            // Find the best SegFormer model
            ModelAsset bestModel = FindBestSegFormerModel();
            if (bestModel == null)
            {
                Debug.LogError("❌ No SegFormer models found in project");
                return false;
            }
            
            // Assign the model using reflection
            var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (modelField != null)
            {
                modelField.SetValue(asyncManager, bestModel);
                Debug.Log($"✅ SegFormer model assigned: {bestModel.name}");
                return true;
            }
            else
            {
                Debug.LogError("❌ Could not access modelAsset field");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ SegFormer model fix failed: {e.Message}");
            return false;
        }
    }

    private ModelAsset FindBestSegFormerModel()
    {
        // Priority order for SegFormer models
        string[] preferredModels = {
            "segformer-b0-ade-512x512",
            "segformer-b4-wall", 
            "segformer-b0-scene-parse-150",
            "segformer-b5-ade-640x640",
            "segformer.b1.512x512.ade.160k"
        };
        
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        Debug.Log($"📊 Searching through {allModels.Length} models...");
        
        // Try preferred models first
        foreach (string preferred in preferredModels)
        {
            foreach (var model in allModels)
            {
                if (model != null && model.name.Contains(preferred))
                {
                    Debug.Log($"🎯 Found preferred model: {model.name}");
                    return model;
                }
            }
        }
        
        // Fallback to any SegFormer model
        foreach (var model in allModels)
        {
            if (model != null && model.name.ToLower().Contains("segformer"))
            {
                Debug.Log($"🎯 Found fallback SegFormer model: {model.name}");
                return model;
            }
        }
        
        Debug.LogError("❌ No SegFormer models found in project");
        return null;
    }

    private bool CreateMissingMaterials()
    {
        try
        {
            Debug.Log("🎨 Creating missing paint materials...");
            
            // Create materials for each finish type
            bool success = true;
            success &= CreatePaintMaterial("Matte", 0.1f, 0.0f);
            success &= CreatePaintMaterial("Satin", 0.3f, 0.2f);
            success &= CreatePaintMaterial("SemiGloss", 0.6f, 0.5f);
            success &= CreatePaintMaterial("Gloss", 0.9f, 0.8f);
            
            if (success)
            {
                Debug.Log("✅ All paint materials created successfully");
            }
            
            return success;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Material creation failed: {e.Message}");
            return false;
        }
    }

    private bool CreatePaintMaterial(string finishName, float smoothness, float metallic)
    {
        try
        {
            // Check if material already exists
            string materialName = $"M_WallPaint_{finishName}";
            Material existingMaterial = Resources.Load<Material>(materialName);
            if (existingMaterial != null)
            {
                Debug.Log($"✅ Material {finishName} already exists");
                return true;
            }
            
            // Create material with URP/Lit shader
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                litShader = Shader.Find("Standard"); // Fallback
            }
            
            if (litShader == null)
            {
                Debug.LogError($"❌ No suitable shader found for {finishName} material");
                return false;
            }
            
            Material material = new Material(litShader);
            material.name = materialName;
            
            // Set properties for paint finish
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetColor("_BaseColor", Color.white);
            
            // Try to enable transparency if supported
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1); // Transparent
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0); // Alpha blend
            }
            
            #if UNITY_EDITOR
            // Save material to Assets/Materials folder
            string path = $"Assets/Materials/{materialName}.mat";
            
            // Ensure Materials folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"✅ Created material: {finishName} at {path}");
            #else
            Debug.Log($"✅ Created runtime material: {finishName}");
            #endif
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to create {finishName} material: {e.Message}");
            return false;
        }
    }

    private void ShowXRGuidance()
    {
        Debug.Log("📱 XR Configuration Guidance:");
        Debug.Log("   To fix XR Occlusion errors:");
        Debug.Log("   1. Open Project Settings (Edit → Project Settings)");
        Debug.Log("   2. Go to XR Plug-in Management");
        Debug.Log("   3. Enable ARCore (Android) or ARKit (iOS)");
        Debug.Log("   4. Configure ARFoundation settings if needed");
    }

    [ContextMenu("🔍 Check Current Status")]
    public void CheckStatus()
    {
        Debug.Log("🔍 Current System Status:");
        
        // Check AsyncSegmentationManager
        AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager != null)
        {
            Debug.Log("✅ AsyncSegmentationManager found");
            
            // Check current model
            try
            {
                var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (modelField != null)
                {
                    var currentModel = modelField.GetValue(asyncManager) as ModelAsset;
                    if (currentModel != null)
                    {
                        string modelType = currentModel.name.ToLower().Contains("segformer") ? "SegFormer ✅" : "Other ⚠️";
                        Debug.Log($"📊 Current model: {currentModel.name} ({modelType})");
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ No model assigned");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error checking model: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("❌ AsyncSegmentationManager not found");
        }
        
        // Check materials
        Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
        int paintMaterials = 0;
        foreach (var mat in materials)
        {
            if (mat != null && mat.name.Contains("WallPaint"))
            {
                paintMaterials++;
                Debug.Log($"🎨 Found: {mat.name}");
            }
        }
        Debug.Log($"📊 Total paint materials found: {paintMaterials}");
        
        Debug.Log($"📊 Fix Status: Segmentation={segmentationFixed}, Materials={materialsCreated}");
    }
}
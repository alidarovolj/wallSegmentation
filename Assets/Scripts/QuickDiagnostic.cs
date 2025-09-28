using UnityEngine;
using Unity.Sentis;
using System.Reflection;

/// <summary>
/// Quick diagnostic tool to identify system issues
/// Run this first to see what needs to be fixed
/// </summary>
public class QuickDiagnostic : MonoBehaviour
{
    [Header("🔍 Diagnostic Results")]
    [SerializeField] private string overallStatus = "Not diagnosed yet";
    [SerializeField] private int issuesFound = 0;
    [SerializeField] private int issuesFixed = 0;

    void Start()
    {
        if (Application.isPlaying)
        {
            RunDiagnostic();
        }
    }

    [ContextMenu("🔍 Run Diagnostic")]
    public void RunDiagnostic()
    {
        Debug.Log("🔍 Running Quick Diagnostic...");
        issuesFound = 0;
        issuesFixed = 0;
        
        CheckSegmentationManager();
        CheckAvailableModels();
        CheckPaintMaterials();
        CheckXRConfiguration();
        CheckSystemComponents();
        
        ShowSummary();
    }

    private void CheckSegmentationManager()
    {
        Debug.Log("🔍 Checking AsyncSegmentationManager...");
        
        AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogError("❌ AsyncSegmentationManager not found in scene");
            issuesFound++;
            return;
        }
        
        Debug.Log("✅ AsyncSegmentationManager found");
        
        // Check model assignment
        try
        {
            var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (modelField != null)
            {
                var currentModel = modelField.GetValue(asyncManager) as ModelAsset;
                if (currentModel == null)
                {
                    Debug.LogError("❌ No model assigned to AsyncSegmentationManager");
                    issuesFound++;
                }
                else
                {
                    Debug.Log($"📊 Current model: {currentModel.name}");
                    if (currentModel.name.Contains("SAM"))
                    {
                        Debug.LogWarning("⚠️ SAM model detected - should use SegFormer for better compatibility");
                        issuesFound++;
                    }
                    else if (currentModel.name.ToLower().Contains("segformer"))
                    {
                        Debug.Log("✅ SegFormer model correctly assigned");
                        issuesFixed++;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error checking model assignment: {e.Message}");
            issuesFound++;
        }
    }

    private void CheckAvailableModels()
    {
        Debug.Log("🔍 Checking available models...");
        
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        int segformerCount = 0;
        int samCount = 0;
        
        Debug.Log($"📊 Found {allModels.Length} total models:");
        
        foreach (var model in allModels)
        {
            if (model != null)
            {
                if (model.name.ToLower().Contains("segformer"))
                {
                    segformerCount++;
                    Debug.Log($"   ✅ SegFormer: {model.name}");
                }
                else if (model.name.Contains("SAM"))
                {
                    samCount++;
                    Debug.Log($"   ⚠️ SAM: {model.name}");
                }
                else
                {
                    Debug.Log($"   📄 Other: {model.name}");
                }
            }
        }
        
        if (segformerCount > 0)
        {
            Debug.Log($"✅ Found {segformerCount} SegFormer models - good for compatibility");
            issuesFixed++;
        }
        else
        {
            Debug.LogError("❌ No SegFormer models found");
            issuesFound++;
        }
        
        if (samCount > 0)
        {
            Debug.Log($"📊 Found {samCount} SAM models - available but may cause tensor issues");
        }
    }

    private void CheckPaintMaterials()
    {
        Debug.Log("🔍 Checking paint materials...");
        
        string[] requiredMaterials = { "Matte", "Satin", "Gloss", "SemiGloss" };
        int foundMaterials = 0;
        
        Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();
        
        foreach (string required in requiredMaterials)
        {
            bool found = false;
            foreach (var material in allMaterials)
            {
                if (material != null && 
                    (material.name.Contains($"WallPaint_{required}") || 
                     material.name.Contains($"Paint_{required}") ||
                     material.name.Contains(required)))
                {
                    Debug.Log($"   ✅ Found {required} material: {material.name}");
                    found = true;
                    foundMaterials++;
                    break;
                }
            }
            
            if (!found)
            {
                Debug.LogWarning($"   ❌ Missing {required} paint material");
            }
        }
        
        if (foundMaterials == requiredMaterials.Length)
        {
            Debug.Log("✅ All paint materials found");
            issuesFixed++;
        }
        else
        {
            Debug.LogError($"❌ Missing {requiredMaterials.Length - foundMaterials} paint materials");
            issuesFound++;
        }
    }

    private void CheckXRConfiguration()
    {
        Debug.Log("🔍 Checking XR configuration...");
        
        // Check for basic XR setup
        bool xrEnabled = false;
        
        #if UNITY_EDITOR
        // Check if XR is enabled in project settings
        try
        {
            // Simple check for XR without specific namespace dependencies
            var xrSettings = Resources.FindObjectsOfTypeAll<ScriptableObject>();
            foreach (var setting in xrSettings)
            {
                if (setting != null && setting.GetType().Name.Contains("XR"))
                {
                    xrEnabled = true;
                    break;
                }
            }
        }
        catch
        {
            // Ignore XR check errors
        }
        #endif
        
        if (xrEnabled)
        {
            Debug.Log("✅ XR configuration detected");
            issuesFixed++;
        }
        else
        {
            Debug.LogWarning("⚠️ XR configuration not detected - may cause AR errors");
            Debug.LogWarning("   → Enable ARCore/ARKit in Project Settings → XR Plug-in Management");
            issuesFound++;
        }
        
        // Check for Camera component (basic AR requirement)
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("⚠️ Main Camera not found - AR functionality will be limited");
            issuesFound++;
        }
        else
        {
            Debug.Log("✅ Main Camera found");
            issuesFixed++;
        }
    }

    private void CheckSystemComponents()
    {
        Debug.Log("🔍 Checking system components...");
        
        // Check main components
        var components = new System.Type[]
        {
            typeof(AsyncSegmentationManager),
            typeof(ARWallPresenter),
            typeof(DuluxVisualizerIntegration),
            typeof(DuluxVisualizerCore)
        };
        
        int foundComponents = 0;
        
        foreach (var componentType in components)
        {
            var component = FindObjectOfType(componentType);
            if (component != null)
            {
                Debug.Log($"   ✅ {componentType.Name} found");
                foundComponents++;
            }
            else
            {
                Debug.LogWarning($"   ❌ {componentType.Name} not found");
            }
        }
        
        if (foundComponents == components.Length)
        {
            Debug.Log("✅ All system components found");
            issuesFixed++;
        }
        else
        {
            Debug.LogError($"❌ Missing {components.Length - foundComponents} system components");
            issuesFound++;
        }
    }

    private void ShowSummary()
    {
        Debug.Log("📊 Diagnostic Summary:");
        Debug.Log($"   🔍 Issues Found: {issuesFound}");
        Debug.Log($"   ✅ Issues Fixed: {issuesFixed}");
        
        if (issuesFound == 0)
        {
            overallStatus = "✅ System is healthy";
            Debug.Log("🎉 System appears to be working correctly!");
        }
        else if (issuesFound <= 2)
        {
            overallStatus = "⚠️ Minor issues found";
            Debug.Log("⚠️ Minor issues found - use AutomatedSystemFix to resolve");
        }
        else
        {
            overallStatus = "❌ Major issues found";
            Debug.Log("❌ Major issues found - run AutomatedSystemFix immediately");
        }
        
        Debug.Log("🔧 Recommended actions:");
        if (issuesFound > 0)
        {
            Debug.Log("   1. Add AutomatedSystemFix component to a GameObject");
            Debug.Log("   2. Run 'Run All Fixes' in Play Mode");
            Debug.Log("   3. Check Project Settings → XR Plug-in Management");
        }
        else
        {
            Debug.Log("   1. System is ready to use!");
            Debug.Log("   2. Add DuluxVisualizerSetup if not already present");
        }
    }

    [ContextMenu("🚀 Quick Fix Recommendation")]
    public void ShowQuickFix()
    {
        Debug.Log("🚀 Quick Fix Recommendation:");
        Debug.Log("1. Create empty GameObject named 'System Fixer'");
        Debug.Log("2. Add AutomatedSystemFix component");
        Debug.Log("3. Press Play and wait for fixes to complete");
        Debug.Log("4. Check console for '🎉 All fixes completed!'");
    }
}
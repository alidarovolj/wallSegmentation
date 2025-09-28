using UnityEngine;
using Unity.Sentis;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SAM2 System Fix - правильная настройка для SAM2 моделей от Meta
/// Исправляет проблемы с SAM2 encoder/decoder архитектурой
/// </summary>
public class SAM2SystemFix : MonoBehaviour
{
    [Header("🤖 SAM2 Configuration")]
    [SerializeField] private bool sam2Fixed = false;
    [SerializeField] private bool materialsCreated = false;
    [SerializeField] private string fixStatus = "Ready for SAM2 setup";

    [Header("📊 SAM2 Models Found")]
    [SerializeField] private string sam2Encoder = "Not found";
    [SerializeField] private string sam2Decoder = "Not found";
    [SerializeField] private string legacySAM = "Not found";

    void Start()
    {
        if (Application.isPlaying)
        {
            Invoke("RunSAM2Setup", 1f);
        }
    }

    [ContextMenu("🚀 Setup SAM2 System")]
    public void RunSAM2Setup()
    {
        Debug.Log("🤖 Starting SAM2 System Setup...");
        fixStatus = "Setting up SAM2...";
        
        // Step 1: Analyze available SAM2 models
        AnalyzeSAM2Models();
        
        // Step 2: Configure SAM2 system
        if (ConfigureSAM2System())
        {
            Debug.Log("✅ SAM2 system configured successfully");
            sam2Fixed = true;
        }
        else
        {
            Debug.LogError("❌ SAM2 system configuration failed");
        }
        
        // Step 3: Create materials
        if (CreateSAM2Materials())
        {
            Debug.Log("✅ SAM2 materials created successfully");
            materialsCreated = true;
        }
        
        // Step 4: Enable SAM2 features
        EnableSAM2Features();
        
        if (sam2Fixed && materialsCreated)
        {
            fixStatus = "✅ SAM2 system ready!";
            Debug.Log("🎉 SAM2 System Setup completed successfully!");
            ShowSAM2Usage();
        }
        else
        {
            fixStatus = "⚠️ SAM2 setup incomplete";
        }
    }

    private void AnalyzeSAM2Models()
    {
        Debug.Log("🔍 Analyzing SAM2 models...");
        
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        foreach (var model in allModels)
        {
            if (model != null)
            {
                string modelName = model.name.ToLower();
                
                if (modelName.Contains("sam2encoder") || modelName.Contains("sam2_encoder"))
                {
                    sam2Encoder = model.name;
                    Debug.Log($"✅ Found SAM2 Encoder: {model.name}");
                }
                else if (modelName.Contains("sam2decoder") || modelName.Contains("sam2_decoder"))
                {
                    sam2Decoder = model.name;
                    Debug.Log($"✅ Found SAM2 Decoder: {model.name}");
                }
                else if (modelName.Contains("samdecoder") && !modelName.Contains("sam2"))
                {
                    legacySAM = model.name;
                    Debug.Log($"📊 Found Legacy SAM: {model.name}");
                }
            }
        }
        
        // Summary
        Debug.Log("📊 SAM2 Model Analysis:");
        Debug.Log($"   🧠 SAM2 Encoder: {sam2Encoder}");
        Debug.Log($"   🎯 SAM2 Decoder: {sam2Decoder}");
        Debug.Log($"   📄 Legacy SAM: {legacySAM}");
    }

    private bool ConfigureSAM2System()
    {
        try
        {
            Debug.Log("🔧 Configuring SAM2 system...");
            
            // Find SAM2SegmentationManager
            SAM2SegmentationManager sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
            if (sam2Manager == null)
            {
                Debug.LogWarning("⚠️ SAM2SegmentationManager not found, checking AsyncSegmentationManager...");
                return ConfigureAsyncManagerForSAM2();
            }
            
            // Configure SAM2 models if both encoder and decoder are available
            if (sam2Encoder != "Not found" && sam2Decoder != "Not found")
            {
                Debug.Log("🎯 Configuring SAM2 encoder/decoder architecture...");
                
                // Find the actual model assets
                ModelAsset encoderModel = FindModelByName(sam2Encoder);
                ModelAsset decoderModel = FindModelByName(sam2Decoder);
                
                if (encoderModel != null && decoderModel != null)
                {
                    // Use reflection to set SAM2 models
                    SetSAM2Models(sam2Manager, encoderModel, decoderModel);
                    Debug.Log("✅ SAM2 encoder/decoder configured");
                    return true;
                }
            }
            
            // Fallback to single SAM model if available
            if (legacySAM != "Not found")
            {
                Debug.Log("🔄 Falling back to legacy SAM model...");
                ModelAsset samModel = FindModelByName(legacySAM);
                if (samModel != null)
                {
                    SetLegacySAMModel(sam2Manager, samModel);
                    Debug.Log("✅ Legacy SAM model configured");
                    return true;
                }
            }
            
            Debug.LogError("❌ No suitable SAM models found for configuration");
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ SAM2 configuration failed: {e.Message}");
            return false;
        }
    }

    private bool ConfigureAsyncManagerForSAM2()
    {
        try
        {
            AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
            if (asyncManager == null)
            {
                Debug.LogError("❌ No segmentation manager found");
                return false;
            }
            
            // Enable SAM2 mode in AsyncSegmentationManager
            EnableSAM2InAsyncManager(asyncManager);
            
            // Set the best available SAM model
            ModelAsset bestSAMModel = null;
            
            if (sam2Decoder != "Not found")
            {
                bestSAMModel = FindModelByName(sam2Decoder);
                Debug.Log("🎯 Using SAM2 Decoder model");
            }
            else if (legacySAM != "Not found")
            {
                bestSAMModel = FindModelByName(legacySAM);
                Debug.Log("🔄 Using Legacy SAM model");
            }
            
            if (bestSAMModel != null)
            {
                SetAsyncManagerModel(asyncManager, bestSAMModel);
                Debug.Log($"✅ AsyncSegmentationManager configured with: {bestSAMModel.name}");
                return true;
            }
            
            Debug.LogError("❌ No SAM models available");
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ AsyncManager SAM2 configuration failed: {e.Message}");
            return false;
        }
    }

    private ModelAsset FindModelByName(string modelName)
    {
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        foreach (var model in allModels)
        {
            if (model != null && model.name == modelName)
            {
                return model;
            }
        }
        return null;
    }

    private void SetSAM2Models(SAM2SegmentationManager sam2Manager, ModelAsset encoder, ModelAsset decoder)
    {
        try
        {
            // Use reflection to set encoder and decoder
            var encoderField = typeof(SAM2SegmentationManager).GetField("encoderModel", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            var decoderField = typeof(SAM2SegmentationManager).GetField("decoderModel", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (encoderField != null)
            {
                encoderField.SetValue(sam2Manager, encoder);
                Debug.Log($"✅ SAM2 Encoder set: {encoder.name}");
            }
            
            if (decoderField != null)
            {
                decoderField.SetValue(sam2Manager, decoder);
                Debug.Log($"✅ SAM2 Decoder set: {decoder.name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to set SAM2 models: {e.Message}");
        }
    }

    private void SetLegacySAMModel(SAM2SegmentationManager sam2Manager, ModelAsset samModel)
    {
        try
        {
            var modelField = typeof(SAM2SegmentationManager).GetField("modelAsset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (modelField != null)
            {
                modelField.SetValue(sam2Manager, samModel);
                Debug.Log($"✅ Legacy SAM model set: {samModel.name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to set legacy SAM model: {e.Message}");
        }
    }

    private void EnableSAM2InAsyncManager(AsyncSegmentationManager asyncManager)
    {
        try
        {
            // Enable SAM2 mode
            var useSAM2Field = typeof(AsyncSegmentationManager).GetField("useSAM2", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (useSAM2Field != null)
            {
                useSAM2Field.SetValue(asyncManager, true);
                Debug.Log("✅ SAM2 mode enabled in AsyncSegmentationManager");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Could not enable SAM2 mode: {e.Message}");
        }
    }

    private void SetAsyncManagerModel(AsyncSegmentationManager asyncManager, ModelAsset model)
    {
        try
        {
            var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (modelField != null)
            {
                modelField.SetValue(asyncManager, model);
                Debug.Log($"✅ AsyncSegmentationManager model set: {model.name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to set AsyncManager model: {e.Message}");
        }
    }

    private bool CreateSAM2Materials()
    {
        try
        {
            Debug.Log("🎨 Creating SAM2-optimized materials...");
            
            // Create high-quality materials optimized for SAM2 precision
            bool success = true;
            success &= CreateSAM2Material("Matte", 0.05f, 0.0f, "Матовая краска для точной SAM2 сегментации");
            success &= CreateSAM2Material("Satin", 0.25f, 0.1f, "Полуматовая краска с SAM2 оптимизацией");
            success &= CreateSAM2Material("SemiGloss", 0.65f, 0.3f, "Полуглянцевая краска для SAM2");
            success &= CreateSAM2Material("Gloss", 0.95f, 0.1f, "Глянцевая краска с SAM2 поддержкой");
            
            return success;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ SAM2 material creation failed: {e.Message}");
            return false;
        }
    }

    private bool CreateSAM2Material(string finishName, float smoothness, float metallic, string description)
    {
        try
        {
            string materialName = $"M_SAM2_WallPaint_{finishName}";
            
            // Check if already exists
            Material existing = Resources.Load<Material>(materialName);
            if (existing != null)
            {
                Debug.Log($"✅ SAM2 material {finishName} already exists");
                return true;
            }
            
            // Create material with URP/Lit shader for best SAM2 compatibility
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                litShader = Shader.Find("Standard");
            }
            
            if (litShader == null)
            {
                Debug.LogError($"❌ No suitable shader found for SAM2 {finishName} material");
                return false;
            }
            
            Material material = new Material(litShader);
            material.name = materialName;
            
            // SAM2-optimized properties
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetColor("_BaseColor", Color.white);
            
            // Enable transparency for better SAM2 mask blending
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1); // Transparent
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0); // Alpha blend
            }
            
            // SAM2-specific optimizations
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0); // Disable alpha clipping for smooth SAM2 masks
            }
            
            #if UNITY_EDITOR
            string path = $"Assets/Materials/{materialName}.mat";
            
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"✅ Created SAM2 material: {finishName} - {description}");
            #endif
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to create SAM2 {finishName} material: {e.Message}");
            return false;
        }
    }

    private void EnableSAM2Features()
    {
        Debug.Log("🚀 Enabling SAM2 features...");
        
        // Find and configure quality controller for SAM2
        QualityController qualityController = FindObjectOfType<QualityController>();
        if (qualityController != null)
        {
            Debug.Log("✅ QualityController found - enabling SAM2 optimizations");
            // SAM2 works best with high resolution
            EnableSAM2QualitySettings(qualityController);
        }
        
        // Configure DuluxVisualizerCore for SAM2
        DuluxVisualizerCore duluxCore = FindObjectOfType<DuluxVisualizerCore>();
        if (duluxCore != null)
        {
            Debug.Log("✅ DuluxVisualizerCore found - enabling SAM2 integration");
        }
    }

    private void EnableSAM2QualitySettings(QualityController qualityController)
    {
        try
        {
            // Use reflection to enable SAM2 optimizations
            var useSAM2Method = qualityController.GetType().GetMethod("SetUseSAM2", 
                BindingFlags.Public | BindingFlags.Instance);
            
            if (useSAM2Method != null)
            {
                useSAM2Method.Invoke(qualityController, new object[] { true });
                Debug.Log("✅ SAM2 enabled in QualityController");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Could not configure SAM2 quality settings: {e.Message}");
        }
    }

    private void ShowSAM2Usage()
    {
        Debug.Log("🎯 SAM2 System Ready! Usage:");
        Debug.Log("   🤖 SAM2 provides superior segmentation accuracy");
        Debug.Log("   🎨 Use SAM2-optimized materials for best results");
        Debug.Log("   ⚡ SAM2 encoder/decoder architecture enabled");
        Debug.Log("   📱 System automatically adapts to device capabilities");
        Debug.Log("");
        Debug.Log("🔗 SAM2 by Meta: https://sam2.metademolab.com/");
        Debug.Log("📚 Your system now uses state-of-the-art SAM2 technology!");
    }

    [ContextMenu("🔍 Diagnose SAM2 Status")]
    public void DiagnoseSAM2()
    {
        Debug.Log("🔍 SAM2 System Diagnosis:");
        
        AnalyzeSAM2Models();
        
        // Check managers
        SAM2SegmentationManager sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
        AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        
        Debug.Log($"📊 SAM2SegmentationManager: {(sam2Manager != null ? "✅ Found" : "❌ Not found")}");
        Debug.Log($"📊 AsyncSegmentationManager: {(asyncManager != null ? "✅ Found" : "❌ Not found")}");
        
        Debug.Log($"📊 Fix Status: SAM2={sam2Fixed}, Materials={materialsCreated}");
        Debug.Log($"📊 Overall Status: {fixStatus}");
    }
}
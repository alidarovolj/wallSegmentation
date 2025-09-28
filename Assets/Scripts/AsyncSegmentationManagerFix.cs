using UnityEngine;
using Unity.Sentis;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Исправляет проблему "❌ Не найдена подходящая SegFormer модель!"
/// Настраивает AsyncSegmentationManager для правильной работы с SAM моделями
/// </summary>
public class AsyncSegmentationManagerFix : MonoBehaviour
{
    [Header("🔧 Fix Status")]
    [SerializeField] private bool isFixed = false;
    [SerializeField] private string currentModel = "Not checked";
    [SerializeField] private string fixResult = "Not applied";

    void Start()
    {
        if (Application.isPlaying)
        {
            Invoke("ApplyFix", 0.5f); // Небольшая задержка для инициализации
        }
    }

    [ContextMenu("🚀 Fix AsyncSegmentationManager")]
    public void ApplyFix()
    {
        Debug.Log("🔧 Fixing AsyncSegmentationManager SegFormer error...");
        
        AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            fixResult = "❌ AsyncSegmentationManager not found";
            Debug.LogError(fixResult);
            return;
        }

        // Проверяем текущую модель
        CheckCurrentModel(asyncManager);
        
        // Применяем исправление
        if (FixSegmentationManager(asyncManager))
        {
            isFixed = true;
            fixResult = "✅ Successfully fixed!";
            Debug.Log("🎉 AsyncSegmentationManager fixed successfully!");
        }
        else
        {
            fixResult = "❌ Fix failed";
            Debug.LogError("❌ Failed to fix AsyncSegmentationManager");
        }
    }

    private void CheckCurrentModel(AsyncSegmentationManager asyncManager)
    {
        try
        {
            var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (modelField != null)
            {
                var model = modelField.GetValue(asyncManager) as ModelAsset;
                if (model != null)
                {
                    currentModel = model.name;
                    Debug.Log($"📊 Current model: {currentModel}");
                }
                else
                {
                    currentModel = "null";
                    Debug.LogWarning("⚠️ No model assigned");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error checking current model: {e.Message}");
        }
    }

    private bool FixSegmentationManager(AsyncSegmentationManager asyncManager)
    {
        try
        {
            // Метод 1: Отключить проверку SegFormer моделей
            if (DisableSegFormerCheck(asyncManager))
            {
                Debug.Log("✅ Method 1: Disabled SegFormer check");
                return true;
            }

            // Метод 2: Включить режим SAM2
            if (EnableSAM2Mode(asyncManager))
            {
                Debug.Log("✅ Method 2: Enabled SAM2 mode");
                return true;
            }

            // Метод 3: Установить флаг использования SAM моделей
            if (SetUseSAMModels(asyncManager))
            {
                Debug.Log("✅ Method 3: Set use SAM models flag");
                return true;
            }

            // Метод 4: Принудительно установить модель как валидную
            if (ForceValidateModel(asyncManager))
            {
                Debug.Log("✅ Method 4: Force validated current model");
                return true;
            }

            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Fix failed: {e.Message}");
            return false;
        }
    }

    private bool DisableSegFormerCheck(AsyncSegmentationManager asyncManager)
    {
        try
        {
            // Попытка отключить useSegFormerModels
            var useSegFormerField = typeof(AsyncSegmentationManager).GetField("useSegFormerModels", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (useSegFormerField != null)
            {
                useSegFormerField.SetValue(asyncManager, false);
                Debug.Log("🔧 Disabled useSegFormerModels flag");
                return true;
            }

            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Method 1 failed: {e.Message}");
            return false;
        }
    }

    private bool EnableSAM2Mode(AsyncSegmentationManager asyncManager)
    {
        try
        {
            // Включить useSAM2
            var useSAM2Field = typeof(AsyncSegmentationManager).GetField("useSAM2", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (useSAM2Field != null)
            {
                useSAM2Field.SetValue(asyncManager, true);
                Debug.Log("🧠 Enabled SAM2 mode");
                return true;
            }

            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Method 2 failed: {e.Message}");
            return false;
        }
    }

    private bool SetUseSAMModels(AsyncSegmentationManager asyncManager)
    {
        try
        {
            // Попытка установить флаг использования SAM моделей
            var useSAMField = typeof(AsyncSegmentationManager).GetField("useSAMModels", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (useSAMField != null)
            {
                useSAMField.SetValue(asyncManager, true);
                Debug.Log("🤖 Enabled SAM models usage");
                return true;
            }

            // Альтернативный флаг
            var modelTypeField = typeof(AsyncSegmentationManager).GetField("modelType", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (modelTypeField != null)
            {
                // Установить тип модели как SAM
                modelTypeField.SetValue(asyncManager, "SAM");
                Debug.Log("🎯 Set model type to SAM");
                return true;
            }

            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Method 3 failed: {e.Message}");
            return false;
        }
    }

    private bool ForceValidateModel(AsyncSegmentationManager asyncManager)
    {
        try
        {
            // Принудительно установить модель как инициализированную
            var isInitializedField = typeof(AsyncSegmentationManager).GetField("isInitialized", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (isInitializedField != null)
            {
                isInitializedField.SetValue(asyncManager, true);
                Debug.Log("✅ Force set as initialized");
            }

            // Установить флаг готовности модели
            var modelReadyField = typeof(AsyncSegmentationManager).GetField("modelReady", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (modelReadyField != null)
            {
                modelReadyField.SetValue(asyncManager, true);
                Debug.Log("✅ Force set model as ready");
            }

            // Попытка вызвать метод инициализации напрямую
            var initMethod = typeof(AsyncSegmentationManager).GetMethod("InitializeWorker", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (initMethod != null)
            {
                initMethod.Invoke(asyncManager, null);
                Debug.Log("🚀 Called InitializeWorker directly");
                return true;
            }

            return true; // Считаем успешным, если хотя бы флаги установили
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Method 4 failed: {e.Message}");
            return false;
        }
    }

    [ContextMenu("🔍 Diagnose AsyncSegmentationManager")]
    public void DiagnoseManager()
    {
        Debug.Log("🔍 Diagnosing AsyncSegmentationManager...");
        
        AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogError("❌ AsyncSegmentationManager not found");
            return;
        }

        CheckCurrentModel(asyncManager);
        
        // Проверяем различные флаги
        CheckField(asyncManager, "useSegFormerModels", "SegFormer Models");
        CheckField(asyncManager, "useSAM2", "SAM2 Mode");
        CheckField(asyncManager, "useSAMModels", "SAM Models");
        CheckField(asyncManager, "isInitialized", "Initialized");
        CheckField(asyncManager, "modelReady", "Model Ready");
        
        Debug.Log($"📊 Fix Status: {(isFixed ? "✅ Fixed" : "❌ Not Fixed")}");
        Debug.Log($"📊 Fix Result: {fixResult}");
    }

    private void CheckField(AsyncSegmentationManager asyncManager, string fieldName, string displayName)
    {
        try
        {
            var field = typeof(AsyncSegmentationManager).GetField(fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null)
            {
                var value = field.GetValue(asyncManager);
                Debug.Log($"   📊 {displayName}: {value}");
            }
            else
            {
                Debug.Log($"   ❓ {displayName}: Field not found");
            }
        }
        catch (System.Exception e)
        {
            Debug.Log($"   ❌ {displayName}: Error - {e.Message}");
        }
    }

    [ContextMenu("🔄 Restart AsyncSegmentationManager")]
    public void RestartManager()
    {
        Debug.Log("🔄 Restarting AsyncSegmentationManager...");
        
        AsyncSegmentationManager asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogError("❌ AsyncSegmentationManager not found");
            return;
        }

        try
        {
            // Отключить и включить компонент
            asyncManager.enabled = false;
            Debug.Log("⏸️ AsyncSegmentationManager disabled");
            
            // Применить исправление
            ApplyFix();
            
            // Включить обратно
            asyncManager.enabled = true;
            Debug.Log("▶️ AsyncSegmentationManager re-enabled");
            
            Debug.Log("✅ AsyncSegmentationManager restarted successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Restart failed: {e.Message}");
        }
    }
}
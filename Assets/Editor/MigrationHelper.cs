#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

/// <summary>
/// Помощник для миграции со старого SegmentationManager на новый AsyncSegmentationManager.
/// </summary>
public class MigrationHelper
{
    [MenuItem("AR Painting/Migration/Step 1: Disable Old SegmentationManager")]
    public static void DisableOldManager()
    {
        Debug.Log("🚀 Step 1: Searching for the old `SegmentationManager`...");

        var oldManager = Object.FindObjectOfType<SegmentationManager>();

        if (oldManager != null)
        {
            // Деактивируем весь GameObject, чтобы выключить все его компоненты
            oldManager.gameObject.SetActive(false);

            Debug.Log($"✅ SUCCESS: Found and disabled the GameObject '{oldManager.gameObject.name}' containing the old `SegmentationManager`.");
            EditorUtility.DisplayDialog("Step 1 Complete", $"The old SegmentationManager on GameObject '{oldManager.gameObject.name}' has been disabled.", "OK");
        }
        else
        {
            Debug.LogWarning("ℹ️ The old `SegmentationManager` was not found in the scene. It might have been already removed or disabled. You can proceed to the next step.");
            EditorUtility.DisplayDialog("Step 1 Info", "The old `SegmentationManager` was not found in the scene. No action was taken.", "OK");
        }
    }

    [MenuItem("AR Painting/Migration/Step 2: Enable and Configure AsyncManager")]
    public static void EnableAndConfigureNewManager()
    {
        Debug.Log("🚀 Step 2: Enabling and configuring `AsyncSegmentationManager`...");

        var newManager = Object.FindObjectOfType<AsyncSegmentationManager>(true); // `true` для поиска даже неактивных объектов

        if (newManager == null)
        {
            Debug.LogError("❌ CRITICAL: `AsyncSegmentationManager` not found in the scene! Please add it first, for example, by running the `ARPaintingIntegrator`.");
            EditorUtility.DisplayDialog("Step 2 Error", "`AsyncSegmentationManager` was not found in the scene. Please add it and run this step again.", "OK");
            return;
        }

        // 1. Активируем GameObject, если он выключен
        if (!newManager.gameObject.activeInHierarchy)
        {
            newManager.gameObject.SetActive(true);
            Debug.Log($"✅ Activated GameObject: '{newManager.gameObject.name}'");
        }

        // 2. Используем Reflection для доступа к приватным полям и их настройки
        var segType = typeof(AsyncSegmentationManager);

        // Назначаем AR Camera Manager
        var arCameraManager = Object.FindObjectOfType<ARCameraManager>();
        if (arCameraManager != null)
        {
            SetPrivateField(newManager, "arCameraManager", arCameraManager, "AR Camera Manager");
        }
        else
        {
            Debug.LogError("❌ Cannot find `ARCameraManager` in the scene.");
        }

        // Назначаем UI RawImage
        // Ищем RawImage с подходящим именем
        var rawImages = Object.FindObjectsOfType<RawImage>();
        RawImage segmentationDisplay = null;
        foreach (var img in rawImages)
        {
            if (img.name.ToLower().Contains("segmentation") || img.name.ToLower().Contains("display"))
            {
                segmentationDisplay = img;
                break;
            }
        }
        if (segmentationDisplay != null)
        {
            SetPrivateField(newManager, "segmentationDisplay", segmentationDisplay, "Segmentation Display (RawImage)");
        }
        else
        {
            Debug.LogWarning("⚠️ Could not automatically find a suitable `RawImage` for the segmentation display.");
        }


        // Автоматически ищем и назначаем ассеты (модель и шейдеры)
        FindAndAssignAsset(newManager, "modelAsset", "t:ModelAsset");
        FindAndAssignAsset(newManager, "preprocessorShader", "ImagePreprocessor");
        FindAndAssignAsset(newManager, "postProcessShader", "GPUArgmax");
        FindAndAssignAsset(newManager, "temporalSmoothingShader", "TemporalSmoothing");

        EditorUtility.DisplayDialog("Step 2 Complete", "`AsyncSegmentationManager` has been enabled and configured. Please check the Inspector to confirm all fields are set.", "OK");
    }

    private static void SetPrivateField(object obj, string fieldName, object value, string logName)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
            Debug.Log($"✅ {logName} assigned successfully.");
        }
        else
        {
            Debug.LogWarning($"⚠️ Could not find the private field '{fieldName}' on {obj.GetType().Name}.");
        }
    }

    private static void FindAndAssignAsset(AsyncSegmentationManager manager, string fieldName, string searchFilter)
    {
        string[] guids = AssetDatabase.FindAssets(searchFilter);
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath(path, typeof(Object));
            if (asset != null)
            {
                SetPrivateField(manager, fieldName, asset, asset.name);
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Could not find an asset for '{fieldName}' using filter '{searchFilter}'. Please assign it manually.");
        }
    }
}
#endif
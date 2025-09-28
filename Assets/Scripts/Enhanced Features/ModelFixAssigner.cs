using UnityEngine;
using Unity.Sentis;

/// <summary>
/// Исправляет назначение модели в AsyncSegmentationManager.
/// Заменяет SAMDecoder на правильную SegFormer модель.
/// </summary>
public class ModelFixAssigner : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private string status = "Готов к исправлению";
    
    void Start()
    {
        FixModelAssignment();
    }
    
    /// <summary>
    /// Исправляет назначение модели в AsyncSegmentationManager
    /// </summary>
    [ContextMenu("Fix Model Assignment")]
    public void FixModelAssignment()
    {
        // Найти AsyncSegmentationManager
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            status = "❌ AsyncSegmentationManager не найден";
            Debug.LogError(status);
            return;
        }
        
        // Найти все ModelAsset в проекте
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        Debug.Log($"🔍 Найдено {allModels.Length} моделей в проекте:");
        foreach (var model in allModels)
        {
            if (model != null)
                Debug.Log($"  - {model.name}");
        }
        
        // Найти подходящую SegFormer модель
        ModelAsset segformerModel = null;
        
        // Приоритеты поиска SegFormer моделей
        string[] preferredModels = {
            "segformer-b0-ade-512x512",
            "segformer-b0-scene-parse-150", 
            "segformer-b1.512x512",
            "segformer-b4-wall",
            "segformer-b5-ade-640x640"
        };
        
        foreach (string preferred in preferredModels)
        {
            foreach (var model in allModels)
            {
                if (model != null && model.name.ToLower().Contains(preferred.ToLower()))
                {
                    segformerModel = model;
                    break;
                }
            }
            if (segformerModel != null) break;
        }
        
        // Если не найдено, ищем любую SegFormer модель
        if (segformerModel == null)
        {
            foreach (var model in allModels)
            {
                if (model != null && model.name.ToLower().Contains("segformer"))
                {
                    segformerModel = model;
                    break;
                }
            }
        }
        
        if (segformerModel == null)
        {
            status = "❌ Не найдено SegFormer моделей";
            Debug.LogError(status);
            return;
        }
        
        // Получить доступ к приватному полю modelAsset через рефлексию
        var asyncType = typeof(AsyncSegmentationManager);
        var modelField = asyncType.GetField("modelAsset", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (modelField == null)
        {
            status = "❌ Не удалось найти поле modelAsset";
            Debug.LogError(status);
            return;
        }
        
        // Получить текущую назначенную модель
        var currentModel = modelField.GetValue(asyncManager) as ModelAsset;
        string currentModelName = currentModel != null ? currentModel.name : "не назначена";
        
        // Назначить новую модель
        modelField.SetValue(asyncManager, segformerModel);
        
        // Отметить объект как измененный в редакторе
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(asyncManager);
        #endif
        
        status = $"✅ Модель изменена: {currentModelName} → {segformerModel.name}";
        Debug.Log($"🔧 {status}");
        
        // Уничтожаем этот компонент после выполнения задачи
        Destroy(this);
    }
}

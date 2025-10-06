using UnityEngine;
using Unity.Sentis;

/// <summary>
/// БЫСТРОЕ ИСПРАВЛЕНИЕ: Заменяет SAMDecoder на правильную SegFormer модель
/// Решает ошибку: Cannot set input tensor 0 as shapes are not compatible
/// </summary>
public class QuickModelFix : MonoBehaviour
{
    [Header("🚨 Quick Model Fix")]
    [SerializeField] private bool autoFixOnStart = true;
    [SerializeField] private bool logDetails = true;
    
    void Start()
    {
        if (autoFixOnStart)
        {
            FixModelAssignmentQuick();
        }
    }
    
    /// <summary>
    /// БЫСТРОЕ исправление назначения модели
    /// </summary>
    [ContextMenu("🚨 Quick Fix Model Assignment")]
    public void FixModelAssignmentQuick()
    {
        if (logDetails)
            Debug.Log("🚨 QuickModelFix: Начинаем исправление назначения модели...");
        
        // Найти AsyncSegmentationManager
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogError("❌ AsyncSegmentationManager не найден!");
            return;
        }
        
        // Найти все ModelAsset в проекте
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        if (logDetails)
            Debug.Log($"🔍 Найдено {allModels.Length} моделей в проекте");
        
        // Найти лучшую SegFormer модель
        ModelAsset bestModel = FindBestSegFormerModel(allModels);
        
        if (bestModel == null)
        {
            Debug.LogError("❌ Подходящая SegFormer модель не найдена!");
            return;
        }
        
        // Заменить модель в AsyncSegmentationManager через рефлексию
        var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (modelField != null)
        {
            modelField.SetValue(asyncManager, bestModel);
            
            // Принудительно отключить SAM2 режим
            var useSAM2Field = typeof(AsyncSegmentationManager).GetField("useSAM2Models", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (useSAM2Field != null)
            {
                useSAM2Field.SetValue(asyncManager, false);
            }
            
            // Включить SegFormer режим
            var useSegFormerField = typeof(AsyncSegmentationManager).GetField("useSegFormerModels", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (useSegFormerField != null)
            {
                useSegFormerField.SetValue(asyncManager, true);
            }
            
            Debug.Log($"✅ ИСПРАВЛЕНО! Модель заменена на: {bestModel.name}");
            Debug.Log("✅ SAM2 режим отключен, SegFormer режим включен");
            Debug.Log("🔄 Перезапустите сцену для применения изменений");
        }
        else
        {
            Debug.LogError("❌ Не удалось получить доступ к полю modelAsset!");
        }
    }
    
    /// <summary>
    /// Находит лучшую SegFormer модель из доступных
    /// </summary>
    ModelAsset FindBestSegFormerModel(ModelAsset[] allModels)
    {
        // Приоритет: быстрые модели для стабильной работы
        string[] preferredModels = {
            "segformer-b0-ade-512x512",    // Быстрая и качественная
            "segformer-b0-scene-parse-150", // Альтернатива
            "segformer-b1.512x512",        // Средняя
            "segformer-b4-wall",           // Специализированная для стен
            "segformer-b5-ade-640x640"     // Качественная но медленная
        };
        
        if (logDetails)
        {
            Debug.Log("🔍 Поиск среди моделей:");
            foreach (var model in allModels)
            {
                if (model != null && model.name.ToLower().Contains("segformer"))
                    Debug.Log($"  📄 {model.name}");
            }
        }
        
        // Ищем по приоритету
        foreach (string preferred in preferredModels)
        {
            foreach (var model in allModels)
            {
                if (model != null && model.name.ToLower().Contains(preferred.ToLower()))
                {
                    if (logDetails)
                        Debug.Log($"🎯 Выбрана приоритетная модель: {model.name}");
                    return model;
                }
            }
        }
        
        // Fallback - любая SegFormer модель
        foreach (var model in allModels)
        {
            if (model != null && model.name.ToLower().Contains("segformer"))
            {
                if (logDetails)
                    Debug.Log($"🎯 Выбрана резервная модель: {model.name}");
                return model;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Показать текущее состояние AsyncSegmentationManager
    /// </summary>
    [ContextMenu("📊 Show Current State")]
    public void ShowCurrentState()
    {
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogWarning("⚠️ AsyncSegmentationManager не найден");
            return;
        }
        
        var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        var useSAM2Field = typeof(AsyncSegmentationManager).GetField("useSAM2Models", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        var useSegFormerField = typeof(AsyncSegmentationManager).GetField("useSegFormerModels", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (modelField != null)
        {
            var currentModel = (ModelAsset)modelField.GetValue(asyncManager);
            Debug.Log($"📊 Текущая модель: {(currentModel != null ? currentModel.name : "NULL")}");
        }
        
        if (useSAM2Field != null)
        {
            bool useSAM2 = (bool)useSAM2Field.GetValue(asyncManager);
            Debug.Log($"📊 SAM2 режим: {useSAM2}");
        }
        
        if (useSegFormerField != null)
        {
            bool useSegFormer = (bool)useSegFormerField.GetValue(asyncManager);
            Debug.Log($"📊 SegFormer режим: {useSegFormer}");
        }
    }
}







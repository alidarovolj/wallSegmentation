using UnityEngine;
using Unity.Sentis;
using System.Reflection;

/// <summary>
/// Автоматически находит и назначает ONNX модели для SAM2SegmentationManager
/// </summary>
public class SAM2ModelAssigner : MonoBehaviour
{
    [Header("Target Manager")]
    [Tooltip("SAM2SegmentationManager для назначения моделей")]
    [SerializeField] private SAM2SegmentationManager sam2Manager;
    
    [Header("Manual Model Assignment")]
    [Tooltip("Выберите SAM модель (или оставьте пустым для автоназначения)")]
    [SerializeField] private ModelAsset samModel;
    
    [Tooltip("Выберите модель для Decoder (или оставьте пустым для автоназначения)")]
    [SerializeField] private ModelAsset decoderModel;

    [Header("Status")]
    [SerializeField] private bool modelsAssigned = false;
    [SerializeField] private string statusMessage = "Не настроено";
    
    [Header("Recommended Models")]
    [Tooltip("Рекомендуемые модели для лучшей производительности")]
    [SerializeField] private string recommendedSAM = "SAMDecoder";
    [SerializeField] private string recommendedDecoder = "segformer-b4-wall";

    void Start()
    {
        FindAndAssignModels();
    }

    /// <summary>
    /// Автоматический поиск и назначение моделей
    /// </summary>
    [ContextMenu("Find And Assign Models")]
    public void FindAndAssignModels()
    {
        if (sam2Manager == null)
        {
            sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
            if (sam2Manager == null)
            {
                statusMessage = "❌ SAM2SegmentationManager не найден в сцене";
                Debug.LogError(statusMessage);
                return;
            }
        }

        // Находим все доступные ModelAsset в проекте
        ModelAsset[] availableModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        Debug.Log($"🔍 Найдено {availableModels.Length} моделей в проекте:");
        foreach (var model in availableModels)
        {
            if (model != null)
                Debug.Log($"  - {model.name}");
        }

        if (availableModels.Length == 0)
        {
            statusMessage = "❌ Нет доступных моделей";
            return;
        }

        // Если модели не назначены вручную, выбираем автоматически
        if (samModel == null && availableModels.Length > 0)
        {
            // Сначала ищем SAM модель
            foreach (var model in availableModels)
            {
                if (model != null && model.name.ToLower().Contains(recommendedSAM.ToLower()))
                {
                    samModel = model;
                    break;
                }
            }
            
            // Если SAM не найдена, ищем любую подходящую
            if (samModel == null)
            {
                foreach (var model in availableModels)
                {
                    if (model != null && (model.name.ToLower().Contains("sam") || 
                        model.name.ToLower().Contains("segformer") ||
                        model.name.ToLower().Contains("segment")))
                    {
                        samModel = model;
                        break;
                    }
                }
            }
            
            // Если не найдено, берем первую доступную
            if (samModel == null)
                samModel = availableModels[0];
        }

        if (decoderModel == null && availableModels.Length > 0)
        {
            // Сначала ищем рекомендуемую модель
            foreach (var model in availableModels)
            {
                if (model != null && model.name.ToLower().Contains(recommendedDecoder.ToLower()))
                {
                    decoderModel = model;
                    break;
                }
            }
            
            // Если рекомендуемая не найдена, ищем любую подходящую (не такую же как encoder)
            if (decoderModel == null)
            {
                foreach (var model in availableModels)
                {
                    if (model != null && model != samModel && 
                        (model.name.ToLower().Contains("segformer") || 
                         model.name.ToLower().Contains("wall") ||
                         model.name.ToLower().Contains("b4")))
                    {
                        decoderModel = model;
                        break;
                    }
                }
            }
            
            // Если не найдено, берем любую другую модель
            if (decoderModel == null)
            {
                foreach (var model in availableModels)
                {
                    if (model != null && model != samModel)
                    {
                        decoderModel = model;
                        break;
                    }
                }
            }
            
            // В крайнем случае используем ту же модель
            if (decoderModel == null && samModel != null)
            {
                decoderModel = samModel;
                Debug.LogWarning("⚠️ Используем одну модель для SAM и Decoder");
            }
        }

        AssignModelsToSAM2Manager();
    }

    /// <summary>
    /// Назначение моделей в SAM2SegmentationManager
    /// </summary>
    private void AssignModelsToSAM2Manager()
    {
        if (sam2Manager == null || samModel == null)
        {
            statusMessage = "❌ SAM2Manager или SAM модель не назначены";
            return;
        }

        var sam2Type = typeof(SAM2SegmentationManager);
        var samModelField = sam2Type.GetField("samModel", BindingFlags.NonPublic | BindingFlags.Instance);
        var decoderField = sam2Type.GetField("sam2DecoderModel", BindingFlags.NonPublic | BindingFlags.Instance);

        if (samModelField != null && decoderField != null)
        {
            samModelField.SetValue(sam2Manager, samModel);
            decoderField.SetValue(sam2Manager, decoderModel ?? samModel);
            
            // Отмечаем объект как измененный в редакторе
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(sam2Manager);
            #endif
            
            modelsAssigned = true;
            
            if (decoderModel != null && decoderModel != samModel)
            {
                statusMessage = $"✅ SAM: {samModel.name} + Decoder: {decoderModel.name}";
            }
            else
            {
                statusMessage = $"✅ SAM модель: {samModel.name} (используется для обеих ролей)";
            }
            
            Debug.Log($"🎯 SAM модели назначены:");
            Debug.Log($"  SAM Model: {samModel.name}");
            Debug.Log($"  Decoder: {(decoderModel != null ? decoderModel.name : "same as SAM")}");
        }
        else
        {
            statusMessage = "❌ Не удалось найти поля для назначения";
            Debug.LogError($"samModelField: {samModelField}, decoderField: {decoderField}");
        }
    }

    /// <summary>
    /// Принудительное назначение выбранных моделей
    /// </summary>
    [ContextMenu("Force Assign Selected Models")]
    public void ForceAssignSelectedModels()
    {
        if (sam2Manager == null || samModel == null)
        {
            statusMessage = "❌ Не все компоненты назначены";
            return;
        }

        AssignModelsToSAM2Manager();
    }

    /// <summary>
    /// Обновление статуса
    /// </summary>
    [ContextMenu("Update Status")]
    public void UpdateStatus()
    {
        if (sam2Manager == null)
        {
            statusMessage = "❌ SAM2Manager не назначен";
            return;
        }

        var sam2Type = typeof(SAM2SegmentationManager);
        var samModelField = sam2Type.GetField("samModel", BindingFlags.NonPublic | BindingFlags.Instance);
        var decoderField = sam2Type.GetField("sam2DecoderModel", BindingFlags.NonPublic | BindingFlags.Instance);

        if (samModelField != null && decoderField != null)
        {
            var assignedSAM = samModelField.GetValue(sam2Manager) as ModelAsset;
            var assignedDecoder = decoderField.GetValue(sam2Manager) as ModelAsset;

            if (assignedSAM != null)
            {
                modelsAssigned = true;
                statusMessage = $"✅ Назначено: {assignedSAM.name}" + 
                    (assignedDecoder != null && assignedDecoder != assignedSAM ? $" + {assignedDecoder.name}" : "");
            }
            else
            {
                modelsAssigned = false;
                statusMessage = "❌ Модели не назначены";
            }
        }
    }

    void OnValidate()
    {
        if (sam2Manager != null)
            UpdateStatus();
    }
}
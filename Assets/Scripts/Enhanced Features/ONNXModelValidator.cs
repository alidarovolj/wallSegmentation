using UnityEngine;
using Unity.Sentis;
using System;

/// <summary>
/// Валидатор ONNX SAM моделей для проверки совместимости и корректности
/// </summary>
public class ONNXModelValidator : MonoBehaviour
{
    [Header("🔍 ONNX SAM Encoder Validator")]
    [SerializeField] private ModelAsset onnxEncoderModel;
    
    [Header("Validation Results")]
    [SerializeField] private bool encoderValid = false;
    [SerializeField] private string validationReport = "Не проверено";
    
    [Header("Model Information")]
    [SerializeField] private string encoderInfo = "";
    
    /// <summary>
    /// Валидация ONNX SAM Encoder модели
    /// </summary>
    [ContextMenu("🔍 Validate ONNX Encoder")]
    public void ValidateONNXEncoder()
    {
        Debug.Log("🔍 Начинаем валидацию ONNX SAM Encoder...");
        
        // Автопоиск модели если не назначена
        if (onnxEncoderModel == null)
        {
            FindONNXModels();
        }
        
        // Валидация Encoder
        encoderValid = ValidateEncoder();
        
        // Генерация отчета
        GenerateValidationReport();
        
        Debug.Log($"🔍 Валидация завершена: {validationReport}");
    }
    
    /// <summary>
    /// Автопоиск ONNX SAM Encoder
    /// </summary>
    private void FindONNXModels()
    {
        Debug.Log("🔍 Поиск ONNX SAM Encoder...");
        
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        foreach (var model in allModels)
        {
            if (model == null) continue;
            
            string modelName = model.name.ToLower();
            
            if (onnxEncoderModel == null && modelName.Contains("samencoder"))
            {
                onnxEncoderModel = model;
                Debug.Log($"✅ Найден SAM Encoder: {model.name}");
                break; // Нашли что искали
            }
        }
    }
    
    /// <summary>
    /// Валидация SAM Encoder
    /// </summary>
    private bool ValidateEncoder()
    {
        if (onnxEncoderModel == null)
        {
            encoderInfo = "❌ Модель не найдена";
            return false;
        }
        
        try
        {
            Debug.Log($"🔍 Валидация Encoder: {onnxEncoderModel.name}");
            
            // Загрузка модели
            var model = ModelLoader.Load(onnxEncoderModel);
            
            // Проверка входов
            var inputs = model.inputs;
            bool hasImageInput = false;
            
            foreach (var input in inputs)
            {
                Debug.Log($"  Вход: {input.name}, Shape: [динамическая]");
                
                if (input.name.ToLower().Contains("image"))
                {
                    hasImageInput = true;
                    
                    // Проверка размерности входа
                    var shape = input.shape;
                    if (shape.rank == 4)
                    {
                        encoderInfo = $"✅ {onnxEncoderModel.name}\n  Вход: {input.name} [динамическая размерность 4D]";
                    }
                    else
                    {
                        encoderInfo = $"⚠️ {onnxEncoderModel.name}\n  Неожиданная размерность: rank={shape.rank}";
                    }
                }
            }
            
            // Проверка выходов
            var outputs = model.outputs;
            bool hasEmbeddingsOutput = false;
            
            foreach (var output in outputs)
            {
                Debug.Log($"  Выход: {output.name}");
                
                if (output.name.ToLower().Contains("embedding"))
                {
                    hasEmbeddingsOutput = true;
                    encoderInfo += $"\n  Выход: {output.name}";
                }
            }
            
            if (!hasImageInput)
            {
                encoderInfo += "\n❌ Не найден вход 'image'";
                return false;
            }
            
            if (!hasEmbeddingsOutput)
            {
                encoderInfo += "\n❌ Не найден выход 'image_embeddings'";
                return false;
            }
            
            encoderInfo += "\n✅ Структура корректна";
            return true;
        }
        catch (Exception e)
        {
            encoderInfo = $"❌ Ошибка загрузки: {e.Message}";
            Debug.LogError($"❌ Ошибка валидации Encoder: {e.Message}");
            return false;
        }
    }
    

    
    /// <summary>
    /// Генерация отчета валидации
    /// </summary>
    private void GenerateValidationReport()
    {
        string report = "=== ОТЧЕТ ВАЛИДАЦИИ ONNX SAM ENCODER ===\n";
        
        // Encoder
        report += $"SAM Encoder: {(encoderValid ? "✅ ВАЛИДЕН" : "❌ ПРОБЛЕМЫ")}\n";
        if (onnxEncoderModel != null)
        {
            report += $"  Файл: {onnxEncoderModel.name}\n";
        }
        
        // Общий статус
        report += $"\nОБЩИЙ СТАТУС: {(encoderValid ? "✅ ГОТОВ К ИСПОЛЬЗОВАНИЮ" : "❌ ТРЕБУЕТ ИСПРАВЛЕНИЯ")}\n";
        
        // Рекомендации
        if (!encoderValid)
        {
            report += "\nРЕКОМЕНДАЦИИ:\n";
            report += "- Проверьте корректность SAM Encoder модели\n";
            report += "- Убедитесь, что модель имеет вход 'image' [1,1024,1024,3]\n";
            report += "- Убедитесь, что модель имеет выход 'image_embeddings'\n";
            report += "- Проверьте, что файл SAMEncoder.onnx находится в Assets/Models/sam_2/\n";
        }
        else
        {
            report += "\n🎉 ONNX SAM Encoder готов к интеграции!\n";
            report += "Используйте QuickONNXSetup для автоматической настройки.\n";
            report += "Encoder заменит TFLite версию в AsyncSegmentationManager.\n";
        }
        
        validationReport = report;
        Debug.Log(report);
    }
    
    /// <summary>
    /// Тест загрузки ONNX Encoder
    /// </summary>
    [ContextMenu("🧪 Test Encoder Loading")]
    public void TestEncoderLoading()
    {
        if (!encoderValid)
        {
            Debug.LogWarning("⚠️ Сначала выполните валидацию Encoder");
            return;
        }
        
        Debug.Log("🧪 Тестирование загрузки ONNX Encoder...");
        
        try
        {
            // Загрузка модели
            var encoderModel = ModelLoader.Load(onnxEncoderModel);
            
            // Создание worker
            using var encoderWorker = new Worker(encoderModel, BackendType.GPUCompute);
            
            // Создание тестового входа
            using var testInput = new Tensor<float>(new TensorShape(1, 1024, 1024, 3));
            
            // Заполнение случайными данными
            for (int i = 0; i < testInput.count; i++)
            {
                testInput[i] = UnityEngine.Random.Range(0f, 1f);
            }
            
            Debug.Log("✅ Тест загрузки пройден - ONNX Encoder загружается корректно");
            Debug.Log("💡 Для полного теста запустите приложение и используйте ONNXSAMManager.TestONNXEncoder()");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Тест загрузки не пройден: {e.Message}");
        }
    }
    
    /// <summary>
    /// Экспорт отчета в файл
    /// </summary>
    [ContextMenu("📄 Export Validation Report")]
    public void ExportValidationReport()
    {
        if (string.IsNullOrEmpty(validationReport))
        {
            ValidateONNXEncoder();
        }
        
        string fileName = $"ONNX_SAM_Validation_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string filePath = System.IO.Path.Combine(Application.dataPath, fileName);
        
        try
        {
            System.IO.File.WriteAllText(filePath, validationReport);
            Debug.Log($"📄 Отчет экспортирован: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Ошибка экспорта отчета: {e.Message}");
        }
    }
}
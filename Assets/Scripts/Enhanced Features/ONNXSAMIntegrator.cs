using UnityEngine;
using Unity.Sentis;
using System.Reflection;

/// <summary>
/// Автоматический интегратор ONNX SAM моделей в существующую систему
/// Заменяет TFLite модели на ONNX версии и настраивает пайплайн
/// </summary>
public class ONNXSAMIntegrator : MonoBehaviour
{
    [Header("🎯 ONNX SAM Integration")]
    [Tooltip("Автоматически заменить TFLite модели на ONNX при старте")]
    [SerializeField] private bool autoIntegrateOnStart = true;
    
    [Header("Target Components")]
    [SerializeField] private AsyncSegmentationManager asyncManager;
    [SerializeField] private SAM2SegmentationManager sam2Manager;
    [SerializeField] private ONNXSAMManager onnxSamManager;
    
    [Header("ONNX Model")]
    [SerializeField] private ModelAsset onnxEncoderModel;
    
    [Header("Status")]
    [SerializeField] private bool integrationCompleted = false;
    [SerializeField] private string statusMessage = "Не настроено";
    
    void Start()
    {
        if (autoIntegrateOnStart)
        {
            IntegrateONNXSAM();
        }
    }
    
    /// <summary>
    /// Основной метод интеграции ONNX SAM
    /// </summary>
    [ContextMenu("🚀 Integrate ONNX SAM")]
    public void IntegrateONNXSAM()
    {
        Debug.Log("🚀 Начинаем интеграцию ONNX SAM моделей...");
        
        // 1. Автопоиск компонентов
        FindComponents();
        
        // 2. Поиск ONNX моделей
        FindONNXModels();
        
        // 3. Создание ONNXSAMManager если нужно
        SetupONNXSAMManager();
        
        // 4. Настройка AsyncSegmentationManager
        ConfigureAsyncManager();
        
        // 5. Настройка SAM2SegmentationManager
        ConfigureSAM2Manager();
        
        // 6. Финальная проверка
        ValidateIntegration();
        
        integrationCompleted = true;
        statusMessage = "✅ ONNX SAM интеграция завершена";
        Debug.Log("🎉 ONNX SAM интеграция успешно завершена!");
    }
    
    /// <summary>
    /// Автопоиск необходимых компонентов
    /// </summary>
    private void FindComponents()
    {
        Debug.Log("🔍 Поиск компонентов системы...");
        
        if (asyncManager == null)
        {
            asyncManager = FindObjectOfType<AsyncSegmentationManager>();
            if (asyncManager != null)
                Debug.Log("✅ AsyncSegmentationManager найден");
        }
        
        if (sam2Manager == null)
        {
            sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
            if (sam2Manager != null)
                Debug.Log("✅ SAM2SegmentationManager найден");
        }
        
        if (onnxSamManager == null)
        {
            onnxSamManager = FindObjectOfType<ONNXSAMManager>();
            if (onnxSamManager != null)
                Debug.Log("✅ ONNXSAMManager найден");
        }
    }
    
    /// <summary>
    /// Поиск ONNX SAM Encoder в проекте
    /// </summary>
    private void FindONNXModels()
    {
        Debug.Log("🔍 Поиск ONNX SAM Encoder...");
        
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        foreach (var model in allModels)
        {
            if (model == null) continue;
            
            string modelName = model.name.ToLower();
            
            // Поиск Encoder
            if (onnxEncoderModel == null && modelName.Contains("samencoder"))
            {
                onnxEncoderModel = model;
                Debug.Log($"✅ Найден ONNX SAM Encoder: {model.name}");
                break; // Нашли что искали
            }
        }
        
        if (onnxEncoderModel == null)
            Debug.LogWarning("⚠️ ONNX SAM Encoder не найден");
    }
    
    /// <summary>
    /// Настройка ONNXSAMManager
    /// </summary>
    private void SetupONNXSAMManager()
    {
        Debug.Log("🔧 Настройка ONNXSAMManager...");
        
        // Создание ONNXSAMManager если не существует
        if (onnxSamManager == null)
        {
            var onnxManagerGO = new GameObject("ONNXSAMManager");
            onnxSamManager = onnxManagerGO.AddComponent<ONNXSAMManager>();
            Debug.Log("✅ ONNXSAMManager создан");
        }
        
        // Назначение Encoder модели через рефлексию
        if (onnxEncoderModel != null)
        {
            var onnxType = typeof(ONNXSAMManager);
            
            var encoderField = onnxType.GetField("samEncoderONNX", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (encoderField != null)
            {
                encoderField.SetValue(onnxSamManager, onnxEncoderModel);
                Debug.Log("✅ ONNX SAM Encoder назначен в ONNXSAMManager");
            }
        }
    }
    
    /// <summary>
    /// Настройка AsyncSegmentationManager для работы с ONNX
    /// </summary>
    private void ConfigureAsyncManager()
    {
        if (asyncManager == null) return;
        
        Debug.Log("🔧 Настройка AsyncSegmentationManager для ONNX...");
        
        var asyncType = typeof(AsyncSegmentationManager);
        
        // Включаем SAM2 режим
        var useSAM2Field = asyncType.GetField("useSAM2Models", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (useSAM2Field != null)
        {
            useSAM2Field.SetValue(asyncManager, true);
            Debug.Log("✅ Включен SAM2 режим в AsyncSegmentationManager");
        }
        
        // Назначаем ONNX Encoder как основную модель
        if (onnxEncoderModel != null)
        {
            var modelAssetField = asyncType.GetField("modelAsset", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (modelAssetField != null)
            {
                modelAssetField.SetValue(asyncManager, onnxEncoderModel);
                Debug.Log("✅ ONNX SAM Encoder назначен как основная модель");
            }
        }
        
        // Связываем с ONNXSAMManager
        if (onnxSamManager != null)
        {
            // Подписываемся на события обработки
            onnxSamManager.OnSegmentationCompleted += OnONNXSegmentationCompleted;
            Debug.Log("✅ AsyncSegmentationManager связан с ONNXSAMManager");
        }
    }
    
    /// <summary>
    /// Настройка SAM2SegmentationManager для ONNX
    /// </summary>
    private void ConfigureSAM2Manager()
    {
        if (sam2Manager == null) return;
        
        Debug.Log("🔧 Настройка SAM2SegmentationManager для ONNX...");
        
        var sam2Type = typeof(SAM2SegmentationManager);
        
        // Назначаем ONNX Encoder модель
        if (onnxEncoderModel != null)
        {
            var encoderField = sam2Type.GetField("sam2EncoderModel", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (encoderField != null)
            {
                encoderField.SetValue(sam2Manager, onnxEncoderModel);
                Debug.Log("✅ ONNX SAM Encoder назначен в SAM2SegmentationManager");
            }
        }
        
        // Включаем SAM2
        var enableSAM2Field = sam2Type.GetField("enableSAM2", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (enableSAM2Field != null)
        {
            enableSAM2Field.SetValue(sam2Manager, true);
            Debug.Log("✅ SAM2 включен в SAM2SegmentationManager");
        }
    }
    
    /// <summary>
    /// Обработчик результатов ONNX сегментации
    /// </summary>
    private void OnONNXSegmentationCompleted(Texture2D result)
    {
        Debug.Log("📤 Получен результат ONNX сегментации");
        
        // Передача результата в ARWallPresenter через AsyncSegmentationManager
        if (asyncManager != null)
        {
            var arWallPresenter = FindObjectOfType<ARWallPresenter>();
            if (arWallPresenter != null)
            {
                var renderTexture = new RenderTexture(result.width, result.height, 0, RenderTextureFormat.RFloat);
                Graphics.Blit(result, renderTexture);
                arWallPresenter.SetSegmentationMask(renderTexture);
                Debug.Log("📤 Результат передан в ARWallPresenter");
            }
        }
    }
    
    /// <summary>
    /// Проверка корректности интеграции
    /// </summary>
    private void ValidateIntegration()
    {
        Debug.Log("🔍 Проверка интеграции...");
        
        bool isValid = true;
        
        if (onnxEncoderModel == null)
        {
            Debug.LogWarning("⚠️ ONNX Encoder модель не найдена");
            isValid = false;
        }
        

        
        if (onnxSamManager == null)
        {
            Debug.LogWarning("⚠️ ONNXSAMManager не настроен");
            isValid = false;
        }
        
        if (asyncManager == null)
        {
            Debug.LogWarning("⚠️ AsyncSegmentationManager не найден");
            isValid = false;
        }
        
        if (isValid)
        {
            Debug.Log("✅ Интеграция прошла все проверки");
            statusMessage = "✅ Все компоненты настроены корректно";
        }
        else
        {
            Debug.LogWarning("⚠️ Интеграция завершена с предупреждениями");
            statusMessage = "⚠️ Интеграция с предупреждениями";
        }
    }
    
    /// <summary>
    /// Переключение между ONNX и TFLite моделями
    /// </summary>
    [ContextMenu("🔄 Toggle ONNX/TFLite")]
    public void ToggleONNXTFLite()
    {
        if (asyncManager == null) return;
        
        var asyncType = typeof(AsyncSegmentationManager);
        var useSAM2Field = asyncType.GetField("useSAM2Models", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (useSAM2Field != null)
        {
            bool currentValue = (bool)useSAM2Field.GetValue(asyncManager);
            useSAM2Field.SetValue(asyncManager, !currentValue);
            
            string mode = !currentValue ? "ONNX" : "TFLite";
            Debug.Log($"🔄 Переключено на {mode} модели");
            statusMessage = $"🔄 Активны {mode} модели";
        }
    }
    
    /// <summary>
    /// Диагностика системы
    /// </summary>
    [ContextMenu("🔍 Diagnose System")]
    public void DiagnoseSystem()
    {
        Debug.Log("=== ДИАГНОСТИКА ONNX SAM СИСТЕМЫ ===");
        Debug.Log($"Интеграция завершена: {integrationCompleted}");
        Debug.Log($"Статус: {statusMessage}");
        Debug.Log($"AsyncSegmentationManager: {(asyncManager != null ? "✅" : "❌")}");
        Debug.Log($"SAM2SegmentationManager: {(sam2Manager != null ? "✅" : "❌")}");
        Debug.Log($"ONNXSAMManager: {(onnxSamManager != null ? "✅" : "❌")}");
        Debug.Log($"ONNX Encoder: {(onnxEncoderModel != null ? onnxEncoderModel.name : "❌")}");
        Debug.Log("ONNX Decoder: Не используется (только Encoder)");
        
        if (onnxSamManager != null)
        {
            Debug.Log($"ONNXSAMManager инициализирован: {onnxSamManager.IsInitialized}");
            Debug.Log($"ONNXSAMManager обрабатывает: {onnxSamManager.IsProcessing}");
        }
        
        Debug.Log("=== КОНЕЦ ДИАГНОСТИКИ ===");
    }
    
    /// <summary>
    /// Принудительная переинициализация
    /// </summary>
    [ContextMenu("🔄 Force Reinitialize")]
    public void ForceReinitialize()
    {
        integrationCompleted = false;
        statusMessage = "🔄 Переинициализация...";
        
        // Отписываемся от событий
        if (onnxSamManager != null)
        {
            onnxSamManager.OnSegmentationCompleted -= OnONNXSegmentationCompleted;
        }
        
        // Запускаем интеграцию заново
        IntegrateONNXSAM();
    }
}
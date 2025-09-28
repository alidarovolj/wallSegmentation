using UnityEngine;
using Unity.Sentis;

/// <summary>
/// Быстрая настройка ONNX SAM моделей через Inspector
/// Простой интерфейс для замены TFLite на ONNX
/// </summary>
public class QuickONNXSetup : MonoBehaviour
{
    [Header("🚀 Quick ONNX SAM Setup")]
    [Space(10)]
    
    [Header("📋 Инструкция:")]
    [TextArea(3, 5)]
    [SerializeField] private string instructions = 
        "1. Нажмите 'Setup ONNX SAM' для автоматической настройки\n" +
        "2. Проверьте статус ниже\n" +
        "3. При необходимости используйте 'Test Integration'";
    
    [Header("🎯 Статус")]
    [SerializeField] private bool setupCompleted = false;
    [SerializeField] private string currentStatus = "Не настроено";
    
    [Header("🔧 Найденная модель")]
    [SerializeField] private ModelAsset foundEncoder;
    
    [Header("📊 Компоненты системы")]
    [SerializeField] private AsyncSegmentationManager asyncManager;
    [SerializeField] private ONNXSAMManager onnxManager;
    [SerializeField] private ONNXSAMIntegrator integrator;
    
    [Header("⚙️ Настройки")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool autoSetupOnStart = false;
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupONNXSAM();
        }
    }
    
    /// <summary>
    /// Основной метод быстрой настройки
    /// </summary>
    [ContextMenu("🚀 Setup ONNX SAM")]
    public void SetupONNXSAM()
    {
        LogDebug("🚀 Начинаем быструю настройку ONNX SAM...");
        currentStatus = "🔄 Настройка в процессе...";
        
        // 1. Поиск моделей
        FindONNXModels();
        
        // 2. Поиск/создание компонентов
        SetupComponents();
        
        // 3. Запуск интеграции
        RunIntegration();
        
        // 4. Финальная проверка
        ValidateSetup();
        
        LogDebug("✅ Быстрая настройка ONNX SAM завершена!");
    }
    
    /// <summary>
    /// Поиск ONNX SAM Encoder в проекте
    /// </summary>
    private void FindONNXModels()
    {
        LogDebug("🔍 Поиск ONNX SAM Encoder...");
        
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        foreach (var model in allModels)
        {
            if (model == null) continue;
            
            string modelName = model.name.ToLower();
            
            if (foundEncoder == null && modelName.Contains("samencoder"))
            {
                foundEncoder = model;
                LogDebug($"✅ Найден SAM Encoder: {model.name}");
                break; // Нашли что искали
            }
        }
        
        if (foundEncoder == null)
        {
            LogDebug("⚠️ ONNX SAM Encoder не найден");
            currentStatus = "⚠️ Encoder не найден";
        }
        else
        {
            LogDebug("✅ ONNX SAM Encoder найден");
        }
    }
    
    /// <summary>
    /// Настройка необходимых компонентов
    /// </summary>
    private void SetupComponents()
    {
        LogDebug("🔧 Настройка компонентов...");
        
        // Поиск AsyncSegmentationManager
        if (asyncManager == null)
        {
            asyncManager = FindObjectOfType<AsyncSegmentationManager>();
            if (asyncManager != null)
                LogDebug("✅ AsyncSegmentationManager найден");
            else
                LogDebug("⚠️ AsyncSegmentationManager не найден");
        }
        
        // Создание/поиск ONNXSAMManager
        if (onnxManager == null)
        {
            onnxManager = FindObjectOfType<ONNXSAMManager>();
            if (onnxManager == null)
            {
                var onnxGO = new GameObject("ONNXSAMManager");
                onnxManager = onnxGO.AddComponent<ONNXSAMManager>();
                LogDebug("✅ ONNXSAMManager создан");
            }
            else
            {
                LogDebug("✅ ONNXSAMManager найден");
            }
        }
        
        // Создание/поиск ONNXSAMIntegrator
        if (integrator == null)
        {
            integrator = FindObjectOfType<ONNXSAMIntegrator>();
            if (integrator == null)
            {
                var integratorGO = new GameObject("ONNXSAMIntegrator");
                integrator = integratorGO.AddComponent<ONNXSAMIntegrator>();
                LogDebug("✅ ONNXSAMIntegrator создан");
            }
            else
            {
                LogDebug("✅ ONNXSAMIntegrator найден");
            }
        }
    }
    
    /// <summary>
    /// Запуск интеграции
    /// </summary>
    private void RunIntegration()
    {
        if (integrator == null)
        {
            LogDebug("❌ ONNXSAMIntegrator не найден");
            currentStatus = "❌ Интегратор не найден";
            return;
        }
        
        LogDebug("🔗 Запуск интеграции...");
        integrator.IntegrateONNXSAM();
    }
    
    /// <summary>
    /// Проверка результатов настройки
    /// </summary>
    private void ValidateSetup()
    {
        LogDebug("🔍 Проверка настройки...");
        
        bool isValid = true;
        string issues = "";
        
        if (foundEncoder == null)
        {
            isValid = false;
            issues += "SAM Encoder не найден; ";
        }
        
        if (asyncManager == null)
        {
            isValid = false;
            issues += "AsyncManager не найден; ";
        }
        
        if (onnxManager == null)
        {
            isValid = false;
            issues += "ONNXManager не найден; ";
        }
        
        if (integrator == null)
        {
            isValid = false;
            issues += "Integrator не найден; ";
        }
        
        if (isValid)
        {
            setupCompleted = true;
            currentStatus = "✅ Настройка завершена успешно";
            LogDebug("🎉 Все компоненты настроены корректно!");
        }
        else
        {
            setupCompleted = false;
            currentStatus = $"❌ Проблемы: {issues.TrimEnd(' ', ';')}";
            LogDebug($"⚠️ Настройка завершена с проблемами: {issues}");
        }
    }
    
    /// <summary>
    /// Тестирование интеграции
    /// </summary>
    [ContextMenu("🧪 Test Integration")]
    public void TestIntegration()
    {
        LogDebug("🧪 Тестирование интеграции...");
        
        if (!setupCompleted)
        {
            LogDebug("⚠️ Сначала выполните настройку");
            return;
        }
        
        if (onnxManager != null)
        {
            onnxManager.TestONNXEncoder();
            LogDebug("🧪 Тест ONNX Encoder запущен");
        }
        
        if (integrator != null)
        {
            integrator.DiagnoseSystem();
            LogDebug("🔍 Диагностика системы выполнена");
        }
    }
    
    /// <summary>
    /// Сброс настроек
    /// </summary>
    [ContextMenu("🔄 Reset Setup")]
    public void ResetSetup()
    {
        LogDebug("🔄 Сброс настроек...");
        
        setupCompleted = false;
        currentStatus = "🔄 Сброшено";
        foundEncoder = null;
        
        LogDebug("✅ Настройки сброшены");
    }
    
    /// <summary>
    /// Переключение между ONNX и TFLite
    /// </summary>
    [ContextMenu("🔄 Toggle ONNX/TFLite")]
    public void ToggleModelType()
    {
        if (integrator != null)
        {
            integrator.ToggleONNXTFLite();
            LogDebug("🔄 Переключение типа моделей выполнено");
        }
        else
        {
            LogDebug("⚠️ Интегратор не найден");
        }
    }
    
    /// <summary>
    /// Показать информацию о найденных моделях
    /// </summary>
    [ContextMenu("📋 Show Model Info")]
    public void ShowModelInfo()
    {
        LogDebug("📋 Информация о модели:");
        
        if (foundEncoder != null)
            LogDebug($"  SAM Encoder: {foundEncoder.name}");
        else
            LogDebug("  SAM Encoder: не найден");
            
        LogDebug($"  Статус: {currentStatus}");
        LogDebug($"  Настройка завершена: {setupCompleted}");
    }
    
    /// <summary>
    /// Автоматическое исправление проблем
    /// </summary>
    [ContextMenu("🔧 Auto Fix Issues")]
    public void AutoFixIssues()
    {
        LogDebug("🔧 Автоматическое исправление проблем...");
        
        // Повторный поиск модели
        if (foundEncoder == null)
        {
            FindONNXModels();
        }
        
        // Повторная настройка компонентов
        SetupComponents();
        
        // Повторная интеграция
        if (integrator != null)
        {
            integrator.ForceReinitialize();
        }
        
        // Повторная проверка
        ValidateSetup();
        
        LogDebug("🔧 Автоматическое исправление завершено");
    }
    
    /// <summary>
    /// Логирование
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[QuickONNXSetup] {message}");
    }
    
    /// <summary>
    /// Обновление статуса в Inspector
    /// </summary>
    void OnValidate()
    {
        if (Application.isPlaying && setupCompleted)
        {
            // Обновляем информацию в реальном времени
            if (foundEncoder == null)
            {
                FindONNXModels();
            }
        }
    }
}
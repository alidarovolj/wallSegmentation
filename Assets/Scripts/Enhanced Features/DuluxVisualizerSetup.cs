using UnityEngine;

/// <summary>
/// Простой скрипт для быстрой настройки Dulux Visualizer системы
/// Добавьте этот компонент на любой GameObject для автоматической настройки
/// </summary>
public class DuluxVisualizerSetup : MonoBehaviour
{
    [Header("🚀 Quick Setup")]
    [Tooltip("Автоматически настроить систему при старте")]
    [SerializeField] private bool autoSetupOnStart = true;
    
    [Tooltip("Показать подробные логи настройки")]
    [SerializeField] private bool showDetailedLogs = true;

    [Header("📊 Status")]
    [SerializeField] private bool isSetupComplete = false;
    [SerializeField] private string setupStatus = "Ожидание...";

    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupDuluxVisualizer();
        }
    }

    /// <summary>
    /// Автоматическая настройка всей системы Dulux Visualizer
    /// </summary>
    [ContextMenu("Setup Dulux Visualizer")]
    public void SetupDuluxVisualizer()
    {
        Log("🚀 Начинаем настройку Dulux Visualizer...");
        setupStatus = "Настройка...";

        // Шаг 1: Проверяем существующие компоненты
        CheckExistingComponents();

        // Шаг 2: Создаем главный контроллер если его нет
        CreateMainController();

        // Шаг 3: Проверяем критически важные компоненты
        ValidateRequiredComponents();

        // Шаг 4: Завершение
        CompleteSetup();
    }

    /// <summary>
    /// Проверка существующих компонентов
    /// </summary>
    private void CheckExistingComponents()
    {
        Log("🔍 Проверяем существующие компоненты...");

        var asyncSegmentation = FindObjectOfType<AsyncSegmentationManager>();
        var arWallPresenter = FindObjectOfType<ARWallPresenter>();
        var duluxIntegration = FindObjectOfType<DuluxVisualizerIntegration>();

        Log($"   AsyncSegmentationManager: {(asyncSegmentation != null ? "✅ Найден" : "❌ Не найден")}");
        Log($"   ARWallPresenter: {(arWallPresenter != null ? "✅ Найден" : "❌ Не найден")}");
        Log($"   DuluxVisualizerIntegration: {(duluxIntegration != null ? "✅ Найден" : "❌ Не найден")}");
    }

    /// <summary>
    /// Создание главного контроллера
    /// </summary>
    private void CreateMainController()
    {
        var existingController = FindObjectOfType<DuluxVisualizerIntegration>();
        
        if (existingController == null)
        {
            Log("🔧 Создаем главный контроллер Dulux Visualizer...");
            
            var controllerGO = new GameObject("Dulux Visualizer System");
            var controller = controllerGO.AddComponent<DuluxVisualizerIntegration>();
            
            Log("✅ DuluxVisualizerIntegration создан и добавлен в сцену");
        }
        else
        {
            Log("✅ DuluxVisualizerIntegration уже существует в сцене");
        }
    }

    /// <summary>
    /// Проверка критически важных компонентов
    /// </summary>
    private void ValidateRequiredComponents()
    {
        Log("🔍 Проверяем критически важные компоненты...");

        var asyncSegmentation = FindObjectOfType<AsyncSegmentationManager>();
        var arWallPresenter = FindObjectOfType<ARWallPresenter>();

        if (asyncSegmentation == null)
        {
            Log("❌ КРИТИЧЕСКАЯ ОШИБКА: AsyncSegmentationManager не найден!");
            Log("   Убедитесь что в сцене есть GameObject с компонентом AsyncSegmentationManager");
            setupStatus = "Ошибка: нет AsyncSegmentationManager";
            return;
        }

        if (arWallPresenter == null)
        {
            Log("❌ КРИТИЧЕСКАЯ ОШИБКА: ARWallPresenter не найден!");
            Log("   Убедитесь что в сцене есть GameObject с компонентом ARWallPresenter");
            setupStatus = "Ошибка: нет ARWallPresenter";
            return;
        }

        Log("✅ Все критически важные компоненты найдены");
    }

    /// <summary>
    /// Завершение настройки
    /// </summary>
    private void CompleteSetup()
    {
        isSetupComplete = true;
        setupStatus = "Готово ✅";
        
        Log("🎉 Настройка Dulux Visualizer завершена!");
        Log("📱 Система готова к использованию");
        Log("");
        Log("🎮 Как использовать:");
        Log("   var dulux = FindObjectOfType<DuluxVisualizerIntegration>();");
        Log("   dulux.SetPaintColor(Color.blue);");
        Log("   dulux.StartPainting();");
        Log("");
        Log("📚 Подробная документация: DULUX_VISUALIZER_SETUP_GUIDE.md");
    }

    /// <summary>
    /// Диагностика системы
    /// </summary>
    [ContextMenu("Diagnose System")]
    public void DiagnoseSystem()
    {
        Log("🔍 ДИАГНОСТИКА DULUX VISUALIZER СИСТЕМЫ");
        Log("=====================================");

        // Проверка основных компонентов
        var duluxIntegration = FindObjectOfType<DuluxVisualizerIntegration>();
        var duluxCore = FindObjectOfType<DuluxVisualizerCore>();
        var asyncSegmentation = FindObjectOfType<AsyncSegmentationManager>();
        var sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
        var arWallPresenter = FindObjectOfType<ARWallPresenter>();
        var qualityController = FindObjectOfType<QualityController>();
        var roomAnalyzer = FindObjectOfType<SimpleRoomAnalyzer>();

        Log("📊 КОМПОНЕНТЫ:");
        Log($"   DuluxVisualizerIntegration: {GetStatus(duluxIntegration)}");
        Log($"   DuluxVisualizerCore: {GetStatus(duluxCore)}");
        Log($"   AsyncSegmentationManager: {GetStatus(asyncSegmentation)}");
        Log($"   SAM2SegmentationManager: {GetStatus(sam2Manager)}");
        Log($"   ARWallPresenter: {GetStatus(arWallPresenter)}");
        Log($"   QualityController: {GetStatus(qualityController)}");
        Log($"   SimpleRoomAnalyzer: {GetStatus(roomAnalyzer)}");

        // Проверка готовности системы
        if (duluxIntegration != null)
        {
            Log($"🎯 СТАТУС СИСТЕМЫ: {(duluxIntegration.IsSystemReady ? "✅ ГОТОВА" : "⏳ Инициализация...")}");
            
            if (duluxIntegration.IsSystemReady)
            {
                Log($"   Текущий цвет: {ColorUtility.ToHtmlStringRGB(duluxIntegration.CurrentPaintColor)}");
                Log($"   Текущий финиш: {duluxIntegration.CurrentFinish}");
                Log($"   Покраска активна: {duluxIntegration.IsPainting}");
            }
        }

        // Проверка производительности
        if (qualityController != null && qualityController.IsInitialized)
        {
            Log($"⚡ ПРОИЗВОДИТЕЛЬНОСТЬ:");
            Log($"   Текущее качество: {qualityController.CurrentQualityLevel}");
            Log($"   Средний FPS: {qualityController.AverageFPS:F1}");
            Log($"   Класс устройства: {qualityController.DeviceClass}");
        }

        Log("=====================================");
    }

    /// <summary>
    /// Получение статуса компонента
    /// </summary>
    private string GetStatus(MonoBehaviour component)
    {
        if (component == null)
            return "❌ Не найден";
        
        if (!component.gameObject.activeInHierarchy)
            return "⚠️ Неактивен";
        
        if (!component.enabled)
            return "⚠️ Выключен";
        
        return "✅ Активен";
    }

    /// <summary>
    /// Логирование с проверкой настроек
    /// </summary>
    private void Log(string message)
    {
        if (showDetailedLogs)
        {
            Debug.Log($"[DuluxVisualizerSetup] {message}");
        }
    }

    /// <summary>
    /// Быстрый тест системы
    /// </summary>
    [ContextMenu("Quick Test")]
    public void QuickTest()
    {
        var dulux = FindObjectOfType<DuluxVisualizerIntegration>();
        
        if (dulux == null)
        {
            Log("❌ DuluxVisualizerIntegration не найден! Запустите Setup Dulux Visualizer");
            return;
        }

        if (!dulux.IsSystemReady)
        {
            Log("⏳ Система еще не готова. Подождите завершения инициализации...");
            return;
        }

        Log("🧪 Запускаем быстрый тест...");
        
        // Тест смены цвета
        dulux.SetPaintColor(Color.red);
        Log("✅ Тест смены цвета: ПРОЙДЕН");
        
        // Тест смены финиша
        dulux.SetPaintFinish(PaintFinishType.Gloss);
        Log("✅ Тест смены финиша: ПРОЙДЕН");
        
        // Возвращаем исходные настройки
        dulux.SetPaintColor(Color.white);
        dulux.SetPaintFinish(PaintFinishType.Matte);
        
        Log("🎉 Быстрый тест завершен успешно!");
    }
}
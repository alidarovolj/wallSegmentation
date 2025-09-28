using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Главный контроллер интеграции Dulux Visualizer
/// Объединяет все компоненты системы и обеспечивает единый API
/// </summary>
public class DuluxVisualizerIntegration : MonoBehaviour
{
    [Header("🎯 System Status")]
    [Tooltip("Статус инициализации системы")]
    [SerializeField] private bool isSystemReady = false;
    
    [Tooltip("Текущий режим работы")]
    [SerializeField] private VisualizerMode currentMode = VisualizerMode.WallPainting;

    [Header("🎨 Paint Settings")]
    [Tooltip("Текущий цвет краски")]
    [SerializeField] private Color currentPaintColor = Color.white;
    
    [Tooltip("Текущий тип финиша")]
    [SerializeField] private PaintFinishType currentFinish = PaintFinishType.Matte;
    
    [Tooltip("Коллекция доступных цветов")]
    [SerializeField] private PaintColorCollection colorCollection;

    [Header("🏠 Room Settings")]
    [Tooltip("Автоматический анализ помещения")]
    [SerializeField] private bool enableRoomAnalysis = true;
    
    [Tooltip("Показать информацию о помещении")]
    [SerializeField] private bool showRoomInfo = false;

    [Header("⚙️ Performance")]
    [Tooltip("Автоматическая оптимизация производительности")]
    [SerializeField] private bool enableAutoOptimization = true;
    
    [Tooltip("Целевой FPS")]
    [SerializeField] private int targetFPS = 30;

    [Header("🔧 Components")]
    [SerializeField] private DuluxVisualizerCore visualizerCore;
    [SerializeField] private AsyncSegmentationManager segmentationManager;
    [SerializeField] private SAM2SegmentationManager sam2Manager;
    [SerializeField] private ARWallPresenter wallPresenter;
    [SerializeField] private QualityController qualityController;
    [SerializeField] private SimpleRoomAnalyzer roomAnalyzer;
    [SerializeField] private LightingAnalyzer lightingAnalyzer;
    [SerializeField] private PaintRenderer paintRenderer;

    // Состояние системы
    private bool isInitialized = false;
    private bool isPainting = false;
    private SimpleRoomData currentRoom;
    private LightingData currentLighting;

    // События для UI
    public System.Action OnSystemReady;
    public System.Action<Color> OnPaintColorChanged;
    public System.Action<PaintFinishType> OnFinishChanged;
    public System.Action<SimpleRoomData> OnRoomAnalyzed;
    public System.Action<float> OnPerformanceChanged;
    public System.Action<string> OnStatusMessage;

    void Start()
    {
        StartCoroutine(InitializeSystem());
    }

    /// <summary>
    /// Инициализация всей системы Dulux Visualizer
    /// </summary>
    private IEnumerator InitializeSystem()
    {
        OnStatusMessage?.Invoke("🚀 Инициализация Dulux Visualizer...");
        
        // Этап 1: Поиск и проверка компонентов
        yield return StartCoroutine(FindAndValidateComponents());
        
        // Этап 2: Инициализация базовых компонентов
        yield return StartCoroutine(InitializeBaseComponents());
        
        // Этап 3: Инициализация продвинутых функций
        yield return StartCoroutine(InitializeAdvancedFeatures());
        
        // Этап 4: Настройка интеграции
        yield return StartCoroutine(SetupIntegration());
        
        // Этап 5: Финальная проверка
        yield return StartCoroutine(FinalValidation());
        
        isSystemReady = true;
        isInitialized = true;
        
        OnSystemReady?.Invoke();
        OnStatusMessage?.Invoke("✅ Dulux Visualizer готов к работе!");
        
        Debug.Log("🎉 Dulux Visualizer Integration полностью инициализирован!");
    }

    /// <summary>
    /// Поиск и проверка компонентов
    /// </summary>
    private IEnumerator FindAndValidateComponents()
    {
        OnStatusMessage?.Invoke("🔍 Поиск компонентов системы...");
        
        // Автопоиск компонентов
        if (visualizerCore == null)
            visualizerCore = FindObjectOfType<DuluxVisualizerCore>();
        
        if (segmentationManager == null)
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        
        if (sam2Manager == null)
            sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
        
        if (wallPresenter == null)
            wallPresenter = FindObjectOfType<ARWallPresenter>();
        
        if (qualityController == null)
            qualityController = FindObjectOfType<QualityController>();
        
        if (roomAnalyzer == null)
            roomAnalyzer = FindObjectOfType<SimpleRoomAnalyzer>();
        
        if (lightingAnalyzer == null)
            lightingAnalyzer = FindObjectOfType<LightingAnalyzer>();
        
        if (paintRenderer == null)
            paintRenderer = FindObjectOfType<PaintRenderer>();

        // Создаем недостающие компоненты
        yield return StartCoroutine(CreateMissingComponents());
        
        // Проверяем критически важные компоненты
        ValidateCriticalComponents();
        
        yield return null;
    }

    /// <summary>
    /// Создание недостающих компонентов
    /// </summary>
    private IEnumerator CreateMissingComponents()
    {
        if (visualizerCore == null)
        {
            var coreGO = new GameObject("Dulux Visualizer Core");
            visualizerCore = coreGO.AddComponent<DuluxVisualizerCore>();
            Debug.Log("✅ Создан DuluxVisualizerCore");
        }
        
        if (qualityController == null)
        {
            var qualityGO = new GameObject("Quality Controller");
            qualityController = qualityGO.AddComponent<QualityController>();
            Debug.Log("✅ Создан QualityController");
        }
        
        yield return null;
    }

    /// <summary>
    /// Проверка критически важных компонентов
    /// </summary>
    private void ValidateCriticalComponents()
    {
        if (segmentationManager == null)
        {
            Debug.LogError("❌ AsyncSegmentationManager не найден! Система не может работать без сегментации.");
            return;
        }
        
        if (wallPresenter == null)
        {
            Debug.LogError("❌ ARWallPresenter не найден! Визуализация невозможна.");
            return;
        }
        
        Debug.Log("✅ Все критически важные компоненты найдены");
    }

    /// <summary>
    /// Инициализация базовых компонентов
    /// </summary>
    private IEnumerator InitializeBaseComponents()
    {
        OnStatusMessage?.Invoke("⚙️ Инициализация базовых компонентов...");
        
        // Инициализация контроллера качества
        if (qualityController != null)
        {
            qualityController.Initialize();
            yield return new WaitForSeconds(0.5f);
        }
        
        // Инициализация анализатора освещения
        if (lightingAnalyzer != null)
        {
            var lightEstimation = FindObjectOfType<ARLightEstimation>();
            if (lightEstimation == null)
            {
                var lightGO = new GameObject("AR Light Estimation");
                lightEstimation = lightGO.AddComponent<ARLightEstimation>();
            }
            lightingAnalyzer.Initialize(lightEstimation);
            yield return new WaitForSeconds(0.3f);
        }
        
        // Инициализация рендерера краски
        if (paintRenderer != null && wallPresenter != null)
        {
            paintRenderer.Initialize(wallPresenter, currentPaintColor, currentFinish);
            yield return new WaitForSeconds(0.3f);
        }
        
        yield return null;
    }

    /// <summary>
    /// Инициализация продвинутых функций
    /// </summary>
    private IEnumerator InitializeAdvancedFeatures()
    {
        OnStatusMessage?.Invoke("🚀 Инициализация продвинутых функций...");
        
        // Инициализация анализатора помещения
        if (roomAnalyzer != null && enableRoomAnalysis)
        {
            roomAnalyzer.Initialize(visualizerCore);
            yield return new WaitForSeconds(0.3f);
        }
        
        // Инициализация ядра визуализатора
        if (visualizerCore != null)
        {
            // Ядро инициализируется автоматически в Start()
            yield return new WaitUntil(() => visualizerCore.IsInitialized);
        }
        
        yield return null;
    }

    /// <summary>
    /// Настройка интеграции между компонентами
    /// </summary>
    private IEnumerator SetupIntegration()
    {
        OnStatusMessage?.Invoke("🔗 Настройка интеграции компонентов...");
        
        // Подписка на события
        SetupEventHandlers();
        
        // Синхронизация настроек
        SynchronizeSettings();
        
        // Настройка автоматической оптимизации
        if (enableAutoOptimization && qualityController != null)
        {
            qualityController.SetQualityLevel(QualityLevel.Auto);
        }
        
        yield return null;
    }

    /// <summary>
    /// Настройка обработчиков событий
    /// </summary>
    private void SetupEventHandlers()
    {
        // События изменения цвета
        if (visualizerCore != null)
        {
            visualizerCore.OnPaintColorChanged += HandlePaintColorChanged;
            visualizerCore.OnPaintFinishChanged += HandlePaintFinishChanged;
        }
        
        // События анализа помещения
        if (roomAnalyzer != null)
        {
            roomAnalyzer.OnRoomAnalyzed += HandleRoomAnalyzed;
            roomAnalyzer.OnAnalysisProgress += HandleAnalysisProgress;
        }
        
        // События производительности
        if (qualityController != null)
        {
            qualityController.OnPerformanceChanged += HandlePerformanceChanged;
            qualityController.OnQualityLevelChanged += HandleQualityLevelChanged;
        }
        
        // События освещения
        if (lightingAnalyzer != null)
        {
            lightingAnalyzer.OnLightingChanged += HandleLightingChanged;
        }
    }

    /// <summary>
    /// Синхронизация настроек между компонентами
    /// </summary>
    private void SynchronizeSettings()
    {
        // Синхронизация цвета краски
        if (visualizerCore != null)
        {
            visualizerCore.SetPaintColor(currentPaintColor);
            visualizerCore.SetPaintFinish(currentFinish);
        }
        
        // Синхронизация настроек сегментации
        if (segmentationManager != null)
        {
            segmentationManager.SetPaintColor(currentPaintColor);
            segmentationManager.SetSelectedClass(0); // Стены по умолчанию
        }
    }

    /// <summary>
    /// Финальная проверка системы
    /// </summary>
    private IEnumerator FinalValidation()
    {
        OnStatusMessage?.Invoke("🔍 Финальная проверка системы...");
        
        // Проверяем готовность всех компонентов
        bool allReady = true;
        
        if (visualizerCore != null && !visualizerCore.IsInitialized)
        {
            Debug.LogWarning("⚠️ DuluxVisualizerCore не готов");
            allReady = false;
        }
        
        if (qualityController != null && !qualityController.IsInitialized)
        {
            Debug.LogWarning("⚠️ QualityController не готов");
            allReady = false;
        }
        
        if (segmentationManager != null && !segmentationManager.GetIsInitialized())
        {
            Debug.LogWarning("⚠️ AsyncSegmentationManager не готов");
            allReady = false;
        }
        
        if (!allReady)
        {
            Debug.LogWarning("⚠️ Не все компоненты готовы, но система будет работать");
        }
        
        yield return null;
    }

    // ===== ПУБЛИЧНЫЕ МЕТОДЫ API =====

    /// <summary>
    /// Изменение цвета краски
    /// </summary>
    public void SetPaintColor(Color color)
    {
        currentPaintColor = color;
        
        if (visualizerCore != null)
            visualizerCore.SetPaintColor(color);
        
        if (segmentationManager != null)
            segmentationManager.SetPaintColor(color);
        
        OnPaintColorChanged?.Invoke(color);
    }

    /// <summary>
    /// Изменение типа финиша
    /// </summary>
    public void SetPaintFinish(PaintFinishType finish)
    {
        currentFinish = finish;
        
        if (visualizerCore != null)
            visualizerCore.SetPaintFinish(finish);
        
        OnFinishChanged?.Invoke(finish);
    }

    /// <summary>
    /// Начало покраски
    /// </summary>
    public void StartPainting()
    {
        if (!isSystemReady)
        {
            Debug.LogWarning("⚠️ Система еще не готова к покраске");
            return;
        }
        
        isPainting = true;
        
        if (visualizerCore != null)
            visualizerCore.StartPainting();
        
        OnStatusMessage?.Invoke("🖌️ Покраска начата");
    }

    /// <summary>
    /// Остановка покраски
    /// </summary>
    public void StopPainting()
    {
        isPainting = false;
        
        if (visualizerCore != null)
            visualizerCore.StopPainting();
        
        OnStatusMessage?.Invoke("⏹️ Покраска остановлена");
    }

    /// <summary>
    /// Запуск анализа помещения
    /// </summary>
    public void AnalyzeRoom()
    {
        if (roomAnalyzer != null && enableRoomAnalysis)
        {
            roomAnalyzer.StartRoomAnalysis();
            OnStatusMessage?.Invoke("🏠 Анализ помещения запущен...");
        }
    }

    /// <summary>
    /// Переключение режима качества
    /// </summary>
    public void SetQualityMode(QualityLevel level)
    {
        if (qualityController != null)
        {
            qualityController.SetQualityLevel(level);
        }
    }

    /// <summary>
    /// Получение доступных цветов
    /// </summary>
    public Color[] GetAvailableColors()
    {
        if (colorCollection != null)
        {
            var allColors = new List<Color>();
            allColors.AddRange(colorCollection.popularColors);
            allColors.AddRange(colorCollection.neutralColors);
            allColors.AddRange(colorCollection.accentColors);
            return allColors.ToArray();
        }
        
        // Цвета по умолчанию
        return new Color[]
        {
            Color.white, Color.red, Color.green, Color.blue,
            Color.yellow, Color.cyan, Color.magenta, Color.gray
        };
    }

    // ===== ОБРАБОТЧИКИ СОБЫТИЙ =====

    private void HandlePaintColorChanged(Color color)
    {
        currentPaintColor = color;
        OnPaintColorChanged?.Invoke(color);
    }

    private void HandlePaintFinishChanged(PaintFinishType finish)
    {
        currentFinish = finish;
        OnFinishChanged?.Invoke(finish);
    }

    private void HandleRoomAnalyzed(SimpleRoomData room)
    {
        currentRoom = room;
        OnRoomAnalyzed?.Invoke(room);
        OnStatusMessage?.Invoke($"🏠 Помещение проанализировано: {room.area:F1}м²");
    }

    private void HandleAnalysisProgress(float progress)
    {
        OnStatusMessage?.Invoke($"🔍 Анализ помещения: {progress * 100:F0}%");
    }

    private void HandlePerformanceChanged(float fps)
    {
        OnPerformanceChanged?.Invoke(fps);
        
        if (fps < targetFPS * 0.8f)
        {
            OnStatusMessage?.Invoke($"⚠️ Производительность: {fps:F0} FPS");
        }
    }

    private void HandleQualityLevelChanged(QualityLevel level)
    {
        OnStatusMessage?.Invoke($"🎛️ Качество изменено: {level}");
    }

    private void HandleLightingChanged(LightingData lighting)
    {
        currentLighting = lighting;
    }

    // ===== ПУБЛИЧНЫЕ СВОЙСТВА =====

    public bool IsSystemReady => isSystemReady;
    public bool IsPainting => isPainting;
    public Color CurrentPaintColor => currentPaintColor;
    public PaintFinishType CurrentFinish => currentFinish;
    public SimpleRoomData CurrentRoom => currentRoom;
    public LightingData CurrentLighting => currentLighting;
    public VisualizerMode CurrentMode => currentMode;

    void OnDestroy()
    {
        // Отписка от событий
        if (visualizerCore != null)
        {
            visualizerCore.OnPaintColorChanged -= HandlePaintColorChanged;
            visualizerCore.OnPaintFinishChanged -= HandlePaintFinishChanged;
        }
        
        if (roomAnalyzer != null)
        {
            roomAnalyzer.OnRoomAnalyzed -= HandleRoomAnalyzed;
            roomAnalyzer.OnAnalysisProgress -= HandleAnalysisProgress;
        }
        
        if (qualityController != null)
        {
            qualityController.OnPerformanceChanged -= HandlePerformanceChanged;
            qualityController.OnQualityLevelChanged -= HandleQualityLevelChanged;
        }
        
        if (lightingAnalyzer != null)
        {
            lightingAnalyzer.OnLightingChanged -= HandleLightingChanged;
        }
    }
}

/// <summary>
/// Режимы работы визуализатора
/// </summary>
public enum VisualizerMode
{
    WallPainting,    // Покраска стен
    FloorCovering,   // Покрытие пола
    CeilingPainting, // Покраска потолка
    FullRoom         // Полное помещение
}
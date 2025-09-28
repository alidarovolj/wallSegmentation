using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Ядро системы визуализации уровня Dulux Visualizer
/// Обеспечивает фотореалистичную покраску стен с учетом освещения и текстур
/// </summary>
public class DuluxVisualizerCore : MonoBehaviour
{
    [Header("🎨 Paint System")]
    [Tooltip("Текущий выбранный цвет краски")]
    [SerializeField] private Color selectedPaintColor = Color.white;
    
    [Tooltip("Коллекция доступных цветов краски")]
    [SerializeField] private PaintColorCollection paintColors;
    
    [Tooltip("Тип финиша краски (матовый, глянцевый, полуглянцевый)")]
    [SerializeField] private PaintFinishType paintFinish = PaintFinishType.Matte;

    [Header("🏠 Room Analysis")]
    [Tooltip("Автоматический анализ помещения для лучшей визуализации")]
    [SerializeField] private bool enableRoomAnalysis = true;
    
    [Tooltip("Детекция углов и краев стен")]
    [SerializeField] private bool enableEdgeDetection = true;
    
    [Tooltip("Анализ освещения помещения")]
    [SerializeField] private bool enableLightingAnalysis = true;

    [Header("📱 AR Enhancement")]
    [Tooltip("Стабилизация трекинга для плавной покраски")]
    [SerializeField] private bool enableTrackingStabilization = true;
    
    [Tooltip("Коррекция перспективы для реалистичного отображения")]
    [SerializeField] private bool enablePerspectiveCorrection = true;
    
    [Tooltip("Адаптивное качество в зависимости от производительности")]
    [SerializeField] private bool enableAdaptiveQuality = true;

    [Header("🎯 Segmentation Enhancement")]
    [Tooltip("Использовать SAM2 для улучшенной сегментации")]
    [SerializeField] private bool useSAM2Enhancement = true;
    
    [Tooltip("Точность сегментации (выше = медленнее)")]
    [Range(0.5f, 1.0f)]
    [SerializeField] private float segmentationAccuracy = 0.8f;
    
    [Tooltip("Сглаживание краев маски")]
    [Range(0f, 5f)]
    [SerializeField] private float edgeSmoothing = 2f;

    [Header("💡 Lighting System")]
    [Tooltip("Автоматическая коррекция цвета под освещение")]
    [SerializeField] private bool enableColorCorrection = true;
    
    [Tooltip("Симуляция теней на окрашенной поверхности")]
    [SerializeField] private bool enableShadowSimulation = true;
    
    [Tooltip("Интенсивность коррекции освещения")]
    [Range(0f, 1f)]
    [SerializeField] private float lightingIntensity = 0.7f;

    [Header("🔧 Dependencies")]
    [SerializeField] private AsyncSegmentationManager segmentationManager;
    [SerializeField] private SAM2SegmentationManager sam2Manager;
    [SerializeField] private ARWallPresenter wallPresenter;
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private ARLightEstimation lightEstimation;

    // Внутренние компоненты
    private SimpleRoomAnalyzer roomAnalyzer;
    private LightingAnalyzer lightingAnalyzer;
    private PaintRenderer paintRenderer;
    private QualityController qualityController;

    // Состояние системы
    private bool isInitialized = false;
    private bool isPainting = false;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;

    // События
    public System.Action<Color> OnPaintColorChanged;
    public System.Action<PaintFinishType> OnPaintFinishChanged;
    public System.Action<float> OnRoomAnalysisProgress;
    public System.Action OnRoomAnalysisComplete;

    void Start()
    {
        InitializeDuluxSystem();
    }

    void Update()
    {
        if (!isInitialized) return;

        UpdateTrackingStabilization();
        UpdateAdaptiveQuality();
        UpdateLightingAnalysis();
    }

    /// <summary>
    /// Инициализация системы Dulux Visualizer
    /// </summary>
    private void InitializeDuluxSystem()
    {
        Debug.Log("🚀 Инициализация Dulux Visualizer Core...");

        // Автопоиск зависимостей
        FindDependencies();

        // Инициализация подсистем
        InitializeSubsystems();

        // Настройка качества
        ConfigureQualitySettings();

        isInitialized = true;
        Debug.Log("✅ Dulux Visualizer Core инициализирован!");
    }

    /// <summary>
    /// Автоматический поиск зависимостей
    /// </summary>
    private void FindDependencies()
    {
        if (segmentationManager == null)
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        
        if (sam2Manager == null)
            sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
        
        if (wallPresenter == null)
            wallPresenter = FindObjectOfType<ARWallPresenter>();
        
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();

        // Создаем ARLightEstimation если его нет
        if (lightEstimation == null)
        {
            var lightEstimationGO = new GameObject("AR Light Estimation");
            lightEstimation = lightEstimationGO.AddComponent<ARLightEstimation>();
        }
    }

    /// <summary>
    /// Инициализация подсистем
    /// </summary>
    private void InitializeSubsystems()
    {
        // Анализатор помещения
        if (enableRoomAnalysis)
        {
            roomAnalyzer = gameObject.AddComponent<SimpleRoomAnalyzer>();
            roomAnalyzer.Initialize(this);
        }

        // Анализатор освещения
        if (enableLightingAnalysis)
        {
            lightingAnalyzer = gameObject.AddComponent<LightingAnalyzer>();
            lightingAnalyzer.Initialize(lightEstimation);
        }

        // Рендерер краски
        paintRenderer = gameObject.AddComponent<PaintRenderer>();
        paintRenderer.Initialize(wallPresenter, selectedPaintColor, paintFinish);

        // Контроллер качества
        if (enableAdaptiveQuality)
        {
            qualityController = gameObject.AddComponent<QualityController>();
            qualityController.Initialize();
        }
    }

    /// <summary>
    /// Настройка параметров качества
    /// </summary>
    private void ConfigureQualitySettings()
    {
        if (segmentationManager != null)
        {
            // Настраиваем сегментацию для максимального качества
            segmentationManager.SetUseSAM2(useSAM2Enhancement);
            
            // Устанавливаем высокое разрешение для лучшей точности
            var highResolution = new Vector2Int(1024, 1024);
            segmentationManager.SetProcessingResolution(highResolution);
            
            // Включаем сглаживание краев
            segmentationManager.SetMaskSmoothing(true, Mathf.RoundToInt(edgeSmoothing));
        }
    }

    /// <summary>
    /// Обновление стабилизации трекинга
    /// </summary>
    private void UpdateTrackingStabilization()
    {
        if (!enableTrackingStabilization || arCameraManager == null) return;

        var currentPos = arCameraManager.transform.position;
        var currentRot = arCameraManager.transform.rotation;

        // Проверяем значительные изменения позиции камеры
        float positionDelta = Vector3.Distance(currentPos, lastCameraPosition);
        float rotationDelta = Quaternion.Angle(currentRot, lastCameraRotation);

        if (positionDelta > 0.1f || rotationDelta > 5f)
        {
            // Обновляем анализ помещения при значительном движении
            if (roomAnalyzer != null)
            {
                roomAnalyzer.UpdateAnalysis();
            }

            lastCameraPosition = currentPos;
            lastCameraRotation = currentRot;
        }
    }

    /// <summary>
    /// Обновление адаптивного качества
    /// </summary>
    private void UpdateAdaptiveQuality()
    {
        if (!enableAdaptiveQuality || qualityController == null) return;

        qualityController.UpdateQuality();
    }

    /// <summary>
    /// Обновление анализа освещения
    /// </summary>
    private void UpdateLightingAnalysis()
    {
        if (!enableLightingAnalysis || lightingAnalyzer == null) return;

        lightingAnalyzer.UpdateLighting();
        
        // Применяем коррекцию цвета если включена
        if (enableColorCorrection)
        {
            var correctedColor = lightingAnalyzer.GetColorCorrectedPaint(selectedPaintColor);
            paintRenderer?.UpdatePaintColor(correctedColor);
        }
    }

    /// <summary>
    /// Публичные методы для управления системой
    /// </summary>
    public void SetPaintColor(Color color)
    {
        selectedPaintColor = color;
        paintRenderer?.UpdatePaintColor(color);
        OnPaintColorChanged?.Invoke(color);
        
        Debug.Log($"🎨 Цвет краски изменен: {ColorUtility.ToHtmlStringRGB(color)}");
    }

    public void SetPaintFinish(PaintFinishType finish)
    {
        paintFinish = finish;
        paintRenderer?.UpdatePaintFinish(finish);
        OnPaintFinishChanged?.Invoke(finish);
        
        Debug.Log($"✨ Финиш краски изменен: {finish}");
    }

    public void StartPainting()
    {
        isPainting = true;
        paintRenderer?.StartPainting();
        Debug.Log("🖌️ Начата покраска");
    }

    public void StopPainting()
    {
        isPainting = false;
        paintRenderer?.StopPainting();
        Debug.Log("⏹️ Покраска остановлена");
    }

    public void StartRoomAnalysis()
    {
        if (roomAnalyzer != null)
        {
            StartCoroutine(roomAnalyzer.AnalyzeRoom());
        }
    }

    public bool IsInitialized => isInitialized;
    public bool IsPainting => isPainting;
    public Color CurrentPaintColor => selectedPaintColor;
    public PaintFinishType CurrentPaintFinish => paintFinish;

    // Методы для интеграции с AsyncSegmentationManager
    public void SetProcessingResolution(Vector2Int resolution)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetProcessingResolution(resolution);
        }
    }

    public void SetFrameSkip(int skipRate)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetFrameSkip(skipRate);
        }
    }

    public void SetMaskSmoothing(bool enabled, int iterations)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetMaskSmoothing(enabled, iterations);
        }
    }

    public void SetUseSAM2(bool enabled)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetUseSAM2(enabled);
        }
    }

    public Texture2D GetCurrentMask()
    {
        if (segmentationManager != null)
        {
            return segmentationManager.GetCurrentMask();
        }
        return null;
    }

    public int GetMaskRotationMode()
    {
        if (segmentationManager != null)
        {
            return segmentationManager.GetMaskRotationMode();
        }
        return 0;
    }

    public bool GetFlipHorizontal()
    {
        if (segmentationManager != null)
        {
            return segmentationManager.GetFlipHorizontal();
        }
        return false;
    }
}

/// <summary>
/// Типы финиша краски
/// </summary>
public enum PaintFinishType
{
    Matte,      // Матовый
    Satin,      // Полуглянцевый
    Gloss,      // Глянцевый
    SemiGloss   // Полуматовый
}

/// <summary>
/// Коллекция цветов краски
/// </summary>
[System.Serializable]
public class PaintColorCollection
{
    [Header("Популярные цвета")]
    public Color[] popularColors = new Color[]
    {
        Color.white,
        new Color(0.95f, 0.95f, 0.9f), // Кремовый
        new Color(0.9f, 0.9f, 0.85f),  // Слоновая кость
        new Color(0.85f, 0.85f, 0.8f), // Светло-серый
        new Color(0.7f, 0.8f, 0.9f),   // Светло-голубой
        new Color(0.9f, 0.8f, 0.7f),   // Персиковый
    };

    [Header("Нейтральные тона")]
    public Color[] neutralColors = new Color[]
    {
        new Color(0.5f, 0.5f, 0.5f),   // Серый
        new Color(0.4f, 0.4f, 0.4f),   // Темно-серый
        new Color(0.6f, 0.55f, 0.5f),  // Бежевый
        new Color(0.55f, 0.5f, 0.45f), // Тауп
    };

    [Header("Акцентные цвета")]
    public Color[] accentColors = new Color[]
    {
        new Color(0.2f, 0.4f, 0.8f),   // Синий
        new Color(0.8f, 0.2f, 0.2f),   // Красный
        new Color(0.2f, 0.8f, 0.2f),   // Зеленый
        new Color(0.8f, 0.6f, 0.2f),   // Оранжевый
    };
}
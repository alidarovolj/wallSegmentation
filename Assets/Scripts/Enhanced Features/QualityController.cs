using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Контроллер адаптивного качества для оптимальной производительности
/// Автоматически настраивает параметры в зависимости от производительности устройства
/// </summary>
public class QualityController : MonoBehaviour
{
    [Header("📊 Performance Monitoring")]
    [Tooltip("Целевой FPS для поддержания")]
    [SerializeField] private int targetFPS = 30;
    
    [Tooltip("Минимальный FPS перед снижением качества")]
    [SerializeField] private int minFPS = 20;
    
    [Tooltip("Интервал мониторинга производительности (сек)")]
    [SerializeField] private float monitoringInterval = 2.0f;

    [Header("🎛️ Quality Levels")]
    [Tooltip("Текущий уровень качества")]
    [SerializeField] private QualityLevel currentQualityLevel = QualityLevel.Auto;
    
    [Tooltip("Автоматическая адаптация качества")]
    [SerializeField] private bool enableAutoQuality = true;
    
    [Tooltip("Агрессивность оптимизации")]
    [Range(0f, 1f)]
    [SerializeField] private float optimizationAggression = 0.5f;

    [Header("📱 Device Detection")]
    [SerializeField] private DevicePerformanceClass deviceClass = DevicePerformanceClass.Unknown;
    [SerializeField] private bool isLowEndDevice = false;
    [SerializeField] private bool isMidRangeDevice = false;
    [SerializeField] private bool isHighEndDevice = false;

    [Header("🔧 Current Settings")]
    [SerializeField] private Enhanced_Features.QualitySettings currentSettings;

    // Мониторинг производительности
    private Queue<float> fpsHistory = new Queue<float>();
    private float lastMonitoringTime = 0f;
    private float averageFPS = 60f;
    private float frameTime = 0f;
    private int frameCount = 0;

    // Зависимости
    private AsyncSegmentationManager segmentationManager;
    private SAM2SegmentationManager sam2Manager;
    private PaintRenderer paintRenderer;
    private DuluxVisualizerCore visualizerCore;

    // Состояние
    private bool isInitialized = false;
    private bool isOptimizing = false;
    private QualityLevel lastQualityLevel;

    // События
    public System.Action<QualityLevel> OnQualityLevelChanged;
    public System.Action<float> OnPerformanceChanged;
    public System.Action<DevicePerformanceClass> OnDeviceClassDetected;

    public void Initialize()
    {
        // Поиск зависимостей
        FindDependencies();

        // Определение класса устройства
        DetectDevicePerformance();

        // Инициализация настроек качества
        InitializeQualitySettings();

        // Применение начальных настроек
        ApplyQualitySettings();

        isInitialized = true;
        Debug.Log($"🎛️ QualityController инициализирован. Класс устройства: {deviceClass}");
    }

    void Update()
    {
        if (!isInitialized) return;

        // Мониторинг производительности
        MonitorPerformance();

        // Автоматическая адаптация качества
        if (enableAutoQuality && Time.time - lastMonitoringTime >= monitoringInterval)
        {
            UpdateQuality();
            lastMonitoringTime = Time.time;
        }
    }

    /// <summary>
    /// Обновление качества на основе производительности
    /// </summary>
    public void UpdateQuality()
    {
        if (!enableAutoQuality || isOptimizing) return;

        float currentFPS = 1.0f / Time.deltaTime;
        CalculateAverageFPS(currentFPS);

        // Определяем нужно ли изменить качество
        QualityLevel targetQuality = DetermineOptimalQuality();

        if (targetQuality != currentQualityLevel)
        {
            StartCoroutine(ChangeQualityGradually(targetQuality));
        }
    }

    /// <summary>
    /// Принудительная установка уровня качества
    /// </summary>
    public void SetQualityLevel(QualityLevel level)
    {
        if (level == QualityLevel.Auto)
        {
            enableAutoQuality = true;
            Debug.Log("🔄 Включен автоматический режим качества");
        }
        else
        {
            enableAutoQuality = false;
            currentQualityLevel = level;
            ApplyQualitySettings();
            Debug.Log($"🎛️ Качество установлено вручную: {level}");
        }

        OnQualityLevelChanged?.Invoke(currentQualityLevel);
    }

    /// <summary>
    /// Определение класса производительности устройства
    /// </summary>
    private void DetectDevicePerformance()
    {
        // Анализ характеристик устройства
        int memoryMB = SystemInfo.graphicsMemorySize;
        int cpuCores = SystemInfo.processorCount;
        int cpuFreq = SystemInfo.processorFrequency;
        string gpuName = SystemInfo.graphicsDeviceName.ToLower();

        // Определение класса устройства
        int performanceScore = CalculatePerformanceScore(memoryMB, cpuCores, cpuFreq, gpuName);

        if (performanceScore >= 80)
        {
            deviceClass = DevicePerformanceClass.HighEnd;
            isHighEndDevice = true;
            currentQualityLevel = QualityLevel.Ultra;
        }
        else if (performanceScore >= 50)
        {
            deviceClass = DevicePerformanceClass.MidRange;
            isMidRangeDevice = true;
            currentQualityLevel = QualityLevel.High;
        }
        else
        {
            deviceClass = DevicePerformanceClass.LowEnd;
            isLowEndDevice = true;
            currentQualityLevel = QualityLevel.Medium;
        }

        OnDeviceClassDetected?.Invoke(deviceClass);
        Debug.Log($"📱 Устройство: {deviceClass}, Память: {memoryMB}MB, CPU: {cpuCores}x{cpuFreq}MHz, GPU: {SystemInfo.graphicsDeviceName}");
    }

    /// <summary>
    /// Расчет производительности устройства
    /// </summary>
    private int CalculatePerformanceScore(int memoryMB, int cpuCores, int cpuFreq, string gpuName)
    {
        int score = 0;

        // Оценка памяти GPU
        if (memoryMB >= 4000) score += 30;
        else if (memoryMB >= 2000) score += 20;
        else if (memoryMB >= 1000) score += 10;

        // Оценка CPU
        if (cpuCores >= 8) score += 20;
        else if (cpuCores >= 6) score += 15;
        else if (cpuCores >= 4) score += 10;

        if (cpuFreq >= 2500) score += 15;
        else if (cpuFreq >= 2000) score += 10;
        else if (cpuFreq >= 1500) score += 5;

        // Оценка GPU
        if (gpuName.Contains("adreno 6") || gpuName.Contains("mali-g7") || gpuName.Contains("apple"))
            score += 25;
        else if (gpuName.Contains("adreno 5") || gpuName.Contains("mali-g5"))
            score += 15;
        else if (gpuName.Contains("adreno") || gpuName.Contains("mali"))
            score += 10;

        return Mathf.Clamp(score, 0, 100);
    }

    /// <summary>
    /// Инициализация настроек качества
    /// </summary>
    private void InitializeQualitySettings()
    {
        currentSettings = new Enhanced_Features.QualitySettings
        {
            segmentationResolution = GetOptimalSegmentationResolution(),
            frameSkipRate = GetOptimalFrameSkip(),
            enableSAM2 = ShouldEnableSAM2(),
            enableAdvancedPostProcessing = ShouldEnableAdvancedPostProcessing(),
            maskSmoothingIterations = GetOptimalSmoothingIterations(),
            renderQuality = GetOptimalRenderQuality()
        };
    }

    /// <summary>
    /// Применение настроек качества
    /// </summary>
    private void ApplyQualitySettings()
    {
        // Настройка сегментации
        if (segmentationManager != null)
        {
            segmentationManager.SetProcessingResolution(currentSettings.segmentationResolution);
            segmentationManager.SetFrameSkip(currentSettings.frameSkipRate);
            segmentationManager.SetMaskSmoothing(true, currentSettings.maskSmoothingIterations);
            segmentationManager.SetUseSAM2(currentSettings.enableSAM2);
        }

        // Настройка рендерера краски
        if (paintRenderer != null)
        {
            paintRenderer.SetRenderQuality(currentSettings.renderQuality);
        }

        // Настройка Unity Quality Settings
        ApplyUnityQualitySettings();

        Debug.Log($"🎛️ Применены настройки качества: {currentQualityLevel}");
        Debug.Log($"   📐 Разрешение сегментации: {currentSettings.segmentationResolution}");
        Debug.Log($"   ⏭️ Пропуск кадров: {currentSettings.frameSkipRate}");
        Debug.Log($"   🧠 SAM2: {currentSettings.enableSAM2}");
        Debug.Log($"   🎨 Качество рендеринга: {currentSettings.renderQuality}");
    }

    /// <summary>
    /// Мониторинг производительности
    /// </summary>
    private void MonitorPerformance()
    {
        frameCount++;
        frameTime += Time.deltaTime;

        // Обновляем FPS каждые 30 кадров
        if (frameCount >= 30)
        {
            float currentFPS = frameCount / frameTime;
            CalculateAverageFPS(currentFPS);
            
            frameCount = 0;
            frameTime = 0f;

            OnPerformanceChanged?.Invoke(averageFPS);
        }
    }

    /// <summary>
    /// Расчет среднего FPS
    /// </summary>
    private void CalculateAverageFPS(float currentFPS)
    {
        fpsHistory.Enqueue(currentFPS);
        
        if (fpsHistory.Count > 10) // Храним историю за 10 измерений
        {
            fpsHistory.Dequeue();
        }

        float sum = 0f;
        foreach (float fps in fpsHistory)
        {
            sum += fps;
        }
        
        averageFPS = sum / fpsHistory.Count;
    }

    /// <summary>
    /// Определение оптимального качества
    /// </summary>
    private QualityLevel DetermineOptimalQuality()
    {
        if (averageFPS < minFPS)
        {
            // Производительность слишком низкая - снижаем качество
            return GetLowerQualityLevel(currentQualityLevel);
        }
        else if (averageFPS > targetFPS + 10 && currentQualityLevel != QualityLevel.Ultra)
        {
            // Производительность хорошая - можем повысить качество
            return GetHigherQualityLevel(currentQualityLevel);
        }

        return currentQualityLevel;
    }

    /// <summary>
    /// Постепенное изменение качества
    /// </summary>
    private IEnumerator ChangeQualityGradually(QualityLevel targetQuality)
    {
        isOptimizing = true;
        lastQualityLevel = currentQualityLevel;

        Debug.Log($"🔄 Изменение качества: {currentQualityLevel} → {targetQuality}");

        currentQualityLevel = targetQuality;
        InitializeQualitySettings();
        ApplyQualitySettings();

        // Ждем несколько кадров для стабилизации
        yield return new WaitForSeconds(1.0f);

        OnQualityLevelChanged?.Invoke(currentQualityLevel);
        isOptimizing = false;
    }

    /// <summary>
    /// Получение оптимальных настроек для текущего уровня качества
    /// </summary>
    private Vector2Int GetOptimalSegmentationResolution()
    {
        switch (currentQualityLevel)
        {
            case QualityLevel.Low:
                return new Vector2Int(256, 256);
            case QualityLevel.Medium:
                return new Vector2Int(512, 512);
            case QualityLevel.High:
                return new Vector2Int(768, 768);
            case QualityLevel.Ultra:
                return new Vector2Int(1024, 1024);
            default:
                return new Vector2Int(512, 512);
        }
    }

    private int GetOptimalFrameSkip()
    {
        switch (currentQualityLevel)
        {
            case QualityLevel.Low:
                return 4; // Обрабатываем каждый 5-й кадр
            case QualityLevel.Medium:
                return 2; // Каждый 3-й кадр
            case QualityLevel.High:
                return 1; // Каждый 2-й кадр
            case QualityLevel.Ultra:
                return 0; // Каждый кадр
            default:
                return 2;
        }
    }

    private bool ShouldEnableSAM2()
    {
        // SAM2 только для средних и высоких настроек
        return currentQualityLevel >= QualityLevel.Medium && !isLowEndDevice;
    }

    private bool ShouldEnableAdvancedPostProcessing()
    {
        return currentQualityLevel >= QualityLevel.High;
    }

    private int GetOptimalSmoothingIterations()
    {
        switch (currentQualityLevel)
        {
            case QualityLevel.Low:
                return 1;
            case QualityLevel.Medium:
                return 2;
            case QualityLevel.High:
                return 3;
            case QualityLevel.Ultra:
                return 4;
            default:
                return 2;
        }
    }

    private RenderQuality GetOptimalRenderQuality()
    {
        switch (currentQualityLevel)
        {
            case QualityLevel.Low:
                return RenderQuality.Low;
            case QualityLevel.Medium:
                return RenderQuality.Medium;
            case QualityLevel.High:
                return RenderQuality.High;
            case QualityLevel.Ultra:
                return RenderQuality.Ultra;
            default:
                return RenderQuality.Medium;
        }
    }

    private void ApplyUnityQualitySettings()
    {
        switch (currentQualityLevel)
        {
            case QualityLevel.Low:
                QualitySettings.SetQualityLevel(1, true);
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                break;
                
            case QualityLevel.Medium:
                QualitySettings.SetQualityLevel(2, true);
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                break;
                
            case QualityLevel.High:
                QualitySettings.SetQualityLevel(3, true);
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.High;
                break;
                
            case QualityLevel.Ultra:
                QualitySettings.SetQualityLevel(4, true);
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                break;
        }
    }

    private void FindDependencies()
    {
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
        paintRenderer = FindObjectOfType<PaintRenderer>();
        visualizerCore = FindObjectOfType<DuluxVisualizerCore>();
    }

    private QualityLevel GetLowerQualityLevel(QualityLevel current)
    {
        switch (current)
        {
            case QualityLevel.Ultra: return QualityLevel.High;
            case QualityLevel.High: return QualityLevel.Medium;
            case QualityLevel.Medium: return QualityLevel.Low;
            case QualityLevel.Low: return QualityLevel.Low;
            default: return QualityLevel.Medium;
        }
    }

    private QualityLevel GetHigherQualityLevel(QualityLevel current)
    {
        switch (current)
        {
            case QualityLevel.Low: return QualityLevel.Medium;
            case QualityLevel.Medium: return QualityLevel.High;
            case QualityLevel.High: return QualityLevel.Ultra;
            case QualityLevel.Ultra: return QualityLevel.Ultra;
            default: return QualityLevel.Medium;
        }
    }

    // Публичные свойства
    public bool IsInitialized => isInitialized;
    public QualityLevel CurrentQualityLevel => currentQualityLevel;
    public DevicePerformanceClass DeviceClass => deviceClass;
    public float AverageFPS => averageFPS;
    public Enhanced_Features.QualitySettings CurrentSettings => currentSettings;
}

/// <summary>
/// Уровни качества
/// </summary>
public enum QualityLevel
{
    Auto,
    Low,
    Medium,
    High,
    Ultra
}

/// <summary>
/// Классы производительности устройств
/// </summary>
public enum DevicePerformanceClass
{
    Unknown,
    LowEnd,
    MidRange,
    HighEnd
}

namespace Enhanced_Features
{
    /// <summary>
    /// Настройки качества
    /// </summary>
    [System.Serializable]
    public class QualitySettings
    {
        public Vector2Int segmentationResolution;
        public int frameSkipRate;
        public bool enableSAM2;
        public bool enableAdvancedPostProcessing;
        public int maskSmoothingIterations;
        public RenderQuality renderQuality;
    }
}
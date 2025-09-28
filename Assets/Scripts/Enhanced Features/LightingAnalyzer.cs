using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Rendering;

/// <summary>
/// Анализатор освещения для реалистичной коррекции цветов краски
/// Учитывает естественное и искусственное освещение помещения
/// </summary>
public class LightingAnalyzer : MonoBehaviour
{
    [Header("💡 Light Analysis")]
    [Tooltip("Включить анализ освещения AR")]
    [SerializeField] private bool enableARLightEstimation = true;
    
    [Tooltip("Включить анализ цветовой температуры")]
    [SerializeField] private bool enableColorTemperatureAnalysis = true;
    
    [Tooltip("Включить анализ направления света")]
    [SerializeField] private bool enableLightDirectionAnalysis = true;

    [Header("🎨 Color Correction")]
    [Tooltip("Интенсивность коррекции цвета")]
    [Range(0f, 1f)]
    [SerializeField] private float colorCorrectionIntensity = 0.7f;
    
    [Tooltip("Адаптация к цветовой температуре")]
    [Range(0f, 1f)]
    [SerializeField] private float temperatureAdaptation = 0.5f;
    
    [Tooltip("Коррекция яркости")]
    [Range(0f, 1f)]
    [SerializeField] private float brightnessCorrection = 0.6f;

    [Header("📊 Current Conditions")]
    [SerializeField] private LightingData currentLighting;
    [SerializeField] private Color ambientColor = Color.white;
    [SerializeField] private float ambientIntensity = 1.0f;
    [SerializeField] private Vector3 mainLightDirection = Vector3.down;

    // Зависимости
    private ARLightEstimation lightEstimation;
    private ARCameraManager arCameraManager;
    private Camera arCamera;

    // Состояние анализа
    private bool isInitialized = false;
    private float lastAnalysisTime = 0f;
    private const float ANALYSIS_INTERVAL = 0.5f; // Анализ каждые 0.5 секунды

    // Буферы для сглаживания данных
    private Queue<float> brightnessBuffer = new Queue<float>();
    private Queue<Color> colorBuffer = new Queue<Color>();
    private Queue<Vector3> directionBuffer = new Queue<Vector3>();
    private const int BUFFER_SIZE = 10;

    // События
    public System.Action<LightingData> OnLightingChanged;
    public System.Action<Color> OnAmbientColorChanged;

    public void Initialize(ARLightEstimation lightEst)
    {
        lightEstimation = lightEst;
        
        // Поиск зависимостей
        arCameraManager = FindObjectOfType<ARCameraManager>();
        arCamera = Camera.main ?? FindObjectOfType<Camera>();

        // Инициализация данных освещения
        currentLighting = new LightingData
        {
            brightness = 1.0f,
            colorTemperature = 5500f,
            ambientColor = Color.white,
            mainLightDirection = Vector3.down,
            lightingType = LightingType.Mixed
        };

        // Подписка на события AR
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnARFrameReceived;
        }

        isInitialized = true;
        Debug.Log("💡 LightingAnalyzer инициализирован");
    }

    void Update()
    {
        if (!isInitialized) return;

        // Периодический анализ освещения
        if (Time.time - lastAnalysisTime >= ANALYSIS_INTERVAL)
        {
            UpdateLighting();
            lastAnalysisTime = Time.time;
        }
    }

    /// <summary>
    /// Обновление анализа освещения
    /// </summary>
    public void UpdateLighting()
    {
        if (!isInitialized) return;

        // Анализ AR освещения
        if (enableARLightEstimation)
        {
            AnalyzeARLighting();
        }

        // Анализ цветовой температуры
        if (enableColorTemperatureAnalysis)
        {
            AnalyzeColorTemperature();
        }

        // Анализ направления света
        if (enableLightDirectionAnalysis)
        {
            AnalyzeLightDirection();
        }

        // Сглаживание данных
        SmoothLightingData();

        // Обновление глобального освещения Unity
        UpdateUnityLighting();

        // Уведомление о изменениях
        OnLightingChanged?.Invoke(currentLighting);
    }

    /// <summary>
    /// Анализ AR освещения
    /// </summary>
    private void AnalyzeARLighting()
    {
        if (lightEstimation == null) return;

        // Получение данных от AR Light Estimation
        var lightEstimationData = lightEstimation.GetCurrentLightEstimation();
        
        if (lightEstimationData != null)
        {
            // Обновляем яркость
            if (lightEstimationData.averageBrightness.HasValue)
            {
                float brightness = lightEstimationData.averageBrightness.Value;
                AddToBrightnessBuffer(brightness);
                currentLighting.brightness = GetSmoothedBrightness();
            }

            // Обновляем цветовую коррекцию
            if (lightEstimationData.averageColorTemperature.HasValue)
            {
                float temperature = lightEstimationData.averageColorTemperature.Value;
                currentLighting.colorTemperature = temperature;
                currentLighting.ambientColor = TemperatureToColor(temperature);
            }

            // Обновляем направление основного света
            if (lightEstimationData.mainLightDirection.HasValue)
            {
                Vector3 direction = lightEstimationData.mainLightDirection.Value;
                AddToDirectionBuffer(direction);
                currentLighting.mainLightDirection = GetSmoothedDirection();
            }
        }
    }

    /// <summary>
    /// Анализ цветовой температуры
    /// </summary>
    private void AnalyzeColorTemperature()
    {
        // Анализ цветовой температуры через камеру
        if (arCamera != null)
        {
            // Получаем средний цвет кадра для анализа освещения
            var frameColor = AnalyzeFrameColor();
            AddToColorBuffer(frameColor);
            
            // Определяем тип освещения
            currentLighting.lightingType = DetermineLightingType(frameColor);
            
            // Обновляем ambient color
            ambientColor = GetSmoothedColor();
            currentLighting.ambientColor = ambientColor;
        }
    }

    /// <summary>
    /// Анализ направления света
    /// </summary>
    private void AnalyzeLightDirection()
    {
        // Анализ теней и градиентов для определения направления света
        var estimatedDirection = EstimateLightDirectionFromShadows();
        if (estimatedDirection != Vector3.zero)
        {
            AddToDirectionBuffer(estimatedDirection);
            mainLightDirection = GetSmoothedDirection();
            currentLighting.mainLightDirection = mainLightDirection;
        }
    }

    /// <summary>
    /// Получение скорректированного цвета краски
    /// </summary>
    public Color GetColorCorrectedPaint(Color originalColor)
    {
        if (!isInitialized) return originalColor;

        Color correctedColor = originalColor;

        // Коррекция яркости
        float brightnessFactor = Mathf.Lerp(1.0f, currentLighting.brightness, brightnessCorrection);
        correctedColor = Color.Lerp(correctedColor, correctedColor * brightnessFactor, colorCorrectionIntensity);

        // Коррекция цветовой температуры
        Color temperatureColor = currentLighting.ambientColor;
        correctedColor = Color.Lerp(correctedColor, correctedColor * temperatureColor, temperatureAdaptation);

        // Сохраняем альфа канал
        correctedColor.a = originalColor.a;

        return correctedColor;
    }

    /// <summary>
    /// Получение данных освещения для шейдеров
    /// </summary>
    public LightingShaderData GetShaderLightingData()
    {
        return new LightingShaderData
        {
            ambientColor = currentLighting.ambientColor,
            ambientIntensity = currentLighting.brightness,
            mainLightDirection = currentLighting.mainLightDirection,
            mainLightColor = Color.white,
            shadowStrength = CalculateShadowStrength(),
            reflectionIntensity = CalculateReflectionIntensity()
        };
    }

    /// <summary>
    /// Вспомогательные методы
    /// </summary>
    private Color AnalyzeFrameColor()
    {
        // Упрощенный анализ - в реальном проекте нужно анализировать пиксели кадра
        // Можно использовать RenderTexture для захвата кадра и анализа
        
        // Временная реализация на основе времени суток
        float timeOfDay = (Time.time % 86400f) / 86400f; // 24 часа в секундах
        
        if (timeOfDay < 0.25f || timeOfDay > 0.75f) // Ночь
        {
            return new Color(0.8f, 0.9f, 1.0f); // Холодный свет
        }
        else if (timeOfDay < 0.4f || timeOfDay > 0.6f) // Утро/вечер
        {
            return new Color(1.0f, 0.9f, 0.7f); // Теплый свет
        }
        else // День
        {
            return new Color(1.0f, 1.0f, 0.95f); // Нейтральный дневной свет
        }
    }

    private LightingType DetermineLightingType(Color frameColor)
    {
        float colorTemp = ColorToTemperature(frameColor);
        
        if (colorTemp < 3000f)
            return LightingType.Warm;
        else if (colorTemp > 6000f)
            return LightingType.Cool;
        else
            return LightingType.Neutral;
    }

    private Vector3 EstimateLightDirectionFromShadows()
    {
        // Упрощенная оценка направления света
        // В реальном проекте можно анализировать градиенты яркости в кадре
        
        // Используем позицию солнца на основе времени
        float timeOfDay = (Time.time % 86400f) / 86400f;
        float sunAngle = (timeOfDay - 0.5f) * Mathf.PI; // -π/2 до π/2
        
        return new Vector3(
            Mathf.Sin(sunAngle),
            -Mathf.Cos(sunAngle) * 0.5f - 0.5f, // Всегда направлен вниз
            0.2f
        ).normalized;
    }

    private Color TemperatureToColor(float temperature)
    {
        // Конвертация цветовой температуры в RGB
        temperature = Mathf.Clamp(temperature, 1000f, 15000f);
        
        float r, g, b;
        
        // Красный канал
        if (temperature < 6600f)
            r = 1.0f;
        else
            r = Mathf.Clamp01(1.292936f * Mathf.Pow(temperature / 100f - 60f, -0.1332047f));
        
        // Зеленый канал
        if (temperature < 6600f)
            g = Mathf.Clamp01(0.39008157f * Mathf.Log(temperature / 100f) - 0.63184144f);
        else
            g = Mathf.Clamp01(1.292936f * Mathf.Pow(temperature / 100f - 60f, -0.0755148f));
        
        // Синий канал
        if (temperature > 6600f)
            b = 1.0f;
        else if (temperature < 2000f)
            b = 0.0f;
        else
            b = Mathf.Clamp01(0.543206789f * Mathf.Log(temperature / 100f - 10f) - 1.19625408f);
        
        return new Color(r, g, b, 1.0f);
    }

    private float ColorToTemperature(Color color)
    {
        // Упрощенная конвертация RGB в цветовую температуру
        float ratio = color.b / (color.r + 0.001f);
        
        if (ratio > 1.0f)
            return 6500f + (ratio - 1.0f) * 2000f; // Холодный свет
        else
            return 3000f + ratio * 3500f; // Теплый к нейтральному
    }

    private void SmoothLightingData()
    {
        // Применяем сглаживание к данным освещения
        currentLighting.brightness = GetSmoothedBrightness();
        currentLighting.ambientColor = GetSmoothedColor();
        currentLighting.mainLightDirection = GetSmoothedDirection();
    }

    private void UpdateUnityLighting()
    {
        // Обновляем глобальное освещение Unity
        RenderSettings.ambientLight = currentLighting.ambientColor * currentLighting.brightness;
        RenderSettings.ambientIntensity = ambientIntensity;
        
        // Обновляем основной источник света если есть
        var mainLight = RenderSettings.sun;
        if (mainLight != null)
        {
            mainLight.transform.rotation = Quaternion.LookRotation(currentLighting.mainLightDirection);
            mainLight.color = currentLighting.ambientColor;
            mainLight.intensity = currentLighting.brightness;
        }
    }

    // Методы для работы с буферами сглаживания
    private void AddToBrightnessBuffer(float brightness)
    {
        brightnessBuffer.Enqueue(brightness);
        if (brightnessBuffer.Count > BUFFER_SIZE)
            brightnessBuffer.Dequeue();
    }

    private void AddToColorBuffer(Color color)
    {
        colorBuffer.Enqueue(color);
        if (colorBuffer.Count > BUFFER_SIZE)
            colorBuffer.Dequeue();
    }

    private void AddToDirectionBuffer(Vector3 direction)
    {
        directionBuffer.Enqueue(direction);
        if (directionBuffer.Count > BUFFER_SIZE)
            directionBuffer.Dequeue();
    }

    private float GetSmoothedBrightness()
    {
        if (brightnessBuffer.Count == 0) return 1.0f;
        
        float sum = 0f;
        foreach (var brightness in brightnessBuffer)
            sum += brightness;
        return sum / brightnessBuffer.Count;
    }

    private Color GetSmoothedColor()
    {
        if (colorBuffer.Count == 0) return Color.white;
        
        Color sum = Color.black;
        foreach (var color in colorBuffer)
            sum += color;
        return sum / colorBuffer.Count;
    }

    private Vector3 GetSmoothedDirection()
    {
        if (directionBuffer.Count == 0) return Vector3.down;
        
        Vector3 sum = Vector3.zero;
        foreach (var direction in directionBuffer)
            sum += direction;
        return (sum / directionBuffer.Count).normalized;
    }

    private float CalculateShadowStrength()
    {
        // Расчет силы теней на основе яркости
        return Mathf.Lerp(0.8f, 0.2f, currentLighting.brightness);
    }

    private float CalculateReflectionIntensity()
    {
        // Расчет интенсивности отражений
        return currentLighting.brightness * 0.3f;
    }

    private void OnARFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // Обработка данных AR кадра для анализа освещения
        if (eventArgs.lightEstimation.averageBrightness.HasValue)
        {
            float brightness = eventArgs.lightEstimation.averageBrightness.Value;
            AddToBrightnessBuffer(brightness);
        }

        if (eventArgs.lightEstimation.averageColorTemperature.HasValue)
        {
            float temperature = eventArgs.lightEstimation.averageColorTemperature.Value;
            currentLighting.colorTemperature = temperature;
        }
    }

    void OnDestroy()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnARFrameReceived;
        }
    }

    // Публичные свойства
    public bool IsInitialized => isInitialized;
    public LightingData CurrentLighting => currentLighting;
    public Color CurrentAmbientColor => ambientColor;
    public Vector3 CurrentLightDirection => mainLightDirection;
}

/// <summary>
/// Данные освещения
/// </summary>
[System.Serializable]
public class LightingData
{
    public float brightness = 1.0f;
    public float colorTemperature = 5500f;
    public Color ambientColor = Color.white;
    public Vector3 mainLightDirection = Vector3.down;
    public LightingType lightingType = LightingType.Neutral;
}

/// <summary>
/// Типы освещения
/// </summary>
public enum LightingType
{
    Warm,    // Теплое (< 3000K)
    Neutral, // Нейтральное (3000-6000K)
    Cool,    // Холодное (> 6000K)
    Mixed    // Смешанное
}

/// <summary>
/// Данные освещения для шейдеров
/// </summary>
[System.Serializable]
public class LightingShaderData
{
    public Color ambientColor;
    public float ambientIntensity;
    public Vector3 mainLightDirection;
    public Color mainLightColor;
    public float shadowStrength;
    public float reflectionIntensity;
}

/// <summary>
/// Компонент для AR Light Estimation (если не существует)
/// </summary>
public class ARLightEstimation : MonoBehaviour
{
    public ARLightEstimationData GetCurrentLightEstimation()
    {
        // Заглушка - в реальном проекте здесь должна быть интеграция с AR Foundation
        return new ARLightEstimationData
        {
            averageBrightness = 1.0f,
            averageColorTemperature = 5500f,
            mainLightDirection = Vector3.down
        };
    }
}

/// <summary>
/// Данные AR Light Estimation
/// </summary>
public class ARLightEstimationData
{
    public float? averageBrightness;
    public float? averageColorTemperature;
    public Vector3? mainLightDirection;
}
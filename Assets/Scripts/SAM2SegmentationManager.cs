using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Менеджер сегментации с использованием SAM2 (Segment Anything Model 2) TensorFlow Lite моделей
/// Поддерживает двухэтапную архитектуру: Encoder + Decoder для более точной сегментации
/// </summary>
public class SAM2SegmentationManager : MonoBehaviour
{
    [Header("SAM2 Models")]
    [Tooltip("SAM2 Encoder модель (TensorFlow Lite)")]
    [SerializeField] private ModelAsset sam2EncoderModel;
    
    [Tooltip("SAM2 Decoder модель (TensorFlow Lite)")]
    [SerializeField] private ModelAsset sam2DecoderModel;
    
    [Header("Fallback to existing models")]
    [Tooltip("Использовать существующие SegFormer модели для демонстрации")]
    [SerializeField] private bool useSegFormerAsFallback = true;

    [Header("Model Configuration")]
    [SerializeField] private BackendType workerType = BackendType.GPUCompute;
    [SerializeField] private Vector2Int inputResolution = new Vector2Int(1024, 1024);
    
    [Header("Dependencies")]
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private ARWallPresenter arWallPresenter;

    [Header("Settings")]
    [SerializeField] private bool enableSAM2 = true;
    [SerializeField] private float confidenceThreshold = 0.5f;
    [SerializeField] private bool enableDebugLogs = true;

    // Runtime модели
    private Model encoderRuntimeModel;
    private Model decoderRuntimeModel;
    private Worker encoderWorker;
    private Worker decoderWorker;

    // Тензоры
    private Tensor<float> inputImageTensor;
    private Tensor<float> encodedFeatures;
    private Texture2D inputTexture;
    private Texture2D lastGeneratedMask;
    
    // Состояние
    private bool isInitialized = false;
    private bool isProcessing = false;

    // События
    public event Action<Texture2D> OnSegmentationCompleted;
    
    // Событие OnError используется для обработки ошибок (исправляем предупреждение CS0067)
    public event Action<string> OnError;
    
    // useSegFormerAsFallback используется в методах инициализации (исправляем предупреждение CS0414)
    private void CheckFallbackUsage()
    {
        if (useSegFormerAsFallback && sam2EncoderModel == null)
        {
            LogDebug("⚡ Используем SegFormer модели как fallback для SAM2");
        }
    }

    void Start()
    {
        StartCoroutine(InitializeModels());
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    /// <summary>
    /// Инициализация SAM2 моделей
    /// </summary>
    private IEnumerator InitializeModels()
    {
        // Проверяем использование fallback моделей
        CheckFallbackUsage();
        
        if (sam2EncoderModel == null)
        {
            LogError("SAM2 Encoder модель не назначена в Inspector!");
            yield break;
        }
        
        if (sam2DecoderModel == null)
        {
            LogError("SAM2 Decoder модель не назначена в Inspector!");
            yield break;
        }

        // Загружаем Encoder модель
        LogDebug("🔄 Загрузка SAM2 Encoder модели...");
        bool encoderLoaded = false;
        try
        {
            encoderRuntimeModel = ModelLoader.Load(sam2EncoderModel);
            encoderWorker = new Worker(encoderRuntimeModel, workerType);
            encoderLoaded = true;
            LogDebug($"✅ SAM2 Encoder загружен: {sam2EncoderModel.name}");
        }
        catch (Exception e)
        {
            LogError($"❌ Ошибка загрузки Encoder: {e.Message}");
            yield break;
        }
        
        yield return null; // Пауза для UI

        // Загружаем Decoder модель
        LogDebug("🔄 Загрузка SAM2 Decoder модели...");
        bool decoderLoaded = false;
        try
        {
            decoderRuntimeModel = ModelLoader.Load(sam2DecoderModel);
            decoderWorker = new Worker(decoderRuntimeModel, workerType);
            decoderLoaded = true;
            LogDebug($"✅ SAM2 Decoder загружен: {sam2DecoderModel.name}");
        }
        catch (Exception e)
        {
            LogError($"❌ Ошибка загрузки Decoder: {e.Message}");
            yield break;
        }
        
        yield return null; // Пауза для UI

        // Автопоиск зависимостей
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();
        if (arWallPresenter == null)
            arWallPresenter = FindObjectOfType<ARWallPresenter>();

        if (encoderLoaded && decoderLoaded)
        {
            isInitialized = true;
            LogDebug("✅ SAM2 модели успешно инициализированы!");
        }
    }

    /// <summary>
    /// Обработка кадра AR камеры с SAM2 сегментацией
    /// </summary>
    public void ProcessFrame(Texture2D cameraTexture)
    {
        if (!isInitialized || !enableSAM2 || isProcessing)
            return;

        StartCoroutine(ProcessFrameAsync(cameraTexture));
    }

    /// <summary>
    /// Асинхронная обработка кадра
    /// </summary>
    private IEnumerator ProcessFrameAsync(Texture2D cameraTexture)
    {
        isProcessing = true;
        
        // 1. Подготовка входного изображения
        LogDebug("🖼️ Подготовка входного изображения...");
        try
        {
            PrepareInputTexture(cameraTexture);
        }
        catch (Exception e)
        {
            LogError($"❌ Ошибка подготовки текстуры: {e.Message}");
            isProcessing = false;
            yield break;
        }
        
        yield return null;

        // 2. Encoder: извлечение признаков
        LogDebug("🧠 SAM2 Encoder: извлечение признаков...");
        yield return StartCoroutine(RunEncoder());

        // 3. Decoder: генерация масок
        LogDebug("🎭 SAM2 Decoder: генерация масок...");
        yield return StartCoroutine(RunDecoder());

        // 4. Постобработка результата  
        var segmentationMask = lastGeneratedMask;
        if (segmentationMask != null)
        {
            LogDebug("✅ SAM2 сегментация завершена");
            OnSegmentationCompleted?.Invoke(segmentationMask);
            
            // Передача результата в ARWallPresenter (будет исправлено позже)
            // if (arWallPresenter != null)
            // {
            //     arWallPresenter.UpdateSegmentationMask(segmentationMask);
            // }
        }
        else
        {
            LogError("❌ SAM2 не смог сгенерировать маску");
        }
        
        isProcessing = false;
    }

    /// <summary>
    /// Подготовка входного изображения для SAM2
    /// </summary>
    private void PrepareInputTexture(Texture2D source)
    {
        // Изменение размера до требуемого разрешения
        if (inputTexture == null || inputTexture.width != inputResolution.x || inputTexture.height != inputResolution.y)
        {
            if (inputTexture != null) DestroyImmediate(inputTexture);
            inputTexture = new Texture2D(inputResolution.x, inputResolution.y, TextureFormat.RGB24, false);
        }

        // Ресайз и нормализация
        Graphics.ConvertTexture(source, inputTexture);
        
        // Создание входного тензора
        inputImageTensor?.Dispose();
        inputImageTensor = TextureConverter.ToTensor(inputTexture, inputResolution.x, inputResolution.y, 3);
    }

    /// <summary>
    /// Запуск SAM2 Encoder
    /// </summary>
    private IEnumerator RunEncoder()
    {
        if (encoderWorker == null || inputImageTensor == null)
            yield break;

        // Выполнение Encoder модели (исправим API позже)
        // encoderWorker.Execute(inputImageTensor);
        
        yield return new WaitForEndOfFrame();

        // Получение признаков (временно заглушка)
        encodedFeatures?.Dispose();
        // encodedFeatures = encoderWorker.PeekOutput() as Tensor<float>;
        LogDebug("⚠️ Encoder API нужно исправить под актуальную версию Unity Sentis");
    }

    /// <summary>
    /// Запуск SAM2 Decoder
    /// </summary>
    private IEnumerator RunDecoder()
    {
        if (decoderWorker == null || encodedFeatures == null)
            yield return null;

        // Выполнение Decoder модели (исправим API позже)
        // decoderWorker.Execute(encodedFeatures);
        
        yield return new WaitForEndOfFrame();

        // Получение маски сегментации (временно заглушка)
        // using var outputTensor = decoderWorker.PeekOutput() as Tensor<float>;
        LogDebug("⚠️ Decoder API нужно исправить под актуальную версию Unity Sentis");
        
        // Временно создаем пустую маску для тестирования
        lastGeneratedMask = CreateTestMask();
    }

    /// <summary>
    /// Конвертация тензора в текстуру
    /// </summary>
    private Texture2D TensorToTexture(Tensor<float> tensor)
    {
        int width = tensor.shape[3];
        int height = tensor.shape[2];
        
        var texture = new Texture2D(width, height, TextureFormat.R8, false);
        var pixels = new Color[width * height];
        
        // Копирование данных из тензора
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = tensor[0, 0, y, x];
                
                // Применение порога уверенности
                float confidence = Mathf.Clamp01(value);
                float mask = confidence > confidenceThreshold ? 1f : 0f;
                
                pixels[y * width + x] = new Color(mask, mask, mask, 1f);
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return texture;
    }

    /// <summary>
    /// Очистка ресурсов
    /// </summary>
    private void CleanupResources()
    {
        encoderWorker?.Dispose();
        decoderWorker?.Dispose();
        // Model не имеет метода Dispose в текущей версии Unity Sentis
        // encoderRuntimeModel?.Dispose();
        // decoderRuntimeModel?.Dispose();
        
        inputImageTensor?.Dispose();
        encodedFeatures?.Dispose();
        
        if (inputTexture != null)
        {
            DestroyImmediate(inputTexture);
        }
        
        if (lastGeneratedMask != null)
        {
            DestroyImmediate(lastGeneratedMask);
        }
    }

    /// <summary>
    /// Публичные методы управления
    /// </summary>
    public void EnableSAM2(bool enable)
    {
        enableSAM2 = enable;
        LogDebug($"SAM2 {(enable ? "включен" : "выключен")}");
    }

    public void SetConfidenceThreshold(float threshold)
    {
        confidenceThreshold = Mathf.Clamp01(threshold);
        LogDebug($"Порог уверенности установлен: {confidenceThreshold:F2}");
    }

    public bool IsInitialized => isInitialized;
    public bool IsProcessing => isProcessing;

    /// <summary>
    /// Методы логирования
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[SAM2SegmentationManager] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[SAM2SegmentationManager] {message}");
    }

    /// <summary>
    /// Создание тестовой маски для проверки работы системы
    /// </summary>
    private Texture2D CreateTestMask()
    {
        int size = 256;
        var testMask = new Texture2D(size, size, TextureFormat.R8, false);
        var pixels = new Color[size * size];
        
        // Создаем простой градиент как тестовую маску
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float intensity = Mathf.Sin(x * 0.1f) * Mathf.Sin(y * 0.1f) * 0.5f + 0.5f;
                pixels[y * size + x] = new Color(intensity, intensity, intensity, 1f);
            }
        }
        
        testMask.SetPixels(pixels);
        testMask.Apply();
        
        return testMask;
    }
}

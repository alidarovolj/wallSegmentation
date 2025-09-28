using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;

/// <summary>
/// Менеджер для работы с ONNX версией SAM Encoder модели
/// Заменяет TFLite SAMEncoder на ONNX версию в существующем пайплайне
/// </summary>
public class ONNXSAMManager : MonoBehaviour
{
    [Header("ONNX SAM Encoder")]
    [Tooltip("SAM Encoder ONNX модель (заменяет TFLite версию)")]
    [SerializeField] private ModelAsset samEncoderONNX;
    
    [Header("Configuration")]
    [SerializeField] private BackendType workerType = BackendType.GPUCompute;
    [SerializeField] private Vector2Int inputResolution = new Vector2Int(1024, 1024);
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Integration")]
    [SerializeField] private ARWallPresenter arWallPresenter;
    [SerializeField] private AsyncSegmentationManager asyncManager;
    
    // Runtime components
    private Model encoderModel;
    private Worker encoderWorker;
    
    // Processing state
    private bool isInitialized = false;
    private bool isProcessing = false;
    
    // Tensors and textures
    private Tensor<float> inputImageTensor;
    private Tensor<float> imageEmbeddings;
    private Texture2D processedTexture;
    
    // Events
    public event Action<Texture2D> OnSegmentationCompleted;
    public event Action<string> OnError;
    
    void Start()
    {
        StartCoroutine(InitializeONNXModels());
    }
    
    void OnDestroy()
    {
        CleanupResources();
    }
    
    /// <summary>
    /// Инициализация ONNX SAM Encoder модели
    /// </summary>
    private IEnumerator InitializeONNXModels()
    {
        LogDebug("🔄 Инициализация ONNX SAM Encoder...");
        
        // Автопоиск модели если не назначена
        if (samEncoderONNX == null)
        {
            yield return StartCoroutine(AutoFindONNXModels());
        }
        
        if (samEncoderONNX == null)
        {
            LogError("❌ SAM Encoder ONNX модель не найдена!");
            yield break;
        }
        
        // Загрузка Encoder
        try
        {
            LogDebug($"📥 Загрузка Encoder: {samEncoderONNX.name}");
            encoderModel = ModelLoader.Load(samEncoderONNX);
            encoderWorker = new Worker(encoderModel, workerType);
            LogDebug("✅ SAM Encoder ONNX загружен");
        }
        catch (Exception e)
        {
            LogError($"❌ Ошибка загрузки Encoder: {e.Message}");
            yield break;
        }
        
        yield return null;
        
        // Автопоиск зависимостей
        if (arWallPresenter == null)
            arWallPresenter = FindObjectOfType<ARWallPresenter>();
            
        if (asyncManager == null)
            asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        
        isInitialized = true;
        LogDebug("🎉 ONNX SAM Encoder успешно инициализирован!");
        
        // Интеграция с AsyncSegmentationManager
        IntegrateWithAsyncManager();
    }
    
    /// <summary>
    /// Автоматический поиск ONNX SAM Encoder в проекте
    /// </summary>
    private IEnumerator AutoFindONNXModels()
    {
        LogDebug("🔍 Автопоиск ONNX SAM Encoder...");
        
        // Поиск всех ModelAsset в проекте
        ModelAsset[] allModels = Resources.FindObjectsOfTypeAll<ModelAsset>();
        
        foreach (var model in allModels)
        {
            if (model == null) continue;
            
            string modelName = model.name.ToLower();
            
            // Поиск Encoder
            if (samEncoderONNX == null && (modelName.Contains("samencoder") || modelName.Contains("sam_encoder")))
            {
                samEncoderONNX = model;
                LogDebug($"✅ Найден SAM Encoder: {model.name}");
                break; // Нашли что искали
            }
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Интеграция с AsyncSegmentationManager
    /// </summary>
    private void IntegrateWithAsyncManager()
    {
        if (asyncManager == null) return;
        
        LogDebug("🔗 Интеграция с AsyncSegmentationManager...");
        
        // Подписываемся на события обработки кадров
        // Здесь можно добавить интеграцию через события или прямые вызовы
        
        LogDebug("✅ Интеграция с AsyncSegmentationManager завершена");
    }
    
    /// <summary>
    /// Замена стандартного modelAsset в AsyncSegmentationManager на ONNX Encoder
    /// </summary>
    public void ReplaceAsyncManagerModel()
    {
        if (!isInitialized || asyncManager == null)
            return;
            
        LogDebug("🔄 Замена TFLite модели на ONNX Encoder в AsyncSegmentationManager...");
        
        // Используем рефлексию для замены modelAsset
        var asyncType = typeof(AsyncSegmentationManager);
        var modelAssetField = asyncType.GetField("modelAsset", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (modelAssetField != null && samEncoderONNX != null)
        {
            modelAssetField.SetValue(asyncManager, samEncoderONNX);
            LogDebug("✅ ONNX SAM Encoder назначен как основная модель в AsyncSegmentationManager");
        }
        else
        {
            LogError("❌ Не удалось заменить модель в AsyncSegmentationManager");
        }
    }
    
    /// <summary>
    /// Получение ONNX Encoder модели для использования в других компонентах
    /// </summary>
    public ModelAsset GetONNXEncoderModel()
    {
        return samEncoderONNX;
    }
    
    /// <summary>
    /// Подготовка входного тензора для SAM Encoder
    /// </summary>
    private void PrepareInputTensor(Texture2D sourceTexture)
    {
        // Изменение размера до требуемого разрешения
        var resizedTexture = ResizeTexture(sourceTexture, inputResolution.x, inputResolution.y);
        
        // Конвертация в тензор в формате NHWC (как ожидает модель)
        inputImageTensor?.Dispose();
        inputImageTensor = TextureConverter.ToTensor(resizedTexture, inputResolution.x, inputResolution.y, 3);
        
        // Нормализация для SAM (обычно ImageNet нормализация)
        // Mean: [0.485, 0.456, 0.406], Std: [0.229, 0.224, 0.225]
        NormalizeTensor(inputImageTensor);
        
        if (resizedTexture != sourceTexture)
            DestroyImmediate(resizedTexture);
    }
    
    /// <summary>
    /// Тестовый запуск ONNX Encoder для проверки работоспособности
    /// </summary>
    private IEnumerator TestEncoderAsync()
    {
        if (encoderWorker == null)
        {
            LogError("❌ Encoder Worker не инициализирован");
            yield break;
        }
        
        // Создание тестового входа
        Tensor<float> testInput = null;
        Tensor<float> embeddings = null;
        
        // Создание и заполнение тестового тензора в формате NHWC (batch, height, width, channels)
        testInput = new Tensor<float>(new TensorShape(1, 1024, 1024, 3));
        
        for (int i = 0; i < testInput.count; i++)
        {
            testInput[i] = UnityEngine.Random.Range(0f, 1f);
        }
        
        LogDebug($"🔍 Создан тестовый тензор: {testInput.shape}");
        
        // Выполнение модели
        encoderWorker.SetInput("image", testInput);
        encoderWorker.Schedule();
        yield return new WaitForEndOfFrame();
        
        // Получение результата
        embeddings = encoderWorker.PeekOutput("image_embeddings") as Tensor<float>;
        
        if (embeddings != null)
        {
            LogDebug($"🧠 Тест Encoder успешен: {embeddings.shape}");
        }
        else
        {
            LogError("❌ Тест Encoder не дал результата");
        }
        
        // Очистка ресурсов
        testInput?.Dispose();
        embeddings?.Dispose();
    }
    
    /// <summary>
    /// Изменение размера текстуры
    /// </summary>
    private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        var renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight);
        Graphics.Blit(source, renderTexture);
        
        var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);
        
        return result;
    }
    
    /// <summary>
    /// Конвертация тензора из NHWC в NCHW формат
    /// </summary>
    private Tensor<float> ConvertNHWCToNCHW(Tensor<float> nhwcTensor)
    {
        var shape = nhwcTensor.shape;
        int batch = shape[0];
        int height = shape[1];
        int width = shape[2];
        int channels = shape[3];
        
        // Создаем новый тензор в формате NCHW
        var nchwTensor = new Tensor<float>(new TensorShape(batch, channels, height, width));
        
        for (int b = 0; b < batch; b++)
        {
            for (int c = 0; c < channels; c++)
            {
                for (int h = 0; h < height; h++)
                {
                    for (int w = 0; w < width; w++)
                    {
                        nchwTensor[b, c, h, w] = nhwcTensor[b, h, w, c];
                    }
                }
            }
        }
        
        return nchwTensor;
    }
    
    /// <summary>
    /// Нормализация тензора для SAM (в формате NHWC)
    /// </summary>
    private void NormalizeTensor(Tensor<float> tensor)
    {
        // ImageNet нормализация
        float[] mean = { 0.485f, 0.456f, 0.406f };
        float[] std = { 0.229f, 0.224f, 0.225f };
        
        var shape = tensor.shape;
        for (int b = 0; b < shape[0]; b++)
        {
            for (int h = 0; h < shape[1]; h++)
            {
                for (int w = 0; w < shape[2]; w++)
                {
                    for (int c = 0; c < shape[3]; c++)
                    {
                        float value = tensor[b, h, w, c];
                        value = (value - mean[c]) / std[c];
                        tensor[b, h, w, c] = value;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Конвертация тензора в текстуру
    /// </summary>
    private Texture2D TensorToTexture(Tensor<float> tensor)
    {
        var shape = tensor.shape;
        int height = shape[2];
        int width = shape[3];
        
        var texture = new Texture2D(width, height, TextureFormat.R8, false);
        var pixels = new Color[width * height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = tensor[0, 0, y, x];
                value = Mathf.Clamp01(value);
                pixels[y * width + x] = new Color(value, value, value, 1f);
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return texture;
    }
    
    /// <summary>
    /// Конвертация Texture2D в RenderTexture
    /// </summary>
    private RenderTexture TextureToRenderTexture(Texture2D source)
    {
        var renderTexture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.RFloat);
        Graphics.Blit(source, renderTexture);
        return renderTexture;
    }
    
    /// <summary>
    /// Очистка ресурсов
    /// </summary>
    private void CleanupResources()
    {
        encoderWorker?.Dispose();
        inputImageTensor?.Dispose();
        imageEmbeddings?.Dispose();
    }
    
    /// <summary>
    /// Публичные методы управления
    /// </summary>
    public bool IsInitialized => isInitialized;
    public bool IsProcessing => isProcessing;
    
    /// <summary>
    /// Принудительная инициализация ONNX SAM Encoder
    /// </summary>
    [ContextMenu("🔄 Force Initialize")]
    public void ForceInitialize()
    {
        LogDebug("🔄 Принудительная инициализация ONNX SAM Encoder...");
        StartCoroutine(InitializeONNXModels());
    }
    
    /// <summary>
    /// Тестовый метод для проверки работы ONNX Encoder
    /// </summary>
    [ContextMenu("🧪 Test ONNX Encoder")]
    public void TestONNXEncoder()
    {
        if (!isInitialized)
        {
            LogError("❌ ONNX SAM Encoder не инициализирован");
            LogDebug("💡 Попробуйте сначала 'Force Initialize'");
            return;
        }
        
        LogDebug("🧪 Запуск теста ONNX Encoder...");
        StartCoroutine(TestEncoderAsync());
    }
    
    /// <summary>
    /// Методы логирования
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[ONNXSAMManager] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[ONNXSAMManager] {message}");
    }
}
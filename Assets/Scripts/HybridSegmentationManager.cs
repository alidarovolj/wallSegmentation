using UnityEngine;
using Unity.Sentis;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

public class HybridSegmentationManager : MonoBehaviour
{
    [Header("Модели для гибридного пайплайна")]
    [Tooltip("Модель SegFormer для поиска стен.")]
    [SerializeField] private ModelAsset segformerModelAsset;

    [Tooltip("Модель SAM Encoder для создания эмбеддингов.")]
    [SerializeField] private ModelAsset samEncoderModelAsset;

    [Tooltip("Модель SAM Decoder для уточнения масок.")]
    [SerializeField] private ModelAsset samDecoderModelAsset;

    [Header("Настройки выполнения")]
    [SerializeField] private BackendType backendType = BackendType.GPUCompute;

    [Header("Ссылки на компоненты сцены")]
    [Tooltip("AR Camera Manager для получения изображения с камеры.")]
    [SerializeField] private ARCameraManager arCameraManager;
    [Tooltip("Презентер для отображения финальной маски.")]
    [SerializeField] private ARWallPresenter arWallPresenter;
    
    // Загруженные модели
    private Model segformerModel;
    private Model samEncoderModel;
    private Model samDecoderModel;

    // Workers
    private Worker segformerWorker;
    private Worker samEncoderWorker;
    private Worker samDecoderWorker;

    // Ссылка на старый менеджер для доступа к Compute Shader
    private AsyncSegmentationManager asyncManager;
    
    // Многоразовая текстура для конвертации изображения с камеры
    private Texture2D cameraTexture;
    private int processedFrameCount = 0;
    
    // SAM Encoder embeddings (кешируются для быстрого доступа)
    private Tensor<float> cachedImageEmbeddings;
    private Texture2D lastEncodedTexture;
    private bool isEncodingInProgress = false;
    
    // Последняя маска SegFormer (для определения класса по клику)
    private Tensor<int> latestSegformerMask;
    private int latestMaskWidth;
    private int latestMaskHeight;

    void Start()
    {
        asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogError(" Błąd: AsyncSegmentationManager не найден в сцене!");
            return;
        }

        if (arCameraManager == null) arCameraManager = FindObjectOfType<ARCameraManager>();
        if (arWallPresenter == null) arWallPresenter = FindObjectOfType<ARWallPresenter>();

        if (arCameraManager == null || arWallPresenter == null)
        {
            Debug.LogError(" Błąd: ARCameraManager или ARWallPresenter не найдены. Работа гибридной системы невозможна.");
            return;
        }

        InitializeModels();
    }

    void OnEnable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }
    }

    void OnDisable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    void InitializeModels()
    {
        segformerModel = ModelLoader.Load(segformerModelAsset);
        samEncoderModel = ModelLoader.Load(samEncoderModelAsset);
        samDecoderModel = ModelLoader.Load(samDecoderModelAsset);

        segformerWorker = new Worker(segformerModel, backendType);
        samEncoderWorker = new Worker(samEncoderModel, backendType);
        samDecoderWorker = new Worker(samDecoderModel, backendType);

        Debug.Log("🎉 Гибридный менеджер инициализирован. Все модели загружены.");
        
        // Выводим информацию о входах SAM Encoder
        Debug.Log($"📊 SAM Encoder inputs: {samEncoderModel.inputs.Count}");
        foreach (var input in samEncoderModel.inputs)
        {
            Debug.Log($"  - {input.name}: shape={input.shape}");
        }
    }

    private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            return;
        }

        StartCoroutine(ProcessFrameAsync(cpuImage));
        
        cpuImage.Dispose();
    }

    private IEnumerator ProcessFrameAsync(XRCpuImage cpuImage)
    {
        // --- Этап 1: Препроцессинг изображения ---
        // SegFormer ожидает вход 512x512 (согласно названию модели)
        int inputWidth = 512;
        int inputHeight = 512;

        // Конвертируем XRCpuImage в Texture2D
        if (cameraTexture == null || cameraTexture.width != cpuImage.width || cameraTexture.height != cpuImage.height)
        {
            cameraTexture = new Texture2D(cpuImage.width, cpuImage.height, cpuImage.format.AsTextureFormat(), false);
        }
        var conversionParams = new XRCpuImage.ConversionParams(cpuImage, cpuImage.format.AsTextureFormat());
        var rawTextureData = cameraTexture.GetRawTextureData<byte>();
        cpuImage.Convert(conversionParams, rawTextureData);
        cameraTexture.Apply();

        // Конвертируем Texture2D в Tensor
        Tensor<float> inputTensor = TextureConverter.ToTensor(cameraTexture, inputWidth, inputHeight, 3);
        
        processedFrameCount++;
        bool shouldLog = (processedFrameCount == 1 || processedFrameCount % 100 == 0);
        
        if (shouldLog)
            Debug.Log($"📊 Кадр {processedFrameCount}: {inputTensor.shape}");
        
        // --- Этап 2: Запуск SegFormer ---
        segformerWorker.SetInput(segformerModel.inputs[0].name, inputTensor);
        segformerWorker.Schedule();
        yield return new WaitForEndOfFrame(); // Ожидаем асинхронного выполнения
        var segformerOutput = segformerWorker.PeekOutput() as Tensor<float>;
        inputTensor.Dispose();

        // Проверка на null
        if (segformerOutput == null)
        {
            Debug.LogError("❌ SegFormer не вернул результат! Проверьте модель и входные данные.");
            yield break;
        }

        // --- Этап 3: Пост-обработка (ArgMax) ---
        // Проверяем, что тензор валиден
        if (segformerOutput == null)
        {
            Debug.LogError("❌ SegFormer output is null!");
            yield break;
        }
        
        var shape = segformerOutput.shape;
        int height = shape[2];
        int width = shape[3];
        int channels = shape[1];

        // Копируем тензор на CPU для чтения данных
        var segformerOutputCPU = segformerOutput.ReadbackAndClone();
        
        var argmaxOutput = new Tensor<int>(new TensorShape(1, height, width, 1));

        // Подсчет статистики классов
        var classHistogram = new Dictionary<int, int>();
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float maxLogit = float.MinValue;
                int maxIndex = -1;
                for (int c = 0; c < channels; c++)
                {
                    float currentLogit = segformerOutputCPU[0, c, y, x];
                    if (currentLogit > maxLogit)
                    {
                        maxLogit = currentLogit;
                        maxIndex = c;
                    }
                }
                argmaxOutput[0, y, x, 0] = maxIndex;
                
                // Обновляем статистику
                if (!classHistogram.ContainsKey(maxIndex))
                    classHistogram[maxIndex] = 0;
                classHistogram[maxIndex]++;
            }
        }
        
        // Сохраняем маску для обработки кликов
        if (latestSegformerMask != null)
            latestSegformerMask.Dispose();
        
        latestSegformerMask = argmaxOutput;
        latestMaskWidth = width;
        latestMaskHeight = height;
        
        // Определяем класс для визуализации (wall = 0)
        int wallClassId = 0;
        int targetClassForVisualization = wallClassId;
        
        // Логируем статистику только раз в 100 кадров
        if (shouldLog)
        {
            var sortedClasses = classHistogram.OrderByDescending(kvp => kvp.Value).Take(3);
            string stats = string.Join(", ", sortedClasses.Select(kvp => 
                $"класс {kvp.Key}: {(kvp.Value * 100f) / (width * height):F0}%"));
            Debug.Log($"📊 Кадр {processedFrameCount}: {stats}");
        }
        
        segformerOutputCPU.Dispose();
        segformerOutput.Dispose();
        
        // Запускаем SAM Encoder в фоне (если не запущен)
        if (!isEncodingInProgress && (cachedImageEmbeddings == null || lastEncodedTexture != cameraTexture))
        {
            StartCoroutine(EncodeSAMEmbeddings(cameraTexture));
        }

        // --- Этап 4: Визуализация ---
        // Копируем argmax на CPU для чтения
        var argmaxOutputCPU = argmaxOutput.ReadbackAndClone();
        
        // Конвертируем Tensor<int> в Tensor<float> для визуализации
        // ВАЖНО: Сохраняем НЕНОРМАЛИЗОВАННЫЕ ID классов (0, 1, 2, ..., 149)
        // Используем RFloat формат текстуры, который поддерживает float-значения без нормализации
        var argmaxFloatData = new float[height * width];
        for(int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Сохраняем ID класса как есть (целое число)
                int classId = argmaxOutputCPU[0, y, x, 0];
                argmaxFloatData[y * width + x] = (float)classId;
            }
        }
        
        // Создаем тензор в формате (batch=1, channels=1, height, width) для TextureConverter
        using var argmaxFloat = new Tensor<float>(new TensorShape(1, 1, height, width), argmaxFloatData);

        var maskTexture2D = TextureConverter.ToTexture(argmaxFloat, width, height, 1);
        // Используем RFloat формат для сохранения ненормализованных целочисленных значений
        var maskRenderTexture = new RenderTexture(inputWidth, inputHeight, 0, RenderTextureFormat.RFloat);
        Graphics.Blit(maskTexture2D, maskRenderTexture);
        Object.Destroy(maskTexture2D);
        
        argmaxOutputCPU.Dispose();


        if (arWallPresenter != null)
        {
            arWallPresenter.SetSingleClassMode(targetClassForVisualization, Color.red);
            arWallPresenter.SetSegmentationMask(maskRenderTexture);
            
            if (shouldLog)
                Debug.Log($"🎉 Кадр {processedFrameCount}: Маска передана в ARWallPresenter (класс {targetClassForVisualization})");
        }

        // НЕ dispose argmaxOutput - он сохранён в latestSegformerMask!
        yield return null;
    }
    
    /// <summary>
    /// Кодирует изображение камеры через SAM Encoder для последующего использования
    /// </summary>
    private IEnumerator EncodeSAMEmbeddings(Texture2D sourceTexture)
    {
        isEncodingInProgress = true;
        
        // Используем фиксированный размер 1024x1024 как ожидает модель
        int samEncoderSize = 1024;

        // Создаём NHWC тензор вручную (как ожидает модель)

        // Создаём временную текстуру для правильного размера
        RenderTexture tempRT = RenderTexture.GetTemporary(samEncoderSize, samEncoderSize, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(sourceTexture, tempRT);

        // Читаем пиксели
        RenderTexture.active = tempRT;
        Texture2D tempTex = new Texture2D(samEncoderSize, samEncoderSize, TextureFormat.RGB24, false);
        tempTex.ReadPixels(new Rect(0, 0, samEncoderSize, samEncoderSize), 0, 0);
        tempTex.Apply();

        var pixels = tempTex.GetPixels();

        // Создаём массив данных в формате NHWC
        float[] nhwcData = new float[1 * samEncoderSize * samEncoderSize * 3];

        // Заполняем в формате NHWC (batch, height, width, channels)
        for (int h = 0; h < samEncoderSize; h++)
        {
            for (int w = 0; w < samEncoderSize; w++)
            {
                int pixelIndex = h * samEncoderSize + w;
                int nhwcIndex = (h * samEncoderSize + w) * 3;

                // BGR порядок каналов в диапазоне [0-1] (финальная попытка)
                nhwcData[nhwcIndex + 0] = pixels[pixelIndex].b; // B канал
                nhwcData[nhwcIndex + 1] = pixels[pixelIndex].g; // G канал
                nhwcData[nhwcIndex + 2] = pixels[pixelIndex].r; // R канал
            }
        }

        Tensor<float> encoderInput = new Tensor<float>(new TensorShape(1, samEncoderSize, samEncoderSize, 3), nhwcData);

        // Очищаем ресурсы
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(tempRT);
        Destroy(tempTex);
        
        if (processedFrameCount % 100 == 0)
        {
            Debug.Log($"🧠 SAM Encoder input: shape={encoderInput.shape}");
        }
        
        // Запускаем SAM Encoder
        samEncoderWorker.SetInput("image", encoderInput);
        samEncoderWorker.Schedule();
        yield return new WaitForEndOfFrame();
        
        // Получаем embeddings
        var newEmbeddings = samEncoderWorker.PeekOutput() as Tensor<float>;
        
        if (newEmbeddings != null)
        {
            // Освобождаем старые embeddings
            if (cachedImageEmbeddings != null)
                cachedImageEmbeddings.Dispose();
            
            // Сохраняем новые embeddings (клонируем, чтобы не потерять после следующего Schedule)
            cachedImageEmbeddings = newEmbeddings.ReadbackAndClone();
            lastEncodedTexture = sourceTexture;
            
            if (processedFrameCount % 100 == 0)
                Debug.Log($"🧠 SAM Encoder: Embeddings обновлены ({cachedImageEmbeddings.shape})");
        }
        
        encoderInput.Dispose();
        isEncodingInProgress = false;
    }
    
    /// <summary>
    /// Обрабатывает клик пользователя: определяет класс и запускает SAM для точной маски
    /// </summary>
    public void ProcessClick(Vector2 screenPosition, System.Action<Tensor<float>, int> onMaskReady)
    {
        if (latestSegformerMask == null)
        {
            Debug.LogWarning("⚠️ Маска SegFormer ещё не готова. Подождите...");
            return;
        }
        
        if (cachedImageEmbeddings == null)
        {
            Debug.LogWarning("⚠️ SAM Embeddings ещё не готовы. Подождите...");
            return;
        }
        
        StartCoroutine(ProcessClickAsync(screenPosition, onMaskReady));
    }
    
    private IEnumerator ProcessClickAsync(Vector2 screenPosition, System.Action<Tensor<float>, int> onMaskReady)
    {
        // Преобразуем экранные координаты в координаты маски SegFormer
        float normalizedX = screenPosition.x / Screen.width;
        float normalizedY = 1.0f - (screenPosition.y / Screen.height); // Flip Y
        
        int maskX = Mathf.Clamp((int)(normalizedX * latestMaskWidth), 0, latestMaskWidth - 1);
        int maskY = Mathf.Clamp((int)(normalizedY * latestMaskHeight), 0, latestMaskHeight - 1);
        
        // Копируем маску на CPU для чтения
        var maskCPU = latestSegformerMask.ReadbackAndClone();
        int clickedClassId = maskCPU[0, maskY, maskX, 0];
        maskCPU.Dispose();
        
        Debug.Log($"🎯 Клик ({screenPosition.x:F0}, {screenPosition.y:F0}) → Класс {clickedClassId}");
        
        // Создаём промпт для SAM (позитивная точка)
        // SAM работает с координатами в исходном разрешении (1024x1024)
        float samX = normalizedX * 1024f;
        float samY = normalizedY * 1024f;
        
        // Входы для SAM Decoder
        var pointCoords = new Tensor<float>(new TensorShape(1, 1, 2), new float[] { samX, samY });
        var pointLabels = new Tensor<float>(new TensorShape(1, 1), new float[] { 1.0f }); // 1 = positive point
        var hasMaskInput = new Tensor<float>(new TensorShape(1), new float[] { 0.0f });
        var origImSize = new Tensor<float>(new TensorShape(2), new float[] { 1024f, 1024f });
        
        // Запускаем SAM Decoder
        samDecoderWorker.SetInput("image_embeddings", cachedImageEmbeddings);
        samDecoderWorker.SetInput("point_coords", pointCoords);
        samDecoderWorker.SetInput("point_labels", pointLabels);
        samDecoderWorker.SetInput("has_mask_input", hasMaskInput);
        samDecoderWorker.SetInput("orig_im_size", origImSize);
        
        samDecoderWorker.Schedule();
        yield return new WaitForEndOfFrame();
        
        // Получаем результат (маска от SAM)
        var samMask = samDecoderWorker.PeekOutput("masks") as Tensor<float>;
        
        if (samMask != null)
        {
            Debug.Log($"✅ SAM Decoder: Маска готова ({samMask.shape})");
            
            // Клонируем маску для передачи
            var maskClone = samMask.ReadbackAndClone();
            onMaskReady?.Invoke(maskClone, clickedClassId);
        }
        else
        {
            Debug.LogError("❌ SAM Decoder не вернул маску!");
        }
        
        // Cleanup
        pointCoords.Dispose();
        pointLabels.Dispose();
        hasMaskInput.Dispose();
        origImSize.Dispose();
    }
    
    void OnDestroy()
    {
        segformerWorker?.Dispose();
        samEncoderWorker?.Dispose();
        samDecoderWorker?.Dispose();
        cachedImageEmbeddings?.Dispose();
        latestSegformerMask?.Dispose();
    }
}


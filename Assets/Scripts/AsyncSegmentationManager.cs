using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class AsyncSegmentationManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private ARCameraManager arCameraManager;
    [SerializeField]
    private RawImage segmentationDisplay;
    [SerializeField]
    private ModelAsset modelAsset;
    [SerializeField]
    private ComputeShader argmaxShader;
    [SerializeField]
    private Material visualizationMaterial; // Материал для визуализации маски

    private Model runtimeModel;
    private Worker worker;

    [Header("Processing & Visualization")]
    [SerializeField]
    private Vector2Int processingResolution = new Vector2Int(512, 512);
    
    // Fields for PerformanceControlUI compatibility
    [Tooltip("The number of frames to skip between processing.")]
    public int frameSkipRate = 1; 
    [Tooltip("This property is obsolete but kept for UI compatibility.")]
    public float minFrameInterval { get; set; }
    [Tooltip("This property is obsolete but kept for UI compatibility.")]
    public bool UseCpuArgmax { get; set; }

    // GPU Textures & Buffers
    private RenderTexture cameraInputTexture;
    private RenderTexture segmentationMaskTexture; // RFloat texture with class indices
    private Material displayMaterialInstance;

    // Sentis Tensors
    private Tensor<float> inputTensor;

    private CancellationTokenSource cancellationTokenSource;
    private bool isProcessing = false;
    private int frameCount = 0;
    
    // Для кэширования, чтобы избежать аллокаций
    private XRCpuImage.ConversionParams conversionParams;

    private const int NUM_CLASSES = 16; 

    void OnEnable()
    {
        cancellationTokenSource = new CancellationTokenSource();
        InitializeSystem();
    }

    void OnDisable()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        worker?.Dispose();
        inputTensor?.Dispose();

        ReleaseRenderTexture(cameraInputTexture);
        ReleaseRenderTexture(segmentationMaskTexture);
    }

    void Update()
    {
        if (ARSession.state < ARSessionState.SessionTracking || worker == null || isProcessing)
        {
            return;
        }

        if (frameCount % (frameSkipRate + 1) == 0)
        {
            if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            {
                Debug.Log($"🎥 Обрабатываем кадр #{frameCount}");
                ProcessFrameAsync(cpuImage);
            }
            else
            {
                Debug.LogWarning("⚠️ Не удалось получить изображение с камеры");
            }
        }
        frameCount++;
    }

    private void InitializeSystem()
    {
        arCameraManager = FindObjectOfType<ARCameraManager>();
        if (segmentationDisplay == null)
        {
            // Попробуем найти RawImage, если не присвоен в инспекторе
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                segmentationDisplay = canvas.GetComponentInChildren<RawImage>();
                if (segmentationDisplay != null)
                {
                    Debug.Log("✅ RawImage для отображения найден автоматически.");
                }
            }
        }


        Debug.Log("🚀 AsyncSegmentationManager: Начинаем инициализацию...");
        
        if (modelAsset == null)
        {
            Debug.LogError("❌ Model Asset не назначен в AsyncSegmentationManager!");
            return;
        }
        
        if (argmaxShader == null)
        {
            Debug.LogError("❌ Argmax Shader не назначен в AsyncSegmentationManager!");
            return;
        }
        
        if (segmentationDisplay == null)
        {
            Debug.LogError("❌ Segmentation Display не назначен в AsyncSegmentationManager!");
            return;
        }

        try
        {
            runtimeModel = ModelLoader.Load(modelAsset);
            Debug.Log($"✅ Модель загружена: {modelAsset.name}");
            
            // --- ИСПРАВЛЕНИЕ ---
            // Динамически получаем разрешение входа модели. 
            // Это исправляет ошибку "width & height must be larger than 0".
            try
            {
                var inputShape = runtimeModel.inputs[0].shape.ToTensorShape();
                // Ожидаем формат NCHW (Batch, Channels, Height, Width)
                int height = inputShape[2];
                int width = inputShape[3];
                processingResolution = new Vector2Int(width, height);
                Debug.Log($"✅ Обнаружено разрешение входа модели: {processingResolution.x}x{processingResolution.y}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Не удалось определить разрешение модели. Используется значение из инспектора: {processingResolution}. Ошибка: {e.Message}");
                if (processingResolution.x <= 0 || processingResolution.y <= 0)
                {
                    Debug.LogError("🚨 'processingResolution' в инспекторе имеет значение 0! Установите корректное значение (например, 512x512) или убедитесь, что модель корректна.");
                    return; // Прерываем инициализацию
                }
            }
            
            worker = new Worker(runtimeModel, BackendType.GPUCompute);
            Debug.Log("✅ Worker создан с GPUCompute backend");

            // Инициализация текстур
            cameraInputTexture = CreateRenderTexture(processingResolution.x, processingResolution.y, RenderTextureFormat.ARGB32);
            // segmentationMaskTexture будет создана динамически по размеру выхода модели

            if (segmentationDisplay!= null && visualizationMaterial!= null)
            {
                displayMaterialInstance = new Material(visualizationMaterial);
                segmentationDisplay.material = displayMaterialInstance;
                Debug.Log("✅ Материал для отображения настроен");

                // Растягиваем RawImage на весь экран
                var rectTransform = segmentationDisplay.rectTransform;
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = new Vector3(-1, -1, 1); // Отзеркаливаем по X и Y
                Debug.Log("✅ SegmentationDisplay растянут на весь экран и отзеркален");
            }
            else
            {
                Debug.LogWarning("⚠️ Visualization Material не назначен!");
            }
            
            Debug.Log("🎉 AsyncSegmentationManager инициализация завершена успешно!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка инициализации AsyncSegmentationManager: {e.Message}\n{e.StackTrace}");
        }

        StartCoroutine(ForceMaterialUpdate());
    }

    private System.Collections.IEnumerator ForceMaterialUpdate()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (segmentationDisplay != null && segmentationDisplay.material.shader.name != "Unlit/VisualizeMask")
            {
                Debug.LogWarning("⚠️ Обнаружен неверный материал! Принудительно устанавливаем правильный материал.");
                var displayMaterial = new Material(visualizationMaterial);
                displayMaterial.SetTexture("_MaskTex", segmentationMaskTexture);
                segmentationDisplay.material = displayMaterial;
            }
        }
    }

    private async void ProcessFrameAsync(XRCpuImage cpuImage)
    {
        isProcessing = true;
        Debug.Log("🔄 Начинаем асинхронную обработку кадра...");

        try
        {
            var convertTask = ConvertCpuImageToTexture(cpuImage);
            await convertTask;
            if (cancellationTokenSource.IsCancellationRequested ||!convertTask.IsCompletedSuccessfully) return;

            // Конвертация в тензор
            inputTensor?.Dispose();
            inputTensor = TextureConverter.ToTensor(cameraInputTexture, processingResolution.x, processingResolution.y, 3);
            Debug.Log($"✅ Тензор создан: {inputTensor.shape}");
            
            worker.Schedule(inputTensor);
            Debug.Log("✅ Inference запущен");

            ProcessOutputWithArgmaxShader();
            Debug.Log("✅ Постобработка завершена");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Frame processing failed: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            cpuImage.Dispose();
            isProcessing = false;
        }
    }
    
    private void ProcessOutputWithArgmaxShader()
    {
        var outputTensor = worker.PeekOutput() as Tensor<float>;
        if (outputTensor == null) 
        {
            Debug.LogError("❌ Выходной тензор равен null!");
            return;
        }

        var shape = outputTensor.shape;
        int height = shape[2];
        int width = shape[3];
        int numClasses = shape[1];

        // Динамически создаем или изменяем размер текстуры для маски
        if (segmentationMaskTexture == null || segmentationMaskTexture.width != width || segmentationMaskTexture.height != height)
        {
            ReleaseRenderTexture(segmentationMaskTexture);
            segmentationMaskTexture = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);
            
            if (displayMaterialInstance != null)
            {
                displayMaterialInstance.SetTexture("_MaskTex", segmentationMaskTexture);
                Debug.Log($"✅ Текстура маски создана/изменена на {width}x{height} и привязана к материалу");
            }
        }

        var tensorData = outputTensor.DownloadToArray();
        
        var cmd = new CommandBuffer { name = "SegmentationPostProcessing" };

        var tensorDataBuffer = new ComputeBuffer(tensorData.Length, sizeof(float));
        tensorDataBuffer.SetData(tensorData);

        int kernel = argmaxShader.FindKernel("Argmax");
        cmd.SetComputeIntParam(argmaxShader, "width", width);
        cmd.SetComputeIntParam(argmaxShader, "height", height);
        cmd.SetComputeIntParam(argmaxShader, "num_classes", numClasses);
        
        cmd.SetComputeBufferParam(argmaxShader, kernel, "InputTensor", tensorDataBuffer);
        cmd.SetComputeTextureParam(argmaxShader, kernel, "Result", segmentationMaskTexture);
        
        int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
        cmd.DispatchCompute(argmaxShader, kernel, threadGroupsX, threadGroupsY, 1);
        
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Dispose();
        
        tensorDataBuffer.Dispose();
        outputTensor.Dispose();
    }

    private async Task ConvertCpuImageToTexture(XRCpuImage cpuImage)
    {
        conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
            outputDimensions = new Vector2Int(processingResolution.x, processingResolution.y),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        var conversionRequest = cpuImage.ConvertAsync(conversionParams);

        while (!conversionRequest.status.IsDone())
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                conversionRequest.Dispose();
                return;
            }
            await Task.Yield();
        }

        if (conversionRequest.status == XRCpuImage.AsyncConversionStatus.Ready)
        {
            var tempTexture = new Texture2D(
                conversionRequest.conversionParams.outputDimensions.x,
                conversionRequest.conversionParams.outputDimensions.y,
                conversionRequest.conversionParams.outputFormat,
                false);
    
            tempTexture.LoadRawTextureData(conversionRequest.GetData<byte>());
            tempTexture.Apply();

            Graphics.Blit(tempTexture, cameraInputTexture);
            
            Destroy(tempTexture);
        }
        else
        {
            Debug.LogError($"CPU Image conversion failed with status: {conversionRequest.status}");
        }
        conversionRequest.Dispose();
    }

    private RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format)
    {
        var rt = new RenderTexture(width, height, 0, format)
        {
            enableRandomWrite = true
        };
        rt.Create();
        return rt;
    }

    private void ReleaseRenderTexture(RenderTexture rt)
    {
        if (rt!= null)
        {
            rt.Release();
            Destroy(rt);
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
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
    private ComputeShader imageNormalizerShader; // Шейдер для нормализации
    [SerializeField]
    private ComputeShader maskPostProcessingShader; // Шейдер для сглаживания маски
    [SerializeField]
    private Material visualizationMaterial; // Материал для визуализации маски
    [SerializeField]
    private ARWallPresenter arWallPresenter; // Ссылка на презентер для фотореалистичной окраски

    private Model runtimeModel;
    private Worker worker;

    [Header("Processing & Visualization")]
    [SerializeField]
    private Vector2Int processingResolution = new Vector2Int(512, 512);
    [Tooltip("Enable median filter to smooth the mask")]
    [SerializeField]
    private bool enableMaskSmoothing = true;
    [Tooltip("Number of smoothing passes to apply.")]
    [SerializeField, Range(1, 10)]
    private int maskSmoothingIterations = 5; // Увеличено для лучшего сглаживания

    [Header("Class Visualization")]
    [Tooltip("Selected class to display (-1 for all classes)")]
    [SerializeField]
    private int selectedClass = 0; // Только стены (класс 0)
    [Tooltip("Opacity of the segmentation overlay")]
    [SerializeField, Range(0f, 1f)]
    private float visualizationOpacity = 0.5f;
    [Tooltip("The color to use for painting the selected class")]
    public Color paintColor = Color.red;
    [Tooltip("Show all classes with different colors")]
    public bool showAllClasses = false; // ОТКЛЮЧЕНО - только стены
    [Tooltip("Show only walls (class 0)")]
    public bool showWalls = true; // ВКЛЮЧЕНО - только стены
    [Tooltip("Show only floors (class 3)")]
    public bool showFloors = false; // ОТКЛЮЧЕНО
    [Tooltip("Show only ceilings (class 5)")]
    public bool showCeilings = false; // ОТКЛЮЧЕНО

    // Fields for PerformanceControlUI compatibility
    [Tooltip("The number of frames to skip between processing.")]
    public int frameSkipRate = 1;
    [Tooltip("This property is obsolete but kept for UI compatibility.")]
    public float minFrameInterval { get; set; }
    [Tooltip("This property is obsolete but kept for UI compatibility.")]
    public bool UseCpuArgmax { get; set; }

    // GPU Textures & Buffers
    private RenderTexture cameraInputTexture;
    private RenderTexture normalizedTexture; // Текстура для нормализованного изображения
    private RenderTexture segmentationMaskTexture; // RFloat texture with class indices
    private RenderTexture smoothedMaskTexture; // RFloat texture for the smoothed mask
    private RenderTexture pingPongMaskTexture; // Temporary texture for multi-pass smoothing
    private Material displayMaterialInstance;

    // Sentis Tensors
    private Tensor<float> inputTensor;

    private CancellationTokenSource cancellationTokenSource;
    private bool isProcessing = false;
    private int frameCount = 0;

    // Для кэширования, чтобы избежать аллокаций
    private XRCpuImage.ConversionParams conversionParams;

    private const int NUM_CLASSES = 150;

    private static readonly Dictionary<int, string> classNames = new Dictionary<int, string>
    {
        {0, "wall"}, {1, "building"}, {2, "sky"}, {3, "floor"}, {4, "tree"},
        {5, "ceiling"}, {6, "road"}, {7, "bed "}, {8, "windowpane"}, {9, "grass"},
        {10, "cabinet"}, {11, "sidewalk"}, {12, "person"}, {13, "earth"}, {14, "door"},
        {15, "table"}, {16, "mountain"}, {17, "plant"}, {18, "curtain"}, {19, "chair"},
        {20, "car"}, {21, "water"}, {22, "painting"}, {23, "sofa"}, {24, "shelf"},
        {25, "house"}, {26, "sea"}, {27, "mirror"}, {28, "rug"}, {29, "field"},
        {30, "armchair"}, {31, "seat"}, {32, "fence"}, {33, "desk"}, {34, "rock"},
        {35, "wardrobe"}, {36, "lamp"}, {37, "bathtub"}, {38, "railing"}, {39, "cushion"},
        {40, "base"}, {41, "box"}, {42, "column"}, {43, "signboard"}, {44, "chest of drawers"},
        {45, "counter"}, {46, "sand"}, {47, "sink"}, {48, "skyscraper"}, {49, "fireplace"},
        {50, "refrigerator"}, {51, "grandstand"}, {52, "path"}, {53, "stairs"}, {54, "runway"},
        {55, "case"}, {56, "pool table"}, {57, "pillow"}, {58, "screen door"}, {59, "stairway"},
        {60, "river"}, {61, "bridge"}, {62, "bookcase"}, {63, "blind"}, {64, "coffee table"},
        {65, "toilet"}, {66, "flower"}, {67, "book"}, {68, "hill"}, {69, "bench"},
        {70, "countertop"}, {71, "stove"}, {72, "palm"}, {73, "kitchen island"}, {74, "computer"},
        {75, "swivel chair"}, {76, "boat"}, {77, "bar"}, {78, "arcade machine"}, {79, "hovel"},
        {80, "bus"}, {81, "towel"}, {82, "light"}, {83, "truck"}, {84, "tower"},
        {85, "chandelier"}, {86, "awning"}, {87, "streetlight"}, {88, "booth"}, {89, "television receiver"},
        {90, "airplane"}, {91, "dirt track"}, {92, "apparel"}, {93, "pole"}, {94, "land"},
        {95, "bannister"}, {96, "escalator"}, {97, "ottoman"}, {98, "bottle"}, {99, "buffet"},
        {100, "poster"}, {101, "stage"}, {102, "van"}, {103, "ship"}, {104, "fountain"},
        {105, "conveyer belt"}, {106, "canopy"}, {107, "washer"}, {108, "plaything"}, {109, "swimming pool"},
        {110, "stool"}, {111, "barrel"}, {112, "basket"}, {113, "waterfall"}, {114, "tent"},
        {115, "bag"}, {116, "minibike"}, {117, "cradle"}, {118, "oven"}, {119, "ball"},
        {120, "food"}, {121, "step"}, {122, "tank"}, {123, "trade name"}, {124, "microwave"},
        {125, "pot"}, {126, "animal"}, {127, "bicycle"}, {128, "lake"}, {129, "dishwasher"},
        {130, "screen"}, {131, "blanket"}, {132, "sculpture"}, {133, "hood"}, {134, "sconce"},
        {135, "vase"}, {136, "traffic light"}, {137, "tray"}, {138, "ashcan"}, {139, "fan"},
        {140, "pier"}, {141, "crt screen"}, {142, "plate"}, {143, "monitor"}, {144, "bulletin board"},
        {145, "shower"}, {146, "radiator"}, {147, "glass"}, {148, "clock"}, {149, "flag"}
    };

    // Поля для отслеживания изменений в настройках отображения
    private int lastSelectedClass = 0; // Стены по умолчанию
    private float lastOpacity = -1f;
    private bool lastShowAll = true;

    void OnEnable()
    {
        // Принудительно устанавливаем начальное состояние, чтобы избежать сохраненных в инспекторе значений
        selectedClass = -2;
        showAllClasses = false;
        showWalls = false;
        showFloors = false;
        showCeilings = false;

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
        ReleaseRenderTexture(normalizedTexture);
        ReleaseRenderTexture(segmentationMaskTexture);
        ReleaseRenderTexture(smoothedMaskTexture);
        ReleaseRenderTexture(pingPongMaskTexture);
    }

    void Update()
    {
        // Обновляем параметр ориентации в шейдере
        if (displayMaterialInstance != null)
        {
            // Используем пропорции для определения ориентации, чтобы было согласовано с ConvertCpuImageToTexture
            bool isPortrait = Screen.height > Screen.width;
            bool isRealDevice = !Application.isEditor;

            // Передаем информацию о портретном режиме и типе устройства в шейдер
            displayMaterialInstance.SetFloat("_IsPortrait", isPortrait ? 1.0f : 0.0f);
            displayMaterialInstance.SetFloat("_IsRealDevice", isRealDevice ? 1.0f : 0.0f);
        }

        // Отладка: определяем класс по клику
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            StartCoroutine(GetClassAtScreenPositionCoroutine(Input.mousePosition));
        }

        // Проверяем изменения в настройках отображения
        if (selectedClass != lastSelectedClass ||
            Mathf.Abs(visualizationOpacity - lastOpacity) > 0.01f ||
            showAllClasses != lastShowAll)
        {
            UpdateMaterialParameters();
            lastSelectedClass = selectedClass;
            lastOpacity = visualizationOpacity;
            lastShowAll = showAllClasses;
        }

        if (ARSession.state < ARSessionState.SessionTracking || worker == null || isProcessing)
        {
            return;
        }

        if (frameCount % (frameSkipRate + 1) == 0)
        {
            if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            {
                ProcessFrameAsync(cpuImage);
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

            if (processingResolution.x <= 0 || processingResolution.y <= 0)
            {
                Debug.LogError("🚨 'processingResolution' в инспекторе имеет значение 0! Установите корректное значение (например, 512x512).");
                return;
            }
            Debug.Log($"✅ Используется разрешение входа из инспектора: {processingResolution.x}x{processingResolution.y}");

            worker = new Worker(runtimeModel, BackendType.GPUCompute);
            Debug.Log("✅ Worker создан с GPUCompute backend");

            // Создаем текстуры с максимальным разрешением (будем изменять размер динамически)
            int maxRes = processingResolution.x;
            cameraInputTexture = CreateRenderTexture(maxRes, maxRes, RenderTextureFormat.ARGB32);
            normalizedTexture = CreateRenderTexture(maxRes, maxRes, RenderTextureFormat.ARGBFloat);

            if (segmentationDisplay != null && visualizationMaterial != null)
            {
                displayMaterialInstance = new Material(visualizationMaterial);
                segmentationDisplay.material = displayMaterialInstance;
                UpdateMaterialParameters();
                Debug.Log("✅ Материал для отображения настроен");

                // Настраиваем правильное соотношение сторон для телефона
                SetupCorrectAspectRatio();
            }
            else
            {
                Debug.LogWarning("⚠️ Visualization Material не назначен!");
            }

            Debug.Log("🎉 AsyncSegmentationManager инициализация завершена успешно!");

            // Автоматически показываем только стены для удобства тестирования
            Invoke(nameof(ShowOnlyWalls), 1f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка инициализации AsyncSegmentationManager: {e.Message}\n{e.StackTrace}");
        }

        // 🚨 ПРИНУДИТЕЛЬНО устанавливаем режим "только стены"
        ForceWallOnlyMode();

        StartCoroutine(ForceMaterialUpdate());
    }

    /// <summary>
    /// Принудительно устанавливает режим отображения только стен
    /// </summary>
    private void ForceWallOnlyMode()
    {
        selectedClass = 0;           // Только класс 0 (стены)
        showAllClasses = false;      // НЕ показывать все классы
        showWalls = true;            // Показывать стены
        showFloors = false;          // НЕ показывать полы
        showCeilings = false;        // НЕ показывать потолки

        Debug.Log("🧱 ПРИНУДИТЕЛЬНО активирован режим: ТОЛЬКО СТЕНЫ (класс 0)");
        Debug.Log("✅ ИСПРАВЛЕНИЯ: убрано двойное UV инвертирование, исправлена логика отображения, добавлена билинейная фильтрация");

        // 🚨 ПРИНУДИТЕЛЬНО включаем максимальное сглаживание
        enableMaskSmoothing = true;
        maskSmoothingIterations = 8; // Увеличиваем еще больше
        Debug.Log($"🎯 ПРИНУДИТЕЛЬНО включено сглаживание: {maskSmoothingIterations} итераций");

        // Обновляем материал с новыми настройками
        UpdateMaterialParameters();
    }

    private void SetupCorrectAspectRatio()
    {
        // ТЕСТ: УБИРАЕМ AspectRatioFitter - пусть маска растягивается на весь экран
        var fitter = segmentationDisplay.GetComponent<AspectRatioFitter>();
        if (fitter != null)
        {
            DestroyImmediate(fitter);
            Debug.Log("🗑️ AspectRatioFitter удален - маска растягивается на весь экран");
        }

        // Настройка для центрирования
        var rectTransform = segmentationDisplay.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        // Масштаб остается 1:1 - отражение только в шейдере
        rectTransform.localScale = Vector3.one;

        // Принудительно устанавливаем позицию в центр
        rectTransform.anchoredPosition = Vector2.zero;

        // Убеждаемся что Canvas Scaler настроен правильно
        var canvas = segmentationDisplay.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var canvasScaler = canvas.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(Screen.width, Screen.height);
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                Debug.Log($"🎯 CanvasScaler настроен: {Screen.width}x{Screen.height}, режим Expand");
            }
        }

        Debug.Log($"✅ RawImage настроен на точное покрытие экрана {Screen.width}x{Screen.height} без AspectRatioFitter");

        // Диагностика соответствия размеров
        Debug.Log($"🔍 ДИАГНОСТИКА РАЗМЕРОВ:");
        Debug.Log($"   📱 Экран: {Screen.width}x{Screen.height} (соотношение {(float)Screen.width / Screen.height:F2})");
        Debug.Log($"   🎯 RawImage: {rectTransform.rect.width:F0}x{rectTransform.rect.height:F0}");

        if (segmentationMaskTexture != null)
        {
            Debug.Log($"   🧱 Маска: {segmentationMaskTexture.width}x{segmentationMaskTexture.height} (соотношение {(float)segmentationMaskTexture.width / segmentationMaskTexture.height:F2})");
        }
    }

    private System.Collections.IEnumerator ForceMaterialUpdate()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (segmentationDisplay != null && segmentationDisplay.material != null &&
                segmentationDisplay.material.shader.name != "Unlit/VisualizeMask")
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

        try
        {
            var convertTask = ConvertCpuImageToTexture(cpuImage);
            await convertTask;
            if (cancellationTokenSource.IsCancellationRequested || !convertTask.IsCompletedSuccessfully) return;

            NormalizeImage();

            inputTensor?.Dispose();
            // Используем размеры текстуры, а не processingResolution
            inputTensor = TextureConverter.ToTensor(normalizedTexture, normalizedTexture.width, normalizedTexture.height, 3);

            worker.Schedule(inputTensor);

            ProcessOutputWithArgmaxShader();
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

    private void NormalizeImage()
    {
        if (imageNormalizerShader == null)
        {
            Debug.LogError("Image Normalizer Shader не назначен!");
            return;
        }

        int kernel = imageNormalizerShader.FindKernel("Normalize");

        imageNormalizerShader.SetVector("image_mean", new Vector4(0.485f, 0.456f, 0.406f, 0));
        imageNormalizerShader.SetVector("image_std", new Vector4(0.229f, 0.224f, 0.225f, 0));

        imageNormalizerShader.SetTexture(kernel, "InputTexture", cameraInputTexture);
        imageNormalizerShader.SetTexture(kernel, "OutputTexture", normalizedTexture);

        // Используем квадратные размеры текстуры
        int threadGroupsX = Mathf.CeilToInt(cameraInputTexture.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(cameraInputTexture.height / 8.0f);
        imageNormalizerShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
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
        // Используем размеры из тензора
        int batchSize = shape[0];
        int numClasses = shape[1];
        int height = shape[2];
        int width = shape[3];

        // Debug.Log($"🔍 Размеры тензора: batch={batchSize}, classes={numClasses}, height={height}, width={width}"); // Убран частый лог
        // Debug.Log($"📏 Входная текстура: {cameraInputTexture.width}x{cameraInputTexture.height}"); // Убран частый лог

        // Проверяем соответствие размеров
        if (width != height)
        {
            Debug.LogError($"❌ Тензор не квадратный: {width}x{height}!");
            return;
        }

        if (segmentationMaskTexture == null || segmentationMaskTexture.width != width || segmentationMaskTexture.height != height)
        {
            ReleaseRenderTexture(segmentationMaskTexture);
            segmentationMaskTexture = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);

            ReleaseRenderTexture(smoothedMaskTexture);
            smoothedMaskTexture = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);
            ReleaseRenderTexture(pingPongMaskTexture);
            pingPongMaskTexture = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);

            if (displayMaterialInstance != null)
            {
                displayMaterialInstance.SetTexture("_MaskTex", segmentationMaskTexture);
                UpdateMaterialParameters();
                // Debug.Log($"✅ Текстура маски создана/изменена на {width}x{height} и привязана к материалу"); // Убран частый лог
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

        RenderTexture finalMask = segmentationMaskTexture; // По умолчанию используем исходную маску

        if (enableMaskSmoothing && maskPostProcessingShader != null && maskSmoothingIterations > 0)
        {
            Debug.Log($"🎯 ПРИМЕНЯЕТСЯ СГЛАЖИВАНИЕ МАСКИ: {maskSmoothingIterations} итераций на {width}x{height}");
            int postProcessingKernel = maskPostProcessingShader.FindKernel("MedianFilter");
            cmd.SetComputeIntParam(maskPostProcessingShader, "width", width);
            cmd.SetComputeIntParam(maskPostProcessingShader, "height", height);

            RenderTexture source = segmentationMaskTexture;
            RenderTexture destination = smoothedMaskTexture;

            for (int i = 0; i < maskSmoothingIterations; i++)
            {
                cmd.SetComputeTextureParam(maskPostProcessingShader, postProcessingKernel, "InputMask", source);
                cmd.SetComputeTextureParam(maskPostProcessingShader, postProcessingKernel, "ResultMask", destination);
                cmd.DispatchCompute(maskPostProcessingShader, postProcessingKernel, threadGroupsX, threadGroupsY, 1);

                RenderTexture temp = source;
                source = destination;

                destination = (source == smoothedMaskTexture) ? pingPongMaskTexture : smoothedMaskTexture;
            }
            displayMaterialInstance.SetTexture("_MaskTex", source);
            finalMask = source; // Запоминаем финальную сглаженную маску
        }
        else
        {
            displayMaterialInstance.SetTexture("_MaskTex", segmentationMaskTexture);
        }

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Dispose();

        // Передаем маску в ARWallPresenter для фотореалистичной окраски
        if (arWallPresenter != null)
        {
            // OPTIMIZATION: Используем оптимизированную маску если доступна
            var maskToSend = OptimizeMaskIfNeeded(finalMask);
            arWallPresenter.SetSegmentationMask(maskToSend);
            // Debug.Log("🎨 Маска передана в ARWallPresenter"); // Убран частый лог
        }
        else
        {
            Debug.LogWarning("⚠️ ARWallPresenter не назначен в AsyncSegmentationManager!");
        }

        tensorDataBuffer.Dispose();
        outputTensor.Dispose();
    }

    /// <summary>
    /// OPTIMIZATION: Оптимизирует маску сегментации для снижения использования памяти
    /// </summary>
    private Texture OptimizeMaskIfNeeded(Texture originalMask)
    {
        // В production версии здесь можно конвертировать в R8 формат
        // Для упрощения возвращаем оригинальную маску
        return originalMask;
    }

    private async Task ConvertCpuImageToTexture(XRCpuImage cpuImage)
    {
        // Определяем правильную трансформацию в зависимости от ориентации
        // Используем пропорции экрана для определения ориентации - это надежнее, чем Screen.orientation в редакторе.
        bool isScreenPortrait = Screen.height > Screen.width;

        // НИКАКИХ ТРАНСФОРМАЦИЙ! Пусть изображение остаётся как есть
        var transformation = XRCpuImage.Transformation.None;

        Debug.Log($"📱 Режим {(isScreenPortrait ? "портрет" : "ландшафт")} ({Screen.width}x{Screen.height}). Трансформация: {transformation} (без изменений)");

        // ТЕСТ: Используем ВСЮ КАМЕРУ, а не центральную область, и растягиваем до квадрата
        // Это может исправить смещение координат
        float cameraAspectRatio = (float)cpuImage.width / cpuImage.height;

        int targetResolution = Mathf.Min(processingResolution.x, Mathf.Min(cpuImage.width, cpuImage.height));

        // Берём ВСЮЮ камеру и растягиваем до квадрата (как было изначально)
        conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height), // ВСЯ камера
            outputDimensions = new Vector2Int(targetResolution, targetResolution), // Квадратный выход
            outputFormat = TextureFormat.RGBA32,
            transformation = transformation
        };

        Debug.Log($"📐 Камера: {cpuImage.width}x{cpuImage.height} (AR: {cameraAspectRatio:F2}), растяжение ВСЕЙ камеры до: {targetResolution}x{targetResolution}");

        // Debug.Log($"📐 Камера: {cpuImage.width}x{cpuImage.height}, сжатие до {targetResolution}x{targetResolution}"); // Убран частый лог

        // Пересоздаем текстуры, если размер изменился
        if (cameraInputTexture.width != targetResolution || cameraInputTexture.height != targetResolution)
        {
            ReleaseRenderTexture(cameraInputTexture);
            ReleaseRenderTexture(normalizedTexture);

            cameraInputTexture = CreateRenderTexture(targetResolution, targetResolution, RenderTextureFormat.ARGB32);
            normalizedTexture = CreateRenderTexture(targetResolution, targetResolution, RenderTextureFormat.ARGBFloat);

            Debug.Log($"🔄 Пересоздали текстуры для разрешения {targetResolution}x{targetResolution} (квадратные для модели)");
        }

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
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear, // Изменено с Point на Bilinear для сглаживания
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }

    private void ReleaseRenderTexture(RenderTexture rt)
    {
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
        }
    }

    private IEnumerator GetClassAtScreenPositionCoroutine(Vector2 screenPos)
    {
        if (segmentationMaskTexture == null)
        {
            Debug.LogWarning("Отладочный тап: Текстура маски недоступна.");
            yield break;
        }

        var request = AsyncGPUReadback.Request(segmentationMaskTexture);
        yield return new WaitUntil(() => request.done);

        if (request.hasError)
        {
            Debug.LogError("Отладочный тап: Ошибка чтения GPU.");
            yield break;
        }

        var data = request.GetData<float>();

        // УПРОЩЕНО: прямые координаты экрана без всяких квадратных областей
        Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

        // НЕ ПРИМЕНЯЕМ НИКАКИХ ИНВЕРТИРОВАНИЙ - используем координаты как есть
        float uv_x = screenUV.x; // Прямые координаты
        float uv_y = screenUV.y; // Прямые координаты

        Debug.Log($"🎯 Клик: экран={screenPos}, screenUV=({screenUV.x:F3}, {screenUV.y:F3}), finalUV=({uv_x:F3}, {uv_y:F3}) [ПРЯМЫЕ координаты, БЕЗ трансформаций]");

        int textureX = (int)(uv_x * segmentationMaskTexture.width);
        int textureY = (int)(uv_y * segmentationMaskTexture.height);

        int index = textureY * segmentationMaskTexture.width + textureX;

        if (index >= 0 && index < data.Length)
        {
            float classIndexFloat = data[index];
            int classIndex = Mathf.RoundToInt(classIndexFloat);
            string className = classNames.ContainsKey(classIndex) ? classNames[classIndex] : "Unknown";
            Debug.Log($"👇 Класс в точке клика: {className} (ID: {classIndex})");

            selectedClass = classIndex;
            showAllClasses = false;
            showWalls = false;
            showFloors = false;
            showCeilings = false;
        }
    }

    /// <summary>
    /// Обновляет параметры материала для отображения классов
    /// </summary>
    private void UpdateMaterialParameters()
    {
        if (displayMaterialInstance == null)
        {
            Debug.LogWarning("⚠️ displayMaterialInstance is null в UpdateMaterialParameters!");
            return;
        }

        int classToShow = selectedClass;

        // 🚨 ЖЕСТКАЯ ЛОГИКА: ТОЛЬКО СТЕНЫ (класс 0)
        if (showAllClasses)
        {
            classToShow = -1;
            Debug.Log("🌈 Режим: ВСЕ КЛАССЫ");
        }
        else if (showWalls)
        {
            classToShow = 0;  // СТЕНЫ
            // Debug.Log("🧱 Режим: ТОЛЬКО СТЕНЫ (класс 0)"); // Отключен для избежания спама
        }
        else if (showFloors)
        {
            classToShow = 3;  // ПОЛЫ
            Debug.Log("🏠 Режим: ТОЛЬКО ПОЛЫ (класс 3)");
        }
        else if (showCeilings)
        {
            classToShow = 5;  // ПОТОЛКИ
            Debug.Log("🏠 Режим: ТОЛЬКО ПОТОЛКИ (класс 5)");
        }
        else
        {
            // Принудительно стены, если ничего не выбрано
            classToShow = 0;
            Debug.Log("⚠️ Режим неопределен - принудительно СТЕНЫ (класс 0)");
        }

        displayMaterialInstance.SetInt("_SelectedClass", classToShow);
        displayMaterialInstance.SetFloat("_Opacity", visualizationOpacity);
        displayMaterialInstance.SetColor("_PaintColor", paintColor);

        // Debug.Log($"✅ МАТЕРИАЛ ОБНОВЛЕН: _SelectedClass={classToShow}, _Opacity={visualizationOpacity}"); // Отключен для избежания спама
    }

    public void SetPaintColor(Color color)
    {
        paintColor = color;
        if (selectedClass >= 0)
        {
            UpdateMaterialParameters();
        }
    }

    public void SetSelectedClass(int classId)
    {
        selectedClass = classId;
        showAllClasses = (classId == -1);
        showWalls = (classId == 0);
        showFloors = (classId == 3);
        showCeilings = (classId == 5);
        UpdateMaterialParameters();
    }

    public void SetVisualizationOpacity(float opacity)
    {
        visualizationOpacity = Mathf.Clamp01(opacity);
        UpdateMaterialParameters();
    }

    public void ToggleShowAllClasses()
    {
        showAllClasses = !showAllClasses;
        if (showAllClasses)
        {
            showWalls = showFloors = showCeilings = false;
            selectedClass = -1;
        }
        UpdateMaterialParameters();
    }

    /// <summary>
    /// Показывает только стены (класс 0)
    /// </summary>
    public void ShowOnlyWalls()
    {
        selectedClass = 0;
        showAllClasses = false;
        showWalls = true;
        showFloors = false;
        showCeilings = false;

        // Принудительно обновляем параметры материала
        UpdateMaterialParameters();

        Debug.Log("🧱 Показываем только стены (класс 0)");

        // Дополнительная проверка, что параметры установлены правильно
        if (displayMaterialInstance != null)
        {
            int currentClass = displayMaterialInstance.GetInt("_SelectedClass");
            Debug.Log($"🔍 Проверка: _SelectedClass в материале = {currentClass}");
        }
    }

    [ContextMenu("Обновить покрытие экрана")]
    public void RefreshScreenCoverage()
    {
        SetupCorrectAspectRatio();
        Debug.Log("🔄 Покрытие экрана принудительно обновлено");
    }

    /// <summary>
    /// Скрывает всю сегментацию
    /// </summary>
    public void HideAllClasses()
    {
        selectedClass = -2;
        showAllClasses = false;
        showWalls = false;
        showFloors = false;
        showCeilings = false;
        UpdateMaterialParameters();
        Debug.Log("👻 Скрываем всю сегментацию");
    }

    /// <summary>
    /// Показывает все классы разными цветами
    /// </summary>
    public void ShowAllClassesColored()
    {
        selectedClass = -1;
        showAllClasses = true;
        showWalls = false;
        showFloors = false;
        showCeilings = false;
        UpdateMaterialParameters();
        Debug.Log("🌈 Показываем все классы разными цветами");
    }
}
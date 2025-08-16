using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// ИСПРАВЛЕНИЕ ИНВЕРСИИ МАСКИ: Убрали XRCpuImage.Transformation.MirrorY из ConvertCpuImageToTexture()
/// чтобы устранить проблему, когда при движении камеры вправо маска двигалась вниз и наоборот.
/// Теперь ориентация маски соответствует реальному миру.
/// </summary>

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
    private ComputeShader advancedPostProcessingShader; // Улучшенный шейдер постобработки
    [SerializeField]
    private ComputeShader upsampleShader; // Шейдер для увеличения разрешения маски
    [SerializeField]
    private Material visualizationMaterial; // Материал для визуализации маски
    [SerializeField]
    private ARWallPresenter arWallPresenter; // Ссылка на презентер для фотореалистичной окраски

    private Model runtimeModel;
    private Worker worker;

    [Header("Processing & Visualization")]
    [SerializeField]
    private Vector2Int processingResolution = new Vector2Int(512, 512); // ИСПРАВЛЕНО: Безопасное разрешение для всех устройств
    [Tooltip("Enable median filter to smooth the mask")]
    [SerializeField]
    private bool enableMaskSmoothing = true; // Сглаживание включено по умолчанию
    [Tooltip("Number of smoothing passes to apply.")]
    [SerializeField, Range(1, 15)]
    private int maskSmoothingIterations = 2; // УМЕНЬШЕНО для сохранения деталей и точности
    [Tooltip("Адаптивно снижать разрешение на слабых устройствах")]
    [SerializeField]
    private bool enableAdaptiveResolution = true;
    [Tooltip("Коэффициент постобработки краёв (0.1-2.0) - DEPRECATED")]
    [SerializeField, Range(0.1f, 2.0f)]
    [System.Obsolete("Поле больше не используется, будет удалено в следующей версии")]
    private float edgeEnhancementFactor = 1.2f;
    [Tooltip("Порог обнаружения краёв для адаптивного сглаживания")]
    [SerializeField, Range(0.01f, 0.5f)]
    private float edgeThreshold = 0.1f;
    [Tooltip("Коэффициент усиления контраста")]
    [SerializeField, Range(1.0f, 10.0f)]
    private float contrastFactor = 3.0f;
    [Tooltip("Использовать улучшенную постобработку")]
    [SerializeField]
    private bool useAdvancedPostProcessing = true;

    [Header("Отладка и логирование")]
    [Tooltip("Включить подробные логи (отключить для production)")]
    [SerializeField]
    private bool enableDebugLogging = false;
    [Tooltip("Показать контуры классов для отладки")]
    [SerializeField]
    private bool showClassOutlines = false;

    [Header("Режимы качества (выберите один)")]
    [Tooltip("Максимальная точность - отключает сглаживание, максимальное разрешение")]
    [SerializeField]
    private bool maxAccuracyMode = false;
    [Tooltip("Сбалансированный режим - оптимальное соотношение качества/производительности")]
    [SerializeField]
    private bool balancedMode = true;
    [Tooltip("Режим производительности - быстрая обработка с базовым качеством")]
    [SerializeField]
    private bool performanceMode = false;
    [Tooltip("Режим поворота маски (0=+90°, 1=-90°, 2=180°, 3=без поворота)")]
    [SerializeField, Range(0, 3)]
    private int maskRotationMode = 0;
    [Tooltip("Принудительно растягивать маску на весь экран")]
    [SerializeField]
    private bool forceFullscreenMask = true;
    [Tooltip("Применять коррекцию аспекта в шейдерах для полноэкранного режима")]
    [SerializeField]
    private bool useCameraAspectRatio = true;

    [Header("Class Visualization")]
    [Tooltip("Selected class to display (-1 for all classes)")]
    [SerializeField]
    private int selectedClass = -1; // Все классы по умолчанию
    [Tooltip("Opacity of the segmentation overlay")]
    [SerializeField, Range(0f, 1f)]
    private float visualizationOpacity = 0.5f; // Нормальное значение
    [Tooltip("Enable legacy RawImage display (disable for new projection system)")]
    [SerializeField]
    private bool enableLegacyDisplay = true;
    [Tooltip("The color to use for painting the selected class")]
    public Color paintColor = Color.red;
    [Tooltip("Show all classes with different colors")]
    public bool showAllClasses = true; // ВКЛЮЧЕНО - показываем все классы разными цветами
    [Tooltip("Show only walls (class 0)")]
    public bool showWalls = false;
    [Tooltip("Show only floors (class 3)")]
    public bool showFloors = false;
    [Tooltip("Show only ceilings (class 5)")]
    public bool showCeilings = false;

    [Header("Blinking Effect")]
    [Tooltip("Enable blinking effect for the mask")]
    [SerializeField]
#pragma warning disable 0414
    private bool enableBlinkingEffect = false;
#pragma warning restore 0414
    [Tooltip("Speed of the blinking effect")]
    [SerializeField, Range(0.1f, 10f)]
#pragma warning disable 0414
    private float blinkingSpeed = 1.0f;
#pragma warning restore 0414

    [Header("Интерактивные цвета")]
    [Tooltip("Массив цветов для смены цветов классов по клику")]
    [SerializeField]
    private Color[] interactiveColors = new Color[]
    {
        Color.red, Color.green, Color.blue, Color.yellow, Color.magenta,
        Color.cyan, new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f),
        new Color(1f, 0.8f, 0.2f), new Color(0.2f, 0.8f, 1f)
    };

    // Словарь пользовательских цветов для классов
    private Dictionary<int, Color> customClassColors = new Dictionary<int, Color>();
    private int currentColorIndex = 0;

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
    private RenderTexture upsampledMaskTexture; // RFloat texture for the upsampled mask
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

    // Переменная для сохранения оригинального аспекта камеры
#pragma warning disable 0414
    private float lastCameraAspect = 0.0f;
#pragma warning restore 0414

    private static readonly Dictionary<int, string> classNames = new Dictionary<int, string>
    {
        {0, "wall"}, {1, "building"}, {2, "background"}, {3, "floor"}, {4, "tree"},
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
        // Принудительно устанавливаем режим "только стены" при запуске
        ForceWallOnlyMode();

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
        ReleaseRenderTexture(upsampledMaskTexture);
        ReleaseRenderTexture(smoothedMaskTexture);
        ReleaseRenderTexture(pingPongMaskTexture);
    }

    void Update()
    {
        // Передаем параметры ориентации в шейдер
        if (displayMaterialInstance != null)
        {
            bool isPortrait = Screen.height > Screen.width;
            displayMaterialInstance.SetFloat("_IsPortrait", isPortrait ? 1.0f : 0.0f);

            // Убираем анимацию прозрачности, чтобы изолировать проблему
            displayMaterialInstance.SetFloat("_Opacity", visualizationOpacity);
        }

        // Проверяем изменение размера экрана и обновляем полноэкранный режим
        if (forceFullscreenMask && segmentationDisplay != null)
        {
            var rectTransform = segmentationDisplay.rectTransform;
            var currentSize = rectTransform.rect.size;
            var screenSize = new Vector2(Screen.width, Screen.height);

            // Если размер маски не соответствует экрану, обновляем
            if (Mathf.Abs(currentSize.x - screenSize.x) > 10f || Mathf.Abs(currentSize.y - screenSize.y) > 10f)
            {
                SetupCorrectAspectRatio();
            }
        }

        // Отладка: определяем класс по клику
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            StartCoroutine(GetClassAtScreenPositionCoroutine(Input.mousePosition));
        }

        // Применяем режимы качества
        ApplyQualityModes();

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
            // Модель TopFormer-S всегда использует 512x512, независимо от настроек Inspector
            Debug.Log($"ℹ️ Модель TopFormer-S использует фиксированное разрешение 512x512 (настройки Inspector: {processingResolution.x}x{processingResolution.y})");

            worker = new Worker(runtimeModel, BackendType.GPUCompute);
            Debug.Log("✅ Worker создан с GPUCompute backend");

            // Автоматическая загрузка шейдеров
            if (upsampleShader == null)
            {
                upsampleShader = Resources.Load<ComputeShader>("BilinearUpsample");
                if (upsampleShader == null)
                {
                    upsampleShader = Resources.Load<ComputeShader>("Shaders/BilinearUpsample");
                }

                if (upsampleShader != null)
                {
                    Debug.Log("✅ Автоматически загружен BilinearUpsample.compute");
                }
                else
                {
                    Debug.LogWarning("⚠️ Не удалось загрузить BilinearUpsample.compute");
                }
            }

            // Автоматическая загрузка улучшенного шейдера постобработки
            if (advancedPostProcessingShader == null)
            {
                advancedPostProcessingShader = Resources.Load<ComputeShader>("AdvancedMaskPostProcessing");
                if (advancedPostProcessingShader == null)
                {
                    advancedPostProcessingShader = Resources.Load<ComputeShader>("Shaders/AdvancedMaskPostProcessing");
                }

                if (advancedPostProcessingShader != null)
                {
                    Debug.Log("✅ Автоматически загружен AdvancedMaskPostProcessing.compute");
                }
                else
                {
                    Debug.LogWarning("⚠️ Не удалось загрузить AdvancedMaskPostProcessing.compute - будет использоваться обычное сглаживание");
                }
            }

            // Создаем текстуры с максимальным разрешением (будем изменять размер динамически)
            int maxRes = processingResolution.x;
            cameraInputTexture = CreateRenderTexture(maxRes, maxRes, RenderTextureFormat.ARGB32);
            normalizedTexture = CreateRenderTexture(maxRes, maxRes, RenderTextureFormat.ARGBFloat);

            // ИСПРАВЛЕНИЕ: Всегда создаем displayMaterialInstance для crop параметров
            if (visualizationMaterial != null)
            {
                displayMaterialInstance = new Material(visualizationMaterial);
                Debug.Log($"✅ displayMaterialInstance создан: {displayMaterialInstance.shader.name}");
            }
            else
            {
                Debug.LogError("❌ visualizationMaterial не назначен! displayMaterialInstance не может быть создан.");
            }

            if (enableLegacyDisplay && segmentationDisplay != null && displayMaterialInstance != null)
            {
                segmentationDisplay.material = displayMaterialInstance;
                UpdateMaterialParameters();
                Debug.Log($"✅ Legacy отображение настроено");

                // Настраиваем правильное соотношение сторон для телефона
                SetupCorrectAspectRatio();
            }
            else if (!enableLegacyDisplay)
            {
                // Отключаем старую систему отображения
                if (segmentationDisplay != null)
                {
                    segmentationDisplay.gameObject.SetActive(false);
                    Debug.Log("🚫 Старая система отображения отключена - используется новая проекционная система");
                }
            }

            Debug.Log("🎉 AsyncSegmentationManager инициализация завершена успешно!");

            // Отправляем Flutter уведомление о готовности Unity
            Invoke(nameof(NotifyFlutterReady), 2f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка инициализации AsyncSegmentationManager: {e.Message}\n{e.StackTrace}");
        }

        // Используем настройки из инспектора вместо принудительного режима
        // ForceWallOnlyMode(); // ОТКЛЮЧЕНО

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
        Debug.Log("✅ УЛУЧШЕНИЯ: увеличено разрешение до 1024x1024, уменьшено сглаживание для точности");

        // 🎯 ОПТИМИЗАЦИЯ: минимальное сглаживание для сохранения деталей
        enableMaskSmoothing = true;
        maskSmoothingIterations = 2; // Минимальное значение для сохранения точности
        Debug.Log($"🎯 ОПТИМИЗИРОВАНО сглаживание: {maskSmoothingIterations} итерации для максимальной точности");

        // Обновляем материал с новыми настройками
        UpdateMaterialParameters();
    }

    private void SetupCorrectAspectRatio()
    {
        if (segmentationDisplay == null) return;

        // Убираем AspectRatioFitter и принудительно растягиваем на весь экран
        var fitter = segmentationDisplay.GetComponent<AspectRatioFitter>();
        if (fitter != null)
        {
            DestroyImmediate(fitter);
            Debug.Log("🗑️ AspectRatioFitter удален для полноэкранного отображения");
        }

        var rectTransform = segmentationDisplay.rectTransform;

        // ПОЛНОЭКРАННОЕ РАСТЯГИВАНИЕ
        rectTransform.anchorMin = Vector2.zero;      // Левый нижний угол
        rectTransform.anchorMax = Vector2.one;       // Правый верхний угол  
        rectTransform.offsetMin = Vector2.zero;      // Убираем отступы
        rectTransform.offsetMax = Vector2.zero;      // Убираем отступы
        rectTransform.localScale = Vector3.one;      // Нормальный масштаб
        rectTransform.localPosition = Vector3.zero;  // Центрируем

        // Убеждаемся что Canvas Renderer включен
        var canvasRenderer = segmentationDisplay.GetComponent<CanvasRenderer>();
        if (canvasRenderer != null)
        {
            canvasRenderer.cull = false; // Отключаем culling
        }

        // Устанавливаем RawImage на полный экран
        segmentationDisplay.uvRect = new Rect(0, 0, 1, 1); // Полная UV область

        // Debug.Log($"📱 Маска растянута на весь экран: {rectTransform.rect.width}x{rectTransform.rect.height}"); // Отключено - слишком много спама
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
            // Используем фактические размеры текстуры с сохранением пропорций
            inputTensor = TextureConverter.ToTensor(normalizedTexture, normalizedTexture.width, normalizedTexture.height, 3);

            // Debug.Log($"🔢 Создан тензор: {normalizedTexture.width}x{normalizedTexture.height}x3 (аспект: {(float)normalizedTexture.width / normalizedTexture.height:F2})"); // Отключено - спам

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

        // ИСПРАВЛЕНИЕ: Улучшенная нормализация для модели сегментации
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

        // Проверяем соответствие размеров - модель должна выдавать квадратный результат
        if (width != height)
        {
            Debug.LogError($"❌ Тензор не квадратный: {width}x{height}! Модель требует квадратные входные данные.");
            return;
        }

        // Debug.Log($"✅ Обрабатываем квадратный тензор: {width}x{height}"); // Отключено - спам

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

        // --- ЭТАП 2: УВЕЛИЧЕНИЕ РАЗРЕШЕНИЯ МАСКИ ---
        int upsampleWidth = cameraInputTexture.width;
        int upsampleHeight = cameraInputTexture.height;

        if (upsampledMaskTexture == null || upsampledMaskTexture.width != upsampleWidth || upsampledMaskTexture.height != upsampleHeight)
        {
            ReleaseRenderTexture(upsampledMaskTexture);
            upsampledMaskTexture = CreateRenderTexture(upsampleWidth, upsampleHeight, RenderTextureFormat.RFloat);
        }

        if (upsampleShader != null)
        {
            int upsampleKernel = upsampleShader.FindKernel("BilinearUpsample");
            cmd.SetComputeVectorParam(upsampleShader, "InputOutputScale", new Vector4(width, height, upsampleWidth, upsampleHeight));
            cmd.SetComputeTextureParam(upsampleShader, upsampleKernel, "InputMask", segmentationMaskTexture);
            cmd.SetComputeTextureParam(upsampleShader, upsampleKernel, "OutputMask", upsampledMaskTexture);

            int upsampleThreadGroupsX = Mathf.CeilToInt(upsampleWidth / 8.0f);
            int upsampleThreadGroupsY = Mathf.CeilToInt(upsampleHeight / 8.0f);
            cmd.DispatchCompute(upsampleShader, upsampleKernel, upsampleThreadGroupsX, upsampleThreadGroupsY, 1);

            // Debug.Log($"📈 Маска увеличена с {width}x{height} (модель выход) до {upsampleWidth}x{upsampleHeight} (камера разрешение) - соотношение {(float)upsampleWidth / width:F1}x"); // Отключено - спам
        }
        else
        {
            Debug.LogWarning("Upsample Shader не назначен! Пропускаем этап увеличения разрешения.");
            Graphics.Blit(segmentationMaskTexture, upsampledMaskTexture); // Просто копируем, если шейдера нет
        }

        RenderTexture finalMask = upsampledMaskTexture; // Теперь начинаем постобработку с увеличенной маски

        // --- ЭТАП 3: УЛУЧШЕННАЯ ПОСТОБРАБОТКА МАСКИ ---
        if (enableMaskSmoothing && maskSmoothingIterations > 0)
        {
            // Настраиваем текстуры для постобработки
            ReleaseRenderTexture(smoothedMaskTexture);
            smoothedMaskTexture = CreateRenderTexture(upsampleWidth, upsampleHeight, RenderTextureFormat.RFloat);
            ReleaseRenderTexture(pingPongMaskTexture);
            pingPongMaskTexture = CreateRenderTexture(upsampleWidth, upsampleHeight, RenderTextureFormat.RFloat);

            RenderTexture source = upsampledMaskTexture;
            RenderTexture destination = smoothedMaskTexture;

            if (useAdvancedPostProcessing && advancedPostProcessingShader != null)
            {
                // Улучшенная постобработка с сохранением краёв
                int edgeAwareKernel = advancedPostProcessingShader.FindKernel("EdgeAwareSmoothing");
                int contrastKernel = advancedPostProcessingShader.FindKernel("ContrastEnhancement");

                cmd.SetComputeIntParam(advancedPostProcessingShader, "width", upsampleWidth);
                cmd.SetComputeIntParam(advancedPostProcessingShader, "height", upsampleHeight);
                cmd.SetComputeFloatParam(advancedPostProcessingShader, "edgeThreshold", edgeThreshold);
                cmd.SetComputeFloatParam(advancedPostProcessingShader, "contrastFactor", contrastFactor);

                int advancedThreadGroupsX = Mathf.CeilToInt(upsampleWidth / 8.0f);
                int advancedThreadGroupsY = Mathf.CeilToInt(upsampleHeight / 8.0f);

                // Применяем адаптивное сглаживание
                for (int i = 0; i < maskSmoothingIterations; i++)
                {
                    cmd.SetComputeTextureParam(advancedPostProcessingShader, edgeAwareKernel, "InputMask", source);
                    cmd.SetComputeTextureParam(advancedPostProcessingShader, edgeAwareKernel, "ResultMask", destination);
                    cmd.DispatchCompute(advancedPostProcessingShader, edgeAwareKernel, advancedThreadGroupsX, advancedThreadGroupsY, 1);

                    // Пинг-понг
                    var temp = source;
                    source = destination;
                    destination = (source == smoothedMaskTexture) ? pingPongMaskTexture : smoothedMaskTexture;
                }

                // Применяем усиление контраста для чётких границ
                cmd.SetComputeTextureParam(advancedPostProcessingShader, contrastKernel, "InputMask", source);
                cmd.SetComputeTextureParam(advancedPostProcessingShader, contrastKernel, "ResultMask", destination);
                cmd.DispatchCompute(advancedPostProcessingShader, contrastKernel, advancedThreadGroupsX, advancedThreadGroupsY, 1);

                finalMask = destination;
                Debug.Log($"🎯 Применена улучшенная постобработка: {maskSmoothingIterations} сглаживаний + усиление контраста");
            }
            else if (maskPostProcessingShader != null)
            {
                // Обычное медианное сглаживание
                int postProcessingKernel = maskPostProcessingShader.FindKernel("MedianFilter");
                cmd.SetComputeIntParam(maskPostProcessingShader, "width", upsampleWidth);
                cmd.SetComputeIntParam(maskPostProcessingShader, "height", upsampleHeight);

                for (int i = 0; i < maskSmoothingIterations; i++)
                {
                    cmd.SetComputeTextureParam(maskPostProcessingShader, postProcessingKernel, "InputMask", source);
                    cmd.SetComputeTextureParam(maskPostProcessingShader, postProcessingKernel, "ResultMask", destination);
                    int smoothThreadGroupsX = Mathf.CeilToInt(upsampleWidth / 8.0f);
                    int smoothThreadGroupsY = Mathf.CeilToInt(upsampleHeight / 8.0f);
                    cmd.DispatchCompute(maskPostProcessingShader, postProcessingKernel, smoothThreadGroupsX, smoothThreadGroupsY, 1);

                    var temp = source;
                    source = destination;
                    destination = (source == smoothedMaskTexture) ? pingPongMaskTexture : smoothedMaskTexture;
                }
                finalMask = source;
                Debug.Log($"🎯 Применено обычное сглаживание: {maskSmoothingIterations} итераций");
            }
        }

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Dispose();

        // DEBUG: Анализируем выходные данные модели для диагностики
        StartCoroutine(DebugModelOutput(segmentationMaskTexture));

        // Передаем маску в ARWallPresenter для фотореалистичной окраски
        if (arWallPresenter != null)
        {
            // OPTIMIZATION: Используем оптимизированную маску если доступна
            var maskToSend = OptimizeMaskIfNeeded(finalMask);
            arWallPresenter.SetSegmentationMask(maskToSend);

            // ИСПРАВЛЕНИЕ: Также передаем crop параметры в ARWallPresenter
            if (displayMaterialInstance != null)
            {
                float cropOffsetX = displayMaterialInstance.GetFloat("_CropOffsetX");
                float cropOffsetY = displayMaterialInstance.GetFloat("_CropOffsetY");
                float cropScale = displayMaterialInstance.GetFloat("_CropScale");
                arWallPresenter.SetCropParameters(cropOffsetX, cropOffsetY, cropScale);
            }
            Debug.Log("🎨 Маска и crop параметры переданы в ARWallPresenter");
        }
        else
        {
            // АВТОПОИСК: Если не назначен, пытаемся найти автоматически
            if (arWallPresenter == null)
            {
                arWallPresenter = FindObjectOfType<ARWallPresenter>();
                if (arWallPresenter != null)
                {
                    Debug.Log("✅ ARWallPresenter найден автоматически!");
                    // Повторяем передачу маски
                    var maskToSend = OptimizeMaskIfNeeded(finalMask);
                    arWallPresenter.SetSegmentationMask(maskToSend);

                    if (displayMaterialInstance != null)
                    {
                        float cropOffsetX = displayMaterialInstance.GetFloat("_CropOffsetX");
                        float cropOffsetY = displayMaterialInstance.GetFloat("_CropOffsetY");
                        float cropScale = displayMaterialInstance.GetFloat("_CropScale");
                        arWallPresenter.SetCropParameters(cropOffsetX, cropOffsetY, cropScale);
                    }
                    Debug.Log("🎨 Маска передана в автоматически найденный ARWallPresenter");
                }
                else
                {
                    Debug.LogWarning("⚠️ ARWallPresenter не назначен и не найден в сцене! Назначьте в инспекторе AsyncSegmentationManager.");
                }
            }
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
        // УПРОЩЕНИЕ: Убираем трансформацию в коде, используем только поворот в шейдере
        var transformation = XRCpuImage.Transformation.None;

        // УЛУЧШЕНИЕ: Адаптивное разрешение на основе качества устройства
        int targetResolution = Mathf.Min(processingResolution.x, Mathf.Min(cpuImage.width, cpuImage.height));

        if (enableAdaptiveResolution)
        {
            int maxDeviceResolution = GetOptimalResolutionForDevice();
            targetResolution = Mathf.Min(targetResolution, maxDeviceResolution);
            if (enableDebugLogging)
            {
                Debug.Log($"🎯 Адаптивное разрешение включено: {targetResolution}x{targetResolution} (устройство поддерживает до {maxDeviceResolution}x{maxDeviceResolution})");
            }
        }
        else
        {
            // Debug.Log($"🔒 Адаптивное разрешение отключено: используем фиксированное {targetResolution}x{targetResolution}"); // Отключено - спам
        }

        // ИСПРАВЛЕНИЕ: Модель требует квадратные данные, но мы сохраним аспект камеры для правильного отображения
        float cameraAspect = (float)cpuImage.width / cpuImage.height;

        // ИСПРАВЛЕНИЕ: TopFormer-S требует КВАДРАТНЫЕ данные - используем crop/pad подход
        int maxResolution = 512; // Безопасное значение для GPU
        int outputWidth = maxResolution;
        int outputHeight = maxResolution;

        // Но сохраняем информацию об аспекте для корректного отображения маски
        Debug.Log($"🔲 Принудительно квадратный вход: {outputWidth}x{outputHeight} для модели (камера: {cameraAspect:F2})");

        // Debug.Log($"🔲 ПРИНУДИТЕЛЬНО устанавливаем размер входа модели: {outputWidth}x{outputHeight} (аспект камеры: {cameraAspect:F2})"); // Отключено - спам

        // ДИАГНОСТИКА: Проверяем размеры камеры
        Debug.Log($"🔍 ДИАГНОСТИКА камеры: width={cpuImage.width}, height={cpuImage.height}");

        // ИСПРАВЛЕНИЕ CROP: Принудительно центрируем квадратный crop
        int inputSize = Mathf.Min(cpuImage.width, cpuImage.height);
        int cropX = (cpuImage.width - inputSize) / 2;
        int cropY = (cpuImage.height - inputSize) / 2;

        // ИСПРАВЛЕНИЕ: cropY=0 правильно для ландшафтной камеры
        // Проблема в том, что камера 1920x1440 (ландшафт), а экран 1170x2532 (портрет)
        Debug.Log($"🔍 КАМЕРА vs ЭКРАН: камера={cpuImage.width}x{cpuImage.height} (соотношение {(float)cpuImage.width / cpuImage.height:F2}), экран=1170x2532 (соотношение 0.46)");

        Debug.Log($"🔍 CROP РАСЧЕТ: inputSize={inputSize}, cropX={cropX}, cropY={cropY}");

        conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(cropX, cropY, inputSize, inputSize), // Квадратный crop
            outputDimensions = new Vector2Int(outputWidth, outputHeight),
            outputFormat = TextureFormat.RGBA32,
            transformation = transformation
        };

        Debug.Log($"📐 Квадратный crop: {cropX},{cropY} размер {inputSize}x{inputSize} → {outputWidth}x{outputHeight}");

        // ИСПРАВЛЕНИЕ: Сохраняем информацию о crop для правильного отображения
        float cropOffsetX = (float)cropX / cpuImage.width;
        float cropOffsetY = (float)cropY / cpuImage.height;
        float cropScale = (float)inputSize / Mathf.Max(cpuImage.width, cpuImage.height);

        // АГРЕССИВНАЯ КОРРЕКЦИЯ: Исправляем смещение вправо
        float originalOffsetX = cropOffsetX;
        float originalOffsetY = cropOffsetY;
        // cropOffsetX += 0.05f; // ОТКЛЮЧЕНО: сдвигаем маску ВПРАВО 
        // cropOffsetY += 0.12f; // ОТКЛЮЧЕНО: сильно опускаем маску ВНИЗ (было 0.08f)

        // ПРИНУДИТЕЛЬНОЕ логирование для диагностики
        Debug.Log($"🔧 АГРЕССИВНАЯ КОРРЕКЦИЯ crop: X {originalOffsetX:F3}→{cropOffsetX:F3}, Y {originalOffsetY:F3}→{cropOffsetY:F3}");
        Debug.Log($"🔍 ПРОВЕРКА ПРИМЕНЕНИЯ: передаем в шейдер cropOffsetX={cropOffsetX:F3}, cropOffsetY={cropOffsetY:F3}, cropScale={cropScale:F3}");

        // Передаем crop параметры в материал для корректного UV mapping
        if (displayMaterialInstance != null)
        {
            displayMaterialInstance.SetFloat("_CropOffsetX", cropOffsetX);
            displayMaterialInstance.SetFloat("_CropOffsetY", cropOffsetY);
            displayMaterialInstance.SetFloat("_CropScale", cropScale);
        }

        if (cameraInputTexture.width != outputWidth || cameraInputTexture.height != outputHeight)
        {
            ReleaseRenderTexture(cameraInputTexture);
            ReleaseRenderTexture(normalizedTexture);
            cameraInputTexture = CreateRenderTexture(outputWidth, outputHeight, RenderTextureFormat.ARGB32);
            normalizedTexture = CreateRenderTexture(outputWidth, outputHeight, RenderTextureFormat.ARGBFloat);
            Debug.Log($"📐 Текстуры пересозданы: {outputWidth}x{outputHeight} (аспект камеры: {cameraAspect:F2})");
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
                conversionRequest.conversionParams.outputFormat, false);
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
            filterMode = FilterMode.Bilinear, // Билинейная фильтрация для сглаживания
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1, // Отключаем MSAA для экономии памяти на высоком разрешении
            useMipMap = false, // Отключаем мип-мапы для маски
            autoGenerateMips = false,
            // Улучшения для качества
            name = $"SegmentationRT_{width}x{height}_{format}",
            hideFlags = HideFlags.DontSave
        };
        rt.Create();

        if (!rt.IsCreated())
        {
            Debug.LogError($"❌ Не удалось создать RenderTexture {width}x{height} формата {format}!");
        }

        return rt;
    }

    private void ReleaseRenderTexture(RenderTexture rt)
    {
        if (rt != null)
        {
            rt.Release();
            if (Application.isPlaying)
            {
                Destroy(rt);
            }
            else
            {
                DestroyImmediate(rt);
            }
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

        // ИСПРАВЛЕНИЕ ПОВОРОТА: UV координаты с учётом правильной трансформации
        Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

        // Применяем тот же поворот что и в шейдере: поворот на +90 градусов
        float uv_x = 1.0f - screenUV.y;
        float uv_y = screenUV.x;

        Debug.Log($"🎯 Клик: экран={screenPos}, screenUV=({screenUV.x:F3}, {screenUV.y:F3}), finalUV=({uv_x:F3}, {uv_y:F3}) [Y-позиция: {(screenUV.y > 0.7f ? "ВЕРХ (потолок)" : "НИЗ/СЕРЕДИНА (стены)")}]");

        // Каждые 10 кликов показываем статистику классов
        if (Time.frameCount % 600 == 0) // Раз в 10 секунд
        {
            StartCoroutine(ShowClassStatistics());
        }

        int textureX = (int)(uv_x * segmentationMaskTexture.width);
        int textureY = (int)(uv_y * segmentationMaskTexture.height);

        int index = textureY * segmentationMaskTexture.width + textureX;

        if (index >= 0 && index < data.Length)
        {
            float classIndexFloat = data[index];
            int originalClassIndex = Mathf.RoundToInt(classIndexFloat);

            // ИСПРАВЛЕНИЕ: Умная коррекция классов на основе позиции
            int correctedClassIndex = CorrectClassBasedOnPosition(originalClassIndex, screenUV);

            string originalClassName = classNames.ContainsKey(originalClassIndex) ? classNames[originalClassIndex] : "Unknown";
            string correctedClassName = classNames.ContainsKey(correctedClassIndex) ? classNames[correctedClassIndex] : "Unknown";

            if (originalClassIndex != correctedClassIndex)
            {
                Debug.Log($"🔄 Коррекция класса: {originalClassName} (ID: {originalClassIndex}) → {correctedClassName} (ID: {correctedClassIndex}) на позиции Y={screenUV.y:F2}");
            }

            Debug.Log($"👇 Класс в точке клика: {correctedClassName} (ID: {correctedClassIndex})");
            int classIndex = correctedClassIndex;

            // Отправляем информацию о кликнутом классе во Flutter
            var clickData = new FlutterClassInfo
            {
                classId = classIndex,
                className = correctedClassName,
                currentColor = customClassColors.ContainsKey(classIndex) ?
                    ColorToHex(customClassColors[classIndex]) : "#808080"
            };

            string jsonData = JsonUtility.ToJson(clickData);
            SendMessageToFlutter("onClassClicked", jsonData);

            Debug.Log($"📱→👆 Flutter: Отправлена информация о клике по классу {correctedClassName}");
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНИЕ: Корректирует классы на основе их позиции на экране
    /// </summary>
    private int CorrectClassBasedOnPosition(int originalClass, Vector2 screenUV)
    {
        // Если это потолок (5) или стена (0), применяем логическую коррекцию
        if (originalClass == 0 || originalClass == 5)
        {
            // Верхняя часть экрана (Y > 0.7) - скорее всего потолок  
            if (screenUV.y > 0.7f)
            {
                return 5; // ceiling
            }
            // Средняя и нижняя часть экрана (Y < 0.7) - скорее всего стены
            else if (screenUV.y < 0.7f)
            {
                return 0; // wall
            }
        }

        // Для всех остальных классов возвращаем как есть
        return originalClass;
    }

    /// <summary>
    /// Получает следующий цвет из массива интерактивных цветов
    /// </summary>
    private Color GetNextInteractiveColor()
    {
        if (interactiveColors == null || interactiveColors.Length == 0)
        {
            return Color.white;
        }

        Color color = interactiveColors[currentColorIndex];
        currentColorIndex = (currentColorIndex + 1) % interactiveColors.Length;
        return color;
    }

    /// <summary>
    /// Конвертирует цвет в hex строку для красивого логирования
    /// </summary>
    private string ColorToHex(Color color)
    {
        return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
    }

    /// <summary>
    /// Получает цвет для класса (с учетом пользовательских изменений)
    /// </summary>
    public Color GetClassColor(int classId)
    {
        if (customClassColors.ContainsKey(classId))
        {
            return customClassColors[classId];
        }

        // Возвращаем стандартный цвет (можно добавить логику для стандартных цветов)
        return paintColor;
    }

    /// <summary>
    /// Сброс всех пользовательских цветов
    /// </summary>
    [ContextMenu("Сбросить пользовательские цвета")]
    public void ResetCustomColors()
    {
        customClassColors.Clear();
        currentColorIndex = 0;
        Debug.Log("🔄 Все пользовательские цвета сброшены");
    }

    /// <summary>
    /// Вернуться к режиму отображения всех классов
    /// </summary>
    [ContextMenu("Показать все классы")]
    public void ShowAllClasses()
    {
        showAllClasses = true;
        showWalls = false;
        showFloors = false;
        showCeilings = false;

        Debug.Log("🌈 Включен режим отображения всех классов");
    }

    #region Flutter Integration - Методы для приема команд от Flutter

    /// <summary>
    /// [FLUTTER] Устанавливает цвет для конкретного класса по команде от Flutter
    /// </summary>
    /// <param name="message">JSON строка: {"classId": 0, "color": "#FF0000"}</param>
    public void SetClassColorFromFlutter(string message)
    {
        try
        {
            var data = JsonUtility.FromJson<FlutterColorCommand>(message);
            Color color = HexToColor(data.color);

            customClassColors[data.classId] = color;

            string className = classNames.ContainsKey(data.classId) ? classNames[data.classId] : "Unknown";
            Debug.Log($"📱→🎨 Flutter: Установлен цвет {data.color} для класса {className} (ID: {data.classId})");

            // Обновляем ARWallPresenter
            if (arWallPresenter != null)
            {
                arWallPresenter.SetClassColor(data.classId, color);
            }

            // Переключаемся в режим отображения выбранного класса
            showAllClasses = false;
            selectedClass = data.classId;
            paintColor = color;

            // Отправляем подтверждение обратно во Flutter
            SendMessageToFlutter("onColorChanged", $"{{\"classId\": {data.classId}, \"color\": \"{data.color}\", \"className\": \"{className}\"}}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка обработки команды цвета от Flutter: {e.Message}");
        }
    }

    /// <summary>
    /// [FLUTTER] Получает список доступных классов в текущей сцене
    /// </summary>
    public void GetAvailableClassesFromFlutter(string message = "")
    {
        try
        {
            // Собираем все обнаруженные классы
            var availableClasses = new System.Collections.Generic.List<FlutterClassInfo>();

            // Проверяем последнюю обработанную маску
            if (segmentationMaskTexture != null)
            {
                var detectedClasses = GetDetectedClassesInCurrentFrame();
                foreach (var classId in detectedClasses)
                {
                    string className = classNames.ContainsKey(classId) ? classNames[classId] : "Unknown";
                    string currentColor = customClassColors.ContainsKey(classId) ?
                        ColorToHex(customClassColors[classId]) : "#808080"; // серый по умолчанию

                    availableClasses.Add(new FlutterClassInfo
                    {
                        classId = classId,
                        className = className,
                        currentColor = currentColor
                    });
                }
            }

            var response = new FlutterClassListResponse { classes = availableClasses.ToArray() };
            string jsonResponse = JsonUtility.ToJson(response);

            Debug.Log($"📱→📋 Отправляем Flutter список классов: {availableClasses.Count} классов");
            SendMessageToFlutter("onAvailableClasses", jsonResponse);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка получения списка классов для Flutter: {e.Message}");
        }
    }

    /// <summary>
    /// [FLUTTER] Сброс всех пользовательских цветов по команде от Flutter
    /// </summary>
    public void ResetColorsFromFlutter(string message = "")
    {
        ResetCustomColors();
        showAllClasses = true;

        Debug.Log("📱→🔄 Flutter: Все цвета сброшены, включен режим всех классов");
        SendMessageToFlutter("onColorsReset", "{\"status\": \"success\"}");
    }

    /// <summary>
    /// [FLUTTER] Переключение в режим отображения всех классов
    /// </summary>
    public void ShowAllClassesFromFlutter(string message = "")
    {
        ShowAllClasses();
        SendMessageToFlutter("onModeChanged", "{\"mode\": \"all_classes\"}");
    }

    #endregion

    #region Helper Methods для Flutter интеграции

    /// <summary>
    /// Отправляет сообщение во Flutter через FlutterUnityManager
    /// </summary>
    private void SendMessageToFlutter(string method, string data)
    {
        var flutterManager = FindObjectOfType<FlutterUnityManager>();
        if (flutterManager != null)
        {
            flutterManager.SendMessage(method, data);
        }
        else
        {
            Debug.LogWarning("⚠️ FlutterUnityManager не найден для отправки сообщения во Flutter");
        }
    }

    /// <summary>
    /// Конвертирует hex строку в Unity Color
    /// </summary>
    private Color HexToColor(string hex)
    {
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        if (hex.Length == 6)
        {
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }

        Debug.LogWarning($"⚠️ Неверный формат цвета: {hex}");
        return Color.white;
    }

    /// <summary>
    /// Получает список классов, обнаруженных в текущем кадре
    /// </summary>
    private System.Collections.Generic.HashSet<int> GetDetectedClassesInCurrentFrame()
    {
        var detectedClasses = new System.Collections.Generic.HashSet<int>();

        if (segmentationMaskTexture == null) return detectedClasses;

        // Читаем данные из текстуры маски (упрощенная версия)
        RenderTexture.active = segmentationMaskTexture;
        Texture2D tempTexture = new Texture2D(segmentationMaskTexture.width, segmentationMaskTexture.height, TextureFormat.RFloat, false);
        tempTexture.ReadPixels(new Rect(0, 0, segmentationMaskTexture.width, segmentationMaskTexture.height), 0, 0);
        tempTexture.Apply();

        Color[] pixels = tempTexture.GetPixels();
        for (int i = 0; i < pixels.Length; i += 100) // Проверяем каждый 100-й пиксель для оптимизации
        {
            int classId = Mathf.RoundToInt(pixels[i].r * 255);
            if (classId > 0 && classId < 150) // Только валидные классы ADE20K
            {
                detectedClasses.Add(classId);
            }
        }

        DestroyImmediate(tempTexture);
        RenderTexture.active = null;

        return detectedClasses;
    }

    /// <summary>
    /// Уведомляет Flutter о готовности Unity и отправляет начальный список классов
    /// </summary>
    private void NotifyFlutterReady()
    {
        Debug.Log("📱→✅ Уведомляем Flutter о готовности Unity");
        SendMessageToFlutter("onUnityReady", "{\"status\": \"ready\"}");

        // Через секунду отправляем список доступных классов
        Invoke(nameof(SendInitialClassList), 1f);
    }

    /// <summary>
    /// Отправляет первоначальный список классов во Flutter
    /// </summary>
    private void SendInitialClassList()
    {
        GetAvailableClassesFromFlutter();
    }

    #endregion

    #region JSON Data Classes для Flutter интеграции

    [System.Serializable]
    public class FlutterColorCommand
    {
        public int classId;
        public string color; // Hex формат, например "#FF0000"
    }

    [System.Serializable]
    public class FlutterClassInfo
    {
        public int classId;
        public string className;
        public string currentColor; // Текущий цвет в hex формате
    }

    [System.Serializable]
    public class FlutterClassListResponse
    {
        public FlutterClassInfo[] classes;
    }

    #endregion

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

        // Приоритет настройкам showAllClasses
        if (showAllClasses)
        {
            classToShow = -1;
            Debug.Log($"🌈 Режим: ВСЕ КЛАССЫ (showAllClasses={showAllClasses}, showWalls={showWalls})");
        }
        else if (showWalls)
        {
            classToShow = 0;  // СТЕНЫ
            Debug.Log("🧱 Режим: ТОЛЬКО СТЕНЫ (класс 0)");
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
            // По умолчанию показываем все классы
            classToShow = -1;
            showAllClasses = true;
            Debug.Log("🌈 Режим по умолчанию: ВСЕ КЛАССЫ");
        }

        displayMaterialInstance.SetInt("_SelectedClass", classToShow);
        displayMaterialInstance.SetFloat("_Opacity", visualizationOpacity);
        displayMaterialInstance.SetColor("_PaintColor", paintColor);
        displayMaterialInstance.SetInt("_RotationMode", maskRotationMode);

        // Параметры полноэкранного отображения  
        displayMaterialInstance.SetInt("_ForceFullscreen", forceFullscreenMask && useCameraAspectRatio ? 1 : 0);

        // Вычисляем и передаем соотношения сторон
        if (segmentationMaskTexture != null)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            float maskAspect = (float)segmentationMaskTexture.width / segmentationMaskTexture.height;

            // Учитываем поворот маски при вычислении аспекта
            // Если поворот на 90 или -90 градусов, меняем местами ширину и высоту
            if (maskRotationMode == 0 || maskRotationMode == 1)
            {
                maskAspect = 1.0f / maskAspect; // Инвертируем аспект для поворота на 90°
            }

            float aspectRatio = screenAspect / maskAspect;

            displayMaterialInstance.SetFloat("_ScreenAspect", screenAspect);
            displayMaterialInstance.SetFloat("_MaskAspect", maskAspect);
            displayMaterialInstance.SetFloat("_AspectRatio", aspectRatio);

            // Отладочная информация
            if (Time.frameCount % 60 == 0) // Логируем раз в секунду
            {
                Debug.Log($"📱 Screen: {Screen.width}x{Screen.height} (aspect: {screenAspect:F2})");
                Debug.Log($"🎭 Mask: {segmentationMaskTexture.width}x{segmentationMaskTexture.height} (aspect: {maskAspect:F2} after rotation)");
                Debug.Log($"📐 Rotation mode: {maskRotationMode}, Force fullscreen: {forceFullscreenMask}");
            }
        }

        Debug.Log($"✅ МАТЕРИАЛ ОБНОВЛЕН: _SelectedClass={classToShow}, _Opacity={visualizationOpacity}");
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

    /// <summary>
    /// Возвращает текущий режим поворота маски для использования в других компонентах
    /// </summary>
    public int GetMaskRotationMode()
    {
        return maskRotationMode;
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
        UpdateMaterialParameters();
        Debug.Log("🔄 Покрытие экрана принудительно обновлено");
    }

    /// <summary>
    /// Включить/выключить полноэкранный режим маски
    /// </summary>
    public void SetFullscreenMode(bool enabled)
    {
        forceFullscreenMask = enabled;
        if (enabled)
        {
            SetupCorrectAspectRatio();
        }
        UpdateMaterialParameters();
        Debug.Log($"📱 Полноэкранный режим маски: {(enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
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

    /// <summary>
    /// Тестирование разных режимов поворота маски
    /// </summary>
    [ContextMenu("Тест: Следующий режим поворота")]
    public void TestNextRotationMode()
    {
        maskRotationMode = (maskRotationMode + 1) % 4;
        string[] modeNames = { "+90°", "-90°", "180°", "Без поворота" };
        Debug.Log($"🔄 Режим поворота изменен на: {maskRotationMode} ({modeNames[maskRotationMode]})");
        UpdateMaterialParameters();
    }

    /// <summary>
    /// Переключение режима соотношения сторон
    /// </summary>
    [ContextMenu("Тест: Переключить режим аспекта")]
    public void ToggleCameraAspectRatio()
    {
        useCameraAspectRatio = !useCameraAspectRatio;
        UpdateMaterialParameters();
        Debug.Log($"📐 Коррекция аспекта: {(useCameraAspectRatio ? "ВКЛЮЧЕНА (полноэкранный режим)" : "ВЫКЛЮЧЕНА (пропорциональный режим)")}");
    }

    /// <summary>
    /// Переключение адаптивного разрешения
    /// </summary>
    [ContextMenu("Тест: Переключить адаптивное разрешение")]
    public void ToggleAdaptiveResolution()
    {
        enableAdaptiveResolution = !enableAdaptiveResolution;
        Debug.Log($"🎯 Адаптивное разрешение: {(enableAdaptiveResolution ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО - используется фиксированное разрешение")}");
    }

    /// <summary>
    /// Форсированное максимальное качество для лучшей точности сегментации
    /// </summary>
    [ContextMenu("Тест: Максимальное качество")]
    public void ForceMaxQuality()
    {
        enableAdaptiveResolution = false;
        processingResolution = new Vector2Int(1280, 1280);
        maskSmoothingIterations = 1; // Минимальное сглаживание для максимальной детализации
        useAdvancedPostProcessing = true;
        edgeThreshold = 0.05f; // Минимальный порог для сохранения мелких деталей
        contrastFactor = 5.0f; // Усиленный контраст для чётких границ
        Debug.Log($"🚀 ФОРСИРОВАНО максимальное качество: {processingResolution.x}x{processingResolution.y}, сглаживание={maskSmoothingIterations}, улучшенная постобработка=включена");
    }

    /// <summary>
    /// Увеличить качество сглаживания
    /// </summary>
    [ContextMenu("Тест: Увеличить сглаживание")]
    public void IncreaseSmoothing()
    {
        if (maskSmoothingIterations < 15)
        {
            maskSmoothingIterations++;
            Debug.Log($"🎯 Сглаживание увеличено до {maskSmoothingIterations} итераций");
        }
    }

    /// <summary>
    /// Уменьшить качество сглаживания
    /// </summary>
    [ContextMenu("Тест: Уменьшить сглаживание")]
    public void DecreaseSmoothing()
    {
        if (maskSmoothingIterations > 1)
        {
            maskSmoothingIterations--;
            Debug.Log($"🎯 Сглаживание уменьшено до {maskSmoothingIterations} итераций");
        }
    }

    /// <summary>
    /// Переключение сглаживания маски
    /// </summary>
    [ContextMenu("Тест: Переключить сглаживание")]
    public void ToggleSmoothing()
    {
        enableMaskSmoothing = !enableMaskSmoothing;
        Debug.Log($"🎯 Сглаживание маски: {(enableMaskSmoothing ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
    }

    /// <summary>
    /// Показать статистику классов в текущем кадре
    /// </summary>
    [ContextMenu("Отладка: Показать статистику классов")]
    public void ShowClassStatisticsNow()
    {
        StartCoroutine(ShowClassStatistics());
    }

    /// <summary>
    /// Включить режим отладки соответствия маски
    /// </summary>
    [ContextMenu("Отладка: Переключить показ контуров")]
    public void ToggleClassOutlines()
    {
        showClassOutlines = !showClassOutlines;
        Debug.Log($"🎨 Показ контуров классов: {(showClassOutlines ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
    }

    /// <summary>
    /// Применяет выбранные режимы качества через Inspector
    /// </summary>
    private void ApplyQualityModes()
    {
        // Убеждаемся что активен только один режим
        int activeModesCount = (maxAccuracyMode ? 1 : 0) + (balancedMode ? 1 : 0) + (performanceMode ? 1 : 0);

        if (activeModesCount == 0)
        {
            // Если ничего не выбрано, включаем сбалансированный режим по умолчанию
            balancedMode = true;
        }
        else if (activeModesCount > 1)
        {
            // Если выбрано несколько режимов, оставляем только последний изменённый
            // Приоритет: maxAccuracy > balanced > performance
            if (maxAccuracyMode)
            {
                balancedMode = false;
                performanceMode = false;
            }
            else if (balancedMode)
            {
                performanceMode = false;
            }
        }

        // Применяем настройки в зависимости от выбранного режима
        if (maxAccuracyMode)
        {
            ApplyMaxAccuracySettings();
        }
        else if (balancedMode)
        {
            ApplyBalancedSettings();
        }
        else if (performanceMode)
        {
            ApplyPerformanceSettings();
        }
    }

    /// <summary>
    /// Настройки максимальной точности
    /// </summary>
    private void ApplyMaxAccuracySettings()
    {
        enableMaskSmoothing = false; // Отключаем сглаживание для максимальной детализации
        enableAdaptiveResolution = false;
        processingResolution = new Vector2Int(512, 512); // ИСПРАВЛЕНО: Безопасное разрешение
        useAdvancedPostProcessing = false; // Отключаем постобработку

        if (enableDebugLogging && Time.frameCount % 300 == 0) // Логируем раз в 5 секунд только если отладка включена
        {
            Debug.Log("🎯 РЕЖИМ МАКСИМАЛЬНОЙ ТОЧНОСТИ: сглаживание отключено, разрешение 512x512");
        }
    }

    /// <summary>
    /// Сбалансированные настройки качества и производительности
    /// </summary>
    private void ApplyBalancedSettings()
    {
        enableMaskSmoothing = true;
        maskSmoothingIterations = 2;
        enableAdaptiveResolution = true;
        useAdvancedPostProcessing = true;
        edgeThreshold = 0.1f;
        contrastFactor = 3.0f;

        if (enableDebugLogging && Time.frameCount % 300 == 0) // Логируем раз в 5 секунд только если отладка включена
        {
            Debug.Log("⚖️ СБАЛАНСИРОВАННЫЙ РЕЖИМ: оптимальные настройки качества и производительности");
        }
    }

    /// <summary>
    /// Настройки максимальной производительности
    /// </summary>
    private void ApplyPerformanceSettings()
    {
        enableMaskSmoothing = true;
        maskSmoothingIterations = 1;
        enableAdaptiveResolution = true;
        useAdvancedPostProcessing = false;
        processingResolution = new Vector2Int(256, 256); // ИСПРАВЛЕНО: Очень низкое разрешение для максимальной производительности

        if (enableDebugLogging && Time.frameCount % 300 == 0) // Логируем раз в 5 секунд только если отладка включена
        {
            Debug.Log("⚡ РЕЖИМ ПРОИЗВОДИТЕЛЬНОСТИ: быстрая обработка, разрешение 256x256");
        }
    }

    /// <summary>
    /// Публичные методы для программного переключения режимов (совместимость с SegmentationDebugger)
    /// </summary>
    public void EnableMaxAccuracyMode()
    {
        maxAccuracyMode = true;
        balancedMode = false;
        performanceMode = false;
        ApplyQualityModes();
    }

    public void EnableBalancedMode()
    {
        maxAccuracyMode = false;
        balancedMode = true;
        performanceMode = false;
        ApplyQualityModes();
    }

    public void EnablePerformanceMode()
    {
        maxAccuracyMode = false;
        balancedMode = false;
        performanceMode = true;
        ApplyQualityModes();
    }

    /// <summary>
    /// Показывает статистику классов в текущем кадре для отладки
    /// </summary>
    private System.Collections.IEnumerator ShowClassStatistics()
    {
        if (segmentationMaskTexture == null) yield break;

        yield return new WaitForEndOfFrame();

        RenderTexture.active = segmentationMaskTexture;
        Texture2D debugTexture = new Texture2D(segmentationMaskTexture.width, segmentationMaskTexture.height, TextureFormat.RFloat, false);
        debugTexture.ReadPixels(new Rect(0, 0, segmentationMaskTexture.width, segmentationMaskTexture.height), 0, 0);
        debugTexture.Apply();
        RenderTexture.active = null;

        var classCount = new System.Collections.Generic.Dictionary<int, int>();
        int totalPixels = 0;

        // Анализируем каждый 10-й пиксель для производительности
        for (int y = 0; y < debugTexture.height; y += 10)
        {
            for (int x = 0; x < debugTexture.width; x += 10)
            {
                Color pixel = debugTexture.GetPixel(x, y);
                int classIndex = Mathf.RoundToInt(pixel.r);

                if (!classCount.ContainsKey(classIndex))
                    classCount[classIndex] = 0;
                classCount[classIndex]++;
                totalPixels++;
            }
        }

        // Показываем топ-5 классов
        var sortedClasses = classCount.OrderByDescending(kvp => kvp.Value).Take(5);
        Debug.Log("📊 СТАТИСТИКА КЛАССОВ В КАДРЕ:");

        foreach (var kvp in sortedClasses)
        {
            float percentage = (kvp.Value / (float)totalPixels) * 100f;
            string className = classNames.ContainsKey(kvp.Key) ? classNames[kvp.Key] : "Unknown";
            Debug.Log($"  {className} (ID: {kvp.Key}): {percentage:F1}% ({kvp.Value} пикселей)");
        }

        DestroyImmediate(debugTexture);
    }

    /// <summary>
    /// Диагностический метод для анализа выходных данных модели
    /// </summary>
    private System.Collections.IEnumerator DebugModelOutput(RenderTexture maskTexture)
    {
        // Подождем кадр для завершения GPU операций
        yield return new WaitForEndOfFrame();

        // Читаем пиксели из текстуры маски для анализа
        RenderTexture.active = maskTexture;
        Texture2D debugTexture = new Texture2D(maskTexture.width, maskTexture.height, TextureFormat.RFloat, false);
        debugTexture.ReadPixels(new Rect(0, 0, maskTexture.width, maskTexture.height), 0, 0);
        debugTexture.Apply();
        RenderTexture.active = null;

        // Анализируем центральную область 64x64 пикселя
        int centerX = maskTexture.width / 2;
        int centerY = maskTexture.height / 2;
        int samples = 0;
        System.Collections.Generic.Dictionary<int, int> classCount = new System.Collections.Generic.Dictionary<int, int>();

        for (int y = centerY - 32; y < centerY + 32; y++)
        {
            for (int x = centerX - 32; x < centerX + 32; x++)
            {
                if (x >= 0 && x < maskTexture.width && y >= 0 && y < maskTexture.height)
                {
                    Color pixel = debugTexture.GetPixel(x, y);
                    // ИСПРАВЛЕНИЕ: RFloat текстура содержит индекс класса напрямую, без умножения на 255
                    int classIndex = Mathf.RoundToInt(pixel.r);

                    if (!classCount.ContainsKey(classIndex))
                        classCount[classIndex] = 0;
                    classCount[classIndex]++;
                    samples++;
                }
            }
        }

        // Выводим статистику
        foreach (var kvp in classCount)
        {
            float percentage = (kvp.Value / (float)samples) * 100f;
        }

        // Освобождаем память
        if (Application.isPlaying)
        {
            Destroy(debugTexture);
        }
        else
        {
            DestroyImmediate(debugTexture);
        }
    }

    /// <summary>
    /// Получить текущую текстуру маски сегментации для использования в других компонентах
    /// </summary>
    /// <returns>RenderTexture с результатом сегментации или null, если не готова</returns>
    public RenderTexture GetCurrentSegmentationMask()
    {
        // Возвращаем сглаженную маску, если доступна
        if (smoothedMaskTexture != null && smoothedMaskTexture.IsCreated())
        {
            return smoothedMaskTexture;
        }

        // Если сглаживание отключено, возвращаем обычную маску
        if (segmentationMaskTexture != null && segmentationMaskTexture.IsCreated())
        {
            return segmentationMaskTexture;
        }

        return null;
    }

    /// <summary>
    /// Проверить, доступна ли текстура сегментации
    /// </summary>
    /// <returns>true, если маска готова к использованию</returns>
    public bool IsSegmentationMaskReady()
    {
        return GetCurrentSegmentationMask() != null;
    }

    /// <summary>
    /// Определяет оптимальное разрешение для текущего устройства
    /// </summary>
    /// <returns>Максимальное разрешение для обработки</returns>
    private int GetOptimalResolutionForDevice()
    {
        // Определяем производительность устройства по количеству ядер, памяти и GPU
        int coreCount = SystemInfo.processorCount;
        int memoryMB = SystemInfo.systemMemorySize;
        string deviceModel = SystemInfo.deviceModel.ToLower();

        // Проверяем, является ли это современным iPhone/iPad
        bool isModernAppleDevice = deviceModel.Contains("iphone") &&
            (deviceModel.Contains("13") || deviceModel.Contains("14") || deviceModel.Contains("15") ||
             deviceModel.Contains("pro") || deviceModel.Contains("max"));

        // ИСПРАВЛЕНО: Все разрешения снижены для предотвращения превышения лимитов GPU
        // Флагманские устройства - максимальное качество
        if ((coreCount >= 8 && memoryMB >= 6000) || isModernAppleDevice)
        {
            Debug.Log($"🚀 Флагманское устройство ({deviceModel}): используем разрешение 512x512");
            return 512;
        }
        // Современные средне-высокие устройства
        else if (coreCount >= 6 && memoryMB >= 4000)
        {
            Debug.Log($"⚡ Современное устройство ({deviceModel}): используем разрешение 512x512");
            return 512;
        }
        // Средние устройства
        else if (coreCount >= 4 && memoryMB >= 3000)
        {
            Debug.Log($"📱 Среднее устройство ({deviceModel}): используем разрешение 384x384");
            return 384;
        }
        // Слабые/старые устройства
        else
        {
            Debug.Log($"🔧 Слабое устройство ({deviceModel}): используем разрешение 256x256");
            return 256;
        }
    }
}

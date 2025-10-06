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
    // [УДАЛЕНО] Legacy RawImage больше не используется - только ARWallPresenter
    [SerializeField]
    private ModelAsset modelAsset;

    [Header("Hybrid Pipeline Models (SegFormer + SAM)")]
    [Tooltip("Модель SegFormer для поиска стен (грубая маска).")]
    [SerializeField] private ModelAsset segformerModelAsset;

    [Tooltip("Модель SAM Encoder для создания эмбеддингов.")]
    [SerializeField] private ModelAsset samEncoderModelAsset;

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

    // Публичный геттер для доступа к шейдеру из других классов
    public ComputeShader GetArgmaxShader()
    {
        return argmaxShader;
    }

    private Model runtimeModel;
    private Worker worker;

    // Workers для гибридного пайплайна
    private Worker segformerWorker;
    private Worker samEncoderWorker;

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
    // edgeEnhancementFactor удален - больше не используется
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
    private bool enableDebugLogging = false; // ПРИНУДИТЕЛЬНО ОТКЛЮЧЕНО для устранения спама логов
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
    private int maskRotationMode = 3; // ИСПРАВЛЕНИЕ: TopFormer без поворота для правильной ориентации
    [Tooltip("Горизонтальное отражение маски для исправления инверсии")]
    [SerializeField]
    private bool flipHorizontal = false; // ИСПРАВЛЕНИЕ: TopFormer без отражения - убираем отзеркаливание
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
    private float visualizationOpacity = 0.6f; // Умеренная прозрачность для комфортного просмотра
    // [УДАЛЕНО] Legacy display больше не поддерживается
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

    [Header("🤖 Model Selection")]
    [Tooltip("Use TopFormer ADE20K (150 classes) instead of BiSeNet Cityscapes (19 classes)")]
    [SerializeField]
    private bool useTopFormerADE20K = true;

    [Header("🔥 SegFormer Models Support")]
    [Tooltip("Use SegFormer models instead of TopFormer/BiSeNet")]
    [SerializeField]
    private bool useSegFormerModels = false;
    [Tooltip("SegFormer model type selection")]
    [SerializeField]
    private SegFormerModelType segformerModelType = SegFormerModelType.B1_512x512;
    [Tooltip("Enable ImageNet normalization for SegFormer")]
    [SerializeField]
    private bool useImageNetNormalization = true;

    [Tooltip("Enable temporal stabilization to reduce mask jitter during camera movement")]
    [SerializeField]
    private bool enableTemporalStabilization = true;

    [Header("🎯 SAM2 Integration")]
    [Tooltip("⚠️ ОТКЛЮЧЕНО: SAM модель требует encoder+decoder архитектуру. Использует стандартные SegFormer модели.")]
    [SerializeField]
    private bool useSAM2Models = false;
    [Tooltip("SAM2 Manager component reference")]
    [SerializeField]
    private SAM2SegmentationManager sam2Manager;

    // useBilinearUpscaling перенесен в другое место - здесь больше не нужен

    [Header("🎯 Mask Alignment Testing")]
    [Tooltip("Test: 180° rotation + горизонтальный flip для TopFormer (исправляет полное отзеркаливание)")]
    [SerializeField]
    private bool testMode180NoFlip = false;

    [Tooltip("Test: No rotation + horizontal flip (альтернативный режим для TopFormer)")]
    [SerializeField]
    private bool testModeNoRotationWithFlip = false;

    [Tooltip("Test: +90° rotation (попробовать для исправления пространственного соответствия)")]
    [SerializeField]
    private bool testModePlus90 = true;

    [Tooltip("Test: -90° rotation")]
    [SerializeField]
    private bool testModeMinus90 = false;

    // Словарь пользовательских цветов для классов
    private Dictionary<int, Color> customClassColors = new Dictionary<int, Color>();
    private int currentColorIndex = 0;

    // ADE20K яркие контрастные цвета классов для лучшей видимости
    private Dictionary<int, Color> ade20kClassColors = new Dictionary<int, Color>()
    {
        {0, new Color(1.00f, 0.00f, 0.00f, 1f)},   // wall - ярко-красный
        {1, new Color(0.00f, 0.00f, 1.00f, 1f)},   // building - синий
        {2, new Color(1.00f, 1.00f, 0.00f, 1f)},   // sky - желтый
        {3, new Color(0.60f, 0.30f, 0.00f, 1f)},   // floor - коричневый
        {4, new Color(0.00f, 1.00f, 0.00f, 1f)},   // tree - зеленый
        {5, new Color(0.00f, 1.00f, 1.00f, 1f)},   // ceiling - голубой
        {12, new Color(0.24f, 0.02f, 0.59f, 1f)},  // person - фиолетовый
        {14, new Color(0.20f, 1.00f, 0.03f, 1f)},  // door - ярко-зеленый
        {15, new Color(0.32f, 0.02f, 1.00f, 1f)},  // table - синий
        {18, new Color(0.01f, 0.20f, 1.00f, 1f)},  // curtain - темно-синий
        {19, new Color(0.01f, 0.27f, 0.80f, 1f)},  // chair - синий
        {22, new Color(0.20f, 0.02f, 1.00f, 1f)},  // painting - пурпурный
        {23, new Color(1.00f, 0.40f, 0.04f, 1f)},  // sofa - оранжевый
        {24, new Color(0.28f, 0.03f, 1.00f, 1f)},  // shelf - фиолетово-синий
        {28, new Color(0.86f, 0.86f, 0.86f, 1f)},  // mirror - светло-серый
        {31, new Color(0.84f, 1.00f, 0.03f, 1f)},  // armchair - желто-зеленый
        {32, new Color(0.88f, 1.00f, 0.03f, 1f)},  // seat - светло-зеленый
        {35, new Color(0.28f, 1.00f, 0.04f, 1f)},  // desk - зеленый
        {38, new Color(0.03f, 1.00f, 0.88f, 1f)},  // lamp - бирюзовый
        {39, new Color(1.00f, 0.03f, 0.40f, 1f)},  // bathtub - розовый
        {41, new Color(0.03f, 0.76f, 1.00f, 1f)},  // cushion - голубой
    };

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

    // Поля для отслеживания изменений удалены - больше не используются

    private bool isInitialized = false;

    // Стабилизированные параметры для плавной обрезки (crop)
    private float stableCropOffsetX = 0f;
    private float stableCropOffsetY = 0f;
    private float stableCropScale = 1f;

    [Tooltip("Enable segmentation processing")]
    [SerializeField]
    private bool segmentationEnabled = true;

    [Tooltip("Сколько кадров пропускать между обработкой. 0 = каждый кадр, 1 = каждый второй и т.д.")]
    [SerializeField, Range(0, 10)]
    private int frameSkip = 2; // Оптимизация: обрабатываем каждый 3-й кадр по умолчанию

    private bool isHybridMode = false; // Флаг для отключения, если работает HybridManager

    void Awake()
    {
        // Проверяем, есть ли в сцене HybridManager
        if (FindObjectOfType<HybridSegmentationManager>() != null)
        {
            isHybridMode = true;
            Debug.Log("🚀 AsyncSegmentationManager: Обнаружен HybridManager. Переход в пассивный режим.");
            return; // Не выполняем инициализацию, если гибридный менеджер активен
        }

        // ... остальная часть Awake()
    }

    void OnEnable()
    {
        if (isHybridMode) return; // Не подписываемся на события в гибридном режиме

        // Автопоиск SAM2Manager если не назначен
        if (useSAM2Models && sam2Manager == null)
        {
            sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
            if (sam2Manager != null && enableDebugLogging)
            {
                Debug.Log("✅ SAM2SegmentationManager найден автоматически");
            }
        }

        // ИСПРАВЛЕНИЕ: Принудительно устанавливаем правильные значения для BiSeNet
        // ForceModelSettings();

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
        if (!segmentationEnabled || worker == null || !arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            return;
        }

        if (frameCount % (frameSkip + 1) == 0)
        {
            // Асинхронный метод заберет владение и сам освободит cpuImage
            ProcessFrameAsync(cpuImage);
        }
        else
        {
            // Если кадр пропускается, мы должны освободить его здесь
            cpuImage.Dispose();
        }
        
        frameCount++;
    }

    /// <summary>
    /// Обрабатывает AR режим (реальная камера)
    /// </summary>
    private void ProcessARMode()
    {
        if (ARSession.state < ARSessionState.SessionTracking || worker == null || isProcessing)
        {
            return;
        }

        if (frameCount % (frameSkip + 1) == 0)
        {
            if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            {
                ProcessFrameAsync(cpuImage);
            }
        }
    }




    private void InitializeSystem()
    {
        if (isInitialized)
        {
            return;
        }

        arCameraManager = FindObjectOfType<ARCameraManager>();
        // [УДАЛЕНО] Автопоиск RawImage больше не нужен

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

        // [УДАЛЕНО] Проверка segmentationDisplay больше не нужна

        // ИСПРАВЛЕНИЕ: Поддержка SAM моделей - не заменяем их на SegFormer
        if (modelAsset != null && modelAsset.name.ToLower().Contains("samdecoder"))
        {
            Debug.Log($"🤖 ОБНАРУЖЕНА SAM МОДЕЛЬ: {modelAsset.name}");
            Debug.Log("✅ SAM модели поддерживаются - продолжаем с текущей моделью");
            
            // Включаем SAM2 режим для правильной обработки
            useSAM2Models = true;
            useSegFormerModels = false;
            
            Debug.Log("🧠 Автоматически включен SAM2 режим для совместимости");
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

            // [УДАЛЕНО] Legacy display код - используется только ARWallPresenter

            // ДОПОЛНИТЕЛЬНАЯ проверка: принудительно устанавливаем настройки после создания материала
            ForceModelSettings();

            string modelName;
            if (useSegFormerModels) modelName = $"SegFormer {segformerModelType}";
            else if (useTopFormerADE20K) modelName = "TopFormer ADE20K";
            else modelName = "BiSeNet Cityscapes";

            Debug.Log($"🎉 AsyncSegmentationManager инициализация завершена успешно! Активная модель: {modelName}, Режим поворота: {maskRotationMode} ({GetRotationModeDescription(maskRotationMode)})");

            // Отправляем Flutter уведомление о готовности Unity
            Invoke(nameof(NotifyFlutterReady), 2f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка инициализации AsyncSegmentationManager: {e.Message}\n{e.StackTrace}");
        }

        // Используем настройки из инспектора вместо принудительного режима
        // ForceWallOnlyMode(); // ОТКЛЮЧЕНО

        // [УДАЛЕНО] ForceMaterialUpdate корутина больше не нужна

        isInitialized = true;
    }

    /// <summary>
    /// Принудительно устанавливает оптимальные настройки для выбранной модели (TopFormer или BiSeNet)
    /// </summary>
    private void ForceModelSettings()
    {
        // Основные настройки отображения - ТОЛЬКО СТЕНЫ
        selectedClass = 0;           // Показать только класс 0 (стены)
        showAllClasses = false;      // Возвращаемся к режиму стен с улучшенной фильтрацией
        showWalls = true;            // ВКЛЮЧИТЬ только стены
        showFloors = false;          // НЕ показывать полы  
        showCeilings = false;        // НЕ показывать потолки

        // Высокая видимость для четких границ
        visualizationOpacity = 0.7f; // Оптимальная полупрозрачность

        // Адаптивные настройки поворота в зависимости от модели
        if (useSegFormerModels)
        {
            // ПОЛЬЗОВАТЕЛЬСКАЯ НАСТРОЙКА: Поворот вправо на 90° с горизонтальным отражением
            maskRotationMode = 0; // Поворот на +90° (вправо)
            flipHorizontal = true; // С горизонтальным отражением
        }
        else if (useTopFormerADE20K)
        {
            // ИСПРАВЛЕНИЕ: TopFormer без дополнительных поворотов для корректной ориентации
            maskRotationMode = 3; // Без поворота - правильная ориентация
            flipHorizontal = false; // Без отражения - убираем отзеркаливание
        }
        else
        {
            // BiSeNet требует поворот на 180°
            maskRotationMode = 2; // Поворот на 180°
            flipHorizontal = false; // Отключаем дополнительное отражение
        }

        string modelName = useSegFormerModels ? $"SegFormer {segformerModelType}" :
                          (useTopFormerADE20K ? "TopFormer ADE20K" : "BiSeNet Cityscapes");
        Debug.Log($"🔧 ПРИНУДИТЕЛЬНО установлены настройки для '{modelName}':");
        Debug.Log($"   🧱 Режим отображения: ТОЛЬКО СТЕНЫ (класс {selectedClass})");
        Debug.Log($"   📺 Показать все классы: {showAllClasses}");
        Debug.Log($"   🔆 Opacity: {visualizationOpacity}");
        Debug.Log($"   🔄 Режим поворота: {maskRotationMode} ({GetRotationModeDescription(maskRotationMode)})");
        Debug.Log($"   🔄 Горизонтальное отражение: {flipHorizontal}");

        // СИНХРОНИЗИРУЕМ с ARWallPresenter если он подключен
        if (arWallPresenter != null)
        {
            if (showAllClasses)
            {
                arWallPresenter.SetAllClassesMode();
                Debug.Log("🌈 ARWallPresenter синхронизирован - режим ВСЕХ классов");
            }
            else
            {
                arWallPresenter.SetSingleClassMode(selectedClass, paintColor);
                Debug.Log("🔄 ARWallPresenter синхронизирован - режим одного класса");
            }
        }

        // Принудительно обновляем материал, если он уже создан
        if (displayMaterialInstance != null)
        {
            UpdateMaterialParameters();
            Debug.Log("✅ Материал немедленно обновлен с новыми настройками");
        }
    }

    /// <summary>
    /// Принудительно устанавливает режим отображения только стен
    /// </summary>
    private void ForceWallOnlyMode()
    {
        selectedClass = 0;           // Только класс 0 (стены)
        showAllClasses = false;      // Возвращаемся к режиму стен с улучшенной фильтрацией
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

    // [УДАЛЕНО] SetupCorrectAspectRatio - заменен ARWallPresenter.FitToScreen()

    // [УДАЛЕНО] ForceMaterialUpdate - больше не нужен для ARWallPresenter

    private async void ProcessFrameAsync(XRCpuImage cpuImage)
    {
        isProcessing = true;

        try
        {
            var convertTask = ConvertCpuImageToTexture(cpuImage);
            await convertTask;
            if (cancellationTokenSource.IsCancellationRequested || !convertTask.IsCompletedSuccessfully) return;

            // Если включен SAM2, используем его вместо стандартных моделей
            if (useSAM2Models && sam2Manager != null)
            {
                if (enableDebugLogging) Debug.Log("🎯 Переключение на SAM2 для сегментации");
                // Преобразуем RenderTexture в Texture2D для SAM2
                var tempTexture2D = RenderTextureToTexture2D(cameraInputTexture);
                sam2Manager.ProcessFrame(tempTexture2D);
                return;
            }

            // Выбираем нормализацию в зависимости от типа модели
            if (useSegFormerModels)
            {
                ApplySegFormerNormalization();
            }
            else
            {
                NormalizeImage();
            }

            inputTensor?.Dispose();

            // ИСПРАВЛЕНИЕ: Специальная обработка для SAM моделей
            if (useSAM2Models && modelAsset.name.ToLower().Contains("sam"))
            {
                Debug.Log("🤖 Обрабатываем SAM модель - переключаемся на SAM2SegmentationManager");
                
                // Для SAM моделей используем SAM2SegmentationManager или SimpleSAM2Manager
                if (sam2Manager == null)
                {
                    sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
                    if (sam2Manager != null)
                    {
                        Debug.Log("✅ SAM2SegmentationManager найден автоматически");
                    }
                }
                
                // Если SAM2SegmentationManager не найден, ищем SimpleSAM2Manager
                SimpleSAM2Manager simpleSAM2 = null;
                if (sam2Manager == null)
                {
                    simpleSAM2 = FindObjectOfType<SimpleSAM2Manager>();
                    if (simpleSAM2 != null)
                    {
                        Debug.Log("✅ SimpleSAM2Manager найден как альтернатива");
                    }
                }
                
                if (sam2Manager != null)
                {
                    var tempTexture2D = RenderTextureToTexture2D(cameraInputTexture);
                    sam2Manager.ProcessFrame(tempTexture2D);
                    Destroy(tempTexture2D);
                    Debug.Log("🤖 Кадр передан в SAM2SegmentationManager для обработки");
                }
                else if (simpleSAM2 != null)
                {
                    var tempTexture2D = RenderTextureToTexture2D(cameraInputTexture);
                    simpleSAM2.ProcessFrame(tempTexture2D);
                    Destroy(tempTexture2D);
                    Debug.Log("🤖 Кадр передан в SimpleSAM2Manager для обработки");
                }
                else
                {
                    Debug.LogWarning("⚠️ Ни SAM2SegmentationManager, ни SimpleSAM2Manager не найдены");
                    Debug.LogWarning("💡 Создайте GameObject с компонентом SimpleSAM2Manager для обработки SAM моделей");
                }
                return; // Выходим из обработки, так как SAM2Manager берет на себя всю работу
            }

            // Адаптивный размер тензора в зависимости от модели (для не-SAM моделей)
            int tensorWidth, tensorHeight;
            if (useSegFormerModels)
            {
                // SegFormer использует собственные размеры
                Vector2Int segformerSize = GetSegFormerInputSize();
                tensorWidth = segformerSize.x;
                tensorHeight = segformerSize.y;
                // Debug.Log($"🔥 SegFormer {segformerModelType}: используем размер {tensorWidth}x{tensorHeight}"); // ОТКЛЮЧЕНО: спам
            }
            else if (useTopFormerADE20K)
            {
                // TopFormer работает с квадратными изображениями
                // ФИКСИРОВАННЫЙ размер 512x512 - модель была обучена именно на этом
                tensorWidth = 512;
                tensorHeight = 512;
            }
            else
            {
                // BiSeNet требует фиксированный размер 720x960
                tensorWidth = 960;
                tensorHeight = 720;
            }

            inputTensor = TextureConverter.ToTensor(normalizedTexture, tensorWidth, tensorHeight, 3);

            string currentModelName = useSegFormerModels ? $"SegFormer {segformerModelType}" :
                                     (useTopFormerADE20K ? "TopFormer" : "BiSeNet");
            // Debug.Log($"🔧 МОДЕЛЬ {currentModelName}: текстура {normalizedTexture.width}x{normalizedTexture.height} → тензор {tensorWidth}x{tensorHeight}"); // ОТКЛЮЧЕНО: спам

            // Debug.Log($"🔢 Создан тензор: {normalizedTexture.width}x{normalizedTexture.height}x3 (аспект: {(float)normalizedTexture.width / normalizedTexture.height:F2})"); // Отключено - спам

            worker.Schedule(inputTensor);

            // Выбираем обработку в зависимости от типа модели
            if (useSegFormerModels)
            {
                ProcessSegFormerOutput();
            }
            else if (useTopFormerADE20K)
            {
                ProcessTopFormerOutput();
            }
            else
            {
                ProcessOutputWithArgmaxShader();
            }
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

        // Проверяем соответствие размеров для BiSeNet
        Debug.Log($"✅ Обрабатываем тензор BiSeNet: {width}x{height}");

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
    /// Обрабатывает выходные данные SegFormer моделей
    /// </summary>
    private void ProcessSegFormerOutput()
    {
        var outputTensor = worker.PeekOutput() as Tensor<float>;
        if (outputTensor == null)
        {
            Debug.LogError("❌ SegFormer: Выходной тензор равен null!");
            return;
        }

        var shape = outputTensor.shape;
        int batchSize = shape[0];
        int numClasses = shape[1];  // SegFormer ADE20K имеет 150 классов
        int height = shape[2];
        int width = shape[3];

        // Debug.Log($"🔥 SegFormer выход: batch={batchSize}, classes={numClasses}, size={width}x{height}"); // ОТКЛЮЧЕНО: спам

        // SegFormer выдает логиты, нужен argmax как у BiSeNet
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
            }
        }

        var tensorData = outputTensor.DownloadToArray();
        var cmd = new CommandBuffer { name = "SegFormerPostProcessing" };

        var tensorDataBuffer = new ComputeBuffer(tensorData.Length, sizeof(float));
        tensorDataBuffer.SetData(tensorData);

        // Применяем argmax шейдер для получения индексов классов
        int kernel = argmaxShader.FindKernel("Argmax");
        cmd.SetComputeIntParam(argmaxShader, "width", width);
        cmd.SetComputeIntParam(argmaxShader, "height", height);
        cmd.SetComputeIntParam(argmaxShader, "num_classes", numClasses);

        cmd.SetComputeBufferParam(argmaxShader, kernel, "InputTensor", tensorDataBuffer);
        cmd.SetComputeTextureParam(argmaxShader, kernel, "Result", segmentationMaskTexture);

        int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
        cmd.DispatchCompute(argmaxShader, kernel, threadGroupsX, threadGroupsY, 1);

        // Увеличение разрешения маски
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
        }
        else
        {
            Graphics.Blit(segmentationMaskTexture, upsampledMaskTexture);
        }

        RenderTexture finalMask = upsampledMaskTexture;

        // Постобработка маски (сглаживание)
        if (enableMaskSmoothing && maskSmoothingIterations > 0)
        {
            ReleaseRenderTexture(smoothedMaskTexture);
            smoothedMaskTexture = CreateRenderTexture(upsampleWidth, upsampleHeight, RenderTextureFormat.RFloat);
            ReleaseRenderTexture(pingPongMaskTexture);
            pingPongMaskTexture = CreateRenderTexture(upsampleWidth, upsampleHeight, RenderTextureFormat.RFloat);

            RenderTexture source = upsampledMaskTexture;
            RenderTexture destination = smoothedMaskTexture;

            if (useAdvancedPostProcessing && advancedPostProcessingShader != null)
            {
                // Улучшенная постобработка
                int edgeAwareKernel = advancedPostProcessingShader.FindKernel("EdgeAwareSmoothing");
                cmd.SetComputeIntParam(advancedPostProcessingShader, "width", upsampleWidth);
                cmd.SetComputeIntParam(advancedPostProcessingShader, "height", upsampleHeight);
                cmd.SetComputeFloatParam(advancedPostProcessingShader, "edgeThreshold", edgeThreshold);
                cmd.SetComputeFloatParam(advancedPostProcessingShader, "contrastFactor", contrastFactor);

                int advancedThreadGroupsX = Mathf.CeilToInt(upsampleWidth / 8.0f);
                int advancedThreadGroupsY = Mathf.CeilToInt(upsampleHeight / 8.0f);

                for (int i = 0; i < maskSmoothingIterations; i++)
                {
                    cmd.SetComputeTextureParam(advancedPostProcessingShader, edgeAwareKernel, "InputMask", source);
                    cmd.SetComputeTextureParam(advancedPostProcessingShader, edgeAwareKernel, "ResultMask", destination);
                    cmd.DispatchCompute(advancedPostProcessingShader, edgeAwareKernel, advancedThreadGroupsX, advancedThreadGroupsY, 1);

                    var temp = source;
                    source = destination;
                    destination = (source == smoothedMaskTexture) ? pingPongMaskTexture : smoothedMaskTexture;
                }

                finalMask = source;
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
            }
        }

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Dispose();

        // Передаем маску в ARWallPresenter
        if (arWallPresenter != null)
        {
            var maskToSend = (enableMaskSmoothing && maskSmoothingIterations > 0 && smoothedMaskTexture != null) ? smoothedMaskTexture : upsampledMaskTexture;
            arWallPresenter.SetSegmentationMask(maskToSend);
            
            float cropOffsetX = stableCropOffsetX;
            float cropOffsetY = stableCropOffsetY;
            float cropScale = stableCropScale;
            arWallPresenter.SetCropParameters(cropOffsetX, cropOffsetY, cropScale);

            // ОТКЛЮЧЕНО: Слишком частый вызов, вызывает спам в логах
            // Debug.Log("🔥 SegFormer маска передана в ARWallPresenter");
        }

        tensorDataBuffer.Dispose();
        outputTensor.Dispose();
    }

    /// <summary>
    /// Обрабатывает выходные данные TopFormer (уже готовые индексы классов)
    /// </summary>
    private void ProcessTopFormerOutput()
    {
        // Сначала пробуем int тензор (TopFormer-B экспортирован с argmax)
        var intOutputTensor = worker.PeekOutput() as Tensor<int>;
        if (intOutputTensor != null)
        {
            Debug.Log($"✅ TopFormer-B выход (int): форма={string.Join("x", intOutputTensor.shape)}");
            ProcessTopFormerIntTensor(intOutputTensor);
            return;
        }

        // Если нет int тензора, пробуем float (TopFormer-S логиты)
        var floatOutputTensor = worker.PeekOutput() as Tensor<float>;
        if (floatOutputTensor != null)
        {
            Debug.Log($"✅ TopFormer-S выход (float): форма={string.Join("x", floatOutputTensor.shape)}");
            ProcessTopFormerFloatTensor(floatOutputTensor);
            return;
        }

        Debug.LogError("❌ TopFormer: Выходной тензор равен null!");
    }

    private void ProcessTopFormerIntTensor(Tensor<int> intTensor)
    {
        var shape = intTensor.shape;
        int shapeLength = shape.rank;

        if (shapeLength != 4 || shape[0] != 1 || shape[1] != 1)
        {
            Debug.LogError($"❌ TopFormer-B: Неожиданная форма int тензора: [{string.Join(",", shape)}]");
            intTensor.Dispose();
            return;
        }

        int height = shape[2];
        int width = shape[3];
        Debug.Log($"🎯 TopFormer-B обработка: размер маски {width}x{height}");

        // Создаем текстуры для маски
        if (segmentationMaskTexture == null || segmentationMaskTexture.width != width || segmentationMaskTexture.height != height)
        {
            ReleaseRenderTexture(segmentationMaskTexture);
            segmentationMaskTexture = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);

            if (displayMaterialInstance != null)
            {
                displayMaterialInstance.SetTexture("_MaskTex", segmentationMaskTexture);
                UpdateMaterialParameters();
            }
        }

        // Конвертируем int тензор в float массив
        var intData = intTensor.DownloadToArray();
        var floatData = new float[intData.Length];
        for (int i = 0; i < intData.Length; i++)
        {
            floatData[i] = (float)intData[i];
        }

        var texture2D = new Texture2D(width, height, TextureFormat.RFloat, false);
        texture2D.SetPixelData(floatData, 0);
        texture2D.Apply();

        // Копируем в RenderTexture
        Graphics.CopyTexture(texture2D, segmentationMaskTexture);

        // Передаем маску в ARWallPresenter
        SetTopFormerMaskToPresenter();

        // Очистка
        Destroy(texture2D);
        intTensor.Dispose();
    }

    private void ProcessTopFormerFloatTensor(Tensor<float> floatTensor)
    {
        var shape = floatTensor.shape;
        int shapeLength = shape.rank;
        Debug.Log($"🔍 ДИАГНОСТИКА TopFormer тензора: shape.rank={shapeLength}, размеры=[{string.Join(",", shape)}]");

        int height, width;

        // TopFormer может выдавать разные форматы:
        // [batch, height, width] - прямые индексы классов
        // [batch, classes, height, width] - логиты (как BiSeNet)
        if (shapeLength == 3)
        {
            // Формат [batch, height, width] - прямые индексы
            height = shape[1];
            width = shape[2];
            Debug.Log($"✅ TopFormer формат [batch, height, width]: {width}x{height}");
        }
        else if (shapeLength == 4)
        {
            // Формат [batch, classes, height, width] - логиты, нужен argmax
            int numClasses = shape[1];
            height = shape[2];
            width = shape[3];
            Debug.Log($"⚠️ TopFormer формат [batch, classes, height, width]: classes={numClasses}, size={width}x{height}");
            Debug.Log("⚠️ TopFormer выдает логиты вместо индексов! Переключите обратно на BiSeNet обработку.");
            Debug.Log($"🔍 АНАЛИЗ: Вход 512x512 → Выход {width}x{height} (downsample factor: {512 / width}x)");

            // В этом случае используем BiSeNet обработку с тем же тензором
            ProcessOutputWithArgmaxShader();
            return;
        }
        else
        {
            Debug.LogError($"❌ TopFormer: Неподдерживаемая форма тензора {shapeLength}D: [{string.Join(",", shape)}]");
            floatTensor.Dispose();
            return;
        }

        Debug.Log($"✅ Обрабатываем тензор TopFormer: {width}x{height}");

        // Создаем текстуры для маски
        if (segmentationMaskTexture == null || segmentationMaskTexture.width != width || segmentationMaskTexture.height != height)
        {
            ReleaseRenderTexture(segmentationMaskTexture);
            segmentationMaskTexture = CreateRenderTexture(width, height, RenderTextureFormat.RFloat);

            if (displayMaterialInstance != null)
            {
                displayMaterialInstance.SetTexture("_MaskTex", segmentationMaskTexture);
                UpdateMaterialParameters();
            }
        }

        // Конвертируем тензор в текстуру через CPU (TopFormer уже выдает готовые индексы)
        var tensorData = floatTensor.DownloadToArray();
        var texture2D = new Texture2D(width, height, TextureFormat.RFloat, false);

        // Копируем данные напрямую (без argmax, так как TopFormer уже выдает индексы)
        texture2D.SetPixelData(tensorData, 0);
        texture2D.Apply();

        // Копируем в RenderTexture
        Graphics.CopyTexture(texture2D, segmentationMaskTexture);

        // Передаем маску в ARWallPresenter
        SetTopFormerMaskToPresenter();

        // Очистка
        Destroy(texture2D);
        floatTensor.Dispose();
    }

    private void SetTopFormerMaskToPresenter()
    {
        if (arWallPresenter != null)
        {
            arWallPresenter.SetSegmentationMask(segmentationMaskTexture);

            // Стабилизированные crop параметры для TopFormer
            float cropOffsetX = 0f;
            float cropOffsetY = 0f;
            float cropScale = 1f;

            // Компенсируем низкое разрешение маски через crop adjustment
            if (enableTemporalStabilization)
            {
                cropOffsetX = 0.0f; // Без смещения по X
                cropOffsetY = 0.0f; // Без смещения по Y  
                cropScale = 1.0f;   // Масштаб 1:1
            }

            arWallPresenter.SetCropParameters(cropOffsetX, cropOffsetY, cropScale);
            Debug.Log($"🎨 TopFormer маска передана в ARWallPresenter: crop({cropOffsetX:F2}, {cropOffsetY:F3}, {cropScale:F2}), стабилизация={enableTemporalStabilization}");
        }
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
            // ОТКЛЮЧЕНО: спам на каждый кадр
            // if (enableDebugLogging)
            // {
            //     Debug.Log($"🎯 Адаптивное разрешение включено: {targetResolution}x{targetResolution} (устройство поддерживает до {maxDeviceResolution}x{maxDeviceResolution})");
            // }
        }
        else
        {
            // Debug.Log($"🔒 Адаптивное разрешение отключено: используем фиксированное {targetResolution}x{targetResolution}"); // Отключено - спам
        }

        // ИСПРАВЛЕНИЕ: Модель требует квадратные данные, но мы сохраним аспект камеры для правильного отображения
        float cameraAspect = (float)cpuImage.width / cpuImage.height;

        // ИСПРАВЛЕНИЕ: Используем высокое разрешение для промежуточной текстуры
        // TextureConverter.ToTensor() потом сделает resize до 720x960 для BiSeNet
        int maxDimension = Mathf.Min(cpuImage.width, cpuImage.height);
        int outputWidth = maxDimension;
        int outputHeight = maxDimension;

        // Используем максимально возможное разрешение без превышения размеров камеры
        outputWidth = Mathf.Min(outputWidth, cpuImage.width);
        outputHeight = Mathf.Min(outputHeight, cpuImage.height);

        string modelTargetResolution;
        if (useSegFormerModels)
        {
            Vector2Int segformerSize = GetSegFormerInputSize();
            modelTargetResolution = $"SegFormer {segformerModelType}={segformerSize.x}x{segformerSize.y}";
        }
        else if (useTopFormerADE20K)
        {
            modelTargetResolution = "TopFormer=512x512";
        }
        else
        {
            modelTargetResolution = "BiSeNet=960x720";
        }

        // ОТКЛЮЧЕНО: спам логов на каждый кадр
        // if(enableDebugLogging)
        // {
        //     Debug.Log($"🔧 ПРОМЕЖУТОЧНОЕ РАЗРЕШЕНИЕ: камера={cpuImage.width}x{cpuImage.height} → текстура={outputWidth}x{outputHeight} → {modelTargetResolution}");
        // }

        // Но сохраняем информацию об аспекте для корректного отображения маски
        string modelName = useTopFormerADE20K ? "TopFormer" : "BiSeNet";
        // ОТКЛЮЧЕНО: спам на каждый кадр
        // if(enableDebugLogging)
        // {
        //      Debug.Log($"🔲 Входное разрешение для модели: {outputWidth}x{outputHeight} (соотношение сторон камеры: {cameraAspect:F2})");
        // }

        // Debug.Log($"🔲 ПРИНУДИТЕЛЬНО устанавливаем размер входа модели: {outputWidth}x{outputHeight} (аспект камеры: {cameraAspect:F2})"); // Отключено - спам

        // ДИАГНОСТИКА: Проверяем размеры камеры
        // ОТКЛЮЧЕНО: спам на каждый кадр
        // if(enableDebugLogging)
        // {
        //     Debug.Log($"🔍 ДИАГНОСТИКА камеры: width={cpuImage.width}, height={cpuImage.height}");
        // }

        // ИСПРАВЛЕНИЕ CROP: Используем весь кадр без crop для BiSeNet
        int cropX = 0;
        int cropY = 0;
        int cropWidth = cpuImage.width;
        int cropHeight = cpuImage.height;

        // ИСПРАВЛЕНИЕ: cropY=0 правильно для ландшафтной камеры
        // Проблема в том, что камера 1920x1440 (ландшафт), а экран 1170x2532 (портрет)
        // ОТКЛЮЧЕНО: спам на каждый кадр
        // if(enableDebugLogging)
        // {
        //     Debug.Log($"🔍 КАМЕРА vs ЭКРАН: камера={cpuImage.width}x{cpuImage.height} (соотношение {(float)cpuImage.width / cpuImage.height:F2}), экран={Screen.width}x{Screen.height} (соотношение {(float)Screen.width / Screen.height:F2})");
        //     Debug.Log($"🔍 CROP РАСЧЕТ: cropX={cropX}, cropY={cropY}, cropWidth={cropWidth}, cropHeight={cropHeight}");
        // }

        conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(cropX, cropY, cropWidth, cropHeight), // Полный кадр
            outputDimensions = new Vector2Int(outputWidth, outputHeight),
            outputFormat = TextureFormat.RGBA32,
            transformation = transformation
        };

        // ОТКЛЮЧЕНО: спам на каждый кадр
        // if(enableDebugLogging)
        // {
        //     Debug.Log($"📐 Полный кадр: {cropX},{cropY} размер {cropWidth}x{cropHeight} → {outputWidth}x{outputHeight}");
        // }

        // ИСПРАВЛЕНИЕ: Сохраняем информацию о crop для правильного отображения
        float cropOffsetX = (float)cropX / cpuImage.width;
        float cropOffsetY = (float)cropY / cpuImage.height;
        float cropScale = 1.0f; // Полный кадр без crop

        // АГРЕССИВНАЯ КОРРЕКЦИЯ: Исправляем смещение вправо
        float originalOffsetX = cropOffsetX;
        float originalOffsetY = cropOffsetY;
        // cropOffsetX += 0.05f; // ОТКЛЮЧЕНО: сдвигаем маску ВПРАВО 
        // cropOffsetY += 0.12f; // ОТКЛЮЧЕНО: сильно опускаем маску ВНИЗ (было 0.08f)

        // ОТКЛЮЧЕНО: спам на каждый кадр
        // Debug.Log($"🔧 АГРЕССИВНАЯ КОРРЕКЦИЯ crop: X {originalOffsetX:F3}→{cropOffsetX:F3}, Y {originalOffsetY:F3}→{cropOffsetY:F3}");
        // Debug.Log($"🔍 ПРОВЕРКА ПРИМЕНЕНИЯ: передаем в шейдер cropOffsetX={cropOffsetX:F3}, cropOffsetY={cropOffsetY:F3}, cropScale={cropScale:F3}");

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
            // Debug.Log($"📐 Текстуры пересозданы: {outputWidth}x{outputHeight} (аспект камеры: {cameraAspect:F2})"); // ОТКЛЮЧЕНО: спам
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

        // Используем ADE20K стандартные цвета
        if (ade20kClassColors.ContainsKey(classId))
        {
            return ade20kClassColors[classId];
        }

        // Для неизвестных классов генерируем цвет на основе ID
        return GenerateColorFromId(classId);
    }

    /// <summary>
    /// Генерирует уникальный цвет на основе ID класса
    /// </summary>
    private Color GenerateColorFromId(int classId)
    {
        // Простая хеш-функция для генерации цвета
        float hue = (classId * 137.5f) % 360f / 360f; // Golden angle для равномерного распределения
        return Color.HSVToRGB(hue, 0.7f, 0.9f);
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

    #region SegFormer Integration

    /// <summary>
    /// Типы моделей SegFormer
    /// </summary>
    public enum SegFormerModelType
    {
        B0_512x512,
        B1_512x512,
        B2_512x512,
        B3_512x512,
        B4_512x512,
        B5_640x640,
    }

    /// <summary>
    /// ADE20K Indoor классы для SegFormer
    /// </summary>
    private readonly Dictionary<int, string> segFormerADE20KClasses = new Dictionary<int, string>()
    {
        {0, "wall"},           // стены - основной класс для покраски
        {1, "building"},       // здание (внешние стены)  
        {2, "sky"},           // небо (не актуально для indoor)
        {3, "floor"},         // пол
        {4, "tree"},          // растения
        {5, "ceiling"},       // потолок
        {6, "road"},          // дорога (не актуально)
        {7, "bed"},           // кровать
        {8, "windowpane"},    // окно
        {9, "grass"},         // трава
        {10, "cabinet"},      // шкаф
        {11, "sidewalk"},     // тротуар
        {12, "person"},       // человек
        {13, "earth"},        // земля
        {14, "door"},         // дверь
        {15, "table"},        // стол
        {16, "mountain"},     // гора
        {17, "plant"},        // растение
        {18, "curtain"},      // штора
        {19, "chair"},        // стул
        {20, "car"}           // машина
    };

    /// <summary>
    /// Цвета для SegFormer классов (optimized for indoor)
    /// </summary>
    private readonly Dictionary<int, Color> segFormerClassColors = new Dictionary<int, Color>()
    {
        {0, new Color(1.0f, 0.2f, 0.2f, 1f)},   // wall - красный  
        {1, new Color(0.2f, 0.8f, 0.2f, 1f)},   // building - зеленый
        {2, new Color(0.5f, 0.8f, 1.0f, 1f)},   // sky - голубой
        {3, new Color(0.8f, 0.6f, 0.2f, 1f)},   // floor - коричневый/желтый
        {4, new Color(0.2f, 0.6f, 0.2f, 1f)},   // tree - темно-зеленый
        {5, new Color(0.8f, 0.8f, 0.8f, 1f)},   // ceiling - светло-серый
        {6, new Color(0.4f, 0.4f, 0.4f, 1f)},   // road - серый
        {7, new Color(0.6f, 0.3f, 0.8f, 1f)},   // bed - фиолетовый
        {8, new Color(0.2f, 0.6f, 0.8f, 1f)},   // windowpane - синий
        {9, new Color(0.4f, 0.8f, 0.4f, 1f)},   // grass - светло-зеленый
        {10, new Color(0.6f, 0.4f, 0.2f, 1f)},  // cabinet - коричневый
        {11, new Color(0.7f, 0.7f, 0.7f, 1f)},  // sidewalk - светло-серый
        {12, new Color(1.0f, 0.7f, 0.7f, 1f)},  // person - розовый
        {13, new Color(0.5f, 0.3f, 0.2f, 1f)},  // earth - темно-коричневый
        {14, new Color(0.4f, 0.2f, 0.0f, 1f)},  // door - темно-коричневый
        {15, new Color(0.8f, 0.8f, 0.4f, 1f)},  // table - светло-желтый
        {16, new Color(0.6f, 0.6f, 0.6f, 1f)},  // mountain - серый
        {17, new Color(0.3f, 0.7f, 0.3f, 1f)},  // plant - зеленый
        {18, new Color(0.7f, 0.5f, 0.9f, 1f)},  // curtain - светло-фиолетовый
        {19, new Color(0.9f, 0.6f, 0.3f, 1f)},  // chair - светло-коричневый
        {20, new Color(0.3f, 0.3f, 0.3f, 1f)}   // car - темно-серый
    };

    /// <summary>
    /// Получает размер входного тензора для SegFormer модели
    /// </summary>
    private Vector2Int GetSegFormerInputSize()
    {
        switch (segformerModelType)
        {
            case SegFormerModelType.B0_512x512:
            case SegFormerModelType.B1_512x512:
            case SegFormerModelType.B2_512x512:
            case SegFormerModelType.B3_512x512:
            case SegFormerModelType.B4_512x512:
                return new Vector2Int(512, 512);
            case SegFormerModelType.B5_640x640:
                return new Vector2Int(640, 640);
            default:
                return new Vector2Int(512, 512);
        }
    }

    /// <summary>
    /// Применяет ImageNet нормализацию для SegFormer
    /// </summary>
    private void ApplySegFormerNormalization()
    {
        if (imageNormalizerShader == null || !useImageNetNormalization)
        {
            // Если нет шейдера нормализации или отключена, используем стандартную
            NormalizeImage();
            return;
        }

        int kernel = imageNormalizerShader.FindKernel("Normalize");

        // ImageNet нормализация для SegFormer
        imageNormalizerShader.SetVector("image_mean", new Vector4(0.485f, 0.456f, 0.406f, 0));
        imageNormalizerShader.SetVector("image_std", new Vector4(0.229f, 0.224f, 0.225f, 0));

        imageNormalizerShader.SetTexture(kernel, "InputTexture", cameraInputTexture);
        imageNormalizerShader.SetTexture(kernel, "OutputTexture", normalizedTexture);

        int threadGroupsX = Mathf.CeilToInt(cameraInputTexture.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(cameraInputTexture.height / 8.0f);
        imageNormalizerShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

        // Debug.Log("✅ Применена ImageNet нормализация для SegFormer"); // ОТКЛЮЧЕНО: спам
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
        displayMaterialInstance.SetFloat("_FlipHorizontal", flipHorizontal ? 1.0f : 0.0f);

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

    /// <summary>
    /// Возвращает текущую маску сегментации для использования в других компонентах
    /// </summary>
    public RenderTexture GetSegmentationMask()
    {
        return segmentationMaskTexture;
    }

    /// <summary>
    /// Возвращает настройку горизонтального отражения для использования в других компонентах
    /// </summary>
    public bool GetFlipHorizontal()
    {
        return flipHorizontal;
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
        // [УДАЛЕНО] Legacy метод - используйте ARWallPresenter.RefreshScreenFit()
        if (arWallPresenter != null)
        {
            arWallPresenter.RefreshScreenFit();
        }
        UpdateMaterialParameters();
        Debug.Log("🔄 Покрытие экрана принудительно обновлено через ARWallPresenter");
    }

    /// <summary>
    /// Включить/выключить полноэкранный режим маски
    /// </summary>
    public void SetFullscreenMode(bool enabled)
    {
        forceFullscreenMask = enabled;
        if (enabled && arWallPresenter != null)
        {
            arWallPresenter.RefreshScreenFit();
        }
        UpdateMaterialParameters();
        Debug.Log($"📱 Полноэкранный режим маски: {(enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")} - используется ARWallPresenter");
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
    /// Увеличить видимость маски для лучшего тестирования
    /// </summary>
    [ContextMenu("Тест: Максимальная видимость")]
    public void SetMaxVisibility()
    {
        visualizationOpacity = 0.8f;
        showAllClasses = true;
        showWalls = showFloors = showCeilings = false;
        selectedClass = -1;
        UpdateMaterialParameters();
        Debug.Log("🌈 Установлена максимальная видимость: opacity=1.0, показываем все классы");
    }

    /// <summary>
    /// Принудительно исправить ориентацию маски BiSeNet
    /// </summary>
    [ContextMenu("Тест: Исправить ориентацию BiSeNet")]
    public void FixBiSeNetOrientation()
    {
        // ForceModelSettings();
        Debug.Log("🔧 Принудительно исправлена ориентация и настройки BiSeNet");
    }

    /// <summary>
    /// Переключить горизонтальное отражение маски
    /// </summary>
    [ContextMenu("Тест: Переключить горизонтальное отражение")]
    public void ToggleHorizontalFlip()
    {
        flipHorizontal = !flipHorizontal;
        UpdateMaterialParameters();
        Debug.Log($"🔄 Горизонтальное отражение: {(flipHorizontal ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
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
    /// 📝 Получить описание режима поворота
    /// </summary>
    private string GetRotationModeDescription(int mode)
    {
        switch (mode)
        {
            case 0: return "+90°";
            case 1: return "-90°";
            case 2: return "180°";
            case 3: return "Без поворота (0°)";
            default: return $"неизвестный режим {mode}";
        }
    }

    /// <summary>
    /// Применяет тестовые режимы выравнивания маски на основе флагов Inspector
    /// </summary>
    private void ApplyTestAlignmentModes()
    {
        // Убеждаемся что активен только один режим
        int activeModes = (testMode180NoFlip ? 1 : 0) + (testModeNoRotationWithFlip ? 1 : 0) +
                         (testModePlus90 ? 1 : 0) + (testModeMinus90 ? 1 : 0);

        if (activeModes > 1)
        {
            // Если выбрано несколько режимов, оставляем только приоритетный
            if (testMode180NoFlip)
            {
                testModeNoRotationWithFlip = false;
                testModePlus90 = false;
                testModeMinus90 = false;
            }
            else if (testModeNoRotationWithFlip)
            {
                testModePlus90 = false;
                testModeMinus90 = false;
            }
            else if (testModePlus90)
            {
                testModeMinus90 = false;
            }
        }

        // Применяем выбранный режим
        if (testMode180NoFlip)
        {
            // Адаптивная логика в зависимости от модели
            int targetRotationMode = useTopFormerADE20K ? 3 : 2; // TopFormer: без поворота (исправление), BiSeNet: 180°
            bool targetFlipHorizontal = useTopFormerADE20K ? false : false; // TopFormer: без отражения (исправление)

            if (maskRotationMode != targetRotationMode || flipHorizontal != targetFlipHorizontal)
            {
                maskRotationMode = targetRotationMode;
                flipHorizontal = targetFlipHorizontal;
                UpdateMaterialParameters();
                string modelName = useTopFormerADE20K ? "TopFormer (без поворота - исправление отзеркаливания)" : "BiSeNet (180°)";
                Debug.Log($"🎯 ПРИМЕНЕН режим для {modelName}: rotation={targetRotationMode}, flip={targetFlipHorizontal}");
            }
        }
        else if (testModeNoRotationWithFlip)
        {
            // Адаптивная логика для "без поворота + flip"
            int targetRotationMode = useTopFormerADE20K ? 3 : 3; // Без поворота для обеих моделей
            bool targetFlipHorizontal = useTopFormerADE20K ? true : true; // Flip для обеих моделей

            if (maskRotationMode != targetRotationMode || flipHorizontal != targetFlipHorizontal)
            {
                maskRotationMode = targetRotationMode;
                flipHorizontal = targetFlipHorizontal;
                UpdateMaterialParameters();
                string modelName = useTopFormerADE20K ? "TopFormer (без поворота + flip)" : "BiSeNet (без поворота + flip)";
                Debug.Log($"🎯 ПРИМЕНЕН режим для {modelName}: rotation={targetRotationMode}, flip={targetFlipHorizontal}");
            }
        }
        else if (testModePlus90)
        {
            // Адаптивная логика для +90° в зависимости от модели
            int targetRotationMode = useTopFormerADE20K ? 0 : 0; // +90° для обеих моделей
            bool targetFlipHorizontal = useTopFormerADE20K ? true : false; // TopFormer: с flip, BiSeNet: без flip

            if (maskRotationMode != targetRotationMode || flipHorizontal != targetFlipHorizontal)
            {
                maskRotationMode = targetRotationMode;
                flipHorizontal = targetFlipHorizontal;
                UpdateMaterialParameters();
                string modelName = useTopFormerADE20K ? "TopFormer (+90° + горизонтальный flip)" : "BiSeNet (+90°)";
                Debug.Log($"🎯 ПРИМЕНЕН режим для {modelName}: rotation={targetRotationMode}, flip={targetFlipHorizontal}");
            }
        }
        else if (testModeMinus90)
        {
            if (maskRotationMode != 1 || flipHorizontal != false)
            {
                maskRotationMode = 1;
                flipHorizontal = false;
                UpdateMaterialParameters();
                Debug.Log("🎯 ПРИМЕНЕН режим: -90°");
            }
        }
    }

    /// <summary>
    /// Публичные методы для работы с SegFormer
    /// </summary>
    public void EnableSegFormerModels(bool enable = true)
    {
        useSegFormerModels = enable;
        if (enable)
        {
            useTopFormerADE20K = false; // Отключаем TopFormer
        }
        ForceModelSettings();
        Debug.Log($"🔥 SegFormer модели: {(enable ? "ВКЛЮЧЕНЫ" : "ВЫКЛЮЧЕНЫ")}");
    }

    public void SetSegFormerModelType(SegFormerModelType modelType)
    {
        segformerModelType = modelType;
        if (useSegFormerModels)
        {
            ForceModelSettings();
            Debug.Log($"🔥 SegFormer тип модели изменен на: {modelType}");
        }
    }

    public void EnableImageNetNormalization(bool enable = true)
    {
        useImageNetNormalization = enable;
        Debug.Log($"🔥 ImageNet нормализация: {(enable ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
    }

    /// <summary>
    /// Быстрое переключение на SegFormer B0 (производительность)
    /// </summary>
    [ContextMenu("SegFormer: Быстрая модель B0")]
    public void SwitchToSegFormerB0()
    {
        EnableSegFormerModels(true);
        SetSegFormerModelType(SegFormerModelType.B0_512x512);
        Debug.Log("🚀 Переключено на SegFormer B0 (быстрая модель)");
    }

    /// <summary>
    /// Быстрое переключение на SegFormer B5 (качество)
    /// </summary>
    [ContextMenu("SegFormer: Качественная модель B5")]
    public void SwitchToSegFormerB5()
    {
        EnableSegFormerModels(true);
        SetSegFormerModelType(SegFormerModelType.B5_640x640);
        Debug.Log("💎 Переключено на SegFormer B5 (высокое качество)");
    }

    /// <summary>
    /// Вернуться к TopFormer/BiSeNet
    /// </summary>
    [ContextMenu("SegFormer: Вернуться к TopFormer/BiSeNet")]
    public void SwitchToLegacyModels()
    {
        EnableSegFormerModels(false);
        Debug.Log("🔄 Возврат к TopFormer/BiSeNet моделям");
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
            // Debug.Log($"🚀 Флагманское устройство ({deviceModel}): используем разрешение 512x512"); // ОТКЛЮЧЕНО: спам
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

    /// <summary>
    /// Преобразование RenderTexture в Texture2D для SAM2
    /// </summary>
    private Texture2D RenderTextureToTexture2D(RenderTexture renderTexture)
    {
        if (renderTexture == null) return null;

        var texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        
        RenderTexture.active = renderTexture;
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;
        
        return texture2D;
    }

    // ===== МЕТОДЫ ДЛЯ ИНТЕГРАЦИИ С DULUX VISUALIZER CORE =====

    /// <summary>
    /// Установка разрешения обработки
    /// </summary>
    public void SetProcessingResolution(Vector2Int resolution)
    {
        processingResolution = resolution;
        Debug.Log($"🔧 Разрешение обработки изменено: {resolution}");
    }

    /// <summary>
    /// Установка пропуска кадров
    /// </summary>
    public void SetFrameSkip(int skipRate)
    {
        frameSkip = Mathf.Clamp(skipRate, 0, 10);
        Debug.Log($"⏭️ Пропуск кадров изменен: {frameSkip}");
    }

    /// <summary>
    /// Настройка сглаживания маски
    /// </summary>
    public void SetMaskSmoothing(bool enabled, int iterations)
    {
        enableMaskSmoothing = enabled;
        maskSmoothingIterations = Mathf.Clamp(iterations, 1, 15);
        Debug.Log($"🎯 Сглаживание маски: {enabled}, итерации: {maskSmoothingIterations}");
    }

    /// <summary>
    /// Переключение SAM2
    /// </summary>
    public void SetUseSAM2(bool enabled)
    {
        useSAM2Models = enabled;
        Debug.Log($"🧠 SAM2 модели: {(enabled ? "включены" : "выключены")}");
    }

    /// <summary>
    /// Получение текущей маски сегментации
    /// </summary>
    public Texture2D GetCurrentMask()
    {
        if (segmentationMaskTexture != null)
        {
            return RenderTextureToTexture2D(segmentationMaskTexture);
        }
        return null;
    }



    /// <summary>
    /// Включение/выключение сегментации
    /// </summary>
    public void SetSegmentationEnabled(bool enabled)
    {
        segmentationEnabled = enabled;
        Debug.Log($"🎯 Сегментация: {(enabled ? "включена" : "выключена")}");
    }

    /// <summary>
    /// Получение статуса инициализации
    /// </summary>
    public bool GetIsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// Получение статуса обработки
    /// </summary>
    public bool GetIsProcessing()
    {
        return isProcessing;
    }



    /// <summary>
    /// Получение текущего цвета покраски
    /// </summary>
    public Color GetPaintColor()
    {
        return paintColor;
    }



    /// <summary>
    /// Получение имени класса по ID
    /// </summary>
    private string GetClassName(int classId)
    {
        if (classNames.ContainsKey(classId))
        {
            return classNames[classId];
        }
        return $"класс {classId}";
    }

    /// <summary>
    /// Переключение в режим показа всех классов
    /// </summary>
    public void SetShowAllClasses(bool show)
    {
        showAllClasses = show;
        if (show)
        {
            selectedClass = -1;
            showWalls = false;
            showFloors = false;
            showCeilings = false;
        }
        
        // Синхронизируем с ARWallPresenter
        if (arWallPresenter != null)
        {
            if (show)
            {
                arWallPresenter.SetAllClassesMode();
            }
            else
            {
                arWallPresenter.SetSingleClassMode(selectedClass, paintColor);
            }
        }
        
        Debug.Log($"🌈 Показ всех классов: {show}");
    }
}

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Управляет визуализацией окрашенной стены в AR.
/// Получает маску сегментации и данные об освещении,
/// а затем передает их в специальный шейдер для фотореалистичного рендеринга.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ARWallPresenter : MonoBehaviour
{
    [Header("Ссылки и настройки")]
    [Tooltip("Материал, использующий шейдер для фотореалистичной окраски стен.")]
    [SerializeField]
    private Material wallPaintMaterial;

    [Tooltip("Базовый цвет краски для визуализации.")]
    [SerializeField]
    private Color paintColor = Color.blue;

    [Tooltip("Насколько мягким будет переход на границе окрашенной области. Использует fwidth-based сглаживание. Рекомендуемые значения: 0.1 (резкие) - 5.0 (максимальные).")]
    [Range(0.1f, 5.0f)]
    [SerializeField]
    private float edgeSoftness = 3.0f; // Увеличено для более мягких краев

    [Tooltip("Ссылка на AR Camera Manager для получения данных об освещении.")]
    [SerializeField]
    private ARCameraManager arCameraManager;

    [Header("Исправление ориентации")]
    [Tooltip("Отразить изображение по горизонтали (если эффект сдвинут влево/вправо)")]
    [SerializeField]
    private bool flipHorizontally = false; // Отключено - координаты исправлены в шейдере

    [Tooltip("Отразить изображение по вертикали (если эффект перевернут вверх ногами)")]
    [SerializeField]
    private bool flipVertically = false; // Отключено - координаты исправлены в шейдере

    [Tooltip("Инвертировать маску (если красится всё кроме стен, включите эту опцию)")]
    [SerializeField]
    private bool invertMask = false; // ОТКЛЮЧЕНО - теперь VisualizeMask.shader правильно показывает только стены

    [Tooltip("Режим смешивания: 0 = Luminance (сохраняет освещение), 1 = Overlay (более яркий эффект).")]
    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float blendMode = 1.0f; // По умолчанию Overlay

    [Header("🧪 Быстрые тесты Edge Softness")]
    [Tooltip("Быстро установить резкие края (0.1)")]
    [SerializeField]
    private bool applySharpEdges = false;

    [Tooltip("Быстро установить умеренные края (1.0)")]
    [SerializeField]
    private bool applySoftEdges = false;

    [Tooltip("Быстро установить мягкие края (2.5)")]
    [SerializeField]
    private bool applyMediumBlur = false;

    [Tooltip("Быстро установить максимальное размытие (5.0)")]
    [SerializeField]
    private bool applyMaxBlur = false;

    [Header("🎨 Быстрые тесты Blend Mode")]
    [Tooltip("Быстро установить Luminance режим (сохраняет освещение)")]
    [SerializeField]
    private bool applyLuminanceMode = false;

    [Tooltip("Быстро установить Overlay режим (яркий эффект)")]
    [SerializeField]
    private bool applyOverlayMode = false;

    [Tooltip("Быстро установить гибридный режим (50/50)")]
    [SerializeField]
    private bool applyHybridMode = false;

    [Header("⚡ Оптимизация производительности")]
    [Tooltip("Использовать оптимизированный шейдер с half precision")]
    [SerializeField]
    private bool useOptimizedShader = false;

    [Tooltip("Ссылка на оптимизированный материал")]
    [SerializeField]
    private Material optimizedMaterial;

    [Tooltip("Ссылка на стандартный материал")]
    [SerializeField]
    private Material standardMaterial;

    [Header("🌟 Продвинутое освещение")]
    [Tooltip("Использовать фотореалистичный шейдер с продвинутым освещением")]
    [SerializeField]
    private bool usePhotorealisticShader = false;

    [Tooltip("Ссылка на фотореалистичный материал")]
    [SerializeField]
    private Material photorealisticMaterial;



    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    // Идентификаторы свойств шейдера для оптимизации
    private static readonly int MaskTexId = Shader.PropertyToID("_SegmentationMask");
    private static readonly int PaintColorId = Shader.PropertyToID("_PaintColor");
    private static readonly int GlobalBrightnessId = Shader.PropertyToID("_GlobalBrightness");
    private static readonly int RealWorldLightColorId = Shader.PropertyToID("_RealWorldLightColor");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int FlipHorizontallyId = Shader.PropertyToID("_FlipHorizontally");
    private static readonly int FlipVerticallyId = Shader.PropertyToID("_FlipVertically");
    private static readonly int InvertMaskId = Shader.PropertyToID("_InvertMask");
    private static readonly int BlendModeId = Shader.PropertyToID("_BlendMode");
    private static readonly int LitPaintColorId = Shader.PropertyToID("_LitPaintColor");

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();

        // Устанавливаем наш специальный материал при запуске
        if (wallPaintMaterial != null)
        {
            _renderer.material = wallPaintMaterial;
        }
        else
        {
            Debug.LogError("Материал для окраски стен (wallPaintMaterial) не назначен!");
            this.enabled = false;
        }

        // Устанавливаем начальный цвет и мягкость краев
        _propertyBlock.SetColor(PaintColorId, paintColor);
        _propertyBlock.SetFloat(EdgeSoftnessId, edgeSoftness);
        _propertyBlock.SetFloat(FlipHorizontallyId, flipHorizontally ? 1.0f : 0.0f);
        _propertyBlock.SetFloat(FlipVerticallyId, flipVertically ? 1.0f : 0.0f);
        _propertyBlock.SetFloat(InvertMaskId, invertMask ? 1.0f : 0.0f);
        _propertyBlock.SetFloat(BlendModeId, blendMode);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    void OnEnable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnFrameReceived;
        }
        else
        {
            Debug.LogError("ARCameraManager не назначен! Данные об освещении не будут обновляться.");
        }
    }

    void Start()
    {
        FitToScreen(); // Растягиваем на весь экран один раз при старте

        // Принудительно убеждаемся, что AsyncSegmentationManager показывает только стены
        var segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager != null)
        {
            segmentationManager.ShowOnlyWalls();
            Debug.Log("🧱 ARWallPresenter принудительно активировал режим только стен");
        }

        // 🚨 ПРИНУДИТЕЛЬНО применяем максимальное сглаживание краев
        edgeSoftness = 5.0f; // Максимальное значение
        SetEdgeSoftness(edgeSoftness);
        Debug.Log($"🎯 ПРИНУДИТЕЛЬНО установлено максимальное сглаживание краев: {edgeSoftness}");
    }

    void OnDisable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnFrameReceived;
        }
    }

    void Update()
    {
        // Обработка быстрых тестов Edge Softness
        if (applySharpEdges)
        {
            applySharpEdges = false;
            SetEdgeSoftness(0.1f);
            // Debug.Log("🔧 Установлены резкие края: 0.1");
        }

        if (applySoftEdges)
        {
            applySoftEdges = false;
            SetEdgeSoftness(1.0f);
            // Debug.Log("🔧 Установлены умеренные края: 1.0");
        }

        if (applyMediumBlur)
        {
            applyMediumBlur = false;
            SetEdgeSoftness(2.5f);
            // Debug.Log("🔧 Установлены мягкие края: 2.5");
        }

        if (applyMaxBlur)
        {
            applyMaxBlur = false;
            SetEdgeSoftness(5.0f);
            // Debug.Log("🔧 Установлено максимальное размытие: 5.0");
        }

        // Обработка быстрых тестов Blend Mode
        if (applyLuminanceMode)
        {
            applyLuminanceMode = false;
            SetBlendMode(0.0f);
            // Debug.Log("🎨 Установлен режим смешивания: Luminance (сохраняет текстуру и освещение)");
        }

        if (applyOverlayMode)
        {
            applyOverlayMode = false;
            SetBlendMode(1.0f);
            // Debug.Log("🎨 Установлен режим смешивания: Overlay (яркий эффект)");
        }

        if (applyHybridMode)
        {
            applyHybridMode = false;
            SetBlendMode(0.5f);
            // Debug.Log("🎨 Установлен режим смешивания: Гибридный (50% Luminance + 50% Overlay)");
        }

        // Обработка переключения шейдеров (по приоритету)
        Material targetMaterial = null;

        if (usePhotorealisticShader && photorealisticMaterial != null)
        {
            targetMaterial = photorealisticMaterial;
        }
        else if (useOptimizedShader && optimizedMaterial != null)
        {
            targetMaterial = optimizedMaterial;
        }
        else if (standardMaterial != null)
        {
            targetMaterial = standardMaterial;
        }

        if (targetMaterial != null && _renderer.material != targetMaterial)
        {
            _renderer.material = targetMaterial;
            // Лог только при реальном изменении материала
        }
    }

    /// <summary>
    /// Этот метод вызывается извне (предположительно AsyncSegmentationManager)
    /// для передачи актуальной маски сегментации.
    /// </summary>
    /// <param name="maskTexture">Текстура с маской стен.</param>
    public void SetSegmentationMask(Texture maskTexture)
    {
        if (maskTexture == null)
        {
            // Иногда может приходить пустая маска, игнорируем ее
            return;
        }

        if (_propertyBlock != null)
        {
            _propertyBlock.SetTexture(MaskTexId, maskTexture);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void OnFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // Используем GetPropertyBlock, чтобы не создавать мусор (GC) каждый кадр
        _renderer.GetPropertyBlock(_propertyBlock);
        bool needsUpdate = false;

        if (eventArgs.lightEstimation.averageBrightness.HasValue)
        {
            float brightness = eventArgs.lightEstimation.averageBrightness.Value;
            // Применяем яркость, только если она больше нуля, чтобы избежать черных вспышек
            if (brightness > 0.01f)
            {
                _propertyBlock.SetFloat(GlobalBrightnessId, brightness);
                needsUpdate = true;
            }
        }

        if (eventArgs.lightEstimation.colorCorrection.HasValue)
        {
            Color colorCorrection = eventArgs.lightEstimation.colorCorrection.Value;
            _propertyBlock.SetColor(RealWorldLightColorId, colorCorrection);
            needsUpdate = true;
        }

        // OPTIMIZATION: Предвычисляем lit paint color на CPU
        if (needsUpdate)
        {
            UpdatePrecomputedLitPaintColor();
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    /// <summary>
    /// OPTIMIZATION: Предвычисление освещенного цвета краски на CPU для снижения нагрузки на GPU
    /// </summary>
    private void UpdatePrecomputedLitPaintColor()
    {
        // Получаем текущие значения
        float globalBrightness = _propertyBlock.GetFloat(GlobalBrightnessId);
        Color realWorldLightColor = _propertyBlock.GetColor(RealWorldLightColorId);

        // Предвычисляем итоговый цвет на CPU
        Color litPaintColor = new Color(
            paintColor.r * realWorldLightColor.r * globalBrightness,
            paintColor.g * realWorldLightColor.g * globalBrightness,
            paintColor.b * realWorldLightColor.b * globalBrightness,
            paintColor.a
        );

        // Передаем предвычисленный результат в шейдер
        _propertyBlock.SetColor(LitPaintColorId, litPaintColor);
    }

    // Этот метод можно использовать для смены цвета из UI
    public void SetPaintColor(Color newColor)
    {
        paintColor = newColor;
        if (_propertyBlock != null)
        {
            _propertyBlock.SetColor(PaintColorId, paintColor);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    public void SetEdgeSoftness(float newSoftness)
    {
        edgeSoftness = newSoftness;
        if (_propertyBlock != null)
        {
            _propertyBlock.SetFloat(EdgeSoftnessId, edgeSoftness);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    /// <summary>
    /// Устанавливает режим смешивания цветов
    /// </summary>
    /// <param name="newBlendMode">Новый режим смешивания (0 = Luminance, 1 = Overlay)</param>
    public void SetBlendMode(float newBlendMode)
    {
        blendMode = Mathf.Clamp01(newBlendMode);
        if (_propertyBlock != null)
        {
            _propertyBlock.SetFloat(BlendModeId, blendMode);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    /// <summary>
    /// Принудительно обновляет размер и позицию плоскости
    /// Вызывайте этот метод, если размер экрана изменился или плоскость отображается неправильно
    /// </summary>
    [ContextMenu("Обновить размер плоскости")]
    public void RefreshScreenFit()
    {
        FitToScreen();
        Debug.Log("🔄 Размер ARWallPresenter принудительно обновлен");
    }

    /// <summary>
    /// Переключает горизонтальное отражение
    /// </summary>
    [ContextMenu("Переключить горизонтальное отражение")]
    public void ToggleFlipHorizontally()
    {
        flipHorizontally = !flipHorizontally;
        if (_propertyBlock != null)
        {
            _propertyBlock.SetFloat(FlipHorizontallyId, flipHorizontally ? 1.0f : 0.0f);
            _renderer.SetPropertyBlock(_propertyBlock);
            Debug.Log($"🔄 Горизонтальное отражение: {(flipHorizontally ? "Включено" : "Выключено")}");
        }
    }

    /// <summary>
    /// Переключает вертикальное отражение
    /// </summary>
    [ContextMenu("Переключить вертикальное отражение")]
    public void ToggleFlipVertically()
    {
        flipVertically = !flipVertically;
        if (_propertyBlock != null)
        {
            _propertyBlock.SetFloat(FlipVerticallyId, flipVertically ? 1.0f : 0.0f);
            _renderer.SetPropertyBlock(_propertyBlock);
            Debug.Log($"🔄 Вертикальное отражение: {(flipVertically ? "Включено" : "Выключено")}");
        }
    }



    /// <summary>
    /// Переключает инверсию маски
    /// </summary>
    [ContextMenu("Переключить инверсию маски")]
    public void ToggleInvertMask()
    {
        invertMask = !invertMask;
        if (_propertyBlock != null)
        {
            _propertyBlock.SetFloat(InvertMaskId, invertMask ? 1.0f : 0.0f);
            _renderer.SetPropertyBlock(_propertyBlock);
            Debug.Log($"🔄 Инверсия маски: {(invertMask ? "Включена" : "Выключена")}");
        }
    }

    /// <summary>
    /// Тестирует разные уровни качества рендеринга
    /// </summary>
    [ContextMenu("Переключить на фотореалистичный шейдер")]
    public void SwitchToPhotorealisticShader()
    {
        usePhotorealisticShader = true;
        useOptimizedShader = false;
        Debug.Log("🌟 Переключен на фотореалистичный шейдер с продвинутым освещением");
    }

    [ContextMenu("Переключить на оптимизированный шейдер")]
    public void SwitchToOptimizedShader()
    {
        usePhotorealisticShader = false;
        useOptimizedShader = true;
        Debug.Log("⚡ Переключен на оптимизированный шейдер");
    }

    [ContextMenu("Переключить на стандартный шейдер")]
    public void SwitchToStandardShader()
    {
        usePhotorealisticShader = false;
        useOptimizedShader = false;
        Debug.Log("🔧 Переключен на стандартный шейдер");
    }

    /// <summary>
    /// Растягивает Quad-объект, чтобы он идеально заполнял экран камеры.
    /// Работает как с ортографической, так и с перспективной проекцией.
    /// </summary>
    private void FitToScreen()
    {
        if (arCameraManager == null)
        {
            Debug.LogWarning("ARCameraManager не назначен!");
            return;
        }

        Camera arCamera = arCameraManager.GetComponent<Camera>();
        if (arCamera == null)
        {
            Debug.LogWarning("Camera component не найден на ARCameraManager!");
            return;
        }

        // Делаем объект дочерним к камере, чтобы он всегда был перед ней
        transform.SetParent(arCameraManager.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Расстояние от камеры (достаточно далеко для покрытия всей сцены)
        // Используем большее расстояние для максимального покрытия AR сцены
        float distance = 10.0f;

        float planeWidth, planeHeight;

        if (arCamera.orthographic)
        {
            // Для ортографической камеры
            planeHeight = arCamera.orthographicSize * 2.0f;
            planeWidth = planeHeight * arCamera.aspect;
        }
        else
        {
            // Для перспективной камеры (стандартно для AR)
            float halfFOV = arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            planeHeight = 2.0f * distance * Mathf.Tan(halfFOV);
            planeWidth = planeHeight * arCamera.aspect;

            // Увеличиваем размер плоскости на 20% для гарантированного покрытия
            planeWidth *= 1.2f;
            planeHeight *= 1.2f;
        }

        transform.localPosition = new Vector3(0, 0, distance);
        transform.localScale = new Vector3(planeWidth, planeHeight, 1);

        Debug.Log($"🖼️ ARWallPresenter размеры: Width={planeWidth:F2}, Height={planeHeight:F2}, Distance={distance:F2}");
        Debug.Log($"📷 Камера: FOV={arCamera.fieldOfView:F1}°, Aspect={arCamera.aspect:F2}, Orthographic={arCamera.orthographic}");
    }
}

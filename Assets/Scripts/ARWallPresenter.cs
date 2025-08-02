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

    [Tooltip("Насколько мягким будет переход на границе окрашенной области. Рекомендуемые значения от 0.05 до 0.5.")]
    [Range(0.01f, 1.0f)]
    [SerializeField]
    private float edgeSoftness = 0.2f; // Увеличили значение по умолчанию

    [Tooltip("Ссылка на AR Camera Manager для получения данных об освещении.")]
    [SerializeField]
    private ARCameraManager arCameraManager;

    [Header("Исправление ориентации")]
    [Tooltip("Отразить изображение по горизонтали (если эффект сдвинут влево/вправо)")]
    [SerializeField]
    private bool flipHorizontally = false;

    [Tooltip("Отразить изображение по вертикали (если эффект перевернут вверх ногами)")]
    [SerializeField]
    private bool flipVertically = false;

    [Tooltip("Инвертировать маску (если красится всё кроме стен, включите эту опцию)")]
    [SerializeField]
    private bool invertMask = true;

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
    }

    void OnDisable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnFrameReceived;
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

        if (eventArgs.lightEstimation.averageBrightness.HasValue)
        {
            float brightness = eventArgs.lightEstimation.averageBrightness.Value;
            // Применяем яркость, только если она больше нуля, чтобы избежать черных вспышек
            if (brightness > 0.01f) 
            {
                _propertyBlock.SetFloat(GlobalBrightnessId, brightness);
            }
        }
        
        if (eventArgs.lightEstimation.colorCorrection.HasValue)
        {
            Color colorCorrection = eventArgs.lightEstimation.colorCorrection.Value;
            _propertyBlock.SetColor(RealWorldLightColorId, colorCorrection);
        }

        _renderer.SetPropertyBlock(_propertyBlock);
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
    /// Тестирует разные значения EdgeSoftness для удобства настройки
    /// </summary>
    [ContextMenu("Тест EdgeSoftness: Мягкие края (0.3)")]
    public void TestSoftEdges()
    {
        SetEdgeSoftness(0.3f);
        Debug.Log("🔧 Установлена мягкость краев: 0.3");
    }

    [ContextMenu("Тест EdgeSoftness: Резкие края (0.05)")]
    public void TestSharpEdges()
    {
        SetEdgeSoftness(0.05f);
        Debug.Log("🔧 Установлена мягкость краев: 0.05");
    }

    [ContextMenu("Тест EdgeSoftness: Максимальное размытие (1.0)")]
    public void TestMaxBlur()
    {
        SetEdgeSoftness(1.0f);
        Debug.Log("🔧 Установлена мягкость краев: 1.0 (максимум)");
    }

    [ContextMenu("Тест EdgeSoftness: Без размытия (0.0)")]
    public void TestNoBlur()
    {
        SetEdgeSoftness(0.0f);
        Debug.Log("🔧 Установлена мягкость краев: 0.0 (без размытия)");
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
        // Используем расстояние ~3 метра для хорошего покрытия AR сцены
        float distance = 3.0f;
        
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
        }

        transform.localPosition = new Vector3(0, 0, distance);
        transform.localScale = new Vector3(planeWidth, planeHeight, 1);
        
        Debug.Log($"🖼️ ARWallPresenter размеры: Width={planeWidth:F2}, Height={planeHeight:F2}, Distance={distance:F2}");
        Debug.Log($"📷 Камера: FOV={arCamera.fieldOfView:F1}°, Aspect={arCamera.aspect:F2}, Orthographic={arCamera.orthographic}");
    }
}

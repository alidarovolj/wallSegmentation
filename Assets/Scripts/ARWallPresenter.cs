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
    [Tooltip("AR Camera Manager для обеспечения совместимости с ARKit. Назначать не обязательно.")]
    [SerializeField]
    private ARCameraManager arCameraManager;

    [Tooltip("Материал, использующий шейдер для визуализации маски.")]
    [SerializeField]
    private Material visualizationMaterial;

    [Header("Режимы визуализации")]
    [Tooltip("Показать все классы разными цветами. Если отключено, будет показан только выбранный класс (обычно стены).")]
    [SerializeField]
    private bool showAllClasses = true; // По умолчанию включен режим мульти-цвет

    [Tooltip("Класс для отображения, если showAllClasses отключен (0=стены, 1=пол, и т.д.).")]
    [SerializeField]
    private int singleClassId = 0;

    [Tooltip("Базовый цвет для режима отображения одного класса.")]
    [SerializeField]
    private Color singleClassColor = Color.blue;

    [Tooltip("Прозрачность наложения маски.")]
    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float opacity = 0.7f;

    [Header("Настройки для реального устройства")]
    [Tooltip("Включить, если приложение запущено на реальном AR-устройстве (не в симуляторе).")]
    [SerializeField]
    private bool isRealDevice = true;

    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    // Идентификаторы свойств шейдера для оптимизации
    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int SelectedClassId = Shader.PropertyToID("_SelectedClass");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int PaintColorId = Shader.PropertyToID("_PaintColor");
    // private static readonly int IsPortraitId = Shader.PropertyToID("_IsPortrait"); // Удалено, управляется DisplayMatrix
    private static readonly int IsRealDeviceId = Shader.PropertyToID("_IsRealDevice");
    private static readonly int DisplayMatrixId = Shader.PropertyToID("_DisplayMatrix");

    // Параметры аспекта для коррекции UV координат
    private static readonly int ScreenAspectId = Shader.PropertyToID("_ScreenAspect");
    private static readonly int MaskAspectId = Shader.PropertyToID("_MaskAspect");
    private static readonly int ForceFullscreenId = Shader.PropertyToID("_ForceFullscreen");

    // Ссылка на сегментационный менеджер для получения пользовательских цветов
    private AsyncSegmentationManager segmentationManager;

    // Кешированные значения для отслеживания изменений экрана
    private int lastScreenWidth;
    private int lastScreenHeight;

    // Кешированная матрица для передачи в шейдер
    private Matrix4x4 displayMatrix = Matrix4x4.identity;

    // Последняя полученная текстура маски (для пересчета аспектов при смене экрана)
    private Texture lastMaskTexture;


    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();

        // Гарантированно находим ARCameraManager
        if (arCameraManager == null)
        {
            arCameraManager = FindObjectOfType<ARCameraManager>();
        }

        if (visualizationMaterial != null)
        {
            _renderer.material = visualizationMaterial;
        }
        else
        {
            Debug.LogError("Материал для визуализации (visualizationMaterial) не назначен!");
            this.enabled = false;
        }
    }

    void Start()
    {
        // Находим AsyncSegmentationManager для получения пользовательских цветов
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

        // Инициализируем значения экрана
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        FitToScreen();
        ApplyShaderProperties();

        // Отладочная информация об аспектах
        float screenAspect = (float)Screen.width / Screen.height;
        Debug.Log($"🚀 ARWallPresenter инициализирован. Screen aspect: {screenAspect:F3} ({Screen.width}x{Screen.height})");
    }

    void OnEnable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnFrameReceived;
        }
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
        // Применяем свойства в Update, чтобы можно было менять в инспекторе в реальном времени
        ApplyShaderProperties();

        // Проверяем изменение размера экрана для динамического обновления на реальных устройствах
        if (isRealDevice && Time.frameCount % 30 == 0) // Проверяем каждые полсекунды
        {
            CheckAndUpdateScreenSize();
        }
    }

    /// <summary>
    /// Этот метод вызывается извне (например, AsyncSegmentationManager)
    /// для передачи актуальной маски сегментации.
    /// </summary>
    public void SetSegmentationMask(Texture maskTexture)
    {
        if (maskTexture != null && _propertyBlock != null)
        {
            lastMaskTexture = maskTexture;
            _propertyBlock.SetTexture(MaskTexId, maskTexture);

            // Вычисляем и устанавливаем параметры аспекта для корректного отображения на весь экран
            // UpdateAspectParameters(maskTexture); // ОТКЛЮЧЕНО: Эта логика теперь полностью заменена DisplayMatrix в шейдере

            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    /// <summary>
    /// Применяет все настраиваемые свойства к шейдеру.
    /// </summary>
    private void ApplyShaderProperties()
    {
        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        _renderer.GetPropertyBlock(_propertyBlock);

        // Устанавливаем режим отображения
        if (showAllClasses)
        {
            _propertyBlock.SetInt(SelectedClassId, -1); // -1 для шейдера означает "показать все"
        }
        else
        {
            _propertyBlock.SetInt(SelectedClassId, singleClassId);
            _propertyBlock.SetColor(PaintColorId, singleClassColor);
        }

        _propertyBlock.SetFloat(OpacityId, opacity);

        // Определяем ориентацию экрана - БОЛЬШЕ НЕ НУЖНО, DisplayMatrix делает это автоматически
        // bool isPortrait = Screen.height > Screen.width;
        // _propertyBlock.SetFloat(IsPortraitId, isPortrait ? 1.0f : 0.0f);

        // Передаем флаг реального устройства и матрицу отображения
        _propertyBlock.SetFloat(IsRealDeviceId, isRealDevice ? 1.0f : 0.0f);
        _propertyBlock.SetMatrix(DisplayMatrixId, displayMatrix);

        // Устанавливаем параметры аспекта для коррекции UV координат
        float screenAspect = (float)Screen.width / Screen.height;
        float maskAspect = 1.0f; // Маска квадратная
        _propertyBlock.SetFloat(ScreenAspectId, screenAspect);
        _propertyBlock.SetFloat(MaskAspectId, maskAspect);
        _propertyBlock.SetInt(ForceFullscreenId, 1); // Включаем полноэкранный режим

        // Включение принудительного полноэкранного режима и установка ротации БОЛЬШЕ НЕ НУЖНЫ
        // _propertyBlock.SetInt(ForceFullscreenId, 1);
        // if (segmentationManager != null)
        // {
        //     int rotationMode = GetMaskRotationModeFromManager();
        //     _propertyBlock.SetInt(RotationModeId, rotationMode);
        // }

        _renderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// Устанавливает цвет для конкретного класса
    /// </summary>
    public void SetClassColor(int classId, Color color)
    {
        if (!showAllClasses)
        {
            // Если показываем один класс, обновляем его цвет
            singleClassId = classId;
            singleClassColor = color;
            ApplyShaderProperties();

            Debug.Log($"🎨 ARWallPresenter: Установлен цвет {ColorUtility.ToHtmlStringRGB(color)} для класса {classId}");
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНИЕ: Устанавливает параметры crop для корректного отображения маски
    /// </summary>
    public void SetCropParameters(float cropOffsetX, float cropOffsetY, float cropScale)
    {
        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        _propertyBlock.SetFloat("_CropOffsetX", cropOffsetX);
        _propertyBlock.SetFloat("_CropOffsetY", cropOffsetY);
        _propertyBlock.SetFloat("_CropScale", cropScale);

        if (_renderer != null)
        {
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        Debug.Log($"📐 ARWallPresenter: Crop параметры установлены - Offset({cropOffsetX:F3}, {cropOffsetY:F3}), Scale: {cropScale:F3}");
    }

    [ContextMenu("Обновить размер плоскости")]
    public void RefreshScreenFit()
    {
        FitToScreen();
    }

    // Удалены все тестовые ContextMenu для очистки кода

    private void FitToScreen()
    {
        Camera arCamera = FindObjectOfType<ARCameraManager>()?.GetComponent<Camera>();
        if (arCamera == null)
        {
            Debug.LogWarning("AR-камера не найдена!");
            return;
        }

        transform.SetParent(arCamera.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Размещаем близко к камере для полного покрытия экрана
        float distance = arCamera.nearClipPlane + 0.01f;

        // Вычисляем размеры экрана в мировых координатах на заданном расстоянии
        float height = 2.0f * distance * Mathf.Tan(arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * arCamera.aspect;

        // Для гарантированного покрытия ВСЕГО экрана, включая статус-бар и панель навигации
        if (isRealDevice)
        {
            // Увеличиваем размер на 30% для экстремального покрытия
            width *= 1.30f;
            height *= 1.30f;

            // На реальных устройствах размещаем плоскость близко для максимального покрытия
            distance = arCamera.nearClipPlane + 0.01f;
        }
        else
        {
            // В симуляторе используем сбалансированное масштабирование
            // Для портретных экранов минимизируем искажения
            float screenAspect = (float)Screen.width / Screen.height;

            if (screenAspect < 1.0f) // Портрет
            {
                width *= 1.08f; // Умеренное горизонтальное растяжение
                height *= 1.12f; // Умеренное вертикальное покрытие
            }
            else // Ландшафт
            {
                width *= 1.12f;
                height *= 1.08f;
            }

            Debug.Log($"🎮 Симулятор: сбалансированное масштабирование - screenAspect={screenAspect:F2}, width={width:F2}, height={height:F2}");
        }

        transform.localPosition = new Vector3(0, 0, distance);
        transform.localScale = new Vector3(width, height, 1);

        Debug.Log($"📐 ARWallPresenter FitToScreen: device={isRealDevice}, distance={distance}, width={width}, height={height}, aspect={arCamera.aspect}");
    }

    /// <summary>
    /// Обновляет параметры аспекта в шейдере для корректного отображения маски на весь экран
    /// </summary>
    private void UpdateAspectParameters(Texture maskTexture)
    {
        // ЭТА ФУНКЦИЯ БОЛЬШЕ НЕ ИСПОЛЬЗУЕТСЯ. ВСЯ ЛОГИКА ПЕРЕНЕСЕНА В ШЕЙДЕР И УПРАВЛЯЕТСЯ _DisplayMatrix
        return;

        /*
        if (maskTexture == null) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float maskAspect = (float)maskTexture.width / maskTexture.height;

        // Получаем режим поворота из AsyncSegmentationManager
        int rotationMode = GetMaskRotationModeFromManager();

        // Учитываем поворот маски при вычислении аспекта
        // Если поворот на 90 или -90 градусов, меняем местами ширину и высоту
        if (rotationMode == 0 || rotationMode == 1)
        {
            maskAspect = 1.0f / maskAspect; // Инвертируем аспект для поворота на 90°
        }

        // Возвращаем простую и правильную логику расчета
        float aspectRatio = screenAspect / maskAspect;

        // Применяем параметры к PropertyBlock
        if (_propertyBlock != null)
        {
            _propertyBlock.SetFloat(ScreenAspectId, screenAspect);
            _propertyBlock.SetFloat(MaskAspectId, maskAspect);
            _propertyBlock.SetFloat(AspectRatioId, aspectRatio);
            _propertyBlock.SetInt(RotationModeId, rotationMode);

            // Отладочная информация
            string deviceType = isRealDevice ? "Реальное устройство" : "Симулятор";
            Debug.Log($"📱 {deviceType} - ARWallPresenter Aspect: Screen={screenAspect:F2}, Mask={maskAspect:F2} (rotation {rotationMode}), Ratio={aspectRatio:F2}");
        }
        */
    }

    /// <summary>
    /// Получает режим поворота маски из AsyncSegmentationManager
    /// </summary>
    private int GetMaskRotationModeFromManager()
    {
        if (segmentationManager != null)
        {
            // Просто возвращаем значение из менеджера. Вся логика будет в шейдере.
            return segmentationManager.GetMaskRotationMode();
        }

        // По умолчанию для реального устройства
        return 0;
    }

    private void OnFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (eventArgs.displayMatrix.HasValue)
        {
            displayMatrix = eventArgs.displayMatrix.Value;
        }
    }

    /// <summary>
    /// Проверяет изменение размера экрана и обновляет плоскость при необходимости
    /// </summary>
    private void CheckAndUpdateScreenSize()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            Debug.Log($"📱 Обнаружено изменение размера экрана: {lastScreenWidth}x{lastScreenHeight} → {Screen.width}x{Screen.height}");

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            // Обновляем размер плоскости
            FitToScreen();

            // Если у нас уже есть маска, обновляем её параметры аспекта
            // if (_propertyBlock != null && lastMaskTexture != null)
            // {
            // UpdateAspectParameters(lastMaskTexture); // ОТКЛЮЧЕНО
            // _renderer.SetPropertyBlock(_propertyBlock);
            // }
        }
    }
}

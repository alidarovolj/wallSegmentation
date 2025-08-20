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

    // Crop параметры для коррекции UV координат
    private static readonly int CropOffsetXId = Shader.PropertyToID("_CropOffsetX");
    private static readonly int CropOffsetYId = Shader.PropertyToID("_CropOffsetY");
    private static readonly int CropScaleId = Shader.PropertyToID("_CropScale");

    // Параметр поворота маски
    private static readonly int RotationModeId = Shader.PropertyToID("_RotationMode");

    // Параметр горизонтального отражения
    private static readonly int FlipHorizontalId = Shader.PropertyToID("_FlipHorizontal");

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

        // АВТОМАТИЧЕСКОЕ ОПРЕДЕЛЕНИЕ СРЕДЫ: В редакторе всегда считаем, что это не реальное устройство
#if UNITY_EDITOR
        isRealDevice = false;
#endif

        // Устанавливаем слой, который точно будет виден в симуляторе
        gameObject.layer = LayerMask.NameToLayer("Default");

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
        Debug.Log($"📐 Aspect correction: screenAspect={screenAspect:F3}, maskAspect=1.0, режим={(screenAspect < 1.0 ? "портрет" : "ландшафт")}");
        Debug.Log($"🔧 ARWallPresenter состояние: GameObject.active={gameObject.activeInHierarchy}, Component.enabled={enabled}, Renderer.enabled={_renderer.enabled}");
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

        // ИСПРАВЛЕНИЕ: Проверяем корректность иерархии каждые 2 секунды
        if (Time.frameCount % 120 == 0)
        {
            ValidateHierarchy();
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

            Debug.Log($"✅ ARWallPresenter: Маска сегментации получена и установлена в шейдер! Размер: {maskTexture.width}x{maskTexture.height}");
        }
        else
        {
            if (maskTexture == null)
                Debug.LogWarning("⚠️ ARWallPresenter: Получена пустая маска сегментации!");
            if (_propertyBlock == null)
                Debug.LogWarning("⚠️ ARWallPresenter: PropertyBlock не инициализирован!");
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

        // Передаем режим поворота маски
        int rotationMode = GetMaskRotationModeFromManager();
        _propertyBlock.SetInt(RotationModeId, rotationMode);

        // Передаем настройку горизонтального отражения
        bool flipHorizontal = GetFlipHorizontalFromManager();
        _propertyBlock.SetFloat(FlipHorizontalId, flipHorizontal ? 1.0f : 0.0f);

        // Устанавливаем параметры аспекта для коррекции UV координат
        float screenAspect = (float)Screen.width / Screen.height;
        float maskAspect = 1.0f; // По умолчанию квадратная, будет обновлена при получении маски
        if (lastMaskTexture != null)
        {
            maskAspect = (float)lastMaskTexture.width / lastMaskTexture.height;
            // Учитываем поворот: для 180° аспект не меняется, для 90°/-90° - инвертируется
            int currentRotationMode = GetMaskRotationModeFromManager();
            if (currentRotationMode == 0 || currentRotationMode == 1) // +90° или -90°
            {
                maskAspect = 1.0f / maskAspect;
            }
        }
        _propertyBlock.SetFloat(ScreenAspectId, screenAspect);
        _propertyBlock.SetFloat(MaskAspectId, maskAspect);
        _propertyBlock.SetInt(ForceFullscreenId, 1); // Включаем полноэкранный режим

        // Логируем изредка для отладки
        if (Time.frameCount < 5 || Time.frameCount % 300 == 0)
        {
            Debug.Log($"📊 ARWallPresenter передает в шейдер: screenAspect={screenAspect:F3}, maskAspect={maskAspect:F1}");
        }

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
        // ИСПРАВЛЕНИЕ: Принудительно переключаемся в режим одного класса при установке цвета
        showAllClasses = false;
        singleClassId = classId;
        singleClassColor = color;
        ApplyShaderProperties();

        Debug.Log($"🎨 ARWallPresenter: Принудительно установлен цвет {ColorUtility.ToHtmlStringRGB(color)} для класса {classId}, режим showAllClasses=false");
    }

    /// <summary>
    /// Принудительно переключает в режим показа только указанного класса
    /// </summary>
    public void SetSingleClassMode(int classId, Color color)
    {
        showAllClasses = false;
        singleClassId = classId;
        singleClassColor = color;
        ApplyShaderProperties();

        Debug.Log($"🎯 ARWallPresenter: Включен режим одного класса - ID: {classId}, цвет: {ColorUtility.ToHtmlStringRGB(color)}");
    }

    /// <summary>
    /// Возвращает в режим показа всех классов
    /// </summary>
    public void SetAllClassesMode()
    {
        showAllClasses = true;
        ApplyShaderProperties();

        Debug.Log($"🌈 ARWallPresenter: Включен режим всех классов");
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

        _propertyBlock.SetFloat(CropOffsetXId, cropOffsetX);
        _propertyBlock.SetFloat(CropOffsetYId, cropOffsetY);
        _propertyBlock.SetFloat(CropScaleId, cropScale);

        if (_renderer != null)
        {
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        Debug.Log($"📐 ARWallPresenter: Crop параметры установлены - Offset({cropOffsetX:F3}, {cropOffsetY:F3}), Scale: {cropScale:F3}");
        Debug.Log($"🔍 ARWallPresenter: PropertyBlock установлен на renderer={(_renderer != null ? "OK" : "NULL")}");
    }

    [ContextMenu("Обновить размер плоскости")]
    public void RefreshScreenFit()
    {
        FitToScreen();
    }

    // Удалены все тестовые ContextMenu для очистки кода

    private void FitToScreen()
    {
        Camera arCamera = Camera.main;

        if (arCamera != null)
        {
            Debug.Log($"[ARWallPresenter] Найдена основная камера (Camera.main): '{arCamera.name}'. Активна: {arCamera.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogWarning("[ARWallPresenter] Camera.main вернула null. Основная камера не найдена или неактивна. Ищем AR-камеру...");
            arCamera = FindObjectOfType<ARCameraManager>()?.GetComponent<Camera>();
            if (arCamera != null)
            {
                Debug.Log($"[ARWallPresenter] Найдена AR-камера через FindObjectOfType<ARCameraManager>: '{arCamera.name}'. Активна: {arCamera.gameObject.activeInHierarchy}");
            }
        }

        if (arCamera == null)
        {
            Debug.LogError("[ARWallPresenter] Не удалось найти НИ ОДНОЙ подходящей камеры (ни MainCamera, ни ARCamera). Плоскость не может быть отображена. Проверьте теги и активность камер в сцене.");
            return;
        }

        // ИСПРАВЛЕНИЕ: Убеждаемся, что родитель установлен корректно
        if (transform.parent != arCamera.transform)
        {
            transform.SetParent(arCamera.transform, false);
            Debug.Log($"🔗 ARWallPresenter: Установлен родитель '{arCamera.name}'");
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Размещаем близко к камере для полного покрытия экрана
        float distance = arCamera.nearClipPlane + 0.01f;

        // ИСПРАВЛЕНИЕ: В симуляторе размещаем плоскость дальше для правильного соответствия
        if (!isRealDevice)
        {
            distance = 1.0f; // В симуляторе размещаем на расстоянии 1 единица
        }

        // Вычисляем размеры экрана в мировых координатах на заданном расстоянии
        float height = 2.0f * distance * Mathf.Tan(arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * arCamera.aspect;

        // Для гарантированного покрытия ВСЕГО экрана, включая статус-бар и панель навигации
        float screenAspect = (float)Screen.width / Screen.height;

        if (isRealDevice)
        {
            // ИСПРАВЛЕНИЕ: Оптимальное масштабирование для покрытия экрана без приближения
            width *= 1.0f;  // Золотая середина между 1.10f (мало) и 2.20f (много)
            height *= 1.0f; // Золотая середина между 1.10f (мало) и 2.20f (много)

            // На реальных устройствах размещаем плоскость близко для максимального покрытия
            distance = arCamera.nearClipPlane + 0.01f;

            Debug.Log($"📱 Реальное устройство: оптимальное покрытие экрана - screenAspect={screenAspect:F3}");
        }
        else
        {
            // В симуляторе НЕ масштабируем - точное соответствие frustum камеры
            width *= 1.0f;   // Точное соответствие размерам frustum
            height *= 1.0f;  // Точное соответствие размерам frustum

            Debug.Log($"🎮 Симулятор: точное соответствие frustum камеры - screenAspect={screenAspect:F3}");
        }

        // ИСПРАВЛЕНИЕ: Принудительно размещаем объект ПЕРЕД камерой
        Vector3 targetLocalPosition = new Vector3(0, 0, -distance);
        transform.localPosition = targetLocalPosition;
        transform.localScale = new Vector3(width, height, 1);

        // Дополнительная проверка позиционирования
        Vector3 worldPos = transform.position;
        Vector3 cameraPos = arCamera.transform.position;
        Vector3 directionToObject = (worldPos - cameraPos).normalized;
        float dotProduct = Vector3.Dot(directionToObject, arCamera.transform.forward);

        if (dotProduct <= 0)
        {
            Debug.LogWarning($"⚠️ ПРОБЛЕМА: Объект все еще позади камеры после позиционирования! dotProduct={dotProduct:F3}");
            Debug.LogWarning($"📍 Принудительно корректируем позицию...");

            // Принудительно размещаем объект точно перед камерой
            transform.position = arCamera.transform.position + arCamera.transform.forward * distance;
        }

        Debug.Log($"📐 ARWallPresenter FitToScreen: device={isRealDevice}, distance={distance}, width={width}, height={height}, aspect={arCamera.aspect}");

        // ДОПОЛНИТЕЛЬНАЯ ДИАГНОСТИКА: Обновляем переменные после возможной коррекции  
        worldPos = transform.position;
        cameraPos = arCamera.transform.position;
        Vector3 cameraForward = arCamera.transform.forward;
        float distanceToCamera = Vector3.Distance(worldPos, cameraPos);
        bool isInFrontOfCamera = Vector3.Dot((worldPos - cameraPos).normalized, cameraForward) > 0;
        // ИСПРАВЛЕНИЕ: В локальных координатах отрицательный Z означает "перед камерой"
        bool isInFrontLocal = transform.localPosition.z < 0;

        Debug.Log($"🌍 [ARWallPresenter] Мировые координаты: Объект={worldPos}, Камера={cameraPos}");
        Debug.Log($"📏 [ARWallPresenter] Расстояние до камеры: {distanceToCamera:F3}, Перед камерой (мировые): {isInFrontOfCamera}, Перед камерой (локальные): {isInFrontLocal}");
        Debug.Log($"🎯 [ARWallPresenter] Локальная позиция: {transform.localPosition}, Масштаб: {transform.localScale}");

        // ДОПОЛНИТЕЛЬНАЯ ДИАГНОСТИКА: Показываем все камеры в сцене
        Camera[] allCameras = FindObjectsOfType<Camera>();
        Debug.Log($"🎥 [ARWallPresenter] Всего камер в сцене: {allCameras.Length}");
        for (int i = 0; i < allCameras.Length; i++)
        {
            Camera cam = allCameras[i];
            string tagInfo = cam.gameObject.tag;
            string activeInfo = cam.gameObject.activeInHierarchy ? "АКТИВНА" : "неактивна";
            string enabledInfo = cam.enabled ? "включена" : "выключена";
            Debug.Log($"🎥 Камера {i}: '{cam.name}' (тег: {tagInfo}, {activeInfo}, {enabledInfo}, глубина: {cam.depth})");
        }
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

    /// <summary>
    /// Получает настройку горизонтального отражения из AsyncSegmentationManager
    /// </summary>
    private bool GetFlipHorizontalFromManager()
    {
        if (segmentationManager != null)
        {
            return segmentationManager.GetFlipHorizontal();
        }

        // По умолчанию отражение выключено
        return false;
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

    /// <summary>
    /// Проверяет корректность иерархии объектов
    /// </summary>
    private void ValidateHierarchy()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<ARCameraManager>()?.GetComponent<Camera>();
        }

        if (mainCamera != null && transform.parent != mainCamera.transform)
        {
            Debug.LogWarning($"⚠️ ARWallPresenter: Родитель потерялся! Восстанавливаем связь с '{mainCamera.name}'");
            FitToScreen(); // Принудительно пересоздаем иерархию
        }

        // Проверим, что объект находится перед камерой
        if (mainCamera != null)
        {
            Vector3 toObject = (transform.position - mainCamera.transform.position).normalized;
            float dot = Vector3.Dot(toObject, mainCamera.transform.forward);

            if (dot <= 0)
            {
                Debug.LogWarning($"⚠️ ARWallPresenter: Объект находится позади камеры! dot={dot:F3}");
                Debug.LogWarning($"📍 Позиции: Объект={transform.position}, Камера={mainCamera.transform.position}");
                Debug.LogWarning($"🔄 Локальная позиция: {transform.localPosition}");
            }
        }
    }
}

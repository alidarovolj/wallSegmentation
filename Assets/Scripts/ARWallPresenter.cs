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
    private static readonly int IsPortraitId = Shader.PropertyToID("_IsPortrait");
    private static readonly int IsRealDeviceId = Shader.PropertyToID("_IsRealDevice");

    // Ссылка на сегментационный менеджер для получения пользовательских цветов
    private AsyncSegmentationManager segmentationManager;


    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();

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

        FitToScreen();
        ApplyShaderProperties();
    }

    void OnEnable()
    {
        // Подписки на события, если нужны
    }

    void OnDisable()
    {
        // Отписки от событий, если нужны
    }

    void Update()
    {
        // Применяем свойства в Update, чтобы можно было менять в инспекторе в реальном времени
        ApplyShaderProperties();
    }

    /// <summary>
    /// Этот метод вызывается извне (например, AsyncSegmentationManager)
    /// для передачи актуальной маски сегментации.
    /// </summary>
    public void SetSegmentationMask(Texture maskTexture)
    {
        if (maskTexture != null && _propertyBlock != null)
        {
            _propertyBlock.SetTexture(MaskTexId, maskTexture);
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

        // Определяем ориентацию экрана
        bool isPortrait = Screen.height > Screen.width;
        _propertyBlock.SetFloat(IsPortraitId, isPortrait ? 1.0f : 0.0f);

        // Передаем флаг реального устройства
        _propertyBlock.SetFloat(IsRealDeviceId, isRealDevice ? 1.0f : 0.0f);

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

    [ContextMenu("Обновить размер плоскости")]
    public void RefreshScreenFit()
    {
        FitToScreen();
    }

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

        float distance = arCamera.nearClipPlane + 0.1f; // Размещаем близко к камере
        float height = 2.0f * distance * Mathf.Tan(arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * arCamera.aspect;

        transform.localPosition = new Vector3(0, 0, distance);
        transform.localScale = new Vector3(width, height, 1);
    }
}

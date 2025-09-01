using UnityEngine;

/// <summary>
/// Интегратор для автоматической настройки ARMaskInteraction с существующими системами
/// Находит и подключает все необходимые компоненты автоматически
/// </summary>
public class ARMaskInteractionIntegrator : MonoBehaviour
{
    [Header("Автоматическая интеграция")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private bool replaceExistingClickHandlers = false;
    
    [Header("Настройки маркеров")]
    [SerializeField] private Material markerMaterial;
    [SerializeField] private float markerSize = 0.03f; // 3см
    
    [Header("Отладка")]
    [SerializeField] private bool enableDebugLogs = true;

    private ARMaskInteraction arMaskInteraction;

    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupARMaskInteraction();
        }
    }

    /// <summary>
    /// Автоматически настраивает ARMaskInteraction компонент
    /// </summary>
    [ContextMenu("Настроить AR Mask Interaction")]
    public void SetupARMaskInteraction()
    {
        LogDebug("🔧 Начинаем автоматическую настройку ARMaskInteraction...");

        // 1. Добавляем ARMaskInteraction компонент если его нет
        arMaskInteraction = GetComponent<ARMaskInteraction>();
        if (arMaskInteraction == null)
        {
            arMaskInteraction = gameObject.AddComponent<ARMaskInteraction>();
            LogDebug("➕ Добавлен компонент ARMaskInteraction");
        }

        // 2. Находим и подключаем зависимости через рефлексию
        ConnectDependencies();

        // 3. Создаем и настраиваем префаб маркера
        CreateMarkerPrefab();

        // 4. Отключаем конфликтующие скрипты если нужно
        if (replaceExistingClickHandlers)
        {
            DisableConflictingScripts();
        }

        LogDebug("✅ Автоматическая настройка ARMaskInteraction завершена!");
    }

    /// <summary>
    /// Подключает все необходимые зависимости
    /// </summary>
    private void ConnectDependencies()
    {
        // Используем рефлексию для доступа к приватным полям ARMaskInteraction
        var type = typeof(ARMaskInteraction);
        
        // AsyncSegmentationManager
        var segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager != null)
        {
            SetPrivateField("segmentationManager", segmentationManager);
            LogDebug("🔗 Подключен AsyncSegmentationManager");
        }

        // AROcclusionManager  
        var occlusionManager = FindObjectOfType<UnityEngine.XR.ARFoundation.AROcclusionManager>();
        if (occlusionManager != null)
        {
            SetPrivateField("occlusionManager", occlusionManager);
            LogDebug("🔗 Подключен AROcclusionManager");
        }

        // ARCameraManager
        var cameraManager = FindObjectOfType<UnityEngine.XR.ARFoundation.ARCameraManager>();
        if (cameraManager != null)
        {
            SetPrivateField("cameraManager", cameraManager);
            
            // AR Camera
            var arCamera = cameraManager.GetComponent<Camera>();
            if (arCamera != null)
            {
                SetPrivateField("arCamera", arCamera);
                LogDebug("🔗 Подключен ARCameraManager и AR Camera");
            }
        }

        // Создаем контейнер для маркеров
        var markersParent = transform.Find("3D_Markers");
        if (markersParent == null)
        {
            var markerContainer = new GameObject("3D_Markers");
            markerContainer.transform.SetParent(transform);
            markersParent = markerContainer.transform;
            LogDebug("📁 Создан контейнер для маркеров");
        }
        SetPrivateField("markersParent", markersParent);
    }

    /// <summary>
    /// Создает префаб маркера
    /// </summary>
    private void CreateMarkerPrefab()
    {
        // Создаем простую сферу как маркер
        GameObject markerPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerPrefab.name = "AR_Marker_Prefab";
        markerPrefab.transform.localScale = Vector3.one * markerSize;

        // Убираем коллайдер
        var collider = markerPrefab.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);

        // Настраиваем материал
        var renderer = markerPrefab.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (markerMaterial != null)
            {
                renderer.material = markerMaterial;
            }
            else
            {
                // Создаем простой материал с эмиссией
                Material defaultMaterial = new Material(Shader.Find("Standard"));
                defaultMaterial.color = Color.red;
                defaultMaterial.EnableKeyword("_EMISSION");
                defaultMaterial.SetColor("_EmissionColor", Color.red * 0.5f);
                renderer.material = defaultMaterial;
                LogDebug("🎨 Создан стандартный материал маркера");
            }
        }

        // Добавляем простую анимацию (пульсация)
        var pulseAnimation = markerPrefab.AddComponent<MarkerPulseAnimation>();

        // Сохраняем как префаб в памяти (не в файловой системе)
        markerPrefab.SetActive(false);
        
        // Подключаем к ARMaskInteraction
        SetPrivateField("markerPrefab", markerPrefab);
        LogDebug("📍 Создан префаб маркера");
    }

    /// <summary>
    /// Отключает конфликтующие скрипты обработки кликов
    /// </summary>
    private void DisableConflictingScripts()
    {
        // Отключаем SimpleClickPainter если есть
        var simpleClickPainter = FindObjectOfType<SimpleClickPainter>();
        if (simpleClickPainter != null)
        {
            simpleClickPainter.enabled = false;
            LogDebug("⏸️ Отключен SimpleClickPainter");
        }

        // Отключаем другие скрипты обработки кликов
        var segmentationManager = FindObjectOfType<SegmentationManager>();
        if (segmentationManager != null)
        {
            // Можно добавить логику отключения HandleTap в SegmentationManager
            LogDebug("⚠️ Найден SegmentationManager - проверьте конфликты с HandleTap");
        }
    }

    /// <summary>
    /// Устанавливает значение приватного поля через рефлексию
    /// </summary>
    private void SetPrivateField(string fieldName, object value)
    {
        if (arMaskInteraction == null) return;

        var field = typeof(ARMaskInteraction).GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(arMaskInteraction, value);
        }
        else
        {
            LogDebug($"⚠️ Поле '{fieldName}' не найдено в ARMaskInteraction");
        }
    }

    /// <summary>
    /// Логирование с проверкой флага
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[ARMaskInteractionIntegrator] {message}");
    }

    // Публичные методы для управления

    /// <summary>
    /// Переключает взаимодействие
    /// </summary>
    public void ToggleInteraction()
    {
        if (arMaskInteraction != null)
        {
            bool currentState = arMaskInteraction.enabled;
            arMaskInteraction.SetInteractionEnabled(!currentState);
            LogDebug($"🎮 Взаимодействие переключено: {!currentState}");
        }
    }

    /// <summary>
    /// Очищает все маркеры
    /// </summary>
    public void ClearMarkers()
    {
        if (arMaskInteraction != null)
        {
            arMaskInteraction.ClearAllMarkers();
        }
    }

    /// <summary>
    /// Получает ссылку на ARMaskInteraction
    /// </summary>
    public ARMaskInteraction GetARMaskInteraction()
    {
        return arMaskInteraction;
    }
}

/// <summary>
/// Простая анимация пульсации для маркеров
/// </summary>
public class MarkerPulseAnimation : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.2f;
    
    private Vector3 originalScale;

    private void Start()
    {
        originalScale = transform.localScale;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        transform.localScale = originalScale * pulse;
    }
}

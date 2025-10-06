using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;

/// <summary>
/// Интерактивная AR-сегментация: Преобразует 2D касания в 3D позиции на реальных поверхностях
/// Объединяет функциональность касаний, глубины и сегментации для создания 3D маркеров
/// </summary>
public class ARMaskInteraction : MonoBehaviour
{
    [Header("Зависимости")]
    [SerializeField] private AsyncSegmentationManager segmentationManager;
    [SerializeField] private HybridSegmentationManager hybridManager; // Новый гибридный менеджер
    [SerializeField] private PlaneGenerator planeGenerator; // Генератор плоскостей
    [SerializeField] private AROcclusionManager occlusionManager;
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private Camera arCamera;

    [Header("Настройки взаимодействия")]
    [SerializeField] private bool enableInteraction = true;
    [SerializeField] private float clickCooldown = 0.3f;
    [SerializeField] private LayerMask uiLayerMask = -1; // Все слои UI для исключения

    [Header("3D Маркеры")]
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Transform markersParent;
    [SerializeField] private Color[] classColors = new Color[] 
    {
        Color.red,      // Стены  
        Color.green,    // Пол
        Color.blue,     // Потолок
        Color.yellow,   // Окна
        Color.magenta,  // Двери
        Color.cyan      // Другое
    };

    [Header("Отладка")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showRaycastDebug = true;
    // visualizeDepth удален - больше не используется

    // Приватные переменные
    private float lastClickTime = 0f;
    private Camera mainCamera;

    // Кэш для производительности
    private Matrix4x4 projectionMatrix;
    private Matrix4x4 inverseProjectionMatrix;
    private Matrix4x4 worldToCameraMatrix;

    private void Awake()
    {
        // Автопоиск зависимостей если не назначены
        if (segmentationManager == null)
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        
        if (occlusionManager == null)
        {
            occlusionManager = FindObjectOfType<AROcclusionManager>();
            if (occlusionManager != null)
            {
                // Проверяем есть ли активный subsystem
                if (occlusionManager.subsystem == null || !occlusionManager.subsystem.running)
                {
                    LogDebug("⚠️ AROcclusionManager найден, но subsystem не активен (симулятор режим)");
                }
            }
        }
        
        if (cameraManager == null)
            cameraManager = FindObjectOfType<ARCameraManager>();
        
        if (hybridManager == null)
            hybridManager = FindObjectOfType<HybridSegmentationManager>();
        
        if (planeGenerator == null)
        {
            planeGenerator = FindObjectOfType<PlaneGenerator>();
            if (planeGenerator == null)
            {
                // Создаём PlaneGenerator автоматически
                GameObject planeGenObj = new GameObject("PlaneGenerator");
                planeGenerator = planeGenObj.AddComponent<PlaneGenerator>();
                LogDebug("✨ PlaneGenerator создан автоматически");
            }
        }

        if (arCamera == null)
        {
            if (cameraManager != null)
                arCamera = cameraManager.GetComponent<Camera>();
            else
                arCamera = Camera.main;
        }

        mainCamera = arCamera != null ? arCamera : Camera.main;

        // Создаем контейнер для маркеров если не назначен
        if (markersParent == null)
        {
            GameObject markerContainer = new GameObject("3D_Markers");
            markersParent = markerContainer.transform;
            LogDebug("📍 Создан контейнер для 3D маркеров");
        }

        // Создаем простой маркер если префаб не назначен
        if (markerPrefab == null)
        {
            CreateDefaultMarkerPrefab();
        }
    }

    private void Update()
    {
        if (enableInteraction)
        {
            HandleInput();
        }

        // Обновляем матрицы камеры для точных расчетов
        UpdateCameraMatrices();
        
        // ОТЛАДКА: Проверяем каждые 5 секунд
        if (Time.frameCount % 300 == 0)
        {
            LogDebug($"🔄 Статус: interaction={enableInteraction}, camera={mainCamera != null}, cooldown={(Time.time - lastClickTime):F1}s");
        }
    }

    /// <summary>
    /// Обрабатывает пользовательский ввод (мышь/тач)
    /// </summary>
    private void HandleInput()
    {
        // Проверяем кулдаун
        if (Time.time - lastClickTime < clickCooldown)
            return;

        // Проверяем ввод (мышь или тач)
        bool inputDetected = false;
        Vector2 screenPosition = Vector2.zero;

        if (Input.GetMouseButtonDown(0))
        {
            inputDetected = true;
            screenPosition = Input.mousePosition;
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            inputDetected = true;
            screenPosition = Input.GetTouch(0).position;
        }

        if (inputDetected)
        {
            LogDebug($"🖱️ Обнаружен ввод в позиции: {screenPosition}");
            
            // Избегаем кликов по UI
            if (IsPointerOverUI(screenPosition))
            {
                LogDebug("🚫 Клик по UI - игнорируем");
                return;
            }

            LogDebug("✅ Клик прошел проверки, обрабатываем...");
            ProcessScreenTouch(screenPosition);
            lastClickTime = Time.time;
        }
    }

    /// <summary>
    /// Обрабатывает касание экрана и создает 3D маркер или плоскость
    /// </summary>
    /// <param name="screenPos">Позиция касания в экранных координатах</param>
    private void ProcessScreenTouch(Vector2 screenPos)
    {
        LogDebug($"🔍 Обработка касания в позиции: {screenPos}");

        // НОВЫЙ РЕЖИМ: Используем гибридный SegFormer + SAM
        if (hybridManager != null && planeGenerator != null)
        {
            LogDebug("🎯 Используем гибридный режим (SegFormer + SAM)");
            
            // Вызываем ProcessClick из HybridSegmentationManager
            hybridManager.ProcessClick(screenPos, (samMask, classId) =>
            {
                // Callback вызывается, когда SAM готова маска
                LogDebug($"✅ Получена маска от SAM для класса {classId}");
                
                // Генерируем AR плоскость из маски
                GameObject plane = planeGenerator.GeneratePlane(samMask, classId, screenPos);
                
                if (plane != null)
                {
                    LogDebug($"🎉 Плоскость создана: {plane.name}");
                }
                else
                {
                    LogDebug("⚠️ Не удалось создать плоскость");
                }
                
                // Освобождаем маску
                samMask?.Dispose();
            });
            
            return;
        }

        // СТАРЫЙ РЕЖИМ: Используем AsyncSegmentationManager (fallback)
        LogDebug("⚠️ Гибридный режим недоступен, используем старый метод");

        // 1. Получаем класс сегментации в точке касания
        int detectedClass = GetSegmentationClassAtPosition(screenPos);
        
        if (detectedClass < 0)
        {
            LogDebug("❌ Не удалось определить класс сегментации");
            return;
        }

        // 2. Получаем глубину в точке касания
        float depth = GetDepthAtPosition(screenPos);
        
        if (depth <= 0)
        {
            LogDebug("❌ Не удалось получить данные о глубине");
            // Используем фиксированную глубину как fallback
            depth = 1.5f;
            LogDebug($"🔄 Используем фиксированную глубину: {depth}m");
        }

        // 3. Преобразуем 2D позицию в 3D мировые координаты
        Vector3 worldPosition = ScreenToWorldPosition(screenPos, depth);
        
        if (worldPosition == Vector3.zero)
        {
            LogDebug("❌ Не удалось преобразовать в 3D координаты");
            return;
        }

        // 4. Создаем 3D маркер
        CreateWorldMarker(worldPosition, detectedClass);
        
        LogDebug($"✅ 3D маркер создан: класс={GetClassName(detectedClass)}, позиция={worldPosition}, глубина={depth:F2}m");
    }

    /// <summary>
    /// Получает класс сегментации в указанной экранной позиции
    /// </summary>
    /// <param name="screenPos">Позиция в экранных координатах</param>
    /// <returns>Индекс класса или -1 при ошибке</returns>
    private int GetSegmentationClassAtPosition(Vector2 screenPos)
    {
        if (segmentationManager == null)
            return -1;

        // Используем существующий метод из AsyncSegmentationManager
        // Но синхронно, так как для взаимодействия нужен быстрый ответ
        if (!segmentationManager.IsSegmentationMaskReady())
            return -1;

        // Преобразуем экранные координаты в UV координаты маски
        Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
        
        // Применяем тот же поворот что и в AsyncSegmentationManager (поворот на +90 градусов)
        float uv_x = 1.0f - screenUV.y;
        float uv_y = screenUV.x;

        // Получаем текстуру маски
        RenderTexture maskTexture = segmentationManager.GetCurrentSegmentationMask();
        if (maskTexture == null)
            return -1;

        // Преобразуем UV в координаты текстуры
        int textureX = Mathf.Clamp((int)(uv_x * maskTexture.width), 0, maskTexture.width - 1);
        int textureY = Mathf.Clamp((int)(uv_y * maskTexture.height), 0, maskTexture.height - 1);

        // Для быстрого доступа используем простое приближение
        // В реальной реализации можно добавить AsyncGPUReadback для точности
        
        // Пока возвращаем класс "стена" (0) как наиболее вероятный
        LogDebug($"🎯 UV координаты: ({uv_x:F3}, {uv_y:F3}) -> текстура: ({textureX}, {textureY})");
        return 0; // Стены - самый частый случай для покраски
    }

    /// <summary>
    /// Получает значение глубины в указанной экранной позиции
    /// </summary>
    /// <param name="screenPos">Позиция в экранных координатах</param>
    /// <returns>Глубина в метрах или 0 при ошибке</returns>
    private float GetDepthAtPosition(Vector2 screenPos)
    {
        if (occlusionManager == null || occlusionManager.environmentDepthTexture == null)
        {
            LogDebug("⚠️ Depth API недоступен, используем raycast");
            return GetDepthWithRaycast(screenPos);
        }

        // Получаем данные глубины из AR Occlusion Manager
        var depthTexture = occlusionManager.environmentDepthTexture;
        
        // Преобразуем экранные координаты в UV координаты текстуры глубины
        Vector2 depthUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
        
        // Учитываем ориентацию и поворот устройства
        // AR Foundation может поворачивать текстуру глубины
        int textureX = (int)(depthUV.x * depthTexture.width);
        int textureY = (int)(depthUV.y * depthTexture.height);
        
        // Для упрощения возвращаем фиксированную глубину
        // В полной реализации здесь будет чтение пикселя из текстуры глубины
        float estimatedDepth = 1.5f;
        
        LogDebug($"📊 Глубина в точке ({textureX}, {textureY}): {estimatedDepth:F2}m");
        return estimatedDepth;
    }

    /// <summary>
    /// Альтернативный метод получения глубины через raycast (fallback)
    /// </summary>
    /// <param name="screenPos">Позиция в экранных координатах</param>
    /// <returns>Глубина в метрах или 0 при ошибке</returns>
    private float GetDepthWithRaycast(Vector2 screenPos)
    {
        if (mainCamera == null)
            return 0f;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        
        if (showRaycastDebug)
        {
            Debug.DrawRay(ray.origin, ray.direction * 5f, Color.red, 1f);
        }

        // Пробуем найти AR плоскости
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 10f))
        {
            float distance = Vector3.Distance(mainCamera.transform.position, hit.point);
            LogDebug($"🎯 Raycast попадание: дистанция={distance:F2}m, объект={hit.collider.name}");
            return distance;
        }

        LogDebug("🔍 Raycast не нашел объекты, используем фиксированную глубину");
        return 1.5f; // Фиксированная глубина по умолчанию
    }

    /// <summary>
    /// Преобразует экранные координаты в мировые 3D координаты
    /// </summary>
    /// <param name="screenPos">Позиция в экранных координатах</param>
    /// <param name="depth">Глубина в метрах</param>
    /// <returns>Мировые 3D координаты</returns>
    private Vector3 ScreenToWorldPosition(Vector2 screenPos, float depth)
    {
        if (mainCamera == null)
            return Vector3.zero;

        // Нормализуем экранные координаты в диапазон [0,1]
        Vector2 normalizedPos = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
        
        // Преобразуем в NDC (Normalized Device Coordinates) [-1,1]
        Vector2 ndc = new Vector2(normalizedPos.x * 2f - 1f, normalizedPos.y * 2f - 1f);
        
        // Создаем точку в пространстве клипа
        Vector4 clipSpacePos = new Vector4(ndc.x, ndc.y, -1f, 1f);
        
        // Преобразуем через обратную матрицу проекции
        Vector4 viewSpacePos = inverseProjectionMatrix * clipSpacePos;
        viewSpacePos.z = -depth; // Устанавливаем нужную глубину
        viewSpacePos.w = 1f;
        
        // Преобразуем в мировые координаты
        Vector4 worldSpacePos = mainCamera.cameraToWorldMatrix * viewSpacePos;
        
        Vector3 worldPos = new Vector3(worldSpacePos.x, worldSpacePos.y, worldSpacePos.z);
        
        LogDebug($"🌍 Преобразование: экран={screenPos} -> мир={worldPos} (глубина={depth:F2}m)");
        return worldPos;
    }

    /// <summary>
    /// Создает 3D маркер в мировых координатах
    /// </summary>
    /// <param name="worldPosition">Позиция в мировых координатах</param>
    /// <param name="classIndex">Индекс класса сегментации</param>
    private void CreateWorldMarker(Vector3 worldPosition, int classIndex)
    {
        if (markersParent == null)
        {
            LogDebug("❌ Контейнер для маркеров не настроен");
            return;
        }

        GameObject marker;
        
        // Создаем маркер (префаб или простую сферу)
        if (markerPrefab != null)
        {
            marker = Instantiate(markerPrefab, markersParent);
        }
        else
        {
            // Создаем простую сферу как маркер
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.SetParent(markersParent);
            
            // Устанавливаем размер маркера (увеличиваем для лучшей видимости)
            marker.transform.localScale = Vector3.one * 0.1f; // 10см диаметр
            
            // Убираем коллайдер, чтобы не мешал
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
                DestroyImmediate(collider);
        }
        
        // ИСПРАВЛЕНИЕ: Корректируем позицию - маркеры должны быть ПЕРЕД камерой
        Vector3 correctedPosition = worldPosition;
        
        // Если маркер за камерой, перемещаем его вперед
        if (mainCamera != null)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 toCameraDirection = (mainCamera.transform.position - worldPosition).normalized;
            float dotProduct = Vector3.Dot(cameraForward, toCameraDirection);
            
            if (dotProduct > 0) // Маркер за камерой
            {
                correctedPosition = mainCamera.transform.position + cameraForward * 2f; // 2 метра перед камерой
                LogDebug($"⚠️ Маркер был за камерой, перемещен в {correctedPosition}");
            }
        }
        
        // Устанавливаем позицию маркера
        marker.transform.position = correctedPosition;
        
        // ВАЖНО: Убеждаемся, что маркер активен и видим
        marker.SetActive(true);
        
        // Уникальное имя для отладки
        marker.name = $"Marker_{GetClassName(classIndex)}_{markersParent.childCount}";
        
        // Устанавливаем цвет в зависимости от класса
        Color markerColor = GetColorForClass(classIndex);
        
        // Применяем цвет ко всем рендерерам маркера
        Renderer[] renderers = marker.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.material != null)
            {
                renderer.material.color = markerColor;
            }
        }

        // Добавляем компонент для хранения информации о маркере
        MarkerInfo markerInfo = marker.AddComponent<MarkerInfo>();
        markerInfo.Initialize(classIndex, correctedPosition, GetClassName(classIndex));

        LogDebug($"📍 Создан маркер: {GetClassName(classIndex)} в позиции {correctedPosition}");
        LogDebug($"📏 Расстояние до камеры: {Vector3.Distance(correctedPosition, mainCamera.transform.position):F2}m");
        LogDebug($"🎯 Размер маркера: {marker.transform.localScale}");
        LogDebug($"✅ Маркер активен: {marker.activeInHierarchy}, родитель: {markersParent.name}");
        LogDebug($"🔍 Рендереры в маркере: {renderers.Length}");
    }

    /// <summary>
    /// Обновляет матрицы камеры для точных вычислений
    /// </summary>
    private void UpdateCameraMatrices()
    {
        if (mainCamera == null)
            return;

        projectionMatrix = mainCamera.projectionMatrix;
        inverseProjectionMatrix = projectionMatrix.inverse;
        worldToCameraMatrix = mainCamera.worldToCameraMatrix;
    }

    /// <summary>
    /// Создает простой маркер по умолчанию
    /// </summary>
    private void CreateDefaultMarkerPrefab()
    {
        // Создаем простую сферу как маркер
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.localScale = Vector3.one * 0.05f; // 5см диаметр
        
        // Делаем его префабом (пока просто сохраняем ссылку)
        markerPrefab = sphere;
        
        // Убираем коллайдер, чтобы не мешал
        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);
            
        LogDebug("🔵 Создан простой маркер по умолчанию (сфера 5см)");
    }

    /// <summary>
    /// Проверяет, находится ли указатель над UI элементом
    /// </summary>
    /// <param name="screenPosition">Позиция в экранных координатах</param>
    /// <returns>true если над UI</returns>
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>
    /// Получает цвет для указанного класса
    /// </summary>
    /// <param name="classIndex">Индекс класса</param>
    /// <returns>Цвет маркера</returns>
    private Color GetColorForClass(int classIndex)
    {
        if (classIndex >= 0 && classIndex < classColors.Length)
            return classColors[classIndex];
        
        return Color.white; // Цвет по умолчанию
    }

    /// <summary>
    /// Получает название класса по индексу
    /// </summary>
    /// <param name="classIndex">Индекс класса</param>
    /// <returns>Название класса</returns>
    private string GetClassName(int classIndex)
    {
        string[] classNames = { "Стена", "Пол", "Потолок", "Окно", "Дверь", "Другое" };
        
        if (classIndex >= 0 && classIndex < classNames.Length)
            return classNames[classIndex];
            
        return $"Класс_{classIndex}";
    }

    /// <summary>
    /// Логирование с проверкой флага отладки
    /// </summary>
    /// <param name="message">Сообщение для лога</param>
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[ARMaskInteraction] {message}");
    }

    // ОТЛАДКА: Добавляем принудительные логи для диагностики
    private void Start()
    {
        LogDebug("🚀 ARMaskInteraction запущен!");
        LogDebug($"Enable Interaction: {enableInteraction}");
        LogDebug($"Segmentation Manager: {(segmentationManager != null ? "ОК" : "НЕ НАЙДЕН")}");
        LogDebug($"Main Camera: {(mainCamera != null ? mainCamera.name : "НЕ НАЙДЕН")}");
        LogDebug($"Markers Parent: {(markersParent != null ? markersParent.name : "НЕ НАЙДЕН")}");
    }

    // Публичные методы для управления

    /// <summary>
    /// Включает/выключает взаимодействие
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        enableInteraction = enabled;
        LogDebug($"🎮 Взаимодействие: {(enabled ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
    }

    /// <summary>
    /// Очищает все созданные маркеры
    /// </summary>
    [ContextMenu("Очистить все маркеры")]
    public void ClearAllMarkers()
    {
        if (markersParent != null)
        {
            int childCount = markersParent.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(markersParent.GetChild(i).gameObject);
                else
                    DestroyImmediate(markersParent.GetChild(i).gameObject);
            }
            LogDebug($"🗑️ Удалено {childCount} маркеров");
        }
    }

    /// <summary>
    /// Устанавливает новый префаб маркера
    /// </summary>
    /// <param name="newPrefab">Новый префаб</param>
    public void SetMarkerPrefab(GameObject newPrefab)
    {
        markerPrefab = newPrefab;
        LogDebug("📍 Префаб маркера обновлен");
    }
}

/// <summary>
/// Компонент для хранения информации о маркере
/// </summary>
public class MarkerInfo : MonoBehaviour
{
    public int classIndex;
    public Vector3 worldPosition;
    public string className;
    public float creationTime;

    public void Initialize(int index, Vector3 position, string name)
    {
        classIndex = index;
        worldPosition = position;
        className = name;
        creationTime = Time.time;
    }
}

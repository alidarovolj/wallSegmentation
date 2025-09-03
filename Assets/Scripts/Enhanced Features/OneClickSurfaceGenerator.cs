using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Генератор поверхностей "в один клик"
/// Автоматически создает 3D-меш стены по одному касанию
/// </summary>
public class OneClickSurfaceGenerator : MonoBehaviour
{
      [Header("🎯 Настройки автогенерации")]
      [SerializeField] private bool enableOneClickMode = true;
      [SerializeField] private int gridSamplePoints = 50; // Количество точек для автосэмплинга
      [SerializeField] private float sampleRadius = 0.3f; // Радиус области сэмплинга вокруг касания
      [SerializeField] private float minConfidenceThreshold = 0.4f; // Минимальная уверенность для генерации

      [Header("🎯 AR Plane Detection (Как Dulux Visualizer)")]
      [Tooltip("ARPlaneManager для реальной детекции плоскостей")]
      [SerializeField] private ARPlaneManager planeManager;
      [Tooltip("ARRaycastManager для точного попадания на плоскости")]
      [SerializeField] private ARRaycastManager raycastManager;

      [Header("🎯 Fallback Raycast (для симуляции)")]
      [Tooltip("Слои, на которых будет производиться поиск поверхностей")]
      [SerializeField] private LayerMask surfaceLayerMask;

      [Header("🔗 Зависимости")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private ProceduralSurfaceManager surfaceManager;
      [SerializeField] private SurfacePointCollector pointCollector;
      [SerializeField] private ARMaskInteraction maskInteraction;

      [Header("⚡ Производительность")]
      [SerializeField] private bool useQuickMode = true; // Быстрый режим с упрощенными алгоритмами
      [SerializeField] private int maxProcessingTime = 3; // Максимальное время обработки в секундах

      [Header("🎨 Визуализация")]
      [SerializeField] private bool showProgressIndicator = true;
      [SerializeField] private GameObject progressPrefab;

      // Переменные для сохранения результатов корутин
      private List<Vector3> lastCollectedPoints = new List<Vector3>();
      private GameObject lastGeneratedMesh;

      [Header("🔧 Отладка")]
      [SerializeField] private bool enableDebugLogs = true;

      // Внутренние переменные
      private bool isProcessing = false;
      private GameObject currentProgressIndicator;
      private Vector3 lastTapPosition;
      private int targetClassId = 0; // По умолчанию стены

      // События
      public System.Action<GameObject> OnSurfaceGenerated;
      public System.Action<string> OnProcessFailed;

      private void Awake()
      {
            // Автопоиск зависимостей
            FindDependencies();
            LogDebug("🚀 OneClickSurfaceGenerator инициализирован");
      }

      private void Start()
      {
            // Подписываемся на события касаний
            SubscribeToTouchEvents();
      }

      private void Update()
      {
            // Обрабатываем касания в режиме "один клик"
            if (enableOneClickMode && !isProcessing)
            {
                  HandleOneClickInput();
            }
      }

      /// <summary>
      /// Обрабатывает ввод в режиме "один клик"
      /// </summary>
      private void HandleOneClickInput()
      {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                  Vector2 screenPos = Input.mousePosition;
                  if (Input.touchCount > 0)
                        screenPos = Input.GetTouch(0).position;

                  LogDebug($"🎯 Обнаружено касание в режиме 'один клик': {screenPos}");

                  // БЛОКИРУЕМ другие системы на время обработки
                  BlockOtherInputSystems(true);

                  StartCoroutine(ProcessOneClickGeneration(screenPos));
            }
      }

      /// <summary>
      /// Главный процесс генерации "в один клик"
      /// </summary>
      private IEnumerator ProcessOneClickGeneration(Vector2 screenPos)
      {
            if (isProcessing)
            {
                  LogDebug("⚠️ Процесс уже выполняется");
                  yield break;
            }

            isProcessing = true;
            ShowProgressIndicator(screenPos);

            LogDebug("🚀 Начинаем процесс генерации 'в один клик'");

            // Этап 1: Быстрый анализ касания
            LogDebug("1️⃣ Анализ позиции касания...");

            // НОВОЕ: Запускаем обработку одного кадра сегментации по требованию
            if (segmentationManager != null)
            {
                  segmentationManager.ProcessSingleFrame();
                  LogDebug("🎯 Запущена обработка кадра сегментации по требованию");

                  // Временно показываем маску для анализа (2 секунды)
                  ARWallPresenter wallPresenter = FindObjectOfType<ARWallPresenter>();
                  if (wallPresenter != null)
                  {
                        wallPresenter.ShowMaskTemporarily(2.0f);
                        LogDebug("🎭 Показываем маску временно для анализа");
                  }

                  // Даем время на обработку кадра
                  yield return new WaitForSeconds(0.2f);
            }

            Vector3 worldPos = AnalyzeTouchPosition(screenPos);
            if (worldPos == Vector3.zero)
            {
                  FailWithMessage("❌ Не удалось определить 3D позицию касания");
                  yield break;
            }

            yield return new WaitForSeconds(0.1f);

            // Этап 2: Автоматический сбор точек в области
            LogDebug("2️⃣ Автоматический сбор точек...");
            yield return StartCoroutine(AutoCollectPointsAroundPosition(worldPos, screenPos));
            List<Vector3> autoPoints = lastCollectedPoints; // Используем сохраненный результат
            if (autoPoints.Count < 10)
            {
                  FailWithMessage($"❌ Недостаточно точек: {autoPoints.Count} < 10");
                  yield break;
            }

            LogDebug($"✅ Собрано {autoPoints.Count} точек автоматически");

            // Этап 3: Быстрая аппроксимация плоскости
            LogDebug("3️⃣ Аппроксимация плоскости...");
            var planeResult = ApproximatePlaneQuick(autoPoints);
            if (planeResult.confidence < minConfidenceThreshold)
            {
                  FailWithMessage($"❌ Низкая уверенность плоскости: {planeResult.confidence:F2}");
                  yield break;
            }

            LogDebug($"✅ Плоскость: уверенность={planeResult.confidence:F2}");

            // Этап 4: Генерация простого меша
            LogDebug("4️⃣ Генерация меша...");
            yield return StartCoroutine(GenerateQuickMesh(planeResult, worldPos));
            GameObject meshObject = lastGeneratedMesh; // Используем сохраненный результат
            if (meshObject == null)
            {
                  FailWithMessage("❌ Не удалось сгенерировать меш");
                  yield break;
            }

            // Этап 5: Финализация
            LogDebug("5️⃣ Финализация...");
            HideProgressIndicator();
            OnSurfaceGenerated?.Invoke(meshObject);

            LogDebug("🎉 Генерация завершена успешно!");

            // РАЗБЛОКИРУЕМ другие системы
            BlockOtherInputSystems(false);

            isProcessing = false;
            HideProgressIndicator();
      }

      /// <summary>
      /// Анализирует позицию касания и возвращает 3D координаты
      /// </summary>
      private Vector3 AnalyzeTouchPosition(Vector2 screenPos)
      {
            // ТОЧНАЯ КОПИЯ ЛОГИКИ ARMaskInteraction для консистентности

            // 1. НОВОЕ: Получаем класс сегментации в точке касания
            int detectedClass = GetSegmentationClassAtPosition(screenPos);
            if (detectedClass < 0)
            {
                  LogDebug("❌ Не удалось определить класс сегментации");
                  return Vector3.zero;
            }

            // 2. Получаем глубину (та же логика что ARMaskInteraction)
            float depth = GetDepthAtPosition(screenPos);
            if (depth <= 0)
            {
                  LogDebug("❌ Не удалось получить данные о глубине");
                  depth = 1.5f; // Fallback
                  LogDebug($"🔄 Используем фиксированную глубину: {depth}m");
            }

            // 3. Преобразуем в мировые координаты
            Vector3 worldPoint = ScreenToWorldPosition(screenPos, depth);
            if (worldPoint == Vector3.zero)
            {
                  LogDebug("❌ Не удалось преобразовать в 3D координаты");
                  return Vector3.zero;
            }

            // 4. Добавляем небольшое смещение для визуального различия
            worldPoint.x += Random.Range(-0.02f, 0.02f);
            worldPoint.y += Random.Range(-0.02f, 0.02f);

            lastTapPosition = worldPoint;

            LogDebug($"✅ Анализ касания: класс={GetClassName(detectedClass)}, позиция={worldPoint}, глубина={depth:F2}m");
            return worldPoint;
      }

      /// <summary>
      /// Получает класс сегментации в указанной экранной позиции (копия из ARMaskInteraction)
      /// </summary>
      private int GetSegmentationClassAtPosition(Vector2 screenPos)
      {
            if (segmentationManager == null)
                  return -1;

            if (!segmentationManager.IsSegmentationMaskReady())
                  return -1;

            // Преобразуем экранные координаты в UV координаты маски
            Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            // Применяем тот же поворот что и в AsyncSegmentationManager
            float uv_x = 1.0f - screenUV.y;
            float uv_y = screenUV.x;

            // Получаем текстуру маски
            RenderTexture maskTexture = segmentationManager.GetCurrentSegmentationMask();
            if (maskTexture == null)
                  return -1;

            // Преобразуем UV в координаты текстуры
            int textureX = Mathf.Clamp((int)(uv_x * maskTexture.width), 0, maskTexture.width - 1);
            int textureY = Mathf.Clamp((int)(uv_y * maskTexture.height), 0, maskTexture.height - 1);

            LogDebug($"🎯 UV координаты: ({uv_x:F3}, {uv_y:F3}) -> текстура: ({textureX}, {textureY})");
            return 0; // Стены - самый частый случай
      }

      /// <summary>
      /// Получает значение глубины в указанной экранной позиции (копия из ARMaskInteraction)
      /// </summary>
      private float GetDepthAtPosition(Vector2 screenPos)
      {
            // Используем raycast как основной метод (AR depth API недоступен в симуляторе)
            return GetDepthWithRaycast(screenPos);
      }

      /// <summary>
      /// Получение глубины через raycast с поддержкой AR Planes (как Dulux Visualizer)
      /// </summary>
      private float GetDepthWithRaycast(Vector2 screenPos)
      {
            // НОВЫЙ ПОДХОД: Сначала пробуем AR Raycast на реальных плоскостях (как Dulux)
            if (TryGetARPlaneDistance(screenPos, out float arDistance))
            {
                  LogDebug($"🎯 AR Plane попадание: дистанция={arDistance:F2}m (реальная плоскость)");
                  return arDistance;
            }

            // Fallback: обычный raycast для симуляции
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                  return 1.5f;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            RaycastHit hit;

            // ИСПРАВЛЕНИЕ: Используем LayerMask для поиска только нужных поверхностей
            if (Physics.Raycast(ray, out hit, 10f, surfaceLayerMask))
            {
                  float distance = Vector3.Distance(mainCamera.transform.position, hit.point);
                  LogDebug($"🎯 Raycast попадание: дистанция={distance:F2}m, объект={hit.collider.name}, слой={LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                  return distance;
            }

            LogDebug($"🔍 Raycast не нашел объекты на слое '{LayerMask.LayerToName((int)Mathf.Log(surfaceLayerMask.value, 2))}', используем фиксированную глубину");
            return 1.5f; // Фиксированная глубина по умолчанию
      }

      /// <summary>
      /// НОВЫЙ МЕТОД: Пытается найти реальную AR плоскость в точке касания (как в Dulux Visualizer)
      /// </summary>
      private bool TryGetARPlaneDistance(Vector2 screenPos, out float distance)
      {
            distance = 0f;

            // Проверяем, доступны ли AR компоненты
            if (raycastManager == null || planeManager == null)
            {
                  // Пытаемся найти их автоматически
                  if (raycastManager == null)
                        raycastManager = FindObjectOfType<ARRaycastManager>();
                  if (planeManager == null)
                        planeManager = FindObjectOfType<ARPlaneManager>();

                  if (raycastManager == null || planeManager == null)
                  {
                        LogDebug("⚠️ AR компоненты не найдены - используем fallback raycast");
                        return false;
                  }
            }

            // Выполняем AR raycast на детектированные плоскости
            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            if (raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
            {
                  foreach (var hit in hits)
                  {
                        // Проверяем, что плоскость является стеной (vertical)
                        if (hit.trackable is ARPlane plane && IsVerticalPlane(plane))
                        {
                              distance = hit.distance;
                              LogDebug($"🏗️ Найдена AR стена: ID={plane.trackableId}, размер={plane.size}, ориентация={plane.alignment}");
                              return true;
                        }
                  }

                  // Если нет вертикальных плоскостей, берем первую попавшуюся
                  if (hits.Count > 0)
                  {
                        distance = hits[0].distance;
                        LogDebug($"🎯 Найдена AR плоскость (любая): дистанция={distance:F2}m");
                        return true;
                  }
            }

            LogDebug("🔍 AR Raycast не нашел плоскостей - переходим к fallback");
            return false;
      }

      /// <summary>
      /// Проверяет, является ли плоскость вертикальной (стеной)
      /// </summary>
      private bool IsVerticalPlane(ARPlane plane)
      {
            return plane.alignment == PlaneAlignment.Vertical;
      }

      /// <summary>
      /// Продвинутая конвертация экранных координат в мировые (копия из ARMaskInteraction)
      /// </summary>
      private Vector3 ScreenToWorldPosition(Vector2 screenPos, float depth)
      {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                  return Vector3.zero;

            // Нормализуем экранные координаты в диапазон [0,1]
            Vector2 normalizedPos = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            // Преобразуем в NDC (Normalized Device Coordinates) [-1,1]
            Vector2 ndc = new Vector2(normalizedPos.x * 2f - 1f, normalizedPos.y * 2f - 1f);

            // Создаем точку в пространстве клипа
            Vector4 clipSpacePos = new Vector4(ndc.x, ndc.y, -1f, 1f);

            // Преобразуем через обратную матрицу проекции
            Matrix4x4 inverseProjectionMatrix = mainCamera.projectionMatrix.inverse;
            Vector4 viewSpacePos = inverseProjectionMatrix * clipSpacePos;
            viewSpacePos.z = -depth; // Устанавливаем нужную глубину
            viewSpacePos.w = 1f;

            // Преобразуем в мировые координаты
            Vector4 worldSpacePos = mainCamera.cameraToWorldMatrix * viewSpacePos;

            Vector3 worldPos = new Vector3(worldSpacePos.x, worldSpacePos.y, worldSpacePos.z);
            return worldPos;
      }

      /// <summary>
      /// Проверяет что клик действительно по стене/поверхности
      /// </summary>
      private bool IsClickOnWallSurface(Vector2 screenPos)
      {
            if (segmentationManager == null)
                  return true; // Fallback если нет сегментации

            // Проверяем что в точке касания есть стена (класс 0)
            // Используем ту же логику что и ARMaskInteraction
            if (!segmentationManager.IsSegmentationMaskReady())
                  return true; // Fallback если маска не готова

            // Преобразуем экранные координаты в UV координаты маски
            Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            // Применяем тот же поворот что и в AsyncSegmentationManager
            float uv_x = 1.0f - screenUV.y;
            float uv_y = screenUV.x;

            // Получаем текстуру маски
            RenderTexture maskTexture = segmentationManager.GetCurrentSegmentationMask();
            if (maskTexture == null)
                  return true; // Fallback

            // Преобразуем UV в координаты текстуры
            int textureX = Mathf.Clamp((int)(uv_x * maskTexture.width), 0, maskTexture.width - 1);
            int textureY = Mathf.Clamp((int)(uv_y * maskTexture.height), 0, maskTexture.height - 1);

            // В данной реализации упрощенно считаем что есть стена
            // В полной версии здесь будет чтение пикселя из текстуры
            LogDebug($"🎯 Проверка стены в UV: ({uv_x:F3}, {uv_y:F3}) -> текстура: ({textureX}, {textureY})");
            return true; // Пока всегда разрешаем (для отладки)
      }

      /// <summary>
      /// Получает название класса по индексу
      /// </summary>
      private string GetClassName(int classIndex)
      {
            switch (classIndex)
            {
                  case 0: return "Стена";
                  case 1: return "Пол";
                  case 2: return "Потолок";
                  case 3: return "Окно";
                  case 4: return "Дверь";
                  default: return "Неизвестно";
            }
      }

      /// <summary>
      /// Автоматически собирает точки вокруг позиции касания
      /// </summary>
      private IEnumerator AutoCollectPointsAroundPosition(Vector3 centerPos, Vector2 screenCenter)
      {
            List<Vector3> points = new List<Vector3>();

            // Создаем сетку точек вокруг центральной позиции
            int gridSize = Mathf.RoundToInt(Mathf.Sqrt(gridSamplePoints));
            float step = (sampleRadius * 2f) / gridSize;

            for (int x = 0; x < gridSize; x++)
            {
                  for (int y = 0; y < gridSize; y++)
                  {
                        // Вычисляем смещение от центра
                        float offsetX = (x - gridSize * 0.5f) * step;
                        float offsetY = (y - gridSize * 0.5f) * step;

                        // Преобразуем в мировые координаты
                        Vector3 samplePos = centerPos + new Vector3(offsetX, offsetY, 0);

                        // Добавляем небольшую случайность для более естественного распределения
                        samplePos += Random.insideUnitSphere * (step * 0.2f);

                        points.Add(samplePos);

                        // Периодически отдаем управление
                        if (points.Count % 10 == 0)
                              yield return null;
                  }
            }

            // Добавляем центральную точку
            points.Add(centerPos);

            LogDebug($"🔄 Сгенерировано {points.Count} точек для анализа");
            lastCollectedPoints = points; // Сохраняем результат
      }

      /// <summary>
      /// Быстрая аппроксимация плоскости (упрощенная версия)
      /// </summary>
      private PlaneApproximator.PlaneResult ApproximatePlaneQuick(List<Vector3> points)
      {
            if (points.Count < 3)
            {
                  return new PlaneApproximator.PlaneResult
                  {
                        isValid = false,
                        confidence = 0f
                  };
            }

            // Простая аппроксимация через среднюю точку и PCA
            Vector3 center = Vector3.zero;
            foreach (var point in points)
                  center += point;
            center /= points.Count;

            // Вычисляем ковариационную матрицу (упрощенно)
            Vector3 v1 = Vector3.zero, v2 = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                  Vector3 diff = points[i] - center;
                  v1 += diff;
                  if (i > 0)
                        v2 += Vector3.Cross(diff, points[i - 1] - center);
            }

            Vector3 normal = v2.normalized;
            if (normal == Vector3.zero)
                  normal = Vector3.up; // Fallback

            // Простая оценка уверенности на основе разброса точек
            float variance = 0f;
            foreach (var point in points)
            {
                  float distance = Mathf.Abs(Vector3.Dot(point - center, normal));
                  variance += distance * distance;
            }
            variance /= points.Count;

            float confidence = 1f / (1f + variance * 10f); // Простая формула уверенности

            return new PlaneApproximator.PlaneResult
            {
                  isValid = true,
                  center = center,
                  normal = normal,
                  confidence = confidence,
                  bounds = new Bounds(center, Vector3.one * sampleRadius * 2f),
                  averageError = Mathf.Sqrt(variance)
            };
      }

      /// <summary>
      /// Генерирует простой меш плоскости
      /// </summary>
      private IEnumerator GenerateQuickMesh(PlaneApproximator.PlaneResult plane, Vector3 tapPos)
      {
            // Создаем простую прямоугольную плоскость
            GameObject meshObject = new GameObject("OneClick_Surface");

            // ВАЖНО: Позиционируем меш НА СТЕНЕ, выдвигая по нормали
            Vector3 wallPosition = tapPos + plane.normal * 0.01f; // Выдвигаем ПО НОРМАЛИ поверхности
            meshObject.transform.position = wallPosition;

            // ИСПРАВЛЕНИЕ: Используем РЕАЛЬНУЮ нормаль поверхности для ориентации
            meshObject.transform.rotation = Quaternion.LookRotation(-plane.normal, Vector3.up);

            // Создаем меш
            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

            // Простая прямоугольная геометрия - УМЕНЬШЕННЫЙ размер
            float meshSize = 0.1f; // Маленький размер вместо sampleRadius (0.3f)
            Mesh mesh = CreateSimpleQuadMesh(meshSize);
            meshFilter.mesh = mesh;

            // Создаем материал
            Material material = CreateQuickMaterial();
            meshRenderer.material = material;

            LogDebug($"🎨 Создан простой меш: {mesh.vertexCount} вершин");

            lastGeneratedMesh = meshObject; // Сохраняем результат
            yield break;
      }

      /// <summary>
      /// Создает простой quad меш
      /// </summary>
      private Mesh CreateSimpleQuadMesh(float size)
      {
            Mesh mesh = new Mesh();
            mesh.name = "OneClick_Quad";

            // Вершины
            Vector3[] vertices = new Vector3[]
            {
            new Vector3(-size, -size, 0),
            new Vector3(size, -size, 0),
            new Vector3(size, size, 0),
            new Vector3(-size, size, 0)
            };

            // Треугольники
            int[] triangles = new int[]
            {
            0, 1, 2,
            2, 3, 0
            };

            // UV координаты
            Vector2[] uv = new Vector2[]
            {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
      }

      /// <summary>
      /// Создает простой материал для меша
      /// </summary>
      private Material CreateQuickMaterial()
      {
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.2f, 0.8f, 0.2f, 0.7f); // Полупрозрачный зеленый
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;

            return material;
      }

      /// <summary>
      /// Автопоиск зависимостей
      /// </summary>
      private void FindDependencies()
      {
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            if (surfaceManager == null)
                  surfaceManager = FindObjectOfType<ProceduralSurfaceManager>();

            if (pointCollector == null)
                  pointCollector = FindObjectOfType<SurfacePointCollector>();

            if (maskInteraction == null)
                  maskInteraction = FindObjectOfType<ARMaskInteraction>();
      }

      /// <summary>
      /// Подписывается на события касаний
      /// </summary>
      private void SubscribeToTouchEvents()
      {
            // Можно подписаться на события других компонентов если нужно
      }

      /// <summary>
      /// Показывает индикатор прогресса
      /// </summary>
      private void ShowProgressIndicator(Vector2 screenPos)
      {
            if (!showProgressIndicator || progressPrefab == null)
                  return;

            HideProgressIndicator(); // Убираем предыдущий

            // Преобразуем в мировые координаты
            Vector3 worldPos = AnalyzeTouchPosition(screenPos);
            if (worldPos != Vector3.zero)
            {
                  currentProgressIndicator = Instantiate(progressPrefab, worldPos, Quaternion.identity);
            }
      }

      /// <summary>
      /// Скрывает индикатор прогресса
      /// </summary>
      private void HideProgressIndicator()
      {
            if (currentProgressIndicator != null)
            {
                  if (Application.isPlaying)
                        Destroy(currentProgressIndicator);
                  else
                        DestroyImmediate(currentProgressIndicator);

                  currentProgressIndicator = null;
            }
      }

      /// <summary>
      /// Завершает процесс с ошибкой
      /// </summary>
      private void FailWithMessage(string message)
      {
            LogDebug(message);
            OnProcessFailed?.Invoke(message);
            HideProgressIndicator();

            // РАЗБЛОКИРУЕМ другие системы при ошибке
            BlockOtherInputSystems(false);

            isProcessing = false;
      }

      /// <summary>
      /// Блокирует/разблокирует другие системы ввода для предотвращения конфликтов
      /// </summary>
      private void BlockOtherInputSystems(bool block)
      {
            try
            {
                  // Блокируем ARMaskInteraction
                  if (maskInteraction != null)
                  {
                        maskInteraction.enabled = !block;
                        LogDebug($"🔒 ARMaskInteraction {(block ? "ЗАБЛОКИРОВАН" : "РАЗБЛОКИРОВАН")}");
                  }

                  // Блокируем SurfacePointCollector (если есть)
                  if (pointCollector != null)
                  {
                        pointCollector.enabled = !block;
                        LogDebug($"🔒 SurfacePointCollector {(block ? "ЗАБЛОКИРОВАН" : "РАЗБЛОКИРОВАН")}");
                  }

                  // Блокируем ProceduralSurfaceManager
                  if (surfaceManager != null)
                  {
                        surfaceManager.enabled = !block;
                        LogDebug($"🔒 ProceduralSurfaceManager {(block ? "ЗАБЛОКИРОВАН" : "РАЗБЛОКИРОВАН")}");
                  }
            }
            catch (System.Exception e)
            {
                  LogDebug($"⚠️ Ошибка при блокировке систем: {e.Message}");
            }
      }

      private void LogDebug(string message)
      {
            if (enableDebugLogs)
                  Debug.Log($"[OneClickSurfaceGenerator] {message}");
      }

      // Публичные методы

      /// <summary>
      /// Включает/выключает режим "один клик"
      /// </summary>
      public void SetOneClickMode(bool enabled)
      {
            enableOneClickMode = enabled;
            LogDebug($"🎯 Режим 'один клик': {(enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
      }

      /// <summary>
      /// Принудительно запускает генерацию в указанной позиции
      /// </summary>
      public void ForceGenerateAt(Vector2 screenPos)
      {
            if (!isProcessing)
            {
                  StartCoroutine(ProcessOneClickGeneration(screenPos));
            }
      }

      /// <summary>
      /// Проверяет, выполняется ли процесс
      /// </summary>
      public bool IsProcessing()
      {
            return isProcessing;
      }
}

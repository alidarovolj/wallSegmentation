using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Сборщик облака точек для одной поверхности
/// Собирает множество 3D-точек для последующей аппроксимации плоскости
/// </summary>
public class SurfacePointCollector : MonoBehaviour
{
      [Header("Настройки сбора")]
      [SerializeField] private int targetClassId = 0; // Класс поверхности для сбора (0 = стены)
      [SerializeField] private float collectionRadius = 0.5f; // Радиус сбора точек вокруг касания
      [SerializeField] private int samplesPerTap = 16; // Количество образцов вокруг каждого касания
      [SerializeField] private float maxPointDistance = 5f; // Максимальное расстояние точки от камеры
      [SerializeField] private float minPointDistance = 0.3f; // Минимальное расстояние точки от камеры

      [Header("Визуализация")]
      [SerializeField] private bool showPointCloud = true;
      [SerializeField] private GameObject pointVisualizationPrefab;
      [SerializeField] private Color pointCloudColor = Color.cyan;
      [SerializeField] private float pointSize = 0.02f; // 2см диаметр

      [Header("Управление сбором")]
      [SerializeField] private KeyCode toggleCollectionKey = KeyCode.C;
      [SerializeField] private KeyCode clearPointsKey = KeyCode.X;
      [SerializeField] private bool autoStartCollection = true;

      [Header("Отладка")]
      [SerializeField] private bool enableDebugLogs = true;

      // Состояние сбора
      private bool isCollecting = false;
      private List<Vector3> collectedPoints = new List<Vector3>();
      private List<GameObject> pointVisualizations = new List<GameObject>();
      private Transform pointsParent;

      // Зависимости
      private ARMaskInteraction arMaskInteraction;
      private AsyncSegmentationManager segmentationManager;
      private Camera arCamera;

      // События
      public System.Action<List<Vector3>> OnPointCloudUpdated;
      public System.Action<List<Vector3>> OnCollectionCompleted;

      private void Awake()
      {
            // Автопоиск зависимостей
            arMaskInteraction = FindObjectOfType<ARMaskInteraction>();
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            arCamera = Camera.main;

            // Создаем контейнер для визуализации точек
            GameObject pointContainer = new GameObject("Surface_Point_Cloud");
            pointsParent = pointContainer.transform;
            pointsParent.SetParent(transform);

            // Создаем простой префаб если не назначен
            if (pointVisualizationPrefab == null)
            {
                  CreateDefaultPointPrefab();
            }

            LogDebug("🔍 SurfacePointCollector инициализирован");
      }

      private void Start()
      {
            if (autoStartCollection)
            {
                  StartCollection();
            }
      }

      private void Update()
      {
            HandleInput();

            // Отладочный статус каждые 5 секунд
            if (Time.frameCount % 300 == 0 && enableDebugLogs)
            {
                  LogDebug($"📊 Статус: сбор={isCollecting}, точек={collectedPoints.Count}, класс={targetClassId}");
            }
      }

      /// <summary>
      /// Обрабатывает пользовательский ввод
      /// </summary>
      private void HandleInput()
      {
            if (Input.GetKeyDown(toggleCollectionKey))
            {
                  ToggleCollection();
            }

            if (Input.GetKeyDown(clearPointsKey))
            {
                  ClearPointCloud();
            }
      }

      /// <summary>
      /// Начинает сбор облака точек
      /// </summary>
      public void StartCollection()
      {
            isCollecting = true;
            LogDebug($"▶️ Начат сбор облака точек для класса {GetClassName(targetClassId)}");

            // Подписываемся на события касания от ARMaskInteraction
            if (arMaskInteraction != null)
            {
                  // В следующей итерации добавим событие в ARMaskInteraction
                  StartCoroutine(MonitorForTaps());
            }
      }

      /// <summary>
      /// Останавливает сбор облака точек
      /// </summary>
      public void StopCollection()
      {
            isCollecting = false;
            LogDebug($"⏹️ Остановлен сбор облака точек. Собрано {collectedPoints.Count} точек");

            OnCollectionCompleted?.Invoke(new List<Vector3>(collectedPoints));
      }

      /// <summary>
      /// Переключает режим сбора
      /// </summary>
      public void ToggleCollection()
      {
            if (isCollecting)
                  StopCollection();
            else
                  StartCollection();
      }

      /// <summary>
      /// Очищает собранное облако точек
      /// </summary>
      public void ClearPointCloud()
      {
            collectedPoints.Clear();

            // Удаляем визуализацию
            foreach (var point in pointVisualizations)
            {
                  if (point != null)
                  {
                        if (Application.isPlaying)
                              Destroy(point);
                        else
                              DestroyImmediate(point);
                  }
            }
            pointVisualizations.Clear();

            LogDebug("🗑️ Облако точек очищено");
      }

      /// <summary>
      /// Мониторит касания для сбора точек (временная реализация)
      /// В будущем это будет событие от ARMaskInteraction
      /// </summary>
      private IEnumerator MonitorForTaps()
      {
            while (isCollecting)
            {
                  // Проверяем касания
                  if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
                  {
                        Vector2 screenPos = Input.mousePosition;
                        if (Input.touchCount > 0)
                              screenPos = Input.GetTouch(0).position;

                        // Собираем точки вокруг касания
                        yield return StartCoroutine(CollectPointsAroundTap(screenPos));
                  }

                  yield return new WaitForSeconds(0.1f); // Проверяем 10 раз в секунду
            }
      }

      /// <summary>
      /// Собирает облако точек вокруг указанного касания
      /// </summary>
      /// <param name="centerTap">Центральная точка касания</param>
      private IEnumerator CollectPointsAroundTap(Vector2 centerTap)
      {
            LogDebug($"🎯 Сбор точек вокруг касания: {centerTap}");

            List<Vector3> newPoints = new List<Vector3>();

            // Создаем сетку точек вокруг касания
            for (int i = 0; i < samplesPerTap; i++)
            {
                  // Генерируем случайные офсеты в радиусе
                  float angle = (i / (float)samplesPerTap) * 2f * Mathf.PI;
                  float radius = Random.Range(0f, collectionRadius);

                  Vector2 offset = new Vector2(
                      Mathf.Cos(angle) * radius * Screen.width * 0.1f,
                      Mathf.Sin(angle) * radius * Screen.height * 0.1f
                  );

                  Vector2 samplePos = centerTap + offset;

                  // Проверяем, что точка на экране
                  if (samplePos.x < 0 || samplePos.x >= Screen.width ||
                      samplePos.y < 0 || samplePos.y >= Screen.height)
                        continue;

                  // Получаем класс в этой точке
                  int classAtPoint = GetSegmentationClassAtPosition(samplePos);
                  if (classAtPoint != targetClassId)
                        continue;

                  // Получаем глубину
                  float depth = GetDepthAtPosition(samplePos);
                  if (depth <= minPointDistance || depth > maxPointDistance)
                        continue;

                  // Преобразуем в 3D
                  Vector3 worldPoint = ScreenToWorldPosition(samplePos, depth);
                  if (worldPoint != Vector3.zero)
                  {
                        newPoints.Add(worldPoint);
                        collectedPoints.Add(worldPoint);

                        // Визуализируем точку
                        if (showPointCloud)
                        {
                              CreatePointVisualization(worldPoint);
                        }
                  }

                  // Даем немного времени для обработки
                  if (i % 4 == 0)
                        yield return null;
            }

            LogDebug($"✅ Собрано {newPoints.Count} новых точек. Всего: {collectedPoints.Count}");
            OnPointCloudUpdated?.Invoke(new List<Vector3>(collectedPoints));
      }

      /// <summary>
      /// Создает визуализацию для точки облака
      /// </summary>
      /// <param name="worldPos">Позиция точки в мире</param>
      private void CreatePointVisualization(Vector3 worldPos)
      {
            if (!showPointCloud || pointVisualizationPrefab == null)
                  return;

            GameObject pointViz = Instantiate(pointVisualizationPrefab, pointsParent);
            pointViz.transform.position = worldPos;
            pointViz.transform.localScale = Vector3.one * pointSize;
            pointViz.name = $"CloudPoint_{pointVisualizations.Count}";

            // Применяем цвет
            Renderer renderer = pointViz.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                  renderer.material.color = pointCloudColor;
            }

            pointVisualizations.Add(pointViz);
      }

      /// <summary>
      /// Создает простой префаб точки по умолчанию
      /// </summary>
      private void CreateDefaultPointPrefab()
      {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "DefaultPointPrefab";
            sphere.transform.localScale = Vector3.one * pointSize;

            // Убираем коллайдер
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
                  DestroyImmediate(collider);

            // Создаем яркий материал
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                  Material mat = new Material(Shader.Find("Standard"));
                  mat.color = pointCloudColor;
                  mat.EnableKeyword("_EMISSION");
                  mat.SetColor("_EmissionColor", pointCloudColor * 0.3f);
                  renderer.material = mat;
            }

            sphere.SetActive(false);
            pointVisualizationPrefab = sphere;

            LogDebug("🔵 Создан стандартный префаб точки облака");
      }

      // Временные заглушки - используют те же методы что и ARMaskInteraction
      // В будущем это будет рефакторинг для использования общих утилит

      private int GetSegmentationClassAtPosition(Vector2 screenPos)
      {
            // Заглушка - возвращаем класс стены
            // В реальности здесь будет вызов segmentationManager
            return targetClassId;
      }

      private float GetDepthAtPosition(Vector2 screenPos)
      {
            // Заглушка - возвращаем случайную глубину в разумных пределах
            return Random.Range(1f, 3f);
      }

      private Vector3 ScreenToWorldPosition(Vector2 screenPos, float depth)
      {
            if (arCamera == null)
                  return Vector3.zero;

            // Простое преобразование экран -> мир
            Vector3 screenPoint = new Vector3(screenPos.x, screenPos.y, depth);
            return arCamera.ScreenToWorldPoint(screenPoint);
      }

      private string GetClassName(int classId)
      {
            switch (classId)
            {
                  case 0: return "Стена";
                  case 1: return "Пол";
                  case 2: return "Потолок";
                  default: return "Неизвестно";
            }
      }

      private void LogDebug(string message)
      {
            if (enableDebugLogs)
                  Debug.Log($"[SurfacePointCollector] {message}");
      }

      // Публичные методы для доступа к данным

      /// <summary>
      /// Получает текущее облако точек
      /// </summary>
      public List<Vector3> GetPointCloud()
      {
            return new List<Vector3>(collectedPoints);
      }

      /// <summary>
      /// Получает количество собранных точек
      /// </summary>
      public int GetPointCount()
      {
            return collectedPoints.Count;
      }

      /// <summary>
      /// Устанавливает целевой класс для сбора
      /// </summary>
      public void SetTargetClass(int classId)
      {
            targetClassId = classId;
            LogDebug($"🎯 Целевой класс изменен на: {GetClassName(classId)}");
      }

      /// <summary>
      /// Проверяет, активен ли сбор
      /// </summary>
      public bool IsCollecting()
      {
            return isCollecting;
      }
}

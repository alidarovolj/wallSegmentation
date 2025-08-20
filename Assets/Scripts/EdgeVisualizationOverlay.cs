using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Система визуализации краев и углов поверх AR сцены
/// Отображает обнаруженные контуры и углы стен в реальном времени
/// </summary>
public class EdgeVisualizationOverlay : MonoBehaviour
{
      [Header("Ссылки")]
      [SerializeField] private EdgeCornerDetector edgeDetector;
      [SerializeField] private Canvas overlayCanvas;
      [SerializeField] private RectTransform overlayContainer;

      [Header("Префабы для визуализации")]
      [SerializeField] private GameObject edgePointPrefab;
      [SerializeField] private GameObject cornerPointPrefab;
      [SerializeField] private GameObject edgeLinePrefab;

      [Header("Настройки отображения")]
      [SerializeField] private bool showEdgePoints = true;
      [SerializeField] private bool showCornerPoints = true;
      [SerializeField] private bool showEdgeLines = true;
      [SerializeField] private bool showContourOutline = true;

      [Header("Визуальные параметры")]
      [SerializeField] private float pointSize = 8f;
      [SerializeField] private float lineWidth = 2f;
      [SerializeField] private Color edgePointColor = Color.red;
      [SerializeField] private Color cornerPointColor = Color.yellow;
      [SerializeField] private Color contourLineColor = Color.green;

      [Header("Анимация")]
      [SerializeField] private bool animatePoints = true;
      [SerializeField] private float pulseSpeed = 2f;
      [SerializeField] private float pulseScale = 1.5f;

      // Пулы объектов для оптимизации
      private List<GameObject> edgePointPool = new List<GameObject>();
      private List<GameObject> cornerPointPool = new List<GameObject>();
      private List<GameObject> edgeLinePool = new List<GameObject>();

      // Активные объекты
      private List<GameObject> activeEdgePoints = new List<GameObject>();
      private List<GameObject> activeCornerPoints = new List<GameObject>();
      private List<GameObject> activeEdgeLines = new List<GameObject>();

      void Start()
      {
            // Автопоиск компонентов
            if (edgeDetector == null)
                  edgeDetector = FindObjectOfType<EdgeCornerDetector>();

            // Создаем Canvas если не назначен
            if (overlayCanvas == null)
            {
                  CreateOverlayCanvas();
            }

            // Инициализируем пулы объектов
            InitializeObjectPools();

            Debug.Log("🎨 Визуализация краев и углов инициализирована");
      }

      void Update()
      {
            if (edgeDetector != null)
            {
                  UpdateVisualization();
            }
      }

      /// <summary>
      /// Создает Canvas для наложения визуализации
      /// </summary>
      private void CreateOverlayCanvas()
      {
            GameObject canvasGO = new GameObject("EdgeVisualizationCanvas");
            canvasGO.transform.SetParent(transform);

            overlayCanvas = canvasGO.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 100; // Поверх всего

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();

            // Создаем контейнер для элементов
            GameObject containerGO = new GameObject("OverlayContainer");
            containerGO.transform.SetParent(canvasGO.transform, false);
            overlayContainer = containerGO.AddComponent<RectTransform>();
            overlayContainer.anchorMin = Vector2.zero;
            overlayContainer.anchorMax = Vector2.one;
            overlayContainer.offsetMin = Vector2.zero;
            overlayContainer.offsetMax = Vector2.zero;

            Debug.Log("🖼️ Создан Canvas для визуализации краев");
      }

      /// <summary>
      /// Инициализирует пулы объектов для переиспользования
      /// </summary>
      private void InitializeObjectPools()
      {
            // Создаем пулы объектов заранее
            int poolSize = 100;

            for (int i = 0; i < poolSize; i++)
            {
                  // Пул точек краев
                  edgePointPool.Add(CreateEdgePoint());

                  // Пул угловых точек
                  cornerPointPool.Add(CreateCornerPoint());

                  // Пул линий
                  edgeLinePool.Add(CreateEdgeLine());
            }

            Debug.Log($"🔧 Инициализированы пулы объектов: {poolSize} точек краев, {poolSize} углов, {poolSize} линий");
      }

      /// <summary>
      /// Создает точку края
      /// </summary>
      private GameObject CreateEdgePoint()
      {
            if (edgePointPrefab != null)
            {
                  GameObject point = Instantiate(edgePointPrefab, overlayContainer);
                  point.SetActive(false);
                  return point;
            }

            // Создаем простую точку если префаб не назначен
            GameObject pointGO = new GameObject("EdgePoint");
            pointGO.transform.SetParent(overlayContainer, false);

            Image image = pointGO.AddComponent<Image>();
            image.color = edgePointColor;
            image.sprite = CreateCircleSprite();

            RectTransform rect = pointGO.GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.one * pointSize;

            pointGO.SetActive(false);
            return pointGO;
      }

      /// <summary>
      /// Создает угловую точку
      /// </summary>
      private GameObject CreateCornerPoint()
      {
            if (cornerPointPrefab != null)
            {
                  GameObject point = Instantiate(cornerPointPrefab, overlayContainer);
                  point.SetActive(false);
                  return point;
            }

            // Создаем простую угловую точку
            GameObject pointGO = new GameObject("CornerPoint");
            pointGO.transform.SetParent(overlayContainer, false);

            Image image = pointGO.AddComponent<Image>();
            image.color = cornerPointColor;
            image.sprite = CreateSquareSprite();

            RectTransform rect = pointGO.GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.one * pointSize * 1.5f; // Углы больше краев

            pointGO.SetActive(false);
            return pointGO;
      }

      /// <summary>
      /// Создает линию края
      /// </summary>
      private GameObject CreateEdgeLine()
      {
            if (edgeLinePrefab != null)
            {
                  GameObject line = Instantiate(edgeLinePrefab, overlayContainer);
                  line.SetActive(false);
                  return line;
            }

            // Создаем простую линию
            GameObject lineGO = new GameObject("EdgeLine");
            lineGO.transform.SetParent(overlayContainer, false);

            Image image = lineGO.AddComponent<Image>();
            image.color = contourLineColor;

            RectTransform rect = lineGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100f, lineWidth);

            lineGO.SetActive(false);
            return lineGO;
      }

      /// <summary>
      /// Обновляет визуализацию на основе обнаруженных краев и углов
      /// </summary>
      private void UpdateVisualization()
      {
            // Деактивируем все объекты
            DeactivateAllObjects();

            if (showEdgePoints)
            {
                  ShowEdgePoints();
            }

            if (showCornerPoints)
            {
                  ShowCornerPoints();
            }

            if (showEdgeLines)
            {
                  ShowEdgeLines();
            }

            // Анимируем точки если включено
            if (animatePoints)
            {
                  AnimatePoints();
            }
      }

      /// <summary>
      /// Отображает точки краев
      /// </summary>
      private void ShowEdgePoints()
      {
            var screenEdges = edgeDetector.GetEdgesInScreenSpace();

            for (int i = 0; i < screenEdges.Count && i < edgePointPool.Count; i++)
            {
                  GameObject point = edgePointPool[i];
                  point.SetActive(true);
                  activeEdgePoints.Add(point);

                  // Позиционируем точку
                  RectTransform rect = point.GetComponent<RectTransform>();
                  rect.anchoredPosition = ScreenToCanvasPosition(screenEdges[i]);
            }
      }

      /// <summary>
      /// Отображает угловые точки
      /// </summary>
      private void ShowCornerPoints()
      {
            var screenCorners = edgeDetector.GetCornersInScreenSpace();

            for (int i = 0; i < screenCorners.Count && i < cornerPointPool.Count; i++)
            {
                  GameObject point = cornerPointPool[i];
                  point.SetActive(true);
                  activeCornerPoints.Add(point);

                  // Позиционируем точку
                  RectTransform rect = point.GetComponent<RectTransform>();
                  rect.anchoredPosition = ScreenToCanvasPosition(screenCorners[i]);
            }
      }

      /// <summary>
      /// Отображает линии краев (соединяет соседние точки)
      /// </summary>
      private void ShowEdgeLines()
      {
            var screenEdges = edgeDetector.GetEdgesInScreenSpace();

            // Простой алгоритм соединения близких точек
            for (int i = 0; i < screenEdges.Count - 1 && i < edgeLinePool.Count; i++)
            {
                  Vector2 point1 = screenEdges[i];
                  Vector2 point2 = screenEdges[i + 1];

                  float distance = Vector2.Distance(point1, point2);

                  // Соединяем только близкие точки (порог расстояния)
                  if (distance < 50f) // 50 пикселей
                  {
                        GameObject line = edgeLinePool[i];
                        line.SetActive(true);
                        activeEdgeLines.Add(line);

                        // Позиционируем и поворачиваем линию
                        SetupLineBetweenPoints(line, point1, point2);
                  }
            }
      }

      /// <summary>
      /// Настраивает линию между двумя точками
      /// </summary>
      private void SetupLineBetweenPoints(GameObject line, Vector2 point1, Vector2 point2)
      {
            RectTransform rect = line.GetComponent<RectTransform>();

            // Вычисляем центр и длину
            Vector2 center = (point1 + point2) * 0.5f;
            float length = Vector2.Distance(point1, point2);

            // Вычисляем угол
            Vector2 direction = (point2 - point1).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Устанавливаем позицию, размер и поворот
            rect.anchoredPosition = ScreenToCanvasPosition(center);
            rect.sizeDelta = new Vector2(length, lineWidth);
            rect.rotation = Quaternion.Euler(0, 0, angle);
      }

      /// <summary>
      /// Анимирует точки (пульсация)
      /// </summary>
      private void AnimatePoints()
      {
            float pulseValue = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f) * 0.5f;

            // Анимируем точки краев
            foreach (var point in activeEdgePoints)
            {
                  point.transform.localScale = Vector3.one * pulseValue;
            }

            // Анимируем угловые точки
            foreach (var point in activeCornerPoints)
            {
                  point.transform.localScale = Vector3.one * pulseValue * 1.2f; // Углы пульсируют сильнее
            }
      }

      /// <summary>
      /// Конвертирует экранные координаты в координаты Canvas
      /// </summary>
      private Vector2 ScreenToCanvasPosition(Vector2 screenPos)
      {
            // Простая конвертация для ScreenSpaceOverlay Canvas
            Vector2 canvasSize = overlayContainer.rect.size;
            Vector2 normalizedPos = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            return new Vector2(
                (normalizedPos.x - 0.5f) * canvasSize.x,
                (normalizedPos.y - 0.5f) * canvasSize.y
            );
      }

      /// <summary>
      /// Деактивирует все объекты
      /// </summary>
      private void DeactivateAllObjects()
      {
            foreach (var point in activeEdgePoints)
                  point.SetActive(false);

            foreach (var point in activeCornerPoints)
                  point.SetActive(false);

            foreach (var line in activeEdgeLines)
                  line.SetActive(false);

            activeEdgePoints.Clear();
            activeCornerPoints.Clear();
            activeEdgeLines.Clear();
      }

      /// <summary>
      /// Создает спрайт круга
      /// </summary>
      private Sprite CreateCircleSprite()
      {
            int size = 32;
            Texture2D texture = new Texture2D(size, size);
            Vector2 center = Vector2.one * size * 0.5f;
            float radius = size * 0.4f;

            for (int y = 0; y < size; y++)
            {
                  for (int x = 0; x < size; x++)
                  {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        Color color = distance <= radius ? Color.white : Color.clear;
                        texture.SetPixel(x, y, color);
                  }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
      }

      /// <summary>
      /// Создает спрайт квадрата
      /// </summary>
      private Sprite CreateSquareSprite()
      {
            int size = 32;
            Texture2D texture = new Texture2D(size, size);

            for (int y = 0; y < size; y++)
            {
                  for (int x = 0; x < size; x++)
                  {
                        texture.SetPixel(x, y, Color.white);
                  }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
      }

      /// <summary>
      /// Включает/выключает отображение элементов
      /// </summary>
      public void SetEdgePointsVisible(bool visible) { showEdgePoints = visible; }
      public void SetCornerPointsVisible(bool visible) { showCornerPoints = visible; }
      public void SetEdgeLinesVisible(bool visible) { showEdgeLines = visible; }
      public void SetAnimationEnabled(bool enabled) { animatePoints = enabled; }

      /// <summary>
      /// Контекстные меню
      /// </summary>
      [ContextMenu("Переключить точки краев")]
      public void ToggleEdgePoints() { showEdgePoints = !showEdgePoints; }

      [ContextMenu("Переключить углы")]
      public void ToggleCornerPoints() { showCornerPoints = !showCornerPoints; }

      [ContextMenu("Переключить линии")]
      public void ToggleEdgeLines() { showEdgeLines = !showEdgeLines; }
}


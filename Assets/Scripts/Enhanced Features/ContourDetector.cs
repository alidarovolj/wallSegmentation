using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.Collections;

/// <summary>
/// Детектор контуров на маске сегментации
/// Находит границы сегментированных областей для построения 3D-мешей
/// </summary>
public class ContourDetector : MonoBehaviour
{
      [Header("Настройки обнаружения")]
      [SerializeField] private int targetClassId = 0; // Класс для поиска контуров
      [SerializeField] private float contourSimplificationTolerance = 2f; // Упрощение контура в пикселях
      [SerializeField] private int minContourPoints = 10; // Минимальное количество точек в контуре
      [SerializeField] private bool useGaussianBlur = true; // Размытие перед обнаружением
      [SerializeField] private float blurRadius = 1f; // Радиус размытия

      [Header("Фильтрация")]
      [SerializeField] private float minContourArea = 100f; // Минимальная площадь контура в пикселях
      [SerializeField] private float maxContourArea = 50000f; // Максимальная площадь контура
      [SerializeField] private bool filterByAspectRatio = true; // Фильтр по соотношению сторон
      [SerializeField] private float maxAspectRatio = 10f; // Максимальное соотношение сторон

      [Header("Визуализация")]
      [SerializeField] private bool showContourOverlay = true;
      [SerializeField] private LineRenderer contourLinePrefab;
      [SerializeField] private Color contourColor = Color.yellow;
      [SerializeField] private float lineWidth = 0.02f;

      [Header("Отладка")]
      [SerializeField] private bool enableDebugLogs = true;
      [SerializeField] private bool saveDebugTextures = false;

      // Результаты обнаружения
      private List<List<Vector2>> detectedContours = new List<List<Vector2>>();
      private List<GameObject> contourVisualizations = new List<GameObject>();
      private Transform contoursParent;

      // Зависимости
      private AsyncSegmentationManager segmentationManager;

      // События
      public System.Action<List<List<Vector2>>> OnContoursDetected;

      private void Awake()
      {
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            // Создаем контейнер для визуализации
            GameObject contourContainer = new GameObject("Detected_Contours");
            contoursParent = contourContainer.transform;
            contoursParent.SetParent(transform);

            LogDebug("🔍 ContourDetector инициализирован");
      }

      /// <summary>
      /// Обнаруживает контуры на текущей маске сегментации
      /// </summary>
      public void DetectContours()
      {
            if (segmentationManager == null)
            {
                  LogDebug("❌ AsyncSegmentationManager не найден");
                  return;
            }

            if (!segmentationManager.IsSegmentationMaskReady())
            {
                  LogDebug("❌ Маска сегментации не готова");
                  return;
            }

            RenderTexture maskTexture = segmentationManager.GetCurrentSegmentationMask();
            if (maskTexture == null)
            {
                  LogDebug("❌ Не удалось получить маску сегментации");
                  return;
            }

            StartCoroutine(ProcessMaskForContours(maskTexture));
      }

      /// <summary>
      /// Обнаруживает контуры для определенного класса
      /// </summary>
      /// <param name="classId">ID класса для поиска</param>
      public void DetectContoursForClass(int classId)
      {
            targetClassId = classId;
            DetectContours();
      }

      /// <summary>
      /// Обрабатывает маску для поиска контуров
      /// </summary>
      private IEnumerator ProcessMaskForContours(RenderTexture maskTexture)
      {
            LogDebug($"🔍 Начинаем поиск контуров для класса {targetClassId}");

            // ПРОВЕРКА: убеждаемся что текстура все еще валидна
            if (maskTexture == null || !maskTexture.IsCreated())
            {
                  LogDebug("❌ Маска уничтожена или не создана");
                  yield break;
            }

            // 1. Читаем данные маски с GPU
            var request = UnityEngine.Rendering.AsyncGPUReadback.Request(maskTexture);
            yield return new WaitUntil(() => request.done);

            if (request.hasError)
            {
                  LogDebug("❌ Ошибка чтения маски");
                  yield break;
            }

            var maskData = request.GetData<float>();
            int width = maskTexture.width;
            int height = maskTexture.height;

            LogDebug($"📏 Размер маски: {width}x{height}, точек данных: {maskData.Length}");

            // 2. Создаем бинарную маску для целевого класса
            bool[,] binaryMask = CreateBinaryMask(maskData, width, height, targetClassId);

            // 3. Применяем размытие если включено
            if (useGaussianBlur)
            {
                  binaryMask = ApplyGaussianBlur(binaryMask, width, height, blurRadius);
                  yield return null;
            }

            // 4. Обнаруживаем контуры методом марширующих квадратов
            var contours = FindContoursMarchingSquares(binaryMask, width, height);
            yield return null;

            // 5. Фильтруем и упрощаем контуры
            var filteredContours = FilterAndSimplifyContours(contours, width, height);

            // 6. Сохраняем результаты
            detectedContours = filteredContours;

            LogDebug($"✅ Найдено {detectedContours.Count} валидных контуров");

            // 7. Визуализируем результаты
            if (showContourOverlay)
            {
                  VisualizeContours();
            }

            // 8. Уведомляем подписчиков
            OnContoursDetected?.Invoke(new List<List<Vector2>>(detectedContours));

            // 9. Сохраняем отладочные данные если нужно
            if (saveDebugTextures)
            {
                  SaveDebugTexture(binaryMask, width, height);
            }
      }

      /// <summary>
      /// Создает бинарную маску для определенного класса
      /// </summary>
      private bool[,] CreateBinaryMask(NativeArray<float> maskData, int width, int height, int classId)
      {
            bool[,] binaryMask = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        int index = y * width + x;
                        if (index < maskData.Length)
                        {
                              int pixelClass = Mathf.RoundToInt(maskData[index]);
                              binaryMask[x, y] = (pixelClass == classId);
                        }
                  }
            }

            LogDebug($"🎯 Создана бинарная маска для класса {classId}");
            return binaryMask;
      }

      /// <summary>
      /// Применяет размытие по Гауссу к бинарной маске
      /// </summary>
      private bool[,] ApplyGaussianBlur(bool[,] mask, int width, int height, float radius)
      {
            // Упрощенное размытие для бинарной маски
            bool[,] blurred = new bool[width, height];
            int kernelSize = Mathf.RoundToInt(radius * 2) + 1;
            float sigma = radius / 3f;

            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        float sum = 0f;
                        float weightSum = 0f;

                        for (int ky = -kernelSize / 2; ky <= kernelSize / 2; ky++)
                        {
                              for (int kx = -kernelSize / 2; kx <= kernelSize / 2; kx++)
                              {
                                    int px = Mathf.Clamp(x + kx, 0, width - 1);
                                    int py = Mathf.Clamp(y + ky, 0, height - 1);

                                    float weight = Mathf.Exp(-(kx * kx + ky * ky) / (2 * sigma * sigma));
                                    weightSum += weight;

                                    if (mask[px, py])
                                          sum += weight;
                              }
                        }

                        blurred[x, y] = (sum / weightSum) > 0.5f;
                  }
            }

            return blurred;
      }

      /// <summary>
      /// Находит контуры методом марширующих квадратов (упрощенная версия)
      /// </summary>
      private List<List<Vector2>> FindContoursMarchingSquares(bool[,] mask, int width, int height)
      {
            var contours = new List<List<Vector2>>();
            bool[,] visited = new bool[width, height];

            // Сканируем всю маску в поисках границ
            for (int y = 0; y < height - 1; y++)
            {
                  for (int x = 0; x < width - 1; x++)
                  {
                        if (visited[x, y])
                              continue;

                        // Проверяем, есть ли граница в этом квадрате
                        bool tl = mask[x, y];     // top-left
                        bool tr = mask[x + 1, y];   // top-right
                        bool bl = mask[x, y + 1];   // bottom-left
                        bool br = mask[x + 1, y + 1]; // bottom-right

                        // Если все пиксели одинаковые, граници нет
                        if (tl == tr && tr == bl && bl == br)
                              continue;

                        // Если есть граница, начинаем трассировку контура
                        var contour = TraceContour(mask, width, height, x, y, visited);
                        if (contour.Count >= minContourPoints)
                        {
                              contours.Add(contour);
                        }
                  }
            }

            LogDebug($"🔄 Найдено {contours.Count} предварительных контуров");
            return contours;
      }

      /// <summary>
      /// Трассирует контур начиная с указанной точки
      /// </summary>
      private List<Vector2> TraceContour(bool[,] mask, int width, int height, int startX, int startY, bool[,] visited)
      {
            var contour = new List<Vector2>();
            var directions = new Vector2Int[]
            {
            new Vector2Int(1, 0),   // право
            new Vector2Int(0, 1),   // вниз
            new Vector2Int(-1, 0),  // лево
            new Vector2Int(0, -1)   // вверх
            };

            int x = startX;
            int y = startY;
            int dir = 0;
            int maxSteps = width * height; // Защита от бесконечного цикла

            do
            {
                  contour.Add(new Vector2(x, y));
                  visited[x, y] = true;

                  // Ищем следующую точку границы
                  bool found = false;
                  for (int i = 0; i < 4 && !found; i++)
                  {
                        Vector2Int next = directions[(dir + i) % 4];
                        int nx = x + next.x;
                        int ny = y + next.y;

                        if (nx >= 0 && nx < width - 1 && ny >= 0 && ny < height - 1)
                        {
                              if (IsBoundaryPoint(mask, nx, ny))
                              {
                                    x = nx;
                                    y = ny;
                                    dir = (dir + i) % 4;
                                    found = true;
                              }
                        }
                  }

                  if (!found)
                        break;

                  maxSteps--;
            }
            while ((x != startX || y != startY) && maxSteps > 0);

            return contour;
      }

      /// <summary>
      /// Проверяет, является ли точка граничной
      /// </summary>
      private bool IsBoundaryPoint(bool[,] mask, int x, int y)
      {
            if (x >= mask.GetLength(0) - 1 || y >= mask.GetLength(1) - 1)
                  return false;

            bool current = mask[x, y];
            return (mask[x + 1, y] != current ||
                    mask[x, y + 1] != current ||
                    mask[x + 1, y + 1] != current);
      }

      /// <summary>
      /// Фильтрует и упрощает найденные контуры
      /// </summary>
      private List<List<Vector2>> FilterAndSimplifyContours(List<List<Vector2>> contours, int width, int height)
      {
            var filtered = new List<List<Vector2>>();

            foreach (var contour in contours)
            {
                  // 1. Проверяем размер
                  if (contour.Count < minContourPoints)
                        continue;

                  // 2. Вычисляем площадь контура
                  float area = CalculateContourArea(contour);
                  if (area < minContourArea || area > maxContourArea)
                        continue;

                  // 3. Проверяем соотношение сторон если включено
                  if (filterByAspectRatio)
                  {
                        var bounds = CalculateContourBounds(contour);
                        float aspectRatio = bounds.size.x / Mathf.Max(bounds.size.y, 0.001f);
                        if (aspectRatio > maxAspectRatio || aspectRatio < 1f / maxAspectRatio)
                              continue;
                  }

                  // 4. Упрощаем контур методом Дугласа-Пекера
                  var simplified = SimplifyContourDouglasPeucker(contour, contourSimplificationTolerance);

                  if (simplified.Count >= minContourPoints)
                  {
                        filtered.Add(simplified);
                  }
            }

            LogDebug($"🔽 Отфильтровано контуров: {contours.Count} → {filtered.Count}");
            return filtered;
      }

      /// <summary>
      /// Вычисляет площадь контура методом Шнурочной формулы
      /// </summary>
      private float CalculateContourArea(List<Vector2> contour)
      {
            if (contour.Count < 3)
                  return 0f;

            float area = 0f;
            int j = contour.Count - 1;

            for (int i = 0; i < contour.Count; i++)
            {
                  area += (contour[j].x + contour[i].x) * (contour[j].y - contour[i].y);
                  j = i;
            }

            return Mathf.Abs(area / 2f);
      }

      /// <summary>
      /// Вычисляет границы контура
      /// </summary>
      private Rect CalculateContourBounds(List<Vector2> contour)
      {
            if (contour.Count == 0)
                  return Rect.zero;

            Vector2 min = contour[0];
            Vector2 max = contour[0];

            foreach (var point in contour)
            {
                  min = Vector2.Min(min, point);
                  max = Vector2.Max(max, point);
            }

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
      }

      /// <summary>
      /// Упрощает контур алгоритмом Дугласа-Пекера
      /// </summary>
      private List<Vector2> SimplifyContourDouglasPeucker(List<Vector2> contour, float tolerance)
      {
            if (contour.Count <= 2)
                  return new List<Vector2>(contour);

            var simplified = new List<Vector2>();
            DouglasPeuckerRecursive(contour, 0, contour.Count - 1, tolerance, simplified);

            return simplified;
      }

      /// <summary>
      /// Рекурсивная часть алгоритма Дугласа-Пекера
      /// </summary>
      private void DouglasPeuckerRecursive(List<Vector2> points, int startIndex, int endIndex, float tolerance, List<Vector2> result)
      {
            float maxDistance = 0f;
            int maxIndex = startIndex;

            // Находим точку с максимальным расстоянием от прямой
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                  float distance = PointToLineDistance(points[i], points[startIndex], points[endIndex]);
                  if (distance > maxDistance)
                  {
                        maxDistance = distance;
                        maxIndex = i;
                  }
            }

            // Если максимальное расстояние больше допуска, рекурсивно упрощаем
            if (maxDistance > tolerance)
            {
                  DouglasPeuckerRecursive(points, startIndex, maxIndex, tolerance, result);
                  DouglasPeuckerRecursive(points, maxIndex, endIndex, tolerance, result);
            }
            else
            {
                  // Добавляем только начальную и конечную точки
                  if (result.Count == 0 || result[result.Count - 1] != points[startIndex])
                        result.Add(points[startIndex]);
                  result.Add(points[endIndex]);
            }
      }

      /// <summary>
      /// Вычисляет расстояние от точки до прямой
      /// </summary>
      private float PointToLineDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
      {
            Vector2 line = lineEnd - lineStart;
            Vector2 pointToStart = point - lineStart;

            float lineLength = line.magnitude;
            if (lineLength < 0.001f)
                  return pointToStart.magnitude;

            Vector2 lineNormalized = line / lineLength;
            Vector2 perpendicular = new Vector2(-lineNormalized.y, lineNormalized.x);

            return Mathf.Abs(Vector2.Dot(pointToStart, perpendicular));
      }

      /// <summary>
      /// Визуализирует найденные контуры
      /// </summary>
      private void VisualizeContours()
      {
            // Очищаем предыдущую визуализацию
            ClearVisualization();

            if (contourLinePrefab == null)
            {
                  CreateDefaultLinePrefab();
            }

            for (int i = 0; i < detectedContours.Count; i++)
            {
                  var contour = detectedContours[i];
                  if (contour.Count < 2)
                        continue;

                  // Создаем LineRenderer для контура
                  GameObject lineObj = Instantiate(contourLinePrefab.gameObject, contoursParent);
                  lineObj.name = $"Contour_{i}";

                  LineRenderer line = lineObj.GetComponent<LineRenderer>();
                  if (line != null)
                  {
                        SetupLineRenderer(line, contour);
                  }

                  contourVisualizations.Add(lineObj);
            }

            LogDebug($"🎨 Визуализировано {contourVisualizations.Count} контуров");
      }

      /// <summary>
      /// Настраивает LineRenderer для контура
      /// </summary>
      private void SetupLineRenderer(LineRenderer line, List<Vector2> contour)
      {
            line.positionCount = contour.Count + 1; // +1 для замыкания контура
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            if (line.material != null)
                  line.material.color = contourColor;
            line.useWorldSpace = false;

            // Конвертируем 2D контур в 3D позиции (плоскость Z=0)
            for (int i = 0; i < contour.Count; i++)
            {
                  Vector3 pos = new Vector3(contour[i].x, contour[i].y, 0f);
                  line.SetPosition(i, pos);
            }

            // Замыкаем контур
            line.SetPosition(contour.Count, new Vector3(contour[0].x, contour[0].y, 0f));
      }

      /// <summary>
      /// Создает простой LineRenderer по умолчанию
      /// </summary>
      private void CreateDefaultLinePrefab()
      {
            GameObject lineObj = new GameObject("DefaultContourLine");
            LineRenderer line = lineObj.AddComponent<LineRenderer>();

            // Создаем простой материал
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = contourColor;
            line.material = mat;

            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.useWorldSpace = false;

            lineObj.SetActive(false);
            contourLinePrefab = line;

            LogDebug("📏 Создан стандартный префаб линии контура");
      }

      /// <summary>
      /// Очищает визуализацию контуров
      /// </summary>
      private void ClearVisualization()
      {
            foreach (var viz in contourVisualizations)
            {
                  if (viz != null)
                  {
                        if (Application.isPlaying)
                              Destroy(viz);
                        else
                              DestroyImmediate(viz);
                  }
            }
            contourVisualizations.Clear();
      }

      /// <summary>
      /// Сохраняет отладочную текстуру
      /// </summary>
      private void SaveDebugTexture(bool[,] mask, int width, int height)
      {
            Texture2D debugTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        Color color = mask[x, y] ? Color.white : Color.black;
                        debugTexture.SetPixel(x, y, color);
                  }
            }

            debugTexture.Apply();

            byte[] pngData = debugTexture.EncodeToPNG();
            string path = Application.persistentDataPath + $"/contour_debug_{System.DateTime.Now.Ticks}.png";
            System.IO.File.WriteAllBytes(path, pngData);

            LogDebug($"💾 Отладочная текстура сохранена: {path}");

            if (Application.isPlaying)
                  Destroy(debugTexture);
            else
                  DestroyImmediate(debugTexture);
      }

      private void LogDebug(string message)
      {
            if (enableDebugLogs)
                  Debug.Log($"[ContourDetector] {message}");
      }

      // Публичные методы для доступа к данным

      /// <summary>
      /// Получает обнаруженные контуры
      /// </summary>
      public List<List<Vector2>> GetDetectedContours()
      {
            return new List<List<Vector2>>(detectedContours);
      }

      /// <summary>
      /// Очищает все обнаруженные контуры
      /// </summary>
      public void ClearContours()
      {
            detectedContours.Clear();
            ClearVisualization();
            LogDebug("🗑️ Контуры очищены");
      }

      /// <summary>
      /// Устанавливает целевой класс для поиска
      /// </summary>
      public void SetTargetClass(int classId)
      {
            targetClassId = classId;
            LogDebug($"🎯 Целевой класс изменен на: {classId}");
      }
}

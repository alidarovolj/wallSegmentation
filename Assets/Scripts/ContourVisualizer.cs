using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Система визуализации контуров и углов на маске сегментации
/// Интегрируется с AsyncSegmentationManager и EdgeCornerDetector
/// для отображения черных линий в углах комнаты
/// </summary>
public class ContourVisualizer : MonoBehaviour
{
      [Header("Ссылки на компоненты")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private EdgeCornerDetector edgeDetector;
      [SerializeField] private ARWallPresenter arWallPresenter;

      [Header("Настройки визуализации")]
      [SerializeField] private bool enableContourVisualization = true;
      [SerializeField] private Color contourColor = Color.black;
      [SerializeField] private float contourThickness = 2.0f;
      [SerializeField] private bool showCorners = true;
      [SerializeField] private bool showEdges = true;

      [Header("Производительность")]
      [SerializeField] private int processingFrameRate = 5; // Обрабатывать каждые N кадров
      [SerializeField] private bool useGPUProcessing = true;

      [Header("Ссылки на шейдеры (Shader References)")]
      [Tooltip("Назначьте стандартный шейдер 'Unlit/Texture' сюда")]
      [SerializeField] private Shader contourShader;

      // Текстуры для контуров
      private RenderTexture contourTexture;
      private RenderTexture combinedTexture;
      private Material contourMaterial;

      // Кэш для оптимизации
      private int frameCounter = 0;
      private bool isProcessingContours = false;

      // Результаты анализа
      private List<Vector2> lastDetectedCorners = new List<Vector2>();
      private List<Vector2> lastDetectedEdges = new List<Vector2>();

      void Start()
      {
            InitializeComponents();
            InitializeTextures();
            CreateContourMaterial();

            Debug.Log("🔍 ContourVisualizer инициализирован");
      }

      void Update()
      {
            if (!enableContourVisualization) return;

            frameCounter++;

            // Обрабатываем контуры не каждый кадр для оптимизации
            if (frameCounter % processingFrameRate == 0 && !isProcessingContours)
            {
                  StartCoroutine(ProcessContoursAsync());
            }
      }

      [ContextMenu("ВЫКЛЮЧИТЬ визуализацию контуров")]
      public void DisableContourVisualization()
      {
            enableContourVisualization = false;
            Debug.Log("❌ Визуализация контуров ОТКЛЮЧЕНА - теперь показывается только обычная маска");
      }

      [ContextMenu("ВКЛЮЧИТЬ визуализацию контуров")]
      public void EnableContourVisualization()
      {
            enableContourVisualization = true;
            Debug.Log("✅ Визуализация контуров ВКЛЮЧЕНА");
      }

      /// <summary>
      /// Инициализирует ссылки на компоненты
      /// </summary>
      private void InitializeComponents()
      {
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            if (edgeDetector == null)
                  edgeDetector = FindObjectOfType<EdgeCornerDetector>();

            if (arWallPresenter == null)
                  arWallPresenter = FindObjectOfType<ARWallPresenter>();

            // Создаем EdgeCornerDetector если его нет
            if (edgeDetector == null)
            {
                  GameObject detectorGO = new GameObject("EdgeCornerDetector");
                  edgeDetector = detectorGO.AddComponent<EdgeCornerDetector>();
                  Debug.Log("🔧 EdgeCornerDetector создан автоматически");
            }

            Debug.Log($"🔗 Компоненты найдены: SegmentationManager={segmentationManager != null}, EdgeDetector={edgeDetector != null}, ARWallPresenter={arWallPresenter != null}");
      }

      /// <summary>
      /// Инициализирует текстуры для обработки контуров
      /// </summary>
      private void InitializeTextures()
      {
            int textureSize = 512; // Размер маски TopFormer

            // Текстура для контуров
            contourTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            contourTexture.enableRandomWrite = true;
            contourTexture.Create();

            // Текстура для комбинированного результата (маска + контуры)
            combinedTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            combinedTexture.enableRandomWrite = true;
            combinedTexture.Create();

            Debug.Log($"🖼️ Текстуры для контуров инициализированы: {textureSize}x{textureSize}");
      }

      /// <summary>
      /// Создает материал для отрисовки контуров
      /// </summary>
      private void CreateContourMaterial()
      {
            // Создаем простой материал для отрисовки линий
            if (this.contourShader != null)
            {
                  contourMaterial = new Material(this.contourShader);
                  contourMaterial.color = contourColor;
                  Debug.Log("🎨 Материал для контуров создан");
            }
            else
            {
                  Debug.LogWarning("⚠️ Не удалось найти шейдер для контуров. Назначьте шейдер 'Unlit/Texture' в поле 'Contour Shader' компонента ContourVisualizer.");
            }
      }

      /// <summary>
      /// Асинхронно обрабатывает контуры
      /// </summary>
      private IEnumerator ProcessContoursAsync()
      {
            if (isProcessingContours) yield break;
            isProcessingContours = true;

            try
            {
                  // Получаем текущую маску сегментации
                  var maskTexture = segmentationManager?.GetSegmentationMask();
                  if (maskTexture == null)
                  {
                        yield break;
                  }

                  // Обновляем обнаружение краев и углов
                  if (edgeDetector != null)
                  {
                        // EdgeCornerDetector работает автоматически, просто получаем результаты
                        lastDetectedCorners = edgeDetector.GetCornersInScreenSpace();
                        lastDetectedEdges = edgeDetector.GetEdgesInScreenSpace();
                  }

                  // Создаем текстуру с контурами
                  yield return StartCoroutine(CreateContourTexture());

                  // Комбинируем маску с контурами
                  CombineMaskWithContours(maskTexture);

                  // Передаем результат в ARWallPresenter
                  if (arWallPresenter != null)
                  {
                        arWallPresenter.SetSegmentationMask(combinedTexture);
                        Debug.Log($"🎨 ContourVisualizer: Передана комбинированная текстура в ARWallPresenter (краев: {lastDetectedEdges.Count}, углов: {lastDetectedCorners.Count})");
                  }
                  else
                  {
                        Debug.LogWarning("⚠️ ContourVisualizer: ARWallPresenter не найден!");
                  }

            }
            finally
            {
                  isProcessingContours = false;
            }
      }

      /// <summary>
      /// Создает текстуру с контурами
      /// </summary>
      private IEnumerator CreateContourTexture()
      {
            // Очищаем текстуру контуров
            RenderTexture.active = contourTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = null;

            if (useGPUProcessing)
            {
                  // GPU обработка через Graphics.DrawTexture или Custom Render Targets
                  yield return StartCoroutine(DrawContoursGPU());
            }
            else
            {
                  // CPU обработка
                  DrawContoursCPU();
            }

            yield return null;
      }

      /// <summary>
      /// Рисует контуры на GPU (более производительно)
      /// </summary>
      private IEnumerator DrawContoursGPU()
      {
            // Используем Graphics API для рисования линий
            RenderTexture.active = contourTexture;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, contourTexture.width, contourTexture.height, 0);

            if (contourMaterial != null)
            {
                  contourMaterial.SetPass(0);

                  GL.Begin(GL.LINES);
                  GL.Color(contourColor);

                  // Рисуем углы
                  if (showCorners)
                  {
                        DrawCorners();
                  }

                  // Рисуем края
                  if (showEdges)
                  {
                        DrawEdgeLines();
                  }

                  GL.End();
            }

            GL.PopMatrix();
            RenderTexture.active = null;

            yield return null;
      }

      /// <summary>
      /// Рисует углы как крестики
      /// </summary>
      private void DrawCorners()
      {
            float crossSize = contourThickness * 3f;

            foreach (var corner in lastDetectedCorners)
            {
                  // Конвертируем экранные координаты в координаты текстуры
                  Vector2 texCoord = ScreenToTextureCoordinates(corner);

                  // Рисуем крестик
                  // Горизонтальная линия
                  GL.Vertex3(texCoord.x - crossSize, texCoord.y, 0);
                  GL.Vertex3(texCoord.x + crossSize, texCoord.y, 0);

                  // Вертикальная линия
                  GL.Vertex3(texCoord.x, texCoord.y - crossSize, 0);
                  GL.Vertex3(texCoord.x, texCoord.y + crossSize, 0);
            }
      }

      /// <summary>
      /// Рисует линии краев
      /// </summary>
      private void DrawEdgeLines()
      {
            // Соединяем близкие точки краев линиями
            for (int i = 0; i < lastDetectedEdges.Count - 1; i++)
            {
                  Vector2 point1 = ScreenToTextureCoordinates(lastDetectedEdges[i]);
                  Vector2 point2 = ScreenToTextureCoordinates(lastDetectedEdges[i + 1]);

                  // Рисуем линию только между близкими точками
                  float distance = Vector2.Distance(point1, point2);
                  if (distance < 50f) // Порог близости в пикселях
                  {
                        GL.Vertex3(point1.x, point1.y, 0);
                        GL.Vertex3(point2.x, point2.y, 0);
                  }
            }
      }

      /// <summary>
      /// CPU версия отрисовки контуров (запасной вариант)
      /// </summary>
      private void DrawContoursCPU()
      {
            // Простая CPU реализация для совместимости
            // В реальном проекте лучше использовать GPU версию
            Debug.Log("🖥️ Использована CPU отрисовка контуров");
      }

      /// <summary>
      /// Комбинирует маску сегментации с контурами
      /// </summary>
      private void CombineMaskWithContours(RenderTexture maskTexture)
      {
            // УПРОЩЕННЫЙ ПОДХОД: Просто рисуем контуры прямо на исходной маске
            // Это более надежно для начала

            // Используем простое наложение контуров
            Graphics.Blit(maskTexture, combinedTexture);

            // Рисуем простые линии поверх маски
            DrawSimpleContoursOnTexture(combinedTexture);
      }

      /// <summary>
      /// Рисует простые контуры прямо на текстуре
      /// </summary>
      private void DrawSimpleContoursOnTexture(RenderTexture targetTexture)
      {
            if (lastDetectedEdges.Count == 0) return;

            // Простой подход: рисуем белые квадраты в местах краев
            RenderTexture.active = targetTexture;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, targetTexture.width, targetTexture.height, 0);

            GL.Begin(GL.QUADS); // Используем QUADS вместо POINTS
            GL.Color(Color.white); // Белые квадраты для видимости

            // Рисуем квадраты краев
            float pointSize = contourThickness * 5; // УВЕЛИЧИВАЕМ размер точки в 5 раз для видимости
            foreach (var edge in lastDetectedEdges)
            {
                  Vector2 texCoord = ScreenToTextureCoordinates(edge);

                  // Рисуем квадрат больших размеров
                  float x = texCoord.x;
                  float y = texCoord.y;

                  GL.Vertex3(x - pointSize, y - pointSize, 0); // Левый нижний
                  GL.Vertex3(x + pointSize, y - pointSize, 0); // Правый нижний
                  GL.Vertex3(x + pointSize, y + pointSize, 0); // Правый верхний
                  GL.Vertex3(x - pointSize, y + pointSize, 0); // Левый верхний
            }

            // Рисуем углы большими красными квадратами
            GL.Color(Color.red);
            float cornerSize = contourThickness * 10; // УВЕЛИЧИВАЕМ размер углов
            foreach (var corner in lastDetectedCorners)
            {
                  Vector2 texCoord = ScreenToTextureCoordinates(corner);

                  float x = texCoord.x;
                  float y = texCoord.y;

                  GL.Vertex3(x - cornerSize, y - cornerSize, 0);
                  GL.Vertex3(x + cornerSize, y - cornerSize, 0);
                  GL.Vertex3(x + cornerSize, y + cornerSize, 0);
                  GL.Vertex3(x - cornerSize, y + cornerSize, 0);
            }

            GL.End();
            GL.PopMatrix();

            RenderTexture.active = null;

            Debug.Log($"🎨 Нарисовано {lastDetectedEdges.Count} краев (белые) и {lastDetectedCorners.Count} углов (красные) на текстуре");
      }

      /// <summary>
      /// Конвертирует экранные координаты в координаты текстуры
      /// </summary>
      private Vector2 ScreenToTextureCoordinates(Vector2 screenPos)
      {
            // Нормализуем экранные координаты (0-1)
            Vector2 normalizedPos = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            // Конвертируем в координаты текстуры
            return new Vector2(
                normalizedPos.x * contourTexture.width,
                (1f - normalizedPos.y) * contourTexture.height // Инвертируем Y для текстуры
            );
      }

      /// <summary>
      /// Включает/выключает визуализацию контуров
      /// </summary>
      public void SetContourVisualization(bool enabled)
      {
            enableContourVisualization = enabled;
            Debug.Log($"🔍 Визуализация контуров: {(enabled ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
      }

      /// <summary>
      /// Устанавливает цвет контуров
      /// </summary>
      public void SetContourColor(Color color)
      {
            contourColor = color;
            if (contourMaterial != null)
            {
                  contourMaterial.color = contourColor;
            }
            Debug.Log($"🎨 Цвет контуров изменен на: {ColorUtility.ToHtmlStringRGB(color)}");
      }

      /// <summary>
      /// Устанавливает толщину контуров
      /// </summary>
      public void SetContourThickness(float thickness)
      {
            contourThickness = Mathf.Max(0.5f, thickness);
            Debug.Log($"📏 Толщина контуров: {contourThickness}");
      }

      /// <summary>
      /// Получает статистику обнаружения
      /// </summary>
      public void PrintDetectionStats()
      {
            Debug.Log($"📊 Статистика обнаружения:");
            Debug.Log($"   🔍 Углов найдено: {lastDetectedCorners.Count}");
            Debug.Log($"   📏 Краев найдено: {lastDetectedEdges.Count}");
            Debug.Log($"   🎨 Визуализация: {(enableContourVisualization ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
            Debug.Log($"   🖥️ Обработка: {(useGPUProcessing ? "GPU" : "CPU")}");
      }

      void OnDestroy()
      {
            // Освобождаем ресурсы
            if (contourTexture != null)
            {
                  contourTexture.Release();
                  Destroy(contourTexture);
            }

            if (combinedTexture != null)
            {
                  combinedTexture.Release();
                  Destroy(combinedTexture);
            }

            if (contourMaterial != null)
            {
                  Destroy(contourMaterial);
            }
      }

      /// <summary>
      /// Контекстные меню для тестирования
      /// </summary>
      [ContextMenu("Переключить визуализацию")]
      public void ToggleVisualization()
      {
            SetContourVisualization(!enableContourVisualization);
      }

      [ContextMenu("Установить черный цвет")]
      public void SetBlackContours()
      {
            SetContourColor(Color.black);
      }

      [ContextMenu("Установить красный цвет")]
      public void SetRedContours()
      {
            SetContourColor(Color.red);
      }

      [ContextMenu("Показать статистику")]
      public void ShowStats()
      {
            PrintDetectionStats();
      }

      [ContextMenu("ТЕСТ: Принудительно нарисовать контуры")]
      public void ForceDrawTestContours()
      {
            // Создаем тестовые точки для проверки
            lastDetectedCorners.Clear();
            lastDetectedEdges.Clear();

            // Добавляем тестовые углы (4 угла экрана)
            lastDetectedCorners.Add(new Vector2(0.1f, 0.1f)); // Левый верх
            lastDetectedCorners.Add(new Vector2(0.9f, 0.1f)); // Правый верх  
            lastDetectedCorners.Add(new Vector2(0.9f, 0.9f)); // Правый низ
            lastDetectedCorners.Add(new Vector2(0.1f, 0.9f)); // Левый низ

            // Добавляем тестовые края (рамка)
            for (float t = 0.1f; t <= 0.9f; t += 0.05f)
            {
                  lastDetectedEdges.Add(new Vector2(t, 0.1f)); // Верх
                  lastDetectedEdges.Add(new Vector2(t, 0.9f)); // Низ
                  lastDetectedEdges.Add(new Vector2(0.1f, t)); // Лево
                  lastDetectedEdges.Add(new Vector2(0.9f, t)); // Право
            }

            Debug.Log($"🧪 ТЕСТ: Созданы тестовые контуры - углов: {lastDetectedCorners.Count}, краев: {lastDetectedEdges.Count}");

            // Принудительно запускаем обработку
            StartCoroutine(ProcessContoursAsync());
      }

      [ContextMenu("ТЕСТ: Простая заливка контуров")]
      public void ForceTestFillContours()
      {
            // Получаем маску от AsyncSegmentationManager
            RenderTexture maskTexture = segmentationManager?.GetSegmentationMask();
            if (maskTexture == null)
            {
                  Debug.LogError("❌ Маска сегментации не найдена для теста!");
                  return;
            }

            // Создаем копию и заливаем ее зеленым
            RenderTexture testTexture = RenderTexture.GetTemporary(maskTexture.width, maskTexture.height, 0, RenderTextureFormat.ARGB32);

            // Заливаем всю текстуру зеленым цветом
            RenderTexture.active = testTexture;
            GL.Clear(true, true, Color.green);
            RenderTexture.active = null;

            // Передаем зеленую текстуру в ARWallPresenter
            if (arWallPresenter != null)
            {
                  arWallPresenter.SetSegmentationMask(testTexture);
                  Debug.Log("🟢 ТЕСТ: Передана ЗЕЛЕНАЯ тестовая текстура в ARWallPresenter!");
            }

            // Освобождаем через 3 секунды
            StartCoroutine(ReleaseTestTextureAfterDelay(testTexture, 3.0f));
      }

      private System.Collections.IEnumerator ReleaseTestTextureAfterDelay(RenderTexture texture, float delay)
      {
            yield return new WaitForSeconds(delay);
            RenderTexture.ReleaseTemporary(texture);
            Debug.Log("🗑️ Тестовая текстура освобождена");
      }
}

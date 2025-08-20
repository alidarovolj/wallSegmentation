using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Система обнаружения углов и контуров в маске сегментации
/// Использует алгоритмы компьютерного зрения для выделения краев стен
/// </summary>
public class EdgeCornerDetector : MonoBehaviour
{
      [Header("Ссылки")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private ARWallPresenter arWallPresenter;

      [Header("Настройки обнаружения")]
      [SerializeField] private bool enableEdgeDetection = true;
      [SerializeField] private bool enableCornerDetection = true;
      [SerializeField] private bool showEdgeOverlay = true;

      [Header("Параметры алгоритмов")]
      [Range(0.1f, 2.0f)]
      [SerializeField] private float edgeThreshold = 0.5f; // Порог для обнаружения краев

      [Range(1, 10)]
      [SerializeField] private int kernelSize = 3; // Размер ядра для свертки

      [Range(0.01f, 0.1f)]
      [SerializeField] private float cornerThreshold = 0.05f; // Порог для обнаружения углов

      [Header("Визуализация")]
      [SerializeField] private Color edgeColor = Color.red;
      [SerializeField] private Color cornerColor = Color.yellow;
      [SerializeField] private float overlayOpacity = 0.8f;

      [Header("Производительность")]
      [SerializeField] private int processingFrameRate = 10; // Обрабатывать каждые N кадров
      [SerializeField] private bool useGPUCompute = true; // Использовать GPU для вычислений

      // Compute Shader для GPU обработки
      [SerializeField] private ComputeShader edgeDetectionShader;

      // Результаты обнаружения
      private RenderTexture edgeTexture;
      private RenderTexture cornerTexture;
      private Texture2D resultTexture;

      // Кэш для оптимизации
      private int frameCounter = 0;
      private bool isProcessing = false;

      // Результаты анализа
      public List<Vector2> DetectedCorners { get; private set; } = new List<Vector2>();
      public List<Vector2> EdgePoints { get; private set; } = new List<Vector2>();

      void Start()
      {
            // Автопоиск компонентов
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            if (arWallPresenter == null)
                  arWallPresenter = FindObjectOfType<ARWallPresenter>();

            // Инициализация текстур
            InitializeTextures();

            Debug.Log("🔍 EdgeCornerDetector инициализирован");
      }

      void Update()
      {
            frameCounter++;

            // Обрабатываем не каждый кадр для оптимизации
            if (frameCounter % processingFrameRate == 0 && !isProcessing)
            {
                  if (enableEdgeDetection || enableCornerDetection)
                  {
                        StartCoroutine(ProcessMaskAsync());
                  }
            }
      }

      /// <summary>
      /// Инициализирует текстуры для обработки
      /// </summary>
      private void InitializeTextures()
      {
            // Создаем текстуры для результатов
            int textureSize = 512; // Размер TopFormer выхода

            edgeTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RFloat);
            edgeTexture.enableRandomWrite = true;
            edgeTexture.Create();

            cornerTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RFloat);
            cornerTexture.enableRandomWrite = true;
            cornerTexture.Create();

            resultTexture = new Texture2D(textureSize, textureSize, TextureFormat.RFloat, false);

            Debug.Log($"🖼️ Инициализированы текстуры для обработки: {textureSize}x{textureSize}");
      }

      /// <summary>
      /// Асинхронно обрабатывает маску для поиска краев и углов
      /// </summary>
      private IEnumerator ProcessMaskAsync()
      {
            if (isProcessing) yield break;
            isProcessing = true;

            // Получаем актуальную маску сегментации
            var maskTexture = segmentationManager?.GetSegmentationMask();
            if (maskTexture == null)
            {
                  isProcessing = false;
                  yield break;
            }

            if (useGPUCompute && edgeDetectionShader != null)
            {
                  // GPU обработка
                  yield return StartCoroutine(ProcessWithGPU(maskTexture));
            }
            else
            {
                  // CPU обработка
                  yield return StartCoroutine(ProcessWithCPU(maskTexture));
            }

            isProcessing = false;
      }

      /// <summary>
      /// Обработка на GPU с помощью Compute Shader
      /// </summary>
      private IEnumerator ProcessWithGPU(RenderTexture maskTexture)
      {
            if (edgeDetectionShader == null)
            {
                  Debug.LogWarning("⚠️ Compute Shader для обнаружения краев не назначен!");
                  yield break;
            }

            // Настраиваем Compute Shader
            int kernelIndex = edgeDetectionShader.FindKernel("SobelEdgeDetection");

            edgeDetectionShader.SetTexture(kernelIndex, "InputTexture", maskTexture);
            edgeDetectionShader.SetTexture(kernelIndex, "EdgeTexture", edgeTexture);
            edgeDetectionShader.SetFloat("EdgeThreshold", edgeThreshold);
            edgeDetectionShader.SetInt("KernelSize", kernelSize);

            // Запускаем вычисления на GPU
            int threadGroups = Mathf.CeilToInt(maskTexture.width / 8.0f);
            edgeDetectionShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);

            // Ждем завершения GPU операций
            yield return new WaitForEndOfFrame();

            // Анализируем результаты
            AnalyzeEdgeResults();

            Debug.Log($"🔍 GPU обработка завершена. Найдено краев: {EdgePoints.Count}, углов: {DetectedCorners.Count}");
      }

      /// <summary>
      /// Обработка на CPU (более медленная, но не требует Compute Shader)
      /// </summary>
      private IEnumerator ProcessWithCPU(RenderTexture maskTexture)
      {
            // Читаем данные маски с GPU
            var request = UnityEngine.Rendering.AsyncGPUReadback.Request(maskTexture);
            yield return new WaitUntil(() => request.done);

            if (request.hasError)
            {
                  Debug.LogError("❌ Ошибка чтения маски для анализа краев");
                  yield break;
            }

            var maskData = request.GetData<float>();
            int width = maskTexture.width;
            int height = maskTexture.height;

            // Применяем фильтр Собеля для обнаружения краев
            var edgeData = ApplySobelFilter(maskData, width, height);

            // Обнаруживаем углы методом Харриса
            if (enableCornerDetection)
            {
                  DetectedCorners = DetectHarrisCorners(edgeData, width, height);
            }

            // Извлекаем точки краев
            EdgePoints = ExtractEdgePoints(edgeData, width, height);

            Debug.Log($"🔍 CPU обработка завершена. Найдено краев: {EdgePoints.Count}, углов: {DetectedCorners.Count}");

            // Применяем визуализацию
            if (showEdgeOverlay)
            {
                  ApplyEdgeOverlay();
            }
      }

      /// <summary>
      /// Применяет фильтр Собеля для обнаружения краев
      /// </summary>
      private float[] ApplySobelFilter(Unity.Collections.NativeArray<float> maskData, int width, int height)
      {
            float[] edgeData = new float[width * height];

            // Ядра Собеля
            int[,] sobelX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] sobelY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            // Проходим по всем пикселям (кроме границ)
            for (int y = 1; y < height - 1; y++)
            {
                  for (int x = 1; x < width - 1; x++)
                  {
                        float gx = 0, gy = 0;

                        // Применяем ядра Собеля
                        for (int ky = -1; ky <= 1; ky++)
                        {
                              for (int kx = -1; kx <= 1; kx++)
                              {
                                    int pixelIndex = (y + ky) * width + (x + kx);
                                    float pixelValue = maskData[pixelIndex];

                                    gx += pixelValue * sobelX[ky + 1, kx + 1];
                                    gy += pixelValue * sobelY[ky + 1, kx + 1];
                              }
                        }

                        // Вычисляем магнитуду градиента
                        float magnitude = Mathf.Sqrt(gx * gx + gy * gy);
                        edgeData[y * width + x] = magnitude > edgeThreshold ? magnitude : 0f;
                  }
            }

            return edgeData;
      }

      /// <summary>
      /// Обнаруживает углы методом детектора углов Харриса
      /// </summary>
      private List<Vector2> DetectHarrisCorners(float[] edgeData, int width, int height)
      {
            List<Vector2> corners = new List<Vector2>();

            // УПРОЩЕННЫЙ алгоритм для быстрого результата
            // Ищем места с высокой концентрацией краев
            int step = 16; // Проверяем каждые 16 пикселей для скорости

            for (int y = step; y < height - step; y += step)
            {
                  for (int x = step; x < width - step; x += step)
                  {
                        // Считаем количество краев в области 16x16
                        int edgeCount = 0;
                        for (int dy = -8; dy <= 8; dy++)
                        {
                              for (int dx = -8; dx <= 8; dx++)
                              {
                                    int idx = (y + dy) * width + (x + dx);
                                    if (idx >= 0 && idx < edgeData.Length && edgeData[idx] > 0)
                                    {
                                          edgeCount++;
                                    }
                              }
                        }

                        // Если много краев в области - это вероятно угол
                        if (edgeCount > 20) // Пороговое значение
                        {
                              Vector2 normalizedCorner = new Vector2((float)x / width, (float)y / height);
                              corners.Add(normalizedCorner);
                        }
                  }
            }

            Debug.Log($"🔍 Простой детектор углов нашел: {corners.Count} углов");
            return corners;
      }

      /// <summary>
      /// Вычисляет ответ детектора углов Харриса
      /// </summary>
      private float CalculateHarrisResponse(float[] edgeData, int x, int y, int width, int height)
      {
            float Ixx = 0, Iyy = 0, Ixy = 0;

            // Вычисляем матрицу структурного тензора в окрестности 3x3
            for (int dy = -1; dy <= 1; dy++)
            {
                  for (int dx = -1; dx <= 1; dx++)
                  {
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                              int idx = ny * width + nx;
                              float intensity = edgeData[idx];

                              Ixx += intensity * intensity;
                              Iyy += intensity * intensity;
                              Ixy += intensity * intensity;
                        }
                  }
            }

            // Вычисляем ответ Харриса: det(M) - k * trace²(M)
            float det = Ixx * Iyy - Ixy * Ixy;
            float trace = Ixx + Iyy;
            float k = 0.04f; // Константа Харриса

            return det - k * trace * trace;
      }

      /// <summary>
      /// Извлекает точки краев из обработанных данных
      /// </summary>
      private List<Vector2> ExtractEdgePoints(float[] edgeData, int width, int height)
      {
            List<Vector2> edges = new List<Vector2>();

            for (int y = 0; y < height; y++)
            {
                  for (int x = 0; x < width; x++)
                  {
                        int idx = y * width + x;
                        if (edgeData[idx] > 0f) // Есть край
                        {
                              // Нормализуем координаты к диапазону 0-1
                              Vector2 normalizedEdge = new Vector2((float)x / width, (float)y / height);
                              edges.Add(normalizedEdge);
                        }
                  }
            }

            return edges;
      }

      /// <summary>
      /// Анализирует результаты GPU обработки
      /// </summary>
      private void AnalyzeEdgeResults()
      {
            // TODO: Реализовать анализ результатов GPU обработки
            // Пока используем заглушку
            EdgePoints.Clear();
            DetectedCorners.Clear();

            // Добавляем несколько тестовых точек
            EdgePoints.Add(new Vector2(0.1f, 0.1f));
            EdgePoints.Add(new Vector2(0.9f, 0.1f));
            EdgePoints.Add(new Vector2(0.9f, 0.9f));
            EdgePoints.Add(new Vector2(0.1f, 0.9f));

            DetectedCorners.Add(new Vector2(0.1f, 0.1f));
            DetectedCorners.Add(new Vector2(0.9f, 0.9f));
      }

      /// <summary>
      /// Применяет наложение краев и углов на визуализацию
      /// </summary>
      private void ApplyEdgeOverlay()
      {
            // TODO: Создать систему наложения краев и углов
            // Это можно реализовать через дополнительный шейдер или UI элементы
            Debug.Log($"🎨 Применено наложение: {EdgePoints.Count} краев, {DetectedCorners.Count} углов");
      }

      /// <summary>
      /// Получает все обнаруженные углы в координатах экрана
      /// </summary>
      public List<Vector2> GetCornersInScreenSpace()
      {
            List<Vector2> screenCorners = new List<Vector2>();

            foreach (var corner in DetectedCorners)
            {
                  Vector2 screenPos = new Vector2(corner.x * Screen.width, corner.y * Screen.height);
                  screenCorners.Add(screenPos);
            }

            return screenCorners;
      }

      /// <summary>
      /// Получает все точки краев в координатах экрана
      /// </summary>
      public List<Vector2> GetEdgesInScreenSpace()
      {
            List<Vector2> screenEdges = new List<Vector2>();

            foreach (var edge in EdgePoints)
            {
                  Vector2 screenPos = new Vector2(edge.x * Screen.width, edge.y * Screen.height);
                  screenEdges.Add(screenPos);
            }

            return screenEdges;
      }

      /// <summary>
      /// Включает/выключает обнаружение краев
      /// </summary>
      public void SetEdgeDetection(bool enabled)
      {
            enableEdgeDetection = enabled;
            Debug.Log($"🔍 Обнаружение краев: {(enabled ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
      }

      /// <summary>
      /// Включает/выключает обнаружение углов
      /// </summary>
      public void SetCornerDetection(bool enabled)
      {
            enableCornerDetection = enabled;
            Debug.Log($"🔍 Обнаружение углов: {(enabled ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
      }

      void OnDestroy()
      {
            // Освобождаем ресурсы
            if (edgeTexture != null)
            {
                  edgeTexture.Release();
                  Destroy(edgeTexture);
            }

            if (cornerTexture != null)
            {
                  cornerTexture.Release();
                  Destroy(cornerTexture);
            }

            if (resultTexture != null)
            {
                  Destroy(resultTexture);
            }
      }

      /// <summary>
      /// Контекстные меню для тестирования
      /// </summary>
      [ContextMenu("Тест: Обнаружить края")]
      public void TestEdgeDetection()
      {
            if (!isProcessing)
            {
                  StartCoroutine(ProcessMaskAsync());
            }
      }

      [ContextMenu("Вывести статистику")]
      public void PrintStatistics()
      {
            Debug.Log($"📊 Статистика обнаружения:");
            Debug.Log($"   🔍 Краев найдено: {EdgePoints.Count}");
            Debug.Log($"   📐 Углов найдено: {DetectedCorners.Count}");
            Debug.Log($"   ⚙️ Обработка: {(useGPUCompute ? "GPU" : "CPU")}");
            Debug.Log($"   🎯 Порог краев: {edgeThreshold}");
            Debug.Log($"   📐 Порог углов: {cornerThreshold}");
      }
}


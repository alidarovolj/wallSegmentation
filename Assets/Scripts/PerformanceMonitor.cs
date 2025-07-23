using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Profiling;

/// <summary>
/// Система мониторинга производительности AR-приложения
/// Отслеживает ключевые метрики и предоставляет UI для анализа
/// </summary>
public class PerformanceMonitor : MonoBehaviour
{
      [Header("UI References")]
      [SerializeField] private TextMeshProUGUI fpsText;
      [SerializeField] private TextMeshProUGUI inferenceTimeText;
      [SerializeField] private TextMeshProUGUI memoryText;
      [SerializeField] private TextMeshProUGUI gpuMemoryText;
      [SerializeField] private Slider performanceSlider;
      [SerializeField] private Image performanceBar;

      [Header("Settings")]
      [SerializeField] private bool enableUI = true;
      [SerializeField] private float updateInterval = 0.5f;
      [SerializeField] private int historySize = 60;

      [Header("Performance Targets")]
      [SerializeField] private float targetFPS = 30f;
      [SerializeField] private float targetInferenceTime = 50f; // ms

      [Header("References")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;

      // Performance tracking
      private readonly List<float> fpsHistory = new List<float>();
      private readonly List<float> inferenceHistory = new List<float>();
      private float lastUpdateTime;
      private int frameCount;

      // Unity Profiler markers
      private ProfilerMarker argmaxMarker = new ProfilerMarker("GPU.Argmax");
      private ProfilerMarker inferenceMarker = new ProfilerMarker("ML.Inference");
      private ProfilerMarker paintingMarker = new ProfilerMarker("Painting.Update");

      // Memory tracking
      private long lastGCMemory;
      private float lastGPUMemory;

      // Performance score (0-100)
      private float currentPerformanceScore = 100f;

      void Start()
      {
            lastUpdateTime = Time.realtimeSinceStartup;

            if (!enableUI)
            {
                  HideUI();
            }

            // Инициализация Unity Profiler
            Application.targetFrameRate = (int)targetFPS;

            Debug.Log("📊 PerformanceMonitor инициализирован");
      }

      void Update()
      {
            frameCount++;

            // Обновляем UI с заданным интервалом
            if (Time.realtimeSinceStartup - lastUpdateTime >= updateInterval)
            {
                  UpdatePerformanceMetrics();
                  lastUpdateTime = Time.realtimeSinceStartup;
            }
      }

      void UpdatePerformanceMetrics()
      {
            // FPS расчет
            float currentFPS = frameCount / updateInterval;
            frameCount = 0;

            UpdateFPSMetrics(currentFPS);
            UpdateInferenceMetrics();
            UpdateMemoryMetrics();
            UpdatePerformanceScore();
            UpdateUI();
      }

      void UpdateFPSMetrics(float currentFPS)
      {
            fpsHistory.Add(currentFPS);

            if (fpsHistory.Count > historySize)
            {
                  fpsHistory.RemoveAt(0);
            }
      }

      void UpdateInferenceMetrics()
      {
            // Закомментировано из-за рефакторинга в AsyncSegmentationManager
            /*
            if (segmentationManager != null)
            {
                  float inferenceTime = segmentationManager.AverageInferenceTime * 1000f; // в ms
                  inferenceHistory.Add(inferenceTime);

                  if (inferenceHistory.Count > historySize)
                  {
                        inferenceHistory.RemoveAt(0);
                  }
            }
            */
      }

      void UpdateMemoryMetrics()
      {
            // CPU память (GC)
            lastGCMemory = System.GC.GetTotalMemory(false);

            // GPU память (приблизительно)
            lastGPUMemory = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f);
      }

      void UpdatePerformanceScore()
      {
            float fpsScore = GetAverageFPS() / targetFPS * 100f;
            float inferenceScore = targetInferenceTime / GetAverageInferenceTime() * 100f;
            float memoryScore = CalculateMemoryScore();

            // Взвешенный средний балл
            currentPerformanceScore = (fpsScore * 0.4f + inferenceScore * 0.4f + memoryScore * 0.2f);
            currentPerformanceScore = Mathf.Clamp(currentPerformanceScore, 0f, 100f);
      }

      float CalculateMemoryScore()
      {
            // Простая оценка на основе использования памяти
            float maxAcceptableMemory = 512f; // MB
            float currentMemoryMB = lastGCMemory / (1024f * 1024f);

            return Mathf.Clamp((maxAcceptableMemory - currentMemoryMB) / maxAcceptableMemory * 100f, 0f, 100f);
      }

      void UpdateUI()
      {
            if (!enableUI) return;

            // FPS
            if (fpsText != null)
            {
                  float avgFPS = GetAverageFPS();
                  Color fpsColor = avgFPS >= targetFPS * 0.9f ? Color.green : (avgFPS >= targetFPS * 0.7f ? Color.yellow : Color.red);
                  fpsText.text = $"FPS: {avgFPS:F1}";
                  fpsText.color = fpsColor;
            }

            // Inference Time
            if (inferenceTimeText != null)
            {
                  float avgInference = GetAverageInferenceTime();
                  Color inferenceColor = avgInference <= targetInferenceTime * 1.1f ? Color.green : (avgInference <= targetInferenceTime * 1.5f ? Color.yellow : Color.red);
                  inferenceTimeText.text = $"ML: {avgInference:F1}ms";
                  inferenceTimeText.color = inferenceColor;
            }

            // Memory
            if (memoryText != null)
            {
                  float memoryMB = lastGCMemory / (1024f * 1024f);
                  memoryText.text = $"RAM: {memoryMB:F0}MB";
            }

            if (gpuMemoryText != null)
            {
                  gpuMemoryText.text = $"GPU: {lastGPUMemory:F0}MB";
            }

            // Performance Bar
            if (performanceSlider != null)
            {
                  performanceSlider.value = currentPerformanceScore / 100f;
            }

            if (performanceBar != null)
            {
                  Color barColor = currentPerformanceScore >= 80f ? Color.green :
                                 (currentPerformanceScore >= 60f ? Color.yellow : Color.red);
                  performanceBar.color = barColor;
            }
      }

      float GetAverageFPS()
      {
            if (fpsHistory.Count == 0) return 0f;

            float sum = 0f;
            foreach (float fps in fpsHistory)
            {
                  sum += fps;
            }
            return sum / fpsHistory.Count;
      }

      public float GetAverageInferenceTime()
      {
            if (inferenceHistory.Count == 0) return 0f;

            float sum = 0f;
            foreach (float time in inferenceHistory)
            {
                  sum += time;
            }
            return sum / inferenceHistory.Count;
      }

      void HideUI()
      {
            if (fpsText != null) fpsText.gameObject.SetActive(false);
            if (inferenceTimeText != null) inferenceTimeText.gameObject.SetActive(false);
            if (memoryText != null) memoryText.gameObject.SetActive(false);
            if (gpuMemoryText != null) gpuMemoryText.gameObject.SetActive(false);
            if (performanceSlider != null) performanceSlider.gameObject.SetActive(false);
            if (performanceBar != null) performanceBar.gameObject.SetActive(false);
      }

      /// <summary>
      /// Публичные методы для логирования производительности
      /// </summary>
      public void LogArgmaxOperation(System.Action operation)
      {
            argmaxMarker.Begin();
            operation();
            argmaxMarker.End();
      }

      public void LogInferenceOperation(System.Action operation)
      {
            inferenceMarker.Begin();
            operation();
            inferenceMarker.End();
      }

      public void LogPaintingOperation(System.Action operation)
      {
            paintingMarker.Begin();
            operation();
            paintingMarker.End();
      }

      /// <summary>
      /// Автоматическая настройка качества на основе производительности
      /// </summary>
      public void AutoAdjustQuality()
      {
            if (currentPerformanceScore < 60f)
            {
                  // Снижаем качество для улучшения производительности
                  if (segmentationManager != null)
                  {
                        Debug.Log("⚠️ Производительность низкая, снижаем качество...");
                        // Можно добавить логику изменения разрешения, интервалов и т.д.
                  }
            }
            else if (currentPerformanceScore > 90f)
            {
                  // Можем повысить качество
                  Debug.Log("✅ Производительность отличная, можно повысить качество");
            }
      }

      /// <summary>
      /// Экспорт метрик производительности
      /// </summary>
      public PerformanceReport GetPerformanceReport()
      {
            return new PerformanceReport
            {
                  averageFPS = GetAverageFPS(),
                  averageInferenceTime = GetAverageInferenceTime(),
                  memoryUsageMB = lastGCMemory / (1024f * 1024f),
                  gpuMemoryUsageMB = lastGPUMemory,
                  performanceScore = currentPerformanceScore,
                  timestamp = System.DateTime.Now
            };
      }

      /// <summary>
      /// Переключение видимости UI
      /// </summary>
      public void ToggleUI()
      {
            enableUI = !enableUI;

            if (enableUI)
            {
                  // Показываем UI элементы
                  if (fpsText != null) fpsText.gameObject.SetActive(true);
                  if (inferenceTimeText != null) inferenceTimeText.gameObject.SetActive(true);
                  if (memoryText != null) memoryText.gameObject.SetActive(true);
                  if (gpuMemoryText != null) gpuMemoryText.gameObject.SetActive(true);
                  if (performanceSlider != null) performanceSlider.gameObject.SetActive(true);
                  if (performanceBar != null) performanceBar.gameObject.SetActive(true);
            }
            else
            {
                  HideUI();
            }
      }

      void OnDestroy()
      {
            // ProfilerMarker не требует явного освобождения в Unity
            // Маркеры автоматически очищаются при завершении работы
      }
}

/// <summary>
/// Структура для экспорта отчетов о производительности
/// </summary>
[System.Serializable]
public struct PerformanceReport
{
      public float averageFPS;
      public float averageInferenceTime;
      public float memoryUsageMB;
      public float gpuMemoryUsageMB;
      public float performanceScore;
      public System.DateTime timestamp;

      public override string ToString()
      {
            return $"FPS: {averageFPS:F1}, Inference: {averageInferenceTime:F1}ms, RAM: {memoryUsageMB:F0}MB, Score: {performanceScore:F0}%";
      }
}
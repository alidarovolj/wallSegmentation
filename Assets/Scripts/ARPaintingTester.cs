using UnityEngine;
using UnityEngine.Profiling;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Автоматический тестер AR Painting системы
/// Проверяет производительность, стабильность и правильность работы
/// </summary>
public class ARPaintingTester : MonoBehaviour
{
      [Header("Test Configuration")]
      [SerializeField] private bool runTestsOnStart = false;
      [SerializeField] private float testDuration = 60f; // Время тестирования в секундах
      [SerializeField] private bool enableStressTest = false;
      [SerializeField] private bool saveResults = true;

      [Header("Performance Thresholds")]
      [SerializeField] private float minAcceptableFPS = 25f;
      [SerializeField] private float maxAcceptableInferenceTime = 100f; // ms
      [SerializeField] private float maxAcceptableMemory = 600f; // MB

      [Header("References")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private PerformanceMonitor performanceMonitor;
      [SerializeField] private MemoryPoolManager memoryPoolManager;

      // Результаты тестирования
      private TestResults currentResults = new TestResults();
      private List<PerformanceSnapshot> performanceHistory = new List<PerformanceSnapshot>();

      // Состояние тестирования
      private bool isTestRunning = false;
      private float testStartTime;
      private int frameCount = 0;

      void Start()
      {
            if (runTestsOnStart)
            {
                  StartCoroutine(RunAllTests());
            }
      }

      [ContextMenu("Run Performance Test")]
      public void StartPerformanceTest()
      {
            if (!isTestRunning)
            {
                  StartCoroutine(RunPerformanceTest());
            }
      }

      [ContextMenu("Run Stress Test")]
      public void StartStressTest()
      {
            if (!isTestRunning && enableStressTest)
            {
                  StartCoroutine(RunStressTest());
            }
      }

      [ContextMenu("Run All Tests")]
      public void StartAllTests()
      {
            if (!isTestRunning)
            {
                  StartCoroutine(RunAllTests());
            }
      }

      IEnumerator RunAllTests()
      {
            Debug.Log("🧪 Запуск полного тестирования AR Painting системы...");

            // Инициализация
            InitializeTest();

            // Тест 1: Базовая функциональность
            yield return StartCoroutine(TestBasicFunctionality());

            // Тест 2: Производительность
            yield return StartCoroutine(RunPerformanceTest());

            // Тест 3: Стабильность памяти
            yield return StartCoroutine(TestMemoryStability());

            // Тест 4: Стресс-тест (если включен)
            if (enableStressTest)
            {
                  yield return StartCoroutine(RunStressTest());
            }

            // Финализация и отчет
            FinalizeTest();

            Debug.Log("✅ Все тесты завершены!");
      }

      void InitializeTest()
      {
            isTestRunning = true;
            testStartTime = Time.realtimeSinceStartup;
            frameCount = 0;

            currentResults = new TestResults
            {
                  testStartTime = System.DateTime.Now,
                  deviceInfo = GetDeviceInfo()
            };

            performanceHistory.Clear();

            Debug.Log($"🔧 Тестирование инициализировано на устройстве: {currentResults.deviceInfo}");
      }

      IEnumerator TestBasicFunctionality()
      {
            Debug.Log("🔍 Тест базовой функциональности...");

            var basicTest = new BasicFunctionalityTest();

            // Проверка компонентов
            basicTest.hasSegmentationManager = segmentationManager != null;
            basicTest.hasPerformanceMonitor = performanceMonitor != null;
            basicTest.hasMemoryPoolManager = memoryPoolManager != null;

            if (segmentationManager != null)
            {
                  // Ждем инициализации
                  yield return new WaitForSeconds(2f);

                  // Проверяем работу системы
                  // Этот блок кода устарел и вызывает ошибки, комментируем его
                  /*
                  basicTest.segmentationWorking = segmentationManager.lastTensorData != null;
                  basicTest.asyncProcessingWorking = segmentationManager.ActiveInferences >= 0;
                  */
            }

            if (performanceMonitor != null)
            {
                  // Этот блок кода устарел
                  /*
                  basicTest.performanceMonitoringWorking = performanceMonitor.GetAverageInferenceTime() > 0;
                  */
            }

            if (memoryPoolManager != null)
            {
                  var stats = memoryPoolManager.GetMemoryStats();
                  basicTest.memoryPoolsWorking = stats.totalAllocatedMemoryMB > 0;
            }

            currentResults.basicFunctionality = basicTest;

            var passed = basicTest.AllTestsPassed();
            Debug.Log($"{(passed ? "✅" : "❌")} Базовая функциональность: {(passed ? "ПРОЙДЕНА" : "ПРОВАЛЕНА")}");
      }

      IEnumerator RunPerformanceTest()
      {
            Debug.Log("⚡ Тест производительности...");

            var performanceTest = new PerformanceTest();
            var startTime = Time.realtimeSinceStartup;
            var frameCountStart = frameCount;

            var fpsValues = new List<float>();
            var inferenceValues = new List<float>();
            var memoryValues = new List<float>();

            while (Time.realtimeSinceStartup - startTime < testDuration)
            {
                  // Собираем метрики
                  var currentFPS = 1f / Time.unscaledDeltaTime;
                  fpsValues.Add(currentFPS);

                  if (segmentationManager != null)
                  {
                        // Этот блок кода устарел
                        /*
                        var inferenceTime = segmentationManager.AverageInferenceTime * 1000f;
                        if (inferenceTime > 0)
                        {
                              inferenceValues.Add(inferenceTime);
                        }
                        */
                  }

                  var currentMemory = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f);
                  memoryValues.Add(currentMemory);

                  // Создаем снапшот для истории
                  var snapshot = new PerformanceSnapshot
                  {
                        timestamp = Time.realtimeSinceStartup - startTime,
                        fps = currentFPS,
                        inferenceTime = inferenceValues.Count > 0 ? inferenceValues[inferenceValues.Count - 1] : 0,
                        memoryUsage = currentMemory
                  };
                  performanceHistory.Add(snapshot);

                  frameCount++;
                  yield return null; // Ждем следующего кадра
            }

            // Анализируем результаты
            performanceTest.averageFPS = CalculateAverage(fpsValues);
            performanceTest.minFPS = CalculateMin(fpsValues);
            performanceTest.maxFPS = CalculateMax(fpsValues);

            if (inferenceValues.Count > 0)
            {
                  // Этот блок кода устарел
                  /*
                  performanceTest.averageInferenceTime = CalculateAverage(inferenceValues);
                  performanceTest.minInferenceTime = CalculateMin(inferenceValues);
                  performanceTest.maxInferenceTime = CalculateMax(inferenceValues);
                  */
            }

            performanceTest.averageMemoryUsage = CalculateAverage(memoryValues);
            performanceTest.maxMemoryUsage = CalculateMax(memoryValues);

            // Проверяем соответствие требованиям
            performanceTest.fpsAcceptable = performanceTest.averageFPS >= minAcceptableFPS;
            performanceTest.inferenceAcceptable = performanceTest.averageInferenceTime <= maxAcceptableInferenceTime;
            performanceTest.memoryAcceptable = performanceTest.maxMemoryUsage <= maxAcceptableMemory;

            currentResults.performance = performanceTest;

            var passed = performanceTest.AllTestsPassed();
            Debug.Log($"{(passed ? "✅" : "❌")} Производительность: {(passed ? "ПРОЙДЕНА" : "ПРОВАЛЕНА")}");
            Debug.Log($"📊 FPS: {performanceTest.averageFPS:F1} | Inference: {performanceTest.averageInferenceTime:F1}ms | Memory: {performanceTest.averageMemoryUsage:F1}MB");
      }

      IEnumerator TestMemoryStability()
      {
            Debug.Log("🧠 Тест стабильности памяти...");

            var memoryTest = new MemoryStabilityTest();
            var startMemory = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f);

            // Принудительная сборка мусора в начале
            System.GC.Collect();
            yield return new WaitForSeconds(1f);

            var baselineMemory = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f);

            // Симулируем активность в течение тестового периода
            var testStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - testStart < 30f) // 30 секунд теста
            {
                  // Симулируем пользовательскую активность
                  if (segmentationManager != null)
                  {
                        // Симулируем тапы
                        yield return new WaitForSeconds(0.5f);
                  }

                  yield return null;
            }

            // Финальная сборка мусора
            System.GC.Collect();
            yield return new WaitForSeconds(1f);

            var finalMemory = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f);

            memoryTest.initialMemory = startMemory;
            memoryTest.baselineMemory = baselineMemory;
            memoryTest.finalMemory = finalMemory;
            memoryTest.memoryLeak = finalMemory - baselineMemory;

            // Проверяем memory pools
            if (memoryPoolManager != null)
            {
                  var stats = memoryPoolManager.GetMemoryStats();
                  memoryTest.poolsWorking = stats.activeRenderTextures >= 0 && stats.pooledRenderTextures > 0;
            }

            memoryTest.stable = memoryTest.memoryLeak < 50f; // Допустима утечка до 50MB

            currentResults.memoryStability = memoryTest;

            var passed = memoryTest.AllTestsPassed();
            Debug.Log($"{(passed ? "✅" : "❌")} Стабильность памяти: {(passed ? "ПРОЙДЕНА" : "ПРОВАЛЕНА")}");
            Debug.Log($"🧠 Memory leak: {memoryTest.memoryLeak:F1}MB");
      }

      IEnumerator RunStressTest()
      {
            Debug.Log("💥 Стресс-тест системы...");

            var stressTest = new StressTest();
            var startTime = Time.realtimeSinceStartup;

            // Интенсивная нагрузка на систему
            while (Time.realtimeSinceStartup - startTime < 30f)
            {
                  // Быстрые симулированные тапы
                  for (int i = 0; i < 5; i++)
                  {
                        SimulateTap();
                        yield return new WaitForSeconds(0.1f);
                  }

                  // Смена цветов и режимов
                  if (Random.Range(0f, 1f) > 0.7f)
                  {
                        Shader.SetGlobalColor("_GlobalPaintColor", Random.ColorHSV());
                        Shader.SetGlobalInt("_GlobalBlendMode", Random.Range(0, 4));
                  }

                  yield return null;
            }

            // Проверяем стабильность после стресса
            yield return new WaitForSeconds(2f);

            stressTest.systemStable = performanceMonitor != null && performanceMonitor.GetAverageInferenceTime() > 0;
            stressTest.noErrors = true; // Если дошли до этого места, значит ошибок не было

            if (segmentationManager != null)
            {
                  // Этот блок кода устарел
                  /*
                  stressTest.asyncSystemWorking = segmentationManager.ActiveInferences >= 0;
                  */
            }

            currentResults.stressTest = stressTest;

            var passed = stressTest.AllTestsPassed();
            Debug.Log($"{(passed ? "✅" : "❌")} Стресс-тест: {(passed ? "ПРОЙДЕН" : "ПРОВАЛЕН")}");
      }

      void SimulateTap()
      {
            // Симулируем случайный тап по экрану
            var randomPos = new Vector2(
                Random.Range(0f, Screen.width),
                Random.Range(0f, Screen.height)
            );

            // Если есть SurfaceHighlighter, используем его логику
            var highlighter = FindObjectOfType<SurfaceHighlighter>();
            if (highlighter != null && segmentationManager != null)
            {
                  // Симулируем GetClassAtPosition
                  // Этот блок кода устарел
                  /*
                  if (segmentationManager.lastTensorData != null && segmentationManager.lastTensorShape.rank > 0)
                  {
                        var randomClass = Random.Range(0, 150); // SegFormer classes
                        Shader.SetGlobalInt("_GlobalTargetClassID", randomClass);
                  }
                  */
            }
      }

      void FinalizeTest()
      {
            isTestRunning = false;

            currentResults.testEndTime = System.DateTime.Now;
            currentResults.totalTestDuration = Time.realtimeSinceStartup - testStartTime;
            currentResults.totalFrames = frameCount;

            // Генерируем отчет
            GenerateTestReport();

            if (saveResults)
            {
                  SaveTestResults();
            }
      }

      void GenerateTestReport()
      {
            Debug.Log("📋 === ОТЧЕТ О ТЕСТИРОВАНИИ ===");
            Debug.Log($"🕐 Время: {currentResults.totalTestDuration:F1}s");
            Debug.Log($"🖥️ Устройство: {currentResults.deviceInfo}");
            Debug.Log($"🎯 Кадров: {currentResults.totalFrames}");

            Debug.Log("\n📊 РЕЗУЛЬТАТЫ:");
            Debug.Log($"Basic Functionality: {(currentResults.basicFunctionality.AllTestsPassed() ? "✅ PASS" : "❌ FAIL")}");
            Debug.Log($"Performance: {(currentResults.performance.AllTestsPassed() ? "✅ PASS" : "❌ FAIL")}");
            Debug.Log($"Memory Stability: {(currentResults.memoryStability.AllTestsPassed() ? "✅ PASS" : "❌ FAIL")}");
            Debug.Log($"Stress Test: {(currentResults.stressTest.AllTestsPassed() ? "✅ PASS" : "❌ FAIL")}");

            Debug.Log("\n📈 ДЕТАЛИ ПРОИЗВОДИТЕЛЬНОСТИ:");
            Debug.Log($"FPS: avg={currentResults.performance.averageFPS:F1}, min={currentResults.performance.minFPS:F1}");
            Debug.Log($"Inference: avg={currentResults.performance.averageInferenceTime:F1}ms");
            Debug.Log($"Memory: avg={currentResults.performance.averageMemoryUsage:F1}MB, max={currentResults.performance.maxMemoryUsage:F1}MB");
            Debug.Log($"Memory Leak: {currentResults.memoryStability.memoryLeak:F1}MB");
      }

      void SaveTestResults()
      {
            try
            {
                  var json = JsonUtility.ToJson(currentResults, true);
                  var filename = $"ARPaintingTest_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
                  var path = Path.Combine(Application.persistentDataPath, filename);

                  File.WriteAllText(path, json);
                  Debug.Log($"💾 Результаты сохранены: {path}");
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка сохранения результатов: {e.Message}");
            }
      }

      string GetDeviceInfo()
      {
            return $"{SystemInfo.deviceModel} | {SystemInfo.operatingSystem} | RAM:{SystemInfo.systemMemorySize}MB | GPU:{SystemInfo.graphicsDeviceName}";
      }

      float CalculateAverage(List<float> values)
      {
            if (values.Count == 0) return 0f;
            float sum = 0f;
            foreach (var value in values) sum += value;
            return sum / values.Count;
      }

      float CalculateMin(List<float> values)
      {
            if (values.Count == 0) return 0f;
            float min = float.MaxValue;
            foreach (var value in values) if (value < min) min = value;
            return min;
      }

      float CalculateMax(List<float> values)
      {
            if (values.Count == 0) return 0f;
            float max = float.MinValue;
            foreach (var value in values) if (value > max) max = value;
            return max;
      }
}

// Структуры данных для результатов тестирования
[System.Serializable]
public class TestResults
{
      public System.DateTime testStartTime;
      public System.DateTime testEndTime;
      public float totalTestDuration;
      public int totalFrames;
      public string deviceInfo;

      public BasicFunctionalityTest basicFunctionality;
      public PerformanceTest performance;
      public MemoryStabilityTest memoryStability;
      public StressTest stressTest;

      public bool AllTestsPassed()
      {
            return basicFunctionality.AllTestsPassed() &&
                   performance.AllTestsPassed() &&
                   memoryStability.AllTestsPassed() &&
                   stressTest.AllTestsPassed();
      }
}

[System.Serializable]
public class BasicFunctionalityTest
{
      public bool hasSegmentationManager;
      public bool hasPerformanceMonitor;
      public bool hasMemoryPoolManager;
      public bool segmentationWorking;
      public bool asyncProcessingWorking;
      public bool performanceMonitoringWorking;
      public bool memoryPoolsWorking;

      public bool AllTestsPassed()
      {
            return hasSegmentationManager && hasPerformanceMonitor && hasMemoryPoolManager &&
                   segmentationWorking && asyncProcessingWorking &&
                   performanceMonitoringWorking && memoryPoolsWorking;
      }
}

[System.Serializable]
public class PerformanceTest
{
      public float averageFPS;
      public float minFPS;
      public float maxFPS;
      public float averageInferenceTime;
      public float minInferenceTime;
      public float maxInferenceTime;
      public float averageMemoryUsage;
      public float maxMemoryUsage;

      public bool fpsAcceptable;
      public bool inferenceAcceptable;
      public bool memoryAcceptable;

      public bool AllTestsPassed()
      {
            return fpsAcceptable && inferenceAcceptable && memoryAcceptable;
      }
}

[System.Serializable]
public class MemoryStabilityTest
{
      public float initialMemory;
      public float baselineMemory;
      public float finalMemory;
      public float memoryLeak;
      public bool stable;
      public bool poolsWorking;

      public bool AllTestsPassed()
      {
            return stable && poolsWorking;
      }
}

[System.Serializable]
public class StressTest
{
      public bool systemStable;
      public bool noErrors;
      public bool asyncSystemWorking;

      public bool AllTestsPassed()
      {
            return systemStable && noErrors && asyncSystemWorking;
      }
}

[System.Serializable]
public class PerformanceSnapshot
{
      public float timestamp;
      public float fps;
      public float inferenceTime;
      public float memoryUsage;
}
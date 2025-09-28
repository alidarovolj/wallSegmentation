using UnityEngine;

/// <summary>
/// Простой тестер для проверки работы Dulux Visualizer системы
/// Добавьте этот компонент на любой GameObject для автоматического тестирования
/// </summary>
public class DuluxTester : MonoBehaviour
{
    [Header("🧪 Test Settings")]
    [Tooltip("Автоматически запустить тесты при старте")]
    [SerializeField] private bool autoTestOnStart = true;
    
    [Tooltip("Задержка перед началом тестов (сек)")]
    [SerializeField] private float testDelay = 3f;
    
    [Tooltip("Показать подробные логи тестирования")]
    [SerializeField] private bool showDetailedLogs = true;

    [Header("📊 Test Results")]
    [SerializeField] private bool systemFound = false;
    [SerializeField] private bool systemReady = false;
    [SerializeField] private bool allTestsPassed = false;
    [SerializeField] private string testStatus = "Ожидание...";

    private DuluxVisualizerIntegration dulux;
    private int testsPassed = 0;
    private int totalTests = 6;

    void Start()
    {
        if (autoTestOnStart)
        {
            Invoke(nameof(StartTesting), testDelay);
        }
    }

    /// <summary>
    /// Запуск полного тестирования системы
    /// </summary>
    [ContextMenu("Start Full Test")]
    public void StartTesting()
    {
        Log("🧪 Начинаем полное тестирование Dulux Visualizer...");
        testStatus = "Тестирование...";
        testsPassed = 0;
        
        // Тест 1: Поиск системы
        TestSystemExists();
        
        if (systemFound)
        {
            // Тест 2: Проверка готовности
            TestSystemReady();
            
            if (systemReady)
            {
                // Остальные тесты
                TestColorChange();
                TestFinishChange();
                TestRoomAnalysis();
                TestPaintingControl();
                
                // Финальная оценка
                EvaluateResults();
            }
        }
    }

    /// <summary>
    /// Тест 1: Проверка существования системы
    /// </summary>
    private void TestSystemExists()
    {
        Log("🔍 Тест 1: Поиск DuluxVisualizerIntegration...");
        
        dulux = FindObjectOfType<DuluxVisualizerIntegration>();
        
        if (dulux != null)
        {
            systemFound = true;
            testsPassed++;
            Log("✅ Тест 1 ПРОЙДЕН: DuluxVisualizerIntegration найден");
        }
        else
        {
            systemFound = false;
            testStatus = "Ошибка: система не найдена";
            Log("❌ Тест 1 ПРОВАЛЕН: DuluxVisualizerIntegration не найден!");
            Log("💡 Решение: Добавьте компонент DuluxVisualizerSetup и запустите Setup");
        }
    }

    /// <summary>
    /// Тест 2: Проверка готовности системы
    /// </summary>
    private void TestSystemReady()
    {
        Log("⏳ Тест 2: Проверка готовности системы...");
        
        if (dulux.IsSystemReady)
        {
            systemReady = true;
            testsPassed++;
            Log("✅ Тест 2 ПРОЙДЕН: Система готова к работе");
        }
        else
        {
            systemReady = false;
            testStatus = "Ошибка: система не готова";
            Log("❌ Тест 2 ПРОВАЛЕН: Система еще не готова");
            Log("💡 Решение: Подождите 5-10 секунд для инициализации");
            
            // Повторяем тест через 2 секунды
            Invoke(nameof(RetryReadinessTest), 2f);
        }
    }

    /// <summary>
    /// Повторная проверка готовности
    /// </summary>
    private void RetryReadinessTest()
    {
        if (dulux != null && dulux.IsSystemReady)
        {
            systemReady = true;
            testsPassed++;
            Log("✅ Тест 2 ПРОЙДЕН (повторно): Система готова к работе");
            
            // Продолжаем остальные тесты
            TestColorChange();
            TestFinishChange();
            TestRoomAnalysis();
            TestPaintingControl();
            EvaluateResults();
        }
        else
        {
            Log("❌ Система все еще не готова. Проверьте консоль на ошибки.");
            testStatus = "Ошибка: таймаут инициализации";
        }
    }

    /// <summary>
    /// Тест 3: Смена цвета краски
    /// </summary>
    private void TestColorChange()
    {
        Log("🎨 Тест 3: Смена цвета краски...");
        
        try
        {
            Color originalColor = dulux.CurrentPaintColor;
            Color testColor = Color.red;
            
            dulux.SetPaintColor(testColor);
            
            if (ColorApproximatelyEqual(dulux.CurrentPaintColor, testColor))
            {
                testsPassed++;
                Log($"✅ Тест 3 ПРОЙДЕН: Цвет изменен на {ColorUtility.ToHtmlStringRGB(testColor)}");
                
                // Возвращаем исходный цвет
                dulux.SetPaintColor(originalColor);
            }
            else
            {
                Log("❌ Тест 3 ПРОВАЛЕН: Цвет не изменился");
            }
        }
        catch (System.Exception e)
        {
            Log($"❌ Тест 3 ПРОВАЛЕН: Ошибка - {e.Message}");
        }
    }

    /// <summary>
    /// Тест 4: Смена типа финиша
    /// </summary>
    private void TestFinishChange()
    {
        Log("✨ Тест 4: Смена типа финиша...");
        
        try
        {
            PaintFinishType originalFinish = dulux.CurrentFinish;
            PaintFinishType testFinish = PaintFinishType.Gloss;
            
            dulux.SetPaintFinish(testFinish);
            
            if (dulux.CurrentFinish == testFinish)
            {
                testsPassed++;
                Log($"✅ Тест 4 ПРОЙДЕН: Финиш изменен на {testFinish}");
                
                // Возвращаем исходный финиш
                dulux.SetPaintFinish(originalFinish);
            }
            else
            {
                Log("❌ Тест 4 ПРОВАЛЕН: Финиш не изменился");
            }
        }
        catch (System.Exception e)
        {
            Log($"❌ Тест 4 ПРОВАЛЕН: Ошибка - {e.Message}");
        }
    }

    /// <summary>
    /// Тест 5: Анализ помещения
    /// </summary>
    private void TestRoomAnalysis()
    {
        Log("🏠 Тест 5: Анализ помещения...");
        
        try
        {
            dulux.AnalyzeRoom();
            testsPassed++;
            Log("✅ Тест 5 ПРОЙДЕН: Анализ помещения запущен");
        }
        catch (System.Exception e)
        {
            Log($"❌ Тест 5 ПРОВАЛЕН: Ошибка - {e.Message}");
        }
    }

    /// <summary>
    /// Тест 6: Управление покраской
    /// </summary>
    private void TestPaintingControl()
    {
        Log("🖌️ Тест 6: Управление покраской...");
        
        try
        {
            // Начинаем покраску
            dulux.StartPainting();
            
            if (dulux.IsPainting)
            {
                Log("✅ Покраска начата успешно");
                
                // Останавливаем покраску через 1 секунду
                Invoke(nameof(StopPaintingTest), 1f);
            }
            else
            {
                Log("❌ Тест 6 ПРОВАЛЕН: Покраска не началась");
            }
        }
        catch (System.Exception e)
        {
            Log($"❌ Тест 6 ПРОВАЛЕН: Ошибка - {e.Message}");
        }
    }

    /// <summary>
    /// Завершение теста покраски
    /// </summary>
    private void StopPaintingTest()
    {
        try
        {
            dulux.StopPainting();
            
            if (!dulux.IsPainting)
            {
                testsPassed++;
                Log("✅ Тест 6 ПРОЙДЕН: Покраска остановлена успешно");
            }
            else
            {
                Log("❌ Тест 6 ПРОВАЛЕН: Покраска не остановилась");
            }
        }
        catch (System.Exception e)
        {
            Log($"❌ Тест 6 ПРОВАЛЕН: Ошибка остановки - {e.Message}");
        }
        
        // Финальная оценка
        EvaluateResults();
    }

    /// <summary>
    /// Оценка результатов тестирования
    /// </summary>
    private void EvaluateResults()
    {
        float successRate = (float)testsPassed / totalTests * 100f;
        
        Log("📊 РЕЗУЛЬТАТЫ ТЕСТИРОВАНИЯ:");
        Log($"   Пройдено тестов: {testsPassed}/{totalTests}");
        Log($"   Процент успеха: {successRate:F1}%");
        
        if (testsPassed == totalTests)
        {
            allTestsPassed = true;
            testStatus = "Все тесты пройдены ✅";
            Log("🎉 ВСЕ ТЕСТЫ ПРОЙДЕНЫ УСПЕШНО!");
            Log("🚀 Dulux Visualizer система полностью функциональна!");
        }
        else if (testsPassed >= totalTests * 0.8f)
        {
            testStatus = "Большинство тестов пройдено ⚠️";
            Log("⚠️ Большинство тестов пройдено, но есть проблемы");
            Log("💡 Проверьте консоль на предупреждения");
        }
        else
        {
            allTestsPassed = false;
            testStatus = "Критические ошибки ❌";
            Log("❌ КРИТИЧЕСКИЕ ОШИБКИ В СИСТЕМЕ!");
            Log("🔧 Требуется диагностика и исправление");
        }
        
        Log("=====================================");
    }

    /// <summary>
    /// Быстрый тест основных функций
    /// </summary>
    [ContextMenu("Quick Test")]
    public void QuickTest()
    {
        Log("⚡ Быстрый тест основных функций...");
        
        dulux = FindObjectOfType<DuluxVisualizerIntegration>();
        
        if (dulux == null)
        {
            Log("❌ DuluxVisualizerIntegration не найден!");
            return;
        }
        
        if (!dulux.IsSystemReady)
        {
            Log("⏳ Система еще не готова. Подождите...");
            return;
        }
        
        // Быстрые тесты
        dulux.SetPaintColor(Color.blue);
        dulux.SetPaintFinish(PaintFinishType.Satin);
        
        Log("✅ Быстрый тест завершен успешно!");
    }

    /// <summary>
    /// Сравнение цветов с погрешностью
    /// </summary>
    private bool ColorApproximatelyEqual(Color a, Color b, float threshold = 0.01f)
    {
        return Mathf.Abs(a.r - b.r) < threshold &&
               Mathf.Abs(a.g - b.g) < threshold &&
               Mathf.Abs(a.b - b.b) < threshold;
    }

    /// <summary>
    /// Логирование с проверкой настроек
    /// </summary>
    private void Log(string message)
    {
        if (showDetailedLogs)
        {
            Debug.Log($"[DuluxTester] {message}");
        }
    }
}
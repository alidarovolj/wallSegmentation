using UnityEngine;
using System.Collections;

/// <summary>
/// Тестер многоцветной покраски - демонстрирует возможности системы
/// </summary>
public class MultiColorTester : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool runTestOnStart = true;
    [SerializeField] private float testDelay = 2f;
    
    private MultiColorPaintManager paintManager;
    private bool isTestRunning = false;
    
    void Start()
    {
        if (runTestOnStart)
        {
            StartCoroutine(WaitAndStartTest());
        }
    }
    
    IEnumerator WaitAndStartTest()
    {
        // Подождать пока система инициализируется
        yield return new WaitForSeconds(3f);
        
        paintManager = FindObjectOfType<MultiColorPaintManager>();
        if (paintManager == null)
        {
            Debug.LogError("[MultiColorTester] ❌ MultiColorPaintManager не найден!");
            yield break;
        }
        
        StartCoroutine(RunColorTests());
    }
    
    IEnumerator RunColorTests()
    {
        if (isTestRunning) yield break;
        isTestRunning = true;
        
        Debug.Log("[MultiColorTester] 🧪 Начинаем демонстрацию многоцветной покраски...");
        
        // Тест 1: Классическая покраска
        yield return StartCoroutine(TestClassicColors());
        
        // Тест 2: Яркие цвета
        yield return StartCoroutine(TestBrightColors());
        
        // Тест 3: Тематическая покраска
        yield return StartCoroutine(TestThematicColors());
        
        // Тест 4: Многоцветный режим
        yield return StartCoroutine(TestMultiColorMode());
        
        // Тест 5: Прозрачность
        yield return StartCoroutine(TestOpacity());
        
        Debug.Log("[MultiColorTester] ✅ Демонстрация завершена!");
        isTestRunning = false;
    }
    
    IEnumerator TestClassicColors()
    {
        Debug.Log("[MultiColorTester] 🏠 Тест 1: Классическая покраска");
        
        // Белые стены
        paintManager.PaintClass(0, Color.white);
        Debug.Log("   🧱 Стены покрашены в белый");
        yield return new WaitForSeconds(testDelay);
        
        // Коричневый пол
        paintManager.PaintClass(3, new Color(0.6f, 0.4f, 0.2f));
        Debug.Log("   🟫 Пол покрашен в коричневый");
        yield return new WaitForSeconds(testDelay);
        
        // Кремовый потолок
        paintManager.PaintClass(5, new Color(0.95f, 0.95f, 0.9f));
        Debug.Log("   ⬜ Потолок покрашен в кремовый");
        yield return new WaitForSeconds(testDelay);
    }
    
    IEnumerator TestBrightColors()
    {
        Debug.Log("[MultiColorTester] 🌈 Тест 2: Яркие цвета");
        
        // Синие стены
        paintManager.PaintClass(0, Color.blue);
        Debug.Log("   🔵 Стены покрашены в синий");
        yield return new WaitForSeconds(testDelay);
        
        // Зеленый пол
        paintManager.PaintClass(3, Color.green);
        Debug.Log("   🟢 Пол покрашен в зеленый");
        yield return new WaitForSeconds(testDelay);
        
        // Желтый потолок
        paintManager.PaintClass(5, Color.yellow);
        Debug.Log("   🟡 Потолок покрашен в желтый");
        yield return new WaitForSeconds(testDelay);
    }
    
    IEnumerator TestThematicColors()
    {
        Debug.Log("[MultiColorTester] 🎨 Тест 3: Тематическая покраска (детская комната)");
        
        // Розовые стены
        paintManager.PaintClass(0, Color.magenta);
        Debug.Log("   🩷 Стены покрашены в розовый");
        yield return new WaitForSeconds(testDelay);
        
        // Красный стол
        paintManager.PaintClass(15, Color.red);
        Debug.Log("   🔴 Стол покрашен в красный");
        yield return new WaitForSeconds(testDelay);
        
        // Синие стулья
        paintManager.PaintClass(19, Color.cyan);
        Debug.Log("   🔵 Стулья покрашены в голубой");
        yield return new WaitForSeconds(testDelay);
        
        // Зеленый диван
        paintManager.PaintClass(23, Color.green);
        Debug.Log("   🟢 Диван покрашен в зеленый");
        yield return new WaitForSeconds(testDelay);
    }
    
    IEnumerator TestMultiColorMode()
    {
        Debug.Log("[MultiColorTester] 🌈 Тест 4: Многоцветный режим");
        
        paintManager.EnableMultiColorMode();
        Debug.Log("   ✨ Включен многоцветный режим - каждый класс своим цветом");
        yield return new WaitForSeconds(testDelay * 2);
    }
    
    IEnumerator TestOpacity()
    {
        Debug.Log("[MultiColorTester] 🔆 Тест 5: Изменение прозрачности");
        
        // Полная непрозрачность
        paintManager.SetOpacity(1.0f);
        Debug.Log("   🔆 Прозрачность: 100%");
        yield return new WaitForSeconds(testDelay);
        
        // Средняя прозрачность
        paintManager.SetOpacity(0.5f);
        Debug.Log("   🔅 Прозрачность: 50%");
        yield return new WaitForSeconds(testDelay);
        
        // Слабая прозрачность
        paintManager.SetOpacity(0.2f);
        Debug.Log("   🔅 Прозрачность: 20%");
        yield return new WaitForSeconds(testDelay);
        
        // Вернуть к нормальной
        paintManager.SetOpacity(0.7f);
        Debug.Log("   🔆 Прозрачность: 70% (по умолчанию)");
        yield return new WaitForSeconds(testDelay);
    }
    
    /// <summary>
    /// Запустить тест вручную
    /// </summary>
    [ContextMenu("Run Color Tests")]
    public void RunTests()
    {
        if (!isTestRunning)
        {
            StartCoroutine(RunColorTests());
        }
    }
    
    /// <summary>
    /// Быстрый тест - покрасить стены в случайный цвет
    /// </summary>
    [ContextMenu("Quick Test - Random Wall Color")]
    public void QuickTestRandomWallColor()
    {
        if (paintManager == null)
            paintManager = FindObjectOfType<MultiColorPaintManager>();
            
        if (paintManager != null)
        {
            Color randomColor = new Color(
                Random.Range(0f, 1f),
                Random.Range(0f, 1f),
                Random.Range(0f, 1f)
            );
            
            paintManager.PaintClass(0, randomColor);
            Debug.Log($"[MultiColorTester] 🎲 Стены покрашены в случайный цвет: {ColorUtility.ToHtmlStringRGB(randomColor)}");
        }
    }
    
    /// <summary>
    /// Сбросить к белым стенам
    /// </summary>
    [ContextMenu("Reset to White Walls")]
    public void ResetToWhiteWalls()
    {
        if (paintManager == null)
            paintManager = FindObjectOfType<MultiColorPaintManager>();
            
        if (paintManager != null)
        {
            paintManager.PaintWallsOnly(Color.white);
            Debug.Log("[MultiColorTester] 🤍 Стены сброшены к белому цвету");
        }
    }
    
    /// <summary>
    /// Демо для презентации
    /// </summary>
    [ContextMenu("Demo Presentation")]
    public void DemoPresentation()
    {
        StartCoroutine(RunDemoPresentation());
    }
    
    IEnumerator RunDemoPresentation()
    {
        Debug.Log("[MultiColorTester] 🎬 Начинаем демо-презентацию...");
        
        if (paintManager == null)
            paintManager = FindObjectOfType<MultiColorPaintManager>();
            
        if (paintManager == null)
        {
            Debug.LogError("[MultiColorTester] ❌ MultiColorPaintManager не найден!");
            yield break;
        }
        
        // Слайд 1: Только стены
        Debug.Log("🎬 Слайд 1: Покраска только стен");
        paintManager.PaintWallsOnly(new Color(0.9f, 0.9f, 0.95f)); // Светло-голубой
        yield return new WaitForSeconds(3f);
        
        // Слайд 2: Добавляем пол
        Debug.Log("🎬 Слайд 2: Добавляем пол");
        paintManager.PaintClass(3, new Color(0.7f, 0.5f, 0.3f)); // Паркет
        yield return new WaitForSeconds(3f);
        
        // Слайд 3: Добавляем мебель
        Debug.Log("🎬 Слайд 3: Добавляем мебель");
        paintManager.PaintClass(15, new Color(0.4f, 0.2f, 0.1f)); // Темный стол
        paintManager.PaintClass(19, new Color(0.2f, 0.4f, 0.8f)); // Синие стулья
        yield return new WaitForSeconds(3f);
        
        // Слайд 4: Полная картина
        Debug.Log("🎬 Слайд 4: Включаем все цвета");
        paintManager.EnableMultiColorMode();
        yield return new WaitForSeconds(3f);
        
        Debug.Log("🎬 Демо-презентация завершена!");
    }
}
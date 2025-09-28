using UnityEngine;
using System.Collections;

/// <summary>
/// Быстрый тест цветов для проверки многоцветной покраски
/// </summary>
public class QuickColorTest : MonoBehaviour
{
    [Header("Тест цветов")]
    [SerializeField] private bool autoTest = true;
    [SerializeField] private float testInterval = 2f;
    
    private AsyncSegmentationManager segmentationManager;
    
    void Start()
    {
        if (autoTest)
        {
            StartCoroutine(RunColorTest());
        }
    }
    
    IEnumerator RunColorTest()
    {
        // Подождать пока система инициализируется
        yield return new WaitForSeconds(3f);
        
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager == null)
        {
            Debug.LogError("[QuickColorTest] ❌ AsyncSegmentationManager не найден!");
            yield break;
        }
        
        Debug.Log("[QuickColorTest] 🎨 Начинаем тест цветов...");
        
        // Тест 1: Красные стены
        Debug.Log("[QuickColorTest] 🔴 Тест 1: Красные стены");
        segmentationManager.SetSelectedClass(0);
        segmentationManager.SetPaintColor(Color.red);
        yield return new WaitForSeconds(testInterval);
        
        // Тест 2: Синие стены
        Debug.Log("[QuickColorTest] 🔵 Тест 2: Синие стены");
        segmentationManager.SetSelectedClass(0);
        segmentationManager.SetPaintColor(Color.blue);
        yield return new WaitForSeconds(testInterval);
        
        // Тест 3: Зеленые стены
        Debug.Log("[QuickColorTest] 🟢 Тест 3: Зеленые стены");
        segmentationManager.SetSelectedClass(0);
        segmentationManager.SetPaintColor(Color.green);
        yield return new WaitForSeconds(testInterval);
        
        // Тест 4: Коричневый пол
        Debug.Log("[QuickColorTest] 🟫 Тест 4: Коричневый пол");
        segmentationManager.SetSelectedClass(3);
        segmentationManager.SetPaintColor(new Color(0.6f, 0.4f, 0.2f));
        yield return new WaitForSeconds(testInterval);
        
        // Тест 5: Многоцветный режим
        Debug.Log("[QuickColorTest] 🌈 Тест 5: Многоцветный режим");
        segmentationManager.ShowAllClassesColored();
        yield return new WaitForSeconds(testInterval);
        
        Debug.Log("[QuickColorTest] ✅ Тест цветов завершен!");
    }
    
    /// <summary>
    /// Ручной тест через контекстное меню
    /// </summary>
    [ContextMenu("Test Colors")]
    public void ManualColorTest()
    {
        StartCoroutine(RunColorTest());
    }
    
    /// <summary>
    /// Быстрый тест одного цвета
    /// </summary>
    [ContextMenu("Quick Red Walls")]
    public void QuickRedWalls()
    {
        if (segmentationManager == null)
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(0);
            segmentationManager.SetPaintColor(Color.red);
            Debug.Log("[QuickColorTest] 🔴 Стены покрашены в красный!");
        }
    }
    
    /// <summary>
    /// Включить многоцветный режим
    /// </summary>
    [ContextMenu("Enable Multi-Color")]
    public void EnableMultiColor()
    {
        if (segmentationManager == null)
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            
        if (segmentationManager != null)
        {
            segmentationManager.ShowAllClassesColored();
            Debug.Log("[QuickColorTest] 🌈 Многоцветный режим включен!");
        }
    }
}
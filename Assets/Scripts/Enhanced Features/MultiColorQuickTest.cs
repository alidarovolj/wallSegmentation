using UnityEngine;
using System.Collections;

/// <summary>
/// Быстрый тест многоцветной покраски для демонстрации
/// </summary>
public class MultiColorQuickTest : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(TestMultiColorPainting());
    }
    
    IEnumerator TestMultiColorPainting()
    {
        // Подождать пока система инициализируется
        yield return new WaitForSeconds(3f);
        
        Debug.Log("[MultiColorQuickTest] 🎨 Начинаем тест многоцветной покраски!");
        
        // Найти AsyncSegmentationManager
        var segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager == null)
        {
            Debug.LogError("[MultiColorQuickTest] ❌ AsyncSegmentationManager не найден!");
            yield break;
        }
        
        // Тест 1: Включить многоцветный режим
        Debug.Log("[MultiColorQuickTest] 🌈 Тест 1: Включаем многоцветный режим");
        segmentationManager.ShowAllClassesColored();
        yield return new WaitForSeconds(3f);
        
        // Тест 2: Покрасить стены в синий
        Debug.Log("[MultiColorQuickTest] 🔵 Тест 2: Красим стены в синий");
        segmentationManager.SetSelectedClass(0); // Стены
        segmentationManager.SetPaintColor(Color.blue);
        yield return new WaitForSeconds(3f);
        
        // Тест 3: Покрасить пол в коричневый
        Debug.Log("[MultiColorQuickTest] 🟫 Тест 3: Красим пол в коричневый");
        segmentationManager.SetSelectedClass(3); // Пол
        segmentationManager.SetPaintColor(new Color(0.6f, 0.4f, 0.2f));
        yield return new WaitForSeconds(3f);
        
        // Тест 4: Покрасить потолок в кремовый
        Debug.Log("[MultiColorQuickTest] ⬜ Тест 4: Красим потолок в кремовый");
        segmentationManager.SetSelectedClass(5); // Потолок
        segmentationManager.SetPaintColor(new Color(0.95f, 0.95f, 0.9f));
        yield return new WaitForSeconds(3f);
        
        // Тест 5: Вернуться к многоцветному режиму
        Debug.Log("[MultiColorQuickTest] 🌈 Тест 5: Возвращаемся к многоцветному режиму");
        segmentationManager.ShowAllClassesColored();
        yield return new WaitForSeconds(3f);
        
        Debug.Log("[MultiColorQuickTest] ✅ Тест многоцветной покраски завершен!");
        Debug.Log("[MultiColorQuickTest] 🎉 Каждый элемент маски можно красить отдельно!");
    }
    
    /// <summary>
    /// Ручной тест через контекстное меню
    /// </summary>
    [ContextMenu("Test Multi-Color Painting")]
    public void ManualTest()
    {
        StartCoroutine(TestMultiColorPainting());
    }
    
    /// <summary>
    /// Быстрый тест случайных цветов
    /// </summary>
    [ContextMenu("Random Colors Test")]
    public void RandomColorsTest()
    {
        StartCoroutine(TestRandomColors());
    }
    
    IEnumerator TestRandomColors()
    {
        var segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager == null) yield break;
        
        Debug.Log("[MultiColorQuickTest] 🎲 Тест случайных цветов!");
        
        // Случайные цвета для разных классов
        int[] classes = {0, 3, 5, 8, 14, 15, 19, 23}; // Стены, пол, потолок, окна, двери, столы, стулья, диваны
        
        foreach (int classId in classes)
        {
            Color randomColor = new Color(
                Random.Range(0.2f, 1f),
                Random.Range(0.2f, 1f),
                Random.Range(0.2f, 1f)
            );
            
            segmentationManager.SetSelectedClass(classId);
            segmentationManager.SetPaintColor(randomColor);
            
            Debug.Log($"[MultiColorQuickTest] 🎨 Класс {classId} покрашен в {ColorUtility.ToHtmlStringRGB(randomColor)}");
            
            yield return new WaitForSeconds(1f);
        }
        
        // Включить многоцветный режим для показа всех цветов
        segmentationManager.ShowAllClassesColored();
        Debug.Log("[MultiColorQuickTest] 🌈 Все цвета применены!");
    }
}
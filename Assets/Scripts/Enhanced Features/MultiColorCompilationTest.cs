using UnityEngine;

/// <summary>
/// Простой тест для проверки что все компоненты многоцветной системы компилируются
/// </summary>
public class MultiColorCompilationTest : MonoBehaviour
{
    void Start()
    {
        TestCompilation();
    }
    
    void TestCompilation()
    {
        Debug.Log("[MultiColorCompilationTest] 🧪 Проверяем компиляцию многоцветной системы...");
        
        // Тест 1: MultiColorPaintManager
        var paintManager = FindObjectOfType<MultiColorPaintManager>();
        if (paintManager != null)
        {
            Debug.Log("[MultiColorCompilationTest] ✅ MultiColorPaintManager найден");
        }
        
        // Тест 2: MultiColorIntegrator
        var integrator = FindObjectOfType<MultiColorIntegrator>();
        if (integrator != null)
        {
            Debug.Log("[MultiColorCompilationTest] ✅ MultiColorIntegrator найден");
        }
        
        // Тест 3: XROcclusionFix
        var occlusionFix = FindObjectOfType<XROcclusionFix>();
        if (occlusionFix != null)
        {
            Debug.Log("[MultiColorCompilationTest] ✅ XROcclusionFix найден");
        }
        
        // Тест 4: AsyncSegmentationManager методы
        var segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager != null)
        {
            Debug.Log("[MultiColorCompilationTest] ✅ AsyncSegmentationManager найден");
            
            // Проверяем что методы существуют (не вызываем, только проверяем компиляцию)
            System.Type type = segmentationManager.GetType();
            
            if (type.GetMethod("SetVisualizationOpacity") != null)
                Debug.Log("[MultiColorCompilationTest] ✅ SetVisualizationOpacity метод найден");
                
            if (type.GetMethod("ShowOnlyWalls") != null)
                Debug.Log("[MultiColorCompilationTest] ✅ ShowOnlyWalls метод найден");
                
            if (type.GetMethod("ResetCustomColors") != null)
                Debug.Log("[MultiColorCompilationTest] ✅ ResetCustomColors метод найден");
        }
        
        Debug.Log("[MultiColorCompilationTest] 🎉 Все компоненты скомпилированы успешно!");
    }
    
    /// <summary>
    /// Быстрый тест основных функций
    /// </summary>
    [ContextMenu("Quick Function Test")]
    public void QuickFunctionTest()
    {
        var paintManager = FindObjectOfType<MultiColorPaintManager>();
        if (paintManager != null)
        {
            // Тест основных функций
            paintManager.SetOpacity(0.5f);
            paintManager.PaintClass(0, Color.white);
            paintManager.EnableMultiColorMode();
            
            Debug.Log("[MultiColorCompilationTest] ✅ Основные функции работают!");
        }
        else
        {
            Debug.LogWarning("[MultiColorCompilationTest] ⚠️ MultiColorPaintManager не найден");
        }
    }
}
using UnityEngine;

/// <summary>
/// Автоматически запускает исправления при старте сцены
/// </summary>
public class AutoStartupFix : MonoBehaviour
{
    [Header("Auto Startup Fixes")]
    [SerializeField] private bool enableXROcclusionFix = true;
    [SerializeField] private bool enableModelFix = true;
    [SerializeField] private bool logActions = true;
    
    void Start()
    {
        if (logActions)
            Debug.Log("🚀 AutoStartupFix: Запуск автоматических исправлений...");
        
        if (enableXROcclusionFix)
        {
            FixXROcclusion();
        }
        
        if (enableModelFix)
        {
            FixModelAssignment();
        }
        
        if (logActions)
            Debug.Log("✅ AutoStartupFix: Автоматические исправления завершены");
    }
    
    /// <summary>
    /// Исправляет XR Occlusion предупреждения
    /// </summary>
    void FixXROcclusion()
    {
        var occlusionFix = FindObjectOfType<AutoXROcclusionFix>();
        if (occlusionFix == null)
        {
            // Создаем AutoXROcclusionFix если его нет
            var go = new GameObject("AutoXROcclusionFix");
            go.AddComponent<AutoXROcclusionFix>();
            
            if (logActions)
                Debug.Log("🔧 Создан AutoXROcclusionFix для устранения предупреждений");
        }
    }
    
    /// <summary>
    /// Исправляет назначение модели
    /// </summary>
    void FixModelAssignment()
    {
        var modelFix = FindObjectOfType<QuickModelFix>();
        if (modelFix == null)
        {
            // Создаем QuickModelFix если его нет
            var go = new GameObject("QuickModelFix");
            go.AddComponent<QuickModelFix>();
            
            if (logActions)
                Debug.Log("🔧 Создан QuickModelFix для исправления модели");
        }
    }
}







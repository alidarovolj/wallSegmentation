using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Автоматически исправляет предупреждение XR Occlusion Subsystem
/// Отключает AROcclusionManager если subsystem недоступен
/// </summary>
public class AutoXROcclusionFix : MonoBehaviour
{
    [Header("Auto XR Occlusion Fix")]
    [SerializeField] private bool enableAutoFix = true;
    [SerializeField] private bool logActions = true;
    
    void Start()
    {
        if (enableAutoFix)
        {
            FixXROcclusionIssue();
        }
    }
    
    /// <summary>
    /// Исправляет проблему XR Occlusion автоматически
    /// </summary>
    void FixXROcclusionIssue()
    {
        // Найти все AROcclusionManager в сцене
        AROcclusionManager[] occlusionManagers = FindObjectsOfType<AROcclusionManager>();
        
        if (occlusionManagers.Length == 0)
        {
            if (logActions)
                Debug.Log("[AutoXROcclusionFix] ✅ AROcclusionManager не найден - предупреждение не актуально");
            return;
        }
        
        foreach (var manager in occlusionManagers)
        {
            if (manager != null && manager.enabled)
            {
                // Проверяем доступность subsystem
                bool isAvailable = IsOcclusionSubsystemAvailable(manager);
                
                if (!isAvailable)
                {
                    // Отключаем manager чтобы убрать предупреждение
                    manager.enabled = false;
                    
                    if (logActions)
                        Debug.Log($"[AutoXROcclusionFix] 🔧 Отключен AROcclusionManager '{manager.name}' (subsystem недоступен в симуляторе/редакторе)");
                }
                else
                {
                    if (logActions)
                        Debug.Log($"[AutoXROcclusionFix] ✅ AROcclusionManager '{manager.name}' работает корректно");
                }
            }
        }
    }
    
    /// <summary>
    /// Проверяет доступность XR Occlusion Subsystem
    /// </summary>
    bool IsOcclusionSubsystemAvailable(AROcclusionManager manager)
    {
        try
        {
            // Простая проверка через subsystem property
            return manager.subsystem != null && manager.subsystem.running;
        }
        catch
        {
            // Если возникает ошибка - subsystem недоступен
            return false;
        }
    }
    
    /// <summary>
    /// Принудительно отключить все AROcclusionManager (для отладки)
    /// </summary>
    [ContextMenu("🔧 Force Disable All AR Occlusion Managers")]
    public void ForceDisableAllOcclusionManagers()
    {
        AROcclusionManager[] managers = FindObjectsOfType<AROcclusionManager>();
        
        foreach (var manager in managers)
        {
            if (manager != null)
            {
                manager.enabled = false;
                if (logActions)
                    Debug.Log($"[AutoXROcclusionFix] 🔧 Принудительно отключен '{manager.name}'");
            }
        }
        
        Debug.Log($"[AutoXROcclusionFix] ✅ Отключено {managers.Length} AROcclusionManager");
    }
    
    /// <summary>
    /// Попробовать включить AROcclusionManager если subsystem стал доступен
    /// </summary>
    [ContextMenu("🔄 Try Enable AR Occlusion Managers")]
    public void TryEnableOcclusionManagers()
    {
        AROcclusionManager[] managers = FindObjectsOfType<AROcclusionManager>();
        int enabledCount = 0;
        
        foreach (var manager in managers)
        {
            if (manager != null && !manager.enabled)
            {
                bool isAvailable = IsOcclusionSubsystemAvailable(manager);
                if (isAvailable)
                {
                    manager.enabled = true;
                    enabledCount++;
                    
                    if (logActions)
                        Debug.Log($"[AutoXROcclusionFix] ✅ Включен '{manager.name}' (subsystem доступен)");
                }
            }
        }
        
        if (enabledCount > 0)
        {
            Debug.Log($"[AutoXROcclusionFix] ✅ Включено {enabledCount} AROcclusionManager");
        }
        else
        {
            Debug.Log("[AutoXROcclusionFix] ⚠️ Ни один AROcclusionManager не был включен (subsystem недоступен)");
        }
    }
}







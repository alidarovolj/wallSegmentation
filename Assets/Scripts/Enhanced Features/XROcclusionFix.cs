using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

/// <summary>
/// Исправляет проблемы с XR Occlusion Subsystem
/// Автоматически отключает occlusion компоненты если subsystem недоступен
/// </summary>
public class XROcclusionFix : MonoBehaviour
{
    [Header("XR Occlusion Fix")]
    [SerializeField] private bool autoDisableOnUnavailable = true;
    [SerializeField] private bool logStatus = true;
    
    private AROcclusionManager occlusionManager;
    
    void Start()
    {
        CheckAndFixOcclusionSubsystem();
    }
    
    void CheckAndFixOcclusionSubsystem()
    {
        // Найти AROcclusionManager в сцене
        occlusionManager = FindObjectOfType<AROcclusionManager>();
        
        if (occlusionManager == null)
        {
            if (logStatus)
                Debug.Log("[XROcclusionFix] ✅ AROcclusionManager не найден - проблема не актуальна");
            return;
        }
        
        // Проверить доступность subsystem
        bool isSubsystemAvailable = CheckOcclusionSubsystemAvailability();
        
        if (!isSubsystemAvailable && autoDisableOnUnavailable)
        {
            DisableOcclusionManager();
        }
        else if (isSubsystemAvailable)
        {
            if (logStatus)
                Debug.Log("[XROcclusionFix] ✅ XR Occlusion Subsystem доступен");
        }
    }
    
    bool CheckOcclusionSubsystemAvailability()
    {
        try
        {
            // Проверяем через XRGeneralSettings
            var xrSettings = XRGeneralSettings.Instance;
            if (xrSettings == null || xrSettings.Manager == null || !xrSettings.Manager.isInitializationComplete)
            {
                if (logStatus)
                    Debug.LogWarning("[XROcclusionFix] ⚠️ XR Manager не инициализирован");
                return false;
            }
            
            // Проверяем активный loader
            var activeLoader = xrSettings.Manager.activeLoader;
            if (activeLoader == null)
            {
                if (logStatus)
                    Debug.LogWarning("[XROcclusionFix] ⚠️ XR Loader не активен");
                return false;
            }
            
            // Проверяем occlusion subsystem через активный loader
            var occlusionSubsystem = activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>();
            bool available = occlusionSubsystem != null && occlusionSubsystem.running;
            
            if (logStatus)
            {
                if (available)
                    Debug.Log("[XROcclusionFix] ✅ XR Occlusion Subsystem активен");
                else
                    Debug.LogWarning("[XROcclusionFix] ⚠️ XR Occlusion Subsystem недоступен");
            }
            
            return available;
        }
        catch (System.Exception e)
        {
            if (logStatus)
                Debug.LogWarning($"[XROcclusionFix] ❌ Ошибка проверки subsystem: {e.Message}");
            return false;
        }
    }
    
    void DisableOcclusionManager()
    {
        if (occlusionManager != null)
        {
            occlusionManager.enabled = false;
            
            if (logStatus)
                Debug.Log("[XROcclusionFix] 🔧 AROcclusionManager отключен (subsystem недоступен)");
        }
    }
    
    /// <summary>
    /// Попытаться включить occlusion manager если subsystem стал доступен
    /// </summary>
    public void TryEnableOcclusion()
    {
        if (occlusionManager != null && !occlusionManager.enabled)
        {
            bool available = CheckOcclusionSubsystemAvailability();
            if (available)
            {
                occlusionManager.enabled = true;
                if (logStatus)
                    Debug.Log("[XROcclusionFix] ✅ AROcclusionManager включен");
            }
        }
    }
    
    /// <summary>
    /// Получить статус occlusion subsystem
    /// </summary>
    public bool IsOcclusionAvailable()
    {
        return CheckOcclusionSubsystemAvailability();
    }
}
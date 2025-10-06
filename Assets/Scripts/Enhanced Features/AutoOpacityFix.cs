using UnityEngine;

/// <summary>
/// Автоматически снижает прозрачность маски при запуске
/// </summary>
public class AutoOpacityFix : MonoBehaviour
{
    [Header("Auto Opacity Fix")]
    [SerializeField] private float targetOpacity = 0.2f;
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private float delayBeforeFix = 1.0f;
    
    void Start()
    {
        if (runOnStart)
        {
            Invoke(nameof(ApplyOpacityFix), delayBeforeFix);
        }
    }
    
    void ApplyOpacityFix()
    {
        Debug.Log($"🔧 AutoOpacityFix: Снижаем прозрачность до {targetOpacity}");
        
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager != null)
        {
            // Принудительно устанавливаем низкую прозрачность
            asyncManager.SetVisualizationOpacity(targetOpacity);
            Debug.Log($"✅ Прозрачность снижена до {targetOpacity} - экран должен стать менее красным");
        }
        else
        {
            Debug.LogError("❌ AsyncSegmentationManager не найден!");
        }
    }
    
    [ContextMenu("🔧 Apply Fix Now")]
    public void ApplyFixNow()
    {
        ApplyOpacityFix();
    }
}




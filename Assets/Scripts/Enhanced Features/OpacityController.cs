using UnityEngine;

/// <summary>
/// Простой контроллер прозрачности маски для быстрой настройки
/// </summary>
public class OpacityController : MonoBehaviour
{
    [Header("Opacity Control")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float currentOpacity = 0.3f;
    
    [Header("Quick Settings")]
    [SerializeField] private bool applyOnStart = true;
    
    private AsyncSegmentationManager asyncManager;
    
    void Start()
    {
        asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        
        if (applyOnStart && asyncManager != null)
        {
            SetOpacity(currentOpacity);
            Debug.Log($"🔆 Прозрачность установлена автоматически: {currentOpacity}");
        }
    }
    
    /// <summary>
    /// Устанавливает прозрачность маски
    /// </summary>
    public void SetOpacity(float opacity)
    {
        currentOpacity = Mathf.Clamp01(opacity);
        
        if (asyncManager != null)
        {
            asyncManager.SetVisualizationOpacity(currentOpacity);
            Debug.Log($"🔆 Прозрачность изменена: {currentOpacity:F2}");
        }
    }
    
    /// <summary>
    /// Кнопки быстрой настройки
    /// </summary>
    [ContextMenu("🔹 Очень прозрачно (0.2)")]
    public void SetVeryTransparent() => SetOpacity(0.2f);
    
    [ContextMenu("🔸 Слегка видимо (0.4)")]
    public void SetSlightlyVisible() => SetOpacity(0.4f);
    
    [ContextMenu("🔶 Умеренно (0.6)")]
    public void SetModerate() => SetOpacity(0.6f);
    
    [ContextMenu("🔺 Непрозрачно (0.8)")]
    public void SetOpaque() => SetOpacity(0.8f);
    
    /// <summary>
    /// Обновляется из инспектора
    /// </summary>
    void OnValidate()
    {
        if (Application.isPlaying && asyncManager != null)
        {
            SetOpacity(currentOpacity);
        }
    }
}







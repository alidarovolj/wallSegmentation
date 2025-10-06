using UnityEngine;

/// <summary>
/// Финальное исправление видимости - делает маску почти невидимой
/// </summary>
public class UltimateVisibilityFix : MonoBehaviour
{
    [Header("Ultimate Visibility Fix")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private float fixDelay = 0.5f; // Быстро после старта
    
    [Header("Ultra Low Settings")]
    [SerializeField] private float ultraLowOpacity = 0.1f;
    [SerializeField] private Color subtleColor = new Color(0.8f, 0.2f, 0.2f, 0.15f);
    
    void Start()
    {
        if (applyOnStart)
        {
            // Применяем исправление сразу и потом еще раз для надежности
            Invoke(nameof(ApplyUltimateFix), fixDelay);
            Invoke(nameof(ApplyUltimateFix), fixDelay + 1.0f);
            Invoke(nameof(ApplyUltimateFix), fixDelay + 2.0f);
        }
    }
    
    /// <summary>
    /// Применяет максимальное снижение видимости
    /// </summary>
    void ApplyUltimateFix()
    {
        Debug.Log("🚀 UltimateVisibilityFix: Применяем максимальное снижение видимости!");
        
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogError("❌ AsyncSegmentationManager не найден!");
            return;
        }
        
        // 1. Устанавливаем ультра-низкую прозрачность
        asyncManager.SetVisualizationOpacity(ultraLowOpacity);
        Debug.Log($"🔆 Прозрачность: {ultraLowOpacity}");
        
        // 2. Меняем цвет на очень тонкий
        SetColorThroughReflection(asyncManager, subtleColor);
        Debug.Log($"🎨 Цвет: {subtleColor}");
        
        // 3. Принудительно обновляем материал
        ForceUpdateMaterial(asyncManager);
        
        // 4. Дополнительно - устанавливаем настройки через поля
        SetOpacityThroughReflection(asyncManager, ultraLowOpacity);
        
        Debug.Log("✅ ULTIMATE FIX APPLIED! Экран должен стать почти прозрачным!");
    }
    
    void SetColorThroughReflection(AsyncSegmentationManager manager, Color color)
    {
        try
        {
            var colorField = typeof(AsyncSegmentationManager).GetField("paintColor", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (colorField != null)
            {
                colorField.SetValue(manager, color);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Ошибка установки цвета: {e.Message}");
        }
    }
    
    void SetOpacityThroughReflection(AsyncSegmentationManager manager, float opacity)
    {
        try
        {
            var opacityField = typeof(AsyncSegmentationManager).GetField("visualizationOpacity", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (opacityField != null)
            {
                opacityField.SetValue(manager, opacity);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Ошибка установки прозрачности: {e.Message}");
        }
    }
    
    void ForceUpdateMaterial(AsyncSegmentationManager manager)
    {
        try
        {
            var updateMethod = typeof(AsyncSegmentationManager).GetMethod("UpdateMaterialParameters", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (updateMethod != null)
            {
                updateMethod.Invoke(manager, null);
                Debug.Log("🔄 Материал обновлен принудительно");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Ошибка обновления материала: {e.Message}");
        }
    }
    
    /// <summary>
    /// Кнопки быстрого доступа
    /// </summary>
    [ContextMenu("🚀 Apply Ultimate Fix")]
    public void ApplyUltimateFixNow()
    {
        ApplyUltimateFix();
    }
    
    [ContextMenu("👻 Make Nearly Invisible")]
    public void MakeNearlyInvisible()
    {
        ultraLowOpacity = 0.05f;
        subtleColor = new Color(0.5f, 0.1f, 0.1f, 0.05f);
        ApplyUltimateFix();
    }
    
    [ContextMenu("📊 Show Current Settings")]
    public void ShowCurrentSettings()
    {
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager != null)
        {
            try
            {
                var opacityField = typeof(AsyncSegmentationManager).GetField("visualizationOpacity", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var colorField = typeof(AsyncSegmentationManager).GetField("paintColor", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (opacityField != null && colorField != null)
                {
                    float currentOpacity = (float)opacityField.GetValue(asyncManager);
                    Color currentColor = (Color)colorField.GetValue(asyncManager);
                    
                    Debug.Log($"📊 Текущая прозрачность: {currentOpacity}");
                    Debug.Log($"📊 Текущий цвет: {currentColor}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка получения настроек: {e.Message}");
            }
        }
    }
}




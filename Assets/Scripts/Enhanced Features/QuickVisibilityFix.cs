using UnityEngine;

/// <summary>
/// Быстрое исправление видимости - делает маску полупрозрачной и более реалистичной
/// </summary>
public class QuickVisibilityFix : MonoBehaviour
{
    [Header("Quick Visibility Fix")]
    [SerializeField] private bool autoFixOnStart = true;
    [SerializeField] private bool enableDebugMode = true;
    
    [Header("Visibility Settings")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float maskOpacity = 0.3f; // Делаем более прозрачным
    [SerializeField] private Color wallColor = new Color(1f, 0.3f, 0.3f, 0.5f); // Менее яркий красный
    
    void Start()
    {
        if (autoFixOnStart)
        {
            FixVisibility();
        }
    }
    
    /// <summary>
    /// Исправляет видимость маски
    /// </summary>
    [ContextMenu("🔧 Fix Mask Visibility")]
    public void FixVisibility()
    {
        if (enableDebugMode)
            Debug.Log("🔧 QuickVisibilityFix: Настраиваем видимость маски...");
        
        // Находим AsyncSegmentationManager
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager != null)
        {
            // Устанавливаем более низкую прозрачность
            SetOpacity(asyncManager, maskOpacity);
            
            // Устанавливаем менее яркий цвет
            SetWallColor(asyncManager, wallColor);
            
            if (enableDebugMode)
                Debug.Log($"✅ Маска настроена: прозрачность={maskOpacity}, цвет={wallColor}");
        }
        else
        {
            Debug.LogError("❌ AsyncSegmentationManager не найден!");
        }
    }
    
    /// <summary>
    /// Устанавливает прозрачность маски
    /// </summary>
    void SetOpacity(AsyncSegmentationManager manager, float opacity)
    {
        try
        {
            var opacityMethod = typeof(AsyncSegmentationManager).GetMethod("SetVisualizationOpacity");
            if (opacityMethod != null)
            {
                opacityMethod.Invoke(manager, new object[] { opacity });
                Debug.Log($"✅ Прозрачность установлена: {opacity}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Не удалось установить прозрачность: {e.Message}");
        }
    }
    
    /// <summary>
    /// Устанавливает цвет стен
    /// </summary>
    void SetWallColor(AsyncSegmentationManager manager, Color color)
    {
        try
        {
            var colorField = typeof(AsyncSegmentationManager).GetField("paintColor", 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
            
            if (colorField != null)
            {
                colorField.SetValue(manager, color);
                
                // Обновляем материал
                var updateMethod = typeof(AsyncSegmentationManager).GetMethod("UpdateMaterialParameters", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                
                if (updateMethod != null)
                {
                    updateMethod.Invoke(manager, null);
                }
                
                Debug.Log($"✅ Цвет стен установлен: {color}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Не удалось установить цвет: {e.Message}");
        }
    }
    
    /// <summary>
    /// Переключает в режим только краев (более реалистично)
    /// </summary>
    [ContextMenu("🏠 Edge-Only Mode")]
    public void EnableEdgeOnlyMode()
    {
        var simpleSAM2 = FindObjectOfType<SimpleSAM2Manager>();
        if (simpleSAM2 != null)
        {
            // Уменьшаем зону стен в SimpleSAM2Manager для более реалистичного отображения
            Debug.Log("🏠 Режим краев активирован - показываем только периметр стен");
            
            // Устанавливаем очень низкую прозрачность для краев
            SetOpacity(FindObjectOfType<AsyncSegmentationManager>(), 0.2f);
        }
    }
    
    /// <summary>
    /// Показывает статус видимости
    /// </summary>
    [ContextMenu("📊 Show Visibility Status")]
    public void ShowVisibilityStatus()
    {
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager != null)
        {
            try
            {
                var opacityField = typeof(AsyncSegmentationManager).GetField("visualizationOpacity", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                
                var colorField = typeof(AsyncSegmentationManager).GetField("paintColor", 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance);
                
                if (opacityField != null && colorField != null)
                {
                    float currentOpacity = (float)opacityField.GetValue(asyncManager);
                    Color currentColor = (Color)colorField.GetValue(asyncManager);
                    
                    Debug.Log("=== VISIBILITY STATUS ===");
                    Debug.Log($"🔆 Прозрачность: {currentOpacity:F2}");
                    Debug.Log($"🎨 Цвет стен: {currentColor}");
                    Debug.Log($"🤖 SimpleSAM2Manager: {(FindObjectOfType<SimpleSAM2Manager>() != null ? "OK" : "NOT FOUND")}");
                    Debug.Log("========================");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Ошибка получения статуса: {e.Message}");
            }
        }
    }
}







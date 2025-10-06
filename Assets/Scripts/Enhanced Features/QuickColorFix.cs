using UnityEngine;

/// <summary>
/// Быстрое исправление цвета - делает стены менее яркими и более прозрачными
/// </summary>
public class QuickColorFix : MonoBehaviour
{
    [Header("Quick Color Fix")]
    [SerializeField] private bool autoFixOnStart = true;
    [SerializeField] private float delayBeforeFix = 2.0f;
    
    [Header("New Settings")]
    [SerializeField] private Color newWallColor = new Color(1f, 0.2f, 0.2f, 0.3f); // Тёмно-красный, прозрачный
    [SerializeField] private float newOpacity = 0.2f;
    
    void Start()
    {
        if (autoFixOnStart)
        {
            Invoke(nameof(ApplyColorFix), delayBeforeFix);
        }
    }
    
    void ApplyColorFix()
    {
        Debug.Log("🎨 QuickColorFix: Применяем менее яркий цвет и низкую прозрачность");
        
        var asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager != null)
        {
            // Меняем цвет через рефлексию
            var colorField = typeof(AsyncSegmentationManager).GetField("paintColor", 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
            
            if (colorField != null)
            {
                colorField.SetValue(asyncManager, newWallColor);
                Debug.Log($"🎨 Цвет изменен на: {newWallColor}");
            }
            
            // Устанавливаем низкую прозрачность
            asyncManager.SetVisualizationOpacity(newOpacity);
            Debug.Log($"🔆 Прозрачность установлена: {newOpacity}");
            
            // Обновляем материал
            var updateMethod = typeof(AsyncSegmentationManager).GetMethod("UpdateMaterialParameters", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (updateMethod != null)
            {
                updateMethod.Invoke(asyncManager, null);
                Debug.Log("✅ Материал обновлен - экран должен стать менее красным!");
            }
        }
        else
        {
            Debug.LogError("❌ AsyncSegmentationManager не найден!");
        }
    }
    
    [ContextMenu("🎨 Apply Color Fix Now")]
    public void ApplyFixNow()
    {
        ApplyColorFix();
    }
    
    [ContextMenu("🔹 Make Nearly Invisible")]
    public void MakeNearlyInvisible()
    {
        newOpacity = 0.1f;
        newWallColor = new Color(1f, 0.1f, 0.1f, 0.1f);
        ApplyColorFix();
    }
    
    [ContextMenu("🏠 Edge-Only Transparent")]
    public void EdgeOnlyTransparent()
    {
        newOpacity = 0.15f;
        newWallColor = new Color(0.8f, 0.3f, 0.3f, 0.2f);
        ApplyColorFix();
    }
}







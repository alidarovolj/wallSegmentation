using UnityEngine;

/// <summary>
/// Мониторит статус SAM2 системы и показывает текущее состояние
/// </summary>
public class SAM2StatusMonitor : MonoBehaviour
{
    [Header("SAM2 System Status")]
    [SerializeField] private bool logStatusOnStart = true;
    [SerializeField] private float statusUpdateInterval = 5.0f;
    
    [Header("Current Status")]
    [SerializeField] private string currentModel = "Not checked";
    [SerializeField] private bool isSAM2Active = false;
    [SerializeField] private bool hasSimpleSAM2Manager = false;
    [SerializeField] private bool isProcessingFrames = false;
    
    private AsyncSegmentationManager asyncManager;
    private SimpleSAM2Manager simpleSAM2Manager;
    
    void Start()
    {
        FindSystemComponents();
        
        if (logStatusOnStart)
        {
            LogCurrentStatus();
        }
        
        // Периодически обновляем статус
        InvokeRepeating(nameof(UpdateStatus), 1.0f, statusUpdateInterval);
    }
    
    /// <summary>
    /// Находит компоненты системы
    /// </summary>
    void FindSystemComponents()
    {
        asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        simpleSAM2Manager = FindObjectOfType<SimpleSAM2Manager>();
        
        if (asyncManager != null)
        {
            Debug.Log("✅ AsyncSegmentationManager найден");
        }
        
        if (simpleSAM2Manager != null)
        {
            Debug.Log("✅ SimpleSAM2Manager найден");
            hasSimpleSAM2Manager = true;
        }
    }
    
    /// <summary>
    /// Обновляет статус системы
    /// </summary>
    void UpdateStatus()
    {
        if (asyncManager != null)
        {
            // Получаем информацию через рефлексию
            var modelField = typeof(AsyncSegmentationManager).GetField("modelAsset", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            var useSAM2Field = typeof(AsyncSegmentationManager).GetField("useSAM2Models", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (modelField != null)
            {
                var model = (Unity.Sentis.ModelAsset)modelField.GetValue(asyncManager);
                currentModel = model != null ? model.name : "NULL";
            }
            
            if (useSAM2Field != null)
            {
                isSAM2Active = (bool)useSAM2Field.GetValue(asyncManager);
            }
        }
    }
    
    /// <summary>
    /// Выводит текущий статус системы
    /// </summary>
    [ContextMenu("📊 Log Current Status")]
    public void LogCurrentStatus()
    {
        Debug.Log("=== SAM2 SYSTEM STATUS ===");
        Debug.Log($"📄 Текущая модель: {currentModel}");
        Debug.Log($"🤖 SAM2 режим активен: {isSAM2Active}");
        Debug.Log($"🔧 SimpleSAM2Manager доступен: {hasSimpleSAM2Manager}");
        Debug.Log($"⚙️ AsyncSegmentationManager: {(asyncManager != null ? "OK" : "NOT FOUND")}");
        
        if (isSAM2Active && hasSimpleSAM2Manager)
        {
            Debug.Log("✅ SAM2 система настроена корректно!");
        }
        else if (isSAM2Active && !hasSimpleSAM2Manager)
        {
            Debug.LogWarning("⚠️ SAM2 активен, но SimpleSAM2Manager не найден!");
        }
        else
        {
            Debug.Log("ℹ️ SAM2 система не активна");
        }
        
        Debug.Log("========================");
    }
    
    /// <summary>
    /// Принудительно переключить на SAM2 режим
    /// </summary>
    [ContextMenu("🔄 Force Enable SAM2")]
    public void ForceEnableSAM2()
    {
        if (asyncManager != null)
        {
            var useSAM2Field = typeof(AsyncSegmentationManager).GetField("useSAM2Models", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            var useSegFormerField = typeof(AsyncSegmentationManager).GetField("useSegFormerModels", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (useSAM2Field != null)
            {
                useSAM2Field.SetValue(asyncManager, true);
                Debug.Log("✅ SAM2 режим принудительно включен");
            }
            
            if (useSegFormerField != null)
            {
                useSegFormerField.SetValue(asyncManager, false);
                Debug.Log("🚫 SegFormer режим отключен");
            }
            
            UpdateStatus();
            LogCurrentStatus();
        }
        else
        {
            Debug.LogError("❌ AsyncSegmentationManager не найден!");
        }
    }
    
    /// <summary>
    /// Тестирует SimpleSAM2Manager
    /// </summary>
    [ContextMenu("🧪 Test SimpleSAM2Manager")]
    public void TestSimpleSAM2Manager()
    {
        if (simpleSAM2Manager != null)
        {
            // Вызываем тестовый метод если он доступен
            var testMethod = typeof(SimpleSAM2Manager).GetMethod("TestSimpleMask");
            if (testMethod != null)
            {
                testMethod.Invoke(simpleSAM2Manager, null);
                Debug.Log("🧪 Тест SimpleSAM2Manager запущен");
            }
            else
            {
                Debug.Log("ℹ️ SimpleSAM2Manager найден, но тестовый метод недоступен");
            }
        }
        else
        {
            Debug.LogError("❌ SimpleSAM2Manager не найден!");
        }
    }
}







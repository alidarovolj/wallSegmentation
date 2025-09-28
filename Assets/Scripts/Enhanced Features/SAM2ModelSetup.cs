using UnityEngine;
using Unity.Sentis;

/// <summary>
/// Компонент для быстрой настройки SAM2 моделей в Unity Inspector
/// Автоматически находит TensorFlow Lite модели в папке Models/sam_2/
/// </summary>
public class SAM2ModelSetup : MonoBehaviour
{
    [Header("SAM2 Model Auto-Setup")]
    [Tooltip("Автоматически найти и назначить SAM2 модели при старте")]
    [SerializeField] private bool autoSetupOnStart = true;
    
    [Header("Target Components")]
    [Tooltip("SAM2SegmentationManager для настройки")]
    [SerializeField] private SAM2SegmentationManager sam2Manager;
    
    [Tooltip("AsyncSegmentationManager для интеграции")]
    [SerializeField] private AsyncSegmentationManager asyncManager;

    [Header("Model Assets")]
    [Tooltip("SAM2 Encoder модель (будет найдена автоматически)")]
    [SerializeField] private ModelAsset encoderModel;
    
    [Tooltip("SAM2 Decoder модель (будет найдена автоматически)")]
    [SerializeField] private ModelAsset decoderModel;

    [Header("Status")]
    [SerializeField] private bool modelsFound = false;
    [SerializeField] private bool setupCompleted = false;

    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupSAM2Models();
        }
    }

    /// <summary>
    /// Автоматическая настройка SAM2 моделей
    /// </summary>
    [ContextMenu("Setup SAM2 Models")]
    public void SetupSAM2Models()
    {
        Debug.Log("🔍 Поиск SAM2 моделей...");

        // Автопоиск компонентов
        if (sam2Manager == null)
            sam2Manager = FindObjectOfType<SAM2SegmentationManager>();
        
        if (asyncManager == null)
            asyncManager = FindObjectOfType<AsyncSegmentationManager>();

        // Поиск моделей
        FindSAM2Models();

        // Настройка компонентов
        if (modelsFound)
        {
            ConfigureComponents();
            setupCompleted = true;
            Debug.Log("✅ SAM2 модели успешно настроены!");
        }
        else
        {
            Debug.LogWarning("⚠️ SAM2 модели не найдены в Assets/Models/sam_2/");
        }
    }

    /// <summary>
    /// Поиск SAM2 TensorFlow Lite моделей
    /// </summary>
    private void FindSAM2Models()
    {
        // Поиск в Resources (если модели там)
        var encoderAsset = Resources.Load<ModelAsset>("Models/sam_2/Segment-Anything-Model-2_SAM2Encoder");
        var decoderAsset = Resources.Load<ModelAsset>("Models/sam_2/Segment-Anything-Model-2_SAM2Decoder");

        if (encoderAsset != null && decoderAsset != null)
        {
            encoderModel = encoderAsset;
            decoderModel = decoderAsset;
            modelsFound = true;
            Debug.Log("✅ SAM2 модели найдены в Resources");
            return;
        }

        // Альтернативный поиск через GUID (если знаем)
        // Можно добавить поиск через AssetDatabase в Editor
        
        #if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("SAM2Encoder t:ModelAsset");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            encoderModel = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(path);
        }

        guids = UnityEditor.AssetDatabase.FindAssets("SAM2Decoder t:ModelAsset");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            decoderModel = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(path);
        }

        if (encoderModel != null && decoderModel != null)
        {
            modelsFound = true;
            Debug.Log("✅ SAM2 модели найдены через AssetDatabase");
        }
        #endif
    }

    /// <summary>
    /// Настройка найденных компонентов
    /// </summary>
    private void ConfigureComponents()
    {
        // Настройка SAM2SegmentationManager
        if (sam2Manager != null && encoderModel != null && decoderModel != null)
        {
            // Используем рефлексию для назначения моделей (так как поля приватные)
            var sam2Type = typeof(SAM2SegmentationManager);
            
            var encoderField = sam2Type.GetField("sam2EncoderModel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decoderField = sam2Type.GetField("sam2DecoderModel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (encoderField != null && decoderField != null)
            {
                encoderField.SetValue(sam2Manager, encoderModel);
                decoderField.SetValue(sam2Manager, decoderModel);
                Debug.Log("✅ SAM2SegmentationManager настроен");
            }
        }

        // Настройка AsyncSegmentationManager для использования SAM2
        if (asyncManager != null && sam2Manager != null)
        {
            var asyncType = typeof(AsyncSegmentationManager);
            
            var useSAM2Field = asyncType.GetField("useSAM2Models", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var sam2ManagerField = asyncType.GetField("sam2Manager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (useSAM2Field != null && sam2ManagerField != null)
            {
                useSAM2Field.SetValue(asyncManager, true);
                sam2ManagerField.SetValue(asyncManager, sam2Manager);
                Debug.Log("✅ AsyncSegmentationManager настроен для использования SAM2");
            }
        }
    }

    /// <summary>
    /// Переключение между SAM2 и стандартными моделями
    /// </summary>
    [ContextMenu("Toggle SAM2 Usage")]
    public void ToggleSAM2Usage()
    {
        if (asyncManager != null)
        {
            var asyncType = typeof(AsyncSegmentationManager);
            var useSAM2Field = asyncType.GetField("useSAM2Models", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (useSAM2Field != null)
            {
                bool currentValue = (bool)useSAM2Field.GetValue(asyncManager);
                useSAM2Field.SetValue(asyncManager, !currentValue);
                
                Debug.Log($"SAM2 {(!currentValue ? "включен" : "выключен")}");
            }
        }
    }

    /// <summary>
    /// Диагностика состояния SAM2
    /// </summary>
    [ContextMenu("Diagnose SAM2 Setup")]
    public void DiagnoseSAM2Setup()
    {
        Debug.Log("=== SAM2 ДИАГНОСТИКА ===");
        Debug.Log($"Модели найдены: {modelsFound}");
        Debug.Log($"Настройка завершена: {setupCompleted}");
        Debug.Log($"SAM2Manager: {(sam2Manager != null ? "✅" : "❌")}");
        Debug.Log($"AsyncManager: {(asyncManager != null ? "✅" : "❌")}");
        Debug.Log($"Encoder Model: {(encoderModel != null ? "✅" : "❌")}");
        Debug.Log($"Decoder Model: {(decoderModel != null ? "✅" : "❌")}");
        
        if (sam2Manager != null)
        {
            Debug.Log($"SAM2Manager инициализирован: {sam2Manager.IsInitialized}");
            Debug.Log($"SAM2Manager обрабатывает: {sam2Manager.IsProcessing}");
        }
    }
}

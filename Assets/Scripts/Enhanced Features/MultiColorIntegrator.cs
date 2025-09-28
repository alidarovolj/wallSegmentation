using UnityEngine;
using System.Collections;

/// <summary>
/// Автоматически интегрирует систему многоцветной покраски в существующий проект
/// Настраивает все необходимые компоненты и связи
/// </summary>
public class MultiColorIntegrator : MonoBehaviour
{
    [Header("Integration Settings")]
    [SerializeField] private bool autoSetup = true;
    [SerializeField] private bool createUI = true;
    [SerializeField] private bool enableOnStart = true;
    
    private MultiColorPaintManager paintManager;
    private MultiColorPaintUI paintUI;
    private AsyncSegmentationManager segmentationManager;
    private ARWallPresenter wallPresenter;
    
    void Start()
    {
        if (autoSetup)
        {
            StartCoroutine(SetupMultiColorSystem());
        }
    }
    
    IEnumerator SetupMultiColorSystem()
    {
        Debug.Log("[MultiColorIntegrator] 🚀 Начинаем интеграцию многоцветной системы...");
        
        // Подождать пока основные системы инициализируются
        yield return new WaitForSeconds(1f);
        
        // Найти существующие компоненты
        FindExistingComponents();
        
        // Создать MultiColorPaintManager если нужно
        SetupPaintManager();
        
        // Создать UI если нужно
        if (createUI)
        {
            SetupPaintUI();
        }
        
        // Настроить интеграцию
        ConfigureIntegration();
        
        // Включить многоцветный режим если нужно
        if (enableOnStart)
        {
            EnableMultiColorMode();
        }
        
        Debug.Log("[MultiColorIntegrator] ✅ Интеграция многоцветной системы завершена!");
    }
    
    void FindExistingComponents()
    {
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        wallPresenter = FindObjectOfType<ARWallPresenter>();
        paintManager = FindObjectOfType<MultiColorPaintManager>();
        paintUI = FindObjectOfType<MultiColorPaintUI>();
        
        Debug.Log($"[MultiColorIntegrator] 🔍 Найдены компоненты:");
        Debug.Log($"   AsyncSegmentationManager: {(segmentationManager != null ? "✅" : "❌")}");
        Debug.Log($"   ARWallPresenter: {(wallPresenter != null ? "✅" : "❌")}");
        Debug.Log($"   MultiColorPaintManager: {(paintManager != null ? "✅" : "❌")}");
        Debug.Log($"   MultiColorPaintUI: {(paintUI != null ? "✅" : "❌")}");
    }
    
    void SetupPaintManager()
    {
        if (paintManager == null)
        {
            // Создать новый GameObject для MultiColorPaintManager
            GameObject managerGO = new GameObject("MultiColorPaintManager");
            paintManager = managerGO.AddComponent<MultiColorPaintManager>();
            
            Debug.Log("[MultiColorIntegrator] ✅ Создан MultiColorPaintManager");
        }
        
        // Настроить ссылки через рефлексию (так как поля private)
        SetPrivateField(paintManager, "segmentationManager", segmentationManager);
        SetPrivateField(paintManager, "wallPresenter", wallPresenter);
        
        var duluxIntegration = FindObjectOfType<DuluxVisualizerIntegration>();
        if (duluxIntegration != null)
        {
            SetPrivateField(paintManager, "duluxIntegration", duluxIntegration);
        }
    }
    
    void SetupPaintUI()
    {
        if (paintUI == null && createUI)
        {
            // Создать новый GameObject для UI
            GameObject uiGO = new GameObject("MultiColorPaintUI");
            paintUI = uiGO.AddComponent<MultiColorPaintUI>();
            
            Debug.Log("[MultiColorIntegrator] ✅ Создан MultiColorPaintUI");
        }
    }
    
    void ConfigureIntegration()
    {
        if (segmentationManager != null)
        {
            // Включить режим показа всех классов
            segmentationManager.ShowAllClassesColored();
            
            Debug.Log("[MultiColorIntegrator] 🔧 AsyncSegmentationManager настроен для многоцветного режима");
        }
        
        if (wallPresenter != null)
        {
            // Включить режим всех классов в ARWallPresenter
            wallPresenter.SetAllClassesMode();
            
            Debug.Log("[MultiColorIntegrator] 🔧 ARWallPresenter настроен для многоцветного режима");
        }
    }
    
    void EnableMultiColorMode()
    {
        if (paintManager != null)
        {
            paintManager.EnableMultiColorMode();
            Debug.Log("[MultiColorIntegrator] 🌈 Многоцветный режим включен");
        }
    }
    
    void SetPrivateField(object obj, string fieldName, object value)
    {
        try
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(obj, value);
                Debug.Log($"[MultiColorIntegrator] 🔗 Установлено поле {fieldName}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MultiColorIntegrator] ⚠️ Не удалось установить поле {fieldName}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Ручная настройка системы
    /// </summary>
    [ContextMenu("Setup Multi-Color System")]
    public void ManualSetup()
    {
        StartCoroutine(SetupMultiColorSystem());
    }
    
    /// <summary>
    /// Включить многоцветный режим
    /// </summary>
    [ContextMenu("Enable Multi-Color Mode")]
    public void ManualEnableMultiColor()
    {
        EnableMultiColorMode();
    }
    
    /// <summary>
    /// Тест многоцветной покраски
    /// </summary>
    [ContextMenu("Test Multi-Color Painting")]
    public void TestMultiColorPainting()
    {
        if (paintManager == null)
        {
            Debug.LogWarning("[MultiColorIntegrator] ⚠️ MultiColorPaintManager не найден!");
            return;
        }
        
        StartCoroutine(TestColorSequence());
    }
    
    IEnumerator TestColorSequence()
    {
        Debug.Log("[MultiColorIntegrator] 🧪 Начинаем тест многоцветной покраски...");
        
        // Тест 1: Покрасить стены в белый
        paintManager.PaintClass(0, Color.white);
        yield return new WaitForSeconds(2f);
        
        // Тест 2: Покрасить пол в коричневый
        paintManager.PaintClass(3, new Color(0.8f, 0.6f, 0.4f));
        yield return new WaitForSeconds(2f);
        
        // Тест 3: Покрасить потолок в кремовый
        paintManager.PaintClass(5, new Color(0.95f, 0.95f, 0.9f));
        yield return new WaitForSeconds(2f);
        
        // Тест 4: Включить многоцветный режим
        paintManager.EnableMultiColorMode();
        yield return new WaitForSeconds(2f);
        
        Debug.Log("[MultiColorIntegrator] ✅ Тест многоцветной покраски завершен!");
    }
    
    /// <summary>
    /// Получить статус интеграции
    /// </summary>
    public bool IsIntegrationComplete()
    {
        return paintManager != null && 
               segmentationManager != null && 
               wallPresenter != null;
    }
    
    /// <summary>
    /// Получить MultiColorPaintManager
    /// </summary>
    public MultiColorPaintManager GetPaintManager()
    {
        return paintManager;
    }
}
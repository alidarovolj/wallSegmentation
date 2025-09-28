using UnityEngine;
using System.Collections;

/// <summary>
/// Простая настройка многоцветной покраски без сложных зависимостей
/// Просто добавьте этот компонент в сцену и всё заработает!
/// </summary>
public class EasyMultiColorSetup : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool createUI = true;
    [SerializeField] private bool showDebugInfo = true;
    
    [Header("Статус")]
    [SerializeField] private bool isSetupComplete = false;
    
    private AsyncSegmentationManager segmentationManager;
    private SimpleMultiColorUI colorUI;
    
    void Start()
    {
        if (autoStart)
        {
            StartCoroutine(SetupMultiColorSystem());
        }
    }
    
    IEnumerator SetupMultiColorSystem()
    {
        Debug.Log("[EasyMultiColorSetup] 🚀 Начинаем настройку многоцветной системы...");
        
        // Подождать пока основные системы инициализируются
        yield return new WaitForSeconds(2f);
        
        // Найти AsyncSegmentationManager
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager == null)
        {
            Debug.LogError("[EasyMultiColorSetup] ❌ AsyncSegmentationManager не найден в сцене!");
            yield break;
        }
        
        Debug.Log("[EasyMultiColorSetup] ✅ AsyncSegmentationManager найден");
        
        // Настроить для многоцветного режима
        ConfigureSegmentationManager();
        
        // Создать UI если нужно
        if (createUI)
        {
            CreateMultiColorUI();
        }
        
        // Запустить демонстрацию
        yield return StartCoroutine(DemoMultiColorPainting());
        
        isSetupComplete = true;
        Debug.Log("[EasyMultiColorSetup] 🎉 Настройка многоцветной системы завершена!");
    }
    
    void ConfigureSegmentationManager()
    {
        if (segmentationManager == null) return;
        
        // Включить многоцветный режим
        segmentationManager.ShowAllClassesColored();
        
        Debug.Log("[EasyMultiColorSetup] 🔧 AsyncSegmentationManager настроен для многоцветного режима");
    }
    
    void CreateMultiColorUI()
    {
        // Проверить, есть ли уже UI
        colorUI = FindObjectOfType<SimpleMultiColorUI>();
        if (colorUI == null)
        {
            GameObject uiGO = new GameObject("SimpleMultiColorUI");
            colorUI = uiGO.AddComponent<SimpleMultiColorUI>();
            Debug.Log("[EasyMultiColorSetup] ✅ UI создан");
        }
        else
        {
            Debug.Log("[EasyMultiColorSetup] ✅ UI уже существует");
        }
    }
    
    IEnumerator DemoMultiColorPainting()
    {
        if (segmentationManager == null) yield break;
        
        Debug.Log("[EasyMultiColorSetup] 🎨 Демонстрация многоцветной покраски...");
        
        // Демо 1: Многоцветный режим
        Debug.Log("[EasyMultiColorSetup] 🌈 Шаг 1: Многоцветный режим");
        segmentationManager.ShowAllClassesColored();
        yield return new WaitForSeconds(3f);
        
        // Демо 2: Синие стены
        Debug.Log("[EasyMultiColorSetup] 🔵 Шаг 2: Синие стены");
        segmentationManager.SetSelectedClass(0);
        segmentationManager.SetPaintColor(Color.blue);
        yield return new WaitForSeconds(2f);
        
        // Демо 3: Коричневый пол
        Debug.Log("[EasyMultiColorSetup] 🟫 Шаг 3: Коричневый пол");
        segmentationManager.SetSelectedClass(3);
        segmentationManager.SetPaintColor(new Color(0.6f, 0.4f, 0.2f));
        yield return new WaitForSeconds(2f);
        
        // Демо 4: Кремовый потолок
        Debug.Log("[EasyMultiColorSetup] ⬜ Шаг 4: Кремовый потолок");
        segmentationManager.SetSelectedClass(5);
        segmentationManager.SetPaintColor(new Color(0.95f, 0.95f, 0.9f));
        yield return new WaitForSeconds(2f);
        
        // Демо 5: Вернуться к многоцветному режиму
        Debug.Log("[EasyMultiColorSetup] 🌈 Шаг 5: Возврат к многоцветному режиму");
        segmentationManager.ShowAllClassesColored();
        
        Debug.Log("[EasyMultiColorSetup] ✅ Демонстрация завершена!");
        Debug.Log("[EasyMultiColorSetup] 🎉 Многоцветная покраска готова к использованию!");
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
    /// Быстрый тест многоцветной покраски
    /// </summary>
    [ContextMenu("Quick Color Test")]
    public void QuickColorTest()
    {
        if (segmentationManager == null)
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            
        if (segmentationManager != null)
        {
            StartCoroutine(QuickColorDemo());
        }
    }
    
    IEnumerator QuickColorDemo()
    {
        Debug.Log("[EasyMultiColorSetup] 🎲 Быстрый тест цветов...");
        
        // Случайные цвета для стен
        Color[] colors = {Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan};
        
        foreach (Color color in colors)
        {
            segmentationManager.SetSelectedClass(0); // Стены
            segmentationManager.SetPaintColor(color);
            Debug.Log($"[EasyMultiColorSetup] 🎨 Стены: {ColorUtility.ToHtmlStringRGB(color)}");
            yield return new WaitForSeconds(1f);
        }
        
        // Вернуться к многоцветному режиму
        segmentationManager.ShowAllClassesColored();
        Debug.Log("[EasyMultiColorSetup] 🌈 Тест завершен!");
    }
    
    /// <summary>
    /// Получить статус настройки
    /// </summary>
    public bool IsSetupComplete()
    {
        return isSetupComplete;
    }
    
    /// <summary>
    /// Получить ссылку на AsyncSegmentationManager
    /// </summary>
    public AsyncSegmentationManager GetSegmentationManager()
    {
        return segmentationManager;
    }
}
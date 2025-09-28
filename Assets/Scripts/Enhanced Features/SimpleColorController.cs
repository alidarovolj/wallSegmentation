using UnityEngine;
using System.Collections;

/// <summary>
/// Простой контроллер цветов без доступа к приватным методам
/// Решает проблему мерцания и нестабильных цветов
/// </summary>
public class SimpleColorController : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool debugMode = true;
    
    [Header("Тестовые цвета")]
    [SerializeField] private Color wallColor = Color.red;
    [SerializeField] private Color floorColor = new Color(0.6f, 0.4f, 0.2f);
    [SerializeField] private Color ceilingColor = new Color(0.95f, 0.95f, 0.9f);
    
    private AsyncSegmentationManager segmentationManager;
    private ARWallPresenter wallPresenter;
    
    void Start()
    {
        if (autoStart)
        {
            StartCoroutine(InitializeController());
        }
    }
    
    IEnumerator InitializeController()
    {
        // Подождать пока система инициализируется
        yield return new WaitForSeconds(3f);
        
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        wallPresenter = FindObjectOfType<ARWallPresenter>();
        
        if (segmentationManager == null)
        {
            Debug.LogError("[SimpleColorController] ❌ AsyncSegmentationManager не найден!");
            yield break;
        }
        
        Debug.Log("[SimpleColorController] ✅ Контроллер инициализирован");
        
        // Запустить демонстрацию
        StartCoroutine(ColorDemo());
    }
    
    IEnumerator ColorDemo()
    {
        Debug.Log("[SimpleColorController] 🎨 Начинаем демонстрацию цветов...");
        
        // Демо 1: Красные стены
        Debug.Log("[SimpleColorController] 🔴 Красные стены");
        SetWallColor(Color.red);
        yield return new WaitForSeconds(3f);
        
        // Демо 2: Синие стены
        Debug.Log("[SimpleColorController] 🔵 Синие стены");
        SetWallColor(Color.blue);
        yield return new WaitForSeconds(3f);
        
        // Демо 3: Зеленые стены
        Debug.Log("[SimpleColorController] 🟢 Зеленые стены");
        SetWallColor(Color.green);
        yield return new WaitForSeconds(3f);
        
        // Демо 4: Многоцветный режим
        Debug.Log("[SimpleColorController] 🌈 Многоцветный режим");
        EnableMultiColorMode();
        yield return new WaitForSeconds(3f);
        
        Debug.Log("[SimpleColorController] ✅ Демонстрация завершена!");
    }
    
    /// <summary>
    /// Установить цвет стен
    /// </summary>
    public void SetWallColor(Color color)
    {
        if (segmentationManager != null)
        {
            // Установить режим показа только стен
            segmentationManager.SetSelectedClass(0); // Стены
            segmentationManager.SetPaintColor(color);
            
            if (debugMode)
                Debug.Log($"[SimpleColorController] 🧱 Стены покрашены в {ColorUtility.ToHtmlStringRGB(color)}");
        }
        
        // Синхронизировать с ARWallPresenter
        if (wallPresenter != null)
        {
            wallPresenter.SetClassColor(0, color);
        }
    }
    
    /// <summary>
    /// Установить цвет пола
    /// </summary>
    public void SetFloorColor(Color color)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(3); // Пол
            segmentationManager.SetPaintColor(color);
            
            if (debugMode)
                Debug.Log($"[SimpleColorController] 🟫 Пол покрашен в {ColorUtility.ToHtmlStringRGB(color)}");
        }
        
        if (wallPresenter != null)
        {
            wallPresenter.SetClassColor(3, color);
        }
    }
    
    /// <summary>
    /// Установить цвет потолка
    /// </summary>
    public void SetCeilingColor(Color color)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(5); // Потолок
            segmentationManager.SetPaintColor(color);
            
            if (debugMode)
                Debug.Log($"[SimpleColorController] ⬜ Потолок покрашен в {ColorUtility.ToHtmlStringRGB(color)}");
        }
        
        if (wallPresenter != null)
        {
            wallPresenter.SetClassColor(5, color);
        }
    }
    
    /// <summary>
    /// Включить многоцветный режим
    /// </summary>
    public void EnableMultiColorMode()
    {
        if (segmentationManager != null)
        {
            segmentationManager.ShowAllClassesColored();
            
            if (debugMode)
                Debug.Log("[SimpleColorController] 🌈 Многоцветный режим включен");
        }
        
        if (wallPresenter != null)
        {
            wallPresenter.SetAllClassesMode();
        }
    }
    
    /// <summary>
    /// Показать только стены
    /// </summary>
    public void ShowWallsOnly()
    {
        if (segmentationManager != null)
        {
            segmentationManager.ShowOnlyWalls();
            
            if (debugMode)
                Debug.Log("[SimpleColorController] 🧱 Показываем только стены");
        }
    }
    
    // Методы для контекстного меню
    
    [ContextMenu("🔴 Red Walls")]
    public void TestRedWalls()
    {
        SetWallColor(Color.red);
    }
    
    [ContextMenu("🔵 Blue Walls")]
    public void TestBlueWalls()
    {
        SetWallColor(Color.blue);
    }
    
    [ContextMenu("🟢 Green Walls")]
    public void TestGreenWalls()
    {
        SetWallColor(Color.green);
    }
    
    [ContextMenu("⚪ White Walls")]
    public void TestWhiteWalls()
    {
        SetWallColor(Color.white);
    }
    
    [ContextMenu("🟫 Brown Floor")]
    public void TestBrownFloor()
    {
        SetFloorColor(new Color(0.6f, 0.4f, 0.2f));
    }
    
    [ContextMenu("🌈 Multi-Color Mode")]
    public void TestMultiColorMode()
    {
        EnableMultiColorMode();
    }
    
    [ContextMenu("🧱 Walls Only")]
    public void TestWallsOnly()
    {
        ShowWallsOnly();
    }
    
    /// <summary>
    /// Установить произвольный цвет для класса
    /// </summary>
    public void SetClassColor(int classId, Color color)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(classId);
            segmentationManager.SetPaintColor(color);
            
            if (debugMode)
                Debug.Log($"[SimpleColorController] 🎨 Класс {classId} покрашен в {ColorUtility.ToHtmlStringRGB(color)}");
        }
        
        if (wallPresenter != null)
        {
            wallPresenter.SetClassColor(classId, color);
        }
    }
    
    /// <summary>
    /// Получить ссылку на AsyncSegmentationManager
    /// </summary>
    public AsyncSegmentationManager GetSegmentationManager()
    {
        return segmentationManager;
    }
}
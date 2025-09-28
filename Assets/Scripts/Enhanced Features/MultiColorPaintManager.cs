using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Управляет многоцветной покраской разных элементов маски сегментации
/// Позволяет назначать разные цвета для стен, полов, потолков и других объектов
/// </summary>
public class MultiColorPaintManager : MonoBehaviour
{
    [Header("Multi-Color Paint System")]
    [SerializeField] private AsyncSegmentationManager segmentationManager;
    [SerializeField] private ARWallPresenter wallPresenter;
    [SerializeField] private DuluxVisualizerCore duluxCore;
    
    [Header("Class Colors")]
    [SerializeField] private List<ClassColorPair> classColors = new List<ClassColorPair>();
    
    [Header("Presets")]
    [SerializeField] private ColorPreset[] colorPresets;
    
    [Header("Settings")]
    [SerializeField] private bool enableMultiColorMode = true;
    [SerializeField] private float opacity = 0.7f;
    [SerializeField] private bool showOtherClassesTransparent = true;
    
    // Словарь для быстрого доступа к цветам классов
    private Dictionary<int, Color> classColorDict = new Dictionary<int, Color>();
    
    // События
    public System.Action<int, Color> OnClassColorChanged;
    public System.Action<bool> OnMultiColorModeChanged;
    
    [System.Serializable]
    public class ClassColorPair
    {
        public int classId;
        public string className;
        public Color color;
        public bool enabled = true;
        
        public ClassColorPair(int id, string name, Color col)
        {
            classId = id;
            className = name;
            color = col;
        }
    }
    
    [System.Serializable]
    public class ColorPreset
    {
        public string presetName;
        public ClassColorPair[] colors;
    }
    
    void Start()
    {
        InitializeSystem();
        SetupDefaultColors();
    }
    
    void InitializeSystem()
    {
        // Найти компоненты если не назначены
        if (segmentationManager == null)
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            
        if (wallPresenter == null)
            wallPresenter = FindObjectOfType<ARWallPresenter>();
            
        if (duluxCore == null)
            duluxCore = FindObjectOfType<DuluxVisualizerCore>();
        
        // Создать словарь цветов
        UpdateColorDictionary();
        
        Debug.Log("[MultiColorPaintManager] 🎨 Система многоцветной покраски инициализирована");
    }
    
    void SetupDefaultColors()
    {
        if (classColors.Count == 0)
        {
            // Добавляем основные классы с красивыми цветами
            classColors.Add(new ClassColorPair(0, "Стены", new Color(0.9f, 0.9f, 0.9f))); // Белый
            classColors.Add(new ClassColorPair(3, "Пол", new Color(0.8f, 0.6f, 0.4f)));    // Коричневый
            classColors.Add(new ClassColorPair(5, "Потолок", new Color(0.95f, 0.95f, 0.9f))); // Кремовый
            classColors.Add(new ClassColorPair(14, "Дверь", new Color(0.6f, 0.4f, 0.2f)));  // Темно-коричневый
            classColors.Add(new ClassColorPair(8, "Окно", new Color(0.7f, 0.9f, 1.0f)));    // Голубой
            classColors.Add(new ClassColorPair(18, "Шторы", new Color(0.8f, 0.2f, 0.2f)));  // Красный
            classColors.Add(new ClassColorPair(15, "Стол", new Color(0.5f, 0.3f, 0.1f)));   // Дерево
            classColors.Add(new ClassColorPair(19, "Стул", new Color(0.2f, 0.4f, 0.8f)));   // Синий
            classColors.Add(new ClassColorPair(23, "Диван", new Color(0.3f, 0.6f, 0.3f)));  // Зеленый
            
            UpdateColorDictionary();
        }
    }
    
    void UpdateColorDictionary()
    {
        classColorDict.Clear();
        foreach (var colorPair in classColors)
        {
            if (colorPair.enabled)
                classColorDict[colorPair.classId] = colorPair.color;
        }
    }
    
    /// <summary>
    /// Включить многоцветный режим - каждый класс своим цветом
    /// </summary>
    public void EnableMultiColorMode()
    {
        enableMultiColorMode = true;
        
        if (segmentationManager != null)
        {
            segmentationManager.ShowAllClassesColored();
            segmentationManager.SetVisualizationOpacity(opacity);
        }
        
        if (wallPresenter != null)
        {
            wallPresenter.SetAllClassesMode();
        }
        
        OnMultiColorModeChanged?.Invoke(true);
        Debug.Log("[MultiColorPaintManager] 🌈 Включен многоцветный режим");
    }
    
    /// <summary>
    /// Покрасить конкретный класс определенным цветом
    /// </summary>
    public void PaintClass(int classId, Color color)
    {
        // Обновить в списке
        var existingPair = classColors.FirstOrDefault(c => c.classId == classId);
        if (existingPair != null)
        {
            existingPair.color = color;
        }
        else
        {
            classColors.Add(new ClassColorPair(classId, $"Класс {classId}", color));
        }
        
        // Обновить словарь
        classColorDict[classId] = color;
        
        // Применить к системам
        if (wallPresenter != null)
        {
            wallPresenter.SetClassColor(classId, color);
        }
        
        if (segmentationManager != null)
        {
            segmentationManager.SetPaintColor(color);
            segmentationManager.SetSelectedClass(classId);
        }
        
        OnClassColorChanged?.Invoke(classId, color);
        Debug.Log($"[MultiColorPaintManager] 🎨 Класс {classId} покрашен в {ColorUtility.ToHtmlStringRGB(color)}");
    }
    
    /// <summary>
    /// Покрасить только стены
    /// </summary>
    public void PaintWallsOnly(Color color)
    {
        PaintClass(0, color);
        
        if (segmentationManager != null)
        {
            segmentationManager.ShowOnlyWalls();
        }
        
        Debug.Log($"[MultiColorPaintManager] 🧱 Стены покрашены в {ColorUtility.ToHtmlStringRGB(color)}");
    }
    
    /// <summary>
    /// Применить цветовой пресет
    /// </summary>
    public void ApplyColorPreset(string presetName)
    {
        var preset = colorPresets?.FirstOrDefault(p => p.presetName == presetName);
        if (preset == null)
        {
            Debug.LogWarning($"[MultiColorPaintManager] ⚠️ Пресет '{presetName}' не найден");
            return;
        }
        
        // Очистить текущие цвета
        classColors.Clear();
        classColorDict.Clear();
        
        // Применить пресет
        foreach (var colorPair in preset.colors)
        {
            classColors.Add(new ClassColorPair(colorPair.classId, colorPair.className, colorPair.color));
            classColorDict[colorPair.classId] = colorPair.color;
            
            if (wallPresenter != null)
            {
                wallPresenter.SetClassColor(colorPair.classId, colorPair.color);
            }
        }
        
        EnableMultiColorMode();
        Debug.Log($"[MultiColorPaintManager] 🎨 Применен пресет '{presetName}'");
    }
    
    /// <summary>
    /// Получить цвет класса
    /// </summary>
    public Color GetClassColor(int classId)
    {
        return classColorDict.TryGetValue(classId, out Color color) ? color : Color.white;
    }
    
    /// <summary>
    /// Получить все активные цвета классов
    /// </summary>
    public Dictionary<int, Color> GetAllClassColors()
    {
        return new Dictionary<int, Color>(classColorDict);
    }
    
    /// <summary>
    /// Сбросить все цвета к умолчанию
    /// </summary>
    public void ResetToDefaults()
    {
        classColors.Clear();
        classColorDict.Clear();
        SetupDefaultColors();
        
        if (segmentationManager != null)
        {
            segmentationManager.ResetCustomColors();
        }
        
        Debug.Log("[MultiColorPaintManager] 🔄 Цвета сброшены к умолчанию");
    }
    
    /// <summary>
    /// Установить прозрачность для всех классов
    /// </summary>
    public void SetOpacity(float newOpacity)
    {
        opacity = Mathf.Clamp01(newOpacity);
        
        if (segmentationManager != null)
        {
            segmentationManager.SetVisualizationOpacity(opacity);
        }
        
        Debug.Log($"[MultiColorPaintManager] 🔆 Прозрачность установлена: {opacity:F2}");
    }
    
    /// <summary>
    /// Переключить видимость класса
    /// </summary>
    public void ToggleClassVisibility(int classId)
    {
        var colorPair = classColors.FirstOrDefault(c => c.classId == classId);
        if (colorPair != null)
        {
            colorPair.enabled = !colorPair.enabled;
            UpdateColorDictionary();
            
            Debug.Log($"[MultiColorPaintManager] 👁️ Класс {classId} {(colorPair.enabled ? "показан" : "скрыт")}");
        }
    }
    
    /// <summary>
    /// Создать пресет из текущих настроек
    /// </summary>
    public ColorPreset CreatePresetFromCurrent(string presetName)
    {
        var preset = new ColorPreset
        {
            presetName = presetName,
            colors = classColors.ToArray()
        };
        
        Debug.Log($"[MultiColorPaintManager] 💾 Создан пресет '{presetName}' с {classColors.Count} цветами");
        return preset;
    }
    
    // Методы для интеграции с Flutter
    
    /// <summary>
    /// [FLUTTER] Установить цвет класса из Flutter
    /// </summary>
    public void SetClassColorFromFlutter(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<ClassColorData>(json);
            if (ColorUtility.TryParseHtmlString(data.color, out Color color))
            {
                PaintClass(data.classId, color);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MultiColorPaintManager] ❌ Ошибка парсинга JSON: {e.Message}");
        }
    }
    
    /// <summary>
    /// [FLUTTER] Включить многоцветный режим из Flutter
    /// </summary>
    public void EnableMultiColorFromFlutter(string message = "")
    {
        EnableMultiColorMode();
        
        // Отправить список доступных классов во Flutter
        SendClassListToFlutter();
    }
    
    void SendClassListToFlutter()
    {
        var classList = new ClassListData
        {
            classes = classColors.Select(c => new ClassInfo 
            { 
                id = c.classId, 
                name = c.className, 
                color = "#" + ColorUtility.ToHtmlStringRGB(c.color),
                enabled = c.enabled
            }).ToArray()
        };
        
        string json = JsonUtility.ToJson(classList);
        
        // Отправить список классов через FlutterUnityManager
        var flutterManager = FindObjectOfType<FlutterUnityManager>();
        if (flutterManager != null)
        {
            flutterManager.SendMessage("onClassList", json);
        }
    }
    
    [System.Serializable]
    public class ClassColorData
    {
        public int classId;
        public string color;
    }
    
    [System.Serializable]
    public class ClassListData
    {
        public ClassInfo[] classes;
    }
    
    [System.Serializable]
    public class ClassInfo
    {
        public int id;
        public string name;
        public string color;
        public bool enabled;
    }
}
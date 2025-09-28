using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Простой UI для тестирования многоцветной покраски
/// Создает кнопки для быстрого изменения цветов разных классов
/// </summary>
public class MultiColorPaintUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private Slider opacitySlider;
    [SerializeField] private Toggle multiColorToggle;
    [SerializeField] private Text statusText;
    
    [Header("Quick Colors")]
    [SerializeField] private Color[] quickColors = new Color[]
    {
        Color.white, Color.red, Color.green, Color.blue,
        Color.yellow, Color.magenta, Color.cyan, new Color(0.8f, 0.6f, 0.4f)
    };
    
    private MultiColorPaintManager paintManager;
    private List<Button> classButtons = new List<Button>();
    private int selectedClassId = 0;
    
    void Start()
    {
        InitializeUI();
    }
    
    void InitializeUI()
    {
        // Найти MultiColorPaintManager
        paintManager = FindObjectOfType<MultiColorPaintManager>();
        if (paintManager == null)
        {
            Debug.LogWarning("[MultiColorPaintUI] ⚠️ MultiColorPaintManager не найден, будем использовать AsyncSegmentationManager напрямую");
        }
        else
        {
            Debug.Log("[MultiColorPaintUI] ✅ MultiColorPaintManager найден");
        }
        
        // Создать UI если компоненты не назначены
        CreateUIIfNeeded();
        
        // Настроить события
        SetupEvents();
        
        // Создать кнопки классов
        CreateClassButtons();
        
        // Создать кнопки быстрых цветов
        CreateQuickColorButtons();
        
        Debug.Log("[MultiColorPaintUI] 🎨 UI многоцветной покраски инициализирован");
    }
    
    void CreateUIIfNeeded()
    {
        if (buttonContainer == null)
        {
            // Создать простой Canvas если его нет
            GameObject canvasGO = GameObject.Find("MultiColorCanvas");
            if (canvasGO == null)
            {
                canvasGO = new GameObject("MultiColorCanvas");
                Canvas canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
                
                // Создать контейнер для кнопок
                GameObject containerGO = new GameObject("ButtonContainer");
                containerGO.transform.SetParent(canvasGO.transform);
                
                RectTransform containerRect = containerGO.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0, 0.8f);
                containerRect.anchorMax = new Vector2(1, 1);
                containerRect.offsetMin = Vector2.zero;
                containerRect.offsetMax = Vector2.zero;
                
                // Добавить GridLayoutGroup
                GridLayoutGroup grid = containerGO.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(100, 40);
                grid.spacing = new Vector2(5, 5);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                
                buttonContainer = containerRect;
            }
        }
        
        // Создать простую кнопку-префаб если нет
        if (buttonPrefab == null)
        {
            CreateSimpleButtonPrefab();
        }
    }
    
    void CreateSimpleButtonPrefab()
    {
        GameObject buttonGO = new GameObject("ButtonPrefab");
        buttonGO.AddComponent<RectTransform>();
        
        // Добавить Image
        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = Color.white;
        
        // Добавить Button
        Button button = buttonGO.AddComponent<Button>();
        
        // Добавить Text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text buttonText = textGO.AddComponent<Text>();
        buttonText.text = "Button";
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 12;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.black;
        
        buttonPrefab = button;
        
        // Сделать префаб неактивным
        buttonGO.SetActive(false);
    }
    
    void SetupEvents()
    {
        if (opacitySlider != null)
        {
            opacitySlider.onValueChanged.AddListener(OnOpacityChanged);
            opacitySlider.value = 0.7f;
        }
        
        if (multiColorToggle != null)
        {
            multiColorToggle.onValueChanged.AddListener(OnMultiColorToggle);
            multiColorToggle.isOn = true;
        }
        
        if (paintManager != null)
        {
            paintManager.OnClassColorChanged += OnClassColorChanged;
            paintManager.OnMultiColorModeChanged += OnMultiColorModeChanged;
        }
    }
    
    void CreateClassButtons()
    {
        if (buttonContainer == null || buttonPrefab == null) return;
        
        // Основные классы для быстрого доступа
        var mainClasses = new Dictionary<int, string>
        {
            {0, "Стены"},
            {3, "Пол"},
            {5, "Потолок"},
            {14, "Дверь"},
            {8, "Окно"},
            {15, "Стол"},
            {19, "Стул"},
            {23, "Диван"}
        };
        
        foreach (var kvp in mainClasses)
        {
            CreateClassButton(kvp.Key, kvp.Value);
        }
    }
    
    void CreateClassButton(int classId, string className)
    {
        if (buttonPrefab == null) return;
        
        Button button = Instantiate(buttonPrefab, buttonContainer);
        button.gameObject.SetActive(true);
        button.name = $"ClassButton_{classId}";
        
        // Настроить текст
        Text buttonText = button.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = className;
        }
        
        // Настроить цвет кнопки
        Color classColor = paintManager.GetClassColor(classId);
        button.GetComponent<Image>().color = classColor;
        
        // Добавить обработчик клика
        button.onClick.AddListener(() => SelectClass(classId));
        
        classButtons.Add(button);
    }
    
    void CreateQuickColorButtons()
    {
        if (buttonContainer == null || buttonPrefab == null) return;
        
        // Создать разделитель
        CreateSeparator();
        
        // Создать кнопки быстрых цветов
        for (int i = 0; i < quickColors.Length; i++)
        {
            Color color = quickColors[i];
            CreateQuickColorButton(color, i);
        }
        
        // Кнопка "Многоцветный режим"
        CreateMultiColorButton();
    }
    
    void CreateSeparator()
    {
        GameObject separatorGO = new GameObject("Separator");
        separatorGO.transform.SetParent(buttonContainer);
        
        RectTransform rect = separatorGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100, 20);
        
        Text text = separatorGO.AddComponent<Text>();
        text.text = "Цвета:";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 10;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }
    
    void CreateQuickColorButton(Color color, int index)
    {
        Button button = Instantiate(buttonPrefab, buttonContainer);
        button.gameObject.SetActive(true);
        button.name = $"ColorButton_{index}";
        
        // Настроить цвет
        button.GetComponent<Image>().color = color;
        
        // Убрать текст или сделать его маленьким
        Text buttonText = button.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = "";
        }
        
        // Добавить обработчик клика
        button.onClick.AddListener(() => ApplyColorToSelectedClass(color));
    }
    
    void CreateMultiColorButton()
    {
        Button button = Instantiate(buttonPrefab, buttonContainer);
        button.gameObject.SetActive(true);
        button.name = "MultiColorButton";
        
        // Настроить внешний вид
        button.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.2f);
        
        Text buttonText = button.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = "Все цвета";
            buttonText.fontSize = 10;
        }
        
        // Добавить обработчик клика
        button.onClick.AddListener(() => paintManager.EnableMultiColorMode());
    }
    
    void SelectClass(int classId)
    {
        selectedClassId = classId;
        UpdateStatusText($"Выбран: Класс {classId}");
        
        // Выделить кнопку
        UpdateClassButtonSelection();
    }
    
    void ApplyColorToSelectedClass(Color color)
    {
        Debug.Log($"[MultiColorPaintUI] 🎨 Применяем цвет: Класс {selectedClassId} = {ColorUtility.ToHtmlStringRGB(color)}");
        
        // Попробовать через стабилизатор (приоритет)
        var stabilizer = FindObjectOfType<MaskStabilizer>();
        if (stabilizer != null)
        {
            stabilizer.SetColorStable(selectedClassId, color);
            UpdateStatusText($"Класс {selectedClassId} покрашен в {ColorUtility.ToHtmlStringRGB(color)} (стабильно)");
            return;
        }
        
        // Попробовать через MultiColorPaintManager
        if (paintManager != null)
        {
            paintManager.PaintClass(selectedClassId, color);
            UpdateStatusText($"Класс {selectedClassId} покрашен в {ColorUtility.ToHtmlStringRGB(color)}");
            return;
        }
        
        // Если ничего не доступно, использовать AsyncSegmentationManager напрямую
        var segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(selectedClassId);
            segmentationManager.SetPaintColor(color);
            
            // Материал обновится автоматически через SetSelectedClass и SetPaintColor
            
            UpdateStatusText($"Класс {selectedClassId} покрашен в {ColorUtility.ToHtmlStringRGB(color)} (прямо)");
            Debug.Log($"[MultiColorPaintUI] 🎨 Прямая покраска: Класс {selectedClassId} = {ColorUtility.ToHtmlStringRGB(color)}");
        }
        else
        {
            UpdateStatusText("❌ Система покраски недоступна");
            Debug.LogError("[MultiColorPaintUI] ❌ Система покраски не найдена!");
        }
    }
    
    void UpdateClassButtonSelection()
    {
        // Сбросить выделение всех кнопок
        foreach (var button in classButtons)
        {
            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }
        
        // Выделить выбранную кнопку
        var selectedButton = classButtons.Find(b => b.name.EndsWith($"_{selectedClassId}"));
        if (selectedButton != null)
        {
            var outline = selectedButton.GetComponent<Outline>();
            if (outline == null)
            {
                outline = selectedButton.gameObject.AddComponent<Outline>();
            }
            outline.enabled = true;
            outline.effectColor = Color.yellow;
            outline.effectDistance = new Vector2(2, 2);
        }
    }
    
    void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        
        Debug.Log($"[MultiColorPaintUI] {message}");
    }
    
    // Event handlers
    
    void OnOpacityChanged(float value)
    {
        if (paintManager != null)
        {
            paintManager.SetOpacity(value);
        }
    }
    
    void OnMultiColorToggle(bool isOn)
    {
        if (paintManager != null)
        {
            if (isOn)
            {
                paintManager.EnableMultiColorMode();
            }
            else
            {
                paintManager.PaintWallsOnly(Color.white);
            }
        }
    }
    
    void OnClassColorChanged(int classId, Color color)
    {
        // Обновить цвет соответствующей кнопки
        var button = classButtons.Find(b => b.name.EndsWith($"_{classId}"));
        if (button != null)
        {
            button.GetComponent<Image>().color = color;
        }
    }
    
    void OnMultiColorModeChanged(bool enabled)
    {
        if (multiColorToggle != null && multiColorToggle.isOn != enabled)
        {
            multiColorToggle.isOn = enabled;
        }
    }
    
    void OnDestroy()
    {
        if (paintManager != null)
        {
            paintManager.OnClassColorChanged -= OnClassColorChanged;
            paintManager.OnMultiColorModeChanged -= OnMultiColorModeChanged;
        }
    }
}
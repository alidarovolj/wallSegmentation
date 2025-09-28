using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Простой UI для многоцветной покраски без зависимости от встроенных шрифтов
/// </summary>
public class SimpleMultiColorUI : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private bool createUI = true;
    [SerializeField] private bool showDebugInfo = true;
    
    private AsyncSegmentationManager segmentationManager;
    private Canvas uiCanvas;
    private GameObject buttonContainer;
    
    void Start()
    {
        if (createUI)
        {
            StartCoroutine(InitializeUI());
        }
    }
    
    IEnumerator InitializeUI()
    {
        // Подождать пока система инициализируется
        yield return new WaitForSeconds(1f);
        
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager == null)
        {
            Debug.LogError("[SimpleMultiColorUI] ❌ AsyncSegmentationManager не найден!");
            yield break;
        }
        
        CreateSimpleUI();
        Debug.Log("[SimpleMultiColorUI] ✅ UI создан успешно!");
    }
    
    void CreateSimpleUI()
    {
        // Создать Canvas
        GameObject canvasGO = new GameObject("MultiColorCanvas");
        uiCanvas = canvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Создать панель для кнопок
        CreateButtonPanel();
        
        // Создать кнопки
        CreateColorButtons();
    }
    
    void CreateButtonPanel()
    {
        GameObject panelGO = new GameObject("ButtonPanel");
        panelGO.transform.SetParent(uiCanvas.transform, false);
        
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0.8f);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Добавить фон панели
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.3f);
        
        // Создать контейнер для кнопок
        buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(panelGO.transform, false);
        
        RectTransform containerRect = buttonContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = new Vector2(10, 10);
        containerRect.offsetMax = new Vector2(-10, -10);
        
        // Добавить GridLayoutGroup
        GridLayoutGroup grid = buttonContainer.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(120, 50);
        grid.spacing = new Vector2(10, 10);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
    }
    
    void CreateColorButtons()
    {
        // Кнопки для основных функций
        CreateFunctionButton("🌈 Все цвета", () => EnableMultiColorMode());
        CreateFunctionButton("🧱 Только стены", () => ShowWallsOnly());
        CreateFunctionButton("🔵 Синие стены", () => PaintWalls(Color.blue));
        CreateFunctionButton("🟢 Зеленые стены", () => PaintWalls(Color.green));
        CreateFunctionButton("🔴 Красные стены", () => PaintWalls(Color.red));
        CreateFunctionButton("⚪ Белые стены", () => PaintWalls(Color.white));
        
        // Кнопки для разных элементов
        CreateFunctionButton("🟫 Коричневый пол", () => PaintFloor());
        CreateFunctionButton("⬜ Кремовый потолок", () => PaintCeiling());
        CreateFunctionButton("🎲 Случайные цвета", () => RandomColors());
    }
    
    void CreateFunctionButton(string text, System.Action onClick)
    {
        GameObject buttonGO = new GameObject($"Button_{text}");
        buttonGO.transform.SetParent(buttonContainer.transform, false);
        
        // Добавить Image для фона кнопки
        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Добавить Button компонент
        Button button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        
        // Настроить цвета кнопки
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        button.colors = colors;
        
        // Добавить текст (без шрифта - будет использован дефолтный)
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text buttonText = textGO.AddComponent<Text>();
        buttonText.text = text;
        buttonText.fontSize = 14;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        
        // Попробовать найти шрифт, если не найдется - будет дефолтный
        try
        {
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch
        {
            // Если шрифт не найден, Unity использует дефолтный
            if (showDebugInfo)
                Debug.Log($"[SimpleMultiColorUI] Используется дефолтный шрифт для кнопки: {text}");
        }
        
        // Добавить обработчик клика
        button.onClick.AddListener(() => {
            if (showDebugInfo)
                Debug.Log($"[SimpleMultiColorUI] Нажата кнопка: {text}");
            onClick?.Invoke();
        });
    }
    
    // Функции для кнопок
    
    void EnableMultiColorMode()
    {
        if (segmentationManager != null)
        {
            segmentationManager.ShowAllClassesColored();
            Debug.Log("[SimpleMultiColorUI] 🌈 Включен многоцветный режим");
        }
    }
    
    void ShowWallsOnly()
    {
        if (segmentationManager != null)
        {
            segmentationManager.ShowOnlyWalls();
            Debug.Log("[SimpleMultiColorUI] 🧱 Показываем только стены");
        }
    }
    
    void PaintWalls(Color color)
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(0); // Стены
            segmentationManager.SetPaintColor(color);
            Debug.Log($"[SimpleMultiColorUI] 🎨 Стены покрашены в {ColorUtility.ToHtmlStringRGB(color)}");
        }
    }
    
    void PaintFloor()
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(3); // Пол
            segmentationManager.SetPaintColor(new Color(0.6f, 0.4f, 0.2f)); // Коричневый
            Debug.Log("[SimpleMultiColorUI] 🟫 Пол покрашен в коричневый");
        }
    }
    
    void PaintCeiling()
    {
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(5); // Потолок
            segmentationManager.SetPaintColor(new Color(0.95f, 0.95f, 0.9f)); // Кремовый
            Debug.Log("[SimpleMultiColorUI] ⬜ Потолок покрашен в кремовый");
        }
    }
    
    void RandomColors()
    {
        if (segmentationManager != null)
        {
            StartCoroutine(ApplyRandomColors());
        }
    }
    
    IEnumerator ApplyRandomColors()
    {
        Debug.Log("[SimpleMultiColorUI] 🎲 Применяем случайные цвета...");
        
        // Классы для покраски
        int[] classes = {0, 3, 5}; // Стены, пол, потолок
        string[] names = {"стены", "пол", "потолок"};
        
        for (int i = 0; i < classes.Length; i++)
        {
            Color randomColor = new Color(
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f)
            );
            
            segmentationManager.SetSelectedClass(classes[i]);
            segmentationManager.SetPaintColor(randomColor);
            
            Debug.Log($"[SimpleMultiColorUI] 🎨 {names[i]} покрашены в {ColorUtility.ToHtmlStringRGB(randomColor)}");
            
            yield return new WaitForSeconds(0.5f);
        }
        
        // Включить многоцветный режим для показа всех цветов
        yield return new WaitForSeconds(1f);
        segmentationManager.ShowAllClassesColored();
        Debug.Log("[SimpleMultiColorUI] 🌈 Все случайные цвета применены!");
    }
    
    /// <summary>
    /// Показать/скрыть UI
    /// </summary>
    public void ToggleUI()
    {
        if (uiCanvas != null)
        {
            uiCanvas.gameObject.SetActive(!uiCanvas.gameObject.activeSelf);
        }
    }
    
    /// <summary>
    /// Ручное создание UI
    /// </summary>
    [ContextMenu("Create UI")]
    public void ManualCreateUI()
    {
        if (uiCanvas == null)
        {
            StartCoroutine(InitializeUI());
        }
    }
}
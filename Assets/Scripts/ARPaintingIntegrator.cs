using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Автоматический интегратор AR Painting системы
/// Настраивает сцену с одного клика для быстрого тестирования
/// </summary>
public class ARPaintingIntegrator : MonoBehaviour
{
      [Header("Интеграция")]
      [SerializeField] private bool autoSetupOnStart = true;
      [SerializeField] private bool createUIElements = true;
      [SerializeField] private bool enablePerformanceMonitoring = true;

      [Header("Prefab References (опционально)")]
      [SerializeField] private GameObject arSessionPrefab;
      [SerializeField] private GameObject arCameraPrefab;
      [SerializeField] private Material paintMaterial;

      // Ссылки на созданные компоненты
      private GameObject arSession;
      private GameObject arCamera;
      private Canvas mainCanvas;
      private AsyncSegmentationManager segmentationManager;
      private PerformanceMonitor performanceMonitor;
      private UIManager uiManager;

      void Start()
      {
            if (autoSetupOnStart)
            {
                  SetupARPaintingSystem();
            }
      }

      [ContextMenu("Setup AR Painting System")]
      public void SetupARPaintingSystem()
      {
            Debug.Log("🚀 Начинаем интеграцию AR Painting системы...");

            try
            {
                  // Шаг 1: Настройка AR Foundation
                  SetupARFoundation();

                  // Шаг 2: Настройка основных компонентов
                  SetupCoreComponents();

                  // Шаг 3: Создание UI
                  if (createUIElements)
                  {
                        SetupUI();
                  }

                  // Шаг 4: Настройка мониторинга производительности
                  if (enablePerformanceMonitoring)
                  {
                        SetupPerformanceMonitoring();
                  }

                  // Шаг 5: Связывание компонентов
                  LinkComponents();

                  Debug.Log("✅ AR Painting система успешно интегрирована!");

#if UNITY_EDITOR
                  EditorUtility.SetDirty(gameObject);
#endif
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка интеграции: {e.Message}");
            }
      }

      void SetupARFoundation()
      {
            Debug.Log("📱 Настройка AR Foundation...");

            // Ищем или создаем AR Session
            var existingSession = FindObjectOfType<ARSession>();
            if (existingSession == null)
            {
                  arSession = new GameObject("AR Session");
                  arSession.AddComponent<ARSession>();
                  Debug.Log("✅ AR Session создан");
            }
            else
            {
                  arSession = existingSession.gameObject;
                  Debug.Log("✅ Найден существующий AR Session");
            }

            // Ищем или создаем XR Origin  
            var existingSessionOrigin = FindObjectOfType<XROrigin>();
            if (existingSessionOrigin == null)
            {
                  var sessionOrigin = new GameObject("XR Origin");
                  var xrOrigin = sessionOrigin.AddComponent<XROrigin>();

                  // Создаем AR Camera
                  var cameraGO = new GameObject("AR Camera");
                  cameraGO.transform.SetParent(sessionOrigin.transform);

                  var camera = cameraGO.AddComponent<Camera>();
                  camera.clearFlags = CameraClearFlags.Color;
                  camera.backgroundColor = Color.black;
                  camera.nearClipPlane = 0.1f;
                  camera.farClipPlane = 20f;

                  cameraGO.AddComponent<ARCameraManager>();
                  cameraGO.AddComponent<ARCameraBackground>();

                  // Добавляем наши компоненты
                  cameraGO.AddComponent<CameraFeedCapture>();
                  cameraGO.AddComponent<SurfaceHighlighter>();

                  // Добавляем менеджер окклюзии для реалистичного перекрытия
                  var occlusionManager = cameraGO.AddComponent<AROcclusionManager>();
                  occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;

                  arCamera = cameraGO;

                  // Настройка XR Origin
                  xrOrigin.Camera = camera;

                  Debug.Log("✅ AR Session Origin и AR Camera созданы");
            }
            else
            {
                  arCamera = existingSessionOrigin.Camera.gameObject;

                  // Добавляем наши компоненты к существующей камере
                  if (arCamera.GetComponent<CameraFeedCapture>() == null)
                  {
                        arCamera.AddComponent<CameraFeedCapture>();
                  }

                  if (arCamera.GetComponent<SurfaceHighlighter>() == null)
                  {
                        arCamera.AddComponent<SurfaceHighlighter>();
                  }

                  // Добавляем менеджер окклюзии, если его нет
                  if (arCamera.GetComponent<AROcclusionManager>() == null)
                  {
                        var occlusionManager = arCamera.AddComponent<AROcclusionManager>();
                        occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
                        Debug.Log("✅ AR Occlusion Manager добавлен и настроен");
                  }

                  Debug.Log("✅ Найден существующий XR Origin");
            }

            // Добавляем AR Mesh Manager для детекции поверхностей
            var sessionOriginComponent = FindObjectOfType<XROrigin>();
            if (sessionOriginComponent.GetComponent<ARMeshManager>() == null)
            {
                  sessionOriginComponent.gameObject.AddComponent<ARMeshManager>();
                  Debug.Log("✅ AR Mesh Manager добавлен");
            }
      }

      void SetupCoreComponents()
      {
            Debug.Log("🔧 Настройка основных компонентов...");

            // Создаем главный GameObject для управления
            var coreManager = new GameObject("AR Painting Core");

            // Добавляем AsyncSegmentationManager
            segmentationManager = coreManager.AddComponent<AsyncSegmentationManager>();

            // Добавляем PaintManager
            var paintManager = coreManager.AddComponent<PaintManager>();

            // Добавляем CommandManager для Undo/Redo
            var commandManager = coreManager.AddComponent<CommandManager>();

            // Добавляем MemoryPoolManager
            var memoryPoolManager = coreManager.AddComponent<MemoryPoolManager>();

            Debug.Log("✅ Основные компоненты созданы");
      }

      void SetupUI()
      {
            Debug.Log("🎨 Создание UI элементов...");

            // Создаем главный Canvas
            var canvasGO = new GameObject("AR Painting UI");
            mainCanvas = canvasGO.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Добавляем UIManager
            uiManager = canvasGO.AddComponent<UIManager>();

            // Создаем основные UI элементы
            CreateColorPalette();
            CreateControlButtons();
            CreatePerformanceDisplay();
            CreateBlendModeDropdown();

            Debug.Log("✅ UI элементы созданы");
      }

      void CreateColorPalette()
      {
            // Создаем панель палитры
            var palettePanel = CreateUIPanel("Color Palette", mainCanvas.transform);
            var paletteRect = palettePanel.GetComponent<RectTransform>();
            paletteRect.anchorMin = new Vector2(0.02f, 0.7f);
            paletteRect.anchorMax = new Vector2(0.25f, 0.98f);
            paletteRect.offsetMin = Vector2.zero;
            paletteRect.offsetMax = Vector2.zero;

            // Создаем сетку для цветов
            var gridLayout = palettePanel.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(60, 60);
            gridLayout.spacing = new Vector2(10, 10);
            gridLayout.padding = new RectOffset(10, 10, 10, 10);

            // Предустановленные цвета
            Color[] colors = {
            Color.red, Color.green, Color.blue, Color.yellow,
            Color.cyan, Color.magenta, Color.white, new Color(0.8f, 0.4f, 0.2f),
            new Color(0.5f, 0.3f, 0.8f), new Color(0.2f, 0.8f, 0.4f)
        };

            foreach (var color in colors)
            {
                  CreateColorButton(color, palettePanel.transform);
            }
      }

      void CreateControlButtons()
      {
            // Создаем панель управления
            var controlPanel = CreateUIPanel("Control Panel", mainCanvas.transform);
            var controlRect = controlPanel.GetComponent<RectTransform>();
            controlRect.anchorMin = new Vector2(0.02f, 0.02f);
            controlRect.anchorMax = new Vector2(0.5f, 0.15f);
            controlRect.offsetMin = Vector2.zero;
            controlRect.offsetMax = Vector2.zero;

            var layout = controlPanel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(15, 15, 15, 15);
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Кнопка Clear
            var clearButton = CreateButton("Clear", controlPanel.transform);
            if (clearButton != null)
            {
                  clearButton.onClick.AddListener(uiManager.OnClearAllClicked);
            }

            // Кнопка Undo
            var undoButton = CreateButton("Undo", controlPanel.transform);
            if (undoButton != null)
            {
                  undoButton.onClick.AddListener(uiManager.OnUndoClicked);
            }

            // Кнопка Redo
            var redoButton = CreateButton("Redo", controlPanel.transform);
            if (redoButton != null)
            {
                  redoButton.onClick.AddListener(uiManager.OnRedoClicked);
            }

            // Присваиваем ссылки в UIManager
            if (uiManager != null)
            {
                  var uiManagerType = typeof(UIManager);
                  var clearField = uiManagerType.GetField("clearButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  var undoField = uiManagerType.GetField("undoButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  var redoField = uiManagerType.GetField("redoButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                  clearField?.SetValue(uiManager, clearButton);
                  undoField?.SetValue(uiManager, undoButton);
                  redoField?.SetValue(uiManager, redoButton);
            }
      }

      void CreateBlendModeDropdown()
      {
            // Создаем Dropdown для режимов смешивания
            var dropdownGO = new GameObject("Blend Mode Dropdown");
            dropdownGO.transform.SetParent(mainCanvas.transform);

            var dropdownRect = dropdownGO.AddComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0.7f, 0.85f);
            dropdownRect.anchorMax = new Vector2(0.98f, 0.95f);
            dropdownRect.offsetMin = Vector2.zero;
            dropdownRect.offsetMax = Vector2.zero;

            var dropdown = dropdownGO.AddComponent<TMP_Dropdown>();

            // Добавляем опции
            dropdown.options.Clear();
            dropdown.options.Add(new TMP_Dropdown.OptionData("Normal"));
            dropdown.options.Add(new TMP_Dropdown.OptionData("Multiply"));
            dropdown.options.Add(new TMP_Dropdown.OptionData("Overlay"));
            dropdown.options.Add(new TMP_Dropdown.OptionData("Soft Light"));

            // Присваиваем в UIManager
            if (uiManager != null)
            {
                  var uiManagerType = typeof(UIManager);
                  var dropdownField = uiManagerType.GetField("blendModeDropdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  dropdownField?.SetValue(uiManager, dropdown);
            }
      }

      void CreatePerformanceDisplay()
      {
            // Создаем панель производительности
            var perfPanel = CreateUIPanel("Performance Panel", mainCanvas.transform);
            var perfRect = perfPanel.GetComponent<RectTransform>();
            perfRect.anchorMin = new Vector2(0.7f, 0.7f);
            perfRect.anchorMax = new Vector2(0.98f, 0.82f);
            perfRect.offsetMin = Vector2.zero;
            perfRect.offsetMax = Vector2.zero;

            var layout = perfPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.padding = new RectOffset(10, 10, 5, 5);

            // FPS текст
            var fpsText = CreateText("FPS: --", perfPanel.transform);
            fpsText.color = Color.green;
            fpsText.fontSize = 16;

            // ML время
            var mlText = CreateText("ML: --ms", perfPanel.transform);
            mlText.color = Color.yellow;
            mlText.fontSize = 16;

            // Память
            var memText = CreateText("RAM: --MB", perfPanel.transform);
            memText.color = Color.cyan;
            memText.fontSize = 16;
      }

      void SetupPerformanceMonitoring()
      {
            Debug.Log("📊 Настройка мониторинга производительности...");

            var perfGO = new GameObject("Performance Monitor");
            performanceMonitor = perfGO.AddComponent<PerformanceMonitor>();

            // Найдем созданные UI элементы и свяжем с PerformanceMonitor
            var fpsText = GameObject.Find("FPS Text")?.GetComponent<TextMeshProUGUI>();
            var mlText = GameObject.Find("ML Text")?.GetComponent<TextMeshProUGUI>();
            var memText = GameObject.Find("Memory Text")?.GetComponent<TextMeshProUGUI>();

            if (performanceMonitor != null && fpsText != null)
            {
                  var perfType = typeof(PerformanceMonitor);
                  var fpsField = perfType.GetField("fpsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  var mlField = perfType.GetField("inferenceTimeText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  var memField = perfType.GetField("memoryText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                  fpsField?.SetValue(performanceMonitor, fpsText);
                  mlField?.SetValue(performanceMonitor, mlText);
                  memField?.SetValue(performanceMonitor, memText);
            }

            Debug.Log("✅ Мониторинг производительности настроен");
      }

      void LinkComponents()
      {
            Debug.Log("🔗 Связывание компонентов...");

            // Связываем AsyncSegmentationManager с другими компонентами
            if (segmentationManager != null)
            {
                  var arCameraManager = arCamera?.GetComponent<ARCameraManager>();
                  var paintManager = FindObjectOfType<PaintManager>();

                  if (arCameraManager != null)
                  {
                        var segType = typeof(AsyncSegmentationManager);
                        var cameraField = segType.GetField("arCameraManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        cameraField?.SetValue(segmentationManager, arCameraManager);

                        var paintField = segType.GetField("paintManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        paintField?.SetValue(segmentationManager, paintManager);
                  }
            }

            // Связываем UIManager с менеджерами
            if (uiManager != null)
            {
                  var paintManager = FindObjectOfType<PaintManager>();
                  var commandManager = FindObjectOfType<CommandManager>();

                  var uiType = typeof(UIManager);
                  var paintField = uiType.GetField("paintManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                  var segField = uiType.GetField("segmentationManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                  var cmdField = uiType.GetField("commandManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                  paintField?.SetValue(uiManager, paintManager);
                  segField?.SetValue(uiManager, segmentationManager);
                  cmdField?.SetValue(uiManager, commandManager);
            }

            // Связываем PerformanceMonitor с AsyncSegmentationManager
            if (performanceMonitor != null && segmentationManager != null)
            {
                  var perfType = typeof(PerformanceMonitor);
                  var segField = perfType.GetField("segmentationManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  segField?.SetValue(performanceMonitor, segmentationManager);
            }

            Debug.Log("✅ Компоненты связаны");
      }

      // Вспомогательные методы для создания UI
      GameObject CreateUIPanel(string name, Transform parent)
      {
            var panelGO = new GameObject(name);
            panelGO.transform.SetParent(parent);

            var rect = panelGO.AddComponent<RectTransform>();
            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.7f);

            return panelGO;
      }

      Button CreateButton(string text, Transform parent)
      {
            var buttonGO = new GameObject(text + " Button");
            buttonGO.transform.SetParent(parent);

            var rect = buttonGO.AddComponent<RectTransform>();
            var image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.5f, 0.8f, 0.8f);

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = image;

            // Добавляем текст
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform);

            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var textComp = textGO.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.fontSize = 16;
            textComp.color = Color.white;

            return button;
      }

      void CreateColorButton(Color color, Transform parent)
      {
            var buttonGO = new GameObject("Color Button");
            buttonGO.transform.SetParent(parent);

            var rect = buttonGO.AddComponent<RectTransform>();
            var image = buttonGO.AddComponent<Image>();
            image.color = color;

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = image;

            var colorButton = buttonGO.AddComponent<ColorButton>();

            // Настраиваем onClick для передачи цвета
            button.onClick.AddListener(() =>
            {
                  if (uiManager != null)
                  {
                        uiManager.OnColorButtonClicked(color);
                  }
            });
      }

      TextMeshProUGUI CreateText(string text, Transform parent)
      {
            var textGO = new GameObject(text.Split(':')[0] + " Text");
            textGO.transform.SetParent(parent);

            var rect = textGO.AddComponent<RectTransform>();
            var textComp = textGO.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 14;
            textComp.color = Color.white;

            return textComp;
      }

      [ContextMenu("Test Performance")]
      public void TestPerformance()
      {
            if (performanceMonitor != null)
            {
                  var report = performanceMonitor.GetPerformanceReport();
                  Debug.Log($"📊 Performance Report: {report}");
            }

            if (MemoryPoolManager.Instance != null)
            {
                  var memStats = MemoryPoolManager.Instance.GetMemoryStats();
                  Debug.Log($"🧠 Memory Stats: {memStats}");
            }
      }

      [ContextMenu("Reset Scene")]
      public void ResetScene()
      {
            Debug.Log("🔄 Сброс AR Painting сцены...");

            // Удаляем созданные объекты
            var objectsToDelete = new string[] {
            "AR Session", "AR Session Origin", "AR Painting Core",
            "AR Painting UI", "Performance Monitor"
        };

            foreach (var objName in objectsToDelete)
            {
                  var obj = GameObject.Find(objName);
                  if (obj != null)
                  {
                        DestroyImmediate(obj);
                  }
            }

            Debug.Log("✅ Сцена сброшена");
      }
}
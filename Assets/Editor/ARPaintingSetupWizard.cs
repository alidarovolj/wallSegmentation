#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using TMPro;
using Unity.Sentis;

/// <summary>
/// Unity Editor Wizard для быстрой настройки AR Painting системы
/// Автоматически создает и настраивает все необходимые компоненты
/// </summary>
public class ARPaintingSetupWizard : EditorWindow
{
      private bool includeTestComponents = true;
      private bool includePerformanceMonitoring = true;
      private bool createSampleMaterials = true;
      private bool setupForMobile = true;

      private ModelAsset segmentationModel;
      private ComputeShader preprocessorShader;
      private ComputeShader postProcessShader;

      private Vector2 scrollPosition;

      [MenuItem("AR Painting/Setup Wizard")]
      public static void ShowWindow()
      {
            var window = GetWindow<ARPaintingSetupWizard>("AR Painting Setup");
            window.minSize = new Vector2(400, 600);
            window.Show();
      }

      void OnGUI()
      {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);

            // Заголовок
            GUILayout.Label("AR Painting Setup Wizard", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("Этот мастер настроит полнофункциональную AR Painting систему в вашей сцене.", MessageType.Info);

            EditorGUILayout.Space(10);

            // Секция конфигурации
            DrawConfigurationSection();

            EditorGUILayout.Space(10);

            // Секция ресурсов
            DrawResourcesSection();

            EditorGUILayout.Space(10);

            // Секция действий
            DrawActionsSection();

            EditorGUILayout.Space(10);

            // Секция информации
            DrawInfoSection();

            EditorGUILayout.EndScrollView();
      }

      void DrawConfigurationSection()
      {
            EditorGUILayout.LabelField("Конфигурация", EditorStyles.boldLabel);

            includeTestComponents = EditorGUILayout.Toggle("Включить тестовые компоненты", includeTestComponents);
            includePerformanceMonitoring = EditorGUILayout.Toggle("Включить мониторинг производительности", includePerformanceMonitoring);
            createSampleMaterials = EditorGUILayout.Toggle("Создать примеры материалов", createSampleMaterials);
            setupForMobile = EditorGUILayout.Toggle("Настроить для мобильных устройств", setupForMobile);
      }

      void DrawResourcesSection()
      {
            EditorGUILayout.LabelField("Ресурсы", EditorStyles.boldLabel);

            segmentationModel = (ModelAsset)EditorGUILayout.ObjectField("Модель сегментации", segmentationModel, typeof(ModelAsset), false);
            preprocessorShader = (ComputeShader)EditorGUILayout.ObjectField("Preprocessor Shader", preprocessorShader, typeof(ComputeShader), false);
            postProcessShader = (ComputeShader)EditorGUILayout.ObjectField("PostProcess Shader", postProcessShader, typeof(ComputeShader), false);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Найти ресурсы автоматически"))
            {
                  FindResourcesAutomatically();
            }

            if (segmentationModel == null || preprocessorShader == null || postProcessShader == null)
            {
                  EditorGUILayout.HelpBox("Не все ресурсы назначены. Используйте кнопку 'Найти ресурсы автоматически' или назначьте их вручную.", MessageType.Warning);
            }
      }

      void DrawActionsSection()
      {
            EditorGUILayout.LabelField("Действия", EditorStyles.boldLabel);

            GUI.enabled = CanSetupScene();

            if (GUILayout.Button("🚀 Настроить AR Painting сцену", GUILayout.Height(40)))
            {
                  SetupARPaintingScene();
            }

            GUI.enabled = true;

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Проверить сцену"))
            {
                  ValidateScene();
            }

            if (GUILayout.Button("Очистить сцену"))
            {
                  ClearScene();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Создать тестовую сцену"))
            {
                  CreateTestScene();
            }
      }

      void DrawInfoSection()
      {
            EditorGUILayout.LabelField("Информация", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "AR Painting система включает:\n" +
                "• AsyncSegmentationManager - оптимизированная ML обработка\n" +
                "• PerformanceMonitor - мониторинг производительности\n" +
                "• MemoryPoolManager - управление памятью\n" +
                "• SurfaceHighlighter - подсветка поверхностей\n" +
                "• CommandSystem - Undo/Redo функциональность\n" +
                "• Полнофункциональный UI с палитрой цветов",
                MessageType.Info
            );

            if (GUILayout.Button("Открыть руководство по интеграции"))
            {
                  Application.OpenURL("file://" + Application.dataPath + "/../IMPLEMENTATION_GUIDE.md");
            }

            if (GUILayout.Button("Открыть руководство по оптимизации"))
            {
                  Application.OpenURL("file://" + Application.dataPath + "/../PERFORMANCE_OPTIMIZATION_GUIDE.md");
            }
      }

      bool CanSetupScene()
      {
            return segmentationModel != null && preprocessorShader != null && postProcessShader != null;
      }

      void FindResourcesAutomatically()
      {
            // Ищем модель сегментации
            var modelGuids = AssetDatabase.FindAssets("t:ModelAsset");
            foreach (var guid in modelGuids)
            {
                  var path = AssetDatabase.GUIDToAssetPath(guid);
                  if (path.Contains("model") || path.Contains("segmentation"))
                  {
                        segmentationModel = AssetDatabase.LoadAssetAtPath<ModelAsset>(path);
                        break;
                  }
            }

            // Ищем Compute Shaders
            var shaderGuids = AssetDatabase.FindAssets("t:ComputeShader");
            foreach (var guid in shaderGuids)
            {
                  var path = AssetDatabase.GUIDToAssetPath(guid);
                  var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);

                  if (path.Contains("Preprocessor") || path.Contains("ImagePreprocessor"))
                  {
                        preprocessorShader = shader;
                  }
                  else if (path.Contains("PostProcess"))
                  {
                        postProcessShader = shader;
                  }
            }

            Debug.Log($"✅ Автопоиск завершен: Model={segmentationModel != null}, Preprocessor={preprocessorShader != null}, PostProcess={postProcessShader != null}");
      }

      void SetupARPaintingScene()
      {
            Debug.Log("🚀 Начинаем настройку AR Painting сцены...");

            try
            {
                  // Регистрируем Undo операцию безопасно
                  if (Selection.activeGameObject != null)
                  {
                        Undo.RegisterCompleteObjectUndo(Selection.activeGameObject, "Setup AR Painting Scene");
                  }

                  // 1. Создаем AR Foundation компоненты
                  CreateARFoundationSetup();

                  // 2. Создаем Core систему
                  CreateCoreSystem();

                  // 3. Создаем UI
                  CreateUISystem();

                  // 4. Настраиваем Performance Monitoring
                  if (includePerformanceMonitoring)
                  {
                        CreatePerformanceMonitoring();
                  }

                  // 5. Создаем тестовые компоненты
                  if (includeTestComponents)
                  {
                        CreateTestComponents();
                  }

                  // 6. Создаем материалы
                  if (createSampleMaterials)
                  {
                        CreateSampleMaterials();
                  }

                  // 7. Настраиваем для мобильных устройств
                  if (setupForMobile)
                  {
                        ConfigureForMobile();
                  }

                  // 8. Связываем все компоненты
                  LinkAllComponents();

                  EditorUtility.SetDirty(null);

                  Debug.Log("✅ AR Painting сцена успешно настроена!");

                  EditorUtility.DisplayDialog("Успех!",
                      "AR Painting система успешно настроена!\n\n" +
                      "Основные компоненты созданы и связаны.\n" +
                      "Теперь вы можете запустить сцену для тестирования.",
                      "OK");
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка настройки сцены: {e.Message}");
                  EditorUtility.DisplayDialog("Ошибка", $"Ошибка настройки сцены:\n{e.Message}", "OK");
            }
      }

      void CreateARFoundationSetup()
      {
            Debug.Log("📱 Создание AR Foundation setup...");

            // AR Session
            if (FindObjectOfType<ARSession>() == null)
            {
                  var arSession = new GameObject("AR Session");
                  arSession.AddComponent<ARSession>();
            }

            // XR Origin
            var sessionOrigin = FindObjectOfType<XROrigin>();
            if (sessionOrigin == null)
            {
                  var sessionOriginGO = new GameObject("XR Origin");
                  sessionOrigin = sessionOriginGO.AddComponent<XROrigin>();

                  // AR Camera
                  var arCamera = new GameObject("AR Camera");
                  arCamera.transform.SetParent(sessionOriginGO.transform);

                  var camera = arCamera.AddComponent<Camera>();
                  camera.clearFlags = CameraClearFlags.Color;
                  camera.backgroundColor = Color.black;
                  camera.nearClipPlane = 0.1f;
                  camera.farClipPlane = 20f;

                  arCamera.AddComponent<ARCameraManager>();
                  arCamera.AddComponent<ARCameraBackground>();

                  sessionOrigin.Camera = camera;
            }

            // AR Mesh Manager для детекции поверхностей - должен быть на XROrigin
            if (sessionOrigin.GetComponent<ARMeshManager>() == null)
            {
                  try
                  {
                        sessionOrigin.gameObject.AddComponent<ARMeshManager>();
                        Debug.Log("✅ AR Mesh Manager добавлен");
                  }
                  catch (System.Exception e)
                  {
                        Debug.LogWarning($"⚠️ Не удалось добавить ARMeshManager: {e.Message}");
                  }
            }
      }

      void CreateCoreSystem()
      {
            Debug.Log("🔧 Создание Core системы...");

            var coreGO = new GameObject("AR Painting Core");

            // AsyncSegmentationManager
            var segManager = coreGO.AddComponent<AsyncSegmentationManager>();
            SetupSegmentationManager(segManager);

            // PaintManager
            coreGO.AddComponent<PaintManager>();

            // CommandManager
            coreGO.AddComponent<CommandManager>();

            // MemoryPoolManager
            coreGO.AddComponent<MemoryPoolManager>();

            // CameraFeedCapture на AR Camera
            var arCamera = FindObjectOfType<ARCameraManager>();
            if (arCamera != null)
            {
                  if (arCamera.GetComponent<CameraFeedCapture>() == null)
                  {
                        arCamera.gameObject.AddComponent<CameraFeedCapture>();
                  }

                  if (arCamera.GetComponent<SurfaceHighlighter>() == null)
                  {
                        arCamera.gameObject.AddComponent<SurfaceHighlighter>();
                  }
            }
      }

      void SetupSegmentationManager(AsyncSegmentationManager segManager)
      {
            // Используем Reflection для настройки приватных полей
            var segType = typeof(AsyncSegmentationManager);

            // Назначаем модель
            var modelField = segType.GetField("modelAsset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            modelField?.SetValue(segManager, segmentationModel);

            // Назначаем шейдеры
            var preprocessorField = segType.GetField("preprocessorShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            preprocessorField?.SetValue(segManager, preprocessorShader);

            var postProcessField = segType.GetField("postProcessShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            postProcessField?.SetValue(segManager, postProcessShader);

            // Назначаем AR Camera Manager
            var arCameraManager = FindObjectOfType<ARCameraManager>();
            var cameraField = segType.GetField("arCameraManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cameraField?.SetValue(segManager, arCameraManager);
      }

      void CreateUISystem()
      {
            Debug.Log("🎨 Создание UI системы...");

            // Главный Canvas
            var canvasGO = new GameObject("AR Painting UI");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // UIManager
            var uiManager = canvasGO.AddComponent<UIManager>();

            // Создаем базовые UI элементы напрямую
            CreateBasicUIElements(canvasGO);
      }

      void CreateBasicUIElements(GameObject canvasGO)
      {
            // Создаем простую цветовую палитру
            var palettePanel = new GameObject("Color Palette");
            palettePanel.transform.SetParent(canvasGO.transform);

            var paletteRect = palettePanel.AddComponent<RectTransform>();
            paletteRect.anchorMin = new Vector2(0.02f, 0.7f);
            paletteRect.anchorMax = new Vector2(0.25f, 0.98f);
            paletteRect.offsetMin = Vector2.zero;
            paletteRect.offsetMax = Vector2.zero;

            var paletteImage = palettePanel.AddComponent<Image>();
            paletteImage.color = new Color(0, 0, 0, 0.7f);

            // Создаем несколько цветных кнопок
            Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta };

            for (int i = 0; i < colors.Length; i++)
            {
                  var colorButton = new GameObject($"Color Button {i}");
                  colorButton.transform.SetParent(palettePanel.transform);

                  var buttonRect = colorButton.AddComponent<RectTransform>();
                  buttonRect.sizeDelta = new Vector2(60, 60);
                  buttonRect.anchoredPosition = new Vector2(35 + (i % 2) * 70, -35 - (i / 2) * 70);

                  var buttonImage = colorButton.AddComponent<Image>();
                  buttonImage.color = colors[i];

                  var button = colorButton.AddComponent<Button>();
                  button.targetGraphic = buttonImage;

                  // Простая функциональность - установка глобального цвета
                  var color = colors[i];
                  button.onClick.AddListener(() =>
                  {
                        Shader.SetGlobalColor("_GlobalPaintColor", color);
                        Debug.Log($"Selected color: {color}");
                  });
            }

            // Создаем кнопку Clear
            var clearButton = new GameObject("Clear Button");
            clearButton.transform.SetParent(canvasGO.transform);

            var clearRect = clearButton.AddComponent<RectTransform>();
            clearRect.anchorMin = new Vector2(0.02f, 0.02f);
            clearRect.anchorMax = new Vector2(0.2f, 0.1f);
            clearRect.offsetMin = Vector2.zero;
            clearRect.offsetMax = Vector2.zero;

            var clearImage = clearButton.AddComponent<Image>();
            clearImage.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);

            var clearButtonComponent = clearButton.AddComponent<Button>();
            clearButtonComponent.targetGraphic = clearImage;
            clearButtonComponent.onClick.AddListener(() =>
            {
                  Shader.SetGlobalInt("_GlobalTargetClassID", -1);
                  Debug.Log("Painting cleared");
            });

            // Добавляем текст к кнопке Clear
            var clearText = new GameObject("Text");
            clearText.transform.SetParent(clearButton.transform);

            var clearTextRect = clearText.AddComponent<RectTransform>();
            clearTextRect.anchorMin = Vector2.zero;
            clearTextRect.anchorMax = Vector2.one;
            clearTextRect.offsetMin = Vector2.zero;
            clearTextRect.offsetMax = Vector2.zero;

            var clearTextComponent = clearText.AddComponent<Text>();
            clearTextComponent.text = "Clear";
            clearTextComponent.alignment = TextAnchor.MiddleCenter;
            clearTextComponent.color = Color.white;
            clearTextComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            clearTextComponent.fontSize = 16;
      }

      void CreatePerformanceMonitoring()
      {
            Debug.Log("📊 Создание Performance Monitoring...");

            var perfGO = new GameObject("Performance Monitor");
            perfGO.AddComponent<PerformanceMonitor>();
      }

      void CreateTestComponents()
      {
            Debug.Log("🧪 Создание тестовых компонентов...");

            var testGO = new GameObject("AR Painting Tester");
            testGO.AddComponent<ARPaintingTester>();
      }

      void CreateSampleMaterials()
      {
            Debug.Log("🎭 Создание примеров материалов...");

            // Создаем папку для материалов если её нет
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                  AssetDatabase.CreateFolder("Assets", "Materials");
            }

            // Создаем базовый материал для покраски
            var paintMaterial = new Material(Shader.Find("Unlit/SurfacePaintShader"));
            AssetDatabase.CreateAsset(paintMaterial, "Assets/Materials/SurfacePaintMaterial.mat");

            // Создаем PBR материал для покраски
            var pbrMaterial = new Material(Shader.Find("Custom/PBRSurfacePaintShader"));
            AssetDatabase.CreateAsset(pbrMaterial, "Assets/Materials/PBRSurfacePaintMaterial.mat");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("✅ Материалы созданы в Assets/Materials/");
      }

      void ConfigureForMobile()
      {
            Debug.Log("📱 Настройка для мобильных устройств...");

            // Настройки качества
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;

            // Настройки рендеринга
            var camera = FindObjectOfType<Camera>();
            if (camera != null)
            {
                  camera.allowMSAA = false;
                  camera.allowHDR = false;
            }

            Debug.Log("✅ Мобильная оптимизация применена");
      }

      void LinkAllComponents()
      {
            Debug.Log("🔗 Связывание компонентов...");

            var segManager = FindObjectOfType<AsyncSegmentationManager>();
            var paintManager = FindObjectOfType<PaintManager>();
            var uiManager = FindObjectOfType<UIManager>();
            var commandManager = FindObjectOfType<CommandManager>();
            var perfMonitor = FindObjectOfType<PerformanceMonitor>();

            // Связываем UIManager
            if (uiManager != null)
            {
                  var uiType = typeof(UIManager);
                  var paintField = uiType.GetField("paintManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                  var segField = uiType.GetField("segmentationManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                  var cmdField = uiType.GetField("commandManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                  paintField?.SetValue(uiManager, paintManager);
                  segField?.SetValue(uiManager, segManager);
                  cmdField?.SetValue(uiManager, commandManager);
            }

            // Связываем PerformanceMonitor
            if (perfMonitor != null && segManager != null)
            {
                  var perfType = typeof(PerformanceMonitor);
                  var segField = perfType.GetField("segmentationManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  segField?.SetValue(perfMonitor, segManager);
            }

            Debug.Log("✅ Все компоненты связаны");
      }

      void ValidateScene()
      {
            Debug.Log("🔍 Проверка сцены...");

            var errors = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();

            // Проверяем AR Foundation
            if (FindObjectOfType<ARSession>() == null)
                  errors.Add("ARSession не найден");

            if (FindObjectOfType<XROrigin>() == null)
                  errors.Add("XROrigin не найден");

            if (FindObjectOfType<ARCameraManager>() == null)
                  errors.Add("ARCameraManager не найден");

            // Проверяем Core компоненты
            if (FindObjectOfType<AsyncSegmentationManager>() == null)
                  errors.Add("AsyncSegmentationManager не найден");

            if (FindObjectOfType<PaintManager>() == null)
                  warnings.Add("PaintManager не найден");

            if (FindObjectOfType<MemoryPoolManager>() == null)
                  warnings.Add("MemoryPoolManager не найден");

            // Проверяем UI
            if (FindObjectOfType<UIManager>() == null)
                  warnings.Add("UIManager не найден");

            // Выводим результат
            if (errors.Count == 0 && warnings.Count == 0)
            {
                  EditorUtility.DisplayDialog("Валидация", "✅ Сцена настроена корректно!", "OK");
            }
            else
            {
                  var message = "";
                  if (errors.Count > 0)
                  {
                        message += "❌ ОШИБКИ:\n" + string.Join("\n", errors) + "\n\n";
                  }
                  if (warnings.Count > 0)
                  {
                        message += "⚠️ ПРЕДУПРЕЖДЕНИЯ:\n" + string.Join("\n", warnings);
                  }

                  EditorUtility.DisplayDialog("Валидация", message, "OK");
            }
      }

      void ClearScene()
      {
            if (EditorUtility.DisplayDialog("Очистка сцены",
                "Вы уверены, что хотите удалить все AR Painting компоненты из сцены?",
                "Да", "Отмена"))
            {
                  Debug.Log("🧹 Очистка AR Painting сцены...");

                  var objectsToDelete = new string[] {
                "AR Session", "AR Session Origin", "AR Painting Core",
                "AR Painting UI", "Performance Monitor", "AR Painting Tester"
            };

                  foreach (var objName in objectsToDelete)
                  {
                        var obj = GameObject.Find(objName);
                        if (obj != null)
                        {
                              Undo.DestroyObjectImmediate(obj);
                        }
                  }

                  Debug.Log("✅ Сцена очищена");
            }
      }

      void CreateTestScene()
      {
            Debug.Log("🧪 Создание тестовой сцены...");

            var scenePath = "Assets/Scenes/ARPaintingTest.unity";

            if (EditorUtility.DisplayDialog("Создание тестовой сцены",
                $"Создать новую тестовую сцену по пути:\n{scenePath}?",
                "Да", "Отмена"))
            {
                  // Создаем новую сцену
                  var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                      UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                      UnityEditor.SceneManagement.NewSceneMode.Single
                  );

                  // Настраиваем AR Painting
                  SetupARPaintingScene();

                  // Сохраняем сцену
                  if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                  {
                        AssetDatabase.CreateFolder("Assets", "Scenes");
                  }

                  UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, scenePath);

                  Debug.Log($"✅ Тестовая сцена создана: {scenePath}");
            }
      }
}
#endif
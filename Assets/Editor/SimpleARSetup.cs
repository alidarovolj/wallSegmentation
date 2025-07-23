#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using Unity.Sentis;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Упрощенный настройщик AR Painting для существующих сцен
/// </summary>
public class SimpleARSetup : EditorWindow
{
      [MenuItem("AR Painting/Simple Setup")]
      public static void ShowWindow()
      {
            var window = GetWindow<SimpleARSetup>("Simple AR Setup");
            window.minSize = new Vector2(400, 300);
            window.Show();
      }

      void OnGUI()
      {
            GUILayout.Label("Simple AR Painting Setup", EditorStyles.largeLabel);

            EditorGUILayout.HelpBox("Этот упрощенный мастер добавит AR Painting компоненты к существующей AR сцене.", MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🔍 Найти и Настроить AR Компоненты", GUILayout.Height(40)))
            {
                  SetupARPainting();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🧹 Удалить AR Painting Компоненты"))
            {
                  CleanupARPainting();
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Компоненты будут добавлены к существующим AR объектам:\n" +
                "• AsyncSegmentationManager → AR Camera\n" +
                "• CameraFeedCapture → AR Camera\n" +
                "• SurfaceHighlighter → AR Camera\n" +
                "• PaintManager → Новый объект\n" +
                "• Простой UI → Canvas",
                MessageType.Info
            );
      }

      void SetupARPainting()
      {
            Debug.Log("🚀 Простая настройка AR Painting...");

            try
            {
                  // Находим существующие AR компоненты
                  var arCameraManager = FindObjectOfType<ARCameraManager>();
                  var xrOrigin = FindObjectOfType<XROrigin>();

                  if (arCameraManager == null)
                  {
                        EditorUtility.DisplayDialog("Ошибка", "AR Camera Manager не найден! Убедитесь, что у вас настроена AR сцена.", "OK");
                        return;
                  }

                  // 1. Добавляем компоненты к AR Camera
                  AddComponentSafely<CameraFeedCapture>(arCameraManager.gameObject, "CameraFeedCapture");
                  AddComponentSafely<SurfaceHighlighter>(arCameraManager.gameObject, "SurfaceHighlighter");

                  var segManager = AddComponentSafely<AsyncSegmentationManager>(arCameraManager.gameObject, "AsyncSegmentationManager");

                  // 2. Создаем Core Manager
                  var coreGO = GameObject.Find("AR Painting Core");
                  if (coreGO == null)
                  {
                        coreGO = new GameObject("AR Painting Core");
                        Debug.Log("✅ AR Painting Core создан");
                  }

                  AddComponentSafely<PaintManager>(coreGO, "PaintManager");
                  AddComponentSafely<CommandManager>(coreGO, "CommandManager");
                  AddComponentSafely<MemoryPoolManager>(coreGO, "MemoryPoolManager");

                  // 3. Настраиваем AsyncSegmentationManager
                  if (segManager != null)
                  {
                        SetupSegmentationManager(segManager, arCameraManager);
                  }

                  // 4. Создаем простой UI
                  CreateSimpleUI();

                  // 5. Добавляем ARMeshManager если нужно
                  if (xrOrigin != null && xrOrigin.GetComponent<ARMeshManager>() == null)
                  {
                        try
                        {
                              xrOrigin.gameObject.AddComponent<ARMeshManager>();
                              Debug.Log("✅ ARMeshManager добавлен к XROrigin");
                        }
                        catch (System.Exception e)
                        {
                              Debug.LogWarning($"⚠️ Не удалось добавить ARMeshManager: {e.Message}");
                        }
                  }

                  EditorUtility.DisplayDialog("Успех!",
                      "AR Painting компоненты успешно добавлены!\n\n" +
                      "Не забудьте:\n" +
                      "• Назначить модель в AsyncSegmentationManager\n" +
                      "• Назначить шейдеры\n" +
                      "• Протестировать в Play Mode",
                      "OK");

            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка настройки: {e.Message}");
                  EditorUtility.DisplayDialog("Ошибка", $"Ошибка настройки: {e.Message}", "OK");
            }
      }

      T AddComponentSafely<T>(GameObject target, string componentName) where T : Component
      {
            if (target.GetComponent<T>() == null)
            {
                  var component = target.AddComponent<T>();
                  Debug.Log($"✅ {componentName} добавлен к {target.name}");
                  return component;
            }
            else
            {
                  Debug.Log($"ℹ️ {componentName} уже существует на {target.name}");
                  return target.GetComponent<T>();
            }
      }

      void SetupSegmentationManager(AsyncSegmentationManager segManager, ARCameraManager arCameraManager)
      {
            Debug.Log("🔧 Настройка AsyncSegmentationManager...");

            // Автопоиск ресурсов
            var modelGuids = AssetDatabase.FindAssets("t:ModelAsset");
            ModelAsset foundModel = null;
            foreach (var guid in modelGuids)
            {
                  var path = AssetDatabase.GUIDToAssetPath(guid);
                  foundModel = AssetDatabase.LoadAssetAtPath<ModelAsset>(path);
                  if (foundModel != null) break;
            }

            var shaderGuids = AssetDatabase.FindAssets("t:ComputeShader");
            ComputeShader preprocessor = null;
            ComputeShader postProcess = null;

            foreach (var guid in shaderGuids)
            {
                  var path = AssetDatabase.GUIDToAssetPath(guid);
                  var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);

                  if (path.Contains("Preprocessor") || path.Contains("ImagePreprocessor"))
                  {
                        preprocessor = shader;
                  }
                  else if (path.Contains("PostProcess"))
                  {
                        postProcess = shader;
                  }
            }

            // Используем reflection для настройки приватных полей
            var segType = typeof(AsyncSegmentationManager);

            if (foundModel != null)
            {
                  var modelField = segType.GetField("modelAsset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  modelField?.SetValue(segManager, foundModel);
                  Debug.Log($"✅ Модель назначена: {foundModel.name}");
            }

            if (preprocessor != null)
            {
                  var preprocessorField = segType.GetField("preprocessorShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  preprocessorField?.SetValue(segManager, preprocessor);
                  Debug.Log($"✅ Preprocessor назначен: {preprocessor.name}");
            }

            if (postProcess != null)
            {
                  var postProcessField = segType.GetField("postProcessShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                  postProcessField?.SetValue(segManager, postProcess);
                  Debug.Log($"✅ PostProcess назначен: {postProcess.name}");
            }

            var cameraField = segType.GetField("arCameraManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cameraField?.SetValue(segManager, arCameraManager);
            Debug.Log("✅ AR Camera Manager назначен");
      }

      void CreateSimpleUI()
      {
            Debug.Log("🎨 Создание простого UI...");

            // Находим или создаем Canvas
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                  var canvasGO = new GameObject("AR Painting Canvas");
                  canvas = canvasGO.AddComponent<Canvas>();
                  canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                  canvasGO.AddComponent<CanvasScaler>();
                  canvasGO.AddComponent<GraphicRaycaster>();
                  Debug.Log("✅ Canvas создан");
            }

            // Создаем простые кнопки для тестирования
            CreateTestButton(canvas.transform, "Red Paint", Color.red, new Vector2(-200, 200));
            CreateTestButton(canvas.transform, "Green Paint", Color.green, new Vector2(-200, 150));
            CreateTestButton(canvas.transform, "Blue Paint", Color.blue, new Vector2(-200, 100));
            CreateTestButton(canvas.transform, "Clear", Color.white, new Vector2(-200, 50));

            Debug.Log("✅ Простой UI создан");
      }

      void CreateTestButton(Transform parent, string text, Color color, Vector2 position)
      {
            var buttonGO = new GameObject(text + " Button");
            buttonGO.transform.SetParent(parent);

            var rectTransform = buttonGO.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(120, 30);
            rectTransform.anchoredPosition = position;

            var image = buttonGO.AddComponent<UnityEngine.UI.Image>();
            image.color = color == Color.white ? new Color(0.5f, 0.5f, 0.5f) : color;

            var button = buttonGO.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            // Добавляем текст
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform);

            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var textComponent = textGO.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.white;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 12;

            // Простая функциональность
            if (text == "Clear")
            {
                  button.onClick.AddListener(() =>
                  {
                        Shader.SetGlobalInt("_GlobalTargetClassID", -1);
                        Debug.Log("🧹 Painting cleared");
                  });
            }
            else
            {
                  button.onClick.AddListener(() =>
                  {
                        Shader.SetGlobalColor("_GlobalPaintColor", color);
                        Debug.Log($"🎨 Color selected: {color}");
                  });
            }
      }

      void CleanupARPainting()
      {
            if (EditorUtility.DisplayDialog("Удаление", "Удалить все AR Painting компоненты?", "Да", "Отмена"))
            {
                  Debug.Log("🧹 Удаление AR Painting компонентов...");

                  // Удаляем компоненты с AR Camera
                  var arCamera = FindObjectOfType<ARCameraManager>();
                  if (arCamera != null)
                  {
                        DestroyComponent<CameraFeedCapture>(arCamera.gameObject);
                        DestroyComponent<SurfaceHighlighter>(arCamera.gameObject);
                        DestroyComponent<AsyncSegmentationManager>(arCamera.gameObject);
                  }

                  // Удаляем Core объект
                  var coreGO = GameObject.Find("AR Painting Core");
                  if (coreGO != null)
                  {
                        DestroyImmediate(coreGO);
                        Debug.Log("✅ AR Painting Core удален");
                  }

                  Debug.Log("✅ Очистка завершена");
            }
      }

      void DestroyComponent<T>(GameObject target) where T : Component
      {
            var component = target.GetComponent<T>();
            if (component != null)
            {
                  DestroyImmediate(component);
                  Debug.Log($"✅ {typeof(T).Name} удален с {target.name}");
            }
      }
}
#endif
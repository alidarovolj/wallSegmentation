using UnityEngine;
using System;

/// <summary>
/// Упрощенный мост Unity-Flutter для работы с простой системой двух цветов.
/// Без сложных каталогов и поиска - только базовые операции с цветами.
/// </summary>
public class SimpleFlutterBridge : MonoBehaviour
{
      [Header("Dependencies")]
      [SerializeField] private SimpleColorManager colorManager;
      [SerializeField] private SimpleColorUI colorUI;
      [SerializeField] private ProjectManager projectManager;

      [Header("Settings")]
      [SerializeField] private bool enableDebugLogs = true;

      void Awake()
      {
            // Автопоиск зависимостей
            if (colorManager == null)
                  colorManager = FindObjectOfType<SimpleColorManager>();
            if (colorUI == null)
                  colorUI = FindObjectOfType<SimpleColorUI>();
            if (projectManager == null)
                  projectManager = FindObjectOfType<ProjectManager>();
      }

      void Start()
      {
            // Подписываемся на события
            SubscribeToEvents();

            // Отправляем сигнал готовности Flutter
            SendMessageToFlutter("onUnityReady", new
            {
                  version = Application.version,
                  features = new string[] { "color_scanning", "simple_colors", "projects" }
            });

            if (enableDebugLogs)
                  Debug.Log("📱 SimpleFlutterBridge готов к работе");
      }

      void SubscribeToEvents()
      {
            if (colorManager != null)
            {
                  colorManager.OnOriginalColorChanged += OnOriginalColorChanged;
                  colorManager.OnPaintColorChanged += OnPaintColorChanged;
                  colorManager.OnColorsSet += OnColorsSet;
            }

            if (projectManager != null)
            {
                  projectManager.OnProjectSaved += OnProjectSaved;
                  projectManager.OnProjectLoaded += OnProjectLoaded;
                  projectManager.OnError += OnProjectError;
            }
      }

      #region Flutter API Methods

      /// <summary>
      /// Flutter -> Unity: Сканирует цвет стены
      /// Параметры: {"x": 0.5, "y": 0.5} или пустая строка для центра экрана
      /// </summary>
      public void ScanWallColor(string position = "")
      {
            try
            {
                  if (colorManager != null)
                  {
                        if (string.IsNullOrEmpty(position))
                        {
                              colorManager.ScanWallColor();
                        }
                        else
                        {
                              var pos = JsonUtility.FromJson<Vector2Wrapper>(position);
                              Vector2 screenPos = new Vector2(pos.x * Screen.width, pos.y * Screen.height);
                              colorManager.ScanWallColorAt(screenPos);
                        }

                        if (enableDebugLogs)
                              Debug.Log($"📱 Flutter: Запуск сканирования цвета");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка сканирования: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter -> Unity: Устанавливает цвет перекраски
      /// Параметры: {"color": "#FF5733"}
      /// </summary>
      public void SetPaintColor(string colorData)
      {
            try
            {
                  var data = JsonUtility.FromJson<ColorWrapper>(colorData);

                  if (colorManager != null)
                  {
                        colorManager.SetPaintColorFromHex(data.color);

                        if (enableDebugLogs)
                              Debug.Log($"📱 Flutter: Установлен цвет {data.color}");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка установки цвета: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter -> Unity: Переключает на следующий цвет из пресетов
      /// </summary>
      public void NextPaintColor(string unused = "")
      {
            try
            {
                  if (colorManager != null)
                  {
                        colorManager.NextPaintColor();

                        if (enableDebugLogs)
                              Debug.Log("📱 Flutter: Следующий цвет");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка переключения цвета: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter -> Unity: Переключает на предыдущий цвет из пресетов
      /// </summary>
      public void PreviousPaintColor(string unused = "")
      {
            try
            {
                  if (colorManager != null)
                  {
                        colorManager.PreviousPaintColor();

                        if (enableDebugLogs)
                              Debug.Log("📱 Flutter: Предыдущий цвет");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка переключения цвета: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter -> Unity: Меняет местами исходный цвет и цвет перекраски
      /// </summary>
      public void SwapColors(string unused = "")
      {
            try
            {
                  if (colorManager != null)
                  {
                        colorManager.SwapColors();

                        if (enableDebugLogs)
                              Debug.Log("📱 Flutter: Смена цветов местами");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка смены цветов: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter -> Unity: Сбрасывает к исходному цвету стены
      /// </summary>
      public void ResetToOriginal(string unused = "")
      {
            try
            {
                  if (colorManager != null)
                  {
                        colorManager.ResetToOriginal();

                        if (enableDebugLogs)
                              Debug.Log("📱 Flutter: Сброс к исходному цвету");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка сброса: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter -> Unity: Получает текущие цвета
      /// </summary>
      public void GetCurrentColors(string unused = "")
      {
            try
            {
                  if (colorManager != null)
                  {
                        var (original, paint) = colorManager.GetCurrentColors();
                        var presets = colorManager.GetPaintPresets();

                        var response = new
                        {
                              originalColor = ColorUtility.ToHtmlStringRGB(original),
                              paintColor = ColorUtility.ToHtmlStringRGB(paint),
                              currentPresetIndex = colorManager.GetCurrentPresetIndex(),
                              presets = System.Array.ConvertAll(presets, color => ColorUtility.ToHtmlStringRGB(color))
                        };

                        SendMessageToFlutter("onCurrentColors", response);

                        if (enableDebugLogs)
                              Debug.Log($"📱 Flutter: Отправлены текущие цвета");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка получения цветов: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter -> Unity: Сохраняет текущий проект
      /// Параметры: {"name": "Моя комната"}
      /// </summary>
      public void SaveProject(string projectData)
      {
            try
            {
                  var data = JsonUtility.FromJson<ProjectWrapper>(projectData);

                  if (projectManager != null)
                  {
                        projectManager.SaveCurrentProject(data.name);

                        if (enableDebugLogs)
                              Debug.Log($"📱 Flutter: Сохранение проекта '{data.name}'");
                  }
            }
            catch (Exception e)
            {
                  SendError($"Ошибка сохранения проекта: {e.Message}");
            }
      }

      #endregion

      #region Event Handlers

      private void OnOriginalColorChanged(Color newColor)
      {
            var response = new
            {
                  type = "originalColor",
                  color = ColorUtility.ToHtmlStringRGB(newColor)
            };

            SendMessageToFlutter("onColorChanged", response);
      }

      private void OnPaintColorChanged(Color newColor)
      {
            var response = new
            {
                  type = "paintColor",
                  color = ColorUtility.ToHtmlStringRGB(newColor),
                  presetIndex = colorManager?.GetCurrentPresetIndex() ?? -1
            };

            SendMessageToFlutter("onColorChanged", response);
      }

      private void OnColorsSet(Color original, Color paint)
      {
            var response = new
            {
                  originalColor = ColorUtility.ToHtmlStringRGB(original),
                  paintColor = ColorUtility.ToHtmlStringRGB(paint),
                  presetIndex = colorManager?.GetCurrentPresetIndex() ?? -1
            };

            SendMessageToFlutter("onColorsSet", response);
      }

      private void OnProjectSaved(string projectId)
      {
            var response = new
            {
                  projectId = projectId,
                  success = true,
                  message = "Проект сохранен успешно"
            };

            SendMessageToFlutter("onProjectSaved", response);
      }

      private void OnProjectLoaded(string projectId)
      {
            var response = new
            {
                  projectId = projectId,
                  success = true,
                  message = "Проект загружен успешно"
            };

            SendMessageToFlutter("onProjectLoaded", response);
      }

      private void OnProjectError(string error)
      {
            SendError($"Проект: {error}");
      }

      #endregion

      #region Private Methods

      private void SendMessageToFlutter(string messageType, object data)
      {
            try
            {
                  string json = JsonUtility.ToJson(new { type = messageType, data = data });

                  // Используем существующий FlutterUnityManager
                  var flutterManager = FlutterUnityManager.Instance;
                  if (flutterManager != null)
                  {
                        // TODO: Добавить метод отправки в FlutterUnityManager
                        if (enableDebugLogs)
                              Debug.Log($"📤 Flutter: {messageType} - {json}");
                  }
                  else
                  {
                        // Fallback
                        Application.ExternalCall("UnityMessageHandler", json);
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка отправки во Flutter: {e.Message}");
            }
      }

      private void SendError(string error)
      {
            SendMessageToFlutter("onError", new { error = error });
            Debug.LogError($"❌ SimpleFlutterBridge: {error}");
      }

      #endregion

      #region Data Structures

      [System.Serializable]
      private struct Vector2Wrapper
      {
            public float x;
            public float y;
      }

      [System.Serializable]
      private struct ColorWrapper
      {
            public string color;
      }

      [System.Serializable]
      private struct ProjectWrapper
      {
            public string name;
      }

      #endregion
}

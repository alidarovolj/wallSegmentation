using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Упрощенный мост для коммуникации между Unity и Flutter.
/// Адаптирован для простой системы с двумя цветами.
/// Интегрируется с SimpleColorManager для управления цветами.
/// </summary>
public class FlutterUnityBridge : MonoBehaviour
{
      [Header("Dependencies")]
      [SerializeField] private SimpleColorManager simpleColorManager;
      [SerializeField] private ColorScanner colorScanner;
      [SerializeField] private ProjectManager projectManager;
      [SerializeField] private AsyncSegmentationManager segmentationManager;

      [Header("Settings")]
      [SerializeField] private bool enableDebugLogs = true;

      void Awake()
      {
            // Автопоиск зависимостей если не назначены
            AutoDiscoverDependencies();

            // Подписываемся на события
            SubscribeToEvents();
      }

      void Start()
      {
            // Отправляем сигнал готовности Flutter
            SendMessageToFlutter("onUnityReady", new
            {
                  version = Application.version,
                  hasSimpleColorManager = simpleColorManager != null,
                  hasColorScanner = colorScanner != null,
                  hasProjectManager = projectManager != null
            });
      }

      #region Auto Discovery

      private void AutoDiscoverDependencies()
      {
            if (simpleColorManager == null)
                  simpleColorManager = FindObjectOfType<SimpleColorManager>();

            if (colorScanner == null)
                  colorScanner = FindObjectOfType<ColorScanner>();

            if (projectManager == null)
                  projectManager = FindObjectOfType<ProjectManager>();

            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            if (enableDebugLogs)
            {
                  Debug.Log($"🔍 FlutterUnityBridge автопоиск:");
                  Debug.Log($"  SimpleColorManager: {(simpleColorManager != null ? "✅" : "❌")}");
                  Debug.Log($"  ColorScanner: {(colorScanner != null ? "✅" : "❌")}");
                  Debug.Log($"  ProjectManager: {(projectManager != null ? "✅" : "❌")}");
                  Debug.Log($"  SegmentationManager: {(segmentationManager != null ? "✅" : "❌")}");
            }
      }

      #endregion

      #region Event Subscriptions

      private void SubscribeToEvents()
      {
            if (simpleColorManager != null)
            {
                  simpleColorManager.OnOriginalColorChanged += OnOriginalColorChanged;
                  simpleColorManager.OnPaintColorChanged += OnPaintColorChanged;
                  simpleColorManager.OnColorsSet += OnColorsSet;
            }

            if (colorScanner != null)
            {
                  colorScanner.OnColorScanned += OnColorScanned;
            }

            if (projectManager != null)
            {
                  projectManager.OnProjectSaved += OnProjectSaved;
                  projectManager.OnProjectLoaded += OnProjectLoaded;
                  projectManager.OnError += OnProjectError;
            }
      }

      private void OnDestroy()
      {
            // Отписываемся от событий
            if (simpleColorManager != null)
            {
                  simpleColorManager.OnOriginalColorChanged -= OnOriginalColorChanged;
                  simpleColorManager.OnPaintColorChanged -= OnPaintColorChanged;
                  simpleColorManager.OnColorsSet -= OnColorsSet;
            }

            if (colorScanner != null)
            {
                  colorScanner.OnColorScanned -= OnColorScanned;
            }

            if (projectManager != null)
            {
                  projectManager.OnProjectSaved -= OnProjectSaved;
                  projectManager.OnProjectLoaded -= OnProjectLoaded;
                  projectManager.OnError -= OnProjectError;
            }
      }

      #endregion

      #region Simple Color Management API

      /// <summary>
      /// Сканирует цвет стены в указанной позиции экрана
      /// Вызывается из Flutter: UnityWidget.postMessage('ScanWallColor', {x: 100, y: 200})
      /// </summary>
      public void ScanWallColor(string payloadJson)
      {
            try
            {
                  if (simpleColorManager == null)
                  {
                        SendError("SimpleColorManager не найден");
                        return;
                  }

                  if (!string.IsNullOrEmpty(payloadJson))
                  {
                        var payload = JsonUtility.FromJson<ScreenPosition>(payloadJson);
                        var screenPos = new Vector2(payload.x, payload.y);

                        if (enableDebugLogs)
                              Debug.Log($"📍 Сканируем цвет стены в позиции: {screenPos}");

                        colorScanner?.ScanColorAtScreenPos(screenPos);
                  }
                  else
                  {
                        // Сканируем центр экрана
                        simpleColorManager.ScanWallColor();
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при сканировании цвета стены: {e.Message}");
                  SendError($"Ошибка сканирования: {e.Message}");
            }
      }

      /// <summary>
      /// Устанавливает цвет перекраски из Flutter
      /// Вызывается из Flutter: UnityWidget.postMessage('SetPaintColor', '#FF0000')
      /// </summary>
      public void SetPaintColor(string hexColor)
      {
            try
            {
                  if (simpleColorManager == null)
                  {
                        SendError("SimpleColorManager не найден");
                        return;
                  }

                  if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
                  {
                        simpleColorManager.SetPaintColor(color);

                        if (enableDebugLogs)
                              Debug.Log($"🎨 Установлен цвет перекраски: {hexColor}");
                  }
                  else
                  {
                        SendError($"Неверный формат цвета: {hexColor}");
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при установке цвета: {e.Message}");
                  SendError($"Ошибка установки цвета: {e.Message}");
            }
      }

      /// <summary>
      /// Переключает на следующий пресет цвета
      /// Вызывается из Flutter: UnityWidget.postMessage('NextPaintColor', '')
      /// </summary>
      public void NextPaintColor(string message = "")
      {
            try
            {
                  if (simpleColorManager == null)
                  {
                        SendError("SimpleColorManager не найден");
                        return;
                  }

                  simpleColorManager.NextPaintColor();

                  if (enableDebugLogs)
                        Debug.Log("➡️ Переключен на следующий цвет");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при переключении цвета: {e.Message}");
                  SendError($"Ошибка переключения: {e.Message}");
            }
      }

      /// <summary>
      /// Переключает на предыдущий пресет цвета
      /// Вызывается из Flutter: UnityWidget.postMessage('PreviousPaintColor', '')
      /// </summary>
      public void PreviousPaintColor(string message = "")
      {
            try
            {
                  if (simpleColorManager == null)
                  {
                        SendError("SimpleColorManager не найден");
                        return;
                  }

                  simpleColorManager.PreviousPaintColor();

                  if (enableDebugLogs)
                        Debug.Log("⬅️ Переключен на предыдущий цвет");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при переключении цвета: {e.Message}");
                  SendError($"Ошибка переключения: {e.Message}");
            }
      }

      /// <summary>
      /// Меняет местами исходный цвет и цвет перекраски
      /// Вызывается из Flutter: UnityWidget.postMessage('SwapColors', '')
      /// </summary>
      public void SwapColors(string message = "")
      {
            try
            {
                  if (simpleColorManager == null)
                  {
                        SendError("SimpleColorManager не найден");
                        return;
                  }

                  simpleColorManager.SwapColors();

                  if (enableDebugLogs)
                        Debug.Log("🔄 Цвета поменяны местами");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при смене цветов: {e.Message}");
                  SendError($"Ошибка смены цветов: {e.Message}");
            }
      }

      /// <summary>
      /// Сбрасывает цвета к значениям по умолчанию
      /// Вызывается из Flutter: UnityWidget.postMessage('ResetColors', '')
      /// </summary>
      public void ResetColors(string message = "")
      {
            try
            {
                  if (simpleColorManager == null)
                  {
                        SendError("SimpleColorManager не найден");
                        return;
                  }

                  simpleColorManager.ResetColors();

                  if (enableDebugLogs)
                        Debug.Log("🔄 Цвета сброшены");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при сбросе цветов: {e.Message}");
                  SendError($"Ошибка сброса: {e.Message}");
            }
      }

      /// <summary>
      /// Получает текущие цвета
      /// Вызывается из Flutter: UnityWidget.postMessage('GetCurrentColors', '')
      /// </summary>
      public void GetCurrentColors(string message = "")
      {
            try
            {
                  if (simpleColorManager == null)
                  {
                        SendError("SimpleColorManager не найден");
                        return;
                  }

                  var colors = simpleColorManager.GetCurrentColors();

                  SendMessageToFlutter("onCurrentColors", new
                  {
                        originalColor = ColorUtility.ToHtmlStringRGB(colors.original),
                        paintColor = ColorUtility.ToHtmlStringRGB(colors.paint),
                        currentPresetIndex = simpleColorManager.CurrentPresetIndex
                  });

                  if (enableDebugLogs)
                        Debug.Log($"📋 Отправлены текущие цвета: original={ColorUtility.ToHtmlStringRGB(colors.original)}, paint={ColorUtility.ToHtmlStringRGB(colors.paint)}");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при получении цветов: {e.Message}");
                  SendError($"Ошибка получения цветов: {e.Message}");
            }
      }

      #endregion

      #region Project Management API

      /// <summary>
      /// Сохраняет текущий проект
      /// Вызывается из Flutter: UnityWidget.postMessage('SaveProject', 'Мой проект')
      /// </summary>
      public void SaveProject(string projectName)
      {
            try
            {
                  if (projectManager == null)
                  {
                        SendError("ProjectManager не найден");
                        return;
                  }

                  if (string.IsNullOrEmpty(projectName))
                        projectName = $"Проект {DateTime.Now:dd.MM.yyyy HH:mm}";

                  projectManager.SaveProject(projectName);

                  if (enableDebugLogs)
                        Debug.Log($"💾 Сохраняем проект: {projectName}");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при сохранении проекта: {e.Message}");
                  SendError($"Ошибка сохранения: {e.Message}");
            }
      }

      /// <summary>
      /// Загружает проект по ID
      /// Вызывается из Flutter: UnityWidget.postMessage('LoadProject', 'project_id_123')
      /// </summary>
      public void LoadProject(string projectId)
      {
            try
            {
                  if (projectManager == null)
                  {
                        SendError("ProjectManager не найден");
                        return;
                  }

                  if (string.IsNullOrEmpty(projectId))
                  {
                        SendError("ID проекта не указан");
                        return;
                  }

                  projectManager.LoadProject(projectId);

                  if (enableDebugLogs)
                        Debug.Log($"📂 Загружаем проект: {projectId}");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при загрузке проекта: {e.Message}");
                  SendError($"Ошибка загрузки: {e.Message}");
            }
      }

      /// <summary>
      /// Получает список всех проектов
      /// Вызывается из Flutter: UnityWidget.postMessage('GetProjectList', '')
      /// </summary>
      public void GetProjectList(string message = "")
      {
            try
            {
                  if (projectManager == null)
                  {
                        SendError("ProjectManager не найден");
                        return;
                  }

                  var projects = projectManager.LoadAll();

                  SendMessageToFlutter("onProjectList", new
                  {
                        projects = projects.ConvertAll(p => new
                        {
                              id = p.id,
                              name = p.name,
                              createdAt = p.created,
                              lastModified = p.modified,
                              hasAppliedColors = p.appliedColors != null && p.appliedColors.Count > 0
                        })
                  });

                  if (enableDebugLogs)
                        Debug.Log($"📋 Отправлен список из {projects.Count} проектов");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при получении списка проектов: {e.Message}");
                  SendError($"Ошибка получения списка: {e.Message}");
            }
      }

      #endregion

      #region AR Settings API

      /// <summary>
      /// Устанавливает прозрачность визуализации
      /// Вызывается из Flutter: UnityWidget.postMessage('SetVisualizationOpacity', '0.8')
      /// TODO: Добавить публичный метод в AsyncSegmentationManager
      /// </summary>
      public void SetVisualizationOpacity(string opacityStr)
      {
            try
            {
                  if (float.TryParse(opacityStr, out float opacity))
                  {
                        // TODO: Вызвать публичный метод когда он будет добавлен
                        // segmentationManager?.SetVisualizationOpacity(Mathf.Clamp01(opacity));

                        if (enableDebugLogs)
                              Debug.Log($"👁️ Запрос установки прозрачности: {opacity} (TODO: реализовать в AsyncSegmentationManager)");
                  }
                  else
                  {
                        SendError($"Неверное значение прозрачности: {opacityStr}");
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при установке прозрачности: {e.Message}");
                  SendError($"Ошибка установки прозрачности: {e.Message}");
            }
      }

      #endregion

      #region Event Handlers

      private void OnOriginalColorChanged(Color color)
      {
            SendMessageToFlutter("onOriginalColorChanged", new
            {
                  color = ColorUtility.ToHtmlStringRGB(color)
            });
      }

      private void OnPaintColorChanged(Color color)
      {
            SendMessageToFlutter("onPaintColorChanged", new
            {
                  color = ColorUtility.ToHtmlStringRGB(color),
                  presetIndex = simpleColorManager?.CurrentPresetIndex ?? 0
            });
      }

      private void OnColorsSet(Color original, Color paint)
      {
            SendMessageToFlutter("onColorsSet", new
            {
                  originalColor = ColorUtility.ToHtmlStringRGB(original),
                  paintColor = ColorUtility.ToHtmlStringRGB(paint)
            });
      }

      private void OnColorScanned(Color color, Vector2 screenPos)
      {
            SendMessageToFlutter("onColorScanned", new
            {
                  color = ColorUtility.ToHtmlStringRGB(color),
                  position = new { x = screenPos.x, y = screenPos.y }
            });
      }

      private void OnProjectSaved(string projectId)
      {
            SendMessageToFlutter("onProjectSaved", new
            {
                  projectId = projectId,
                  message = "Проект сохранен успешно"
            });
      }

      private void OnProjectLoaded(string projectId)
      {
            SendMessageToFlutter("onProjectLoaded", new
            {
                  projectId = projectId,
                  message = "Проект загружен успешно"
            });
      }

      private void OnProjectError(string error)
      {
            SendMessageToFlutter("onProjectError", new
            {
                  error = error
            });
      }

      #endregion

      #region Flutter Communication

      private void SendMessageToFlutter(string messageType, object data)
      {
            try
            {
                  string json = JsonUtility.ToJson(new { type = messageType, data = data });

                  // Используем существующий FlutterUnityManager если доступен
                  var existingBridge = FlutterUnityManager.Instance;
                  if (existingBridge != null)
                  {
                        // TODO: Добавить метод SendMessageToFlutter в FlutterUnityManager
                        // existingBridge.SendMessageToFlutter(json);

                        if (enableDebugLogs)
                              Debug.Log($"📤 Сообщение для Flutter: {json}");
                  }
                  else
                  {
                        // Fallback: используем браузерный API
                        Application.ExternalCall("UnityMessageHandler", json);
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка при отправке сообщения в Flutter: {e.Message}");
            }
      }

      private void SendError(string error)
      {
            SendMessageToFlutter("onError", new { error = error });
      }

      #endregion

      #region Data Structures

      [Serializable]
      private class ScreenPosition
      {
            public float x;
            public float y;
      }

      #endregion
}

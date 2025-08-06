using UnityEngine;
using System.Collections;

/// <summary>
/// Основной менеджер для коммуникации между Flutter и Unity
/// </summary>
public class FlutterUnityManager : MonoBehaviour
{
      public static FlutterUnityManager Instance;

      [Header("Configuration")]
      [Tooltip("Включить отладочный режим")]
      public bool debugMode = true;

      [Header("Dependencies")]
      [Tooltip("Ссылка на AsyncSegmentationManager")]
      [SerializeField]
      private AsyncSegmentationManager segmentationManager;

      // Поле для ARWallPresenter, как в инструкции
      [SerializeField]
      private ARWallPresenter arWallPresenter;


      private void Awake()
      {
            // Singleton pattern
            if (Instance == null)
            {
                  Instance = this;
                  DontDestroyOnLoad(gameObject);
            }
            else
            {
                  Destroy(gameObject);
                  return;
            }

            InitializeManager();
      }

      void OnEnable()
      {
            Debug.Log("✅ FlutterUnityManager включен и готов принимать сообщения");
      }

      private void InitializeManager()
      {
            if (debugMode)
            {
                  Debug.Log("🔗 FlutterUnityManager initialized");
            }

            // Автоматический поиск компонентов, если они не назначены
            if (segmentationManager == null)
            {
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            }

            if (arWallPresenter == null)
            {
                  arWallPresenter = FindObjectOfType<ARWallPresenter>();
            }


            // Проверка, что все компоненты найдены
            if (segmentationManager == null)
            {
                  Debug.LogWarning("⚠️ AsyncSegmentationManager не найден!");
            }

            if (arWallPresenter == null)
            {
                  Debug.LogWarning("⚠️ ARWallPresenter не найден!");
            }
      }

      private void Start()
      {
            // Отправляем сообщение о готовности Unity
            StartCoroutine(SendInitialState());
      }

      private IEnumerator SendInitialState()
      {
            // Ждем один кадр, чтобы все компоненты инициализировались
            yield return new WaitForSeconds(1f);

            SendMessage("onUnityReady", "Unity scene loaded and ready");
      }

      #region Flutter to Unity Communication

      /// <summary>
      /// Устанавливает цвет для окрашивания из Flutter (HEX формат)
      /// </summary>
      /// <param name="hexColor">Цвет в HEX формате (например, #FF5733)</param>
      public void SetPaintColor(string hexColor)
      {
            Debug.Log($"📨 Получено сообщение SetPaintColor: {hexColor}");
            if (debugMode)
            {
                  Debug.Log($"🎨 Flutter -> Unity: SetPaintColor({hexColor})");
            }

            if (string.IsNullOrEmpty(hexColor))
            {
                  Debug.LogError("❌ Получен пустой цвет от Flutter");
                  return;
            }

            if (segmentationManager != null)
            {
                  if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
                  {
                        segmentationManager.SetPaintColor(color);
                        Debug.Log($"✅ Цвет применен к сегментации: {color}");

                        // Отправляем подтверждение обратно в Flutter
                        SendMessage("colorChanged", $"Color changed to {hexColor}");
                  }
                  else
                  {
                        Debug.LogError($"❌ Не удалось разобрать цвет: {hexColor}");
                        SendMessage("error", $"Invalid color format: {hexColor}");
                  }
            }
            else
            {
                  Debug.LogError("❌ SegmentationManager не найден!");
                  SendMessage("error", "SegmentationManager not found");
            }
      }

      /// <summary>
      /// Включает/выключает режим рисования
      /// </summary>
      public void SetPaintingMode(string isEnabled)
      {
            bool enabled = isEnabled.ToLower() == "true";
            Debug.Log($"🖌️ Flutter -> Unity: SetPaintingMode({enabled})");

            // В AsyncSegmentationManager нужно добавить метод SetPaintingEnabled
            // segmentationManager?.SetPaintingEnabled(enabled);

            SendMessage("paintingModeChanged", enabled.ToString());
      }

      /// <summary>
      /// Сбрасывает все покрашенные стены
      /// </summary>
      public void ResetWalls(string unused)
      {
            Debug.Log("🔄 Flutter -> Unity: ResetWalls");

            // В AsyncSegmentationManager нужно добавить метод ResetPaint
            // segmentationManager?.ResetPaint();

            SendMessage("wallsReset", "Walls have been reset");
      }

      /// <summary>
      /// Переключает вспышку камеры
      /// </summary>
      public void ToggleFlashlight(string unused)
      {
            Debug.Log("🔦 Flutter -> Unity: ToggleFlashlight");

            // Здесь добавьте логику управления вспышкой
            // Например, через ARCameraManager

            SendMessage("flashlightToggled", "Flashlight toggled");
      }


      #endregion

      #region Unity to Flutter Communication

      /// <summary>
      /// Отправляет сообщение в Flutter
      /// </summary>
      /// <param name="eventType">Тип события</param>
      /// <param name="data">Данные для отправки</param>
      public void SendMessage(string eventType, string data)
      {
            string message = $"{eventType}:{data}";
            if (debugMode)
            {
                  Debug.Log($"📤 Unity -> Flutter: {message}");
            }

            SendToFlutter.Send(message);
      }

      /// <summary>
      /// Отправляет данные о состоянии сегментации
      /// </summary>
      public void SendSegmentationState()
      {
            if (segmentationManager != null)
            {
                  // Убедимся что в AsyncSegmentationManager есть эти свойства
                  var state = new
                  {
                        // isProcessing = segmentationManager.IsActive,
                        currentColor = ColorUtility.ToHtmlStringRGB(segmentationManager.paintColor)
                        // selectedClass = 0 // walls
                  };

                  string jsonState = JsonUtility.ToJson(state);
                  SendMessage("onSegmentationStateChanged", jsonState);
            }
      }

      /// <summary>
      /// Отправляет уведомление об ошибке
      /// </summary>
      /// <param name="error">Описание ошибки</param>
      public void SendError(string error)
      {
            SendMessage("error", error);
      }

      #endregion

      private void OnApplicationPause(bool pauseStatus)
      {
            if (debugMode)
            {
                  Debug.Log($"🔄 Application pause: {pauseStatus}");
            }

            SendMessage("onUnityPause", pauseStatus.ToString());
      }

      private void OnApplicationFocus(bool hasFocus)
      {
            if (debugMode)
            {
                  Debug.Log($"🎯 Application focus: {hasFocus}");
            }

            SendMessage("onUnityFocus", hasFocus.ToString());
      }
}
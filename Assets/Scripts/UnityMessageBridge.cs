using UnityEngine;

/// <summary>
/// УСТАРЕВШИЙ: Используйте FlutterUnityManager вместо этого класса
/// Оставлен для обратной совместимости
/// </summary>
public class UnityMessageBridge : MonoBehaviour
{
      [Tooltip("Ссылка на менеджер сегментации для управления цветом.")]
      [SerializeField]
      private AsyncSegmentationManager segmentationManager;

      [Header("Compatibility")]
      [Tooltip("Использовать новый FlutterUnityManager (рекомендуется)")]
      public bool useFlutterUnityManager = true;

      void Start()
      {
            if (useFlutterUnityManager)
            {
                  Debug.LogWarning("⚠️ UnityMessageBridge устарел. Используйте FlutterUnityManager!");

                  // Проверяем, есть ли FlutterUnityManager на сцене
                  if (FlutterUnityManager.Instance == null)
                  {
                        Debug.LogError("❌ FlutterUnityManager не найден! Добавьте его на сцену или отключите useFlutterUnityManager.");
                  }
                  return;
            }

            // Старая логика для обратной совместимости
            if (segmentationManager == null)
            {
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
                  if (segmentationManager == null)
                  {
                        Debug.LogError("UnityMessageBridge: AsyncSegmentationManager не найден на сцене! Покраска из Flutter не будет работать.");
                  }
            }
      }

      /// <summary>
      /// Этот метод должен вызываться из Flutter для установки цвета покраски.
      /// УСТАРЕВШИЙ: Используйте FlutterUnityManager.SetPaintColor() вместо этого
      /// </summary>
      /// <param name="hexColor">Строка с цветом в HEX-формате (например, "#RRGGBB").</param>
      public void SetPaintColorFromHex(string hexColor)
      {
            if (useFlutterUnityManager && FlutterUnityManager.Instance != null)
            {
                  Debug.Log("🔄 Перенаправляем вызов в FlutterUnityManager");
                  FlutterUnityManager.Instance.SetPaintColor(hexColor);
                  return;
            }

            // Старая логика для обратной совместимости
            if (segmentationManager == null)
            {
                  Debug.LogError("Невозможно установить цвет: AsyncSegmentationManager не найден.");
                  return;
            }

            if (string.IsNullOrEmpty(hexColor))
            {
                  Debug.LogError("Получена пустая строка цвета.");
                  return;
            }

            Color newColor;
            // Используем ColorUtility для парсинга HEX-строки
            if (ColorUtility.TryParseHtmlString(hexColor, out newColor))
            {
                  Debug.Log($"🎨 Получен цвет из Flutter: {hexColor}. Устанавливаем его для покраски.");
                  segmentationManager.SetPaintColor(newColor);
            }
            else
            {
                  Debug.LogError($"Не удалось распознать HEX-цвет: {hexColor}");
            }
      }
}
using UnityEngine;

public class UnityMessageBridge : MonoBehaviour
{
      [Tooltip("Ссылка на менеджер сегментации для управления цветом.")]
      [SerializeField]
      private AsyncSegmentationManager segmentationManager;

      void Start()
      {
            // Попытка найти менеджер автоматически, если он не назначен в инспекторе
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
      /// </summary>
      /// <param name="hexColor">Строка с цветом в HEX-формате (например, "#RRGGBB").</param>
      public void SetPaintColorFromHex(string hexColor)
      {
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
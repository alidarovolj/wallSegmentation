using UnityEngine;

public class UnityMessageBridge : MonoBehaviour
{
      // Ссылка на ваш SegmentationManager
      [SerializeField] private SegmentationManager segmentationManager;

      /// <summary>
      /// Этот метод будет вызываться из Flutter для установки цвета.
      /// Flutter передаст цвет в виде строки HTML (например, "#FF0000").
      /// </summary>
      /// <param name="colorString">Строка с цветом в формате HTML.</param>
      public void SetPaintColorFromString(string colorString)
      {
            if (segmentationManager == null)
            {
                  Debug.LogError("SegmentationManager не назначен в UnityMessageBridge!");
                  return;
            }

            Color newColor;
            if (ColorUtility.TryParseHtmlString(colorString, out newColor))
            {
                  segmentationManager.SetPaintColor(newColor);
                  Debug.Log($"[UnityMessageBridge] Цвет из Flutter установлен: {colorString}");
            }
            else
            {
                  Debug.LogError($"[UnityMessageBridge] Не удалось распознать цвет из строки: {colorString}");
            }
      }

      /// <summary>
      /// Этот метод будет вызываться из Flutter для выбора класса для покраски.
      /// Flutter передаст ID класса в виде строки.
      /// </summary>
      /// <param name="classIdString">ID класса в виде строки.</param>
      public void SetClassToPaint(string classIdString)
      {
            if (segmentationManager == null)
            {
                  Debug.LogError("SegmentationManager не назначен в UnityMessageBridge!");
                  return;
            }

            if (int.TryParse(classIdString, out int classId))
            {
                  // Здесь нам нужен новый метод в SegmentationManager, который мы сейчас добавим.
                  segmentationManager.SelectClassForPainting(classId);
                  Debug.Log($"[UnityMessageBridge] Класс для покраски из Flutter установлен: {classId}");
            }
            else
            {
                  Debug.LogError($"[UnityMessageBridge] Не удалось распознать ID класса из строки: {classIdString}");
            }
      }

      // В будущем здесь можно добавить методы для отправки сообщений во Flutter,
      // например, о том, какой класс был выбран тапом в Unity.
}
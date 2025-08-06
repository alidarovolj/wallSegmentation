using UnityEngine;
using UnityEngine.UI;

public class SimpleColorPicker : MonoBehaviour
{
      [Tooltip("The segmentation manager to control.")]
      public AsyncSegmentationManager segmentationManager;

      [Tooltip("A list of UI buttons that will act as color swatches.")]
      public Button[] colorButtons;

      void Start()
      {
            if (segmentationManager == null)
            {
                  // Попробуем найти автоматически, если не задано
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
                  if (segmentationManager == null)
                  {
                        Debug.LogError("SimpleColorPicker: AsyncSegmentationManager не найден на сцене!");
                        return;
                  }
            }

            // Назначаем обработчики кликов для каждой кнопки
            foreach (var button in colorButtons)
            {
                  // Получаем цвет самой кнопки
                  Color buttonColor = button.GetComponent<Image>().color;
                  button.onClick.AddListener(() => OnColorButtonClick(buttonColor));
            }
      }

      void OnColorButtonClick(Color color)
      {
            if (segmentationManager != null)
            {
                  Debug.Log($"🎨 Выбран цвет: {color}");
                  segmentationManager.SetPaintColor(color);
            }
      }
}
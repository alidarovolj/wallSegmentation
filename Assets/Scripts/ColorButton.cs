using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Компонент для кнопок палитры цветов
/// Автоматически настраивает onClick события для передачи цвета в UIManager
/// </summary>
[RequireComponent(typeof(Button), typeof(Image))]
public class ColorButton : MonoBehaviour
{
      private Button button;
      private Image image;
      private UIManager uiManager;

      void Start()
      {
            // Получаем компоненты
            button = GetComponent<Button>();
            image = GetComponent<Image>();
            uiManager = FindObjectOfType<UIManager>();

            if (uiManager == null)
            {
                  Debug.LogError("ColorButton: UIManager не найден в сцене!");
                  return;
            }

            // Настраиваем onClick событие
            button.onClick.AddListener(HandleClick);

            Debug.Log($"🎨 ColorButton настроена с цветом: {image.color}");
      }

      private void HandleClick()
      {
            if (uiManager != null)
            {
                  uiManager.OnColorButtonClicked(image.color);
            }
      }
}
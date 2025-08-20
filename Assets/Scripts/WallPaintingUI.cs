using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI для управления покраской отдельных стен
/// Простой интерфейс с кнопками для переключения режимов
/// </summary>
public class WallPaintingUI : MonoBehaviour
{
      [Header("UI Компоненты")]
      [SerializeField] private Button togglePaintingModeButton;
      [SerializeField] private Button nextColorButton;
      [SerializeField] private Button resetColorsButton;
      [SerializeField] private Text statusText;
      [SerializeField] private Text instructionText;

      [Header("Ссылки")]
      [SerializeField] private IndividualWallPainter wallPainter;

      private bool isPaintingMode = false;

      void Start()
      {
            // Автопоиск компонента если не назначен
            if (wallPainter == null)
                  wallPainter = FindObjectOfType<IndividualWallPainter>();

            // Настраиваем кнопки
            if (togglePaintingModeButton != null)
            {
                  togglePaintingModeButton.onClick.AddListener(TogglePaintingMode);
            }

            if (nextColorButton != null)
            {
                  nextColorButton.onClick.AddListener(NextColor);
            }

            if (resetColorsButton != null)
            {
                  resetColorsButton.onClick.AddListener(ResetColors);
            }

            UpdateUI();
      }

      /// <summary>
      /// Переключает режим покраски стен
      /// </summary>
      public void TogglePaintingMode()
      {
            if (wallPainter != null)
            {
                  wallPainter.TogglePaintingMode();
                  isPaintingMode = !isPaintingMode;
                  UpdateUI();
            }
      }

      /// <summary>
      /// Переключает на следующий цвет
      /// </summary>
      public void NextColor()
      {
            if (wallPainter != null)
            {
                  wallPainter.NextColor();
            }
      }

      /// <summary>
      /// Сбрасывает все цвета стен
      /// </summary>
      public void ResetColors()
      {
            if (wallPainter != null)
            {
                  wallPainter.ResetWallColors();
            }
      }

      /// <summary>
      /// Обновляет текст UI в зависимости от режима
      /// </summary>
      private void UpdateUI()
      {
            if (statusText != null)
            {
                  statusText.text = isPaintingMode ? "Режим покраски: ВКЛЮЧЕН" : "Режим покраски: ВЫКЛЮЧЕН";
                  statusText.color = isPaintingMode ? Color.green : Color.gray;
            }

            if (instructionText != null)
            {
                  if (isPaintingMode)
                  {
                        instructionText.text = "Кликните на стену чтобы покрасить ее";
                        instructionText.color = Color.white;
                  }
                  else
                  {
                        instructionText.text = "Нажмите 'Режим покраски' для начала";
                        instructionText.color = Color.gray;
                  }
            }

            // Активируем/деактивируем кнопки
            if (nextColorButton != null)
                  nextColorButton.interactable = isPaintingMode;

            if (resetColorsButton != null)
                  resetColorsButton.interactable = true; // Всегда доступна

            // Обновляем текст кнопки переключения режима
            if (togglePaintingModeButton != null)
            {
                  var buttonText = togglePaintingModeButton.GetComponentInChildren<Text>();
                  if (buttonText != null)
                  {
                        buttonText.text = isPaintingMode ? "Выключить покраску" : "Режим покраски";
                  }
            }
      }
}

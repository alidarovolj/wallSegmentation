using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Простой UI для легкой системы покраски стен
/// Минимальный интерфейс без тяжелых элементов
/// </summary>
public class SimpleWallPaintingUI : MonoBehaviour
{
      [Header("UI Элементы")]
      [SerializeField] private Button toggleButton;
      [SerializeField] private Button colorButton;
      [SerializeField] private Button resetButton;
      [SerializeField] private Text statusText;

      [Header("Ссылки")]
      [SerializeField] private LightweightWallPainter lightweightPainter;

      private bool isPaintingActive = false;

      void Start()
      {
            // Автопоиск если не назначен
            if (lightweightPainter == null)
                  lightweightPainter = FindObjectOfType<LightweightWallPainter>();

            // Настройка кнопок
            if (toggleButton != null)
                  toggleButton.onClick.AddListener(TogglePainting);

            if (colorButton != null)
                  colorButton.onClick.AddListener(NextColor);

            if (resetButton != null)
                  resetButton.onClick.AddListener(ResetColors);

            UpdateUI();
      }

      /// <summary>
      /// Переключает режим покраски
      /// </summary>
      public void TogglePainting()
      {
            if (lightweightPainter != null)
            {
                  lightweightPainter.TogglePaintingMode();
                  isPaintingActive = !isPaintingActive;
                  UpdateUI();
            }
      }

      /// <summary>
      /// Переключает на следующий цвет
      /// </summary>
      public void NextColor()
      {
            if (lightweightPainter != null)
            {
                  lightweightPainter.NextColor();
            }
      }

      /// <summary>
      /// Сбрасывает цвета
      /// </summary>
      public void ResetColors()
      {
            if (lightweightPainter != null)
            {
                  lightweightPainter.ResetWallColors();
            }
      }

      /// <summary>
      /// Обновляет состояние UI
      /// </summary>
      private void UpdateUI()
      {
            // Обновляем текст статуса
            if (statusText != null)
            {
                  statusText.text = isPaintingActive ? "🎨 ПОКРАСКА ВКЛЮЧЕНА" : "⭕ Покраска выключена";
                  statusText.color = isPaintingActive ? Color.green : Color.gray;
            }

            // Обновляем текст кнопки
            if (toggleButton != null)
            {
                  var buttonText = toggleButton.GetComponentInChildren<Text>();
                  if (buttonText != null)
                  {
                        buttonText.text = isPaintingActive ? "ВЫКЛЮЧИТЬ" : "ПОКРАСИТЬ СТЕНЫ";
                  }
            }

            // Активируем/деактивируем кнопки
            if (colorButton != null)
                  colorButton.interactable = isPaintingActive;
      }

      /// <summary>
      /// Быстрый тест - случайный цвет
      /// </summary>
      public void QuickTest()
      {
            if (lightweightPainter != null)
            {
                  lightweightPainter.QuickTestRandomColor();
            }
      }
}

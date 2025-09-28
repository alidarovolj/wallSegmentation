using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Простой UI для управления цветами в AR приложении.
/// Упрощенная версия после очистки проекта от SimpleColorManager.
/// </summary>
public class SimpleColorUI : MonoBehaviour
{
      [Header("UI Elements")]
      [SerializeField] private Button scanColorButton;
      [SerializeField] private Button resetButton;

      [Header("Color Display")]
      [SerializeField] private Image originalColorDisplay;
      [SerializeField] private Image paintColorDisplay;
      [SerializeField] private TextMeshProUGUI originalColorText;
      [SerializeField] private TextMeshProUGUI paintColorText;
      [SerializeField] private TextMeshProUGUI statusText;

      [Header("Dependencies")]
      [SerializeField] private ColorScanner colorScanner;

      // Простые цвета для демонстрации
      private Color originalColor = Color.white;
      private Color paintColor = Color.blue;

      void Start()
      {
            // Автопоиск зависимостей
            if (colorScanner == null)
                  colorScanner = FindObjectOfType<ColorScanner>();

            // Настройка кнопок
            SetupButtons();

            // Начальное обновление UI
            UpdateUI();
            UpdateStatus("✅ Готов к работе");
      }

      void SetupButtons()
      {
            if (scanColorButton != null)
                  scanColorButton.onClick.AddListener(() => ScanWallColor());

            if (resetButton != null)
                  resetButton.onClick.AddListener(() => ResetColors());
      }

      private void ScanWallColor()
      {
            if (colorScanner != null)
            {
                  // Запрос сканирования цвета
                  UpdateStatus("🔍 Сканирование цвета стены...");
                  // В реальной реализации здесь был бы вызов colorScanner.ScanColor()
            }
            else
            {
                  UpdateStatus("❌ ColorScanner не найден");
            }
      }

      private void ResetColors()
      {
            originalColor = Color.white;
            paintColor = Color.blue;
            UpdateUI();
            UpdateStatus("↩️ Цвета сброшены");
      }

      private void UpdateUI()
      {
            UpdateOriginalColorDisplay(originalColor);
            UpdatePaintColorDisplay(paintColor);
      }

      private void UpdateOriginalColorDisplay(Color color)
      {
            if (originalColorDisplay != null)
                  originalColorDisplay.color = color;

            if (originalColorText != null)
            {
                  string hex = ColorUtility.ToHtmlStringRGB(color);
                  originalColorText.text = $"Исходный: #{hex}";
            }
      }

      private void UpdatePaintColorDisplay(Color color)
      {
            if (paintColorDisplay != null)
                  paintColorDisplay.color = color;

            if (paintColorText != null)
            {
                  string hex = ColorUtility.ToHtmlStringRGB(color);
                  paintColorText.text = $"Покраска: #{hex}";
            }
      }

      private void UpdateStatus(string message)
      {
            if (statusText != null)
                  statusText.text = message;

            Debug.Log($"[SimpleColorUI] {message}");
      }

      // Публичные методы для внешнего управления цветами
      public void SetOriginalColor(Color color)
      {
            originalColor = color;
            UpdateOriginalColorDisplay(color);
      }

      public void SetPaintColor(Color color)
      {
            paintColor = color;
            UpdatePaintColorDisplay(color);
      }

      public Color GetOriginalColor() => originalColor;
      public Color GetPaintColor() => paintColor;
}
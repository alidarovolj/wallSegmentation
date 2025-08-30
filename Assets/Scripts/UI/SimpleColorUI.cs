using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Простой UI для управления двумя цветами: исходный цвет стены и цвет перекраски.
/// Без сложных каталогов - только основные функции.
/// </summary>
public class SimpleColorUI : MonoBehaviour
{
      [Header("UI Elements")]
      [SerializeField] private Button scanColorButton;
      [SerializeField] private Button nextColorButton;
      [SerializeField] private Button previousColorButton;
      [SerializeField] private Button swapColorsButton;
      [SerializeField] private Button resetButton;

      [Header("Color Display")]
      [SerializeField] private Image originalColorDisplay;
      [SerializeField] private Image paintColorDisplay;
      [SerializeField] private TextMeshProUGUI originalColorText;
      [SerializeField] private TextMeshProUGUI paintColorText;
      [SerializeField] private TextMeshProUGUI statusText;

      [Header("Color Presets")]
      [SerializeField] private Transform presetContainer;
      [SerializeField] private GameObject presetButtonPrefab;

      [Header("Dependencies")]
      [SerializeField] private SimpleColorManager colorManager;

      private Button[] presetButtons;

      void Start()
      {
            // Автопоиск зависимостей
            if (colorManager == null)
                  colorManager = FindObjectOfType<SimpleColorManager>();

            // Настройка кнопок
            SetupButtons();

            // Подписка на события
            SubscribeToEvents();

            // Создание пресетов
            CreatePresetButtons();

            // Начальное обновление UI
            UpdateUI();

            UpdateStatus("✅ Готов к работе");
      }

      void SetupButtons()
      {
            if (scanColorButton != null)
                  scanColorButton.onClick.AddListener(() => ScanWallColor());

            if (nextColorButton != null)
                  nextColorButton.onClick.AddListener(() => NextPaintColor());

            if (previousColorButton != null)
                  previousColorButton.onClick.AddListener(() => PreviousPaintColor());

            if (swapColorsButton != null)
                  swapColorsButton.onClick.AddListener(() => SwapColors());

            if (resetButton != null)
                  resetButton.onClick.AddListener(() => ResetToOriginal());
      }

      void SubscribeToEvents()
      {
            if (colorManager != null)
            {
                  colorManager.OnOriginalColorChanged += OnOriginalColorChanged;
                  colorManager.OnPaintColorChanged += OnPaintColorChanged;
                  colorManager.OnColorsSet += OnColorsSet;
            }
      }

      void CreatePresetButtons()
      {
            if (colorManager == null || presetContainer == null || presetButtonPrefab == null)
                  return;

            var presets = colorManager.GetPaintPresets();
            presetButtons = new Button[presets.Length];

            // Очищаем контейнер
            foreach (Transform child in presetContainer)
            {
                  DestroyImmediate(child.gameObject);
            }

            // Создаем кнопки пресетов
            for (int i = 0; i < presets.Length; i++)
            {
                  var buttonObj = Instantiate(presetButtonPrefab, presetContainer);
                  var button = buttonObj.GetComponent<Button>();
                  var image = buttonObj.GetComponent<Image>();

                  if (button != null && image != null)
                  {
                        // Устанавливаем цвет кнопки
                        image.color = presets[i];

                        // Настраиваем callback
                        int index = i; // захватываем индекс для замыкания
                        button.onClick.AddListener(() => SelectPreset(index));

                        presetButtons[i] = button;
                  }
            }

            Debug.Log($"🎨 Создано {presets.Length} пресетов цветов");
      }

      #region Button Handlers

      public void ScanWallColor()
      {
            if (colorManager != null)
            {
                  colorManager.ScanWallColor();
                  UpdateStatus("🔍 Сканирование цвета стены...");
            }
      }

      public void NextPaintColor()
      {
            if (colorManager != null)
            {
                  colorManager.NextPaintColor();
                  UpdateStatus("➡️ Следующий цвет");
            }
      }

      public void PreviousPaintColor()
      {
            if (colorManager != null)
            {
                  colorManager.PreviousPaintColor();
                  UpdateStatus("⬅️ Предыдущий цвет");
            }
      }

      public void SwapColors()
      {
            if (colorManager != null)
            {
                  colorManager.SwapColors();
                  UpdateStatus("🔄 Цвета поменяны местами");
            }
      }

      public void ResetToOriginal()
      {
            if (colorManager != null)
            {
                  colorManager.ResetToOriginal();
                  UpdateStatus("↩️ Сброс к исходному цвету");
            }
      }

      public void SelectPreset(int presetIndex)
      {
            if (colorManager != null)
            {
                  colorManager.SetPaintColorPreset(presetIndex);
                  UpdatePresetSelection();
                  UpdateStatus($"🎨 Выбран цвет #{presetIndex + 1}");
            }
      }

      #endregion

      #region Event Handlers

      private void OnOriginalColorChanged(Color newColor)
      {
            UpdateOriginalColorDisplay(newColor);
            UpdateStatus($"🔍 Цвет стены: #{ColorUtility.ToHtmlStringRGB(newColor)}");
      }

      private void OnPaintColorChanged(Color newColor)
      {
            UpdatePaintColorDisplay(newColor);
            UpdatePresetSelection();
            UpdateStatus($"🎨 Цвет краски: #{ColorUtility.ToHtmlStringRGB(newColor)}");
      }

      private void OnColorsSet(Color original, Color paint)
      {
            UpdateOriginalColorDisplay(original);
            UpdatePaintColorDisplay(paint);
            UpdatePresetSelection();
      }

      #endregion

      #region UI Updates

      void UpdateUI()
      {
            if (colorManager != null)
            {
                  var (original, paint) = colorManager.GetCurrentColors();
                  UpdateOriginalColorDisplay(original);
                  UpdatePaintColorDisplay(paint);
                  UpdatePresetSelection();
            }
      }

      void UpdateOriginalColorDisplay(Color color)
      {
            if (originalColorDisplay != null)
                  originalColorDisplay.color = color;

            if (originalColorText != null)
                  originalColorText.text = $"Стена\n#{ColorUtility.ToHtmlStringRGB(color)}";
      }

      void UpdatePaintColorDisplay(Color color)
      {
            if (paintColorDisplay != null)
                  paintColorDisplay.color = color;

            if (paintColorText != null)
                  paintColorText.text = $"Краска\n#{ColorUtility.ToHtmlStringRGB(color)}";
      }

      void UpdatePresetSelection()
      {
            if (colorManager == null || presetButtons == null) return;

            int currentIndex = colorManager.GetCurrentPresetIndex();

            // Обновляем визуальное выделение пресетов
            for (int i = 0; i < presetButtons.Length; i++)
            {
                  if (presetButtons[i] != null)
                  {
                        // Можно добавить визуальное выделение выбранного пресета
                        var outline = presetButtons[i].GetComponent<Outline>();
                        if (outline != null)
                        {
                              outline.enabled = (i == currentIndex);
                        }
                  }
            }
      }

      void UpdateStatus(string message)
      {
            if (statusText != null)
            {
                  statusText.text = message;
            }
            Debug.Log($"[SimpleColorUI] {message}");
      }

      #endregion

      #region Public API for Flutter

      /// <summary>
      /// Flutter API: Сканирует цвет стены
      /// </summary>
      public void FlutterScanColor(string unused = "")
      {
            ScanWallColor();
      }

      /// <summary>
      /// Flutter API: Устанавливает цвет перекраски по HEX
      /// </summary>
      public void FlutterSetPaintColor(string hexColor)
      {
            if (colorManager != null)
            {
                  colorManager.SetPaintColorFromHex(hexColor);
            }
      }

      /// <summary>
      /// Flutter API: Переключает на следующий цвет
      /// </summary>
      public void FlutterNextColor(string unused = "")
      {
            NextPaintColor();
      }

      /// <summary>
      /// Flutter API: Получает текущие цвета для Flutter
      /// </summary>
      public void FlutterGetCurrentColors(string unused = "")
      {
            if (colorManager != null)
            {
                  colorManager.FlutterGetColors();
            }
      }

      #endregion

      #region Context Menu (для тестирования)

      [ContextMenu("Test Scan Color")]
      void TestScanColor() => ScanWallColor();

      [ContextMenu("Test Next Color")]
      void TestNextColor() => NextPaintColor();

      [ContextMenu("Test Swap Colors")]
      void TestSwapColors() => SwapColors();

      [ContextMenu("Update UI")]
      void TestUpdateUI() => UpdateUI();

      #endregion
}

using UnityEngine;
using System;

/// <summary>
/// Простая система управления цветами с двумя основными цветами:
/// - Исходный цвет стен (определяется сканированием)
/// - Цвет перекраски (выбирается пользователем)
/// 
/// Без сложных каталогов и поиска - максимально простое решение.
/// </summary>
public class SimpleColorManager : MonoBehaviour
{
      [Header("Basic Colors")]
      [SerializeField] private Color originalWallColor = Color.white;
      [SerializeField] private Color paintColor = Color.blue;

      [Header("Color Presets")]
      [SerializeField]
      private Color[] paintPresets = new Color[]
      {
        new Color(0.2f, 0.4f, 0.8f),  // Синий
        new Color(0.8f, 0.2f, 0.2f),  // Красный  
        new Color(0.2f, 0.7f, 0.3f),  // Зеленый
        new Color(1.0f, 0.8f, 0.0f),  // Желтый
        new Color(0.6f, 0.3f, 0.8f),  // Фиолетовый
        new Color(0.9f, 0.6f, 0.2f),  // Оранжевый
        new Color(0.5f, 0.5f, 0.5f),  // Серый
        new Color(0.95f, 0.95f, 0.95f) // Белый
      };

      [Header("Dependencies")]
      [SerializeField] private ColorScanner colorScanner;
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private ARWallPresenter wallPresenter;

      [Header("Settings")]
      [SerializeField] private bool enableDebugLogs = true;
      [SerializeField] private bool autoApplyScannedColor = true;

      // События
      public event Action<Color> OnOriginalColorChanged;
      public event Action<Color> OnPaintColorChanged;
      public event Action<Color, Color> OnColorsSet; // (original, paint)

      // Состояние
      private int currentPresetIndex = 0;
      private bool isScanning = false;

      void Awake()
      {
            // Автопоиск зависимостей
            if (colorScanner == null)
                  colorScanner = FindObjectOfType<ColorScanner>();
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            if (wallPresenter == null)
                  wallPresenter = FindObjectOfType<ARWallPresenter>();
      }

      void Start()
      {
            // Подписываемся на события сканирования
            if (colorScanner != null)
            {
                  colorScanner.OnColorScanned += OnColorScannedHandler;
                  colorScanner.OnScanError += OnScanError;
            }

            // Применяем начальные цвета
            ApplyColors();

            if (enableDebugLogs)
                  Debug.Log($"🎨 SimpleColorManager инициализирован. Исходный: {ColorToHex(originalWallColor)}, Краска: {ColorToHex(paintColor)}");
      }

      #region Public API

      /// <summary>
      /// Сканирует цвет стены в центре экрана
      /// </summary>
      public void ScanWallColor()
      {
            if (colorScanner != null && !isScanning)
            {
                  Vector2 centerPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
                  colorScanner.ScanColorAtScreenPos(centerPos);
                  isScanning = true;

                  if (enableDebugLogs)
                        Debug.Log("🔍 Сканирование цвета стены...");
            }
      }

      /// <summary>
      /// Сканирует цвет в указанной позиции экрана
      /// </summary>
      public void ScanWallColorAt(Vector2 screenPosition)
      {
            if (colorScanner != null && !isScanning)
            {
                  colorScanner.ScanColorAtScreenPos(screenPosition);
                  isScanning = true;

                  if (enableDebugLogs)
                        Debug.Log($"🔍 Сканирование цвета в позиции: {screenPosition}");
            }
      }

      /// <summary>
      /// Устанавливает цвет перекраски
      /// </summary>
      public void SetPaintColor(Color color)
      {
            paintColor = color;
            ApplyColors();
            OnPaintColorChanged?.Invoke(paintColor);

            if (enableDebugLogs)
                  Debug.Log($"🎨 Установлен цвет перекраски: {ColorToHex(paintColor)}");
      }

      /// <summary>
      /// Устанавливает цвет перекраски из HEX строки
      /// </summary>
      public void SetPaintColorFromHex(string hexColor)
      {
            if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                  SetPaintColor(color);
            }
            else
            {
                  Debug.LogError($"❌ Неверный HEX цвет: {hexColor}");
            }
      }

      /// <summary>
      /// Переключается на следующий цвет из пресетов
      /// </summary>
      public void NextPaintColor()
      {
            if (paintPresets.Length > 0)
            {
                  currentPresetIndex = (currentPresetIndex + 1) % paintPresets.Length;
                  SetPaintColor(paintPresets[currentPresetIndex]);
            }
      }

      /// <summary>
      /// Переключается на предыдущий цвет из пресетов
      /// </summary>
      public void PreviousPaintColor()
      {
            if (paintPresets.Length > 0)
            {
                  currentPresetIndex = (currentPresetIndex - 1 + paintPresets.Length) % paintPresets.Length;
                  SetPaintColor(paintPresets[currentPresetIndex]);
            }
      }

      /// <summary>
      /// Устанавливает конкретный пресет по индексу
      /// </summary>
      public void SetPaintColorPreset(int presetIndex)
      {
            if (presetIndex >= 0 && presetIndex < paintPresets.Length)
            {
                  currentPresetIndex = presetIndex;
                  SetPaintColor(paintPresets[currentPresetIndex]);
            }
      }

      /// <summary>
      /// Меняет местами исходный цвет и цвет перекраски
      /// </summary>
      public void SwapColors()
      {
            var temp = originalWallColor;
            originalWallColor = paintColor;
            paintColor = temp;

            ApplyColors();
            OnOriginalColorChanged?.Invoke(originalWallColor);
            OnPaintColorChanged?.Invoke(paintColor);

            if (enableDebugLogs)
                  Debug.Log($"🔄 Цвета поменяны местами. Исходный: {ColorToHex(originalWallColor)}, Краска: {ColorToHex(paintColor)}");
      }

      /// <summary>
      /// Сбрасывает к исходному цвету (убирает перекраску)
      /// </summary>
      public void ResetToOriginal()
      {
            if (segmentationManager != null)
            {
                  // TODO: Интегрировать с AsyncSegmentationManager для сброса цвета
                  // segmentationManager.ResetWallColor();
            }

            if (enableDebugLogs)
                  Debug.Log("↩️ Сброс к исходному цвету стены");
      }

      /// <summary>
      /// Получает текущие цвета
      /// </summary>
      public (Color original, Color paint) GetCurrentColors()
      {
            return (originalWallColor, paintColor);
      }

      /// <summary>
      /// Получает доступные пресеты
      /// </summary>
      public Color[] GetPaintPresets()
      {
            return (Color[])paintPresets.Clone();
      }

      /// <summary>
      /// Получает текущий индекс пресета
      /// </summary>
      public int GetCurrentPresetIndex()
      {
            return currentPresetIndex;
      }

      /// <summary>
      /// Свойство для получения текущего индекса пресета
      /// </summary>
      public int CurrentPresetIndex => currentPresetIndex;

      /// <summary>
      /// Сбрасывает цвета к значениям по умолчанию
      /// </summary>
      public void ResetColors()
      {
            if (enableDebugLogs)
                  Debug.Log("🔄 Сброс цветов к значениям по умолчанию");

            originalWallColor = Color.white;
            paintColor = paintPresets[0]; // Синий
            currentPresetIndex = 0;

            ApplyColors();

            OnOriginalColorChanged?.Invoke(originalWallColor);
            OnPaintColorChanged?.Invoke(paintColor);
            OnColorsSet?.Invoke(originalWallColor, paintColor);
      }

      #endregion

      #region Private Methods

      private void OnColorScannedHandler(Color scannedColor, Vector2 position)
      {
            isScanning = false;

            if (autoApplyScannedColor)
            {
                  originalWallColor = scannedColor;
                  ApplyColors();
                  OnOriginalColorChanged?.Invoke(originalWallColor);

                  if (enableDebugLogs)
                  {
                        Debug.Log($"🎨 Отсканирован цвет стены: {ColorToHex(originalWallColor)}");
                  }
            }
            else
            {
                  if (enableDebugLogs)
                        Debug.Log($"⚠️ Ненадежный результат сканирования: {ColorToHex(scannedColor)}. Используйте SetOriginalColor() для принудительной установки.");
            }
      }

      private void OnScanError(string error)
      {
            isScanning = false;
            Debug.LogError($"❌ Ошибка сканирования цвета: {error}");
      }

      private void ApplyColors()
      {
            // Применяем цвета к системе сегментации
            if (segmentationManager != null)
            {
                  // TODO: Интегрировать с AsyncSegmentationManager
                  // segmentationManager.SetOriginalWallColor(originalWallColor);
                  // segmentationManager.SetPaintColor(paintColor);
            }

            // Применяем к презентеру стен
            if (wallPresenter != null)
            {
                  // TODO: Интегрировать с ARWallPresenter
                  // wallPresenter.SetWallColor(paintColor);
            }

            // Уведомляем о изменении
            OnColorsSet?.Invoke(originalWallColor, paintColor);
      }

      private string ColorToHex(Color color)
      {
            return ColorUtility.ToHtmlStringRGB(color);
      }

      #endregion

      #region Context Menu (для тестирования в редакторе)

      [ContextMenu("Scan Wall Color")]
      void ContextScanWallColor() => ScanWallColor();

      [ContextMenu("Next Paint Color")]
      void ContextNextPaintColor() => NextPaintColor();

      [ContextMenu("Previous Paint Color")]
      void ContextPreviousPaintColor() => PreviousPaintColor();

      [ContextMenu("Swap Colors")]
      void ContextSwapColors() => SwapColors();

      [ContextMenu("Reset To Original")]
      void ContextResetToOriginal() => ResetToOriginal();

      [ContextMenu("Log Current Colors")]
      void ContextLogColors()
      {
            Debug.Log($"🎨 Текущие цвета - Исходный: #{ColorToHex(originalWallColor)}, Краска: #{ColorToHex(paintColor)}");
      }

      #endregion

      #region Flutter Integration

      /// <summary>
      /// Flutter API: Сканирует цвет стены
      /// </summary>
      public void FlutterScanWallColor(string position = "{\"x\": 0.5, \"y\": 0.5}")
      {
            try
            {
                  var pos = JsonUtility.FromJson<Vector2Wrapper>(position);
                  Vector2 screenPos = new Vector2(pos.x * Screen.width, pos.y * Screen.height);
                  ScanWallColorAt(screenPos);
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка Flutter API ScanWallColor: {e.Message}");
            }
      }

      /// <summary>
      /// Flutter API: Устанавливает цвет перекраски
      /// </summary>
      public void FlutterSetPaintColor(string hexColor)
      {
            SetPaintColorFromHex(hexColor);
      }

      /// <summary>
      /// Flutter API: Переключает на следующий цвет
      /// </summary>
      public void FlutterNextColor(string unused = "")
      {
            NextPaintColor();
      }

      /// <summary>
      /// Flutter API: Меняет цвета местами
      /// </summary>
      public void FlutterSwapColors(string unused = "")
      {
            SwapColors();
      }

      /// <summary>
      /// Flutter API: Получает текущие цвета
      /// </summary>
      public void FlutterGetColors(string unused = "")
      {
            var response = new
            {
                  originalColor = $"#{ColorToHex(originalWallColor)}",
                  paintColor = $"#{ColorToHex(paintColor)}",
                  presets = System.Array.ConvertAll(paintPresets, color => $"#{ColorToHex(color)}"),
                  currentPresetIndex = currentPresetIndex
            };

            // Отправляем во Flutter
            var flutterBridge = FindObjectOfType<FlutterUnityBridge>();
            if (flutterBridge != null)
            {
                  // TODO: Добавить SendMessage метод
                  Debug.Log($"📱 Colors data for Flutter: {JsonUtility.ToJson(response)}");
            }
      }

      [System.Serializable]
      private struct Vector2Wrapper
      {
            public float x;
            public float y;
      }

      #endregion
}

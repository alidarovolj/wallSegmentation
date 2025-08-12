using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Инструмент отладки для диагностики проблем с точностью сегментации
/// </summary>
public class SegmentationDebugger : MonoBehaviour
{
      [Header("Отладочные компоненты")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private RawImage debugDisplay;
      [SerializeField] private TextMeshProUGUI debugText;
      [SerializeField] private Button maxAccuracyButton;
      [SerializeField] private Button balancedModeButton;
      [SerializeField] private Button toggleSmoothingButton;

      [Header("Настройки отладки")]
      [SerializeField] private bool showDebugInfo = true;
      [SerializeField] private bool showRawMaskValues = false;
      [SerializeField] private float updateInterval = 1.0f;

      private float lastUpdateTime;
      private int frameCount;
      private float fps;

      void Start()
      {
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            SetupButtons();

            if (debugText != null && showDebugInfo)
            {
                  debugText.text = "Отладка сегментации активна\nПроверьте точность на людях в кадре";
            }
      }

      void Update()
      {
            if (showDebugInfo && Time.time - lastUpdateTime > updateInterval)
            {
                  UpdateDebugInfo();
                  lastUpdateTime = Time.time;
            }

            // Отладка по клику: показываем детальную информацию о точке
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                  DebugClickPosition(Input.mousePosition);
            }

            frameCount++;
            if (Time.time > 0)
                  fps = frameCount / Time.time;
      }

      private void SetupButtons()
      {
            if (maxAccuracyButton != null)
            {
                  maxAccuracyButton.onClick.AddListener(() =>
                  {
                        if (segmentationManager != null)
                        {
                              segmentationManager.EnableMaxAccuracyMode();
                              UpdateDebugText("🎯 РЕЖИМ МАКСИМАЛЬНОЙ ТОЧНОСТИ АКТИВИРОВАН");
                        }
                  });
            }

            if (balancedModeButton != null)
            {
                  balancedModeButton.onClick.AddListener(() =>
                  {
                        if (segmentationManager != null)
                        {
                              segmentationManager.EnableBalancedMode();
                              UpdateDebugText("⚖️ СБАЛАНСИРОВАННЫЙ РЕЖИМ АКТИВИРОВАН");
                        }
                  });
            }

            if (toggleSmoothingButton != null)
            {
                  toggleSmoothingButton.onClick.AddListener(() =>
                  {
                        if (segmentationManager != null)
                        {
                              segmentationManager.ToggleSmoothing();
                              UpdateDebugText("🔄 СГЛАЖИВАНИЕ ПЕРЕКЛЮЧЕНО");
                        }
                  });
            }
      }

      private void UpdateDebugInfo()
      {
            if (debugText == null || segmentationManager == null) return;

            var mask = segmentationManager.GetCurrentSegmentationMask();
            string debugInfo = $"ОТЛАДКА СЕГМЕНТАЦИИ\n";
            debugInfo += $"FPS: {fps:F1}\n";
            debugInfo += $"Маска: {(mask != null ? $"{mask.width}x{mask.height}" : "НЕТ")}\n";
            debugInfo += $"Экран: {Screen.width}x{Screen.height}\n";
            debugInfo += $"Ориентация: {(Screen.height > Screen.width ? "Портрет" : "Ландшафт")}\n";

            if (mask != null)
            {
                  debugInfo += $"Формат маски: {mask.format}\n";
                  debugInfo += $"Готова: {(segmentationManager.IsSegmentationMaskReady() ? "ДА" : "НЕТ")}\n";
            }

            debugInfo += "\n🎯 СОВЕТЫ ПО ТОЧНОСТИ:\n";
            debugInfo += "• Люди должны быть КРАСНЫМИ\n";
            debugInfo += "• Стены должны быть СЕРЫМИ\n";
            debugInfo += "• Если люди синие → проблема с моделью\n";
            debugInfo += "• Используйте кнопки для настройки\n";

            debugText.text = debugInfo;
      }

      private void DebugClickPosition(Vector2 screenPos)
      {
            if (segmentationManager == null) return;

            Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            string clickInfo = $"КЛИК: ({screenPos.x:F0}, {screenPos.y:F0})\n";
            clickInfo += $"UV: ({screenUV.x:F3}, {screenUV.y:F3})\n";
            clickInfo += $"Проверьте консоль для детальной информации о классе";

            UpdateDebugText(clickInfo);

            Debug.Log($"🔍 ОТЛАДКА КЛИКА: экранные координаты {screenPos}, UV {screenUV}");
      }

      private void UpdateDebugText(string message)
      {
            if (debugText != null)
            {
                  debugText.text = message + "\n\n" + debugText.text.Split('\n')[0];
            }
            Debug.Log($"🔧 ОТЛАДЧИК СЕГМЕНТАЦИИ: {message}");
      }

      /// <summary>
      /// Включить/выключить режим отображения сырых значений маски
      /// </summary>
      [ContextMenu("Переключить показ сырых значений")]
      public void ToggleRawValues()
      {
            showRawMaskValues = !showRawMaskValues;

            // Устанавливаем параметр в материале визуализации
            if (segmentationManager != null)
            {
                  var material = segmentationManager.GetComponent<RawImage>()?.material;
                  if (material != null)
                  {
                        material.SetFloat("_ShowRawValues", showRawMaskValues ? 1.0f : 0.0f);
                  }
            }

            UpdateDebugText($"Сырые значения маски: {(showRawMaskValues ? "ВКЛЮЧЕНЫ" : "ВЫКЛЮЧЕНЫ")}");
      }

      /// <summary>
      /// Принудительное обновление для диагностики
      /// </summary>
      [ContextMenu("Принудительная диагностика")]
      public void ForceDiagnostics()
      {
            if (segmentationManager != null)
            {
                  Debug.Log("🔍 ДИАГНОСТИКА СЕГМЕНТАЦИИ:");
                  Debug.Log($"  - Маска готова: {segmentationManager.IsSegmentationMaskReady()}");

                  var mask = segmentationManager.GetCurrentSegmentationMask();
                  if (mask != null)
                  {
                        Debug.Log($"  - Размер маски: {mask.width}x{mask.height}");
                        Debug.Log($"  - Формат: {mask.format}");
                        Debug.Log($"  - Создана: {mask.IsCreated()}");
                  }
                  else
                  {
                        Debug.LogWarning("  - МАСКА НЕ ДОСТУПНА!");
                  }

                  UpdateDebugText("Диагностика выполнена - проверьте консоль");
            }
      }

      void OnGUI()
      {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.BeginVertical("box");

            GUILayout.Label("БЫСТРЫЕ КОМАНДЫ ОТЛАДКИ:");

            if (GUILayout.Button("🎯 Максимальная точность"))
            {
                  segmentationManager?.EnableMaxAccuracyMode();
            }

            if (GUILayout.Button("⚖️ Сбалансированный режим"))
            {
                  segmentationManager?.EnableBalancedMode();
            }

            if (GUILayout.Button("👁️ Показать сырые значения"))
            {
                  ToggleRawValues();
            }

            if (GUILayout.Button("🔄 Принудительное обновление"))
            {
                  segmentationManager?.RefreshScreenCoverage();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
      }
}

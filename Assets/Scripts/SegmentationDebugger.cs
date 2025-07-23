using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Отладочный скрипт для проверки работы сегментации
/// Показывает информацию о текстуре сегментации и глобальных свойствах
/// </summary>
public class SegmentationDebugger : MonoBehaviour
{
      [Header("UI References")]
      [SerializeField] private RawImage debugDisplay;
      [SerializeField] private Text debugText;

      [Header("Components")]
      [SerializeField] private SegmentationManager segmentationManager;
      [SerializeField] private AsyncSegmentationManager asyncSegmentationManager;

      private RenderTexture currentSegmentationTexture;

      void Update()
      {
            UpdateDebugInfo();
      }

      void UpdateDebugInfo()
      {
            string debugInfo = "=== SEGMENTATION DEBUG ===\n";

            // NEW: Проверяем наличие обоих менеджеров и выводим предупреждение
            bool oldManagerActive = segmentationManager != null && segmentationManager.isActiveAndEnabled;
            bool newManagerActive = asyncSegmentationManager != null && asyncSegmentationManager.isActiveAndEnabled;

            if (oldManagerActive && newManagerActive)
            {
                  Debug.LogWarning("⚠️ ОБА МЕНЕДЖЕРА (старый и новый) АКТИВНЫ В СЦЕНЕ. Это может вызвать конфликты. Рекомендуется оставить только один.");
            }

            // Определяем, какой менеджер реально работает и какую текстуру использовать
            RenderTexture activeTexture = null;
            string activeManagerName = "None";

            if (oldManagerActive && segmentationManager.GetSegmentationTexture() != null)
            {
                  activeTexture = segmentationManager.GetSegmentationTexture();
                  activeManagerName = "SegmentationManager (старый)";
            }
            else if (newManagerActive && asyncSegmentationManager.GetSegmentationTexture() != null)
            {
                  activeTexture = asyncSegmentationManager.GetSegmentationTexture();
                  activeManagerName = "AsyncSegmentationManager (новый)";
            }

            debugInfo += $"Active Manager: {activeManagerName}\n";
            debugInfo += $"Active Texture: {(activeTexture != null ? $"{activeTexture.width}x{activeTexture.height}" : "NULL")}\n";

            // Проверяем глобальные свойства
            var globalSegmentationTexture = Shader.GetGlobalTexture("_GlobalSegmentationTexture");
            debugInfo += $"Global Shader Texture: {(globalSegmentationTexture != null ? $"{globalSegmentationTexture.width}x{globalSegmentationTexture.height}" : "NULL")}\n";

            // --- АВТО-ИСПРАВЛЕНИЕ ---
            if (activeTexture != null)
            {
                  // 1. Исправляем RawImage, если его текстура не совпадает с активной
                  if (debugDisplay != null && debugDisplay.texture != activeTexture)
                  {
                        debugDisplay.texture = activeTexture;
                        Debug.Log($"[Auto-Fix] Назначена правильная текстура ({activeTexture.width}x{activeTexture.height}) на {debugDisplay.name} от {activeManagerName}.");
                  }

                  // 2. Исправляем глобальное свойство шейдера, если оно не совпадает
                  if (globalSegmentationTexture != activeTexture)
                  {
                        Shader.SetGlobalTexture("_GlobalSegmentationTexture", activeTexture);
                        Debug.Log($"[Auto-Fix] Установлена правильная глобальная текстура шейдера от {activeManagerName}.");
                  }
            }

            if (debugText != null)
            {
                  debugText.text = debugInfo;
            }
            else
            {
                  if (Time.frameCount % 120 == 0) // Выводим в консоль раз в 2 секунды
                  {
                        Debug.Log(debugInfo);
                  }
            }
      }

      [ContextMenu("Force Update Segmentation Display")]
      public void ForceUpdateSegmentationDisplay()
      {
            Debug.Log("🔍 Force updating segmentation display...");

            // Ищем все RawImage в сцене и проверяем их текстуры
            var allRawImages = FindObjectsOfType<RawImage>();

            foreach (var rawImage in allRawImages)
            {
                  Debug.Log($"RawImage '{rawImage.name}': texture = {rawImage.texture}");

                  if (rawImage.name.Contains("Segmentation") || rawImage.gameObject.name.Contains("Segmentation"))
                  {
                        // Пытаемся обновить текстуру
                        if (currentSegmentationTexture != null)
                        {
                              rawImage.texture = currentSegmentationTexture;
                              Debug.Log($"✅ Updated {rawImage.name} with segmentation texture");
                        }
                  }
            }
      }

      [ContextMenu("Test Global Properties")]
      public void TestGlobalProperties()
      {
            Debug.Log("🧪 Testing global shader properties...");

            // Устанавливаем тестовую текстуру
            var testTexture = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32);
            testTexture.enableRandomWrite = true;
            testTexture.Create();

            // Заполняем тестовой текстурой красным цветом
            RenderTexture.active = testTexture;
            GL.Clear(true, true, Color.red);
            RenderTexture.active = null;

            Shader.SetGlobalTexture("_GlobalSegmentationTexture", testTexture);
            Debug.Log("✅ Set test red texture as global segmentation texture");

            // Обновляем display
            if (debugDisplay != null)
            {
                  debugDisplay.texture = testTexture;
            }

            // Убираем через 3 секунды
            Invoke(nameof(ClearTestTexture), 3f);
      }

      void ClearTestTexture()
      {
            Debug.Log("🧹 Clearing test texture");
            Shader.SetGlobalTexture("_GlobalSegmentationTexture", currentSegmentationTexture);

            if (debugDisplay != null && currentSegmentationTexture != null)
            {
                  debugDisplay.texture = currentSegmentationTexture;
            }
      }
}
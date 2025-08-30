using UnityEngine;

/// <summary>
/// Быстрое исправление ориентации маски - убирает отзеркаливание
/// </summary>
public class MaskOrientationFix : MonoBehaviour
{
      [Header("Dependencies")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;

      [Header("Orientation Settings")]
      [Tooltip("Rotation Mode: 0=+90°, 1=-90°, 2=180°, 3=No rotation")]
      [SerializeField, Range(0, 3)] private int rotationMode = 3; // No rotation by default

      [Tooltip("Flip horizontally (causes mirroring issue)")]
      [SerializeField] private bool flipHorizontal = false; // Disabled by default

      [Header("Quick Fixes")]
      [SerializeField] private bool applyOnStart = true;

      // Reflection info for setting private fields
      private System.Reflection.FieldInfo maskRotationModeField;
      private System.Reflection.FieldInfo flipHorizontalField;

      void Start()
      {
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            InitializeReflection();

            if (applyOnStart)
            {
                  ApplyOrientationFix();
            }
      }

      private void InitializeReflection()
      {
            var type = typeof(AsyncSegmentationManager);

            maskRotationModeField = type.GetField("maskRotationMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            flipHorizontalField = type.GetField("flipHorizontal",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      }

      /// <summary>
      /// Применяет исправление ориентации маски
      /// </summary>
      [ContextMenu("Apply Orientation Fix")]
      public void ApplyOrientationFix()
      {
            if (segmentationManager == null)
            {
                  Debug.LogError("❌ AsyncSegmentationManager не найден!");
                  return;
            }

            Debug.Log("🔧 === ИСПРАВЛЕНИЕ ОРИЕНТАЦИИ МАСКИ ===");
            Debug.Log($"   Старые настройки: Поворот={GetCurrentRotationMode()}, Отражение={GetCurrentFlipHorizontal()}");

            // Устанавливаем новые значения
            SetRotationMode(rotationMode);
            SetFlipHorizontal(flipHorizontal);

            Debug.Log($"   Новые настройки: Поворот={rotationMode}, Отражение={flipHorizontal}");
            Debug.Log("✅ Исправление применено! Маска больше не должна быть отзеркалена.");
      }

      /// <summary>
      /// Быстрое исправление: отключить отзеркаливание
      /// </summary>
      [ContextMenu("Fix Mirroring - No Flip")]
      public void FixMirroring()
      {
            rotationMode = 3;     // No rotation
            flipHorizontal = false; // No horizontal flip
            ApplyOrientationFix();
      }

      /// <summary>
      /// Альтернативное исправление: только поворот на 180°
      /// </summary>
      [ContextMenu("Fix Mirroring - 180° Only")]
      public void FixMirroring180()
      {
            rotationMode = 2;     // 180° rotation
            flipHorizontal = false; // No horizontal flip
            ApplyOrientationFix();
      }

      /// <summary>
      /// Новое исправление: Компенсирует вертикальный переворот (180° rot + H-Flip)
      /// </summary>
      [ContextMenu("Fix Vertical Flip (Recommended)")]
      public void FixVerticalFlip()
      {
            rotationMode = 2;     // 180° rotation
            flipHorizontal = true; // + Horizontal flip = Vertical flip
            ApplyOrientationFix();
      }

      /// <summary>
      /// Тест: Поворот на -90 градусов, как предложил пользователь
      /// </summary>
      [ContextMenu("Test: Rotate -90 Degrees")]
      public void RotateMinus90()
      {
            rotationMode = 1; // -90 градусов
            flipHorizontal = false;
            ApplyOrientationFix();
      }

      /// <summary>
      /// Сброс к заводским настройкам SegFormer
      /// </summary>
      [ContextMenu("Reset to SegFormer Defaults")]
      public void ResetToSegFormerDefaults()
      {
            rotationMode = 3;     // No rotation for SegFormer
            flipHorizontal = false; // No flip for SegFormer
            ApplyOrientationFix();
      }

      /// <summary>
      /// Тест всех комбинаций поочередно
      /// </summary>
      [ContextMenu("Test All Orientations")]
      public void TestAllOrientations()
      {
            StartCoroutine(TestOrientationsSequentially());
      }

      private System.Collections.IEnumerator TestOrientationsSequentially()
      {
            Debug.Log("🧪 Тестирование всех ориентаций...");

            // Test different rotation modes
            for (int rotation = 0; rotation <= 3; rotation++)
            {
                  for (int flip = 0; flip <= 1; flip++)
                  {
                        bool flipBool = flip == 1;
                        Debug.Log($"🧪 Тестируем: Поворот={rotation}, Отражение={flipBool}");

                        rotationMode = rotation;
                        flipHorizontal = flipBool;
                        ApplyOrientationFix();

                        yield return new WaitForSeconds(3f); // 3 seconds each
                  }
            }

            Debug.Log("✅ Тестирование завершено. Возвращаемся к правильным настройкам.");
            FixMirroring(); // Apply correct settings
      }

      #region Private Methods

      private void SetRotationMode(int mode)
      {
            if (maskRotationModeField != null)
            {
                  maskRotationModeField.SetValue(segmentationManager, mode);
            }
            else
            {
                  Debug.LogWarning("⚠️ Не удалось установить режим поворота через рефлексию");
            }
      }

      private void SetFlipHorizontal(bool flip)
      {
            if (flipHorizontalField != null)
            {
                  flipHorizontalField.SetValue(segmentationManager, flip);
            }
            else
            {
                  Debug.LogWarning("⚠️ Не удалось установить горизонтальное отражение через рефлексию");
            }
      }

      private int GetCurrentRotationMode()
      {
            if (maskRotationModeField != null)
            {
                  return (int)maskRotationModeField.GetValue(segmentationManager);
            }
            return -1;
      }

      private bool GetCurrentFlipHorizontal()
      {
            if (flipHorizontalField != null)
            {
                  return (bool)flipHorizontalField.GetValue(segmentationManager);
            }
            return false;
      }

      #endregion

      #region Inspector Helpers

      [Header("Current Values (Read-Only)")]
      [SerializeField] private int currentRotationMode = -1;
      [SerializeField] private bool currentFlipHorizontal = false;

      void Update()
      {
            // Update display values in inspector
            if (segmentationManager != null)
            {
                  currentRotationMode = GetCurrentRotationMode();
                  currentFlipHorizontal = GetCurrentFlipHorizontal();
            }
      }

      #endregion
}

using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Исправляет проблемы позиционирования AR объектов и UV координат
/// </summary>
public class ARPositioningFix : MonoBehaviour
{
      [Header("Dependencies")]
      [SerializeField] private ARWallPresenter wallPresenter;
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private Camera arCamera;

      [Header("Position Settings")]
      [SerializeField] private float defaultDistance = 1f;
      [SerializeField] private bool forcePositiveZ = true;
      [SerializeField] private bool useARCameraIfAvailable = true;

      [Header("UV Settings")]
      [SerializeField] private bool fixUVCoordinates = true;
      [SerializeField] private bool usePortraitMode = true;

      [Header("Debug")]
      [SerializeField] private bool enableDebugLogs = true;
      [SerializeField] private bool showGizmos = true;

      private void Start()
      {
            // Автопоиск зависимостей
            if (wallPresenter == null)
                  wallPresenter = FindObjectOfType<ARWallPresenter>();

            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            if (arCamera == null)
            {
                  // Пробуем найти AR камеру
                  var arCameraManager = FindObjectOfType<ARCameraManager>();
                  if (arCameraManager != null && useARCameraIfAvailable)
                  {
                        arCamera = arCameraManager.GetComponent<Camera>();
                        if (enableDebugLogs)
                              Debug.Log("🎥 Найдена AR камера");
                  }

                  // Fallback на основную камеру
                  if (arCamera == null)
                  {
                        arCamera = Camera.main;
                        if (enableDebugLogs)
                              Debug.Log("🎥 Используем основную камеру");
                  }
            }

            // Применяем исправления
            FixPositioning();
            if (fixUVCoordinates)
                  FixUVMapping();
      }

      /// <summary>
      /// Исправляет позиционирование AR объекта
      /// </summary>
      [ContextMenu("Fix AR Positioning")]
      public void FixPositioning()
      {
            if (wallPresenter == null || arCamera == null)
            {
                  Debug.LogWarning("⚠️ Отсутствуют необходимые компоненты для исправления позиционирования");
                  return;
            }

            // Получаем текущую позицию и поворот
            Transform wallTransform = wallPresenter.transform;
            Vector3 cameraPosition = arCamera.transform.position;
            Vector3 cameraForward = arCamera.transform.forward;

            // Вычисляем правильную позицию
            Vector3 targetPosition = cameraPosition + cameraForward * defaultDistance;

            // Проверяем, что объект перед камерой
            Vector3 toWall = (targetPosition - cameraPosition).normalized;
            float dotProduct = Vector3.Dot(toWall, cameraForward);

            if (dotProduct < 0 || (forcePositiveZ && targetPosition.z < 0))
            {
                  if (enableDebugLogs)
                        Debug.Log($"📏 Корректируем позицию: dotProduct={dotProduct:F3}, Z={targetPosition.z:F3}");

                  // Инвертируем Z если нужно
                  if (targetPosition.z < 0)
                        targetPosition.z *= -1;

                  // Поворачиваем к камере
                  wallTransform.forward = -cameraForward;
            }

            // Применяем позицию
            wallTransform.position = targetPosition;

            if (enableDebugLogs)
            {
                  Debug.Log($"📍 Позиция исправлена:");
                  Debug.Log($"   Камера: {cameraPosition}");
                  Debug.Log($"   Объект: {targetPosition}");
                  Debug.Log($"   Расстояние: {Vector3.Distance(cameraPosition, targetPosition):F3}");
                  Debug.Log($"   DotProduct: {dotProduct:F3}");
            }
      }

      /// <summary>
      /// Исправляет UV маппинг для правильного отображения маски
      /// </summary>
      [ContextMenu("Fix UV Mapping")]
      public void FixUVMapping()
      {
            if (wallPresenter == null || segmentationManager == null)
            {
                  Debug.LogWarning("⚠️ Отсутствуют необходимые компоненты для исправления UV");
                  return;
            }

            // Получаем текущие настройки
            float screenAspect = (float)Screen.width / Screen.height;
            bool isPortrait = Screen.height > Screen.width;

            // Корректируем UV для портретной ориентации
            if (usePortraitMode && isPortrait)
            {
                  // Инвертируем соотношение сторон для портретного режима
                  screenAspect = 1f / screenAspect;
            }

            // Устанавливаем параметры шейдера
            var renderer = wallPresenter.GetComponent<Renderer>();
            if (renderer != null)
            {
                  MaterialPropertyBlock props = new MaterialPropertyBlock();
                  renderer.GetPropertyBlock(props);

                  props.SetFloat("_ScreenAspect", screenAspect);
                  props.SetFloat("_MaskAspect", 1.0f); // Маска всегда квадратная

                  if (usePortraitMode)
                  {
                        props.SetFloat("_IsPortrait", isPortrait ? 1f : 0f);
                  }

                  renderer.SetPropertyBlock(props);

                  if (enableDebugLogs)
                  {
                        Debug.Log($"📐 UV настройки обновлены:");
                        Debug.Log($"   Экран: {Screen.width}x{Screen.height} (aspect: {screenAspect:F3})");
                        Debug.Log($"   Режим: {(isPortrait ? "портрет" : "ландшафт")}");
                  }
            }
      }

      void OnDrawGizmos()
      {
            if (!showGizmos || arCamera == null || wallPresenter == null)
                  return;

            // Рисуем линию от камеры до объекта
            Gizmos.color = Color.green;
            Vector3 start = arCamera.transform.position;
            Vector3 end = wallPresenter.transform.position;
            Gizmos.DrawLine(start, end);

            // Рисуем направление камеры
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(start, arCamera.transform.forward * defaultDistance);

            // Рисуем нормаль объекта
            Gizmos.color = Color.red;
            Gizmos.DrawRay(end, wallPresenter.transform.forward * 0.5f);
      }

      /// <summary>
      /// Обновляет позиционирование при изменении ориентации
      /// </summary>
      void OnRectTransformDimensionsChange()
      {
            if (fixUVCoordinates)
                  FixUVMapping();
      }

      /// <summary>
      /// Тест всех комбинаций настроек
      /// </summary>
      [ContextMenu("Test All Combinations")]
      public void TestAllCombinations()
      {
            StartCoroutine(TestCombinationsSequentially());
      }

      private System.Collections.IEnumerator TestCombinationsSequentially()
      {
            Debug.Log("🧪 Тестирование всех комбинаций настроек...");

            // Тестируем разные расстояния
            float[] distances = { 0.5f, 1f, 2f };
            bool[] zModes = { true, false };
            bool[] portraitModes = { true, false };

            foreach (float dist in distances)
            {
                  foreach (bool forceZ in zModes)
                  {
                        foreach (bool portrait in portraitModes)
                        {
                              Debug.Log($"🧪 Тест: distance={dist}, forceZ={forceZ}, portrait={portrait}");

                              defaultDistance = dist;
                              forcePositiveZ = forceZ;
                              usePortraitMode = portrait;

                              FixPositioning();
                              FixUVMapping();

                              yield return new WaitForSeconds(2f);
                        }
                  }
            }

            Debug.Log("✅ Тестирование завершено");

            // Возвращаем оптимальные настройки
            defaultDistance = 1f;
            forcePositiveZ = true;
            usePortraitMode = true;

            FixPositioning();
            FixUVMapping();
      }
}

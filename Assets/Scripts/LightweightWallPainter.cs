using UnityEngine;
using System.Collections;

/// <summary>
/// ОПТИМИЗИРОВАННАЯ система покраски стен - легкая версия без тяжелых алгоритмов
/// Использует быстрые приближения вместо точного flood fill
/// </summary>
public class LightweightWallPainter : MonoBehaviour
{
      [Header("Ссылки")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private ARWallPresenter arWallPresenter;

      [Header("Быстрые настройки")]
      [SerializeField]
      private Color[] wallColors = new Color[] {
        Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta
    };
      [SerializeField] private float paintRadius = 0.1f; // Радиус покраски в UV координатах (0-1)
      [SerializeField] private bool enableDebugLogs = false;

      private int currentColorIndex = 0;
      private bool isInPaintingMode = false;
      private bool isProcessing = false; // Предотвращение множественных кликов

      void Start()
      {
            // Автопоиск компонентов
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            if (arWallPresenter == null)
                  arWallPresenter = FindObjectOfType<ARWallPresenter>();

            Debug.Log("🎨 Легкий режим покраски стен инициализирован");
      }

      void Update()
      {
            HandleQuickPaintInput();
      }

      /// <summary>
      /// Включает/выключает быстрый режим покраски
      /// </summary>
      public void TogglePaintingMode()
      {
            isInPaintingMode = !isInPaintingMode;
            Debug.Log($"🎨 Быстрый режим покраски: {(isInPaintingMode ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");

            if (isInPaintingMode)
            {
                  // Переключаемся в режим показа только стен
                  segmentationManager.ShowOnlyWalls();
            }
            else
            {
                  // Возвращаемся к обычному режиму
                  segmentationManager.ShowAllClassesColored();
            }
      }

      /// <summary>
      /// Быстрая обработка кликов без тяжелых вычислений
      /// </summary>
      private void HandleQuickPaintInput()
      {
            if (!isInPaintingMode || isProcessing) return;

            // Проверяем клик и избегаем UI элементов
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                  Vector2 screenPos = Input.mousePosition;
                  QuickPaintAtPosition(screenPos);
            }
      }

      /// <summary>
      /// Быстрая покраска без GPU readback - просто меняет цвет всех стен
      /// </summary>
      private void QuickPaintAtPosition(Vector2 screenPos)
      {
            if (isProcessing) return;

            isProcessing = true;

            // Простая проверка - клик в центральной области экрана = стена
            Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            // Эвристика: если клик в средней части экрана (0.2-0.8), то скорее всего стена
            if (screenUV.x > 0.2f && screenUV.x < 0.8f && screenUV.y > 0.3f && screenUV.y < 0.7f)
            {
                  Color wallColor = GetNextWallColor();
                  ApplyColorToAllWalls(wallColor);

                  if (enableDebugLogs)
                        Debug.Log($"🎨 Быстрая покраска: {wallColor} в позиции {screenUV}");
            }
            else
            {
                  if (enableDebugLogs)
                        Debug.Log($"🎨 Клик вне области стен: {screenUV}");
            }

            // Небольшая задержка для предотвращения спама кликов
            StartCoroutine(ResetProcessingFlag());
      }

      /// <summary>
      /// Сбрасывает флаг обработки через небольшую задержку
      /// </summary>
      private IEnumerator ResetProcessingFlag()
      {
            yield return new WaitForSeconds(0.3f); // 300ms задержка между кликами
            isProcessing = false;
      }

      /// <summary>
      /// Применяет цвет ко всем стенам (быстрый метод)
      /// </summary>
      private void ApplyColorToAllWalls(Color color)
      {
            if (arWallPresenter != null)
            {
                  arWallPresenter.SetClassColor(0, color); // 0 = класс стен
                  if (enableDebugLogs)
                        Debug.Log($"🎨 Применен цвет {color} ко всем стенам");
            }
      }

      /// <summary>
      /// Получает следующий цвет из палитры
      /// </summary>
      private Color GetNextWallColor()
      {
            Color color = wallColors[currentColorIndex % wallColors.Length];
            currentColorIndex++;
            return color;
      }

      /// <summary>
      /// Переключается на следующий цвет
      /// </summary>
      public void NextColor()
      {
            Color color = GetNextWallColor();
            if (enableDebugLogs)
                  Debug.Log($"🎨 Выбран цвет: {color}");
      }

      /// <summary>
      /// Сбрасывает цвета стен на стандартный серый
      /// </summary>
      public void ResetWallColors()
      {
            currentColorIndex = 0;

            if (arWallPresenter != null)
            {
                  arWallPresenter.SetClassColor(0, new Color(0.47f, 0.47f, 0.47f, 1f)); // Стандартный серый ADE20K
            }

            Debug.Log("🎨 Цвета стен сброшены к стандартному серому");
      }

      /// <summary>
      /// Быстрая покраска в случайный цвет (для тестирования)
      /// </summary>
      [ContextMenu("Быстрый тест - случайный цвет")]
      public void QuickTestRandomColor()
      {
            if (arWallPresenter != null)
            {
                  Color randomColor = new Color(Random.value, Random.value, Random.value, 1f);
                  arWallPresenter.SetClassColor(0, randomColor);
                  Debug.Log($"🎨 Тест: применен случайный цвет {randomColor}");
            }
      }

      /// <summary>
      /// Включает/выключает отладочные логи
      /// </summary>
      public void ToggleDebugLogs()
      {
            enableDebugLogs = !enableDebugLogs;
            Debug.Log($"🔍 Отладочные логи: {(enableDebugLogs ? "ВКЛЮЧЕНЫ" : "ВЫКЛЮЧЕНЫ")}");
      }
}

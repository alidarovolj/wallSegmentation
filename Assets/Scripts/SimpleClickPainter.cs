using UnityEngine;

/// <summary>
/// МАКСИМАЛЬНО ПРОСТАЯ покраска стен - просто клик = синий цвет
/// Никакого UI, никаких сложных алгоритмов, только клик и результат
/// </summary>
public class SimpleClickPainter : MonoBehaviour
{
      [Header("Настройки")]
      [SerializeField] private Color paintColor = Color.blue;
      [SerializeField] private bool enablePainting = true;
      [SerializeField] private float clickCooldown = 0.5f; // Задержка между кликами

      [Header("Автопоиск компонентов")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private ARWallPresenter arWallPresenter;

      private float lastClickTime = 0f;

      void Start()
      {
            // Автопоиск компонентов если не назначены
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            if (arWallPresenter == null)
                  arWallPresenter = FindObjectOfType<ARWallPresenter>();

            // Автоматически переключаемся в режим показа только стен
            if (segmentationManager != null)
            {
                  segmentationManager.ShowOnlyWalls();
                  Debug.Log("🧱 Включен режим показа только стен для покраски");
            }

            // КРИТИЧНО: Принудительно настраиваем ARWallPresenter для режима одного класса
            if (arWallPresenter != null)
            {
                  arWallPresenter.SetSingleClassMode(0, paintColor); // 0 = стены, paintColor = синий
                  Debug.Log("🎯 ARWallPresenter принудительно настроен для режима одного класса (стены)");
            }

            Debug.Log($"🎨 Простая покраска по клику активирована! Цвет: {paintColor}");
      }

      void Update()
      {
            HandleClick();
      }

      /// <summary>
      /// Обрабатывает клики - просто и быстро
      /// </summary>
      private void HandleClick()
      {
            // Проверяем что покраска включена и прошло достаточно времени с последнего клика
            if (!enablePainting || Time.time - lastClickTime < clickCooldown) return;

            // Проверяем клик левой кнопкой мыши (или тач на мобильном)
            if (Input.GetMouseButtonDown(0))
            {
                  // Избегаем кликов по UI элементам
                  if (UnityEngine.EventSystems.EventSystem.current != null &&
                      UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                  {
                        return; // Клик по UI - игнорируем
                  }

                  // Красим стены в заданный цвет
                  PaintWalls();
                  lastClickTime = Time.time;
            }
      }

      /// <summary>
      /// Красит все стены в заданный цвет
      /// </summary>
      private void PaintWalls()
      {
            if (arWallPresenter != null)
            {
                  // Используем новый надежный метод
                  arWallPresenter.SetSingleClassMode(0, paintColor); // 0 = класс стен
                  Debug.Log($"🎨 Стены покрашены в {paintColor} через SetSingleClassMode");
            }
            else
            {
                  Debug.LogWarning("⚠️ ARWallPresenter не найден! Назначьте в Inspector или убедитесь что он есть в сцене.");
            }
      }

      /// <summary>
      /// Меняет цвет покраски (можно вызвать из других скриптов)
      /// </summary>
      public void SetPaintColor(Color newColor)
      {
            paintColor = newColor;
            Debug.Log($"🎨 Цвет покраски изменен на: {paintColor}");
      }

      /// <summary>
      /// Включает/выключает покраску
      /// </summary>
      public void SetPaintingEnabled(bool enabled)
      {
            enablePainting = enabled;
            Debug.Log($"🎨 Покраска по клику: {(enabled ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
      }

      /// <summary>
      /// Сбрасывает цвет стен на стандартный серый
      /// </summary>
      [ContextMenu("Сбросить цвет стен")]
      public void ResetWallColor()
      {
            if (arWallPresenter != null)
            {
                  Color defaultGray = new Color(0.47f, 0.47f, 0.47f, 1f); // Стандартный серый ADE20K
                  arWallPresenter.SetSingleClassMode(0, defaultGray);
                  Debug.Log("🎨 Цвет стен сброшен на стандартный серый через SetSingleClassMode");
            }
      }

      /// <summary>
      /// Быстрые тесты цветов через контекстное меню
      /// </summary>
      [ContextMenu("Тест: Красный")]
      public void TestRed() { SetPaintColor(Color.red); PaintWalls(); }

      [ContextMenu("Тест: Синий")]
      public void TestBlue() { SetPaintColor(Color.blue); PaintWalls(); }

      [ContextMenu("Тест: Зеленый")]
      public void TestGreen() { SetPaintColor(Color.green); PaintWalls(); }

      [ContextMenu("Тест: Желтый")]
      public void TestYellow() { SetPaintColor(Color.yellow); PaintWalls(); }
}

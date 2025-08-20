using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// ⚠️ СИСТЕМА ОТКЛЮЧЕНА ИЗ-ЗА ЛАГОВ! ⚠️
/// 
/// Эта система покраски отдельных стен вызывает критические проблемы производительности:
/// - Синхронное чтение GPU
/// - Тяжелый flood fill алгоритм
/// - Отсутствие кэширования
/// 
/// ИСПОЛЬЗУЙТЕ ВМЕСТО НЕЕ: LightweightWallPainter
/// 
/// Система покраски отдельных стен по клику
/// Использует connected component analysis для выделения отдельных стен
/// </summary>
public class IndividualWallPainter : MonoBehaviour
{
      [Header("Ссылки")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private ARWallPresenter arWallPresenter;

      [Header("Настройки покраски")]
      [SerializeField]
      private Color[] wallColors = new Color[] {
        Color.red, Color.blue, Color.green, Color.yellow,
        Color.cyan, Color.magenta, Color.white, new Color(1f, 0.5f, 0f) // orange
    };

      [Header("Алгоритм выделения стен")]
      [SerializeField] private int minWallSize = 100; // Минимальный размер стены в пикселях
      [SerializeField] private bool enableFloodFill = true; // Использовать flood fill для выделения связанных областей

      // Данные для анализа стен
      private Dictionary<int, Color> wallRegionColors = new Dictionary<int, Color>();
      private RenderTexture wallRegionTexture; // Текстура с ID отдельных стен
      private int currentColorIndex = 0;
      private bool isInPaintingMode = false;

      void Start()
      {
            // Автопоиск компонентов если не назначены
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
            if (arWallPresenter == null)
                  arWallPresenter = FindObjectOfType<ARWallPresenter>();
      }

      void Update()
      {
            // ОТКЛЮЧЕНО ДЛЯ ПРОИЗВОДИТЕЛЬНОСТИ - используйте LightweightWallPainter
            // HandleWallPaintingInput();
      }

      /// <summary>
      /// Включает/выключает режим покраски отдельных стен
      /// </summary>
      public void TogglePaintingMode()
      {
            isInPaintingMode = !isInPaintingMode;
            Debug.Log($"🎨 Режим покраски отдельных стен: {(isInPaintingMode ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");

            if (isInPaintingMode)
            {
                  // Переключаемся в режим показа только стен
                  segmentationManager.ShowOnlyWalls();
                  CreateWallRegionTexture();
            }
            else
            {
                  // Возвращаемся к обычному режиму
                  segmentationManager.ShowAllClassesColored();
            }
      }

      /// <summary>
      /// Обрабатывает клики для покраски отдельных стен
      /// </summary>
      private void HandleWallPaintingInput()
      {
            if (!isInPaintingMode) return;

            // Проверяем клик и избегаем UI элементов
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                  Vector2 screenPos = Input.mousePosition;
                  StartCoroutine(PaintWallAtPosition(screenPos));
            }
      }

      /// <summary>
      /// Красит отдельную стену в точке клика
      /// </summary>
      private System.Collections.IEnumerator PaintWallAtPosition(Vector2 screenPos)
      {
            // Получаем маску сегментации
            var maskTexture = segmentationManager.GetSegmentationMask();
            if (maskTexture == null)
            {
                  Debug.LogWarning("🎨 Маска сегментации недоступна");
                  yield break;
            }

            // Читаем данные маски с GPU
            var request = UnityEngine.Rendering.AsyncGPUReadback.Request(maskTexture);
            yield return new UnityEngine.WaitUntil(() => request.done);

            if (request.hasError)
            {
                  Debug.LogError("🎨 Ошибка чтения маски для покраски");
                  yield break;
            }

            var maskData = request.GetData<float>();

            // Конвертируем координаты экрана в координаты маски
            Vector2Int maskCoords = ScreenToMaskCoordinates(screenPos, maskTexture);

            // Проверяем что клик попал на стену (класс 0)
            int pixelIndex = maskCoords.y * maskTexture.width + maskCoords.x;
            if (pixelIndex >= maskData.Length) yield break;

            float classValue = maskData[pixelIndex];
            int classId = Mathf.RoundToInt(classValue);

            if (classId != 0) // Не стена
            {
                  Debug.Log($"🎨 Клик не на стене: класс {classId}");
                  yield break;
            }

            // Выделяем связанную область стены (flood fill)
            if (enableFloodFill)
            {
                  var wallRegion = FloodFillWallRegion(maskData, maskTexture.width, maskTexture.height, maskCoords);
                  if (wallRegion.Count < minWallSize)
                  {
                        Debug.Log($"🎨 Стена слишком маленькая: {wallRegion.Count} пикселей");
                        yield break;
                  }

                  // Назначаем цвет этой области стены
                  Color wallColor = GetNextWallColor();
                  ApplyColorToWallRegion(wallRegion, wallColor);

                  Debug.Log($"🎨 Окрашена стена: {wallRegion.Count} пикселей, цвет: {wallColor}");
            }
      }

      /// <summary>
      /// Алгоритм flood fill для выделения связанной области стены
      /// </summary>
      private HashSet<Vector2Int> FloodFillWallRegion(Unity.Collections.NativeArray<float> maskData, int width, int height, Vector2Int startPos)
      {
            var region = new HashSet<Vector2Int>();
            var stack = new Stack<Vector2Int>();
            stack.Push(startPos);

            while (stack.Count > 0)
            {
                  var pos = stack.Pop();

                  // Проверяем границы
                  if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height) continue;
                  if (region.Contains(pos)) continue;

                  // Проверяем что это стена (класс 0)
                  int index = pos.y * width + pos.x;
                  if (index >= maskData.Length) continue;

                  float classValue = maskData[index];
                  int classId = Mathf.RoundToInt(classValue);

                  if (classId != 0) continue; // Не стена

                  // Добавляем в область
                  region.Add(pos);

                  // Добавляем соседей (4-connectivity)
                  stack.Push(new Vector2Int(pos.x + 1, pos.y));
                  stack.Push(new Vector2Int(pos.x - 1, pos.y));
                  stack.Push(new Vector2Int(pos.x, pos.y + 1));
                  stack.Push(new Vector2Int(pos.x, pos.y - 1));
            }

            return region;
      }

      /// <summary>
      /// Конвертирует координаты экрана в координаты маски с учетом поворотов
      /// </summary>
      private Vector2Int ScreenToMaskCoordinates(Vector2 screenPos, RenderTexture maskTexture)
      {
            // Нормализованные координаты экрана
            Vector2 screenUV = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            // Применяем те же трансформации что и в шейдере маски
            // TopFormer использует 180° поворот + горизонтальный flip
            float uv_x = 1.0f - screenUV.x; // Горизонтальный flip
            float uv_y = 1.0f - screenUV.y; // Вертикальный flip (180°)

            // Конвертируем в координаты текстуры маски
            int maskX = Mathf.Clamp((int)(uv_x * maskTexture.width), 0, maskTexture.width - 1);
            int maskY = Mathf.Clamp((int)(uv_y * maskTexture.height), 0, maskTexture.height - 1);

            return new Vector2Int(maskX, maskY);
      }

      /// <summary>
      /// Получает следующий цвет для покраски стены
      /// </summary>
      private Color GetNextWallColor()
      {
            Color color = wallColors[currentColorIndex % wallColors.Length];
            currentColorIndex++;
            return color;
      }

      /// <summary>
      /// Применяет цвет к области стены
      /// </summary>
      private void ApplyColorToWallRegion(HashSet<Vector2Int> region, Color color)
      {
            // TODO: Здесь нужно создать текстуру маски для этой области
            // и передать ее в ARWallPresenter для отображения

            // Для простой реализации пока просто меняем цвет всех стен
            if (arWallPresenter != null)
            {
                  arWallPresenter.SetClassColor(0, color); // 0 = класс стен
            }

            Debug.Log($"🎨 Применен цвет {color} к области из {region.Count} пикселей");
      }

      /// <summary>
      /// Создает текстуру для хранения ID отдельных стен
      /// </summary>
      private void CreateWallRegionTexture()
      {
            if (wallRegionTexture != null)
            {
                  wallRegionTexture.Release();
                  Destroy(wallRegionTexture);
            }

            // Создаем текстуру того же размера что и маска сегментации
            var maskTexture = segmentationManager.GetSegmentationMask();
            if (maskTexture != null)
            {
                  wallRegionTexture = new RenderTexture(maskTexture.width, maskTexture.height, 0, UnityEngine.RenderTextureFormat.RInt);
                  wallRegionTexture.Create();
                  Debug.Log($"🎨 Создана текстура областей стен: {wallRegionTexture.width}x{wallRegionTexture.height}");
            }
      }

      /// <summary>
      /// Сбрасывает все цвета стен
      /// </summary>
      [ContextMenu("Сбросить цвета стен")]
      public void ResetWallColors()
      {
            wallRegionColors.Clear();
            currentColorIndex = 0;

            if (arWallPresenter != null)
            {
                  arWallPresenter.SetClassColor(0, Color.gray); // Возвращаем стандартный серый
            }

            Debug.Log("🎨 Цвета стен сброшены");
      }

      /// <summary>
      /// Переключается на следующий цвет для покраски
      /// </summary>
      [ContextMenu("Следующий цвет")]
      public void NextColor()
      {
            Color color = GetNextWallColor();
            Debug.Log($"🎨 Выбран цвет: {color}");
      }

      void OnDestroy()
      {
            if (wallRegionTexture != null)
            {
                  wallRegionTexture.Release();
                  Destroy(wallRegionTexture);
            }
      }
}

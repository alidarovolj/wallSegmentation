using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Менеджер процедурных поверхностей - объединяет все компоненты Этапа 2
/// Координирует сбор точек, аппроксимацию плоскостей, обнаружение контуров и генерацию мешей
/// </summary>
public class ProceduralSurfaceManager : MonoBehaviour
{
      [Header("Компоненты")]
      [SerializeField] private SurfacePointCollector pointCollector;
      [SerializeField] private ContourDetector contourDetector;
      [SerializeField] private ProceduralMeshGenerator meshGenerator;
      [SerializeField] private ARMaskInteraction arMaskInteraction;

      [Header("Настройки процесса")]
      [SerializeField] private int minPointsForPlane = 20; // Минимум точек для аппроксимации плоскости
      [SerializeField] private float minPlaneConfidence = 0.6f; // Минимальная уверенность в плоскости (0-1)
      [SerializeField] private bool autoGenerateMeshes = true; // Автоматически генерировать меши
      [SerializeField] private bool clearPreviousMeshes = true; // Очищать предыдущие меши

      [Header("Управление")]
      [SerializeField] private KeyCode startProcessKey = KeyCode.Space;
      [SerializeField] private KeyCode clearAllKey = KeyCode.Delete;
      [SerializeField] private bool enableAutoProcess = false; // Автоматический процесс при достижении минимума точек
      [SerializeField] private bool enableOneClickMode = true; // НОВЫЙ: Режим "один клик"
      [SerializeField] private int oneClickMinPoints = 10; // НОВЫЙ: Минимум точек для режима "один клик"

      [Header("Отладка")]
      [SerializeField] private bool enableDebugLogs = true;
      [SerializeField] private bool showProcessSteps = true;

      // Состояние процесса
      private bool isProcessing = false;
      private List<Vector3> currentPointCloud = new List<Vector3>();
      private PlaneApproximator.PlaneResult currentPlane;
      private List<List<Vector2>> currentContours = new List<List<Vector2>>();

      // События
      public System.Action<PlaneApproximator.PlaneResult> OnPlaneGenerated;
      public System.Action<List<List<Vector2>>> OnContoursGenerated;
      public System.Action<GameObject> OnMeshGenerated;
      public System.Action OnProcessCompleted;

      private void Awake()
      {
            // Автопоиск компонентов если не назначены
            if (pointCollector == null)
                  pointCollector = GetComponent<SurfacePointCollector>();

            if (contourDetector == null)
                  contourDetector = GetComponent<ContourDetector>();

            if (meshGenerator == null)
                  meshGenerator = GetComponent<ProceduralMeshGenerator>();

            if (arMaskInteraction == null)
                  arMaskInteraction = FindObjectOfType<ARMaskInteraction>();

            LogDebug("🏗️ ProceduralSurfaceManager инициализирован");
      }

      private void Start()
      {
            // Подписываемся на события компонентов
            SubscribeToEvents();

            LogDebug("🚀 ProceduralSurfaceManager запущен");
      }

      private void Update()
      {
            HandleInput();

            // Автоматический процесс если включен
            if (enableAutoProcess && !isProcessing && pointCollector != null)
            {
                  if (pointCollector.IsCollecting() && pointCollector.GetPointCount() >= minPointsForPlane)
                  {
                        StartCoroutine(ProcessSurfaceGeneration());
                  }
            }

            // НОВЫЙ: Режим "один клик" - запускаем с меньшим количеством точек
            if (enableOneClickMode && !isProcessing && pointCollector != null)
            {
                  if (pointCollector.IsCollecting() && pointCollector.GetPointCount() >= oneClickMinPoints)
                  {
                        // Автоматически запускаем процесс в режиме "один клик"
                        StartCoroutine(ProcessSurfaceGeneration());
                  }
            }
      }

      /// <summary>
      /// Подписывается на события компонентов
      /// </summary>
      private void SubscribeToEvents()
      {
            if (pointCollector != null)
            {
                  pointCollector.OnPointCloudUpdated += OnPointCloudUpdated;
                  pointCollector.OnCollectionCompleted += OnPointCollectionCompleted;
            }

            if (contourDetector != null)
            {
                  contourDetector.OnContoursDetected += OnContoursDetected;
            }

            if (meshGenerator != null)
            {
                  meshGenerator.OnMeshGenerated += OnMeshGeneratedInternal;
            }
      }

      /// <summary>
      /// Обрабатывает пользовательский ввод
      /// </summary>
      private void HandleInput()
      {
            if (Input.GetKeyDown(startProcessKey))
            {
                  StartSurfaceGeneration();
            }

            if (Input.GetKeyDown(clearAllKey))
            {
                  ClearAllSurfaces();
            }
      }

      /// <summary>
      /// Запускает процесс генерации поверхности
      /// </summary>
      public void StartSurfaceGeneration()
      {
            if (isProcessing)
            {
                  LogDebug("⚠️ Процесс уже выполняется");
                  return;
            }

            if (pointCollector == null)
            {
                  LogDebug("❌ SurfacePointCollector не найден");
                  return;
            }

            currentPointCloud = pointCollector.GetPointCloud();

            // Выбираем минимум в зависимости от режима
            int requiredMinPoints = enableOneClickMode ? oneClickMinPoints : minPointsForPlane;

            if (currentPointCloud.Count < requiredMinPoints)
            {
                  LogDebug($"❌ Недостаточно точек: {currentPointCloud.Count} < {requiredMinPoints}");
                  return;
            }

            StartCoroutine(ProcessSurfaceGeneration());
      }

      /// <summary>
      /// Основной процесс генерации поверхности
      /// </summary>
      private IEnumerator ProcessSurfaceGeneration()
      {
            if (isProcessing)
                  yield break;

            isProcessing = true;
            LogDebug("🏗️ Начинаем процесс генерации поверхности...");

            // Шаг 1: Аппроксимация плоскости
            LogStep("1️⃣ Аппроксимация плоскости по облаку точек");
            yield return StartCoroutine(ApproximatePlaneFromPoints());

            if (!currentPlane.isValid)
            {
                  LogDebug("❌ Не удалось аппроксимировать плоскость");
                  isProcessing = false;
                  yield break;
            }

            // Шаг 2: Обнаружение контуров
            LogStep("2️⃣ Обнаружение контуров на маске сегментации");
            yield return StartCoroutine(DetectSurfaceContours());

            if (currentContours.Count == 0)
            {
                  LogDebug("❌ Контуры не найдены");
                  isProcessing = false;
                  yield break;
            }

            // Шаг 3: Генерация мешей
            if (autoGenerateMeshes)
            {
                  LogStep("3️⃣ Генерация 3D-мешей");
                  yield return StartCoroutine(GenerateMeshesFromContours());
            }

            LogDebug("✅ Процесс генерации поверхности завершен успешно!");
            OnProcessCompleted?.Invoke();
            isProcessing = false;
      }

      /// <summary>
      /// Аппроксимирует плоскость по облаку точек
      /// </summary>
      private IEnumerator ApproximatePlaneFromPoints()
      {
            yield return null; // Даем время для обновления UI

            currentPlane = PlaneApproximator.ApproximatePlane(currentPointCloud, minPointsForPlane);

            if (currentPlane.isValid)
            {
                  if (currentPlane.confidence < minPlaneConfidence)
                  {
                        LogDebug($"⚠️ Низкая уверенность в плоскости: {currentPlane.confidence:F2} < {minPlaneConfidence:F2}");
                  }

                  LogDebug($"✅ Плоскость аппроксимирована: уверенность={currentPlane.confidence:F2}, ошибка={currentPlane.averageError:F3}м");
                  OnPlaneGenerated?.Invoke(currentPlane);
            }

            yield return new WaitForSeconds(0.1f); // Пауза между шагами
      }

      /// <summary>
      /// Обнаруживает контуры поверхности
      /// </summary>
      private IEnumerator DetectSurfaceContours()
      {
            if (contourDetector == null)
            {
                  LogDebug("❌ ContourDetector не найден");
                  yield break;
            }

            // Очищаем предыдущие контуры
            currentContours.Clear();

            // Устанавливаем целевой класс (берем из pointCollector или используем стены)
            int targetClass = pointCollector != null ? 0 : 0; // По умолчанию стены
            contourDetector.SetTargetClass(targetClass);

            // Запускаем обнаружение
            contourDetector.DetectContours();

            // Ждем результатов (обнаружение асинхронное)
            float timeout = 5f;
            float elapsed = 0f;

            while (currentContours.Count == 0 && elapsed < timeout)
            {
                  yield return new WaitForSeconds(0.1f);
                  elapsed += 0.1f;
            }

            if (currentContours.Count > 0)
            {
                  LogDebug($"✅ Обнаружено {currentContours.Count} контуров");
            }
            else
            {
                  LogDebug("⚠️ Контуры не обнаружены в отведенное время");
            }
      }

      /// <summary>
      /// Генерирует меши из контуров
      /// </summary>
      private IEnumerator GenerateMeshesFromContours()
      {
            if (meshGenerator == null)
            {
                  LogDebug("❌ ProceduralMeshGenerator не найден");
                  yield break;
            }

            if (currentContours.Count == 0)
            {
                  LogDebug("❌ Нет контуров для генерации мешей");
                  yield break;
            }

            // Очищаем предыдущие меши если нужно
            if (clearPreviousMeshes)
            {
                  meshGenerator.ClearAllMeshes();
            }

            // Создаем матрицу преобразования экран->мир
            Matrix4x4 screenToWorldMatrix = CalculateScreenToWorldMatrix();

            // Генерируем меш для каждого контура
            int generatedCount = 0;

            foreach (var contour in currentContours)
            {
                  GameObject meshObject = meshGenerator.GenerateMeshFromContour(contour, currentPlane, screenToWorldMatrix);

                  if (meshObject != null)
                  {
                        generatedCount++;
                        LogDebug($"✅ Меш {generatedCount} сгенерирован");
                  }

                  yield return new WaitForSeconds(0.1f); // Пауза между мешами
            }

            LogDebug($"🎉 Сгенерировано {generatedCount} мешей из {currentContours.Count} контуров");
      }

      /// <summary>
      /// Вычисляет матрицу преобразования экран->мир
      /// </summary>
      private Matrix4x4 CalculateScreenToWorldMatrix()
      {
            Camera arCamera = Camera.main;
            if (arCamera == null)
            {
                  LogDebug("⚠️ AR камера не найдена, используем единичную матрицу");
                  return Matrix4x4.identity;
            }

            // Комбинируем матрицы проекции и мирового преобразования
            Matrix4x4 projectionMatrix = arCamera.projectionMatrix;
            Matrix4x4 worldToCameraMatrix = arCamera.worldToCameraMatrix;
            Matrix4x4 cameraToWorldMatrix = worldToCameraMatrix.inverse;

            return cameraToWorldMatrix * projectionMatrix.inverse;
      }

      /// <summary>
      /// Очищает все поверхности и данные
      /// </summary>
      public void ClearAllSurfaces()
      {
            LogDebug("🗑️ Очистка всех поверхностей...");

            // Останавливаем процесс если выполняется
            if (isProcessing)
            {
                  StopAllCoroutines();
                  isProcessing = false;
            }

            // Очищаем компоненты
            if (pointCollector != null)
            {
                  pointCollector.ClearPointCloud();
            }

            if (contourDetector != null)
            {
                  contourDetector.ClearContours();
            }

            if (meshGenerator != null)
            {
                  meshGenerator.ClearAllMeshes();
            }

            // Очищаем внутренние данные
            currentPointCloud.Clear();
            currentContours.Clear();
            currentPlane = PlaneApproximator.PlaneResult.Invalid;

            LogDebug("✅ Все поверхности очищены");
      }

      // Обработчики событий

      private void OnPointCloudUpdated(List<Vector3> points)
      {
            currentPointCloud = new List<Vector3>(points);
            LogDebug($"📊 Облако точек обновлено: {points.Count} точек");
      }

      private void OnPointCollectionCompleted(List<Vector3> points)
      {
            currentPointCloud = new List<Vector3>(points);
            LogDebug($"🏁 Сбор точек завершен: {points.Count} точек");

            if (enableAutoProcess)
            {
                  StartSurfaceGeneration();
            }
      }

      private void OnContoursDetected(List<List<Vector2>> contours)
      {
            currentContours = new List<List<Vector2>>(contours);
            LogDebug($"🔍 Контуры обнаружены: {contours.Count} контуров");
            OnContoursGenerated?.Invoke(contours);
      }

      private void OnMeshGeneratedInternal(GameObject meshObject, Mesh mesh)
      {
            LogDebug($"🎯 Меш сгенерирован: {mesh.name}");
            OnMeshGenerated?.Invoke(meshObject);
      }

      // Утилиты

      private void LogStep(string step)
      {
            if (showProcessSteps)
            {
                  Debug.Log($"[ProceduralSurfaceManager] {step}");
            }
      }

      private void LogDebug(string message)
      {
            if (enableDebugLogs)
                  Debug.Log($"[ProceduralSurfaceManager] {message}");
      }

      // Публичные методы для внешнего управления

      /// <summary>
      /// Проверяет, выполняется ли процесс генерации
      /// </summary>
      public bool IsProcessing()
      {
            return isProcessing;
      }

      /// <summary>
      /// Получает текущее облако точек
      /// </summary>
      public List<Vector3> GetCurrentPointCloud()
      {
            return new List<Vector3>(currentPointCloud);
      }

      /// <summary>
      /// Получает текущую аппроксимированную плоскость
      /// </summary>
      public PlaneApproximator.PlaneResult GetCurrentPlane()
      {
            return currentPlane;
      }

      /// <summary>
      /// Получает текущие контуры
      /// </summary>
      public List<List<Vector2>> GetCurrentContours()
      {
            return new List<List<Vector2>>(currentContours);
      }

      /// <summary>
      /// Включает/выключает автоматический процесс
      /// </summary>
      public void SetAutoProcess(bool enabled)
      {
            enableAutoProcess = enabled;
            LogDebug($"🔄 Автоматический процесс: {(enabled ? "включен" : "отключен")}");
      }

      /// <summary>
      /// Устанавливает минимальное количество точек для аппроксимации
      /// </summary>
      public void SetMinPointsForPlane(int minPoints)
      {
            minPointsForPlane = Mathf.Max(3, minPoints);
            LogDebug($"📊 Минимум точек для плоскости: {minPointsForPlane}");
      }

      private void OnDestroy()
      {
            // Отписываемся от событий
            if (pointCollector != null)
            {
                  pointCollector.OnPointCloudUpdated -= OnPointCloudUpdated;
                  pointCollector.OnCollectionCompleted -= OnPointCollectionCompleted;
            }

            if (contourDetector != null)
            {
                  contourDetector.OnContoursDetected -= OnContoursDetected;
            }

            if (meshGenerator != null)
            {
                  meshGenerator.OnMeshGenerated -= OnMeshGeneratedInternal;
            }
      }
}

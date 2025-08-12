using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Отвечает за проекцию маски сегментации в 3D-пространство с использованием данных о глубине.
/// Применяет эффект постобработки к AR-камере для корректного отображения маски в мировых координатах.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ProjectionRenderer : MonoBehaviour
{
      [Header("Ссылки на компоненты")]
      [Tooltip("AR Occlusion Manager для получения данных о глубине")]
      [SerializeField]
      private AROcclusionManager occlusionManager;

      [Tooltip("Async Segmentation Manager для получения маски сегментации")]
      [SerializeField]
      private AsyncSegmentationManager segmentationManager;

      [Header("Настройки отладки")]
      [Tooltip("Включить отладочную информацию в консоль")]
      [SerializeField]
      private bool enableDebugLogging = true;

      [Tooltip("Показать отладочную информацию о производительности")]
      [SerializeField]
      private bool showPerformanceStats = false;

      [Header("Настройки качества")]
      [Tooltip("Включить проекционный рендеринг")]
      [SerializeField]
      private bool enableProjection = true;

      [Tooltip("Тестовый режим - показать маску напрямую")]
      [SerializeField]
      private bool testMode = false;

      [Tooltip("Принудительно растянуть маску на весь экран")]
      [SerializeField]
      private bool forceFullscreen = true;

      // Приватные переменные
      private Camera arCamera;
      private Material projectionMaterial;
      private Shader projectionShader;

      // Кэшированные текстуры для производительности
      private Texture2D environmentDepthTexture;
      private RenderTexture segmentationTexture;

      // Статистика производительности
      private float lastFrameTime;
      private int frameCount;

      private void Awake()
      {
            // Получаем компонент камеры
            arCamera = GetComponent<Camera>();
            if (arCamera == null)
            {
                  LogError("ProjectionRenderer должен быть прикреплен к объекту с компонентом Camera");
                  enabled = false;
                  return;
            }

            // Автоматически найти компоненты, если они не назначены
            if (occlusionManager == null)
            {
                  occlusionManager = FindObjectOfType<AROcclusionManager>();
                  if (occlusionManager == null)
                  {
                        LogError("Не найден AROcclusionManager в сцене. Проекция отключена.");
                        enableProjection = false;
                  }
            }

            if (segmentationManager == null)
            {
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
                  if (segmentationManager == null)
                  {
                        LogError("Не найден AsyncSegmentationManager в сцене. Проекция отключена.");
                        enableProjection = false;
                  }
            }

            LogDebug("✅ ProjectionRenderer инициализирован");
      }

      private void Start()
      {
            // Загружаем шейдер проекции
            LoadProjectionShader();

            // Создаем материал
            CreateProjectionMaterial();

            LogDebug("🎯 ProjectionRenderer готов к работе");
      }

      private void Update()
      {
            // Обновляем статистику производительности
            if (showPerformanceStats)
            {
                  UpdatePerformanceStats();
            }

            // Отладка: проверяем доступность данных раз в секунду
            // if (enableDebugLogging && Time.time % 2.0f < 0.1f)
            // {
            //       bool segReady = segmentationManager != null && segmentationManager.IsSegmentationMaskReady();
            //       LogDebug($"🔄 Проверка данных: сегментация готова = {segReady}");
            //       LogDebug($"🔄 Камера: {arCamera != null}, Материал: {projectionMaterial != null}, Проекция включена: {enableProjection}");
            // }
            // ОТКЛЮЧЕНО: слишком много спама в консоли

            // ТЕСТОВЫЙ РЕЖИМ: Показываем маску напрямую для диагностики (отрисовка происходит в OnRenderObject)
            // Отключаем ShowTestMask, чтобы избежать конфликта с OnRenderObject
            // if (testMode && segmentationManager != null && segmentationManager.IsSegmentationMaskReady())
            // {
            //     ShowTestMask();
            // }

            // FALLBACK: Если OnRenderImage не работает, попробуем альтернативный подход
            if (enableProjection && !testMode && projectionMaterial != null && segmentationManager != null && segmentationManager.IsSegmentationMaskReady())
            {
                  // Применяем эффект через CommandBuffer (альтернатива OnRenderImage)
                  TryAlternativeRendering();
            }
      }

      /// <summary>
      /// Основной метод рендеринга - вызывается Unity для каждого кадра
      /// </summary>
      /// <param name="source">Исходное изображение с камеры</param>
      /// <param name="destination">Целевой render target</param>
      private void OnRenderImage(RenderTexture source, RenderTexture destination)
      {
            LogDebug("📷 OnRenderImage вызван");

            // В тестовом режиме показываем только маску
            if (testMode && projectionMaterial != null && segmentationManager != null)
            {
                  var mask = segmentationManager.GetCurrentSegmentationMask();
                  if (mask != null)
                  {
                        LogDebug($"🎨 OnRenderImage: тестовый режим, маска {mask.width}x{mask.height}");

                        projectionMaterial.SetTexture("_SegmentationTex", mask);
                        projectionMaterial.SetInt("_DebugMode", 3); // показать чистую маску
                        projectionMaterial.SetFloat("_MaskOpacity", 1.0f);

                        Graphics.Blit(source, destination, projectionMaterial);
                        LogDebug("🎨 OnRenderImage: тестовая маска отрисована");
                        return;
                  }
            }

            // Если проекция отключена, просто копируем изображение
            if (!enableProjection || projectionMaterial == null)
            {
                  LogDebug("⚠️ Проекция отключена или материал отсутствует");
                  Graphics.Blit(source, destination);
                  return;
            }

            // Получаем необходимые текстуры
            bool hasValidData = UpdateShaderParameters(source);

            LogDebug($"📊 Данные готовы: {hasValidData}");

            if (hasValidData)
            {
                  // Применяем проекционный эффект
                  Graphics.Blit(source, destination, projectionMaterial);
                  LogDebug("🎨 Проекционный эффект применен");
            }
            else
            {
                  // Если данных нет, показываем обычное изображение
                  Graphics.Blit(source, destination);
                  LogDebug("⚠️ Недостаточно данных для проекции, показываем обычное изображение");
            }
      }

      /// <summary>
      /// Загрузка шейдера проекции
      /// </summary>
      private void LoadProjectionShader()
      {
            projectionShader = Shader.Find("Custom/ProjectiveMask");
            if (projectionShader == null)
            {
                  LogError("Не найден шейдер 'Custom/ProjectiveMask'. Создайте шейдер ProjectiveMask.shader");
                  enableProjection = false;
            }
            else
            {
                  LogDebug("✅ Шейдер проекции загружен: " + projectionShader.name);
            }
      }

      /// <summary>
      /// Создание материала для проекции
      /// </summary>
      private void CreateProjectionMaterial()
      {
            if (projectionShader != null)
            {
                  projectionMaterial = new Material(projectionShader);
                  LogDebug("✅ Материал проекции создан");
            }
      }

      /// <summary>
      /// Обновление параметров шейдера перед рендерингом
      /// </summary>
      /// <param name="cameraTexture">Текстура с камеры</param>
      /// <returns>true, если все необходимые данные доступны</returns>
      private bool UpdateShaderParameters(RenderTexture cameraTexture)
      {
            if (projectionMaterial == null)
                  return false;

            bool hasDepthTexture = false;
            bool hasSegmentationTexture = false;

            // Получаем текстуру глубины из AROcclusionManager
            if (occlusionManager != null && occlusionManager.environmentDepthTexture != null)
            {
                  environmentDepthTexture = occlusionManager.environmentDepthTexture;
                  projectionMaterial.SetTexture("_EnvironmentDepthTex", environmentDepthTexture);
                  hasDepthTexture = true;
                  LogDebug("📊 Глубина получена: " + environmentDepthTexture.width + "x" + environmentDepthTexture.height);
            }

            // Получаем текстуру сегментации из AsyncSegmentationManager
            if (segmentationManager != null && segmentationManager.IsSegmentationMaskReady())
            {
                  segmentationTexture = segmentationManager.GetCurrentSegmentationMask();
                  if (segmentationTexture != null)
                  {
                        projectionMaterial.SetTexture("_SegmentationTex", segmentationTexture);
                        hasSegmentationTexture = true;
                        LogDebug($"🎭 Сегментация получена: {segmentationTexture.width}x{segmentationTexture.height}");
                  }
            }

            // Устанавливаем матрицы камеры
            if (arCamera != null)
            {
                  Matrix4x4 projectionMatrix = arCamera.projectionMatrix;
                  Matrix4x4 inverseProjectionMatrix = projectionMatrix.inverse;

                  projectionMaterial.SetMatrix("_ProjectionMatrix", projectionMatrix);
                  projectionMaterial.SetMatrix("_InverseProjectionMatrix", inverseProjectionMatrix);

                  LogDebug("📐 Матрицы камеры обновлены");
            }

            // Устанавливаем прозрачность маски (можно получить из segmentationManager)
            float maskOpacity = 0.5f; // По умолчанию для смешивания с камерой
            projectionMaterial.SetFloat("_MaskOpacity", maskOpacity);

            // Устанавливаем debug mode для нормального смешивания (не тестового режима)
            projectionMaterial.SetInt("_DebugMode", 0); // 0 = нормальный режим, смешивание с камерой

            // Передаем информацию о размерах экрана и маски для коррекции аспекта
            if (segmentationTexture != null && cameraTexture != null)
            {
                  float screenAspect = (float)cameraTexture.width / cameraTexture.height;
                  float maskAspect = (float)segmentationTexture.width / segmentationTexture.height;

                  // Получаем режим поворота из AsyncSegmentationManager
                  int rotationMode = 0; // По умолчанию +90°
                  if (segmentationManager != null)
                  {
                        rotationMode = segmentationManager.GetMaskRotationMode();
                  }

                  // Учитываем поворот маски при вычислении аспекта
                  if (rotationMode == 0 || rotationMode == 1) // +90° или -90°
                  {
                        maskAspect = 1.0f / maskAspect; // Инвертируем аспект
                  }

                  float aspectRatio = screenAspect / maskAspect;

                  projectionMaterial.SetFloat("_ScreenAspect", screenAspect);
                  projectionMaterial.SetFloat("_MaskAspect", maskAspect);
                  projectionMaterial.SetFloat("_AspectRatio", aspectRatio);
                  projectionMaterial.SetInt("_ForceFullscreen", forceFullscreen ? 1 : 0);

                  LogDebug($"🖥️ Аспекты: экран={screenAspect:F2}, маска={maskAspect:F2} (rotation mode: {rotationMode}), коррекция={aspectRatio:F2}");
            }

            // В редакторе глубина недоступна, поэтому тестируем только с сегментацией
            // На устройстве проверяем и глубину, и сегментацию
            bool isReady = hasSegmentationTexture && (hasDepthTexture || Application.isEditor);

            if (enableDebugLogging && Time.time % 3.0f < 0.1f)
            {
                  LogDebug($"📊 Данные готовы: сегментация={hasSegmentationTexture}, глубина={hasDepthTexture}, редактор={Application.isEditor}");
            }

            return isReady;
      }

      /// <summary>
      /// Обновление статистики производительности
      /// </summary>
      private void UpdatePerformanceStats()
      {
            frameCount++;
            float currentTime = Time.realtimeSinceStartup;

            if (currentTime - lastFrameTime >= 1.0f)
            {
                  float fps = frameCount / (currentTime - lastFrameTime);
                  LogDebug($"📊 FPS: {fps:F1}");

                  frameCount = 0;
                  lastFrameTime = currentTime;
            }
      }

      /// <summary>
      /// Логирование отладочной информации
      /// </summary>
      private void LogDebug(string message)
      {
            if (enableDebugLogging)
            {
                  Debug.Log($"[ProjectionRenderer] {message}");
            }
      }

      /// <summary>
      /// Логирование ошибок
      /// </summary>
      private void LogError(string message)
      {
            Debug.LogError($"[ProjectionRenderer] ❌ {message}");
      }

      /// <summary>
      /// Очистка ресурсов
      /// </summary>
      private void OnDestroy()
      {
            if (projectionMaterial != null)
            {
                  DestroyImmediate(projectionMaterial);
            }
      }

      /// <summary>
      /// Тестовый режим - показать маску сегментации напрямую
      /// </summary>
      private void ShowTestMask()
      {
            RenderTexture mask = segmentationManager.GetCurrentSegmentationMask();
            if (mask != null)
            {
                  // Создаем временный RenderTexture для тестирования
                  RenderTexture testRT = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);

                  // Очищаем в черный цвет
                  RenderTexture.active = testRT;
                  GL.Clear(true, true, Color.black);

                  // Отображаем маску с использованием нашего шейдера
                  if (projectionMaterial != null)
                  {
                        projectionMaterial.SetTexture("_SegmentationTex", mask);
                        projectionMaterial.SetFloat("_MaskOpacity", 1.0f);
                        projectionMaterial.SetInt("_DebugMode", 3); // Показать чистую маску

                        Graphics.Blit(null, testRT, projectionMaterial);
                  }

                  // Отображаем результат на экране
                  Graphics.Blit(testRT, (RenderTexture)null);

                  RenderTexture.ReleaseTemporary(testRT);
                  RenderTexture.active = null;

                  LogDebug("🧪 Тестовая маска отображена");
            }
      }

      /// <summary>
      /// Альтернативный метод рендеринга через CommandBuffer
      /// </summary>
      private void TryAlternativeRendering()
      {
            if (arCamera == null || projectionMaterial == null) return;

            // Получаем текущий RenderTexture камеры
            RenderTexture cameraTexture = arCamera.targetTexture;
            if (cameraTexture == null)
            {
                  LogDebug("⚠️ AR камера не имеет targetTexture");
                  return;
            }

            // Обновляем параметры шейдера
            bool hasValidData = UpdateShaderParameters(cameraTexture);

            if (hasValidData)
            {
                  // Создаем временный RenderTexture для результата
                  RenderTexture tempRT = RenderTexture.GetTemporary(cameraTexture.width, cameraTexture.height, 0, cameraTexture.format);

                  // Применяем эффект
                  Graphics.Blit(cameraTexture, tempRT, projectionMaterial);
                  Graphics.Blit(tempRT, cameraTexture);

                  RenderTexture.ReleaseTemporary(tempRT);
                  LogDebug("🎨 Альтернативный эффект применен");
            }
      }

      /// <summary>
      /// Включить/выключить проекцию в runtime
      /// </summary>
      public void ToggleProjection()
      {
            enableProjection = !enableProjection;
            LogDebug($"🔄 Проекция {(enableProjection ? "включена" : "отключена")}");
      }

      /// <summary>
      /// Получить текущий статус проекции
      /// </summary>
      public bool IsProjectionEnabled()
      {
            return enableProjection && projectionMaterial != null;
      }

      // Рендер поверх всего для тестового режима и URP (ОТКЛЮЧЕНО - используем OnRenderImage)
      private void OnRenderObject()
      {
            // Отключено: используем OnRenderImage для стабильности
            return;

            // Закомментировано для избежания предупреждений компилятора
            /*
            if (!testMode || projectionMaterial == null || segmentationManager == null)
            {
                  return;
            }

            var mask = segmentationManager.GetCurrentSegmentationMask();
            if (mask == null)
            {
                  LogDebug("🧪 OnRenderObject: mask == null");
                  return;
            }
            
            LogDebug($"🧪 OnRenderObject: отрисовка маски {mask.width}x{mask.height}");

            // Отключаем глубину и блендинг для рендеринга поверх всего
            GL.PushMatrix();
            GL.LoadOrtho();

            // Настройки рендера для наложения поверх всего
            GL.Clear(false, true, Color.clear); // Очищаем только цвет, сохраняем глубину

            // Устанавливаем параметры материала
            projectionMaterial.SetTexture("_SegmentationTex", mask);
            projectionMaterial.SetInt("_DebugMode", 3); // показать чистую маску
            projectionMaterial.SetFloat("_MaskOpacity", 1.0f);

            // Рисуем полноэкранный квад с явными UV координатами
            projectionMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            // UV координаты для правильного сэмплирования текстуры (исправленный порядок)
            GL.TexCoord2(0f, 0f); GL.Vertex3(0f, 0f, 0.1f); // левый нижний
            GL.TexCoord2(1f, 0f); GL.Vertex3(1f, 0f, 0.1f); // правый нижний
            GL.TexCoord2(1f, 1f); GL.Vertex3(1f, 1f, 0.1f); // правый верхний  
            GL.TexCoord2(0f, 1f); GL.Vertex3(0f, 1f, 0.1f); // левый верхний
            GL.End();
            GL.PopMatrix();

            LogDebug("🧪 OnRenderObject: тестовая маска отрисована поверх кадра");
            */
      }


}

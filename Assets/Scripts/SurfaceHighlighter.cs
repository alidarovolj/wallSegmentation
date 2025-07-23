using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Отвечает за подсветку поверхностей, на которые наведен курсор/палец.
/// Показывает пользователю, какая поверхность будет покрашена при нажатии
/// </summary>
public class SurfaceHighlighter : MonoBehaviour
{
      [Header("Highlight Settings")]
      [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.3f);
      [SerializeField] private float pulseSpeed = 2f;
      [SerializeField] private bool enablePulse = true;

      [Header("References")]
      [SerializeField] private SegmentationManager segmentationManager;
      [SerializeField] private AsyncSegmentationManager asyncSegmentationManager; // Ссылка на новый менеджер

      // Текущий класс под курсором/касанием
      private int currentHoveredClass = -1;
      private float pulseTime = 0f;
      private bool isReadbackInProgress = false; // Флаг для контроля асинхронных запросов

      // Глобальные свойства для подсветки
      private static readonly int HighlightClassID = Shader.PropertyToID("_GlobalHighlightClassID");
      private static readonly int HighlightColor = Shader.PropertyToID("_GlobalHighlightColor");
      private static readonly int HighlightIntensity = Shader.PropertyToID("_GlobalHighlightIntensity");

      void Start()
      {
            // Инициализация глобальных свойств
            Shader.SetGlobalInt(HighlightClassID, -1); // Нет подсветки
            Shader.SetGlobalColor(HighlightColor, highlightColor);
            Shader.SetGlobalFloat(HighlightIntensity, 0f);
      }

      void Update()
      {
            UpdateHoverDetection();
            UpdatePulseAnimation();
      }

      void UpdateHoverDetection()
      {
            // Проверяем позицию мыши/касания
            Vector2 inputPosition;
            bool hasInput = false;

#if UNITY_EDITOR || UNITY_STANDALONE
            // На компьютере используем мышь
            inputPosition = Input.mousePosition;
            hasInput = true;
#elif UNITY_ANDROID || UNITY_IOS
        // На мобильных устройствах используем касание
        if (Input.touchCount > 0)
        {
            inputPosition = Input.GetTouch(0).position;
            hasInput = true;
        }
#endif

            if (hasInput && !isReadbackInProgress)
            {
                  // Используем новый асинхронный метод
                  ReadClassAtScreenPositionAsync(inputPosition);
            }
            else
            {
                  // Убираем подсветку если нет ввода
                  if (currentHoveredClass != -1)
                  {
                        currentHoveredClass = -1;
                        UpdateHighlight(-1);
                  }
            }
      }

      void UpdatePulseAnimation()
      {
            if (enablePulse && currentHoveredClass >= 0)
            {
                  pulseTime += Time.deltaTime * pulseSpeed;
                  float intensity = (Mathf.Sin(pulseTime) + 1f) * 0.5f; // 0-1 диапазон
                  Shader.SetGlobalFloat(HighlightIntensity, intensity);
            }
            else
            {
                  pulseTime = 0f;
                  Shader.SetGlobalFloat(HighlightIntensity, currentHoveredClass >= 0 ? 1f : 0f);
            }
      }

      private void ReadClassAtScreenPositionAsync(Vector2 screenPos)
      {
            // Используем старый SegmentationManager если asyncSegmentationManager не назначен
            RenderTexture segmentationTexture = null;

            if (asyncSegmentationManager != null)
            {
                  segmentationTexture = asyncSegmentationManager.GetSegmentationTexture();
            }
            else if (segmentationManager != null)
            {
                  // Пытаемся получить текстуру из старого менеджера
                  segmentationTexture = segmentationManager.GetSegmentationTexture();
            }

            if (segmentationTexture == null) return;

            isReadbackInProgress = true;

            // Читаем всю текстуру. Для подсветки это приемлемо, так как происходит не каждый кадр.
            AsyncGPUReadback.Request(segmentationTexture, 0, TextureFormat.RFloat, (request) =>
            {
                  isReadbackInProgress = false; // Сбрасываем флаг в коллбэке

                  if (request.hasError)
                  {
                        Debug.LogError("❌ Ошибка AsyncGPUReadback в подсветке.");
                        return;
                  }

                  // Используем screenPos, переданный в этот вызов, а не Input.mousePosition,
                  // чтобы избежать race condition, если мышь сдвинется до завершения запроса.
                  var readX = (int)(screenPos.x * ((float)request.width / Screen.width));
                  var readY = (int)(screenPos.y * ((float)request.height / Screen.height));

                  var data = request.GetData<float>();
                  int index = readY * request.width + readX;

                  if (data.Length > index)
                  {
                        int hoveredClass = (int)data[index];
                        if (hoveredClass != currentHoveredClass)
                        {
                              currentHoveredClass = hoveredClass;
                              UpdateHighlight(hoveredClass);
                        }
                  }
            });
      }

      void UpdateHighlight(int classID)
      {
            Shader.SetGlobalInt(HighlightClassID, classID);

            if (classID >= 0)
            {
                  Debug.Log($"💡 Подсветка класса: {classID}");
            }
      }

      /// <summary>
      /// Методы для настройки подсветки из кода
      /// </summary>
      public void SetHighlightColor(Color color)
      {
            highlightColor = color;
            Shader.SetGlobalColor(HighlightColor, color);
      }

      public void SetPulseEnabled(bool enabled)
      {
            enablePulse = enabled;
      }

      public void SetPulseSpeed(float speed)
      {
            pulseSpeed = speed;
      }
}
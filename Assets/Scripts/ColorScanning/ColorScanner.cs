using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System;
using Unity.Collections;

/// <summary>
/// Сканер цветов из AR камеры с median-of-means алгоритмом для устойчивости к шуму.
/// Оптимизирован для производительности - работает только по запросу, не каждый кадр.
/// 
/// Особенности:
/// - Захватывает RGB кадр из ARCameraManager только по команде UI
/// - Использует median-of-means по 9x9 пикселям (с шагом 2) для устойчивости к бликам
/// - Опциональная экспозиционная нормализация по серой карте
/// - Таймаут 500мс на попытку с ретраем
/// </summary>
public class ColorScanner : MonoBehaviour
{
      [Header("Dependencies")]
      [SerializeField] private ARCameraManager cameraManager;

      [Header("Scanning Settings")]
      [Range(1, 9)]
      [SerializeField] private int radius = 4; // радиус в пикселях (итоговый квадрат ~ (2r+1)^2)

      [Range(1, 3)]
      [SerializeField] private int samplingStep = 2; // шаг выборки для снижения шума

      [SerializeField] private float timeoutSeconds = 0.5f; // таймаут на попытку захвата

      [Header("Calibration (Optional)")]
      [SerializeField] private bool useExposureNormalization = false;
      [SerializeField] private Color whitePoint = Color.white; // калибровочная белая точка

      [Header("Debug")]
      [SerializeField] private bool enableDebugLogs = false;

      // События
      public event Action<Color, Vector2> OnColorScanned; // (rgb, screenPos)
      public event Action<string> OnScanError;

      // Состояние
      private bool isScanning = false;
      private int retryCount = 0;
      private const int MAX_RETRIES = 1;

      void Awake()
      {
            if (cameraManager == null)
            {
                  cameraManager = FindObjectOfType<ARCameraManager>();
                  if (cameraManager == null)
                  {
                        Debug.LogError("ColorScanner: ARCameraManager не найден!");
                        enabled = false;
                  }
            }
      }

      /// <summary>
      /// Главный метод сканирования цвета в точке экрана
      /// </summary>
      public void ScanColorAtScreenPos(Vector2 screenPos)
      {
            if (isScanning)
            {
                  if (enableDebugLogs) Debug.Log("🎨 ColorScanner: Сканирование уже выполняется...");
                  return;
            }

            if (cameraManager == null)
            {
                  OnScanError?.Invoke("ARCameraManager не доступен");
                  return;
            }

            StartCoroutine(ScanColorCoroutine(screenPos));
      }

      /// <summary>
      /// Калибровка по белой точке (вызывать один раз на сессию)
      /// </summary>
      public void CalibrateWhitePoint(Vector2 whiteCardPosition)
      {
            if (enableDebugLogs) Debug.Log("🎨 ColorScanner: Калибровка белой точки...");

            StartCoroutine(ScanColorCoroutine(whiteCardPosition, true));
      }

      private System.Collections.IEnumerator ScanColorCoroutine(Vector2 screenPos, bool isCalibration = false)
      {
            isScanning = true;
            retryCount = 0;

            float startTime = Time.time;

            while (retryCount <= MAX_RETRIES && Time.time - startTime < timeoutSeconds)
            {
                  if (cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                  {
                        Color scannedColor = ProcessCpuImage(image, screenPos);
                        image.Dispose();

                        if (isCalibration)
                        {
                              // Сохраняем как белую точку для нормализации
                              whitePoint = scannedColor;
                              useExposureNormalization = true;
                              if (enableDebugLogs) Debug.Log($"🎨 Белая точка установлена: {ColorUtility.ToHtmlStringRGB(whitePoint)}");
                        }
                        else
                        {
                              // Применяем нормализацию если включена
                              if (useExposureNormalization)
                              {
                                    scannedColor = NormalizeByWhitePoint(scannedColor);
                              }

                              // Оцениваем надежность результата
                              bool isReliable = EvaluateReliability(scannedColor);

                              OnColorScanned?.Invoke(scannedColor, screenPos);

                              if (enableDebugLogs)
                              {
                                    Debug.Log($"🎨 Отсканирован цвет: {ColorUtility.ToHtmlStringRGB(scannedColor)} " +
                                            $"в позиции {screenPos}, надежность: {isReliable}");
                              }
                        }

                        isScanning = false;
                        yield break;
                  }

                  retryCount++;
                  yield return new WaitForSeconds(0.1f); // небольшая пауза перед ретраем
            }

            // Таймаут или превышение ретраев
            OnScanError?.Invoke($"Не удалось захватить кадр камеры за {timeoutSeconds}с");
            isScanning = false;
      }

      /// <summary>
      /// Обрабатывает CPU image с median-of-means алгоритмом
      /// </summary>
      private Color ProcessCpuImage(XRCpuImage image, Vector2 screenPos)
      {
            // Преобразуем в RGBA32
            var conversionParams = new XRCpuImage.ConversionParams
            {
                  inputRect = new RectInt(0, 0, image.width, image.height),
                  outputDimensions = new Vector2Int(image.width, image.height),
                  outputFormat = TextureFormat.RGBA32,
                  transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = image.GetConvertedDataSize(conversionParams);
            using (var buffer = new NativeArray<byte>(size, Allocator.Temp))
            {
                  image.Convert(conversionParams, buffer);

                  // Переводим screen → pixel координаты
                  int px = Mathf.Clamp(Mathf.RoundToInt(screenPos.x / Screen.width * image.width), 0, image.width - 1);
                  int py = Mathf.Clamp(Mathf.RoundToInt(screenPos.y / Screen.height * image.height), 0, image.height - 1);

                  return ExtractColorWithMedianOfMeans(buffer, px, py, image.width, image.height);
            }
      }

      /// <summary>
      /// Median-of-means алгоритм для устойчивого извлечения цвета
      /// </summary>
      private Color ExtractColorWithMedianOfMeans(NativeArray<byte> buffer, int px, int py, int width, int height)
      {
            int r = Mathf.Clamp(radius, 1, 8);
            var luminanceValues = new System.Collections.Generic.List<float>(64);

            // Первый проход: собираем значения освещенности для определения медианы
            for (int by = -r; by <= r; by += samplingStep)
            {
                  for (int bx = -r; bx <= r; bx += samplingStep)
                  {
                        int x = Mathf.Clamp(px + bx, 0, width - 1);
                        int y = Mathf.Clamp(py + by, 0, height - 1);
                        int idx = (y * width + x) * 4;

                        float red = buffer[idx] / 255f;
                        float green = buffer[idx + 1] / 255f;
                        float blue = buffer[idx + 2] / 255f;

                        // Вычисляем освещенность (luma) по стандарту ITU-R BT.709
                        float luma = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
                        luminanceValues.Add(luma);
                  }
            }

            luminanceValues.Sort();

            // Находим медианное значение освещенности
            float medianLuma = luminanceValues[luminanceValues.Count / 2];

            // Определяем диапазон для выборки "средних" значений (исключаем выбросы)
            int take = Mathf.Max(4, luminanceValues.Count / 4);
            float loThreshold = luminanceValues[(luminanceValues.Count - take) / 2];
            float hiThreshold = luminanceValues[(luminanceValues.Count + take) / 2];

            // Второй проход: усредняем только "средние" значения
            float sumR = 0, sumG = 0, sumB = 0;
            int validCount = 0;

            for (int by = -r; by <= r; by += samplingStep)
            {
                  for (int bx = -r; bx <= r; bx += samplingStep)
                  {
                        int x = Mathf.Clamp(px + bx, 0, width - 1);
                        int y = Mathf.Clamp(py + by, 0, height - 1);
                        int idx = (y * width + x) * 4;

                        float red = buffer[idx] / 255f;
                        float green = buffer[idx + 1] / 255f;
                        float blue = buffer[idx + 2] / 255f;
                        float luma = 0.2126f * red + 0.7152f * green + 0.0722f * blue;

                        // Берем только значения в "среднем" диапазоне освещенности
                        if (luma >= loThreshold && luma <= hiThreshold)
                        {
                              sumR += red;
                              sumG += green;
                              sumB += blue;
                              validCount++;
                        }
                  }
            }

            // Возвращаем усредненный цвет или черный если нет валидных значений
            return (validCount > 0) ?
                new Color(sumR / validCount, sumG / validCount, sumB / validCount, 1f) :
                Color.black;
      }

      /// <summary>
      /// Нормализация цвета по белой точке для компенсации освещения
      /// </summary>
      private Color NormalizeByWhitePoint(Color color)
      {
            if (whitePoint == Color.black) return color;

            // Простая нормализация: делим на белую точку и умножаем на идеальный белый
            float normalizedR = Mathf.Clamp01(color.r / whitePoint.r);
            float normalizedG = Mathf.Clamp01(color.g / whitePoint.g);
            float normalizedB = Mathf.Clamp01(color.b / whitePoint.b);

            return new Color(normalizedR, normalizedG, normalizedB, 1f);
      }

      /// <summary>
      /// Оценивает надежность отсканированного цвета
      /// </summary>
      private bool EvaluateReliability(Color color)
      {
            // Простые эвристики для оценки надежности:

            // 1. Не слишком темный (может быть тенью)
            float brightness = (color.r + color.g + color.b) / 3f;
            if (brightness < 0.1f) return false;

            // 2. Не переэкспонированный
            if (brightness > 0.95f) return false;

            // 3. Есть контраст между каналами (не чисто серый)
            float maxChannel = Mathf.Max(color.r, color.g, color.b);
            float minChannel = Mathf.Min(color.r, color.g, color.b);
            float contrast = maxChannel - minChannel;

            // Если контраст слишком низкий, может быть серая карта или блик
            return contrast > 0.05f;
      }

      /// <summary>
      /// Получает текущие настройки сканера для сохранения в проект
      /// </summary>
      public ColorScannerSettings GetCurrentSettings()
      {
            return new ColorScannerSettings
            {
                  radius = this.radius,
                  samplingStep = this.samplingStep,
                  useExposureNormalization = this.useExposureNormalization,
                  whitePoint = this.whitePoint
            };
      }

      /// <summary>
      /// Применяет настройки к сканеру
      /// </summary>
      public void ApplySettings(ColorScannerSettings settings)
      {
            this.radius = settings.radius;
            this.samplingStep = settings.samplingStep;
            this.useExposureNormalization = settings.useExposureNormalization;
            this.whitePoint = settings.whitePoint;
      }
}

/// <summary>
/// Настройки сканера цветов для сохранения/загрузки
/// </summary>
[System.Serializable]
public struct ColorScannerSettings
{
      public int radius;
      public int samplingStep;
      public bool useExposureNormalization;
      public Color whitePoint;
}

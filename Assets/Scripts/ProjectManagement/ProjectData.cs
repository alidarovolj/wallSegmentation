using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Структуры данных для сохранения проектов дизайна.
/// Подготовлены для будущей миграции на SQLite с сохранением обратной совместимости.
/// </summary>

/// <summary>
/// Основная структура сохраненного проекта
/// </summary>
[System.Serializable]
public struct SavedProject
{
      public const int CURRENT_VERSION = 1;

      // Основные поля
      public string id;
      public string name;
      public string description; // опциональное описание проекта
      public long created;       // Unix timestamp
      public long modified;      // Unix timestamp
      public int version;        // версия формата данных для миграций

      // Состояние AR сцены
      public CameraState? cameraState;
      public Dictionary<int, string> appliedColors; // classId -> colorId/hex
      public SegmentationSettings? segmentationSettings;
      public ColorScannerSettings? scannerSettings;

      // Пользовательские данные
      public string[] tags;           // теги для поиска и категоризации
      public bool isFavorite;         // избранный проект
      public string roomType;         // тип комнаты: спальня, гостиная, etc.
      public Vector2 roomSize;        // размеры комнаты в метрах (опционально)

      // Метаданные
      public string deviceModel;      // модель устройства для статистики
      public string appVersion;       // версия приложения
      public ProjectQualityMetrics qualityMetrics; // метрики качества сегментации

      /// <summary>
      /// Создает новый проект с базовыми параметрами
      /// </summary>
      public static SavedProject CreateNew(string name, string description = "")
      {
            return new SavedProject
            {
                  id = Guid.NewGuid().ToString("N"),
                  name = name,
                  description = description,
                  created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                  modified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                  version = CURRENT_VERSION,
                  appliedColors = new Dictionary<int, string>(),
                  tags = new string[0],
                  isFavorite = false,
                  deviceModel = SystemInfo.deviceModel,
                  appVersion = Application.version
            };
      }

      /// <summary>
      /// Обновляет время изменения проекта
      /// </summary>
      public void Touch()
      {
            modified = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      }

      /// <summary>
      /// Получает дату создания как DateTime
      /// </summary>
      public DateTime CreatedDateTime => DateTimeOffset.FromUnixTimeSeconds(created).DateTime;

      /// <summary>
      /// Получает дату изменения как DateTime
      /// </summary>
      public DateTime ModifiedDateTime => DateTimeOffset.FromUnixTimeSeconds(modified).DateTime;

      /// <summary>
      /// Проверяет валидность проекта
      /// </summary>
      public bool IsValid => !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name) && created > 0;
}

/// <summary>
/// Состояние AR камеры
/// </summary>
[System.Serializable]
public struct CameraState
{
      public Vector3 position;
      public Quaternion rotation;
      public float fov;                    // поле зрения
      public Vector2Int resolution;        // разрешение камеры
      public float nearClip;              // ближняя плоскость отсечения
      public float farClip;               // дальняя плоскость отсечения

      // AR-специфичные данные
      public Matrix4x4 projectionMatrix;  // матрица проекции AR камеры
      public Matrix4x4 worldToCameraMatrix; // матрица world-to-camera
      public LightEstimationData lightEstimation; // данные об освещении

      public static CameraState FromCamera(Camera camera)
      {
            return new CameraState
            {
                  position = camera.transform.position,
                  rotation = camera.transform.rotation,
                  fov = camera.fieldOfView,
                  resolution = new Vector2Int(Screen.width, Screen.height),
                  nearClip = camera.nearClipPlane,
                  farClip = camera.farClipPlane,
                  projectionMatrix = camera.projectionMatrix,
                  worldToCameraMatrix = camera.worldToCameraMatrix
            };
      }
}

/// <summary>
/// Данные об освещении сцены
/// </summary>
[System.Serializable]
public struct LightEstimationData
{
      public float averageBrightness;      // средняя яркость
      public float averageColorTemperature; // цветовая температура
      public Color colorCorrection;        // коррекция цвета
      public Vector3 mainLightDirection;   // направление основного света
      public float mainLightIntensity;     // интенсивность основного света
}

/// <summary>
/// Настройки сегментации для воспроизведения результата
/// </summary>
[System.Serializable]
public struct SegmentationSettings
{
      // Базовые настройки
      public Vector2Int processingResolution;
      public bool useSegFormerModels;
      public string segformerModelType;       // B0_512x512, B5_640x640, etc.
      public bool useImageNetNormalization;

      // Настройки качества
      public bool enableMaskSmoothing;
      public int maskSmoothingIterations;
      public bool enableTemporalStabilization;
      public bool useBilinearUpscaling;

      // Настройки производительности
      public int frameSkipRate;
      public bool enableAdaptiveResolution;
      public QualityMode qualityMode;         // максимальное качество, сбалансированный, производительность

      // Настройки отображения
      public bool showAllClasses;
      public int[] visibleClasses;           // какие классы показывать
      public float maskOpacity;
      public bool enableBlinkingEffect;

      public static SegmentationSettings Default => new SegmentationSettings
      {
            processingResolution = new Vector2Int(512, 512),
            useSegFormerModels = true,
            segformerModelType = "B0_512x512",
            useImageNetNormalization = true,
            enableMaskSmoothing = true,
            maskSmoothingIterations = 2,
            enableTemporalStabilization = true,
            useBilinearUpscaling = true,
            frameSkipRate = 2,
            enableAdaptiveResolution = true,
            qualityMode = QualityMode.Balanced,
            showAllClasses = false,
            visibleClasses = new int[] { 0 }, // только стены
            maskOpacity = 0.7f,
            enableBlinkingEffect = false
      };
}

// ColorScannerSettings определена в ColorScanner.cs - используем оттуда

/// <summary>
/// Метрики качества для анализа проекта
/// </summary>
[System.Serializable]
public struct ProjectQualityMetrics
{
      // Метрики сегментации
      public float averageConfidence;        // средняя уверенность модели
      public float maskStability;           // стабильность маски по времени
      public int totalFramesProcessed;      // всего обработано кадров
      public float averageProcessingTime;   // среднее время обработки кадра

      // Метрики сканирования цветов
      public int totalColorScans;           // всего сканирований цвета
      public float averageColorDeltaE;      // средний ΔE сканированных цветов
      public int reliableScans;             // количество надежных сканирований

      // Метрики производительности
      public float averageFPS;              // средний FPS во время сессии
      public float minFPS;                  // минимальный FPS
      public float maxMemoryUsage;          // пик использования памяти (MB)
      public float sessionDurationMinutes;  // длительность сессии в минутах

      // Метрики устройства
      public string devicePerformanceClass; // Low, Medium, High
      public float batteryLevel;            // уровень батареи в начале/конце
      public float deviceTemperature;       // температура устройства
}

/// <summary>
/// Режимы качества сегментации
/// </summary>
public enum QualityMode
{
      Performance = 0,    // максимальная производительность
      Balanced = 1,       // сбалансированный режим
      Quality = 2         // максимальное качество
}

/// <summary>
/// Информация о примененном цвете
/// </summary>
[System.Serializable]
public struct AppliedColor
{
      public int classId;                   // ID класса (стена, пол, etc.)
      public string colorId;                // ID цвета в каталоге
      public string hexColor;               // hex представление
      public string colorName;              // название цвета
      public string colorCode;              // код краски (RAL, NCS)
      public string manufacturer;           // производитель краски
      public float deltaE;                  // ΔE от сканированного цвета
      public long appliedTimestamp;         // когда применен
      public Vector2 scanPosition;          // где был отсканирован цвет
}

/// <summary>
/// Структура для будущей миграции на SQLite
/// </summary>
[System.Serializable]
public struct ProjectMigrationInfo
{
      public int fromVersion;
      public int toVersion;
      public string migrationScript;
      public long migrationTimestamp;
      public bool migrationSuccess;
      public string migrationLog;
}

/// <summary>
/// Фильтры для поиска проектов
/// </summary>
[System.Serializable]
public struct ProjectSearchFilters
{
      public string nameContains;           // поиск по имени
      public string[] tags;                 // фильтр по тегам
      public bool? isFavorite;              // только избранные
      public string roomType;               // тип комнаты
      public DateRange dateRange;           // диапазон дат
      public string[] manufacturers;        // производители красок
      public float minQualityScore;         // минимальный балл качества

      public static ProjectSearchFilters Empty => new ProjectSearchFilters
      {
            nameContains = "",
            tags = new string[0],
            isFavorite = null,
            roomType = "",
            manufacturers = new string[0],
            minQualityScore = 0f
      };
}

/// <summary>
/// Диапазон дат для фильтрации
/// </summary>
[System.Serializable]
public struct DateRange
{
      public long fromTimestamp;
      public long toTimestamp;

      public static DateRange LastWeek
      {
            get
            {
                  var now = DateTimeOffset.UtcNow;
                  return new DateRange
                  {
                        fromTimestamp = now.AddDays(-7).ToUnixTimeSeconds(),
                        toTimestamp = now.ToUnixTimeSeconds()
                  };
            }
      }

      public static DateRange LastMonth
      {
            get
            {
                  var now = DateTimeOffset.UtcNow;
                  return new DateRange
                  {
                        fromTimestamp = now.AddMonths(-1).ToUnixTimeSeconds(),
                        toTimestamp = now.ToUnixTimeSeconds()
                  };
            }
      }
}

/// <summary>
/// Результат экспорта проекта
/// </summary>
[System.Serializable]
public struct ProjectExportResult
{
      public bool success;
      public string filePath;               // путь к экспортированному файлу
      public string format;                 // формат экспорта (JSON, ZIP, etc.)
      public long fileSizeBytes;
      public string errorMessage;

      // Опции экспорта
      public bool includeImages;            // включать ли изображения
      public bool includeMetadata;          // включать ли метаданные
      public bool compressData;             // сжимать ли данные
}

/// <summary>
/// Настройки резервного копирования
/// </summary>
[System.Serializable]
public struct BackupSettings
{
      public bool autoBackupEnabled;       // автоматическое резервное копирование
      public int backupIntervalDays;       // интервал резервного копирования
      public int maxBackupsToKeep;         // максимальное количество бэкапов
      public string backupLocation;        // локация для бэкапов
      public bool includeUserData;         // включать пользовательские данные
      public bool compressBackups;         // сжимать бэкапы

      public static BackupSettings Default => new BackupSettings
      {
            autoBackupEnabled = true,
            backupIntervalDays = 7,
            maxBackupsToKeep = 5,
            backupLocation = "local",
            includeUserData = true,
            compressBackups = true
      };
}

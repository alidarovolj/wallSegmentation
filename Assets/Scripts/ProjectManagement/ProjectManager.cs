using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

/// <summary>
/// Менеджер для сохранения и загрузки проектов дизайна.
/// Использует JSON для метаданных и PNG для превью.
/// 
/// Особенности:
/// - Локальное хранение в Application.persistentDataPath
/// - Автоматическая генерация превью 512x512
/// - Метаданные включают примененные цвета и настройки камеры
/// - Подготовка к миграции на SQLite в будущем
/// </summary>
public class ProjectManager : MonoBehaviour
{
      [Header("Settings")]
      [SerializeField] private Vector2Int thumbnailSize = new Vector2Int(512, 512);
      [SerializeField] private int maxProjects = 100; // лимит проектов для производительности
      [SerializeField] private bool enableDebugLogs = false;

      [Header("Dependencies")]
      [SerializeField] private Camera thumbnailCamera; // камера для создания превью
      [SerializeField] private AsyncSegmentationManager segmentationManager;
      [SerializeField] private ColorPaletteManager colorPaletteManager;

      // События
      public event Action<string> OnProjectSaved;     // (projectId)
      public event Action<string> OnProjectLoaded;    // (projectId)
      public event Action<string> OnProjectDeleted;
      public event Action<List<SavedProject>> OnProjectListUpdated;
      public event Action<string> OnError;

      // Кэш проектов для производительности
      private List<SavedProject> cachedProjects = new List<SavedProject>();
      private bool isCacheValid = false;

      // Путь к папке проектов
      private string ProjectsRoot => Path.Combine(Application.persistentDataPath, "projects");

      void Awake()
      {
            // Создаем папку если не существует
            if (!Directory.Exists(ProjectsRoot))
            {
                  Directory.CreateDirectory(ProjectsRoot);
                  if (enableDebugLogs) Debug.Log($"📁 Создана папка проектов: {ProjectsRoot}");
            }

            // Автопоиск зависимостей если не назначены
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            if (colorPaletteManager == null)
                  colorPaletteManager = FindObjectOfType<ColorPaletteManager>();

            if (thumbnailCamera == null)
                  thumbnailCamera = Camera.main;
      }

      void Start()
      {
            // Загружаем кэш проектов при старте
            RefreshProjectCache();
      }

      /// <summary>
      /// Сохраняет текущий проект с автоматическим созданием превью
      /// </summary>
      public void SaveCurrentProject(string projectName)
      {
            if (string.IsNullOrEmpty(projectName))
            {
                  OnError?.Invoke("Имя проекта не может быть пустым");
                  return;
            }

            StartCoroutine(SaveProjectCoroutine(projectName));
      }

      /// <summary>
      /// Сохраняет проект с пользовательским превью
      /// </summary>
      public (string id, bool success) SaveProject(SavedProject project, Texture2D customThumbnail = null)
      {
            try
            {
                  // Генерируем ID если нужно
                  if (string.IsNullOrEmpty(project.id))
                        project.id = Guid.NewGuid().ToString("N");

                  // Обновляем временные метки
                  long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                  if (project.created == 0) project.created = now;
                  project.modified = now;

                  // Создаем папку проекта
                  string projectDir = Path.Combine(ProjectsRoot, project.id);
                  Directory.CreateDirectory(projectDir);

                  // Сохраняем JSON метаданные
                  string jsonPath = Path.Combine(projectDir, "project.json");
                  string json = JsonUtility.ToJson(project, prettyPrint: true);
                  File.WriteAllText(jsonPath, json);

                  // Сохраняем превью
                  if (customThumbnail != null)
                  {
                        SaveThumbnail(project.id, customThumbnail);
                  }

                  // Обновляем кэш
                  InvalidateCache();

                  OnProjectSaved?.Invoke(project.id);

                  if (enableDebugLogs)
                        Debug.Log($"💾 Проект сохранен: {project.name} (ID: {project.id})");

                  return (project.id, true);
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка сохранения проекта: {e.Message}");
                  OnError?.Invoke($"Не удалось сохранить проект: {e.Message}");
                  return (project.id, false);
            }
      }

      /// <summary>
      /// Загружает проект по ID
      /// </summary>
      public bool LoadProject(string projectId)
      {
            if (string.IsNullOrEmpty(projectId))
            {
                  OnError?.Invoke("ID проекта не указан");
                  return false;
            }

            try
            {
                  string jsonPath = Path.Combine(ProjectsRoot, projectId, "project.json");
                  if (!File.Exists(jsonPath))
                  {
                        OnError?.Invoke($"Проект {projectId} не найден");
                        return false;
                  }

                  string json = File.ReadAllText(jsonPath);
                  var project = JsonUtility.FromJson<SavedProject>(json);

                  // Применяем настройки проекта
                  ApplyProjectSettings(project);

                  OnProjectLoaded?.Invoke(projectId);

                  if (enableDebugLogs)
                        Debug.Log($"📂 Проект загружен: {project.name}");

                  return true;
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка загрузки проекта: {e.Message}");
                  OnError?.Invoke($"Не удалось загрузить проект: {e.Message}");
                  return false;
            }
      }

      /// <summary>
      /// Получает список всех проектов (с кэшированием)
      /// </summary>
      public List<SavedProject> GetAllProjects()
      {
            if (!isCacheValid)
            {
                  RefreshProjectCache();
            }

            return cachedProjects.ToList(); // возвращаем копию
      }

      /// <summary>
      /// Алиас для GetAllProjects() для совместимости
      /// </summary>
      public List<SavedProject> LoadAll()
      {
            return GetAllProjects();
      }

      /// <summary>
      /// Сохраняет проект по имени (обертка для простоты)
      /// </summary>
      public void SaveProject(string projectName)
      {
            SaveCurrentProject(projectName);
      }

      /// <summary>
      /// Удаляет проект
      /// </summary>
      public bool DeleteProject(string projectId)
      {
            if (string.IsNullOrEmpty(projectId))
            {
                  OnError?.Invoke("ID проекта не указан");
                  return false;
            }

            try
            {
                  string projectDir = Path.Combine(ProjectsRoot, projectId);
                  if (Directory.Exists(projectDir))
                  {
                        Directory.Delete(projectDir, true);
                        InvalidateCache();
                        OnProjectDeleted?.Invoke(projectId);

                        if (enableDebugLogs)
                              Debug.Log($"🗑️ Проект удален: {projectId}");

                        return true;
                  }
                  else
                  {
                        OnError?.Invoke("Проект не найден");
                        return false;
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка удаления проекта: {e.Message}");
                  OnError?.Invoke($"Не удалось удалить проект: {e.Message}");
                  return false;
            }
      }

      /// <summary>
      /// Загружает превью проекта
      /// </summary>
      public Texture2D LoadThumbnail(string projectId)
      {
            if (string.IsNullOrEmpty(projectId)) return null;

            try
            {
                  string thumbPath = Path.Combine(ProjectsRoot, projectId, "thumb.png");
                  if (!File.Exists(thumbPath)) return null;

                  byte[] bytes = File.ReadAllBytes(thumbPath);
                  var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                  texture.LoadImage(bytes);
                  return texture;
            }
            catch (Exception e)
            {
                  if (enableDebugLogs)
                        Debug.LogWarning($"⚠️ Не удалось загрузить превью: {e.Message}");
                  return null;
            }
      }

      /// <summary>
      /// Дублирует существующий проект
      /// </summary>
      public string DuplicateProject(string sourceProjectId, string newName)
      {
            try
            {
                  string sourceDir = Path.Combine(ProjectsRoot, sourceProjectId);
                  if (!Directory.Exists(sourceDir))
                  {
                        OnError?.Invoke("Исходный проект не найден");
                        return null;
                  }

                  // Загружаем исходный проект
                  string sourceJson = File.ReadAllText(Path.Combine(sourceDir, "project.json"));
                  var sourceProject = JsonUtility.FromJson<SavedProject>(sourceJson);

                  // Создаем копию с новыми данными
                  var newProject = sourceProject;
                  newProject.id = Guid.NewGuid().ToString("N");
                  newProject.name = newName;
                  newProject.created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                  newProject.modified = newProject.created;

                  // Копируем превью если есть
                  var thumbnail = LoadThumbnail(sourceProjectId);

                  var (newId, success) = SaveProject(newProject, thumbnail);

                  if (thumbnail != null)
                        DestroyImmediate(thumbnail);

                  return success ? newId : null;
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка дублирования проекта: {e.Message}");
                  OnError?.Invoke($"Не удалось дублировать проект: {e.Message}");
                  return null;
            }
      }

      /// <summary>
      /// Получает статистику по проектам
      /// </summary>
      public ProjectStatistics GetStatistics()
      {
            var projects = GetAllProjects();

            return new ProjectStatistics
            {
                  totalProjects = projects.Count,
                  totalSizeBytes = CalculateTotalSize(),
                  oldestProject = projects.OrderBy(p => p.created).FirstOrDefault(),
                  newestProject = projects.OrderByDescending(p => p.created).FirstOrDefault(),
                  mostUsedColors = GetMostUsedColors(projects)
            };
      }

      #region Private Methods

      private System.Collections.IEnumerator SaveProjectCoroutine(string projectName)
      {
            // Создаем превью синхронно
            Texture2D thumbnail = CaptureThumbnailSync();

            // Даем один кадр для обработки
            yield return null;

            // Собираем данные проекта
            var project = CreateProjectFromCurrentState(projectName);

            // Сохраняем
            var (id, success) = SaveProject(project, thumbnail);

            // Очищаем временную текстуру
            if (thumbnail != null)
                  DestroyImmediate(thumbnail);

            if (!success)
            {
                  OnError?.Invoke("Не удалось сохранить проект");
            }
      }

      private Texture2D CaptureThumbnailSync()
      {
            if (thumbnailCamera == null)
            {
                  Debug.LogWarning("⚠️ Камера для превью не назначена");
                  return null;
            }

            // Создаем RenderTexture для захвата
            var renderTexture = new RenderTexture(thumbnailSize.x, thumbnailSize.y, 24);
            var previousTarget = thumbnailCamera.targetTexture;

            try
            {
                  thumbnailCamera.targetTexture = renderTexture;
                  thumbnailCamera.Render();

                  // Копируем в Texture2D
                  RenderTexture.active = renderTexture;
                  var thumbnail = new Texture2D(thumbnailSize.x, thumbnailSize.y, TextureFormat.RGB24, false);
                  thumbnail.ReadPixels(new Rect(0, 0, thumbnailSize.x, thumbnailSize.y), 0, 0);
                  thumbnail.Apply();

                  return thumbnail;
            }
            finally
            {
                  // Восстанавливаем настройки камеры
                  thumbnailCamera.targetTexture = previousTarget;
                  RenderTexture.active = null;

                  if (renderTexture != null)
                        renderTexture.Release();
            }
      }

      private SavedProject CreateProjectFromCurrentState(string projectName)
      {
            var project = new SavedProject
            {
                  id = Guid.NewGuid().ToString("N"),
                  name = projectName,
                  created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                  modified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                  version = SavedProject.CURRENT_VERSION
            };

            // Сохраняем состояние камеры
            if (thumbnailCamera != null)
            {
                  project.cameraState = new CameraState
                  {
                        position = thumbnailCamera.transform.position,
                        rotation = thumbnailCamera.transform.rotation,
                        fov = thumbnailCamera.fieldOfView
                  };
            }

            // Сохраняем примененные цвета
            if (segmentationManager != null)
            {
                  project.appliedColors = GetCurrentAppliedColors();
            }

            // Сохраняем настройки сегментации
            if (segmentationManager != null)
            {
                  project.segmentationSettings = GetCurrentSegmentationSettings();
            }

            return project;
      }

      private Dictionary<int, string> GetCurrentAppliedColors()
      {
            var colors = new Dictionary<int, string>();

            // Получаем цвета из segmentationManager
            // Это нужно будет интегрировать с существующим API
            if (segmentationManager != null)
            {
                  // TODO: Добавить метод в AsyncSegmentationManager для получения примененных цветов
                  // Пример: var appliedColors = segmentationManager.GetAppliedColors();
            }

            return colors;
      }

      private SegmentationSettings GetCurrentSegmentationSettings()
      {
            if (segmentationManager == null) return new SegmentationSettings();

            return new SegmentationSettings
            {
                  // TODO: Интегрировать с существующими настройками AsyncSegmentationManager
                  processingResolution = new Vector2Int(512, 512), // значение по умолчанию
                  useSegFormerModels = true, // значение по умолчанию
                  segformerModelType = "B0_512x512", // значение по умолчанию
                  enableMaskSmoothing = true,
                  maskSmoothingIterations = 2
            };
      }

      private void ApplyProjectSettings(SavedProject project)
      {
            // Применяем настройки камеры
            if (thumbnailCamera != null && project.cameraState.HasValue)
            {
                  var camState = project.cameraState.Value;
                  thumbnailCamera.transform.position = camState.position;
                  thumbnailCamera.transform.rotation = camState.rotation;
                  thumbnailCamera.fieldOfView = camState.fov;
            }

            // Применяем цвета к сегментации
            if (segmentationManager != null && project.appliedColors != null)
            {
                  foreach (var colorPair in project.appliedColors)
                  {
                        // TODO: Интегрировать с AsyncSegmentationManager
                        // segmentationManager.SetClassColor(colorPair.Key, colorPair.Value);
                  }
            }

            // Применяем настройки сегментации  
            if (segmentationManager != null && project.segmentationSettings.HasValue)
            {
                  // TODO: Интегрировать с AsyncSegmentationManager
                  // segmentationManager.ApplySettings(project.segmentationSettings.Value);
            }
      }

      private void SaveThumbnail(string projectId, Texture2D thumbnail)
      {
            if (thumbnail == null) return;

            try
            {
                  string thumbPath = Path.Combine(ProjectsRoot, projectId, "thumb.png");
                  byte[] pngData = thumbnail.EncodeToPNG();
                  File.WriteAllBytes(thumbPath, pngData);
            }
            catch (Exception e)
            {
                  Debug.LogWarning($"⚠️ Не удалось сохранить превью: {e.Message}");
            }
      }

      private void RefreshProjectCache()
      {
            cachedProjects.Clear();

            if (!Directory.Exists(ProjectsRoot))
            {
                  isCacheValid = true;
                  OnProjectListUpdated?.Invoke(cachedProjects);
                  return;
            }

            try
            {
                  foreach (string projectDir in Directory.GetDirectories(ProjectsRoot))
                  {
                        string jsonPath = Path.Combine(projectDir, "project.json");
                        if (File.Exists(jsonPath))
                        {
                              string json = File.ReadAllText(jsonPath);
                              var project = JsonUtility.FromJson<SavedProject>(json);
                              cachedProjects.Add(project);
                        }
                  }

                  // Сортируем по дате изменения (новые сверху)
                  cachedProjects.Sort((a, b) => b.modified.CompareTo(a.modified));

                  // Ограничиваем количество проектов
                  if (cachedProjects.Count > maxProjects)
                  {
                        cachedProjects = cachedProjects.Take(maxProjects).ToList();
                  }

                  isCacheValid = true;
                  OnProjectListUpdated?.Invoke(cachedProjects);

                  if (enableDebugLogs)
                        Debug.Log($"📋 Загружено {cachedProjects.Count} проектов");
            }
            catch (Exception e)
            {
                  Debug.LogError($"❌ Ошибка загрузки кэша проектов: {e.Message}");
                  OnError?.Invoke($"Не удалось загрузить список проектов: {e.Message}");
            }
      }

      private void InvalidateCache()
      {
            isCacheValid = false;
      }

      private long CalculateTotalSize()
      {
            long totalSize = 0;

            try
            {
                  foreach (string projectDir in Directory.GetDirectories(ProjectsRoot))
                  {
                        foreach (string file in Directory.GetFiles(projectDir, "*", SearchOption.AllDirectories))
                        {
                              totalSize += new FileInfo(file).Length;
                        }
                  }
            }
            catch (Exception e)
            {
                  if (enableDebugLogs)
                        Debug.LogWarning($"⚠️ Не удалось рассчитать размер: {e.Message}");
            }

            return totalSize;
      }

      private List<string> GetMostUsedColors(List<SavedProject> projects)
      {
            var colorCount = new Dictionary<string, int>();

            foreach (var project in projects)
            {
                  if (project.appliedColors != null)
                  {
                        foreach (var color in project.appliedColors.Values)
                        {
                              if (colorCount.ContainsKey(color))
                                    colorCount[color]++;
                              else
                                    colorCount[color] = 1;
                        }
                  }
            }

            return colorCount.OrderByDescending(kv => kv.Value)
                             .Take(10)
                             .Select(kv => kv.Key)
                             .ToList();
      }

      #endregion
}

/// <summary>
/// Статистика по проектам
/// </summary>
[System.Serializable]
public struct ProjectStatistics
{
      public int totalProjects;
      public long totalSizeBytes;
      public SavedProject oldestProject;
      public SavedProject newestProject;
      public List<string> mostUsedColors;
}

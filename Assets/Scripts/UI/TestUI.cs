using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Простой UI для тестирования простой системы цветов
/// </summary>
public class TestUI : MonoBehaviour
{
      [Header("UI Components")]
      [SerializeField] private Button scanColorButton;
      [SerializeField] private Button saveProjectButton;
      [SerializeField] private Button loadProjectsButton;
      [SerializeField] private Button nextColorButton;
      [SerializeField] private Button swapColorsButton;
      [SerializeField] private TextMeshProUGUI statusText;

      [Header("Dependencies")]
      [SerializeField] private ColorScanner colorScanner;
      [SerializeField] private ProjectManager projectManager;
      [SerializeField] private SimpleColorManager simpleColorManager;

      void Start()
      {
            // Автопоиск зависимостей
            if (colorScanner == null)
                  colorScanner = FindObjectOfType<ColorScanner>();
            if (projectManager == null)
                  projectManager = FindObjectOfType<ProjectManager>();
            if (simpleColorManager == null)
                  simpleColorManager = FindObjectOfType<SimpleColorManager>();

            // Настраиваем кнопки
            SetupButtons();

            // Подписываемся на события
            SubscribeToEvents();

            UpdateStatus("✅ Тестовый UI готов");
      }

      void SetupButtons()
      {
            if (scanColorButton != null)
            {
                  scanColorButton.onClick.AddListener(() =>
                  {
                        ScanWallColor();
                  });
            }

            if (saveProjectButton != null)
            {
                  saveProjectButton.onClick.AddListener(() =>
                  {
                        SaveTestProject();
                  });
            }

            if (loadProjectsButton != null)
            {
                  loadProjectsButton.onClick.AddListener(() =>
                  {
                        ListProjects();
                  });
            }

            if (nextColorButton != null)
            {
                  nextColorButton.onClick.AddListener(() =>
                  {
                        NextPaintColor();
                  });
            }

            if (swapColorsButton != null)
            {
                  swapColorsButton.onClick.AddListener(() =>
                  {
                        SwapColors();
                  });
            }
      }

      void SubscribeToEvents()
      {
            if (colorScanner != null)
            {
                  colorScanner.OnColorScanned += OnColorScanned;
            }

            if (projectManager != null)
            {
                  projectManager.OnProjectSaved += OnProjectSaved;
                  projectManager.OnError += OnProjectError;
            }

            if (simpleColorManager != null)
            {
                  simpleColorManager.OnOriginalColorChanged += OnOriginalColorChanged;
                  simpleColorManager.OnPaintColorChanged += OnPaintColorChanged;
            }
      }

      /// <summary>
      /// Сканирует цвет стены через SimpleColorManager
      /// </summary>
      public void ScanWallColor()
      {
            if (simpleColorManager != null)
            {
                  simpleColorManager.ScanWallColor();
                  UpdateStatus("🎨 Сканирование цвета стены...");
            }
            else
            {
                  UpdateStatus("❌ SimpleColorManager не найден");
            }
      }

      /// <summary>
      /// Переключает на следующий цвет перекраски
      /// </summary>
      public void NextPaintColor()
      {
            if (simpleColorManager != null)
            {
                  simpleColorManager.NextPaintColor();
                  UpdateStatus("➡️ Следующий цвет");
            }
            else
            {
                  UpdateStatus("❌ SimpleColorManager не найден");
            }
      }

      /// <summary>
      /// Меняет местами исходный цвет и цвет перекраски
      /// </summary>
      public void SwapColors()
      {
            if (simpleColorManager != null)
            {
                  simpleColorManager.SwapColors();
                  UpdateStatus("🔄 Цвета поменяны местами");
            }
            else
            {
                  UpdateStatus("❌ SimpleColorManager не найден");
            }
      }

      /// <summary>
      /// Сохраняет тестовый проект
      /// </summary>
      public void SaveTestProject()
      {
            if (projectManager != null)
            {
                  string projectName = $"Тест {System.DateTime.Now:HH:mm:ss}";
                  projectManager.SaveProject(projectName);
                  UpdateStatus("💾 Сохранение проекта...");
            }
            else
            {
                  UpdateStatus("❌ ProjectManager не найден");
            }
      }

      /// <summary>
      /// Показывает список проектов
      /// </summary>
      public void ListProjects()
      {
            if (projectManager != null)
            {
                  var projects = projectManager.LoadAll();
                  UpdateStatus($"📂 Найдено проектов: {projects.Count}");

                  foreach (var project in projects)
                  {
                        Debug.Log($"Проект: {project.name} (создан: {project.CreatedDateTime})");
                  }
            }
            else
            {
                  UpdateStatus("❌ ProjectManager не найден");
            }
      }

      /// <summary>
      /// Получает информацию о текущих цветах
      /// </summary>
      public void ShowCurrentColors()
      {
            if (simpleColorManager != null)
            {
                  var colors = simpleColorManager.GetCurrentColors();
                  string originalHex = ColorUtility.ToHtmlStringRGB(colors.original);
                  string paintHex = ColorUtility.ToHtmlStringRGB(colors.paint);

                  string message = $"🎨 Текущие цвета:\n" +
                                 $"Исходный: #{originalHex}\n" +
                                 $"Перекраска: #{paintHex}\n" +
                                 $"Пресет: {simpleColorManager.CurrentPresetIndex + 1}/8";

                  UpdateStatus(message);
                  Debug.Log($"Текущие цвета: {message}");
            }
            else
            {
                  UpdateStatus("❌ SimpleColorManager не найден");
            }
      }

      #region Event Handlers

      private void OnColorScanned(Color color, Vector2 position)
      {
            string hexColor = ColorUtility.ToHtmlStringRGB(color);
            UpdateStatus($"🎨 Отсканирован цвет: #{hexColor}");
            Debug.Log($"Отсканирован цвет: #{hexColor}, позиция: {position}");
      }

      private void OnOriginalColorChanged(Color color)
      {
            string hexColor = ColorUtility.ToHtmlStringRGB(color);
            UpdateStatus($"🏠 Исходный цвет: #{hexColor}");
            Debug.Log($"Изменен исходный цвет: #{hexColor}");
      }

      private void OnPaintColorChanged(Color color)
      {
            string hexColor = ColorUtility.ToHtmlStringRGB(color);
            int presetIndex = simpleColorManager?.CurrentPresetIndex ?? 0;
            UpdateStatus($"🎨 Цвет перекраски: #{hexColor} (пресет {presetIndex + 1}/8)");
            Debug.Log($"Изменен цвет перекраски: #{hexColor}");
      }

      private void OnProjectSaved(string projectId)
      {
            UpdateStatus($"✅ Проект сохранен (ID: {projectId})");
            Debug.Log($"Проект сохранен с ID: {projectId}");
      }

      private void OnProjectError(string error)
      {
            UpdateStatus($"❌ Ошибка проекта: {error}");
            Debug.LogError($"Ошибка проекта: {error}");
      }

      #endregion

      private void UpdateStatus(string message)
      {
            if (statusText != null)
            {
                  statusText.text = message;
            }
            Debug.Log($"[TestUI] {message}");
      }

      #region Context Menu Methods (для быстрого тестирования в редакторе)

      [ContextMenu("Scan Wall Color")]
      void ContextScanColor() => ScanWallColor();

      [ContextMenu("Save Test Project")]
      void ContextSaveProject() => SaveTestProject();

      [ContextMenu("Next Paint Color")]
      void ContextNextColor() => NextPaintColor();

      [ContextMenu("Swap Colors")]
      void ContextSwapColors() => SwapColors();

      [ContextMenu("Show Current Colors")]
      void ContextShowColors() => ShowCurrentColors();

      #endregion
}

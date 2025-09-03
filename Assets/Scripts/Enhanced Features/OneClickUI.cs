using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Простой UI для системы "один клик"
/// </summary>
public class OneClickUI : MonoBehaviour
{
      [Header("🎮 UI Элементы")]
      [SerializeField] private Button oneClickButton;
      [SerializeField] private Text statusText;
      [SerializeField] private Text instructionText;
      [SerializeField] private GameObject panelUI;

      [Header("🔗 Зависимости")]
      [SerializeField] private OneClickSurfaceGenerator generator;

      [Header("🎨 Настройки")]
      [SerializeField] private Color activeColor = Color.green;
      [SerializeField] private Color inactiveColor = Color.gray;
      [SerializeField] private Color processingColor = Color.yellow;

      private bool isUIVisible = true;

      private void Awake()
      {
            // Автопоиск компонентов
            if (generator == null)
                  generator = FindObjectOfType<OneClickSurfaceGenerator>();

            SetupUI();
      }

      private void Start()
      {
            // Подписываемся на события
            if (generator != null)
            {
                  generator.OnSurfaceGenerated += OnSurfaceGenerated;
                  generator.OnProcessFailed += OnProcessFailed;
            }

            UpdateUI();
      }

      private void Update()
      {
            // Обновляем UI каждый кадр
            UpdateUI();

            // Переключение видимости UI по клавише
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                  ToggleUI();
            }
      }

      /// <summary>
      /// Настраивает UI элементы
      /// </summary>
      private void SetupUI()
      {
            // Создаем UI если не назначен
            if (panelUI == null)
            {
                  CreateSimpleUI();
            }

            // Настраиваем кнопку
            if (oneClickButton != null)
            {
                  oneClickButton.onClick.AddListener(OnOneClickButtonPressed);
            }

            // Устанавливаем начальные тексты
            if (instructionText != null)
            {
                  instructionText.text = "🎯 Нажмите кнопку или коснитесь экрана\nдля создания 3D-меша стены";
            }
      }

      /// <summary>
      /// Создает простой UI
      /// </summary>
      private void CreateSimpleUI()
      {
            // Создаем Canvas если его нет
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                  GameObject canvasGO = new GameObject("OneClick_Canvas");
                  canvas = canvasGO.AddComponent<Canvas>();
                  canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                  canvas.sortingOrder = 100;

                  // Добавляем CanvasScaler
                  CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                  scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                  scaler.referenceResolution = new Vector2(1920, 1080);

                  // Добавляем GraphicRaycaster
                  canvasGO.AddComponent<GraphicRaycaster>();
            }

            // Создаем панель
            GameObject panel = new GameObject("OneClick_Panel");
            panel.transform.SetParent(canvas.transform, false);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.5f);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0.2f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            panelUI = panel;

            // Создаем кнопку
            CreateButton(panel);
            CreateTexts(panel);

            Debug.Log("✅ Создан простой UI для OneClick системы");
      }

      /// <summary>
      /// Создает кнопку
      /// </summary>
      private void CreateButton(GameObject parent)
      {
            GameObject buttonGO = new GameObject("OneClick_Button");
            buttonGO.transform.SetParent(parent.transform, false);

            Image buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = activeColor;

            Button button = buttonGO.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(OnOneClickButtonPressed);

            RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.1f, 0.3f);
            buttonRect.anchorMax = new Vector2(0.4f, 0.8f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            // Добавляем текст на кнопку
            GameObject buttonTextGO = new GameObject("Button_Text");
            buttonTextGO.transform.SetParent(buttonGO.transform, false);

            Text buttonText = buttonTextGO.AddComponent<Text>();
            buttonText.text = "🚀 СОЗДАТЬ\nСТЕНУ";
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 24;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;

            RectTransform textRect = buttonTextGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            oneClickButton = button;
      }

      /// <summary>
      /// Создает текстовые элементы
      /// </summary>
      private void CreateTexts(GameObject parent)
      {
            // Статус
            GameObject statusGO = new GameObject("Status_Text");
            statusGO.transform.SetParent(parent.transform, false);

            Text status = statusGO.AddComponent<Text>();
            status.text = "Готов к работе";
            status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.fontSize = 20;
            status.color = Color.white;
            status.alignment = TextAnchor.MiddleLeft;

            RectTransform statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.45f, 0.6f);
            statusRect.anchorMax = new Vector2(0.9f, 0.9f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            statusText = status;

            // Инструкции
            GameObject instructionGO = new GameObject("Instruction_Text");
            instructionGO.transform.SetParent(parent.transform, false);

            Text instruction = instructionGO.AddComponent<Text>();
            instruction.text = "Нажмите кнопку или коснитесь экрана";
            instruction.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            instruction.fontSize = 16;
            instruction.color = Color.gray;
            instruction.alignment = TextAnchor.MiddleLeft;

            RectTransform instructionRect = instructionGO.GetComponent<RectTransform>();
            instructionRect.anchorMin = new Vector2(0.45f, 0.1f);
            instructionRect.anchorMax = new Vector2(0.9f, 0.5f);
            instructionRect.offsetMin = Vector2.zero;
            instructionRect.offsetMax = Vector2.zero;

            instructionText = instruction;
      }

      /// <summary>
      /// Обновляет состояние UI
      /// </summary>
      private void UpdateUI()
      {
            if (generator == null) return;

            bool isProcessing = generator.IsProcessing();

            // Обновляем кнопку
            if (oneClickButton != null)
            {
                  oneClickButton.interactable = !isProcessing;

                  if (oneClickButton.image != null)
                  {
                        oneClickButton.image.color = isProcessing ? processingColor : activeColor;
                  }
            }

            // Обновляем статус
            if (statusText != null)
            {
                  if (isProcessing)
                  {
                        statusText.text = "⚙️ Генерация меша...";
                        statusText.color = processingColor;
                  }
                  else
                  {
                        statusText.text = "✅ Готов к работе";
                        statusText.color = Color.white;
                  }
            }
      }

      /// <summary>
      /// Обработчик нажатия кнопки
      /// </summary>
      private void OnOneClickButtonPressed()
      {
            if (generator != null && !generator.IsProcessing())
            {
                  // Используем центр экрана для генерации
                  Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                  generator.ForceGenerateAt(screenCenter);

                  Debug.Log("🎯 Запущена генерация через UI кнопку");
            }
      }

      /// <summary>
      /// Переключает видимость UI
      /// </summary>
      private void ToggleUI()
      {
            isUIVisible = !isUIVisible;
            if (panelUI != null)
            {
                  panelUI.SetActive(isUIVisible);
            }

            Debug.Log($"📱 UI переключен: {(isUIVisible ? "ПОКАЗАН" : "СКРЫТ")}");
      }

      /// <summary>
      /// Обработчик успешной генерации
      /// </summary>
      private void OnSurfaceGenerated(GameObject surface)
      {
            if (statusText != null)
            {
                  statusText.text = "🎉 Стена создана!";
                  statusText.color = Color.green;
            }

            Debug.Log("🎉 Поверхность успешно сгенерирована через OneClick!");
      }

      /// <summary>
      /// Обработчик ошибки
      /// </summary>
      private void OnProcessFailed(string error)
      {
            if (statusText != null)
            {
                  statusText.text = $"❌ Ошибка: {error}";
                  statusText.color = Color.red;
            }

            Debug.LogWarning($"❌ OneClick генерация не удалась: {error}");
      }

      private void OnDestroy()
      {
            // Отписываемся от событий
            if (generator != null)
            {
                  generator.OnSurfaceGenerated -= OnSurfaceGenerated;
                  generator.OnProcessFailed -= OnProcessFailed;
            }
      }

      // Публичные методы

      /// <summary>
      /// Программно запускает генерацию
      /// </summary>
      public void TriggerGeneration()
      {
            OnOneClickButtonPressed();
      }

      /// <summary>
      /// Устанавливает видимость UI
      /// </summary>
      public void SetUIVisible(bool visible)
      {
            isUIVisible = visible;
            if (panelUI != null)
            {
                  panelUI.SetActive(visible);
            }
      }
}

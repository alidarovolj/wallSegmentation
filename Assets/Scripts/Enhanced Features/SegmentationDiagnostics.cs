using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Диагностика и настройка сегментации для правильного разделения стен, пола и потолка
/// </summary>
public class SegmentationDiagnostics : MonoBehaviour
{
      [Header("Dependencies")]
      [SerializeField] private AsyncSegmentationManager segmentationManager;

      [Header("ADE20K Class Analysis")]
      [SerializeField] private bool enableClassAnalysis = true;
      [SerializeField] private bool showIndoorClassesOnly = true;

      [Header("Target Classes")]
      [SerializeField] private bool showWalls = true;      // Class 0
      [SerializeField] private bool showFloors = true;     // Class 3  
      [SerializeField] private bool showCeilings = true;   // Class 5
      [SerializeField] private bool showDoors = false;     // Class 14
      [SerializeField] private bool showWindows = false;   // Class 8

      [Header("Visual Settings")]
      [SerializeField] private float classOpacity = 0.8f;
      [SerializeField] private bool useDistinctColors = true;

      // ADE20K Indoor класс маппинг
      private readonly Dictionary<int, ClassInfo> ade20kIndoorClasses = new Dictionary<int, ClassInfo>
    {
        {0, new ClassInfo("wall", "Стены", new Color(1.0f, 0.3f, 0.3f), true)},
        {3, new ClassInfo("floor", "Пол", new Color(0.6f, 0.4f, 0.2f), true)},
        {5, new ClassInfo("ceiling", "Потолок", new Color(0.8f, 0.8f, 0.9f), true)},
        {7, new ClassInfo("bed", "Кровать", new Color(0.9f, 0.6f, 0.9f), false)},
        {8, new ClassInfo("windowpane", "Окно", new Color(0.6f, 0.9f, 1.0f), false)},
        {10, new ClassInfo("cabinet", "Шкаф", new Color(0.7f, 0.5f, 0.3f), false)},
        {14, new ClassInfo("door", "Дверь", new Color(0.5f, 0.3f, 0.1f), false)},
        {15, new ClassInfo("table", "Стол", new Color(0.8f, 0.7f, 0.5f), false)},
        {18, new ClassInfo("curtain", "Штора", new Color(0.7f, 0.2f, 0.7f), false)},
        {19, new ClassInfo("chair", "Стул", new Color(0.4f, 0.6f, 0.8f), false)},
        {23, new ClassInfo("sofa", "Диван", new Color(0.2f, 0.8f, 0.4f), false)},
        {24, new ClassInfo("shelf", "Полка", new Color(0.9f, 0.8f, 0.3f), false)},
        {27, new ClassInfo("mirror", "Зеркало", new Color(0.9f, 0.9f, 0.9f), false)},
        {36, new ClassInfo("lamp", "Лампа", new Color(1.0f, 1.0f, 0.6f), false)},
        {65, new ClassInfo("toilet", "Туалет", new Color(0.9f, 0.9f, 0.9f), false)},
        {71, new ClassInfo("stove", "Плита", new Color(0.3f, 0.3f, 0.3f), false)}
    };

      [System.Serializable]
      private class ClassInfo
      {
            public string name;
            public string displayName;
            public Color color;
            public bool isPrimary; // wall, floor, ceiling

            public ClassInfo(string name, string displayName, Color color, bool isPrimary)
            {
                  this.name = name;
                  this.displayName = displayName;
                  this.color = color;
                  this.isPrimary = isPrimary;
            }
      }

      void Start()
      {
            if (segmentationManager == null)
                  segmentationManager = FindObjectOfType<AsyncSegmentationManager>();

            if (enableClassAnalysis)
            {
                  AnalyzeAndConfigureClasses();
            }
      }

      /// <summary>
      /// Анализирует и настраивает классы для правильного отображения
      /// </summary>
      [ContextMenu("Analyze and Configure Classes")]
      public void AnalyzeAndConfigureClasses()
      {
            if (segmentationManager == null)
            {
                  Debug.LogError("❌ AsyncSegmentationManager не найден!");
                  return;
            }

            Debug.Log("🔍 === ДИАГНОСТИКА СЕГМЕНТАЦИИ ===");

            // Анализируем текущие настройки
            AnalyzeCurrentSettings();

            // Настраиваем для показа основных классов
            ConfigureIndoorClasses();

            // Отправляем список классов в Flutter
            SendClassListToFlutter();

            Debug.Log("✅ Диагностика завершена!");
      }

      private void AnalyzeCurrentSettings()
      {
            Debug.Log("📊 Текущие настройки:");
            Debug.Log($"   📱 Модель: {(segmentationManager != null ? "AsyncSegmentationManager" : "Не найден")}");

            // TODO: Получить текущие настройки через рефлексию если нужно
            Debug.Log($"   🎯 Целевые классы: Стены={showWalls}, Пол={showFloors}, Потолок={showCeilings}");
            Debug.Log($"   🎨 Режим: {(useDistinctColors ? "Разные цвета" : "Единый цвет")}");
      }

      private void ConfigureIndoorClasses()
      {
            Debug.Log("🔧 Настройка классов для indoor сцен:");

            var activeClasses = new List<int>();

            if (showWalls) activeClasses.Add(0);     // wall
            if (showFloors) activeClasses.Add(3);    // floor  
            if (showCeilings) activeClasses.Add(5);  // ceiling
            if (showDoors) activeClasses.Add(14);    // door
            if (showWindows) activeClasses.Add(8);   // windowpane

            foreach (int classId in activeClasses)
            {
                  if (ade20kIndoorClasses.TryGetValue(classId, out ClassInfo classInfo))
                  {
                        Debug.Log($"   ✅ Класс {classId}: {classInfo.displayName} ({classInfo.name}) - {ColorUtility.ToHtmlStringRGB(classInfo.color)}");

                        // Устанавливаем цвет класса
                        SetClassColor(classId, classInfo.color);
                  }
            }

            // Настраиваем режим отображения  
            if (activeClasses.Count == 1)
            {
                  Debug.Log($"🎯 Режим одного класса: {activeClasses[0]}");
                  SetSingleClassMode(activeClasses[0]);
            }
            else if (activeClasses.Count > 1)
            {
                  Debug.Log($"🎯 Режим нескольких классов: {string.Join(", ", activeClasses)}");
                  SetMultiClassMode(activeClasses);
            }
      }

      private void SetClassColor(int classId, Color color)
      {
            try
            {
                  // Пытаемся вызвать методы через рефлексию или прямые вызовы
                  var method = segmentationManager.GetType().GetMethod("SetClassColor",
                      System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                  if (method != null)
                  {
                        method.Invoke(segmentationManager, new object[] { classId, color });
                        Debug.Log($"   🎨 Установлен цвет класса {classId}: {ColorUtility.ToHtmlStringRGB(color)}");
                  }
                  else
                  {
                        Debug.LogWarning($"⚠️ Метод SetClassColor не найден для класса {classId}");
                  }
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка установки цвета класса {classId}: {e.Message}");
            }
      }

      private void SetSingleClassMode(int classId)
      {
            try
            {
                  // Устанавливаем режим одного класса
                  var showAllField = segmentationManager.GetType().GetField("showAllClasses",
                      System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                  if (showAllField != null)
                        showAllField.SetValue(segmentationManager, false);

                  var singleClassField = segmentationManager.GetType().GetField("singleClassId",
                      System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                  if (singleClassField != null)
                        singleClassField.SetValue(segmentationManager, classId);

                  Debug.Log($"🎯 Режим одного класса установлен: {classId}");
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка установки режима одного класса: {e.Message}");
            }
      }

      private void SetMultiClassMode(List<int> classIds)
      {
            try
            {
                  // Устанавливаем режим нескольких классов
                  var showAllField = segmentationManager.GetType().GetField("showAllClasses",
                      System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                  if (showAllField != null)
                        showAllField.SetValue(segmentationManager, true);

                  Debug.Log($"🎯 Режим нескольких классов установлен: [{string.Join(", ", classIds)}]");
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка установки режима нескольких классов: {e.Message}");
            }
      }

      private void SendClassListToFlutter()
      {
            Debug.Log("📱 Отправка списка классов в Flutter:");

            var classes = new List<object>();

            foreach (var kvp in ade20kIndoorClasses)
            {
                  bool isActive = (kvp.Key == 0 && showWalls) ||
                                 (kvp.Key == 3 && showFloors) ||
                                 (kvp.Key == 5 && showCeilings) ||
                                 (kvp.Key == 14 && showDoors) ||
                                 (kvp.Key == 8 && showWindows);

                  if (isActive || !showIndoorClassesOnly)
                  {
                        classes.Add(new
                        {
                              classId = kvp.Key,
                              className = kvp.Value.name,
                              displayName = kvp.Value.displayName,
                              color = ColorUtility.ToHtmlStringRGB(kvp.Value.color),
                              isPrimary = kvp.Value.isPrimary,
                              isActive = isActive
                        });
                  }
            }

            var classListJson = JsonUtility.ToJson(new { classes = classes });
            Debug.Log($"📤 Список классов: {classListJson}");

            // Отправляем через FlutterUnityManager
            var flutterManager = FlutterUnityManager.Instance;
            if (flutterManager != null)
            {
                  flutterManager.SendMessage("onAvailableClasses", classListJson);
                  Debug.Log("✅ Список классов отправлен в Flutter");
            }
            else
            {
                  Debug.LogWarning("⚠️ FlutterUnityManager не найден");
            }
      }

      /// <summary>
      /// Быстрая настройка для показа только стен
      /// </summary>
      [ContextMenu("Show Only Walls")]
      public void ShowOnlyWalls()
      {
            showWalls = true;
            showFloors = false;
            showCeilings = false;
            showDoors = false;
            showWindows = false;
            AnalyzeAndConfigureClasses();
      }

      /// <summary>
      /// Быстрая настройка для показа стен, пола и потолка
      /// </summary>
      [ContextMenu("Show Walls + Floor + Ceiling")]
      public void ShowWallsFloorCeiling()
      {
            showWalls = true;
            showFloors = true;
            showCeilings = true;
            showDoors = false;
            showWindows = false;
            AnalyzeAndConfigureClasses();
      }

      /// <summary>
      /// Сброс к значениям по умолчанию
      /// </summary>
      [ContextMenu("Reset to Defaults")]
      public void ResetToDefaults()
      {
            showWalls = true;
            showFloors = true;
            showCeilings = true;
            showDoors = false;
            showWindows = false;
            useDistinctColors = true;
            classOpacity = 0.8f;
            AnalyzeAndConfigureClasses();
      }

      /// <summary>
      /// Тест всех классов поочередно
      /// </summary>
      [ContextMenu("Test All Classes")]
      public void TestAllClasses()
      {
            StartCoroutine(TestClassesSequentially());
      }

      private System.Collections.IEnumerator TestClassesSequentially()
      {
            Debug.Log("🧪 Тестирование всех классов поочередно...");

            foreach (var kvp in ade20kIndoorClasses)
            {
                  Debug.Log($"🧪 Тестируем класс {kvp.Key}: {kvp.Value.displayName}");

                  // Показываем только этот класс
                  SetSingleClassMode(kvp.Key);
                  SetClassColor(kvp.Key, kvp.Value.color);

                  yield return new WaitForSeconds(3f); // 3 секунды на класс
            }

            Debug.Log("✅ Тестирование завершено. Возвращаемся к настройкам по умолчанию.");
            ResetToDefaults();
      }
}

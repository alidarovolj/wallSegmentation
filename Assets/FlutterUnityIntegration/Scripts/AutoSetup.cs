using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

#if UNITY_EDITOR

/// <summary>
/// Автоматическая настройка проекта для интеграции с Flutter
/// </summary>
public class AutoSetup : EditorWindow
{
    [MenuItem("Flutter/Auto Setup Project")]
    public static void ShowWindow()
    {
        GetWindow<AutoSetup>("Flutter Auto Setup");
    }

    [MenuItem("Flutter/Quick Setup", false, 1)]
    public static void QuickSetup()
    {
        if (EditorUtility.DisplayDialog(
            "Быстрая настройка",
            "Автоматически настроить проект для интеграции с Flutter?",
            "Да", "Отмена"))
        {
            PerformQuickSetup();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Flutter Unity Auto Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Этот инструмент автоматически настроит ваш Unity проект для работы с Flutter.",
            MessageType.Info
        );

        EditorGUILayout.Space();

        if (GUILayout.Button("Выполнить полную настройку"))
        {
            PerformFullSetup();
        }

        EditorGUILayout.Space();

        GUILayout.Label("Отдельные действия:", EditorStyles.boldLabel);

        if (GUILayout.Button("1. Создать FlutterUnityManager на сцене"))
        {
            CreateFlutterUnityManager();
        }

        if (GUILayout.Button("2. Настроить ссылки компонентов"))
        {
            SetupComponentReferences();
        }

        if (GUILayout.Button("3. Настроить Build Settings"))
        {
            SetupBuildSettings();
        }

        if (GUILayout.Button("4. Проверить зависимости"))
        {
            CheckDependencies();
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "После настройки используйте Flutter → Build Settings для сборки проекта.",
            MessageType.Info
        );
    }

    private static void PerformQuickSetup()
    {
        Debug.Log("🚀 Начинаем быструю настройку проекта...");

        CreateFlutterUnityManager();
        SetupComponentReferences();
        SetupBuildSettings();
        CheckDependencies();

        Debug.Log("✅ Быстрая настройка завершена!");
        EditorUtility.DisplayDialog("Готово", "Проект настроен для интеграции с Flutter!", "OK");
    }

    private static void PerformFullSetup()
    {
        Debug.Log("🔧 Выполняем полную настройку проекта...");

        // Выполняем все этапы настройки
        CreateFlutterUnityManager();
        SetupComponentReferences();
        SetupBuildSettings();
        CheckDependencies();
        SetupOptionalComponents();

        Debug.Log("🎯 Полная настройка завершена!");
        EditorUtility.DisplayDialog("Готово", "Проект полностью настроен для Flutter!", "OK");
    }

    private static void CreateFlutterUnityManager()
    {
        Debug.Log("📋 Создаем FlutterUnityManager...");

        // Проверяем, есть ли уже FlutterUnityManager на сцене
        FlutterUnityManager existing = FindObjectOfType<FlutterUnityManager>();
        if (existing != null)
        {
            Debug.Log("ℹ️ FlutterUnityManager уже существует на сцене");
            return;
        }

        // Создаем новый GameObject с FlutterUnityManager
        GameObject managerObject = new GameObject("FlutterUnityManager");
        FlutterUnityManager manager = managerObject.AddComponent<FlutterUnityManager>();

        // Помечаем как не уничтожаемый при загрузке сцены
        managerObject.transform.SetAsFirstSibling();

        Debug.Log("✅ FlutterUnityManager создан");
    }

    private static void SetupComponentReferences()
    {
        Debug.Log("🔗 Настраиваем ссылки компонентов...");

        FlutterUnityManager manager = FindObjectOfType<FlutterUnityManager>();
        if (manager == null)
        {
            Debug.LogError("❌ FlutterUnityManager не найден! Создайте его сначала.");
            return;
        }

        // Находим и назначаем AsyncSegmentationManager
        AsyncSegmentationManager segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager != null)
        {
            // Используем рефлексию для установки приватных полей
            var field = typeof(FlutterUnityManager).GetField("segmentationManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(manager, segmentationManager);
                Debug.Log("✅ AsyncSegmentationManager назначен");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ AsyncSegmentationManager не найден на сцене");
        }

        // Находим и назначаем ColorPaletteManager
        ColorPaletteManager colorManager = FindObjectOfType<ColorPaletteManager>();
        if (colorManager != null)
        {
            var field = typeof(FlutterUnityManager).GetField("colorPaletteManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(manager, colorManager);
                Debug.Log("✅ ColorPaletteManager назначен");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ ColorPaletteManager не найден на сцене");
        }

        // Сохраняем изменения
        EditorUtility.SetDirty(manager);
    }

    private static void SetupBuildSettings()
    {
        Debug.Log("⚙️ Настраиваем Build Settings...");

        // Основные настройки плеера
        PlayerSettings.companyName = "YourCompany";
        PlayerSettings.productName = "RemaluxAR";

        // Настройки для Android
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

        // Настройки для экспорта
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        Debug.Log("✅ Build Settings настроены");
    }

    private static void CheckDependencies()
    {
        Debug.Log("🔍 Проверяем зависимости...");

        bool allGood = true;

        // Проверяем наличие AR Foundation
        if (FindObjectOfType<ARCameraManager>() == null)
        {
            Debug.LogWarning("⚠️ ARCameraManager не найден. Убедитесь, что AR Foundation настроен правильно.");
            allGood = false;
        }

        // Проверяем наличие основных компонентов
        if (FindObjectOfType<AsyncSegmentationManager>() == null)
        {
            Debug.LogWarning("⚠️ AsyncSegmentationManager не найден на сцене");
            allGood = false;
        }

        // Проверяем, что Unity версии совместима
        string unityVersion = Application.unityVersion;
        if (string.Compare(unityVersion, "2022.3") < 0)
        {
            Debug.LogWarning($"⚠️ Рекомендуется Unity 2022.3 или выше. Текущая версия: {unityVersion}");
        }

        if (allGood)
        {
            Debug.Log("✅ Все зависимости в порядке");
        }
        else
        {
            Debug.Log("⚠️ Найдены проблемы с зависимостями. Проверьте предупреждения выше.");
        }
    }

    private static void SetupOptionalComponents()
    {
        Debug.Log("🔧 Настраиваем дополнительные компоненты...");

        // Настройка UnityMessageBridge для обратной совместимости
        UnityMessageBridge bridge = FindObjectOfType<UnityMessageBridge>();
        if (bridge != null)
        {
            // Включаем использование нового менеджера
            var field = typeof(UnityMessageBridge).GetField("useFlutterUnityManager");
            if (field != null)
            {
                field.SetValue(bridge, true);
                EditorUtility.SetDirty(bridge);
                Debug.Log("✅ UnityMessageBridge настроен для использования FlutterUnityManager");
            }
        }

        // Создаем папки для экспорта, если их нет
        string exportPath = "Builds/Flutter";
        if (!System.IO.Directory.Exists(exportPath))
        {
            System.IO.Directory.CreateDirectory(exportPath);
            Debug.Log($"📁 Создана папка для экспорта: {exportPath}");
        }
    }

    [MenuItem("Flutter/Documentation", false, 100)]
    public static void OpenDocumentation()
    {
        string readmePath = "Assets/FlutterUnityIntegration/flutter_integration_guide.md";
        if (System.IO.File.Exists(readmePath))
        {
            Application.OpenURL(readmePath);
        }
        else
        {
            EditorUtility.DisplayDialog("Документация",
                "Документация находится в Assets/FlutterUnityIntegration/flutter_integration_guide.md", "OK");
        }
    }

    [MenuItem("Flutter/Open Integration Guide", false, 101)]
    public static void OpenIntegrationGuide()
    {
        string guidePath = "Assets/FlutterUnityIntegration/integration_to_existing_flutter.md";
        if (System.IO.File.Exists(guidePath))
        {
            Application.OpenURL(guidePath);
        }
        else
        {
            EditorUtility.DisplayDialog("Руководство",
                "Руководство по интеграции не найдено: " + guidePath, "OK");
        }
    }
}

#endif
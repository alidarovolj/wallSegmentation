using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR

/// <summary>
/// Конфигурация сборки Unity проекта для интеграции с Flutter
/// </summary>
public class UnityFlutterBuildSettings : EditorWindow
{
    private static string flutterProjectPath = "";
    private static string unityExportPath = "";
    private static BuildTarget selectedBuildTarget = BuildTarget.Android;

    [MenuItem("Flutter/Build Settings")]
    public static void ShowWindow()
    {
        GetWindow<UnityFlutterBuildSettings>("Unity-Flutter Build Settings");
    }

    private void OnGUI()
    {
        GUILayout.Label("Unity-Flutter Build Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Выбор целевой платформы
        selectedBuildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Target Platform:", selectedBuildTarget);
        EditorGUILayout.Space();

        // Путь к Flutter проекту
        EditorGUILayout.LabelField("Flutter Project Path:");
        EditorGUILayout.BeginHorizontal();
        flutterProjectPath = EditorGUILayout.TextField(flutterProjectPath);
        if (GUILayout.Button("Browse", GUILayout.Width(100)))
        {
            flutterProjectPath = EditorUtility.OpenFolderPanel("Select Flutter Project", "", "");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Путь экспорта Unity
        EditorGUILayout.LabelField("Unity Export Path:");
        EditorGUILayout.BeginHorizontal();
        unityExportPath = EditorGUILayout.TextField(unityExportPath);
        if (GUILayout.Button("Browse", GUILayout.Width(100)))
        {
            unityExportPath = EditorUtility.OpenFolderPanel("Select Export Path", "", "");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Кнопки действий
        if (GUILayout.Button("Setup Build Settings"))
        {
            SetupBuildSettings();
        }

        if (GUILayout.Button("Build for Flutter"))
        {
            BuildForFlutter();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "1. Выберите целевую платформу\n" +
            "2. Укажите путь к Flutter проекту\n" +
            "3. Укажите путь для экспорта Unity\n" +
            "4. Нажмите 'Setup Build Settings' для настройки\n" +
            "5. Нажмите 'Build for Flutter' для сборки",
            MessageType.Info
        );
    }

    private static void SetupBuildSettings()
    {
        Debug.Log("🔧 Настройка параметров сборки для Flutter...");

        // Общие настройки
        PlayerSettings.companyName = "YourCompany";
        PlayerSettings.productName = "RemaluxAR";

        // Настройки для Android
        if (selectedBuildTarget == BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // AR Foundation требования
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Настройки для экспорта в Flutter
            EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            Debug.Log("✅ Android настройки применены");
        }
        // Настройки для iOS
        else if (selectedBuildTarget == BuildTarget.iOS)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "11.0";

            // Настройки для AR
            PlayerSettings.iOS.cameraUsageDescription = "This app uses the camera for AR functionality";

            Debug.Log("✅ iOS настройки применены");
        }

        // Настройки сцен
        SetupScenes();

        Debug.Log("🎯 Настройка завершена!");
    }

    private static void SetupScenes()
    {
        // Указываем путь к главной сцене
        string mainScenePath = "Assets/Scenes/SampleScene.unity";

        if (!File.Exists(mainScenePath))
        {
            Debug.LogError($"❌ Не удается найти главную сцену по пути: {mainScenePath}");
            return;
        }

        // Создаем массив только с одной сценой
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[1];
        scenes[0] = new EditorBuildSettingsScene(mainScenePath, true);

        // Устанавливаем сцены для сборки
        EditorBuildSettings.scenes = scenes;
        Debug.Log($"📋 Добавлена главная сцена в сборку: {mainScenePath}");
    }

    private static void BuildForFlutter()
    {
        if (string.IsNullOrEmpty(unityExportPath))
        {
            Debug.LogError("❌ Не указан путь экспорта!");
            EditorUtility.DisplayDialog("Ошибка", "Укажите путь для экспорта Unity проекта", "OK");
            return;
        }

        Debug.Log($"🚀 Начинаем сборку для {selectedBuildTarget}...");

        string buildPath = Path.Combine(unityExportPath, selectedBuildTarget.ToString());

        // Создаем директорию, если её нет
        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenePaths(),
            locationPathName = buildPath,
            target = selectedBuildTarget,
            options = BuildOptions.None
        };

        // Для Android экспортируем как Gradle проект
        if (selectedBuildTarget == BuildTarget.Android)
        {
            buildPlayerOptions.options = BuildOptions.AcceptExternalModificationsToPlayer;
        }

        var result = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"✅ Сборка завершена успешно! Путь: {buildPath}");

            // Показываем инструкции для интеграции
            ShowIntegrationInstructions(buildPath);
        }
        else
        {
            Debug.LogError($"❌ Ошибка сборки: {result.summary.result}");
        }
    }

    private static string[] GetScenePaths()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        string[] scenePaths = new string[scenes.Length];

        for (int i = 0; i < scenes.Length; i++)
        {
            scenePaths[i] = scenes[i].path;
        }

        return scenePaths;
    }

    private static void ShowIntegrationInstructions(string buildPath)
    {
        string instructions = selectedBuildTarget == BuildTarget.Android ?
            GetAndroidInstructions(buildPath) :
            GetIOSInstructions(buildPath);

        EditorUtility.DisplayDialog("Сборка завершена", instructions, "OK");
    }

    private static string GetAndroidInstructions(string buildPath)
    {
        return $"Android сборка готова!\n\n" +
               $"Путь: {buildPath}\n\n" +
               $"Следующие шаги:\n" +
               $"1. Скопируйте папку сборки в ваш Flutter проект\n" +
               $"2. Добавьте flutter_unity_widget в pubspec.yaml\n" +
               $"3. Следуйте инструкциям интеграции в README.md";
    }

    private static string GetIOSInstructions(string buildPath)
    {
        return $"iOS сборка готова!\n\n" +
               $"Путь: {buildPath}\n\n" +
               $"Следующие шаги:\n" +
               $"1. Откройте .xcodeproj в Xcode\n" +
               $"2. Интегрируйте с Flutter согласно документации\n" +
               $"3. Настройте подписание и профили";
    }
}

#endif
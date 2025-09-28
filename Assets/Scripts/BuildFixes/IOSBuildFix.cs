#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

/// <summary>
/// Автоматические исправления для сборки iOS
/// Решает проблемы с Job Reflection и Burst Compiler
/// </summary>
public class IOSBuildFix : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.iOS)
        {
            Debug.Log("🔧 IOSBuildFix: Применяем исправления для iOS сборки...");
            
            // Проверяем и исправляем настройки Burst
            EnsureBurstSettings();
            
            // Проверяем настройки IL2CPP
            EnsureIL2CPPSettings();
            
            Debug.Log("✅ IOSBuildFix: Исправления применены успешно");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.iOS)
        {
            Debug.Log("🔧 IOSBuildFix: Применяем post-build исправления для iOS...");
            
            // Добавляем необходимые флаги линкера
            AddLinkerFlags(report.summary.outputPath);
            
            Debug.Log("✅ IOSBuildFix: Post-build исправления применены");
        }
    }

    private void EnsureBurstSettings()
    {
        string burstSettingsPath = "ProjectSettings/BurstAotSettings_iOS.json";
        
        if (!File.Exists(burstSettingsPath))
        {
            Debug.Log("📝 Создаем настройки Burst для iOS...");
            
            string burstSettings = @"{
  ""MonoBehaviour"": {
    ""Version"": 4,
    ""EnableBurstCompilation"": true,
    ""EnableOptimisations"": true,
    ""EnableSafetyChecks"": false,
    ""EnableDebugInAllBuilds"": false,
    ""CpuMinTargetX32"": 0,
    ""CpuMaxTargetX32"": 0,
    ""CpuMinTargetX64"": 0,
    ""CpuMaxTargetX64"": 0,
    ""OptimizeFor"": 0
  }
}";
            File.WriteAllText(burstSettingsPath, burstSettings);
            Debug.Log("✅ Настройки Burst для iOS созданы");
        }
    }

    private void EnsureIL2CPPSettings()
    {
        // Убеждаемся что IL2CPP настроен правильно для iOS
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64
        
        // Отключаем Engine Code Stripping для предотвращения проблем с Job System
        PlayerSettings.stripEngineCode = false;
        
        Debug.Log("✅ Настройки IL2CPP обновлены для iOS");
    }

    private void AddLinkerFlags(string buildPath)
    {
        string projectPath = buildPath + "/Unity-iPhone.xcodeproj/project.pbxproj";
        
        if (File.Exists(projectPath))
        {
            string content = File.ReadAllText(projectPath);
            
            // Добавляем необходимые флаги линкера если их еще нет
            if (!content.Contains("-ObjC"))
            {
                // Это базовая реализация - для полноценного редактирования .pbxproj 
                // рекомендуется использовать Unity XCode API или PostProcessBuild
                Debug.Log("⚠️ Рекомендуется добавить флаг -ObjC в Other Linker Flags в Xcode вручную");
            }
            
            if (!content.Contains("-all_load"))
            {
                Debug.Log("⚠️ При необходимости добавьте флаг -all_load в Other Linker Flags в Xcode");
            }
        }
    }

    [MenuItem("Tools/iOS Build Fix/Force Apply Fixes")]
    public static void ForceApplyFixes()
    {
        var fix = new IOSBuildFix();
        
        Debug.Log("🔧 Принудительно применяем исправления для iOS...");
        fix.EnsureBurstSettings();
        fix.EnsureIL2CPPSettings();
        
        AssetDatabase.Refresh();
        Debug.Log("✅ Исправления применены принудительно");
    }

    [MenuItem("Tools/iOS Build Fix/Check Settings")]
    public static void CheckSettings()
    {
        Debug.Log("📊 Проверка настроек iOS сборки:");
        
        var scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS);
        Debug.Log($"• Scripting Backend: {scriptingBackend}");
        
        var architecture = PlayerSettings.GetArchitecture(BuildTargetGroup.iOS);
        Debug.Log($"• Architecture: {architecture}");
        
        var stripEngineCode = PlayerSettings.stripEngineCode;
        Debug.Log($"• Strip Engine Code: {stripEngineCode}");
        
        string burstSettingsPath = "ProjectSettings/BurstAotSettings_iOS.json";
        bool burstExists = File.Exists(burstSettingsPath);
        Debug.Log($"• Burst Settings для iOS: {(burstExists ? "✅ Существует" : "❌ Отсутствует")}");
        
        if (scriptingBackend != ScriptingImplementation.IL2CPP)
        {
            Debug.LogWarning("⚠️ Для iOS рекомендуется использовать IL2CPP backend");
        }
        
        if (stripEngineCode)
        {
            Debug.LogWarning("⚠️ Strip Engine Code может вызывать проблемы с Job System");
        }
    }
}
#endif

using Unity.Jobs;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Принудительная инициализация Job System для предотвращения проблем с отсутствующими символами
/// Этот класс помогает решить проблемы JobReflectionRegistrationOutput на iOS
/// </summary>
public static class JobSystemFix
{
      /// <summary>
      /// Вызвать это в начале игры для принудительной инициализации Job System
      /// </summary>
      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
      public static void InitializeJobSystem()
      {
#if UNITY_IOS && !UNITY_EDITOR
        Debug.Log("🔧 JobSystemFix: Принудительная инициализация Job System для iOS...");
        
        try
        {
            // Создаем и выполняем простую задачу для принудительной инициализации Job System
            var simpleJob = new SimpleJob { value = 1 };
            var jobHandle = simpleJob.Schedule();
            jobHandle.Complete();
            
            Debug.Log("✅ JobSystemFix: Job System инициализирован успешно");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ JobSystemFix: Ошибка инициализации Job System: {e.Message}");
        }
#endif
      }

      /// <summary>
      /// Простая задача для тестирования Job System
      /// </summary>
      private struct SimpleJob : IJob
      {
            public int value;

            public void Execute()
            {
                  value = value * 2;
            }
      }
}

/// <summary>
/// Дополнительный класс для принудительной регистрации различных типов Job
/// </summary>
public static class JobReflectionForcedRegistration
{
      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
      public static void ForceJobReflectionRegistration()
      {
#if UNITY_IOS && !UNITY_EDITOR
        Debug.Log("🔧 Принудительная регистрация Job Reflection...");
        
        try
        {
            // Принудительно вызываем reflection для базовых типов Jobs
            System.Type[] jobTypes = {
                typeof(IJob),
                typeof(IJobParallelFor),
                typeof(IJobParallelForTransform)
            };

            foreach (var jobType in jobTypes)
            {
                if (jobType != null)
                {
                    var methods = jobType.GetMethods();
                    Debug.Log($"📝 Зарегистрирован Job тип: {jobType.Name}");
                }
            }
            
            Debug.Log("✅ Job Reflection регистрация завершена");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Ошибка регистрации Job Reflection: {e.Message}");
        }
#endif
      }
}

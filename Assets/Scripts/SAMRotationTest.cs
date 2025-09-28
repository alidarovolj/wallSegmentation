using UnityEngine;

/// <summary>
/// Простой тестовый скрипт для проверки автоматического исправления поворота SAM маски
/// </summary>
public class SAMRotationTest : MonoBehaviour
{
    [Header("Тестирование")]
    [SerializeField] private AsyncSegmentationManager segmentationManager;
    
    void Start()
    {
        // Автоматически находим AsyncSegmentationManager если не назначен
        if (segmentationManager == null)
        {
            segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        }
        
        if (segmentationManager != null)
        {
            // Ждем немного, чтобы инициализация завершилась
            Invoke(nameof(CheckRotationSettings), 2f);
        }
        else
        {
            Debug.LogWarning("⚠️ AsyncSegmentationManager не найден в сцене");
        }
    }
    
    private void CheckRotationSettings()
    {
        if (segmentationManager != null)
        {
            int rotationMode = segmentationManager.GetMaskRotationMode();
            string[] modeNames = { "+90°", "-90°", "180°", "Без поворота" };
            string modeName = (rotationMode >= 0 && rotationMode < modeNames.Length) ? modeNames[rotationMode] : $"режим {rotationMode}";
            
            Debug.Log($"🔍 ПРОВЕРКА НАСТРОЕК ПОВОРОТА:");
            Debug.Log($"   Текущий режим поворота: {rotationMode} ({modeName})");
            Debug.Log($"   Ожидаемый для SAM: 2 (180°)");
            
            if (rotationMode == 2)
            {
                Debug.Log("✅ Поворот SAM маски настроен ПРАВИЛЬНО!");
            }
            else
            {
                Debug.LogWarning("⚠️ Поворот SAM маски может быть неправильным. Ожидается режим 2 (180°)");
            }
        }
    }
    
    [ContextMenu("Проверить настройки поворота")]
    public void ManualCheck()
    {
        CheckRotationSettings();
    }
}
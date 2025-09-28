using UnityEngine;

/// <summary>
/// Автоматически исправляет назначение модели при старте сцены
/// </summary>
public class AutoFixModels : MonoBehaviour
{
    [Header("Auto Fix Settings")]
    [SerializeField] private bool enableAutoFix = true;
    [SerializeField] private bool fixOnStart = true;
    
    void Start()
    {
        if (enableAutoFix && fixOnStart)
        {
            // Создаем временный объект с ModelFixAssigner
            GameObject tempFixer = new GameObject("TempModelFixer");
            var fixer = tempFixer.AddComponent<ModelFixAssigner>();
            
            // ModelFixAssigner автоматически исправит модель и уничтожит себя
            Debug.Log("🔧 Автоматическое исправление модели запущено...");
        }
        
        // Уничтожаем себя после выполнения
        Destroy(this);
    }
}

using UnityEngine;
using System.Collections;

/// <summary>
/// Стабилизирует маску сегментации и предотвращает мерцание
/// </summary>
public class MaskStabilizer : MonoBehaviour
{
    [Header("Стабилизация")]
    [SerializeField] private bool enableStabilization = true;
    [SerializeField] private float stabilizationDelay = 0.5f;
    [SerializeField] private bool debugMode = true;
    
    [Header("Принудительные настройки")]
    [SerializeField] private bool forceSettings = true;
    [SerializeField] private int targetClass = 0; // Стены
    [SerializeField] private Color targetColor = Color.red;
    
    private AsyncSegmentationManager segmentationManager;
    private ARWallPresenter wallPresenter;
    private Coroutine stabilizationCoroutine;
    
    // Последние примененные настройки
    private int lastAppliedClass = -999;
    private Color lastAppliedColor = Color.clear;
    
    void Start()
    {
        StartCoroutine(InitializeStabilizer());
    }
    
    IEnumerator InitializeStabilizer()
    {
        // Подождать пока система инициализируется
        yield return new WaitForSeconds(2f);
        
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        wallPresenter = FindObjectOfType<ARWallPresenter>();
        
        if (segmentationManager == null)
        {
            Debug.LogError("[MaskStabilizer] ❌ AsyncSegmentationManager не найден!");
            yield break;
        }
        
        Debug.Log("[MaskStabilizer] ✅ Стабилизатор инициализирован");
        
        if (enableStabilization)
        {
            StartStabilization();
        }
        
        if (forceSettings)
        {
            ApplyForceSettings();
        }
    }
    
    void StartStabilization()
    {
        if (stabilizationCoroutine != null)
        {
            StopCoroutine(stabilizationCoroutine);
        }
        
        stabilizationCoroutine = StartCoroutine(StabilizationLoop());
        Debug.Log("[MaskStabilizer] 🔄 Стабилизация запущена");
    }
    
    IEnumerator StabilizationLoop()
    {
        while (enableStabilization)
        {
            yield return new WaitForSeconds(stabilizationDelay);
            
            if (segmentationManager != null)
            {
                CheckAndStabilize();
            }
        }
    }
    
    void CheckAndStabilize()
    {
        // Проверить текущие настройки
        var currentMaterial = segmentationManager.GetComponent<Renderer>()?.material;
        if (currentMaterial != null)
        {
            int currentClass = currentMaterial.GetInt("_SelectedClass");
            Color currentColor = currentMaterial.GetColor("_PaintColor");
            
            if (debugMode)
            {
                Debug.Log($"[MaskStabilizer] 📊 Текущие настройки: Класс={currentClass}, Цвет={ColorUtility.ToHtmlStringRGB(currentColor)}");
            }
            
            // Проверить стабильность
            if (HasSettingsChanged(currentClass, currentColor))
            {
                if (debugMode)
                {
                    Debug.Log($"[MaskStabilizer] 🔄 Настройки изменились, стабилизируем...");
                }
                
                StabilizeSettings(currentClass, currentColor);
            }
        }
    }
    
    bool HasSettingsChanged(int currentClass, Color currentColor)
    {
        return currentClass != lastAppliedClass || 
               !Mathf.Approximately(currentColor.r, lastAppliedColor.r) ||
               !Mathf.Approximately(currentColor.g, lastAppliedColor.g) ||
               !Mathf.Approximately(currentColor.b, lastAppliedColor.b);
    }
    
    void StabilizeSettings(int classId, Color color)
    {
        // Применить настройки стабильно
        if (segmentationManager != null)
        {
            segmentationManager.SetSelectedClass(classId);
            segmentationManager.SetPaintColor(color);
            
            // Материал обновится автоматически через SetSelectedClass и SetPaintColor
        }
        
        // Синхронизировать с ARWallPresenter
        if (wallPresenter != null && classId >= 0)
        {
            wallPresenter.SetClassColor(classId, color);
        }
        
        // Запомнить примененные настройки
        lastAppliedClass = classId;
        lastAppliedColor = color;
        
        if (debugMode)
        {
            Debug.Log($"[MaskStabilizer] ✅ Стабилизировано: Класс={classId}, Цвет={ColorUtility.ToHtmlStringRGB(color)}");
        }
    }
    
    void ApplyForceSettings()
    {
        Debug.Log($"[MaskStabilizer] 🎯 Принудительно применяем: Класс={targetClass}, Цвет={ColorUtility.ToHtmlStringRGB(targetColor)}");
        
        if (segmentationManager != null)
        {
            // Отключить многоцветный режим
            segmentationManager.SetSelectedClass(targetClass);
            segmentationManager.SetPaintColor(targetColor);
            
            // Материал обновится автоматически
        }
        
        if (wallPresenter != null)
        {
            wallPresenter.SetClassColor(targetClass, targetColor);
        }
        
        lastAppliedClass = targetClass;
        lastAppliedColor = targetColor;
    }
    
    /// <summary>
    /// Установить цвет стабильно
    /// </summary>
    public void SetColorStable(int classId, Color color)
    {
        Debug.Log($"[MaskStabilizer] 🎨 Устанавливаем стабильно: Класс={classId}, Цвет={ColorUtility.ToHtmlStringRGB(color)}");
        
        // Остановить автоматическую стабилизацию на время
        bool wasEnabled = enableStabilization;
        enableStabilization = false;
        
        // Применить настройки
        StabilizeSettings(classId, color);
        
        // Подождать и возобновить стабилизацию
        StartCoroutine(ResumeStabilization(wasEnabled));
    }
    
    IEnumerator ResumeStabilization(bool wasEnabled)
    {
        yield return new WaitForSeconds(1f);
        enableStabilization = wasEnabled;
        
        if (enableStabilization && stabilizationCoroutine == null)
        {
            StartStabilization();
        }
    }
    
    /// <summary>
    /// Включить многоцветный режим стабильно
    /// </summary>
    public void EnableMultiColorStable()
    {
        Debug.Log("[MaskStabilizer] 🌈 Включаем многоцветный режим стабильно");
        
        if (segmentationManager != null)
        {
            segmentationManager.ShowAllClassesColored();
            lastAppliedClass = -1;
            lastAppliedColor = Color.white;
        }
    }
    
    /// <summary>
    /// Ручная стабилизация
    /// </summary>
    [ContextMenu("Stabilize Now")]
    public void StabilizeNow()
    {
        ApplyForceSettings();
    }
    
    /// <summary>
    /// Тест красных стен
    /// </summary>
    [ContextMenu("Test Red Walls")]
    public void TestRedWalls()
    {
        SetColorStable(0, Color.red);
    }
    
    /// <summary>
    /// Тест синих стен
    /// </summary>
    [ContextMenu("Test Blue Walls")]
    public void TestBlueWalls()
    {
        SetColorStable(0, Color.blue);
    }
    
    void OnDestroy()
    {
        if (stabilizationCoroutine != null)
        {
            StopCoroutine(stabilizationCoroutine);
        }
    }
}
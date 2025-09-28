using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Продвинутый рендерер краски для фотореалистичной визуализации
/// Поддерживает различные типы финиша, текстуры и эффекты освещения
/// </summary>
public class PaintRenderer : MonoBehaviour
{
    [Header("🎨 Paint Properties")]
    [Tooltip("Текущий цвет краски")]
    [SerializeField] private Color paintColor = Color.white;
    
    [Tooltip("Тип финиша краски")]
    [SerializeField] private PaintFinishType paintFinish = PaintFinishType.Matte;
    
    [Tooltip("Интенсивность покрытия")]
    [Range(0f, 1f)]
    [SerializeField] private float coverage = 1.0f;

    [Header("✨ Visual Effects")]
    [Tooltip("Включить симуляцию текстуры краски")]
    [SerializeField] private bool enablePaintTexture = true;
    
    [Tooltip("Включить эффекты освещения")]
    [SerializeField] private bool enableLightingEffects = true;
    
    [Tooltip("Включить симуляцию теней")]
    [SerializeField] private bool enableShadows = true;
    
    [Tooltip("Включить отражения")]
    [SerializeField] private bool enableReflections = false;

    [Header("🔧 Advanced Settings")]
    [Tooltip("Качество рендеринга")]
    [SerializeField] private RenderQuality renderQuality = RenderQuality.High;
    
    [Tooltip("Размер текстуры краски")]
    [SerializeField] private int paintTextureSize = 512;
    
    [Tooltip("Интенсивность нормал-маппинга")]
    [Range(0f, 2f)]
    [SerializeField] private float normalIntensity = 1.0f;

    // Материалы для разных типов финиша
    [Header("📦 Materials")]
    [SerializeField] private Material mattePaintMaterial;
    [SerializeField] private Material satinPaintMaterial;
    [SerializeField] private Material glossPaintMaterial;
    [SerializeField] private Material semiGlossPaintMaterial;

    // Текстуры для реалистичности
    [Header("🖼️ Textures")]
    [SerializeField] private Texture2D paintBaseTexture;
    [SerializeField] private Texture2D paintNormalMap;
    [SerializeField] private Texture2D paintRoughnessMap;
    [SerializeField] private Texture2D paintMetallicMap;

    // Зависимости
    private ARWallPresenter wallPresenter;
    private LightingAnalyzer lightingAnalyzer;
    private Material currentMaterial;
    private MaterialPropertyBlock propertyBlock;

    // Состояние рендеринга
    private bool isInitialized = false;
    private bool isPainting = false;
    private RenderTexture paintRenderTexture;
    private Camera paintCamera;

    // Shader property IDs для оптимизации
    private static readonly int PaintColorId = Shader.PropertyToID("_PaintColor");
    private static readonly int PaintFinishId = Shader.PropertyToID("_PaintFinish");
    private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    private static readonly int NormalIntensityId = Shader.PropertyToID("_NormalIntensity");
    private static readonly int LightingDataId = Shader.PropertyToID("_LightingData");
    private static readonly int PaintTextureId = Shader.PropertyToID("_PaintTexture");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int RoughnessMapId = Shader.PropertyToID("_RoughnessMap");
    private static readonly int MetallicMapId = Shader.PropertyToID("_MetallicMap");

    // События
    public System.Action<Color> OnPaintColorChanged;
    public System.Action<PaintFinishType> OnPaintFinishChanged;
    public System.Action OnPaintingStarted;
    public System.Action OnPaintingStopped;

    public void Initialize(ARWallPresenter presenter, Color initialColor, PaintFinishType initialFinish)
    {
        wallPresenter = presenter;
        paintColor = initialColor;
        paintFinish = initialFinish;

        // Поиск зависимостей
        lightingAnalyzer = FindObjectOfType<LightingAnalyzer>();

        // Инициализация материалов
        InitializeMaterials();

        // Создание property block
        propertyBlock = new MaterialPropertyBlock();

        // Создание render texture для продвинутых эффектов
        CreatePaintRenderTexture();

        // Загрузка текстур по умолчанию
        LoadDefaultTextures();

        isInitialized = true;
        Debug.Log("🎨 PaintRenderer инициализирован");

        // Применяем начальные настройки
        UpdatePaintColor(paintColor);
        UpdatePaintFinish(paintFinish);
    }

    void Update()
    {
        if (!isInitialized) return;

        // Обновляем освещение если включено
        if (enableLightingEffects && lightingAnalyzer != null)
        {
            UpdateLightingEffects();
        }

        // Обновляем материал если нужно
        if (isPainting)
        {
            UpdatePaintRendering();
        }
    }

    /// <summary>
    /// Обновление цвета краски
    /// </summary>
    public void UpdatePaintColor(Color newColor)
    {
        paintColor = newColor;
        
        if (propertyBlock != null)
        {
            propertyBlock.SetColor(PaintColorId, paintColor);
            ApplyMaterialProperties();
        }

        // Обновляем презентер
        if (wallPresenter != null)
        {
            wallPresenter.SetClassColor(0, paintColor); // Класс 0 = стены
        }

        OnPaintColorChanged?.Invoke(paintColor);
        Debug.Log($"🎨 Цвет краски обновлен: {ColorUtility.ToHtmlStringRGB(paintColor)}");
    }

    /// <summary>
    /// Обновление типа финиша
    /// </summary>
    public void UpdatePaintFinish(PaintFinishType newFinish)
    {
        paintFinish = newFinish;
        
        // Выбираем подходящий материал
        SelectMaterialForFinish(paintFinish);
        
        if (propertyBlock != null)
        {
            propertyBlock.SetFloat(PaintFinishId, (float)paintFinish);
            ApplyMaterialProperties();
        }

        OnPaintFinishChanged?.Invoke(paintFinish);
        Debug.Log($"✨ Финиш краски обновлен: {paintFinish}");
    }

    /// <summary>
    /// Начало покраски
    /// </summary>
    public void StartPainting()
    {
        isPainting = true;
        
        // Включаем все эффекты для лучшего качества
        EnableHighQualityEffects();
        
        OnPaintingStarted?.Invoke();
        Debug.Log("🖌️ Покраска начата");
    }

    /// <summary>
    /// Остановка покраски
    /// </summary>
    public void StopPainting()
    {
        isPainting = false;
        
        // Возвращаем оптимальные настройки
        RestoreOptimalSettings();
        
        OnPaintingStopped?.Invoke();
        Debug.Log("⏹️ Покраска остановлена");
    }

    /// <summary>
    /// Установка качества рендеринга
    /// </summary>
    public void SetRenderQuality(RenderQuality quality)
    {
        renderQuality = quality;
        ApplyQualitySettings();
        Debug.Log($"🔧 Качество рендеринга: {quality}");
    }

    /// <summary>
    /// Инициализация материалов
    /// </summary>
    private void InitializeMaterials()
    {
        // Загружаем материалы из Resources если не назначены
        if (mattePaintMaterial == null)
            mattePaintMaterial = Resources.Load<Material>("Materials/MattePaint");
        
        if (satinPaintMaterial == null)
            satinPaintMaterial = Resources.Load<Material>("Materials/SatinPaint");
        
        if (glossPaintMaterial == null)
            glossPaintMaterial = Resources.Load<Material>("Materials/GlossPaint");
        
        if (semiGlossPaintMaterial == null)
            semiGlossPaintMaterial = Resources.Load<Material>("Materials/SemiGlossPaint");

        // Выбираем начальный материал
        SelectMaterialForFinish(paintFinish);
    }

    /// <summary>
    /// Выбор материала для типа финиша
    /// </summary>
    private void SelectMaterialForFinish(PaintFinishType finish)
    {
        Material targetMaterial = null;

        switch (finish)
        {
            case PaintFinishType.Matte:
                targetMaterial = mattePaintMaterial;
                break;
            case PaintFinishType.Satin:
                targetMaterial = satinPaintMaterial;
                break;
            case PaintFinishType.Gloss:
                targetMaterial = glossPaintMaterial;
                break;
            case PaintFinishType.SemiGloss:
                targetMaterial = semiGlossPaintMaterial;
                break;
        }

        if (targetMaterial != null)
        {
            currentMaterial = targetMaterial;
            
            // Обновляем материал в презентере
            if (wallPresenter != null)
            {
                var renderer = wallPresenter.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = currentMaterial;
                }
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Материал для финиша {finish} не найден");
        }
    }

    /// <summary>
    /// Создание render texture для продвинутых эффектов
    /// </summary>
    private void CreatePaintRenderTexture()
    {
        if (paintRenderTexture != null)
        {
            paintRenderTexture.Release();
        }

        paintRenderTexture = new RenderTexture(paintTextureSize, paintTextureSize, 0, RenderTextureFormat.ARGB32)
        {
            name = "PaintRenderTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };

        paintRenderTexture.Create();
    }

    /// <summary>
    /// Загрузка текстур по умолчанию
    /// </summary>
    private void LoadDefaultTextures()
    {
        if (paintBaseTexture == null)
            paintBaseTexture = Resources.Load<Texture2D>("Textures/PaintBase");
        
        if (paintNormalMap == null)
            paintNormalMap = Resources.Load<Texture2D>("Textures/PaintNormal");
        
        if (paintRoughnessMap == null)
            paintRoughnessMap = Resources.Load<Texture2D>("Textures/PaintRoughness");
        
        if (paintMetallicMap == null)
            paintMetallicMap = Resources.Load<Texture2D>("Textures/PaintMetallic");
    }

    /// <summary>
    /// Применение свойств материала
    /// </summary>
    private void ApplyMaterialProperties()
    {
        if (propertyBlock == null || wallPresenter == null) return;

        var renderer = wallPresenter.GetComponent<Renderer>();
        if (renderer == null) return;

        // Основные свойства краски
        propertyBlock.SetColor(PaintColorId, paintColor);
        propertyBlock.SetFloat(PaintFinishId, (float)paintFinish);
        propertyBlock.SetFloat(CoverageId, coverage);
        propertyBlock.SetFloat(NormalIntensityId, normalIntensity);

        // Текстуры
        if (paintBaseTexture != null)
            propertyBlock.SetTexture(PaintTextureId, paintBaseTexture);
        
        if (paintNormalMap != null)
            propertyBlock.SetTexture(NormalMapId, paintNormalMap);
        
        if (paintRoughnessMap != null)
            propertyBlock.SetTexture(RoughnessMapId, paintRoughnessMap);
        
        if (paintMetallicMap != null)
            propertyBlock.SetTexture(MetallicMapId, paintMetallicMap);

        // Применяем к рендереру
        renderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// Обновление эффектов освещения
    /// </summary>
    private void UpdateLightingEffects()
    {
        if (lightingAnalyzer == null || propertyBlock == null) return;

        var lightingData = lightingAnalyzer.GetShaderLightingData();
        
        // Передаем данные освещения в шейдер
        propertyBlock.SetVector(LightingDataId, new Vector4(
            lightingData.ambientIntensity,
            lightingData.shadowStrength,
            lightingData.reflectionIntensity,
            0f
        ));

        ApplyMaterialProperties();
    }

    /// <summary>
    /// Обновление рендеринга краски
    /// </summary>
    private void UpdatePaintRendering()
    {
        // Здесь можно добавить продвинутые эффекты рендеринга
        // Например, процедурную генерацию текстуры краски
        
        if (enablePaintTexture)
        {
            GeneratePaintTexture();
        }
    }

    /// <summary>
    /// Генерация процедурной текстуры краски
    /// </summary>
    private void GeneratePaintTexture()
    {
        // Упрощенная генерация - в реальном проекте можно использовать compute shaders
        if (paintRenderTexture == null) return;

        // Создаем временную текстуру для генерации
        var tempTexture = new Texture2D(paintTextureSize, paintTextureSize, TextureFormat.RGBA32, false);
        var pixels = new Color[paintTextureSize * paintTextureSize];

        // Генерируем шум для текстуры краски
        for (int y = 0; y < paintTextureSize; y++)
        {
            for (int x = 0; x < paintTextureSize; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.1f;
                Color pixelColor = paintColor + new Color(noise, noise, noise, 0f);
                pixels[y * paintTextureSize + x] = pixelColor;
            }
        }

        tempTexture.SetPixels(pixels);
        tempTexture.Apply();

        // Копируем в render texture
        Graphics.Blit(tempTexture, paintRenderTexture);
        
        // Обновляем материал
        if (propertyBlock != null)
        {
            propertyBlock.SetTexture(PaintTextureId, paintRenderTexture);
        }

        DestroyImmediate(tempTexture);
    }

    /// <summary>
    /// Включение высококачественных эффектов
    /// </summary>
    private void EnableHighQualityEffects()
    {
        enablePaintTexture = true;
        enableLightingEffects = true;
        enableShadows = true;
        
        if (renderQuality != RenderQuality.Ultra)
        {
            SetRenderQuality(RenderQuality.High);
        }
    }

    /// <summary>
    /// Восстановление оптимальных настроек
    /// </summary>
    private void RestoreOptimalSettings()
    {
        // Возвращаем настройки в зависимости от производительности устройства
        var targetQuality = SystemInfo.graphicsMemorySize > 2000 ? RenderQuality.High : RenderQuality.Medium;
        SetRenderQuality(targetQuality);
    }

    /// <summary>
    /// Применение настроек качества
    /// </summary>
    private void ApplyQualitySettings()
    {
        switch (renderQuality)
        {
            case RenderQuality.Low:
                paintTextureSize = 256;
                normalIntensity = 0.5f;
                enableReflections = false;
                break;
                
            case RenderQuality.Medium:
                paintTextureSize = 512;
                normalIntensity = 1.0f;
                enableReflections = false;
                break;
                
            case RenderQuality.High:
                paintTextureSize = 1024;
                normalIntensity = 1.5f;
                enableReflections = true;
                break;
                
            case RenderQuality.Ultra:
                paintTextureSize = 2048;
                normalIntensity = 2.0f;
                enableReflections = true;
                break;
        }

        // Пересоздаем render texture с новым размером
        CreatePaintRenderTexture();
    }

    void OnDestroy()
    {
        if (paintRenderTexture != null)
        {
            paintRenderTexture.Release();
        }
    }

    // Публичные свойства
    public bool IsInitialized => isInitialized;
    public bool IsPainting => isPainting;
    public Color CurrentPaintColor => paintColor;
    public PaintFinishType CurrentPaintFinish => paintFinish;
    public RenderQuality CurrentRenderQuality => renderQuality;
}

/// <summary>
/// Качество рендеринга
/// </summary>
public enum RenderQuality
{
    Low,
    Medium,
    High,
    Ultra
}
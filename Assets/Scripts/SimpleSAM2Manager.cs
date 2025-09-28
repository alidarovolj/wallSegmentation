using UnityEngine;

/// <summary>
/// Простой SAM2 Manager для обработки SAM моделей
/// Временное решение до полной реализации SAM2 архитектуры
/// </summary>
public class SimpleSAM2Manager : MonoBehaviour
{
    [Header("🤖 Simple SAM2 Manager")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private ARWallPresenter arWallPresenter;
    
    void Start()
    {
        // Найти ARWallPresenter для передачи результатов
        arWallPresenter = FindObjectOfType<ARWallPresenter>();
        
        if (enableDebugLogs)
        {
            Debug.Log("🤖 SimpleSAM2Manager инициализирован");
            if (arWallPresenter != null)
            {
                Debug.Log("✅ ARWallPresenter найден для передачи результатов");
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает кадр от AsyncSegmentationManager
    /// </summary>
    public void ProcessFrame(Texture2D inputTexture)
    {
        if (inputTexture == null)
        {
            if (enableDebugLogs) Debug.LogWarning("⚠️ SimpleSAM2Manager: входная текстура null");
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"🤖 SimpleSAM2Manager: получен кадр {inputTexture.width}x{inputTexture.height}");
        }
        
        // ВРЕМЕННОЕ РЕШЕНИЕ: Создаем простую маску стен
        // В будущем здесь будет полная SAM2 обработка
        CreateSimpleWallMask(inputTexture);
    }
    
    /// <summary>
    /// Создает простую маску стен как временное решение
    /// </summary>
    private void CreateSimpleWallMask(Texture2D inputTexture)
    {
        try
        {
            // Создаем простую маску: центральная область = стены (класс 0)
            int width = inputTexture.width;
            int height = inputTexture.height;
            
            RenderTexture maskTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            maskTexture.enableRandomWrite = true;
            maskTexture.Create();
            
            // Простой шейдер для создания маски стен
            Material simpleMaskMaterial = CreateSimpleMaskMaterial();
            if (simpleMaskMaterial != null)
            {
                Graphics.Blit(inputTexture, maskTexture, simpleMaskMaterial);
                
                // Передаем маску в ARWallPresenter
                if (arWallPresenter != null)
                {
                    arWallPresenter.SetSegmentationMask(maskTexture);
                    arWallPresenter.SetCropParameters(0f, 0f, 1f); // Без crop
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log("🎨 Простая маска стен передана в ARWallPresenter");
                    }
                }
            }
            
            // Очистка
            if (simpleMaskMaterial != null)
            {
                DestroyImmediate(simpleMaskMaterial);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ SimpleSAM2Manager ошибка создания маски: {e.Message}");
        }
    }
    
    /// <summary>
    /// Создает простой материал для генерации маски стен
    /// </summary>
    private Material CreateSimpleMaskMaterial()
    {
        // Простой шейдер, который создает маску стен в центральной области
        string shaderCode = @"
        Shader ""Custom/SimpleWallMask""
        {
            Properties
            {
                _MainTex (""Texture"", 2D) = ""white"" {}
            }
            SubShader
            {
                Tags { ""RenderType""=""Opaque"" }
                Pass
                {
                    CGPROGRAM
                    #pragma vertex vert
                    #pragma fragment frag
                    #include ""UnityCG.cginc""
                    
                    struct appdata
                    {
                        float4 vertex : POSITION;
                        float2 uv : TEXCOORD0;
                    };
                    
                    struct v2f
                    {
                        float2 uv : TEXCOORD0;
                        float4 vertex : SV_POSITION;
                    };
                    
                    sampler2D _MainTex;
                    
                    v2f vert (appdata v)
                    {
                        v2f o;
                        o.vertex = UnityObjectToClipPos(v.vertex);
                        o.uv = v.uv;
                        return o;
                    }
                    
                    fixed4 frag (v2f i) : SV_Target
                    {
                        // Простая логика: центральная область = стены (0), края = фон (2)
                        float2 center = float2(0.5, 0.5);
                        float dist = distance(i.uv, center);
                        
                        // Если близко к центру - стена (класс 0), иначе фон (класс 2)
                        float wallClass = dist < 0.4 ? 0.0 : 2.0;
                        
                        return fixed4(wallClass / 255.0, 0, 0, 1);
                    }
                    ENDCG
                }
            }
        }";
        
        try
        {
            Shader simpleShader = Shader.Find("Custom/SimpleWallMask");
            if (simpleShader == null)
            {
                // Если шейдер не найден, используем стандартный Unlit
                simpleShader = Shader.Find("Unlit/Texture");
            }
            
            if (simpleShader != null)
            {
                return new Material(simpleShader);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Не удалось создать простой шейдер маски: {e.Message}");
        }
        
        return null;
    }
    
    [ContextMenu("🧪 Test Simple Mask")]
    public void TestSimpleMask()
    {
        Debug.Log("🧪 Тестируем простую маску SAM2...");
        
        // Создаем тестовую текстуру
        Texture2D testTexture = new Texture2D(512, 512, TextureFormat.RGB24, false);
        Color[] pixels = new Color[512 * 512];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.gray;
        }
        testTexture.SetPixels(pixels);
        testTexture.Apply();
        
        ProcessFrame(testTexture);
        
        DestroyImmediate(testTexture);
        Debug.Log("✅ Тест простой маски завершен");
    }
}
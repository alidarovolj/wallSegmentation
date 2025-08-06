using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Простой скрипт для тестирования разных шейдеров для эффекта покраски стен
/// </summary>
public class ShaderTester : MonoBehaviour
{
    [Header("Материалы для тестирования")]
    [SerializeField] private Material simpleTestMaterial;
    [SerializeField] private Material photorealisticMaterial;
    [SerializeField] private Material originalMaterial;
    
    [Header("Ссылки")]
    [SerializeField] private ARWallPresenter arWallPresenter;
    [SerializeField] private Button testButton;
    
    private int currentShaderIndex = 0;
    private Material[] materials;
    private string[] shaderNames = { "Original", "Simple Test", "Photorealistic" };
    
    void Start()
    {
        materials = new Material[] { originalMaterial, simpleTestMaterial, photorealisticMaterial };
        
        if (testButton != null)
        {
            testButton.onClick.AddListener(SwitchShader);
            UpdateButtonText();
        }
        
        if (arWallPresenter == null)
        {
            arWallPresenter = FindObjectOfType<ARWallPresenter>();
        }
        
        Debug.Log("🧪 ShaderTester готов! Нажмите кнопку для переключения шейдеров.");
    }
    
    public void SwitchShader()
    {
        currentShaderIndex = (currentShaderIndex + 1) % materials.Length;
        
        if (arWallPresenter != null && materials[currentShaderIndex] != null)
        {
            // Получаем рендерер и меняем материал
            var renderer = arWallPresenter.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = materials[currentShaderIndex];
                Debug.Log($"🎨 Переключено на шейдер: {shaderNames[currentShaderIndex]}");
                
                // Если это простой тест, устанавливаем красный цвет
                if (currentShaderIndex == 1) // Simple Test
                {
                    renderer.material.SetColor("_TestColor", Color.red);
                    Debug.Log("🔴 Установлен красный цвет для простого теста");
                }
                // Если это фотореалистичный, устанавливаем синий цвет
                else if (currentShaderIndex == 2) // Photorealistic
                {
                    renderer.material.SetColor("_PaintColor", Color.blue);
                    renderer.material.SetFloat("_EdgeSoftness", 0.1f);
                    renderer.material.SetFloat("_GlobalBrightness", 1.0f);
                    renderer.material.SetColor("_RealWorldLightColor", Color.white);
                    Debug.Log("🔵 Установлен синий цвет для фотореалистичного шейдера");
                }
            }
        }
        else
        {
            Debug.LogWarning("⚠️ ARWallPresenter или материал не найден!");
        }
        
        UpdateButtonText();
    }
    
    private void UpdateButtonText()
    {
        if (testButton != null)
        {
            var text = testButton.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = $"Shader: {shaderNames[currentShaderIndex]}";
            }
        }
    }
    
    void Update()
    {
        // Переключение по клавише T для удобства тестирования
        if (Input.GetKeyDown(KeyCode.T))
        {
            SwitchShader();
        }
    }
}
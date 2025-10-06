using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using Unity.Collections;
using Unity.Sentis;

[RequireComponent(typeof(SamEncoderRunner))]
[RequireComponent(typeof(SegformerRunner))]
[RequireComponent(typeof(PromptGenerator))]
[RequireComponent(typeof(SamDecoderRunner))]
public class HybridSegmentationController : MonoBehaviour
{
    [Header("Ссылки на компоненты сцены")]
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private ARWallPresenter arWallPresenter;

    // Раннеры для каждого этапа пайплайна
    private SamEncoderRunner samEncoder;
    private SegformerRunner segformer;
    private PromptGenerator promptGenerator;
    private SamDecoderRunner samDecoder;

    private Texture2D cameraTexture;
    
    void Start()
    {
        samEncoder = GetComponent<SamEncoderRunner>();
        segformer = GetComponent<SegformerRunner>();
        promptGenerator = GetComponent<PromptGenerator>();
        samDecoder = GetComponent<SamDecoderRunner>();

        if (arCameraManager == null) arCameraManager = FindObjectOfType<ARCameraManager>();
        if (arWallPresenter == null) arWallPresenter = FindObjectOfType<ARWallPresenter>();
    }

    void OnEnable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }
    }

    void OnDisable()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }
    }

    private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (!samEncoder.IsInitialized || !segformer.IsInitialized || !samDecoder.IsInitialized)
        {
            return; // Модели еще не готовы
        }

        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            return;
        }

        // Запускаем асинхронную обработку кадра
        StartCoroutine(ProcessFrameAsync(cpuImage));
        
        cpuImage.Dispose();
    }

    private IEnumerator ProcessFrameAsync(XRCpuImage cpuImage)
    {
        // 1. Конвертация XRCpuImage в Texture2D
        yield return ConvertCpuImageToTexture(cpuImage);

        // 2. Запуск SAM Encoder
        yield return samEncoder.ProcessImageAsync(cameraTexture);
        var embeddings = samEncoder.GetImageEmbeddings();

        // 3. Запуск SegFormer для получения черновой маски
        yield return segformer.ProcessImageAsync(cameraTexture, coarseMask => 
        {
            if (coarseMask == null) return;
            
            // 4. Генерация подсказок из маски
            // TODO: Указать правильный ID класса для стен
            var prompt = promptGenerator.GeneratePointsFromMask(coarseMask, targetClassId: 1);
            
            // 5. Запуск SAM Decoder
            var imageSize = new Vector2Int(cameraTexture.width, cameraTexture.height);
            StartCoroutine(samDecoder.RunDecoderAsync(embeddings, prompt, imageSize, finalMask =>
            {
                if (finalMask == null) return;
                
                // 6. Визуализация финальной маски
                RenderTexture maskTexture = ConvertMaskToTensorTexture(finalMask);
                arWallPresenter.SetSegmentationMask(maskTexture);

                Debug.Log("🎉 Пайплайн завершен! Финальная маска передана в ARWallPresenter.");

                // Очистка
                finalMask.Dispose();
                // RenderTexture.ReleaseTemporary(maskTexture); // Если используется временная текстура
            }));

            coarseMask.Dispose();
        });
    }

    private IEnumerator ConvertCpuImageToTexture(XRCpuImage cpuImage)
    {
        if (cameraTexture == null || cameraTexture.width != cpuImage.width || cameraTexture.height != cpuImage.height)
        {
            cameraTexture = new Texture2D(cpuImage.width, cpuImage.height, cpuImage.format.AsTextureFormat(), false);
        }

        var conversionParams = new XRCpuImage.ConversionParams(cpuImage, cpuImage.format.AsTextureFormat());
        var rawTextureData = cameraTexture.GetRawTextureData<byte>();
        
        cpuImage.Convert(conversionParams, rawTextureData);
        cameraTexture.Apply();
        
        yield return null;
    }

    private RenderTexture ConvertMaskToTensorTexture(Tensor<float> maskTensor)
    {
        var shape = maskTensor.shape;
        int height = shape[2];
        int width = shape[3];

        var maskTexture2D = TextureConverter.ToTexture(maskTensor);
        var maskRenderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat);
        
        Graphics.Blit(maskTexture2D, maskRenderTexture);
        
        // Очищаем временную Texture2D
        Object.Destroy(maskTexture2D);

        return maskRenderTexture;
    }
}

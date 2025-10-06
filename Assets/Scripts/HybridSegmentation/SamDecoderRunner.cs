using UnityEngine;
using Unity.Sentis;
using System.Collections;
using System.Collections.Generic;

public class SamDecoderRunner : MonoBehaviour
{
    [Header("ONNX Model")]
    [SerializeField] private ModelAsset samDecoderModelAsset;

    [Header("Configuration")]
    [SerializeField] private BackendType backendType = BackendType.GPUCompute;

    private Model decoderModel;
    private Worker decoderWorker;
    
    public bool IsInitialized { get; private set; } = false;

    void Start()
    {
        InitializeDecoder();
    }

    void OnDestroy()
    {
        decoderWorker?.Dispose();
    }

    private void InitializeDecoder()
    {
        if (samDecoderModelAsset == null)
        {
            Debug.LogError("[SamDecoderRunner] ❌ SAM Decoder model asset is not assigned!");
            return;
        }

        decoderModel = ModelLoader.Load(samDecoderModelAsset);
        decoderWorker = new Worker(decoderModel, backendType);
        IsInitialized = true;
        Debug.Log("[SamDecoderRunner] 🎉 SAM Decoder initialized successfully!");
    }

    public IEnumerator RunDecoderAsync(
        Tensor<float> imageEmbeddings, 
        PromptGenerator.Prompt prompt, 
        Vector2Int originalImageSize,
        System.Action<Tensor<float>> onComplete)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SamDecoderRunner] ❌ Decoder is not initialized.");
            onComplete?.Invoke(null);
            yield break;
        }

        // Подготовка входов для модели декодера
        var inputs = new Dictionary<string, Tensor>
        {
            { "image_embeddings", imageEmbeddings },
            { "point_coords", prompt.pointCoords },
            { "point_labels", prompt.pointLabels },
            { "mask_input", prompt.maskInput },
            { "has_mask_input", prompt.hasMaskInput },
            { "orig_im_size", new Tensor<float>(new TensorShape(2), new float[] { originalImageSize.y, originalImageSize.x }) }
        };

        // Запуск модели
        foreach (var item in inputs)
        {
            decoderWorker.SetInput(item.Key, item.Value);
        }
        decoderWorker.Schedule();
        yield return new WaitForEndOfFrame();

        // Получение результата
        Tensor<float> outputMask = decoderWorker.PeekOutput("masks") as Tensor<float>;
        Tensor<float> iouScores = decoderWorker.PeekOutput("iou_scores") as Tensor<float>;

        Debug.Log($"[SamDecoderRunner] 🎭 Decoder finished. Mask shape: {outputMask.shape}, IoU scores shape: {iouScores.shape}");

        // TODO: Реализовать логику выбора лучшей маски на основе iou_scores
        
        onComplete?.Invoke(outputMask);
        
        // Очистка тензоров, созданных в этом методе
        inputs["orig_im_size"].Dispose();
    }
}

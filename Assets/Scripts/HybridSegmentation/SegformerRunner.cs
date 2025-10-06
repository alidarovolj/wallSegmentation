using UnityEngine;
using Unity.Sentis;
using System.Collections;

public class SegformerRunner : MonoBehaviour
{
    [Header("ONNX Model")]
    [SerializeField] private ModelAsset segformerModelAsset;

    [Header("Configuration")]
    [SerializeField] private BackendType backendType = BackendType.GPUCompute;
    
    // Компоненты для работы с моделью
    private Model segformerModel;
    private Worker segformerWorker;
    private AsyncSegmentationManager asyncManager; // Для доступа к Compute Shader

    public bool IsInitialized { get; private set; } = false;

    void Start()
    {
        asyncManager = FindObjectOfType<AsyncSegmentationManager>();
        if (asyncManager == null)
        {
            Debug.LogError("[SegformerRunner] ❌ AsyncSegmentationManager не найден в сцене!");
            return;
        }
        InitializeModel();
    }

    void OnDestroy()
    {
        segformerWorker?.Dispose();
    }

    private void InitializeModel()
    {
        if (segformerModelAsset == null)
        {
            Debug.LogError("[SegformerRunner] ❌ SegFormer model asset is not assigned!");
            return;
        }

        segformerModel = ModelLoader.Load(segformerModelAsset);
        segformerWorker = new Worker(segformerModel, backendType);
        IsInitialized = true;
        Debug.Log("[SegformerRunner] 🎉 SegFormer initialized successfully!");
    }

    public IEnumerator ProcessImageAsync(Texture sourceTexture, System.Action<Tensor<int>> onComplete)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SegformerRunner] ❌ SegFormer is not initialized.");
            onComplete?.Invoke(null);
            yield break;
        }

        var inputShape = segformerModel.inputs[0].shape.ToTensorShape();
        int inputWidth = inputShape[3];
        int inputHeight = inputShape[2];

        // 1. Предобработка и конвертация в тензор
        using var inputTensor = TextureConverter.ToTensor(sourceTexture, inputWidth, inputHeight, 3);
        
        // TODO: Добавить нормализацию, если требуется

        // 2. Запуск модели
        string inputName = segformerModel.inputs[0].name;
        segformerWorker.SetInput(inputName, inputTensor);
        segformerWorker.Schedule();
        yield return new WaitForEndOfFrame();

        // 3. Получение результата (логиты классов)
        using var outputTensor = segformerWorker.PeekOutput() as Tensor<float>;
        
        // 4. ArgMax для получения маски классов
        // Этот шаг может быть выполнен на GPU с помощью Compute Shader для производительности
        Tensor<int> finalMask = PostProcessArgMax(outputTensor);

        onComplete?.Invoke(finalMask);
    }
    
    private Tensor<int> PostProcessArgMax(Tensor<float> logits)
    {
        var shape = logits.shape;
        int height = shape[2];
        int width = shape[3];
        int channels = shape[1];

        var argmaxOutput = new Tensor<int>(new TensorShape(1, height, width, 1));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float maxLogit = float.MinValue;
                int maxIndex = -1;
                for (int c = 0; c < channels; c++)
                {
                    float currentLogit = logits[0, c, y, x];
                    if (currentLogit > maxLogit)
                    {
                        maxLogit = currentLogit;
                        maxIndex = c;
                    }
                }
                argmaxOutput[0, y, x, 0] = maxIndex;
            }
        }
        
        return argmaxOutput;
    }
}

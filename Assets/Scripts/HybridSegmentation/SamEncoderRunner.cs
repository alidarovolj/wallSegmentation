using UnityEngine;
using Unity.Sentis;
using System.Collections;

public class SamEncoderRunner : MonoBehaviour
{
    [Header("ONNX Model")]
    [SerializeField] private ModelAsset samEncoderModelAsset;

    [Header("Configuration")]
    [SerializeField] private BackendType backendType = BackendType.GPUCompute;
    [SerializeField] private Vector2Int inputResolution = new Vector2Int(1024, 1024);

    private Model encoderModel;
    private Worker encoderWorker;
    private Tensor<float> imageEmbeddings;
    private int lastFrameProcessed = -1;

    public bool IsInitialized { get; private set; } = false;

    void Start()
    {
        InitializeEncoder();
    }

    void OnDestroy()
    {
        encoderWorker?.Dispose();
        imageEmbeddings?.Dispose();
    }

    private void InitializeEncoder()
    {
        if (samEncoderModelAsset == null)
        {
            Debug.LogError("[SamEncoderRunner] ❌ SAM Encoder model asset is not assigned!");
            return;
        }

        encoderModel = ModelLoader.Load(samEncoderModelAsset);
        encoderWorker = new Worker(encoderModel, backendType);
        IsInitialized = true;
        Debug.Log("[SamEncoderRunner] 🎉 SAM Encoder initialized successfully!");
    }

    public IEnumerator ProcessImageAsync(Texture sourceTexture)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[SamEncoderRunner] ❌ Encoder is not initialized.");
            yield break;
        }

        // Кэширование: не обрабатываем, если кадр уже обработан
        if (Time.frameCount == lastFrameProcessed && imageEmbeddings != null)
        {
            yield break;
        }

        // 1. Подготовка входного тензора
        using var inputTensor = TextureConverter.ToTensor(sourceTexture, inputResolution.x, inputResolution.y, 3);
        
        // TODO: Добавить нормализацию тензора, если это необходимо для модели.
        // NormalizeTensor(inputTensor);

        // 2. Запуск модели
        string inputName = encoderModel.inputs[0].name;
        encoderWorker.SetInput(inputName, inputTensor);
        encoderWorker.Schedule();
        yield return new WaitForEndOfFrame(); // Ожидаем завершения асинхронной операции

        // 3. Получение и кэширование результата
        imageEmbeddings?.Dispose(); // Очищаем предыдущие эмбеддинги
        imageEmbeddings = encoderWorker.PeekOutput("image_embeddings") as Tensor<float>;

        lastFrameProcessed = Time.frameCount;

        Debug.Log($"[SamEncoderRunner] 🧠 Image processed. Embeddings shape: {imageEmbeddings.shape}");
    }

    public Tensor<float> GetImageEmbeddings()
    {
        return imageEmbeddings;
    }
}

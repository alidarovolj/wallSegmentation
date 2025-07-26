using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class AsyncSegmentationManager : MonoBehaviour
{
      [Header("AR Components")]
      [SerializeField] private ARCameraManager arCameraManager;
      [SerializeField] private RawImage segmentationDisplay;

      [Header("Sentis Model")]
      [SerializeField] private ModelAsset modelAsset;
      private Model runtimeModel;
      private Worker worker;

      [Header("Processing Shaders")]
      [SerializeField] private ComputeShader preprocessingShader;
      [SerializeField] private ComputeShader argmaxShader; // Для GPU-обработки
      [SerializeField] private ComputeShader temporalSmoothingShader;

      [Header("Performance Settings")]
      [SerializeField] private Vector2Int overrideResolution = new Vector2Int(960, 720);
      [SerializeField, Range(0.01f, 1.0f)] private float smoothingFactor = 0.2f;
      [SerializeField] private bool useCpuArgmax = true; // Переключаемся на CPU для лучшей производительности
      [SerializeField, Range(1, 10)] public int frameSkipRate = 2; // Обрабатывать каждый N-й кадр
      [SerializeField, Range(0.016f, 0.1f)] public float minFrameInterval = 0.033f; // ~30 FPS максимум

      // Internal textures and buffers
      private Texture2D cameraTexture;
      private Texture2D cpuSegmentationTexture; // For CPU path
      private RenderTexture preprocessedTexture;
      private RenderTexture neuralNetworkOutputTexture; // Raw output from neural network
      private RenderTexture segmentationTexture;
      private RenderTexture smoothedSegmentationTexture;
      private RenderTexture previousFrameTexture;

      // GPU processing
      private ComputeBuffer tensorBuffer;
      private int argmaxKernel;

      private CancellationTokenSource cancellationTokenSource;
      private bool isProcessingFrame = false;
      private int frameCounter = 0;
      private float lastProcessTime = 0f;
      private float lastUpdateTime = 0f; // Добавляем для измерения времени между кадрами

      private int preprocessKernel;
      private int temporalSmoothingKernel;

      private XRCpuImage.ConversionParams conversionParamsCache;

      // Публичные свойства для управления производительностью
      public bool UseCpuArgmax
      {
            get => useCpuArgmax;
            set => useCpuArgmax = value;
      }

      public Vector2Int CurrentResolution => overrideResolution;

      void OnEnable()
      {
            cancellationTokenSource = new CancellationTokenSource();
            lastUpdateTime = Time.realtimeSinceStartup;
            InitializeAsync(cancellationTokenSource.Token);
      }

      void OnDisable()
      {
            Debug.Log("AsyncSegmentationManager is being disabled.");
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            worker?.Dispose();
            tensorBuffer?.Dispose();

            Destroy(cameraTexture);
            Destroy(cpuSegmentationTexture);
            ReleaseRenderTexture(preprocessedTexture);
            ReleaseRenderTexture(neuralNetworkOutputTexture);
            ReleaseRenderTexture(segmentationTexture);
            ReleaseRenderTexture(smoothedSegmentationTexture);
            ReleaseRenderTexture(previousFrameTexture);
      }

      void Update()
      {
            // Оптимизированная проверка с пропуском кадров
            if (ARSession.state == ARSessionState.SessionTracking &&
                arCameraManager != null &&
                !isProcessingFrame &&
                CanProcessNextFrame())
            {
                if (arCameraManager.TryAcquireLatestCpuImage(out var image))
                {
                    var timeSinceLastFrame = (Time.realtimeSinceStartup - lastUpdateTime) * 1000f;
                    Debug.Log($"⏱️ Frame interval: {timeSinceLastFrame:F1}ms");
                    lastUpdateTime = Time.realtimeSinceStartup;
                    
                    isProcessingFrame = true;
                    // Передаем владение XRCpuImage в асинхронный метод
                    ProcessImageAsync(image, cancellationTokenSource.Token);
                }
            }
      }

      private bool CanProcessNextFrame()
      {
            frameCounter++;

            // Пропуск кадров по частоте
            if (frameCounter % frameSkipRate != 0)
                  return false;

            // Ограничение по времени
            float currentTime = Time.unscaledTime;
            if (currentTime - lastProcessTime < minFrameInterval)
                  return false;

            lastProcessTime = currentTime;
            return true;
      }

      async void InitializeAsync(CancellationToken cancellationToken)
      {
            try
            {
                  Debug.Log("Init Step 1: Loading model...");
                  runtimeModel = ModelLoader.Load(modelAsset);
                  if (cancellationToken.IsCancellationRequested) return;

                  Debug.Log("Init Step 2: Creating worker...");
                  worker = new Worker(runtimeModel, BackendType.GPUCompute);
                  if (cancellationToken.IsCancellationRequested) return;

                  Debug.Log("Init Step 3: Initializing shaders...");
                  InitializeComputeShaders();
                  if (cancellationToken.IsCancellationRequested) return;

                  Debug.Log("Init Step 4: Initializing textures...");
                  InitializeTextures();
                  if (cancellationToken.IsCancellationRequested) return;

                  Debug.Log("Init Step 5: Yielding to next frame...");
                  await Task.Yield();

                  Debug.Log("Initialization complete.");
            }
            catch (Exception e)
            {
                  Debug.LogError($"CRITICAL FAILURE during InitializeAsync: {e}");
            }
      }

      void InitializeComputeShaders()
      {
            preprocessKernel = preprocessingShader.FindKernel("Preprocess");
            temporalSmoothingKernel = temporalSmoothingShader.FindKernel("TemporalSmoothing");
            argmaxKernel = argmaxShader.FindKernel("Argmax");
      }

      void InitializeTextures()
      {
            cameraTexture = new Texture2D(overrideResolution.x, overrideResolution.y, TextureFormat.RGB24, false);
            cpuSegmentationTexture = new Texture2D(overrideResolution.x, overrideResolution.y, TextureFormat.RFloat, false);
            preprocessedTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.ARGB32);
            neuralNetworkOutputTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.ARGB32);
            segmentationTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.ARGB32);
            smoothedSegmentationTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.RFloat);
            previousFrameTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.RFloat);

            if (segmentationDisplay != null)
            {
                  segmentationDisplay.texture = segmentationTexture;
            }
      }

      async void ProcessImageAsync(XRCpuImage image, CancellationToken token)
      {
            var frameStartTime = Time.realtimeSinceStartup;
            try
            {
                if (token.IsCancellationRequested) return;

                var conversionParams = GetConversionParameters(image);

                Debug.Log("Starting async conversion...");
                var conversionStartTime = Time.realtimeSinceStartup;
                var convertTask = image.ConvertAsync(conversionParams);

                while (!convertTask.status.IsDone())
                {
                    if (token.IsCancellationRequested)
                    {
                        convertTask.Dispose();
                        return;
                    }
                    await Task.Yield();
                }
                var conversionTime = (Time.realtimeSinceStartup - conversionStartTime) * 1000f;
                Debug.Log($"Conversion completed in {conversionTime:F1}ms");

                if (convertTask.status != XRCpuImage.AsyncConversionStatus.Ready)
                {
                    Debug.LogError($"Conversion failed with status: {convertTask.status}");
                    return;
                }

                var textureStartTime = Time.realtimeSinceStartup;
                var data = convertTask.GetData<byte>();

                if (cameraTexture.width != conversionParams.outputDimensions.x || cameraTexture.height != conversionParams.outputDimensions.y)
                {
                    cameraTexture.Reinitialize(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y);
                }
                cameraTexture.LoadRawTextureData(data);
                cameraTexture.Apply();
                convertTask.Dispose();

                Graphics.Blit(cameraTexture, preprocessedTexture);
                var textureTime = (Time.realtimeSinceStartup - textureStartTime) * 1000f;
                Debug.Log($"Texture updated from camera image in {textureTime:F1}ms");

                var inferenceStartTime = Time.realtimeSinceStartup;
                var inputTensor = TextureConverter.ToTensor(preprocessedTexture);

                Debug.Log("Scheduling inference...");
                worker.Schedule(inputTensor);
                await Task.Yield();
                if (token.IsCancellationRequested) return;
                inputTensor.Dispose();

                Tensor outputTensor = null;
                while (outputTensor == null)
                {
                    if (token.IsCancellationRequested) return;
                    outputTensor = worker.PeekOutput();
                    if (outputTensor == null)
                        await Task.Yield();
                }
                var inferenceTime = (Time.realtimeSinceStartup - inferenceStartTime) * 1000f;
                Debug.Log($"Inference complete in {inferenceTime:F1}ms, output tensor received.");

                if (token.IsCancellationRequested)
                {
                    outputTensor.Dispose();
                    return;
                }

                var processingStartTime = Time.realtimeSinceStartup;
                if (!useCpuArgmax && argmaxShader != null)
                {
                    await ProcessSegmentationTensorWithGPU(outputTensor as Tensor<float>, token);
                    var processingTime = (Time.realtimeSinceStartup - processingStartTime) * 1000f;
                    Debug.Log($"GPU processing complete in {processingTime:F1}ms");
                }
                else
                {
                    var readbackTensor = (outputTensor as Tensor<float>).ReadbackAndClone();
                    outputTensor.Dispose();

                    float[] segmentationData = await Task.Run(() => ProcessSegmentationTensorWithCPU(readbackTensor), token);
                    var processingTime = (Time.realtimeSinceStartup - processingStartTime) * 1000f;
                    Debug.Log($"CPU processing complete in {processingTime:F1}ms");

                    if (token.IsCancellationRequested) return;

                    if (segmentationData != null)
                    {
                        UpdateSegmentationTexture(segmentationData, readbackTensor.shape);
                    }
                }

                if (segmentationDisplay != null)
                {
                    segmentationDisplay.texture = segmentationTexture;
                    var totalTime = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
                    Debug.Log($"🎯 TOTAL FRAME TIME: {totalTime:F1}ms (Conv: {conversionTime:F1}ms, Tex: {textureTime:F1}ms, Inf: {inferenceTime:F1}ms, Proc: {(Time.realtimeSinceStartup - processingStartTime) * 1000f:F1}ms)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in ProcessImageAsync: {e}");
            }
            finally
            {
                image.Dispose(); // Гарантированное освобождение ресурса
                isProcessingFrame = false;
            }
      }

      XRCpuImage.ConversionParams GetConversionParameters(XRCpuImage image)
      {
            float targetAspectRatio = (float)overrideResolution.x / overrideResolution.y;
            float imageAspectRatio = (float)image.width / image.height;
            int cropWidth = image.width;
            int cropHeight = image.height;
            int cropX = 0;
            int cropY = 0;

            if (imageAspectRatio > targetAspectRatio)
            {
                  cropWidth = (int)(image.height * targetAspectRatio);
                  cropX = (image.width - cropWidth) / 2;
            }
            else
            {
                  cropHeight = (int)(image.width / targetAspectRatio);
                  cropY = (image.height - cropHeight) / 2;
            }

            conversionParamsCache = new XRCpuImage.ConversionParams
            {
                  inputRect = new RectInt(cropX, cropY, cropWidth, cropHeight),
                  // Конвертируем 1-в-1, без масштабирования на этом шаге
                  outputDimensions = new Vector2Int(cropWidth, cropHeight),
                  outputFormat = TextureFormat.RGB24,
                  transformation = XRCpuImage.Transformation.MirrorY
            };
            return conversionParamsCache;
      }

      /// <summary>
      /// Обрабатывает выходной тензор сегментации на CPU для поиска класса с максимальным значением (argmax).
      /// Этот метод предназначен для запуска в фоновом потоке и возвращает массив данных для текстуры.
      /// </summary>
      private float[] ProcessSegmentationTensorWithCPU(Tensor<float> tensor)
      {
            var shape = tensor.shape;
            int h = shape[2];
            int w = shape[3];
            int numClasses = shape[1];
            
            Debug.Log($"CPU processing tensor: {w}x{h}x{numClasses} classes");
            
            // Получаем данные как массив для совместимости с Parallel.For
            float[] data = tensor.AsReadOnlySpan().ToArray();
            float[] result = new float[w * h];
            
            // Параллельная обработка пикселей для улучшения производительности
            System.Threading.Tasks.Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int pixelIndex = y * w + x;
                    float maxValue = float.MinValue;
                    int maxClass = 0;
                    
                    // Optimized argmax: найти класс с максимальным значением
                    for (int c = 0; c < numClasses; c++)
                    {
                        // Индекс в тензоре: [batch=0, class=c, height=y, width=x]
                        int tensorIndex = c * h * w + pixelIndex;
                        float value = data[tensorIndex];
                        
                        if (value > maxValue)
                        {
                            maxValue = value;
                            maxClass = c;
                        }
                    }
                    
                    // Нормализуем класс в диапазон [0, 1] для текстуры
                    result[pixelIndex] = (float)maxClass / (numClasses - 1);
                }
            });
            
            return result;
      }

      private async Task ProcessSegmentationTensorWithGPU(Tensor<float> outputTensor, CancellationToken token)
      {
            if (token.IsCancellationRequested)
            {
                outputTensor.Dispose();
                return;
            }

            var shape = outputTensor.shape;
            int h = shape[2];
            int w = shape[3];
            int numClasses = shape[1];

            Debug.Log($"Tensor shape: {w}x{h}x{numClasses}, Processing with optimized argmax shader");

            // Создаем буфер для всех данных тензора
            int totalElements = w * h * numClasses;
            if (tensorBuffer == null || tensorBuffer.count != totalElements)
            {
                tensorBuffer?.Dispose();
                tensorBuffer = new ComputeBuffer(totalElements, sizeof(float));
            }

            // Эффективно копируем данные из тензора
            var readbackTensor = outputTensor.ReadbackAndClone();
            var tensorData = readbackTensor.AsReadOnlySpan().ToArray();
            tensorBuffer.SetData(tensorData);
            readbackTensor.Dispose();

            // Настраиваем compute shader для обработки всех классов
            argmaxShader.SetBuffer(argmaxKernel, "_InputTensor", tensorBuffer);
            argmaxShader.SetTexture(argmaxKernel, "_OutputTexture", segmentationTexture);
            argmaxShader.SetInt("_TensorWidth", w);
            argmaxShader.SetInt("_TensorHeight", h);
            argmaxShader.SetInt("_NumClasses", numClasses);

            // Вычисляем оптимальные группы потоков для блоков 16x16
            int groupsX = (w + 15) / 16;
            int groupsY = (h + 15) / 16;

            Debug.Log($"Dispatching optimized argmax shader with groups: {groupsX}x{groupsY}, classes: {numClasses}");

            // Запускаем shader
            argmaxShader.Dispatch(argmaxKernel, groupsX, groupsY, 1);

            // Ждем завершения на GPU
            await Task.Yield();

            outputTensor.Dispose();
      }

      private void UpdateSegmentationTexture(float[] segmentationData, TensorShape shape)
      {
            int h = shape[2];
            int w = shape[3];

            // Переиспользуем текстуру для данных CPU, избегая создания новой каждый кадр
            if (cpuSegmentationTexture == null || cpuSegmentationTexture.width != w || cpuSegmentationTexture.height != h)
            {
                if(cpuSegmentationTexture != null) Destroy(cpuSegmentationTexture);
                cpuSegmentationTexture = new Texture2D(w, h, TextureFormat.RFloat, false);
            }

            cpuSegmentationTexture.SetPixelData(segmentationData, 0);
            cpuSegmentationTexture.Apply();

            Graphics.Blit(cpuSegmentationTexture, segmentationTexture);
      }

      void ApplyTemporalSmoothing()
      {
            temporalSmoothingShader.SetTexture(temporalSmoothingKernel, "_CurrentFrame", segmentationTexture);
            temporalSmoothingShader.SetTexture(temporalSmoothingKernel, "_PreviousFrame", previousFrameTexture);
            temporalSmoothingShader.SetTexture(temporalSmoothingKernel, "_SmoothedFrame", smoothedSegmentationTexture);
            temporalSmoothingShader.SetFloat("_SmoothingFactor", smoothingFactor);
            temporalSmoothingShader.Dispatch(temporalSmoothingKernel, overrideResolution.x / 8, overrideResolution.y / 8, 1);

            Graphics.CopyTexture(smoothedSegmentationTexture, previousFrameTexture);
      }

      RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format)
      {
            var rt = new RenderTexture(width, height, 0, format)
            {
                  enableRandomWrite = true
            };
            rt.Create();
            return rt;
      }

      void ReleaseRenderTexture(RenderTexture rt)
      {
            if (rt != null)
            {
                  rt.Release();
                  Destroy(rt);
            }
      }
}
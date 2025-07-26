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
      [SerializeField] private bool useCpuArgmax = false; // Переключился на GPU по умолчанию
      [SerializeField, Range(1, 10)] public int frameSkipRate = 2; // Обрабатывать каждый N-й кадр
      [SerializeField, Range(0.016f, 0.1f)] public float minFrameInterval = 0.033f; // ~30 FPS максимум

      // Internal textures and buffers
      private Texture2D cameraTexture;
      private RenderTexture preprocessedTexture;
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
            ReleaseRenderTexture(preprocessedTexture);
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
                  OnCameraFrameReceived(default);
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
            preprocessedTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.ARGB32);
            segmentationTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.RFloat);
            smoothedSegmentationTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.RFloat);
            previousFrameTexture = CreateRenderTexture(overrideResolution.x, overrideResolution.y, RenderTextureFormat.RFloat);

            if (segmentationDisplay != null)
            {
                  segmentationDisplay.texture = smoothedSegmentationTexture;
            }
      }

      void OnCameraFrameReceived(ARCameraFrameEventArgs args)
      {
            if (isProcessingFrame || !enabled) return;
            if (!arCameraManager.TryAcquireLatestCpuImage(out var image)) return;

            isProcessingFrame = true;
            ProcessImageAsync(image);
            image.Dispose();
      }

      async void ProcessImageAsync(XRCpuImage image)
      {
            try
            {
                  var conversionParams = GetConversionParameters(image);

                  // Оптимизированное ожидание конверсии
                  Debug.Log("Starting async conversion...");
                  var convertTask = image.ConvertAsync(conversionParams);

                  // Ждем завершения конверсии более эффективно
                  while (!convertTask.status.IsDone())
                  {
                        await Task.Delay(1); // Более эффективно чем Task.Yield для ожидания GPU операций
                  }
                  Debug.Log("Conversion completed.");

                  var data = convertTask.GetData<byte>();

                  // Перед загрузкой данных убедимся, что размер текстуры совпадает
                  if (cameraTexture.width != conversionParams.outputDimensions.x || cameraTexture.height != conversionParams.outputDimensions.y)
                  {
                        cameraTexture.Reinitialize(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y);
                  }
                  cameraTexture.LoadRawTextureData(data);
                  cameraTexture.Apply();
                  convertTask.Dispose();

                  Debug.Log("Texture updated from camera image.");

                  Graphics.Blit(cameraTexture, preprocessedTexture);

                  var inputTensor = TextureConverter.ToTensor(preprocessedTexture);

                  Debug.Log("Scheduling inference...");
                  worker.Schedule(inputTensor);
                  await Task.Yield();
                  inputTensor.Dispose();

                  // Оптимизированное ожидание результата
                  Tensor outputTensor = null;
                  while (outputTensor == null)
                  {
                        outputTensor = worker.PeekOutput();
                        if (outputTensor == null)
                              await Task.Yield();
                  }
                  Debug.Log("Inference complete, output tensor received.");

                  // Используем GPU для argmax если доступно
                  if (!useCpuArgmax && argmaxShader != null)
                  {
                        await ProcessSegmentationTensorWithGPU(outputTensor as Tensor<float>);
                        Debug.Log("GPU processing complete.");
                  }
                  else
                  {
                        // Fallback to CPU processing
                        var readbackTensor = (outputTensor as Tensor<float>).ReadbackAndClone();
                        outputTensor.Dispose();

                        float[] segmentationData = await Task.Run(() => ProcessSegmentationTensorWithCPU(readbackTensor));
                        Debug.Log("CPU processing complete.");

                        if (segmentationData != null)
                        {
                              UpdateSegmentationTexture(segmentationData, readbackTensor.shape);
                        }
                  }

                  // ApplyTemporalSmoothing();

                  // Прямое присваивание для теста, без сглаживания
                  if (segmentationDisplay != null)
                  {
                        segmentationDisplay.texture = segmentationTexture;
                        Debug.Log("Updated segmentation display texture!");
                  }
            }
            catch (Exception e)
            {
                  Debug.LogError($"Error in ProcessImageAsync: {e}");
            }
            finally
            {
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
      private float[] ProcessSegmentationTensorWithCPU(Tensor<float> readbackTensor)
      {
            try
            {
                  var shape = readbackTensor.shape;
                  int h = shape[2];
                  int w = shape[3];
                  int numClasses = shape[1];

                  // Тензор уже считан из GPU, поэтому мы можем обрабатывать его напрямую.

                  float[] segmentationData = new float[w * h];

                  for (int y = 0; y < h; y++)
                  {
                        for (int x = 0; x < w; x++)
                        {
                              float maxVal = -float.MaxValue;
                              int maxIndex = 0;
                              for (int c = 0; c < numClasses; c++)
                              {
                                    float val = readbackTensor[0, c, y, x];
                                    if (val > maxVal)
                                    {
                                          maxVal = val;
                                          maxIndex = c;
                                    }
                              }
                              segmentationData[y * w + x] = (float)maxIndex / (numClasses - 1);
                        }
                  }
                  return segmentationData;
            }
            catch (Exception e)
            {
                  Debug.LogError($"Error processing segmentation tensor on CPU: {e}");
                  return null;
            }
            finally
            {
                  // Гарантированно удаляем тензор с данными CPU после обработки
                  readbackTensor.Dispose();
            }
      }

      private async Task ProcessSegmentationTensorWithGPU(Tensor<float> outputTensor)
      {
            var shape = outputTensor.shape;
            int h = shape[2];
            int w = shape[3];
            int numClasses = shape[1];
            int totalElements = w * h * numClasses;

            // Создаем или пересоздаем буфер если размер изменился
            if (tensorBuffer == null || tensorBuffer.count != totalElements)
            {
                  tensorBuffer?.Dispose();
                  tensorBuffer = new ComputeBuffer(totalElements, sizeof(float));
            }

            // Копируем данные из тензора в compute buffer
            var readbackTensor = outputTensor.ReadbackAndClone();
            var tensorData = readbackTensor.AsReadOnlySpan().ToArray();
            tensorBuffer.SetData(tensorData);
            readbackTensor.Dispose();

            // Настраиваем compute shader
            argmaxShader.SetBuffer(argmaxKernel, "_InputTensor", tensorBuffer);
            argmaxShader.SetTexture(argmaxKernel, "_OutputTexture", segmentationTexture);
            argmaxShader.SetInt("_TensorWidth", w);
            argmaxShader.SetInt("_TensorHeight", h);
            argmaxShader.SetInt("_NumClasses", numClasses);

            // Выполняем compute shader
            int groupsX = Mathf.CeilToInt(w / 8.0f);
            int groupsY = Mathf.CeilToInt(h / 8.0f);
            argmaxShader.Dispatch(argmaxKernel, groupsX, groupsY, 1);

            // Ждем завершения GPU операции
            await Task.Yield();

            outputTensor.Dispose();
      }

      private void UpdateSegmentationTexture(float[] segmentationData, TensorShape shape)
      {
            int h = shape[2];
            int w = shape[3];

            // Создаем временную Texture2D для данных и копируем в RenderTexture
            var tempTexture = new Texture2D(w, h, TextureFormat.RFloat, false);
            tempTexture.SetPixelData(segmentationData, 0);
            tempTexture.Apply();

            Graphics.Blit(tempTexture, segmentationTexture);
            Destroy(tempTexture);
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
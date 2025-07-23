using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Sentis;
using TMPro;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading;
using System.Linq;

public static class PaintingEvents
{
      public static Action<int> OnPaintClassRequested;
}

[RequireComponent(typeof(ARCameraManager))]
public class AsyncSegmentationManager : MonoBehaviour
{
      [Header("UI Components")]
      [SerializeField] private ARCameraManager arCameraManager;
      [SerializeField] private RawImage segmentationDisplay;

      [Header("ML Configuration")]
      [SerializeField] private ModelAsset modelAsset;
      [SerializeField] private string modelAddress;
      [SerializeField] private ComputeShader preprocessorShader;
      [SerializeField] private ComputeShader postProcessShader;
      [SerializeField] private ComputeShader temporalSmoothingShader;

      [Header("Performance Settings")]
      // Устанавливаем разрешение, которое ожидает модель
      [SerializeField] private Vector2Int overrideResolution = new Vector2Int(960, 720);
      [SerializeField, Range(0.01f, 1.0f)] private float smoothingFactor = 0.2f;
      [SerializeField] private int maxConcurrentInferences = 2;
      [SerializeField] private float inferenceInterval = 0.1f;

      private Worker worker;
      private Model model;

      private RenderTexture preprocessedTexture;
      private RenderTexture segmentationTexture;
      private RenderTexture smoothedSegmentationTexture;
      private RenderTexture previousFrameTexture;

      private int activeInferences = 0;
      private float lastInferenceTime = 0f;

      // Оптимизация: пропуск кадров для CPU обработки
      private int frameCounter = 0;
      private int frameSkipInterval = 3; // Обрабатываем каждый 3-й кадр

      private int preprocessKernel;
      private int postProcessKernel;
      private int temporalSmoothingKernel;

      private CancellationTokenSource cancellationTokenSource;

      void Awake()
      {
            if (arCameraManager == null) arCameraManager = GetComponent<ARCameraManager>();
      }

      void OnEnable()
      {
            cancellationTokenSource = new CancellationTokenSource();
            InitializeAsync(cancellationTokenSource.Token);

            if (arCameraManager != null)
            {
                  arCameraManager.frameReceived += OnCameraFrameReceived;
            }
      }

      void OnDisable()
      {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            if (arCameraManager != null)
            {
                  arCameraManager.frameReceived -= OnCameraFrameReceived;
            }

            worker?.Dispose();

            preprocessedTexture?.Release();
            if (preprocessedTexture != null) Destroy(preprocessedTexture);

            segmentationTexture?.Release();
            if (segmentationTexture != null) Destroy(segmentationTexture);

            smoothedSegmentationTexture?.Release();
            if (smoothedSegmentationTexture != null) Destroy(smoothedSegmentationTexture);

            previousFrameTexture?.Release();
            if (previousFrameTexture != null) Destroy(previousFrameTexture);
      }

      async void InitializeAsync(CancellationToken cancellationToken)
      {
            try
            {
                  await InitializeSentisAsync(cancellationToken);
                  if (cancellationToken.IsCancellationRequested) return;

                  InitializeComputeShaders();
                  InitializeTextures();
            }
            catch (Exception e)
            {
                  Debug.LogError($"Initialization error: {e.Message}");
            }
      }

      async Task InitializeSentisAsync(CancellationToken cancellationToken)
      {
            if (modelAsset == null && string.IsNullOrEmpty(modelAddress))
            {
                  throw new InvalidOperationException("ModelAsset or ModelAddress must be assigned.");
            }

            ModelAsset loadedModelAsset = modelAsset;
            if (!string.IsNullOrEmpty(modelAddress))
            {
                  AsyncOperationHandle<ModelAsset> handle = Addressables.LoadAssetAsync<ModelAsset>(modelAddress);
                  await handle.Task;
                  if (handle.Status == AsyncOperationStatus.Succeeded)
                  {
                        loadedModelAsset = handle.Result;
                  }
                  else
                  {
                        if (modelAsset == null) throw new InvalidOperationException("Addressable load failed and no local fallback model asset.");
                  }
            }

            if (cancellationToken.IsCancellationRequested) return;

            model = ModelLoader.Load(loadedModelAsset);

            // Create worker using the correct Sentis 2.0.0 API
            worker = new Worker(model, BackendType.GPUCompute);
      }

      void InitializeComputeShaders()
      {
            if (preprocessorShader != null) preprocessKernel = preprocessorShader.FindKernel("Preprocess");
            if (postProcessShader != null) postProcessKernel = postProcessShader.FindKernel("CSMain");
            if (temporalSmoothingShader != null) temporalSmoothingKernel = temporalSmoothingShader.FindKernel("CSMain");
      }

      void InitializeTextures()
      {
            preprocessedTexture = new RenderTexture(overrideResolution.x, overrideResolution.y, 0, RenderTextureFormat.ARGBFloat) { enableRandomWrite = true };
            preprocessedTexture.Create();
            segmentationTexture = new RenderTexture(overrideResolution.x, overrideResolution.y, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true, filterMode = FilterMode.Point };
            segmentationTexture.Create();
            smoothedSegmentationTexture = new RenderTexture(overrideResolution.x, overrideResolution.y, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true, filterMode = FilterMode.Point };
            smoothedSegmentationTexture.Create();
            previousFrameTexture = new RenderTexture(overrideResolution.x, overrideResolution.y, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true, filterMode = FilterMode.Point };
            previousFrameTexture.Create();

            if (segmentationDisplay != null)
            {
                  segmentationDisplay.texture = smoothedSegmentationTexture;
                  segmentationDisplay.color = Color.white;
            }
      }

      void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
      {
            if (worker == null || activeInferences >= maxConcurrentInferences || Time.time - lastInferenceTime < inferenceInterval) return;
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) return;

            lastInferenceTime = Time.time;
            ProcessFrameAsync(image, cancellationTokenSource.Token);
      }

      async void ProcessFrameAsync(XRCpuImage image, CancellationToken cancellationToken)
      {
            activeInferences++;
            Texture2D cameraTexture = null;
            Tensor inputTensor = null;

            try
            {
                  cameraTexture = await ConvertImageAsync(image, cancellationToken);
                  if (cancellationToken.IsCancellationRequested || cameraTexture == null) return;

                  PreprocessImageGPU(cameraTexture);

                  // Указываем правильные размеры и каналы для модели
                  inputTensor = TextureConverter.ToTensor(preprocessedTexture, overrideResolution.x, overrideResolution.y, 3);

                  // Диагностика входного тензора
                  Debug.Log($"Input tensor shape: {inputTensor.shape}, type: {inputTensor.GetType()}");

                  await RunInferenceAsync(inputTensor, cancellationToken);
                  if (cancellationToken.IsCancellationRequested) return;

                  await PostprocessAsync(cancellationToken);
                  if (cancellationToken.IsCancellationRequested) return;

                  await ApplyTemporalSmoothingAsync(cancellationToken);
                  if (cancellationToken.IsCancellationRequested) return;

                  UpdateGlobalShaderProperties();
            }
            catch (Exception e)
            {
                  if (!cancellationToken.IsCancellationRequested)
                  {
                        Debug.LogError($"Error in processing frame: {e.Message}\n{e.StackTrace}");
                  }
            }
            finally
            {
                  activeInferences--;
                  image.Dispose();
                  if (cameraTexture != null) Destroy(cameraTexture);
                  inputTensor?.Dispose();
            }
      }

      async Task<Texture2D> ConvertImageAsync(XRCpuImage image, CancellationToken cancellationToken)
      {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                  inputRect = new RectInt(0, 0, image.width, image.height),
                  outputDimensions = overrideResolution,
                  outputFormat = TextureFormat.RGB24,
                  transformation = XRCpuImage.Transformation.MirrorY
            };

            var request = image.ConvertAsync(conversionParams);

            while (!request.status.IsDone())
            {
                  if (cancellationToken.IsCancellationRequested)
                  {
                        request.Dispose();
                        return null;
                  }
                  await Task.Yield();
            }

            if (request.status != XRCpuImage.AsyncConversionStatus.Ready)
            {
                  Debug.LogError($"Image conversion failed with status {request.status}");
                  request.Dispose();
                  return null;
            }

            var texture = new Texture2D(overrideResolution.x, overrideResolution.y, TextureFormat.RGB24, false);
            texture.LoadRawTextureData(request.GetData<byte>());
            texture.Apply();

            request.Dispose();
            return texture;
      }

      void PreprocessImageGPU(Texture2D inputTexture)
      {
            if (preprocessorShader == null) return;

            preprocessorShader.SetTexture(preprocessKernel, "InputTexture", inputTexture);
            preprocessorShader.SetTexture(preprocessKernel, "Result", preprocessedTexture);
            preprocessorShader.Dispatch(preprocessKernel, Mathf.CeilToInt(overrideResolution.x / 8.0f), Mathf.CeilToInt(overrideResolution.y / 8.0f), 1);
      }

      async Task RunInferenceAsync(Tensor inputTensor, CancellationToken cancellationToken)
      {
            if (cancellationToken.IsCancellationRequested) return;

            worker.Schedule(inputTensor);

            // Просто ждем следующего кадра, чтобы дать Sentis время начать работу
            await Task.Yield();
      }

      async Task PostprocessAsync(CancellationToken cancellationToken)
      {
            Tensor outputTensor = null;

            // Ждем, пока результат не будет готов
            while (true)
            {
                  outputTensor = worker.PeekOutput();
                  if (outputTensor != null)
                        break;

                  if (cancellationToken.IsCancellationRequested) return;
                  await Task.Yield();
            }

            try
            {
                  if (cancellationToken.IsCancellationRequested) return;

                  var tensorFloat = outputTensor as Tensor<float>;
                  if (tensorFloat != null && segmentationTexture != null)
                  {
                        ProcessSegmentationTensorWithGPUBlit(tensorFloat);
                  }
            }
            finally
            {
                  outputTensor?.Dispose();
            }
      }

      async Task ApplyTemporalSmoothingAsync(CancellationToken cancellationToken)
      {
            if (temporalSmoothingShader == null) return;
            if (cancellationToken.IsCancellationRequested) return;

            temporalSmoothingShader.SetTexture(temporalSmoothingKernel, "CurrentFrameMask", segmentationTexture);
            temporalSmoothingShader.SetTexture(temporalSmoothingKernel, "PreviousFrameMask", previousFrameTexture);
            temporalSmoothingShader.SetTexture(temporalSmoothingKernel, "Result", smoothedSegmentationTexture);
            temporalSmoothingShader.SetFloat("SmoothingFactor", smoothingFactor);
            temporalSmoothingShader.Dispatch(temporalSmoothingKernel, Mathf.CeilToInt(smoothedSegmentationTexture.width / 8.0f), Mathf.CeilToInt(smoothedSegmentationTexture.height / 8.0f), 1);

            Graphics.Blit(smoothedSegmentationTexture, previousFrameTexture);
            await Task.Yield();
      }

      void UpdateGlobalShaderProperties()
      {
            Shader.SetGlobalTexture("_GlobalSegmentationTexture", smoothedSegmentationTexture);
      }

      void ProcessSegmentationTensorWithGPUBlit(Tensor<float> outputTensor)
      {
            Tensor<float> readbackTensor = null;
            try
            {
                  var shape = outputTensor.shape;
                  int height = shape[2];
                  int width = shape[3];
                  int classes = shape[1];

                  readbackTensor = outputTensor.ReadbackAndClone();
                  var tensorData = new float[readbackTensor.count];
                  for (int i = 0; i < readbackTensor.count; i++) { tensorData[i] = readbackTensor[i]; }

                  // Создаем МАЛЕНЬКУЮ текстуру, соответствующую выходу модели
                  var tempTexture = new Texture2D(width, height, TextureFormat.RFloat, false);
                  var pixels = new float[width * height];

                  // Быстрый argmax на маленьких данных
                  for (int y = 0; y < height; y++)
                  {
                        for (int x = 0; x < width; x++)
                        {
                              float maxValue = float.MinValue;
                              int maxClassIndex = 0;
                              for (int c = 0; c < classes; c++)
                              {
                                    int tensorIndex = (c * height * width) + (y * width) + x;
                                    float value = tensorData[tensorIndex];
                                    if (value > maxValue)
                                    {
                                          maxValue = value;
                                          maxClassIndex = c;
                                    }
                              }
                              pixels[y * width + x] = maxClassIndex / (float)(classes - 1);
                        }
                  }

                  tempTexture.SetPixelData(pixels, 0);
                  tempTexture.Apply();

                  // ИСПОЛЬЗУЕМ GPU для сверхбыстрого масштабирования
                  Graphics.Blit(tempTexture, segmentationTexture);

                  if (segmentationDisplay != null)
                  {
                        segmentationDisplay.texture = segmentationTexture;
                  }

                  Destroy(tempTexture);
                  readbackTensor.Dispose();
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"Error in ProcessSegmentationTensorWithGPUBlit: {e.Message}\n{e.StackTrace}");
                  if (readbackTensor != null) { readbackTensor.Dispose(); }
            }
      }

      void Update()
      {
            HandleTapWithGPUReadback();
      }

      void HandleTapWithGPUReadback()
      {
            if (Input.GetMouseButtonDown(0))
            {
                  if (smoothedSegmentationTexture == null) return;
                  AsyncGPUReadback.Request(smoothedSegmentationTexture, 0, TextureFormat.RFloat, OnReadbackComplete);
            }
      }

      void OnReadbackComplete(AsyncGPUReadbackRequest request)
      {
            if (request.hasError) return;

            Vector2 screenPos = Input.mousePosition;
            var readX = (int)(screenPos.x * ((float)request.width / Screen.width));
            var readY = (int)(screenPos.y * ((float)request.height / Screen.height));
            var data = request.GetData<float>();
            int index = readY * request.width + readX;

            if (data.Length > index)
            {
                  PaintingEvents.OnPaintClassRequested?.Invoke((int)data[index]);
            }
      }

      public RenderTexture GetSegmentationTexture()
      {
            return smoothedSegmentationTexture;
      }

      void OnDestroy()
      {
            // All cleanup is now in OnDisable
      }
}
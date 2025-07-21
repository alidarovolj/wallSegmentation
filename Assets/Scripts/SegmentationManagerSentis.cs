// Sentis 2.0 для Unity 6.0 - Обновленная версия
// Следует официальному Upgrade Guide от Unity

using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;
using Unity.Sentis;

public class SegmentationManagerSentis : MonoBehaviour
{
      [Header("Model Configuration")]
      [SerializeField] private ModelAsset modelAsset;  // Sentis 2.0 uses ModelAsset
      [SerializeField] private Vector2Int overrideResolution = new Vector2Int(256, 256);

      [Header("UI Components")]
      [SerializeField] private RawImage rawImage;
      [SerializeField] private ARCameraManager arCameraManager;

      [Header("Rendering")]
      [SerializeField] private ComputeShader postProcessShader;
      [SerializeField] private int classIndexToPaint = -1;

      // Sentis 2.0 components
      private Model runtimeModel;
      private Worker worker;

      // Processing components
      private RenderTexture segmentationTexture;
      private ComputeBuffer tensorDataBuffer;
      private ComputeBuffer colorMapBuffer;
      private int postProcessKernel;

      // Model parameters
      private Vector2Int imageSize;
      private Tensor<float> inputTensor;
      private Tensor<float> lastOutputTensor;
      private bool isProcessing = false;

      void Start()
      {
            StartCoroutine(InitializeSentis());
      }

      private IEnumerator InitializeSentis()
      {
            // Load the model (Sentis 2.0 API)
            if (modelAsset == null)
            {
                  Debug.LogError("Model asset is not assigned!");
                  yield break;
            }

            runtimeModel = ModelLoader.Load(modelAsset);
            Debug.Log($"Sentis model loaded: {runtimeModel}");

            // Print model info (Sentis 2.0 style)
            Debug.Log($"Model inputs: {runtimeModel.inputs.Count}, outputs: {runtimeModel.outputs.Count}");
            if (runtimeModel.inputs.Count > 0)
            {
                  var input = runtimeModel.inputs[0];
                  Debug.Log($"Input: {input.name}, shape: {input.shape}");
            }

            // Create worker using Sentis 2.0 API (new Worker constructor)
            worker = new Worker(runtimeModel, BackendType.GPUCompute);
            Debug.Log("Sentis worker created with GPUCompute backend");

            // Determine input resolution
            DetermineInputResolution();

            // Initialize GPU resources
            InitializeGpuResources();

            // Enable camera processing
            if (arCameraManager != null)
            {
                  arCameraManager.frameReceived += OnCameraFrameReceived;
                  Debug.Log("Camera frame processing enabled");
            }

            Debug.Log("Sentis initialization completed!");
      }

      private void DetermineInputResolution()
      {
            // Try to get resolution from model input shape
            if (runtimeModel.inputs.Count > 0)
            {
                  var inputShape = runtimeModel.inputs[0].shape;
                  Debug.Log($"Model input shape: {inputShape}");

                  // For typical segmentation models: [batch, height, width, channels] or [batch, channels, height, width]
                  if (inputShape.rank >= 3)
                  {
                        // Convert to TensorShape to access dimensions
                        var shape = inputShape.ToTensorShape();
                        int height = shape[1];
                        int width = shape[2];

                        // Check if it's NCHW format (channels-first)
                        if (height <= 4 && width > height)
                        {
                              height = shape[2];
                              width = shape[3];
                        }

                        if (height > 0 && width > 0 && height == width)
                        {
                              imageSize = new Vector2Int(width, height);
                              Debug.Log($"Using model input resolution: {imageSize}");
                              return;
                        }
                  }
            }

            // Fallback to model name patterns or overrides
            if (modelAsset.name.ToLower().Contains("deeplabv3"))
            {
                  imageSize = new Vector2Int(513, 513);
                  Debug.Log("Detected DeepLabV3, using 513x513 resolution");
            }
            else if (modelAsset.name.ToLower().Contains("512"))
            {
                  imageSize = new Vector2Int(512, 512);
                  Debug.Log("Detected 512x512 from model name");
            }
            else
            {
                  imageSize = overrideResolution;
                  Debug.Log($"Using override resolution: {imageSize}");
            }
      }

      private void InitializeGpuResources()
      {
            // Create segmentation texture
            segmentationTexture = new RenderTexture(imageSize.x, imageSize.y, 0, RenderTextureFormat.ARGB32);
            segmentationTexture.enableRandomWrite = true;
            segmentationTexture.Create();

            // Assign to UI
            if (rawImage != null)
            {
                  rawImage.texture = segmentationTexture;
                  Debug.Log("RawImage texture assigned");
            }

            // Setup compute shader
            if (postProcessShader != null)
            {
                  postProcessKernel = postProcessShader.FindKernel("CSMain");

                  // Setup color map
                  var colors = ColorMap.GetAllColors();
                  colorMapBuffer = new ComputeBuffer(colors.Length, sizeof(float) * 4);
                  colorMapBuffer.SetData(colors);
                  postProcessShader.SetBuffer(postProcessKernel, "ColorMap", colorMapBuffer);

                  postProcessShader.SetInt("numClasses", colors.Length);
                  postProcessShader.SetTexture(postProcessKernel, "OutputTexture", segmentationTexture);

                  Debug.Log("GPU resources initialized for post-processing");
            }
      }

      private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
      {
            if (isProcessing || worker == null) return;

            var cameraTexture = arCameraManager.GetComponent<Camera>().activeTexture;
            if (cameraTexture == null) return;

            Debug.Log($"Processing camera image: {cameraTexture.width}x{cameraTexture.height}");
            StartCoroutine(ProcessImage(cameraTexture));
      }

      private IEnumerator ProcessImage(Texture cameraTexture)
      {
            isProcessing = true;

            try
            {
                  // Create input tensor from camera texture (Sentis 2.0 style)
                  inputTensor?.Dispose();
                  inputTensor = TextureConverter.ToTensor(cameraTexture, imageSize.x, imageSize.y, 3);
                  Debug.Log($"Input tensor created: {inputTensor.shape}");

                  // Schedule inference (Sentis 2.0 API - worker.Schedule instead of Execute)
                  worker.Schedule(inputTensor);

                  // Get output tensor (Sentis 2.0 API)
                  lastOutputTensor?.Dispose();
                  lastOutputTensor = worker.PeekOutput() as Tensor<float>;
                  Debug.Log($"Output tensor received: {lastOutputTensor.shape}");

                  // Download tensor data to CPU (Sentis 2.0 API)
                  var tensorData = lastOutputTensor.DownloadToArray();
                  Debug.Log($"Tensor data downloaded, length: {tensorData.Length}");

                  // Setup compute shader with output data
                  if (tensorDataBuffer == null || tensorDataBuffer.count != tensorData.Length)
                  {
                        tensorDataBuffer?.Dispose();
                        tensorDataBuffer = new ComputeBuffer(tensorData.Length, sizeof(float));
                        postProcessShader.SetBuffer(postProcessKernel, "TensorData", tensorDataBuffer);
                  }
                  tensorDataBuffer.SetData(tensorData);

                  // Calculate tensor dimensions
                  var outputShape = lastOutputTensor.shape;
                  int tensorHeight = outputShape[1];
                  int tensorWidth = outputShape[2];

                  postProcessShader.SetInt("tensorWidth", tensorWidth);
                  postProcessShader.SetInt("tensorHeight", tensorHeight);
                  postProcessShader.SetInt("classIndexToPaint", classIndexToPaint);

                  // Dispatch compute shader
                  int threadGroupsX = Mathf.CeilToInt(segmentationTexture.width / 8.0f);
                  int threadGroupsY = Mathf.CeilToInt(segmentationTexture.height / 8.0f);

                  Debug.Log($"Dispatching compute shader: {threadGroupsX}x{threadGroupsY} thread groups");
                  postProcessShader.Dispatch(postProcessKernel, threadGroupsX, threadGroupsY, 1);

                  Debug.Log("Segmentation processing completed!");
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"Error during segmentation processing: {e.Message}");
            }
            finally
            {
                  isProcessing = false;
            }

            yield return null;
      }

      void OnDestroy()
      {
            // Cleanup
            if (arCameraManager != null)
            {
                  arCameraManager.frameReceived -= OnCameraFrameReceived;
            }

            worker?.Dispose();
            inputTensor?.Dispose();
            lastOutputTensor?.Dispose();
            tensorDataBuffer?.Dispose();
            colorMapBuffer?.Dispose();

            if (segmentationTexture != null)
            {
                  segmentationTexture.Release();
            }

            Debug.Log("Sentis SegmentationManager cleaned up");
      }

      // Public methods for UI control
      public void SetClassToPaint(int classIndex)
      {
            classIndexToPaint = classIndex;
            Debug.Log($"Class to paint set to: {classIndex}");
      }

      public void ShowAllClasses()
      {
            classIndexToPaint = -1;
            Debug.Log("Showing all classes");
      }
}
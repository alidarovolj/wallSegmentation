// Основной SegmentationManager с Sentis 2.0
// Переименован из SegmentationManagerSentis для удобства
// Использует современный Sentis API для Unity 6.0

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SegmentationManager : MonoBehaviour
{
      [Header("Model Configuration")]
      [SerializeField] private ModelAsset modelAsset;
      [SerializeField] private BackendType workerType = BackendType.GPUCompute;
      [SerializeField] private Vector2Int overrideResolution = new Vector2Int(512, 512);

      [Header("UI Components")]
      [SerializeField] private ARCameraManager arCameraManager;
      [SerializeField] private RawImage segmentationDisplay; // Changed from rawImage
      [SerializeField] private TextMeshProUGUI classNameText;

      [Header("Painting")]
      [SerializeField] private PaintManager paintManager; // Reference to the new PaintManager

      [Header("Performance")]
      [Tooltip("How long the class name stays on screen in seconds.")]
      [SerializeField] private float displayNameDuration = 1.5f;

      [Header("Rendering")]
      [SerializeField] private ComputeShader postProcessShader;
      [SerializeField] private int classIndexToPaint = -1; // Default to all classes
      [SerializeField] private int classIndexToPaint2 = -1; // Second class to paint
      [SerializeField, Range(0.1f, 10f)] private float edgeHardness = 2.5f; // Controls edge smoothness
      [SerializeField] private Color paintColor = Color.blue;
      [SerializeField] private bool mirrorX = false;
      [SerializeField] private bool mirrorY = true;

      [Header("Preprocessing")]
      [SerializeField] private ComputeShader preprocessorShader;
      private RenderTexture preprocessedTexture;
      private int preprocessKernel;

      // Model-related resources
      private Model runtimeModel;
      private Worker worker;

      // GPU resources
      private RenderTexture segmentationTexture;
      private ComputeBuffer tensorDataBuffer;
      private ComputeBuffer colorMapBuffer;
      private int postProcessKernel;

      // Processing state
      private Vector2Int imageSize;
      private float[] lastTensorData; // Cached tensor data for tap handling
      private TensorShape lastTensorShape; // Cached shape for tap handling
      private int numClasses = 21;
      private bool isProcessing = false;
      private Coroutine displayNameCoroutine;

      // Class names and colors
      private readonly List<Color> colorMap = new List<Color>();

      void Start()
      {
            // Initialize color map
            var colors = ColorMap.GetAllColors();
            colorMap.AddRange(colors);

            StartCoroutine(InitializeSentis());
      }

      void OnEnable()
      {
            if (arCameraManager != null)
            {
                  arCameraManager.frameReceived += OnCameraFrameReceived;
                  Debug.Log("📷 AR Camera frame processing enabled");
            }
      }

      void OnDisable()
      {
            if (arCameraManager != null)
            {
                  arCameraManager.frameReceived -= OnCameraFrameReceived;
                  Debug.Log("📷 AR Camera frame processing disabled");
            }
      }

      private IEnumerator InitializeSentis()
      {
            Debug.Log("🚀 Initializing Sentis ML mode...");
            runtimeModel = ModelLoader.Load(modelAsset);

            DetermineInputResolution();

            // Initialize GPU resources with the correct image size
            InitializeGpuResources();

            worker = new Worker(runtimeModel, workerType);
            Debug.Log($"✅ Sentis worker created with {workerType} backend");

            Debug.Log("🎉 Sentis initialization completed!");
            yield return null;
            DiagnoseCameraBackground();
      }

      private void DetermineInputResolution()
      {
            if (overrideResolution.x > 0 && overrideResolution.y > 0)
            {
                  imageSize = overrideResolution;
                  Debug.Log($"✅ Using override resolution: {imageSize}");
            }
            else
            {
                  // Fallback for safety, though override should always be set.
                  imageSize = new Vector2Int(512, 512);
                  Debug.LogWarning("⚠️ Override resolution not set. Using fallback 512x512.");
            }
      }

      private void InitializeGpuResources()
      {
            // Create a render texture for the segmentation mask
            segmentationTexture = new RenderTexture(imageSize.x, imageSize.y, 0, RenderTextureFormat.ARGB32);
            segmentationTexture.enableRandomWrite = true;
            segmentationTexture.Create();

            // Create a render texture for preprocessing
            preprocessedTexture = new RenderTexture(imageSize.x, imageSize.y, 0, RenderTextureFormat.ARGBFloat);
            preprocessedTexture.enableRandomWrite = true;
            preprocessedTexture.Create();

            // Initialize the preprocessor shader
            if (preprocessorShader != null)
            {
                  preprocessKernel = preprocessorShader.FindKernel("Preprocess");
                  preprocessorShader.SetTexture(preprocessKernel, "OutputTexture", preprocessedTexture);
            }

            // Get the kernel index for the post-processing shader.
            postProcessKernel = postProcessShader.FindKernel("CSMain");
            postProcessShader.SetTexture(postProcessKernel, "OutputTexture", segmentationTexture);

            // Setup color map for visualization
            if (colorMap.Count > 0)
            {
                  colorMapBuffer = new ComputeBuffer(colorMap.Count, sizeof(float) * 4);
                  colorMapBuffer.SetData(colorMap);
                  postProcessShader.SetBuffer(postProcessKernel, "ColorMap", colorMapBuffer);
                  postProcessShader.SetInt("numClasses", colorMap.Count);
            }

            // Assign to UI
            if (segmentationDisplay != null)
            {
                  segmentationDisplay.texture = segmentationTexture;
                  Debug.Log("✅ RawImage texture assigned");
            }

            Debug.Log("✅ GPU resources initialized for ML processing");
      }

      private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
      {
            if (isProcessing || worker == null) return;

            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                  return;
            }

            StartCoroutine(ProcessFrame(image));
      }

      private IEnumerator ProcessFrame(XRCpuImage image)
      {
            isProcessing = true;
            Debug.Log("🔄 Starting camera image processing...");

            var conversionParams = new XRCpuImage.ConversionParams
            {
                  inputRect = new RectInt(0, 0, image.width, image.height),
                  outputDimensions = new Vector2Int(imageSize.x, imageSize.y),
                  outputFormat = TextureFormat.RGBA32,
                  transformation = XRCpuImage.Transformation.MirrorY // Apply vertical flip consistently
            };

            var texture = new Texture2D(imageSize.x, imageSize.y, conversionParams.outputFormat, false);
            image.Convert(conversionParams, texture.GetRawTextureData<byte>());
            texture.Apply();
            image.Dispose();

            if (preprocessorShader != null)
            {
                  preprocessorShader.SetTexture(preprocessKernel, "InputTexture", texture);
                  int threadGroupsX = Mathf.CeilToInt(imageSize.x / 8.0f);
                  int threadGroupsY = Mathf.CeilToInt(imageSize.y / 8.0f);
                  preprocessorShader.Dispatch(preprocessKernel, threadGroupsX, threadGroupsY, 1);
            }

            using (var inputTensor = TextureConverter.ToTensor(preprocessorShader != null ? (Texture)preprocessedTexture : texture))
            {
                  worker.Schedule(inputTensor);
            }
            Destroy(texture);

            yield return new WaitForEndOfFrame();

            var outputTensor = worker.PeekOutput() as Tensor<float>;
            if (outputTensor == null)
            {
                  Debug.LogError("❌ Failed to peek output tensor.");
                  isProcessing = false;
                  yield break;
            }

            var tensorData = outputTensor.DownloadToArray();
            var shape = outputTensor.shape;

            // Cache tensor data for tap handling
            lastTensorData = tensorData;
            lastTensorShape = shape;

            outputTensor.Dispose();

            if (tensorData == null || tensorData.Length == 0)
            {
                  Debug.LogError("❌ Tensor data is empty or null after download.");
                  isProcessing = false;
                  yield break;
            }

            if (tensorDataBuffer == null || tensorDataBuffer.count != tensorData.Length)
            {
                  tensorDataBuffer?.Dispose();
                  tensorDataBuffer = new ComputeBuffer(tensorData.Length, sizeof(float));
                  postProcessShader.SetBuffer(postProcessKernel, "TensorData", tensorDataBuffer);
            }
            tensorDataBuffer.SetData(tensorData);

            postProcessShader.SetInt("tensorWidth", shape[3]);
            postProcessShader.SetInt("tensorHeight", shape[2]);
            this.numClasses = shape[1];
            postProcessShader.SetInt("numClasses", this.numClasses);
            postProcessShader.SetInt("classIndexToPaint", classIndexToPaint);
            postProcessShader.SetInt("classIndexToPaint2", classIndexToPaint2);
            postProcessShader.SetFloat("edgeHardness", edgeHardness);

            int threadGroupsX_post = Mathf.CeilToInt(segmentationTexture.width / 8.0f);
            int threadGroupsY_post = Mathf.CeilToInt(segmentationTexture.height / 8.0f);
            postProcessShader.Dispatch(postProcessKernel, threadGroupsX_post, threadGroupsY_post, 1);

            // --- NEW: Update PaintManager every frame ---
            if (paintManager != null)
            {
                  paintManager.UpdateSegmentationTexture(segmentationTexture);
            }

            Debug.Log("✅ Segmentation processing completed!");
            isProcessing = false;
      }

      void OnDestroy()
      {
            worker?.Dispose();

            tensorDataBuffer?.Release();
            colorMapBuffer?.Release();

            if (segmentationTexture != null) segmentationTexture.Release();
            if (preprocessedTexture != null) preprocessedTexture.Release();
      }

      // Public methods for UI control
      public void SetClassToPaint(int classIndex)
      {
            classIndexToPaint = classIndex;
            classIndexToPaint2 = -1; // Reset second class
            Debug.Log($"🎨 Class to paint set to: {classIndex}");
      }

      public void ShowAllClasses()
      {
            classIndexToPaint = -1;
            classIndexToPaint2 = -1;
            Debug.Log("🌈 Showing all classes");
      }

      public void ToggleTestMode()
      {
            // This method is no longer relevant as test mode is removed.
            // Keeping it for now, but it will do nothing.
            Debug.Log("🔄 ToggleTestMode called, but test mode is removed.");
      }

      [ContextMenu("Test ML Model")]
      public void TestMLModel()
      {
            // This method is no longer relevant as test mode is removed.
            // Keeping it for now, but it will do nothing.
            Debug.Log("🧪 TestMLModel called, but test mode is removed.");
      }

      [ContextMenu("Visualize Model Output")]
      public void VisualizeModelOutput()
      {
            Debug.Log($"🔍 VisualizeModelOutput Debug:");
            Debug.Log($"   worker != null: {worker != null}");
            Debug.Log($"   runtimeModel != null: {runtimeModel != null}");
            Debug.Log($"   postProcessShader != null: {postProcessShader != null}");
            Debug.Log($"   segmentationTexture != null: {segmentationTexture != null}");
            Debug.Log($"   segmentationDisplay != null: {segmentationDisplay != null}");
            Debug.Log($"   rawImage != null: {arCameraManager != null && arCameraManager.GetComponent<RawImage>() != null}"); // Assuming rawImage is the ARCameraBackground's texture

            if (worker != null)
            {
                  Debug.Log("🎨 Creating simple test data to visualize model output...");
                  StartCoroutine(CreateSimpleTestData());
            }
            else
            {
                  Debug.Log("⚠️ ML model not loaded yet");
                  if (worker == null) Debug.Log("   - worker is null");
            }
      }

      [ContextMenu("Show Only Chairs")]
      public void ShowOnlyChairs()
      {
            classIndexToPaint = 19; // ADE20K index for chair
            classIndexToPaint2 = -1;
            Debug.Log("🎨 Showing only: chair (19)");
      }

      [ContextMenu("Show Only Walls")]
      public void ShowOnlyWalls()
      {
            classIndexToPaint = 0; // Correct ADE20K index for 'wall' is 0 for this model
            classIndexToPaint2 = -1;
            Debug.Log("🎨 Showing only: wall (0)");
      }

      [ContextMenu("Show Walls and Floor")]
      public void ShowWallsAndFloor()
      {
          classIndexToPaint = 0; // wall
          classIndexToPaint2 = 3; // floor
          Debug.Log("🎨 Showing only: wall (0) and floor (3)");
      }

      // =====================================================
      // INTERACTIVE TAP HANDLING
      // =====================================================

      private void Update()
      {
            HandleTap();
      }

      private void HandleTap()
      {
            if (Input.GetMouseButtonDown(0))
            {
                  if (lastTensorData == null || lastTensorShape.rank == 0) return;

                  Vector2 screenPos = Input.mousePosition;

                  // Convert screen position to UV coordinates (0-1 range)
                  // Note: This assumes the segmentationDisplay RawImage covers the full screen.
                  // If not, you'll need RectTransformUtility.ScreenPointToLocalPointInRectangle.
                  float uv_x = screenPos.x / Screen.width;
                  float uv_y = screenPos.y / Screen.height;

                  // Flip coordinates to match our shader logic if necessary
                  // Based on our last fix, both are inverted.
                  uv_x = 1.0f - uv_x;
                  uv_y = 1.0f - uv_y;

                  // Convert UV coordinates to tensor coordinates
                  int tensorX = (int)(uv_x * lastTensorShape[3]);
                  int tensorY = (int)(uv_y * lastTensorShape[2]);

                  // --- Perform Argmax on CPU for the tapped pixel ---
                  int tappedClass = 0;
                  float maxLogit = float.MinValue;
                  int tensorWidth = lastTensorShape[3];
                  int tensorHeight = lastTensorShape[2];

                  for (int c = 0; c < numClasses; c++)
                  {
                        int logitIndex = c * (tensorWidth * tensorHeight) + tensorY * tensorWidth + tensorX;
                        if (logitIndex < lastTensorData.Length)
                        {
                              float currentLogit = lastTensorData[logitIndex];
                              if (currentLogit > maxLogit)
                              {
                                    maxLogit = currentLogit;
                                    tappedClass = c;
                              }
                        }
                  }

                  Debug.Log($"Tapped on class: {tappedClass}. Setting it as the target for painting.");

                  // Set the class index to be painted in the post-process shader
                  classIndexToPaint = tappedClass;
                  postProcessShader.SetInt("classIndexToPaint", classIndexToPaint);
                  postProcessShader.SetInt("classIndexToPaint2", -1); // Reset second class on tap

                  // --- NEW: Communicate with PaintManager ---
                  if (paintManager != null)
                  {
                        // Tell the paint manager which class to paint
                        paintManager.SetTargetClass(tappedClass);

                        // Pass the latest segmentation texture to the paint manager's material
                        paintManager.UpdateSegmentationTexture(segmentationTexture);
                  }
            }
      }

      private IEnumerator ShowClassName(string name)
      {
            if (classNameText != null)
            {
                  classNameText.text = name;
                  classNameText.enabled = true;

                  if (displayNameCoroutine != null)
                  {
                        StopCoroutine(displayNameCoroutine);
                  }
                  displayNameCoroutine = StartCoroutine(HideTextAfterDelay());
            }
            yield break;
      }

      /// <summary>
      /// Hides the class name text after a specified delay.
      /// </summary>
      private IEnumerator HideTextAfterDelay()
      {
            yield return new WaitForSeconds(displayNameDuration);
            if (classNameText != null)
            {
                  classNameText.enabled = false;
            }
            displayNameCoroutine = null;
      }

      private IEnumerator TestMLModelSimple()
      {
            // Create simple gradient test image
            var testTexture = new Texture2D(520, 520, TextureFormat.RGB24, false);
            var pixels = new Color[520 * 520];

            // Simple gradient pattern
            for (int y = 0; y < 520; y++)
            {
                  for (int x = 0; x < 520; x++)
                  {
                        int index = y * 520 + x;
                        float normalizedX = x / 520f;
                        float normalizedY = y / 520f;

                        // Simple color gradient
                        pixels[index] = new Color(normalizedX, normalizedY, 0.5f);
                  }
            }

            testTexture.SetPixels(pixels);
            testTexture.Apply();

            Debug.Log("🎨 Created simple gradient test image (520x520)");
            Debug.Log("🔬 Processing with DeepLabV3+ model...");

            // Process with real ML model
            // This part needs to be adapted to use the new async processing
            // For now, it will just log and not actually process.
            Debug.Log("⚠️ TestMLModelSimple is not fully functional with new async processing.");
            yield return null;

            // Cleanup
            if (testTexture != null)
            {
                  Destroy(testTexture);
            }
      }

      private IEnumerator CreateSimpleTestData()
      {
            // Create even simpler test - just solid colors in blocks
            var testTexture = new Texture2D(520, 520, TextureFormat.RGB24, false);
            var pixels = new Color[520 * 520];

            for (int y = 0; y < 520; y++)
            {
                  for (int x = 0; x < 520; x++)
                  {
                        int index = y * 520 + x;

                        // Create simple blocks of different colors
                        if (x < 260 && y < 260)
                              pixels[index] = Color.red;    // Top-left red
                        else if (x >= 260 && y < 260)
                              pixels[index] = Color.green;  // Top-right green
                        else if (x < 260 && y >= 260)
                              pixels[index] = Color.blue;   // Bottom-left blue
                        else
                              pixels[index] = Color.yellow; // Bottom-right yellow
                  }
            }

            testTexture.SetPixels(pixels);
            testTexture.Apply();

            Debug.Log("🎨 Created simple 4-color block test image");
            Debug.Log("🔬 Sending to model to see what it outputs...");

            // Process and visualize ANY output from model
            // This part needs to be adapted to use the new async processing
            // For now, it will just log and not actually process.
            Debug.Log("⚠️ CreateSimpleTestData is not fully functional with new async processing.");
            yield return null;

            // Cleanup
            if (testTexture != null)
            {
                  Destroy(testTexture);
            }
      }

      /// <summary>
      /// Переключение на BiSeNet модель
      /// </summary>
      [ContextMenu("Switch to BiSeNet Model")]
      private void SwitchToBiSeNetModel()
      {
            Debug.Log("🚀 Switching to BiSeNet model...");

            // Попробуем найти BiSeNet модель в Assets/Models/
            var bisetModelPath = "Assets/Models/bisenet-bisenet-float.onnx";
            var bisetModel = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(bisetModelPath);

            if (bisetModel != null)
            {
                  modelAsset = bisetModel;

                  // Сбросим override resolution для BiSeNet (использует native 720x960)
                  overrideResolution = Vector2Int.zero;

                  Debug.Log("✅ BiSeNet model loaded successfully!");
                  Debug.Log("📏 Override resolution cleared - using native 720x960");
                  Debug.Log("🔄 Restart play mode to apply changes.");
            }
            else
            {
                  Debug.LogError("❌ BiSeNet model not found at: " + bisetModelPath);
                  Debug.LogError("📋 Make sure bisenet-bisenet-float.onnx is in Assets/Models/ folder");
            }
      }

      /// <summary>
      /// Переключение на DeepLabV3+ модель
      /// </summary>
      [ContextMenu("Switch to DeepLabV3+ Model")]
      private void SwitchToDeepLabModel()
      {
            Debug.Log("🎯 Switching to DeepLabV3+ model...");

            // Попробуем найти DeepLabV3+ модель в Assets/Models/
            var deeplabModelPath = "Assets/Models/deeplabv3_plus_mobilenet-deeplabv3-plus-mobilenet-float.onnx";
            var deeplabModel = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(deeplabModelPath);

            if (deeplabModel != null)
            {
                  modelAsset = deeplabModel;

                  // Сбросим override resolution для DeepLabV3+ (использует native 520x520)
                  overrideResolution = Vector2Int.zero;

                  Debug.Log("✅ DeepLabV3+ model loaded successfully!");
                  Debug.Log("📏 Override resolution cleared - using native 520x520");
                  Debug.Log("🔄 Restart play mode to apply changes.");
            }
            else
            {
                  Debug.LogError("❌ DeepLabV3+ model not found at: " + deeplabModelPath);
            }
      }

      /// <summary>
      /// Переключение на SegFormer модель
      /// </summary>
      [ContextMenu("Switch to SegFormer Model")]
      private void SwitchToSegFormerModel()
      {
            Debug.Log("🤖 Switching to SegFormer model...");

            // Попробуем найти SegFormer модель в Assets/Models/
            var segformerModelPath = "Assets/Models/model_fp16.onnx";
            var segformerModel = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(segformerModelPath);

            if (segformerModel != null)
            {
                  modelAsset = segformerModel;

                  // Сбросим override resolution для SegFormer (использует native 512x512)
                  overrideResolution = Vector2Int.zero;

                  Debug.Log("✅ SegFormer model loaded successfully!");
                  Debug.Log("📏 Override resolution cleared - using native 512x512");
                  Debug.Log("🤖 ADE20K dataset - 150 classes including wall, building, sky, person, etc.");
                  Debug.Log("🔄 Restart play mode to apply changes.");
            }
            else
            {
                  Debug.LogError("❌ SegFormer model not found at: " + segformerModelPath);
                  Debug.LogError("📋 Make sure model_fp16.onnx is in Assets/Models/ folder");
            }
      }

      /// <summary>
      /// Сброс Override Resolution для автоматического определения
      /// </summary>
      [ContextMenu("Clear Override Resolution (Use Auto)")]
      private void ClearOverrideResolution()
      {
            overrideResolution = Vector2Int.zero;
            Debug.Log("✅ Override Resolution cleared - will use auto-detection from model");
            Debug.Log("🔄 Restart play mode to apply changes.");
      }

      /// <summary>
      /// Диагностика AR Camera Background и camera settings
      /// </summary>
      [ContextMenu("Diagnose Camera Background")]
      private void DiagnoseCameraBackground()
      {
            Debug.Log("🔍 === AR CAMERA BACKGROUND ДИАГНОСТИКА ===");

            if (arCameraManager == null)
            {
                  Debug.LogError("❌ ARCameraManager не найден!");
                  return;
            }

            var camera = arCameraManager.GetComponent<Camera>();
            if (camera == null)
            {
                  Debug.LogError("❌ Camera компонент не найден!");
                  return;
            }

            Debug.Log($"📷 Camera ClearFlags: {camera.clearFlags}");
            Debug.Log($"🎨 Camera BackgroundColor: {camera.backgroundColor}");

            var arCameraBackground = arCameraManager.GetComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
            if (arCameraBackground == null)
            {
                  Debug.LogError("❌ ARCameraBackground компонент НЕ НАЙДЕН! Это причина черного экрана!");
                  Debug.LogError("🚨 РЕШЕНИЕ: Добавьте ARCameraBackground компонент на AR Camera!");
                  return;
            }

            Debug.Log($"✅ ARCameraBackground найден, enabled: {arCameraBackground.enabled}");
            Debug.Log($"📱 Custom material: {arCameraBackground.customMaterial}");

            // Попробуем принудительно включить ARCameraBackground
            if (!arCameraBackground.enabled)
            {
                  Debug.LogWarning("⚠️ ARCameraBackground отключен! Включаем...");
                  arCameraBackground.enabled = true;
            }

            // Проверим настройки камеры
            if (camera.clearFlags == CameraClearFlags.SolidColor)
            {
                  Debug.LogWarning("⚠️ Camera ClearFlags = SolidColor (черный экран). ARCameraBackground должен это исправить.");
                  // ARCameraBackground должен автоматически изменить clearFlags на Color
            }

            // Проверим AR Session
            var arSession = FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>();
            if (arSession == null)
            {
                  Debug.LogError("❌ ARSession не найден!");
                  return;
            }

            Debug.Log($"🎯 ARSession state: {UnityEngine.XR.ARFoundation.ARSession.state}");
            Debug.Log($"📡 ARSession enabled: {arSession.enabled}");

            // Детальная диагностика состояния AR Session
            var sessionState = UnityEngine.XR.ARFoundation.ARSession.state;
            switch (sessionState)
            {
                  case UnityEngine.XR.ARFoundation.ARSessionState.CheckingAvailability:
                        Debug.LogWarning("⏳ AR проверяет доступность. Подождите...");
                        break;
                  case UnityEngine.XR.ARFoundation.ARSessionState.Installing:
                        Debug.LogWarning("📦 AR устанавливается. Подождите...");
                        break;
                  case UnityEngine.XR.ARFoundation.ARSessionState.Ready:
                        Debug.Log("✅ AR готов к работе!");
                        break;
                  case UnityEngine.XR.ARFoundation.ARSessionState.SessionInitializing:
                        Debug.LogWarning("🔄 AR инициализируется...");
                        break;
                  case UnityEngine.XR.ARFoundation.ARSessionState.SessionTracking:
                        Debug.Log("🎯 AR отслеживает окружение!");
                        break;
                  case UnityEngine.XR.ARFoundation.ARSessionState.Unsupported:
                        Debug.LogError("❌ AR не поддерживается на этом устройстве!");
                        break;
                  case UnityEngine.XR.ARFoundation.ARSessionState.NeedsInstall:
                        Debug.LogError("📱 Требуется установка AR пакетов!");
                        break;
                  default:
                        Debug.LogWarning($"⚠️ Неизвестное состояние AR: {sessionState}");
                        break;
            }

            // Проверим есть ли camera frame data
            if (arCameraManager.TryAcquireLatestCpuImage(out var image))
            {
                  Debug.Log($"✅ Camera frame доступен: {image.width}x{image.height}, format: {image.format}");
                  image.Dispose();
            }
            else
            {
                  Debug.LogWarning("⚠️ Camera frame НЕ ДОСТУПЕН! AR может быть не инициализирован.");
            }
      }

      [ContextMenu("Toggle Horizontal Mirror")]
      private void ToggleHorizontalMirror()
      {
            mirrorX = !mirrorX;
            Debug.Log($"✅ Horizontal mirror (transformation) set to: {mirrorX}. Restart play mode to apply changes.");
      }

      [ContextMenu("Toggle Vertical Mirror")]
      private void ToggleVerticalMirror()
      {
            mirrorY = !mirrorY;
            Debug.Log($"✅ Vertical mirror (transformation) set to: {mirrorY}. Restart play mode to apply changes.");
      }

      [ContextMenu("Test All Mirror Combinations")]
      private void TestAllMirrorCombinations()
      {
            Debug.Log("🔄 Testing all mirror combinations:");
            Debug.Log($"   Current: MirrorX={mirrorX}, MirrorY={mirrorY}");
            Debug.Log($"   Try: MirrorX=true, MirrorY=true (both mirrors)");
            Debug.Log($"   Try: MirrorX=false, MirrorY=true (only vertical)");
            Debug.Log($"   Try: MirrorX=true, MirrorY=false (only horizontal)");
            Debug.Log($"   Try: MirrorX=false, MirrorY=false (no mirrors)");
            Debug.Log("💡 Change values in inspector and restart play mode to test each combination.");
      }

      [ContextMenu("Diagnose Vertical Lines")]
      private void DiagnoseVerticalLines()
      {
            // This method needs to be adapted to read from the latest processed data
            // For now, it will just log a warning.
            Debug.LogWarning("⚠️ DiagnoseVerticalLines is not fully functional with new async processing.");
      }
}

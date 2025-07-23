using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Эффективный захват видеопотока с AR-камеры для передачи в шейдеры покраски
/// Реализует GPU-центричный подход через CommandBuffer без дорогостоящих операций CPU
/// </summary>
[RequireComponent(typeof(ARCameraManager), typeof(ARCameraBackground))]
public class CameraFeedCapture : MonoBehaviour
{
      [Header("Camera Components")]
      public ARCameraManager cameraManager;
      public ARCameraBackground cameraBackground;

      [Header("Output")]
      private RenderTexture cameraFeedTexture;
      private CommandBuffer blitCommandBuffer;

      public RenderTexture CameraFeedTexture => cameraFeedTexture;

      void Awake()
      {
            // Auto-assign if not set
            if (cameraManager == null)
                  cameraManager = GetComponent<ARCameraManager>();
            if (cameraBackground == null)
                  cameraBackground = GetComponent<ARCameraBackground>();
      }

      void Start()
      {
            InitializeCameraFeedTexture();
      }

      void OnEnable()
      {
            if (cameraManager != null)
            {
                  cameraManager.frameReceived += OnCameraFrameReceived;
                  Debug.Log("📸 CameraFeedCapture: GPU-центричный захват активирован");
            }
      }

      void OnDisable()
      {
            if (cameraManager != null)
            {
                  cameraManager.frameReceived -= OnCameraFrameReceived;
            }
      }

      private void InitializeCameraFeedTexture()
      {
            // Создаем постоянную RenderTexture для хранения видеопотока
            cameraFeedTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.Default);
            cameraFeedTexture.name = "AR_CameraFeed";
            cameraFeedTexture.Create();

            Debug.Log($"✅ CameraFeedTexture создана: {Screen.width}x{Screen.height}");
      }

      private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
      {
            if (cameraBackground == null || cameraFeedTexture == null) return;

            // Получаем текстуру из материала ARCameraBackground безопасно
            if (cameraBackground.material == null) return;

            var cameraTexture = cameraBackground.material.HasProperty("_MainTex")
                ? cameraBackground.material.GetTexture("_MainTex")
                : null;
            if (cameraTexture == null) return;

            // Создаем CommandBuffer один раз для эффективности
            if (blitCommandBuffer == null)
            {
                  blitCommandBuffer = new CommandBuffer();
                  blitCommandBuffer.name = "AR Camera Feed Blit";
            }
            else
            {
                  blitCommandBuffer.Clear();
            }

            // Копируем текстуру камеры в нашу постоянную RenderTexture
            blitCommandBuffer.Blit(cameraTexture, cameraFeedTexture);
            Graphics.ExecuteCommandBuffer(blitCommandBuffer);

            // Устанавливаем как глобальную текстуру для всех шейдеров
            Shader.SetGlobalTexture("_GlobalCameraFeedTex", cameraFeedTexture);

            // Также передаем матрицу трансформации для корректного UV-маппинга
            // Note: displayTransform удален в новых версиях AR Foundation
            Shader.SetGlobalMatrix("_UnityDisplayTransform", Matrix4x4.identity);
      }

      void OnDestroy()
      {
            if (cameraFeedTexture != null)
            {
                  cameraFeedTexture.Release();
                  cameraFeedTexture = null;
            }

            if (blitCommandBuffer != null)
            {
                  blitCommandBuffer.Dispose();
                  blitCommandBuffer = null;
            }

            Debug.Log("🧹 CameraFeedCapture: ресурсы освобождены");
      }
}
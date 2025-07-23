using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Менеджер пулов объектов для оптимизации памяти в AR приложении
/// Управляет переиспользованием текстур, буферов и временных объектов
/// </summary>
public class MemoryPoolManager : MonoBehaviour
{
      [Header("Pool Configuration")]
      [SerializeField] private int texturePoolSize = 10;
      [SerializeField] private int bufferPoolSize = 5;
      [SerializeField] private int meshPoolSize = 20;

      [Header("Texture Pool Settings")]
      [SerializeField] private Vector2Int textureResolution = new Vector2Int(512, 512);
      [SerializeField] private RenderTextureFormat textureFormat = RenderTextureFormat.RFloat;

      // Singleton pattern
      public static MemoryPoolManager Instance { get; private set; }

      // Pools
      private readonly Queue<RenderTexture> renderTexturePool = new Queue<RenderTexture>();
      private readonly Queue<Texture2D> texture2DPool = new Queue<Texture2D>();
      private readonly Queue<GraphicsBuffer> bufferPool = new Queue<GraphicsBuffer>();
      private readonly Queue<Mesh> meshPool = new Queue<Mesh>();

      // Active objects tracking
      private readonly HashSet<RenderTexture> activeRenderTextures = new HashSet<RenderTexture>();
      private readonly HashSet<Texture2D> activeTexture2Ds = new HashSet<Texture2D>();
      private readonly HashSet<GraphicsBuffer> activeBuffers = new HashSet<GraphicsBuffer>();
      private readonly HashSet<Mesh> activeMeshes = new HashSet<Mesh>();

      // Memory statistics
      private long totalAllocatedMemory = 0;
      private int totalPooledObjects = 0;

      void Awake()
      {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                  Destroy(gameObject);
                  return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePools();
      }

      void InitializePools()
      {
            Debug.Log("🔧 Инициализация пулов памяти...");

            // Pre-allocate RenderTextures
            for (int i = 0; i < texturePoolSize; i++)
            {
                  var renderTexture = CreateRenderTexture();
                  renderTexturePool.Enqueue(renderTexture);
            }

            // Pre-allocate Texture2Ds
            for (int i = 0; i < texturePoolSize; i++)
            {
                  var texture2D = CreateTexture2D();
                  texture2DPool.Enqueue(texture2D);
            }

            // Pre-allocate GraphicsBuffers
            for (int i = 0; i < bufferPoolSize; i++)
            {
                  var buffer = CreateGraphicsBuffer();
                  bufferPool.Enqueue(buffer);
            }

            // Pre-allocate Meshes
            for (int i = 0; i < meshPoolSize; i++)
            {
                  var mesh = CreateMesh();
                  meshPool.Enqueue(mesh);
            }

            totalPooledObjects = texturePoolSize * 2 + bufferPoolSize + meshPoolSize;
            Debug.Log($"✅ Пулы инициализированы: {totalPooledObjects} объектов");
      }

      RenderTexture CreateRenderTexture()
      {
            var renderTexture = new RenderTexture(textureResolution.x, textureResolution.y, 0, textureFormat)
            {
                  enableRandomWrite = true,
                  filterMode = FilterMode.Point,
                  name = "PooledRenderTexture"
            };
            renderTexture.Create();

            totalAllocatedMemory += GetTextureMemorySize(renderTexture);
            return renderTexture;
      }

      Texture2D CreateTexture2D()
      {
            var texture = new Texture2D(textureResolution.x, textureResolution.y, TextureFormat.RGB24, false)
            {
                  filterMode = FilterMode.Point,
                  name = "PooledTexture2D"
            };

            totalAllocatedMemory += GetTextureMemorySize(texture);
            return texture;
      }

      GraphicsBuffer CreateGraphicsBuffer()
      {
            int bufferSize = textureResolution.x * textureResolution.y * 4; // RGBA float
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, sizeof(float))
            {
                  name = "PooledGraphicsBuffer"
            };

            totalAllocatedMemory += bufferSize * sizeof(float);
            return buffer;
      }

      Mesh CreateMesh()
      {
            var mesh = new Mesh
            {
                  name = "PooledMesh"
            };

            // Приблизительная оценка памяти меша
            totalAllocatedMemory += 1024; // 1KB на меш (приблизительно)
            return mesh;
      }

      long GetTextureMemorySize(RenderTexture texture)
      {
            return texture.width * texture.height * GetBytesPerPixel(texture.format);
      }

      long GetTextureMemorySize(Texture2D texture)
      {
            return texture.width * texture.height * GetBytesPerPixel(texture.format);
      }

      int GetBytesPerPixel(RenderTextureFormat format)
      {
            switch (format)
            {
                  case RenderTextureFormat.RFloat: return 4;
                  case RenderTextureFormat.RGFloat: return 8;
                  case RenderTextureFormat.ARGBFloat: return 16;
                  case RenderTextureFormat.ARGB32: return 4;
                  default: return 4;
            }
      }

      int GetBytesPerPixel(TextureFormat format)
      {
            switch (format)
            {
                  case TextureFormat.RGB24: return 3;
                  case TextureFormat.RGBA32: return 4;
                  case TextureFormat.RFloat: return 4;
                  case TextureFormat.RGBAFloat: return 16;
                  default: return 4;
            }
      }

      /// <summary>
      /// Получить RenderTexture из пула
      /// </summary>
      public RenderTexture GetRenderTexture()
      {
            RenderTexture texture;

            if (renderTexturePool.Count > 0)
            {
                  texture = renderTexturePool.Dequeue();
            }
            else
            {
                  Debug.LogWarning("⚠️ RenderTexture pool пуст, создаем новую текстуру");
                  texture = CreateRenderTexture();
            }

            activeRenderTextures.Add(texture);
            return texture;
      }

      /// <summary>
      /// Вернуть RenderTexture в пул
      /// </summary>
      public void ReturnRenderTexture(RenderTexture texture)
      {
            if (texture == null || !activeRenderTextures.Contains(texture))
            {
                  return;
            }

            // Очищаем текстуру перед возвратом в пул
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = null;

            activeRenderTextures.Remove(texture);
            renderTexturePool.Enqueue(texture);
      }

      /// <summary>
      /// Получить Texture2D из пула
      /// </summary>
      public Texture2D GetTexture2D()
      {
            Texture2D texture;

            if (texture2DPool.Count > 0)
            {
                  texture = texture2DPool.Dequeue();
            }
            else
            {
                  Debug.LogWarning("⚠️ Texture2D pool пуст, создаем новую текстуру");
                  texture = CreateTexture2D();
            }

            activeTexture2Ds.Add(texture);
            return texture;
      }

      /// <summary>
      /// Вернуть Texture2D в пул
      /// </summary>
      public void ReturnTexture2D(Texture2D texture)
      {
            if (texture == null || !activeTexture2Ds.Contains(texture))
            {
                  return;
            }

            activeTexture2Ds.Remove(texture);
            texture2DPool.Enqueue(texture);
      }

      /// <summary>
      /// Получить GraphicsBuffer из пула
      /// </summary>
      public GraphicsBuffer GetGraphicsBuffer()
      {
            GraphicsBuffer buffer;

            if (bufferPool.Count > 0)
            {
                  buffer = bufferPool.Dequeue();
            }
            else
            {
                  Debug.LogWarning("⚠️ GraphicsBuffer pool пуст, создаем новый буфер");
                  buffer = CreateGraphicsBuffer();
            }

            activeBuffers.Add(buffer);
            return buffer;
      }

      /// <summary>
      /// Вернуть GraphicsBuffer в пул
      /// </summary>
      public void ReturnGraphicsBuffer(GraphicsBuffer buffer)
      {
            if (buffer == null || !activeBuffers.Contains(buffer))
            {
                  return;
            }

            activeBuffers.Remove(buffer);
            bufferPool.Enqueue(buffer);
      }

      /// <summary>
      /// Получить Mesh из пула
      /// </summary>
      public Mesh GetMesh()
      {
            Mesh mesh;

            if (meshPool.Count > 0)
            {
                  mesh = meshPool.Dequeue();
            }
            else
            {
                  Debug.LogWarning("⚠️ Mesh pool пуст, создаем новый меш");
                  mesh = CreateMesh();
            }

            activeMeshes.Add(mesh);
            return mesh;
      }

      /// <summary>
      /// Вернуть Mesh в пул
      /// </summary>
      public void ReturnMesh(Mesh mesh)
      {
            if (mesh == null || !activeMeshes.Contains(mesh))
            {
                  return;
            }

            // Очищаем меш перед возвратом в пул
            mesh.Clear();

            activeMeshes.Remove(mesh);
            meshPool.Enqueue(mesh);
      }

      /// <summary>
      /// Принудительная очистка всех пулов
      /// </summary>
      public void ClearAllPools()
      {
            Debug.Log("🧹 Очистка всех пулов памяти...");

            // Очищаем активные объекты
            foreach (var texture in activeRenderTextures)
            {
                  if (texture != null) texture.Release();
            }
            activeRenderTextures.Clear();

            foreach (var texture in activeTexture2Ds)
            {
                  if (texture != null) DestroyImmediate(texture);
            }
            activeTexture2Ds.Clear();

            foreach (var buffer in activeBuffers)
            {
                  buffer?.Dispose();
            }
            activeBuffers.Clear();

            foreach (var mesh in activeMeshes)
            {
                  if (mesh != null) DestroyImmediate(mesh);
            }
            activeMeshes.Clear();

            // Очищаем пулы
            while (renderTexturePool.Count > 0)
            {
                  var texture = renderTexturePool.Dequeue();
                  if (texture != null) texture.Release();
            }

            while (texture2DPool.Count > 0)
            {
                  var texture = texture2DPool.Dequeue();
                  if (texture != null) DestroyImmediate(texture);
            }

            while (bufferPool.Count > 0)
            {
                  var buffer = bufferPool.Dequeue();
                  buffer?.Dispose();
            }

            while (meshPool.Count > 0)
            {
                  var mesh = meshPool.Dequeue();
                  if (mesh != null) DestroyImmediate(mesh);
            }

            totalAllocatedMemory = 0;
            Debug.Log("✅ Все пулы очищены");
      }

      /// <summary>
      /// Получить статистику использования памяти
      /// </summary>
      public MemoryStats GetMemoryStats()
      {
            return new MemoryStats
            {
                  totalAllocatedMemoryMB = totalAllocatedMemory / (1024f * 1024f),
                  activeRenderTextures = activeRenderTextures.Count,
                  pooledRenderTextures = renderTexturePool.Count,
                  activeTexture2Ds = activeTexture2Ds.Count,
                  pooledTexture2Ds = texture2DPool.Count,
                  activeBuffers = activeBuffers.Count,
                  pooledBuffers = bufferPool.Count,
                  activeMeshes = activeMeshes.Count,
                  pooledMeshes = meshPool.Count
            };
      }

      /// <summary>
      /// Принудительная сборка мусора
      /// </summary>
      public void ForceGarbageCollection()
      {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            Debug.Log("🗑️ Принудительная сборка мусора завершена");
      }

      void OnDestroy()
      {
            ClearAllPools();
      }

      void OnApplicationPause(bool pauseStatus)
      {
            if (pauseStatus)
            {
                  // При паузе приложения можем освободить часть ресурсов
                  ForceGarbageCollection();
            }
      }
}

/// <summary>
/// Статистика использования памяти
/// </summary>
[System.Serializable]
public struct MemoryStats
{
      public float totalAllocatedMemoryMB;
      public int activeRenderTextures;
      public int pooledRenderTextures;
      public int activeTexture2Ds;
      public int pooledTexture2Ds;
      public int activeBuffers;
      public int pooledBuffers;
      public int activeMeshes;
      public int pooledMeshes;

      public override string ToString()
      {
            return $"Memory: {totalAllocatedMemoryMB:F1}MB, Active: RT:{activeRenderTextures} T2D:{activeTexture2Ds} Buf:{activeBuffers} Mesh:{activeMeshes}";
      }
}
using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Генерирует AR плоскости из масок сегментации SAM
/// </summary>
public class PlaneGenerator : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Минимальный размер маски для создания плоскости (в пикселях)")]
    [SerializeField] private int minimumMaskSize = 100;
    
    [Tooltip("Упрощение контура (больше = проще)")]
    [SerializeField] private float contourSimplification = 5f;
    
    [Tooltip("Префаб для визуализации плоскости")]
    [SerializeField] private GameObject planePrefab;
    
    [Header("Отладка")]
    [SerializeField] private bool debugMode = false;
    
    private Camera arCamera;
    private List<GameObject> generatedPlanes = new List<GameObject>();
    
    void Start()
    {
        arCamera = Camera.main;
        
        // Создаём простой префаб, если не назначен
        if (planePrefab == null)
        {
            planePrefab = CreateDefaultPlanePrefab();
        }
    }
    
    /// <summary>
    /// Создаёт AR плоскость из маски SAM
    /// </summary>
    public GameObject GeneratePlane(Tensor<float> samMask, int classId, Vector2 clickPosition)
    {
        if (samMask == null)
        {
            Debug.LogError("❌ PlaneGenerator: Маска SAM пустая!");
            return null;
        }
        
        Debug.Log($"🔨 PlaneGenerator: Генерация плоскости для класса {classId}...");
        
        // Копируем маску на CPU
        var maskCPU = samMask.ReadbackAndClone();
        
        // Извлекаем размеры маски (обычно [1, 1, H, W] или [1, H, W, 1])
        var shape = maskCPU.shape;
        int height = shape.rank == 4 ? shape[2] : shape[1];
        int width = shape.rank == 4 ? shape[3] : shape[2];
        
        Debug.Log($"📐 Размер маски SAM: {width}x{height}");
        
        // Бинаризуем маску (порог 0.5)
        bool[,] binaryMask = new bool[height, width];
        int maskPixelCount = 0;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = shape.rank == 4 ? maskCPU[0, 0, y, x] : maskCPU[0, y, x];
                bool isMask = value > 0.5f;
                binaryMask[y, x] = isMask;
                if (isMask) maskPixelCount++;
            }
        }
        
        maskCPU.Dispose();
        
        Debug.Log($"✅ Пикселей в маске: {maskPixelCount}");
        
        if (maskPixelCount < minimumMaskSize)
        {
            Debug.LogWarning($"⚠️ Маска слишком маленькая ({maskPixelCount} пикселей). Минимум: {minimumMaskSize}");
            return null;
        }
        
        // Находим контур маски
        List<Vector2> contour = FindContour(binaryMask, width, height);
        
        if (contour.Count < 3)
        {
            Debug.LogWarning("⚠️ Контур слишком прост для создания плоскости");
            return null;
        }
        
        Debug.Log($"📍 Контур: {contour.Count} точек");
        
        // Упрощаем контур
        List<Vector2> simplifiedContour = SimplifyContour(contour, contourSimplification);
        Debug.Log($"📍 Упрощённый контур: {simplifiedContour.Count} точек");
        
        // Создаём 3D плоскость
        GameObject plane = CreatePlaneFromContour(simplifiedContour, clickPosition, classId);
        
        if (plane != null)
        {
            generatedPlanes.Add(plane);
            Debug.Log($"🎉 Плоскость создана: {plane.name}");
        }
        
        return plane;
    }
    
    /// <summary>
    /// Находит контур маски (упрощённый алгоритм)
    /// </summary>
    private List<Vector2> FindContour(bool[,] mask, int width, int height)
    {
        List<Vector2> contour = new List<Vector2>();
        
        // Простой алгоритм: ищем пиксели на границе маски
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (mask[y, x])
                {
                    // Проверяем, есть ли рядом пиксель вне маски
                    bool isBorder = !mask[y - 1, x] || !mask[y + 1, x] || 
                                   !mask[y, x - 1] || !mask[y, x + 1];
                    
                    if (isBorder)
                    {
                        // Нормализуем координаты в [0, 1]
                        contour.Add(new Vector2((float)x / width, (float)y / height));
                    }
                }
            }
        }
        
        return contour;
    }
    
    /// <summary>
    /// Упрощает контур (Douglas-Peucker algorithm - упрощённая версия)
    /// </summary>
    private List<Vector2> SimplifyContour(List<Vector2> points, float epsilon)
    {
        if (points.Count < 3)
            return points;
        
        // Простое прореживание: берём каждую N-ую точку
        List<Vector2> simplified = new List<Vector2>();
        int step = Mathf.Max(1, (int)(epsilon));
        
        for (int i = 0; i < points.Count; i += step)
        {
            simplified.Add(points[i]);
        }
        
        // Замыкаем контур
        if (simplified.Count > 0 && simplified[simplified.Count - 1] != simplified[0])
        {
            simplified.Add(simplified[0]);
        }
        
        return simplified;
    }
    
    /// <summary>
    /// Создаёт 3D плоскость из 2D контура
    /// </summary>
    private GameObject CreatePlaneFromContour(List<Vector2> contour, Vector2 clickPosition, int classId)
    {
        GameObject plane = Instantiate(planePrefab);
        plane.name = $"AR_Plane_Class{classId}";
        
        // Raycast от камеры к точке клика для определения 3D позиции
        Ray ray = arCamera.ScreenPointToRay(new Vector3(clickPosition.x, clickPosition.y, 0));
        
        // Размещаем плоскость на расстоянии 2 метра от камеры (можно улучшить с AR depth)
        float distance = 2.0f;
        Vector3 planePosition = ray.GetPoint(distance);
        plane.transform.position = planePosition;
        
        // Ориентируем плоскость к камере
        plane.transform.LookAt(arCamera.transform);
        plane.transform.Rotate(0, 180, 0); // Flip to face camera
        
        // Создаём меш из контура
        MeshFilter meshFilter = plane.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = plane.AddComponent<MeshFilter>();
        
        MeshRenderer meshRenderer = plane.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = plane.AddComponent<MeshRenderer>();
        
        Mesh mesh = CreateMeshFromContour(contour);
        meshFilter.mesh = mesh;
        
        // Назначаем материал по классу
        Material material = GetMaterialForClass(classId);
        meshRenderer.material = material;
        
        return plane;
    }
    
    /// <summary>
    /// Создаёт меш из 2D контура
    /// </summary>
    private Mesh CreateMeshFromContour(List<Vector2> contour)
    {
        Mesh mesh = new Mesh();
        mesh.name = "PlaneFromContour";
        
        // Конвертируем 2D контур в 3D вершины (локальные координаты плоскости)
        Vector3[] vertices = new Vector3[contour.Count];
        for (int i = 0; i < contour.Count; i++)
        {
            // Центрируем и масштабируем
            vertices[i] = new Vector3(
                (contour[i].x - 0.5f) * 2f, // X в [-1, 1]
                (contour[i].y - 0.5f) * 2f, // Y в [-1, 1]
                0                            // Z = 0 (плоская)
            );
        }
        
        // Триангуляция (простой веерный алгоритм)
        List<int> triangles = new List<int>();
        for (int i = 1; i < vertices.Length - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Возвращает материал по ID класса
    /// </summary>
    private Material GetMaterialForClass(int classId)
    {
        Material mat = new Material(Shader.Find("Standard"));
        
        // Цвета по классам (можно настроить)
        switch (classId)
        {
            case 0: // Wall
                mat.color = new Color(0.8f, 0.8f, 0.9f, 0.7f);
                break;
            case 1: // Floor
                mat.color = new Color(0.6f, 0.5f, 0.4f, 0.7f);
                break;
            case 2: // Ceiling
                mat.color = new Color(1f, 1f, 1f, 0.7f);
                break;
            default:
                mat.color = new Color(0.5f, 0.7f, 1f, 0.7f);
                break;
        }
        
        // Полупрозрачность
        mat.SetFloat("_Mode", 3); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        
        return mat;
    }
    
    /// <summary>
    /// Создаёт простой префаб плоскости
    /// </summary>
    private GameObject CreateDefaultPlanePrefab()
    {
        GameObject prefab = new GameObject("PlanePrefab");
        prefab.AddComponent<MeshFilter>();
        prefab.AddComponent<MeshRenderer>();
        prefab.AddComponent<MeshCollider>();
        prefab.SetActive(false);
        return prefab;
    }
    
    /// <summary>
    /// Удаляет все созданные плоскости
    /// </summary>
    public void ClearAllPlanes()
    {
        foreach (var plane in generatedPlanes)
        {
            if (plane != null)
                Destroy(plane);
        }
        generatedPlanes.Clear();
        Debug.Log("🗑️ Все плоскости удалены");
    }
}


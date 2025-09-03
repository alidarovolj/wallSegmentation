using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Генератор процедурных 3D-мешей на основе контуров и плоскостей
/// Проецирует 2D-контуры на аппроксимированные 3D-плоскости
/// </summary>
public class ProceduralMeshGenerator : MonoBehaviour
{
      [Header("Настройки меша")]
      [SerializeField] private Material defaultMeshMaterial;
      [SerializeField] private bool generateColliders = true;
      [SerializeField] private bool generateUVs = true;
      [SerializeField] private float meshThickness = 0.01f; // Толщина меша в метрах

      [Header("Качество меша")]
      [SerializeField] private int triangulationMaxIterations = 100;
      [SerializeField] private float triangulationTolerance = 0.001f;
      [SerializeField] private bool optimizeMesh = true;
      [SerializeField] private float vertexWeldDistance = 0.001f; // Расстояние для слияния вершин

      [Header("Визуализация")]
      [SerializeField] private bool showWireframe = false;
      [SerializeField] private bool showNormals = false;
      [SerializeField] private float normalLength = 0.1f;
      [SerializeField] private Color wireframeColor = Color.green;

      [Header("Отладка")]
      [SerializeField] private bool enableDebugLogs = true;
      [SerializeField] private bool saveMeshAssets = false;

      // Сгенерированные меши
      private List<GameObject> generatedMeshes = new List<GameObject>();
      private Transform meshParent;

      // Кэш для производительности
      private Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

      // События
      public System.Action<GameObject, Mesh> OnMeshGenerated;

      private void Awake()
      {
            // Создаем контейнер для мешей
            GameObject meshContainer = new GameObject("Generated_Meshes");
            meshParent = meshContainer.transform;
            meshParent.SetParent(transform);

            // Создаем стандартный материал если не назначен
            if (defaultMeshMaterial == null)
            {
                  CreateDefaultMaterial();
            }

            LogDebug("🔧 ProceduralMeshGenerator инициализирован");
      }

      /// <summary>
      /// Генерирует 3D-меш из контура и плоскости
      /// </summary>
      /// <param name="contour">2D-контур в экранных координатах</param>
      /// <param name="planeData">Результат аппроксимации плоскости</param>
      /// <param name="screenToWorldMatrix">Матрица преобразования экран->мир</param>
      /// <returns>Созданный GameObject с мешем</returns>
      public GameObject GenerateMeshFromContour(List<Vector2> contour, PlaneApproximator.PlaneResult planeData, Matrix4x4 screenToWorldMatrix)
      {
            if (contour == null || contour.Count < 3)
            {
                  LogDebug("❌ Недостаточно точек контура для генерации меша");
                  return null;
            }

            if (!planeData.isValid)
            {
                  LogDebug("❌ Невалидный результат аппроксимации плоскости");
                  return null;
            }

            LogDebug($"🔧 Генерация меша из контура с {contour.Count} точками");

            try
            {
                  // 1. Проецируем 2D-контур на 3D-плоскость
                  List<Vector3> worldContour = ProjectContourToPlane(contour, planeData, screenToWorldMatrix);

                  // 2. Триангулируем контур
                  var triangulation = TriangulateContour(worldContour, planeData.normal);

                  if (triangulation.vertices.Count < 3 || triangulation.triangles.Count == 0)
                  {
                        LogDebug("❌ Ошибка триангуляции контура");
                        return null;
                  }

                  // 3. Создаем меш
                  Mesh mesh = CreateMeshFromTriangulation(triangulation, planeData.normal);

                  if (mesh == null)
                  {
                        LogDebug("❌ Ошибка создания меша");
                        return null;
                  }

                  // 4. Создаем GameObject с мешем
                  GameObject meshObject = CreateMeshGameObject(mesh, planeData);

                  if (meshObject != null)
                  {
                        generatedMeshes.Add(meshObject);
                        LogDebug($"✅ Меш сгенерирован: {mesh.vertexCount} вершин, {mesh.triangles.Length / 3} треугольников");

                        OnMeshGenerated?.Invoke(meshObject, mesh);
                  }

                  return meshObject;
            }
            catch (System.Exception e)
            {
                  LogDebug($"❌ Ошибка генерации меша: {e.Message}");
                  return null;
            }
      }

      /// <summary>
      /// Проецирует 2D-контур на 3D-плоскость
      /// </summary>
      private List<Vector3> ProjectContourToPlane(List<Vector2> contour, PlaneApproximator.PlaneResult planeResult, Matrix4x4 screenToWorldMatrix)
      {
            var worldContour = new List<Vector3>();

            // Создаем локальную систему координат плоскости
            Vector3 planeNormal = planeResult.normal;
            Vector3 planeCenter = planeResult.center;
            float planeScale = planeResult.bounds.size.magnitude;

            // Находим два ортогональных вектора в плоскости
            Vector3 planeU, planeV;
            CreatePlaneCoordinateSystem(planeNormal, out planeU, out planeV);

            foreach (var point2D in contour)
            {
                  // Проецируем 2D-точку на плоскость
                  Vector3 worldPoint = ProjectPointToPlane(point2D, planeCenter, planeNormal, planeU, planeV, screenToWorldMatrix, planeScale);
                  worldContour.Add(worldPoint);
            }

            LogDebug($"📐 Контур спроецирован на плоскость: {worldContour.Count} точек");
            return worldContour;
      }

      /// <summary>
      /// Создает систему координат в плоскости
      /// </summary>
      private void CreatePlaneCoordinateSystem(Vector3 normal, out Vector3 u, out Vector3 v)
      {
            // Находим два ортогональных вектора в плоскости
            Vector3 worldUp = Vector3.up;

            // Если нормаль близка к мировому Up, используем Forward
            if (Vector3.Dot(normal, worldUp) > 0.9f)
                  worldUp = Vector3.forward;

            u = Vector3.Cross(normal, worldUp).normalized;
            v = Vector3.Cross(normal, u).normalized;
      }

      /// <summary>
      /// Проецирует одну точку на плоскость
      /// </summary>
      private Vector3 ProjectPointToPlane(Vector2 screenPoint, Vector3 planeCenter, Vector3 planeNormal, Vector3 planeU, Vector3 planeV, Matrix4x4 screenToWorldMatrix, float planeScale = 1f)
      {
            // Простая проекция - используем центр плоскости как опорную точку
            // В реальной реализации здесь должна быть более сложная проекция через ray casting

            // Конвертируем экранные координаты в смещения от центра плоскости
            Vector2 normalizedScreen = new Vector2(
                (screenPoint.x / Screen.width - 0.5f) * 2f,
                (screenPoint.y / Screen.height - 0.5f) * 2f
            );

            // Масштабируем в зависимости от размера плоскости (используем переданный масштаб)
            float scale = planeScale * 0.5f;
            Vector3 offset = planeU * normalizedScreen.x * scale + planeV * normalizedScreen.y * scale;

            return planeCenter + offset;
      }

      /// <summary>
      /// Триангулирует 3D-контур
      /// </summary>
      private MeshTriangulation TriangulateContour(List<Vector3> worldContour, Vector3 planeNormal)
      {
            // Проецируем 3D-контур обратно в 2D для триангуляции
            var localContour = ProjectContourTo2D(worldContour, planeNormal);

            // Применяем алгоритм "ушей" (ear clipping) для триангуляции
            var triangles = EarClippingTriangulation(localContour);

            // Конвертируем обратно в 3D
            return new MeshTriangulation
            {
                  vertices = worldContour,
                  triangles = triangles,
                  normals = Enumerable.Repeat(planeNormal, worldContour.Count).ToList()
            };
      }

      /// <summary>
      /// Проецирует 3D-контур в 2D для триангуляции
      /// </summary>
      private List<Vector2> ProjectContourTo2D(List<Vector3> worldContour, Vector3 planeNormal)
      {
            // Создаем локальную систему координат
            Vector3 planeU, planeV;
            CreatePlaneCoordinateSystem(planeNormal, out planeU, out planeV);

            var localContour = new List<Vector2>();
            Vector3 origin = worldContour[0]; // Используем первую точку как начало координат

            foreach (var worldPoint in worldContour)
            {
                  Vector3 relative = worldPoint - origin;
                  Vector2 local = new Vector2(
                      Vector3.Dot(relative, planeU),
                      Vector3.Dot(relative, planeV)
                  );
                  localContour.Add(local);
            }

            return localContour;
      }

      /// <summary>
      /// Триангуляция методом "ушей" (ear clipping)
      /// </summary>
      private List<int> EarClippingTriangulation(List<Vector2> contour)
      {
            var triangles = new List<int>();
            var vertices = new List<Vector2>(contour);
            var indices = new List<int>();

            // Инициализируем индексы
            for (int i = 0; i < vertices.Count; i++)
                  indices.Add(i);

            int iterations = 0;
            while (indices.Count > 3 && iterations < triangulationMaxIterations)
            {
                  bool foundEar = false;

                  for (int i = 0; i < indices.Count; i++)
                  {
                        int prev = indices[(i - 1 + indices.Count) % indices.Count];
                        int curr = indices[i];
                        int next = indices[(i + 1) % indices.Count];

                        // Проверяем, является ли текущая вершина "ухом"
                        if (IsEar(vertices, indices, prev, curr, next))
                        {
                              // Добавляем треугольник
                              triangles.Add(prev);
                              triangles.Add(curr);
                              triangles.Add(next);

                              // Удаляем вершину
                              indices.RemoveAt(i);
                              foundEar = true;
                              break;
                        }
                  }

                  if (!foundEar)
                  {
                        LogDebug("⚠️ Не удалось найти 'ухо' для триангуляции");
                        break;
                  }

                  iterations++;
            }

            // Добавляем последний треугольник
            if (indices.Count == 3)
            {
                  triangles.Add(indices[0]);
                  triangles.Add(indices[1]);
                  triangles.Add(indices[2]);
            }

            LogDebug($"🔺 Триангуляция завершена: {triangles.Count / 3} треугольников за {iterations} итераций");
            return triangles;
      }

      /// <summary>
      /// Проверяет, является ли вершина "ухом" для триангуляции
      /// </summary>
      private bool IsEar(List<Vector2> vertices, List<int> indices, int prevIndex, int currIndex, int nextIndex)
      {
            Vector2 prev = vertices[prevIndex];
            Vector2 curr = vertices[currIndex];
            Vector2 next = vertices[nextIndex];

            // Проверяем, что треугольник имеет правильную ориентацию (против часовой стрелки)
            float cross = (curr.x - prev.x) * (next.y - prev.y) - (curr.y - prev.y) * (next.x - prev.x);
            if (cross <= 0) // Выпуклая вершина
                  return false;

            // Проверяем, что внутри треугольника нет других вершин
            for (int i = 0; i < indices.Count; i++)
            {
                  int testIndex = indices[i];
                  if (testIndex == prevIndex || testIndex == currIndex || testIndex == nextIndex)
                        continue;

                  Vector2 testPoint = vertices[testIndex];
                  if (IsPointInTriangle(testPoint, prev, curr, next))
                        return false;
            }

            return true;
      }

      /// <summary>
      /// Проверяет, находится ли точка внутри треугольника
      /// </summary>
      private bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
      {
            float denom = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(denom) < triangulationTolerance)
                  return false;

            float alpha = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denom;
            float beta = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denom;
            float gamma = 1 - alpha - beta;

            return alpha >= 0 && beta >= 0 && gamma >= 0;
      }

      /// <summary>
      /// Создает меш из результата триангуляции
      /// </summary>
      private Mesh CreateMeshFromTriangulation(MeshTriangulation triangulation, Vector3 normal)
      {
            Mesh mesh = new Mesh();
            mesh.name = $"ProceduralMesh_{System.DateTime.Now.Ticks}";

            // Устанавливаем вершины
            mesh.vertices = triangulation.vertices.ToArray();

            // Устанавливаем треугольники
            mesh.triangles = triangulation.triangles.ToArray();

            // Устанавливаем нормали
            if (triangulation.normals != null && triangulation.normals.Count == triangulation.vertices.Count)
            {
                  mesh.normals = triangulation.normals.ToArray();
            }
            else
            {
                  mesh.RecalculateNormals();
            }

            // Генерируем UV-координаты если нужно
            if (generateUVs)
            {
                  GenerateUVCoordinates(mesh, normal);
            }

            // Оптимизируем меш если включено
            if (optimizeMesh)
            {
                  OptimizeMesh(mesh);
            }

            // Вычисляем границы
            mesh.RecalculateBounds();

            LogDebug($"📐 Меш создан: {mesh.vertexCount} вершин, {mesh.triangles.Length / 3} треугольников");
            return mesh;
      }

      /// <summary>
      /// Генерирует UV-координаты для меша
      /// </summary>
      private void GenerateUVCoordinates(Mesh mesh, Vector3 planeNormal)
      {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = new Vector2[vertices.Length];

            // Создаем локальную систему координат для UV-маппинга
            Vector3 planeU, planeV;
            CreatePlaneCoordinateSystem(planeNormal, out planeU, out planeV);

            // Находим границы для нормализации
            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;

            Vector3 origin = vertices.Length > 0 ? vertices[0] : Vector3.zero;

            for (int i = 0; i < vertices.Length; i++)
            {
                  Vector3 relative = vertices[i] - origin;
                  float u = Vector3.Dot(relative, planeU);
                  float v = Vector3.Dot(relative, planeV);

                  minU = Mathf.Min(minU, u);
                  maxU = Mathf.Max(maxU, u);
                  minV = Mathf.Min(minV, v);
                  maxV = Mathf.Max(maxV, v);
            }

            // Нормализуем UV-координаты
            float uRange = maxU - minU;
            float vRange = maxV - minV;

            for (int i = 0; i < vertices.Length; i++)
            {
                  Vector3 relative = vertices[i] - origin;
                  float u = Vector3.Dot(relative, planeU);
                  float v = Vector3.Dot(relative, planeV);

                  uvs[i] = new Vector2(
                      uRange > 0 ? (u - minU) / uRange : 0f,
                      vRange > 0 ? (v - minV) / vRange : 0f
                  );
            }

            mesh.uv = uvs;
            LogDebug("🎨 UV-координаты сгенерированы");
      }

      /// <summary>
      /// Оптимизирует меш
      /// </summary>
      private void OptimizeMesh(Mesh mesh)
      {
            // Простая оптимизация - удаление дублирующихся вершин
            var vertices = new List<Vector3>(mesh.vertices);
            var triangles = new List<int>(mesh.triangles);
            var newVertices = new List<Vector3>();
            var vertexMap = new Dictionary<int, int>();

            for (int i = 0; i < vertices.Count; i++)
            {
                  bool isDuplicate = false;
                  int duplicateIndex = -1;

                  for (int j = 0; j < newVertices.Count; j++)
                  {
                        if (Vector3.Distance(vertices[i], newVertices[j]) < vertexWeldDistance)
                        {
                              isDuplicate = true;
                              duplicateIndex = j;
                              break;
                        }
                  }

                  if (isDuplicate)
                  {
                        vertexMap[i] = duplicateIndex;
                  }
                  else
                  {
                        vertexMap[i] = newVertices.Count;
                        newVertices.Add(vertices[i]);
                  }
            }

            // Обновляем индексы треугольников
            for (int i = 0; i < triangles.Count; i++)
            {
                  triangles[i] = vertexMap[triangles[i]];
            }

            mesh.vertices = newVertices.ToArray();
            mesh.triangles = triangles.ToArray();

            LogDebug($"⚡ Меш оптимизирован: {vertices.Count} → {newVertices.Count} вершин");
      }

      /// <summary>
      /// Создает GameObject с мешем
      /// </summary>
      private GameObject CreateMeshGameObject(Mesh mesh, PlaneApproximator.PlaneResult planeResult)
      {
            GameObject meshObject = new GameObject($"ProceduralMesh_{generatedMeshes.Count}");
            meshObject.transform.SetParent(meshParent);

            // Добавляем MeshFilter и MeshRenderer
            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.material = defaultMeshMaterial;

            // Добавляем коллайдер если нужно
            if (generateColliders)
            {
                  MeshCollider meshCollider = meshObject.AddComponent<MeshCollider>();
                  meshCollider.sharedMesh = mesh;
                  meshCollider.convex = false; // Для сложных мешей
            }

            // Добавляем информацию о меше
            ProceduralMeshInfo meshInfo = meshObject.AddComponent<ProceduralMeshInfo>();
            meshInfo.Initialize(planeResult, mesh.vertexCount, mesh.triangles.Length / 3);

            // Позиционируем меш
            meshObject.transform.position = planeResult.center;

            return meshObject;
      }

      /// <summary>
      /// Создает стандартный материал
      /// </summary>
      private void CreateDefaultMaterial()
      {
            defaultMeshMaterial = new Material(Shader.Find("Standard"));
            defaultMeshMaterial.name = "DefaultProceduralMaterial";
            defaultMeshMaterial.color = new Color(0.7f, 0.7f, 0.9f, 0.8f); // Полупрозрачный голубой
            defaultMeshMaterial.SetFloat("_Mode", 3); // Transparent
            defaultMeshMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            defaultMeshMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            defaultMeshMaterial.SetInt("_ZWrite", 0);
            defaultMeshMaterial.DisableKeyword("_ALPHATEST_ON");
            defaultMeshMaterial.EnableKeyword("_ALPHABLEND_ON");
            defaultMeshMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            defaultMeshMaterial.renderQueue = 3000;

            LogDebug("🎨 Создан стандартный материал меша");
      }

      /// <summary>
      /// Очищает все сгенерированные меши
      /// </summary>
      public void ClearAllMeshes()
      {
            foreach (var meshObj in generatedMeshes)
            {
                  if (meshObj != null)
                  {
                        if (Application.isPlaying)
                              Destroy(meshObj);
                        else
                              DestroyImmediate(meshObj);
                  }
            }

            generatedMeshes.Clear();
            meshCache.Clear();

            LogDebug("🗑️ Все меши очищены");
      }

      /// <summary>
      /// Получает список сгенерированных мешей
      /// </summary>
      public List<GameObject> GetGeneratedMeshes()
      {
            return new List<GameObject>(generatedMeshes);
      }

      private void LogDebug(string message)
      {
            if (enableDebugLogs)
                  Debug.Log($"[ProceduralMeshGenerator] {message}");
      }

      private void OnDrawGizmos()
      {
            if (showNormals || showWireframe)
            {
                  foreach (var meshObj in generatedMeshes)
                  {
                        if (meshObj == null) continue;

                        MeshFilter meshFilter = meshObj.GetComponent<MeshFilter>();
                        if (meshFilter?.sharedMesh == null) continue;

                        Mesh mesh = meshFilter.sharedMesh;
                        Transform meshTransform = meshObj.transform;

                        if (showWireframe)
                        {
                              Gizmos.color = wireframeColor;
                              Gizmos.matrix = meshTransform.localToWorldMatrix;
                              Gizmos.DrawWireMesh(mesh);
                        }

                        if (showNormals)
                        {
                              Gizmos.color = Color.blue;
                              Vector3[] vertices = mesh.vertices;
                              Vector3[] normals = mesh.normals;

                              for (int i = 0; i < vertices.Length; i++)
                              {
                                    Vector3 worldVertex = meshTransform.TransformPoint(vertices[i]);
                                    Vector3 worldNormal = meshTransform.TransformDirection(normals[i]);
                                    Gizmos.DrawLine(worldVertex, worldVertex + worldNormal * normalLength);
                              }
                        }
                  }
            }
      }
}

/// <summary>
/// Результат триангуляции меша
/// </summary>
public struct MeshTriangulation
{
      public List<Vector3> vertices;
      public List<int> triangles;
      public List<Vector3> normals;
}

/// <summary>
/// Информация о процедурном меше
/// </summary>
public class ProceduralMeshInfo : MonoBehaviour
{
      public PlaneApproximator.PlaneResult sourceePlane;
      public int vertexCount;
      public int triangleCount;
      public float generationTime;

      public void Initialize(PlaneApproximator.PlaneResult plane, int vertices, int triangles)
      {
            sourceePlane = plane;
            vertexCount = vertices;
            triangleCount = triangles;
            generationTime = Time.time;
      }
}

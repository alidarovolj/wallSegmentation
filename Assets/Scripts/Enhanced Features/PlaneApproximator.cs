using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Математический аппроксиматор плоскости по облаку точек
/// Использует метод наименьших квадратов и PCA для определения оптимальной плоскости
/// </summary>
public static class PlaneApproximator
{
      /// <summary>
      /// Результат аппроксимации плоскости
      /// </summary>
      public struct PlaneResult
      {
            public Vector3 normal;        // Нормаль плоскости
            public Vector3 center;        // Центр плоскости
            public float distance;        // Расстояние от начала координат
            public float confidence;      // Уверенность в результате (0-1)
            public float averageError;    // Средняя ошибка точек относительно плоскости
            public Bounds bounds;         // Границы облака точек
            public bool isValid;          // Валидна ли аппроксимация

            public static PlaneResult Invalid => new PlaneResult { isValid = false };
      }

      /// <summary>
      /// Аппроксимирует плоскость по облаку точек методом наименьших квадратов
      /// </summary>
      /// <param name="points">Облако точек</param>
      /// <param name="minPoints">Минимальное количество точек для аппроксимации</param>
      /// <returns>Результат аппроксимации</returns>
      public static PlaneResult ApproximatePlane(List<Vector3> points, int minPoints = 3)
      {
            if (points == null || points.Count < minPoints)
            {
                  Debug.LogWarning($"⚠️ Недостаточно точек для аппроксимации: {points?.Count ?? 0} < {minPoints}");
                  return PlaneResult.Invalid;
            }

            // Удаляем дубликаты и выбросы
            var cleanedPoints = CleanPointCloud(points);
            if (cleanedPoints.Count < minPoints)
            {
                  Debug.LogWarning($"⚠️ После очистки осталось недостаточно точек: {cleanedPoints.Count}");
                  return PlaneResult.Invalid;
            }

            Debug.Log($"🔢 Аппроксимация плоскости по {cleanedPoints.Count} точкам");

            try
            {
                  // 1. Вычисляем центр масс
                  Vector3 centroid = CalculateCentroid(cleanedPoints);

                  // 2. Строим ковариационную матрицу
                  Matrix3x3 covarianceMatrix = CalculateCovarianceMatrix(cleanedPoints, centroid);

                  // 3. Находим собственные векторы (PCA)
                  var eigenResult = FindEigenvectors(covarianceMatrix);

                  // 4. Нормаль плоскости = собственный вектор с наименьшим собственным значением
                  Vector3 normal = eigenResult.smallestEigenvector;

                  // 5. Вычисляем расстояние плоскости от начала координат
                  float distance = Vector3.Dot(normal, centroid);

                  // 6. Оцениваем качество аппроксимации
                  float averageError = CalculateAverageError(cleanedPoints, normal, distance);
                  float confidence = CalculateConfidence(cleanedPoints, normal, distance, averageError);

                  // 7. Вычисляем границы
                  Bounds bounds = CalculateBounds(cleanedPoints);

                  var result = new PlaneResult
                  {
                        normal = normal,
                        center = centroid,
                        distance = distance,
                        confidence = confidence,
                        averageError = averageError,
                        bounds = bounds,
                        isValid = true
                  };

                  Debug.Log($"✅ Плоскость аппроксимирована: нормаль={normal}, центр={centroid}, ошибка={averageError:F3}м, уверенность={confidence:F2}");
                  return result;
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка аппроксимации плоскости: {e.Message}");
                  return PlaneResult.Invalid;
            }
      }

      /// <summary>
      /// Очищает облако точек от дубликатов и выбросов
      /// </summary>
      private static List<Vector3> CleanPointCloud(List<Vector3> points)
      {
            var cleaned = new List<Vector3>();
            const float minDistance = 0.01f; // 1см минимальное расстояние между точками

            // Удаляем дубликаты
            foreach (var point in points)
            {
                  bool isDuplicate = false;
                  foreach (var existing in cleaned)
                  {
                        if (Vector3.Distance(point, existing) < minDistance)
                        {
                              isDuplicate = true;
                              break;
                        }
                  }

                  if (!isDuplicate)
                        cleaned.Add(point);
            }

            // Удаляем статистические выбросы (точки дальше 3 стандартных отклонений)
            if (cleaned.Count > 10)
            {
                  cleaned = RemoveOutliers(cleaned);
            }

            Debug.Log($"🧹 Очистка точек: {points.Count} → {cleaned.Count}");
            return cleaned;
      }

      /// <summary>
      /// Удаляет статистические выбросы из облака точек
      /// </summary>
      private static List<Vector3> RemoveOutliers(List<Vector3> points)
      {
            if (points.Count < 4)
                  return points;

            // Вычисляем центр и стандартное отклонение
            Vector3 center = CalculateCentroid(points);
            float avgDistance = points.Average(p => Vector3.Distance(p, center));
            float variance = points.Average(p => Mathf.Pow(Vector3.Distance(p, center) - avgDistance, 2));
            float stdDev = Mathf.Sqrt(variance);

            // Удаляем точки дальше 2.5 стандартных отклонений
            var filtered = points.Where(p =>
                Vector3.Distance(p, center) <= avgDistance + 2.5f * stdDev
            ).ToList();

            Debug.Log($"🎯 Удаление выбросов: {points.Count} → {filtered.Count} (σ={stdDev:F3})");
            return filtered;
      }

      /// <summary>
      /// Вычисляет центр масс облака точек
      /// </summary>
      private static Vector3 CalculateCentroid(List<Vector3> points)
      {
            Vector3 sum = Vector3.zero;
            foreach (var point in points)
                  sum += point;
            return sum / points.Count;
      }

      /// <summary>
      /// Вычисляет ковариационную матрицу для облака точек
      /// </summary>
      private static Matrix3x3 CalculateCovarianceMatrix(List<Vector3> points, Vector3 centroid)
      {
            Matrix3x3 covariance = Matrix3x3.Zero;

            foreach (var point in points)
            {
                  Vector3 diff = point - centroid;

                  covariance.m00 += diff.x * diff.x;
                  covariance.m01 += diff.x * diff.y;
                  covariance.m02 += diff.x * diff.z;
                  covariance.m11 += diff.y * diff.y;
                  covariance.m12 += diff.y * diff.z;
                  covariance.m22 += diff.z * diff.z;
            }

            // Симметричная матрица
            covariance.m10 = covariance.m01;
            covariance.m20 = covariance.m02;
            covariance.m21 = covariance.m12;

            // Нормализуем
            float invCount = 1f / points.Count;
            covariance *= invCount;

            return covariance;
      }

      /// <summary>
      /// Находит собственные векторы матрицы 3x3 методом итераций
      /// </summary>
      private static EigenResult FindEigenvectors(Matrix3x3 matrix)
      {
            // Упрощенный метод поиска собственных векторов
            // В производственной версии можно использовать более точные алгоритмы

            Vector3[] eigenvectors = new Vector3[3];
            float[] eigenvalues = new float[3];

            // Используем степенной метод для поиска доминирующего собственного вектора
            Vector3 v1 = FindDominantEigenvector(matrix, 50);
            float lambda1 = CalculateEigenvalue(matrix, v1);
            eigenvectors[0] = v1;
            eigenvalues[0] = lambda1;

            // Дефлируем матрицу и находим следующий собственный вектор
            Matrix3x3 deflated1 = DeflateMatrix(matrix, v1, lambda1);
            Vector3 v2 = FindDominantEigenvector(deflated1, 50);
            float lambda2 = CalculateEigenvalue(matrix, v2);
            eigenvectors[1] = v2;
            eigenvalues[1] = lambda2;

            // Третий вектор = векторное произведение первых двух
            Vector3 v3 = Vector3.Cross(v1, v2).normalized;
            float lambda3 = CalculateEigenvalue(matrix, v3);
            eigenvectors[2] = v3;
            eigenvalues[2] = lambda3;

            // Сортируем по убыванию собственных значений
            for (int i = 0; i < 2; i++)
            {
                  for (int j = i + 1; j < 3; j++)
                  {
                        if (eigenvalues[j] > eigenvalues[i])
                        {
                              // Меняем местами
                              float tempVal = eigenvalues[i];
                              eigenvalues[i] = eigenvalues[j];
                              eigenvalues[j] = tempVal;

                              Vector3 tempVec = eigenvectors[i];
                              eigenvectors[i] = eigenvectors[j];
                              eigenvectors[j] = tempVec;
                        }
                  }
            }

            return new EigenResult
            {
                  largestEigenvector = eigenvectors[0],
                  middleEigenvector = eigenvectors[1],
                  smallestEigenvector = eigenvectors[2], // Нормаль плоскости
                  largestEigenvalue = eigenvalues[0],
                  middleEigenvalue = eigenvalues[1],
                  smallestEigenvalue = eigenvalues[2]
            };
      }

      /// <summary>
      /// Находит доминирующий собственный вектор степенным методом
      /// </summary>
      private static Vector3 FindDominantEigenvector(Matrix3x3 matrix, int maxIterations)
      {
            Vector3 v = new Vector3(1f, 1f, 1f).normalized;

            for (int i = 0; i < maxIterations; i++)
            {
                  Vector3 newV = matrix * v;
                  newV = newV.normalized;

                  // Проверяем сходимость
                  if (Vector3.Dot(v, newV) > 0.9999f)
                        break;

                  v = newV;
            }

            return v;
      }

      /// <summary>
      /// Вычисляет собственное значение для данного вектора
      /// </summary>
      private static float CalculateEigenvalue(Matrix3x3 matrix, Vector3 vector)
      {
            Vector3 result = matrix * vector;
            return Vector3.Dot(result, vector) / Vector3.Dot(vector, vector);
      }

      /// <summary>
      /// Дефлирует матрицу, убирая влияние найденного собственного вектора
      /// </summary>
      private static Matrix3x3 DeflateMatrix(Matrix3x3 matrix, Vector3 eigenvector, float eigenvalue)
      {
            Matrix3x3 outerProduct = Matrix3x3.OuterProduct(eigenvector, eigenvector);
            return matrix - outerProduct * eigenvalue;
      }

      /// <summary>
      /// Вычисляет среднюю ошибку точек относительно плоскости
      /// </summary>
      private static float CalculateAverageError(List<Vector3> points, Vector3 normal, float distance)
      {
            float totalError = 0f;

            foreach (var point in points)
            {
                  float pointDistance = Mathf.Abs(Vector3.Dot(normal, point) - distance);
                  totalError += pointDistance;
            }

            return totalError / points.Count;
      }

      /// <summary>
      /// Вычисляет уверенность в аппроксимации (0-1)
      /// </summary>
      private static float CalculateConfidence(List<Vector3> points, Vector3 normal, float distance, float averageError)
      {
            // Уверенность зависит от:
            // 1. Количества точек (больше = лучше)
            // 2. Средней ошибки (меньше = лучше)
            // 3. Равномерности распределения точек

            float pointCountFactor = Mathf.Clamp01(points.Count / 50f); // Оптимально 50+ точек
            float errorFactor = Mathf.Clamp01(1f - averageError / 0.1f); // Ошибка до 10см считается хорошей

            // Проверяем равномерность распределения
            float distributionFactor = CalculateDistributionUniformity(points);

            float confidence = (pointCountFactor * 0.4f + errorFactor * 0.4f + distributionFactor * 0.2f);
            return Mathf.Clamp01(confidence);
      }

      /// <summary>
      /// Оценивает равномерность распределения точек
      /// </summary>
      private static float CalculateDistributionUniformity(List<Vector3> points)
      {
            if (points.Count < 4)
                  return 0f;

            // Простая оценка: смотрим на стандартное отклонение расстояний между соседними точками
            var distances = new List<float>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                  float minDist = float.MaxValue;
                  for (int j = i + 1; j < points.Count; j++)
                  {
                        float dist = Vector3.Distance(points[i], points[j]);
                        if (dist < minDist)
                              minDist = dist;
                  }
                  distances.Add(minDist);
            }

            float avgDistance = distances.Average();
            float variance = distances.Average(d => Mathf.Pow(d - avgDistance, 2));
            float stdDev = Mathf.Sqrt(variance);

            // Чем меньше стандартное отклонение относительно среднего, тем равномернее
            float uniformity = avgDistance > 0 ? 1f - Mathf.Clamp01(stdDev / avgDistance) : 0f;
            return uniformity;
      }

      /// <summary>
      /// Вычисляет границы облака точек
      /// </summary>
      private static Bounds CalculateBounds(List<Vector3> points)
      {
            if (points.Count == 0)
                  return new Bounds();

            Vector3 min = points[0];
            Vector3 max = points[0];

            foreach (var point in points)
            {
                  min = Vector3.Min(min, point);
                  max = Vector3.Max(max, point);
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            return new Bounds(center, size);
      }

      /// <summary>
      /// Результат вычисления собственных векторов
      /// </summary>
      private struct EigenResult
      {
            public Vector3 largestEigenvector;
            public Vector3 middleEigenvector;
            public Vector3 smallestEigenvector;
            public float largestEigenvalue;
            public float middleEigenvalue;
            public float smallestEigenvalue;
      }
}

/// <summary>
/// Простая матрица 3x3 для вычислений
/// </summary>
public struct Matrix3x3
{
      public float m00, m01, m02;
      public float m10, m11, m12;
      public float m20, m21, m22;

      public static Matrix3x3 Zero => new Matrix3x3();

      public static Matrix3x3 operator *(Matrix3x3 a, float scalar)
      {
            return new Matrix3x3
            {
                  m00 = a.m00 * scalar,
                  m01 = a.m01 * scalar,
                  m02 = a.m02 * scalar,
                  m10 = a.m10 * scalar,
                  m11 = a.m11 * scalar,
                  m12 = a.m12 * scalar,
                  m20 = a.m20 * scalar,
                  m21 = a.m21 * scalar,
                  m22 = a.m22 * scalar
            };
      }

      public static Matrix3x3 operator -(Matrix3x3 a, Matrix3x3 b)
      {
            return new Matrix3x3
            {
                  m00 = a.m00 - b.m00,
                  m01 = a.m01 - b.m01,
                  m02 = a.m02 - b.m02,
                  m10 = a.m10 - b.m10,
                  m11 = a.m11 - b.m11,
                  m12 = a.m12 - b.m12,
                  m20 = a.m20 - b.m20,
                  m21 = a.m21 - b.m21,
                  m22 = a.m22 - b.m22
            };
      }

      public static Vector3 operator *(Matrix3x3 matrix, Vector3 vector)
      {
            return new Vector3(
                matrix.m00 * vector.x + matrix.m01 * vector.y + matrix.m02 * vector.z,
                matrix.m10 * vector.x + matrix.m11 * vector.y + matrix.m12 * vector.z,
                matrix.m20 * vector.x + matrix.m21 * vector.y + matrix.m22 * vector.z
            );
      }

      public static Matrix3x3 OuterProduct(Vector3 a, Vector3 b)
      {
            return new Matrix3x3
            {
                  m00 = a.x * b.x,
                  m01 = a.x * b.y,
                  m02 = a.x * b.z,
                  m10 = a.y * b.x,
                  m11 = a.y * b.y,
                  m12 = a.y * b.z,
                  m20 = a.z * b.x,
                  m21 = a.z * b.y,
                  m22 = a.z * b.z
            };
      }
}

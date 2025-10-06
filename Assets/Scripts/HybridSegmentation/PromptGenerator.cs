using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;

public class PromptGenerator : MonoBehaviour
{
    public struct Prompt
    {
        public Tensor<float> pointCoords;
        public Tensor<float> pointLabels;
        public Tensor<float> maskInput; // Не используется в этой реализации, но является частью API
        public Tensor<float> hasMaskInput;
    }

    /// <summary>
    /// Создает подсказки (точки) из бинарной маски сегментации.
    /// </summary>
    /// <param name="mask">Маска от SegFormer (Tensor<int>).</param>
    /// <param name="targetClassId">ID класса, для которого генерируются точки (например, "стена").</param>
    /// <param name="numPositivePoints">Количество позитивных точек для генерации.</param>
    /// <returns>Структура Prompt с координатами и метками точек.</returns>
    public Prompt GeneratePointsFromMask(Tensor<int> mask, int targetClassId, int numPositivePoints = 1)
    {
        // TODO: Реализовать логику поиска связанных компонентов (connected components)
        // и генерации точек внутри самого большого компонента.

        // Временная заглушка: генерируем одну точку в центре маски.
        var shape = mask.shape;
        int height = shape[1];
        int width = shape[2];
        
        // Создаем тензоры для координат и меток
        // Формат: [1, num_points, 2] для координат, [1, num_points] для меток
        var pointCoords = new Tensor<float>(new TensorShape(1, numPositivePoints, 2));
        var pointLabels = new Tensor<float>(new TensorShape(1, numPositivePoints));
        
        // Генерируем одну позитивную точку в центре
        pointCoords[0, 0, 0] = width * 0.5f;
        pointCoords[0, 0, 1] = height * 0.5f;
        pointLabels[0, 0] = 1; // 1 = позитивная точка

        var prompt = new Prompt
        {
            pointCoords = pointCoords,
            pointLabels = pointLabels,
            maskInput = new Tensor<float>(new TensorShape(1, 1, 256, 256)), // Пустой тензор
            hasMaskInput = new Tensor<float>(new TensorShape(1), new float[] { 0 })
        };
        
        Debug.Log("[PromptGenerator] 💡 Generated a single positive point prompt at the center.");

        return prompt;
    }

    /// <summary>
    /// Создает подсказку на основе тапа пользователя.
    /// </summary>
    /// <param name="tapPosition">Координаты тапа (в пикселях).</param>
    /// <param name="isPositive">Является ли тап позитивным (добавление) или негативным (исключение).</param>
    /// <returns>Структура Prompt.</returns>
    public Prompt GeneratePointFromTap(Vector2 tapPosition, bool isPositive = true)
    {
        var pointCoords = new Tensor<float>(new TensorShape(1, 1, 2));
        var pointLabels = new Tensor<float>(new TensorShape(1, 1));
        
        pointCoords[0, 0, 0] = tapPosition.x;
        pointCoords[0, 0, 1] = tapPosition.y;
        pointLabels[0, 0] = isPositive ? 1f : 0f;

        var prompt = new Prompt
        {
            pointCoords = pointCoords,
            pointLabels = pointLabels,
            maskInput = new Tensor<float>(new TensorShape(1, 1, 256, 256)), // Пустой
            hasMaskInput = new Tensor<float>(new TensorShape(1), new float[] { 0 })
        };
        
        Debug.Log($"[PromptGenerator] 👆 Generated a {(isPositive ? "positive" : "negative")} tap prompt at {tapPosition}.");

        return prompt;
    }
}

using UnityEngine;

/// <summary>
/// Координирует процесс покраски, слушая события и создавая команды.
/// </summary>
public class PaintingCoordinator : MonoBehaviour
{
      [Header("References")]
      [SerializeField] private CommandManager commandManager;
      [SerializeField] private PaintManager paintManager;
      [SerializeField] private UIManager uiManager; // Для получения текущего цвета и режима

      void OnEnable()
      {
            PaintingEvents.OnPaintClassRequested += HandlePaintRequest;
      }

      void OnDisable()
      {
            PaintingEvents.OnPaintClassRequested -= HandlePaintRequest;
      }

      private void HandlePaintRequest(int classId)
      {
            if (commandManager == null || paintManager == null || uiManager == null)
            {
                  Debug.LogError("❌ Координатор не настроен!");
                  return;
            }

            // Получаем текущие настройки из UI
            Color currentColor = uiManager.GetCurrentColor();
            int currentBlendMode = uiManager.GetCurrentBlendMode();
            float currentMetallic = uiManager.GetCurrentMetallic();
            float currentSmoothness = uiManager.GetCurrentSmoothness();

            // Создаем и выполняем команду
            var command = new PaintCommand(paintManager, classId, currentColor, currentBlendMode, currentMetallic, currentSmoothness);
            commandManager.ExecuteCommand(command);
      }
}
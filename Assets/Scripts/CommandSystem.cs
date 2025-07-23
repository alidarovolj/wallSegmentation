using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Базовый интерфейс для всех команд
/// </summary>
public interface ICommand
{
      void Execute();
      void Undo();
}

/// <summary>
/// Команда для окрашивания целого класса поверхностей.
/// </summary>
public class PaintCommand : ICommand
{
      private PaintManager _paintManager;
      private int _classId;

      // Новые свойства для покраски
      private Color _newColor;
      private int _newBlendMode;
      private float _newMetallic;
      private float _newSmoothness;

      // Предыдущие свойства для отмены
      private Color _previousColor;
      private int _previousBlendMode;
      private float _previousMetallic;
      private float _previousSmoothness;

      public PaintCommand(PaintManager paintManager, int classId, Color newColor, int newBlendMode, float newMetallic = 0f, float newSmoothness = 0.5f)
      {
            _paintManager = paintManager;
            _classId = classId;
            _newColor = newColor;
            _newBlendMode = newBlendMode;
            _newMetallic = newMetallic;
            _newSmoothness = newSmoothness;
      }

      public void Execute()
      {
            // Перед выполнением сохраняем текущие (старые) свойства класса
            (_previousColor, _previousBlendMode, _previousMetallic, _previousSmoothness) = _paintManager.GetPaintPropertiesForClass(_classId);

            // Устанавливаем новые свойства
            _paintManager.SetPaintPropertiesForClass(_classId, _newColor, _newBlendMode, _newMetallic, _newSmoothness);

            Debug.Log($"🎨 Выполнена команда: Покрашен класс {_classId} цветом {_newColor}");
      }

      public void Undo()
      {
            // Восстанавливаем предыдущие свойства
            _paintManager.SetPaintPropertiesForClass(_classId, _previousColor, _previousBlendMode, _previousMetallic, _previousSmoothness);

            Debug.Log($"⏪ Отменена команда: Восстановлены свойства для класса {_classId}");
      }
}


/// <summary>
/// Команда для полной очистки всех окрашенных поверхностей.
/// </summary>
public class ClearAllPaintCommand : ICommand
{
      private PaintManager _paintManager;

      // Хранилище для состояний всех классов перед очисткой
      private Dictionary<int, (Color color, int blendMode, float metallic, float smoothness)> _previousStates;

      public ClearAllPaintCommand(PaintManager paintManager)
      {
            _paintManager = paintManager;
            _previousStates = new Dictionary<int, (Color, int, float, float)>();
      }

      public void Execute()
      {
            _previousStates.Clear();
            // Сохраняем состояние каждого класса, который был окрашен
            for (int i = 0; i < 32; i++) // Предполагаем MaxClasses = 32
            {
                  var props = _paintManager.GetPaintPropertiesForClass(i);
                  if (props.color.a > 0) // Сохраняем только если класс был окрашен
                  {
                        _previousStates[i] = props;
                  }
            }

            _paintManager.ClearAllPaint();
            Debug.Log("🗑️ Выполнена команда: Вся покраска очищена.");
      }

      public void Undo()
      {
            // Восстанавливаем все сохраненные состояния
            foreach (var state in _previousStates)
            {
                  _paintManager.SetPaintPropertiesForClass(state.Key, state.Value.color, state.Value.blendMode, state.Value.metallic, state.Value.smoothness);
            }
            Debug.Log("⏪ Отменена команда: Восстановлена предыдущая покраска.");
      }
}

/// <summary>
/// Менеджер, управляющий стеками команд для Undo/Redo.
/// </summary>
public class CommandManager : MonoBehaviour
{
      [Header("History Settings")]
      [SerializeField] private int maxHistorySize = 20;

      private Stack<ICommand> undoStack = new Stack<ICommand>();
      private Stack<ICommand> redoStack = new Stack<ICommand>();

      // События для обновления UI
      public System.Action<bool> OnUndoAvailabilityChanged;
      public System.Action<bool> OnRedoAvailabilityChanged;

      public bool CanUndo => undoStack.Count > 0;
      public bool CanRedo => redoStack.Count > 0;

      void Start()
      {
            // Инициализация событий UI
            UpdateUIEvents();
      }

      /// <summary>
      /// Выполняет команду и добавляет её в историю
      /// </summary>
      public void ExecuteCommand(ICommand command)
      {
            // Выполняем команду
            command.Execute();

            // Добавляем в стек отмены
            undoStack.Push(command);

            // Очищаем стек повтора, так как выполнена новая команда
            redoStack.Clear();

            // Ограничиваем размер истории
            if (undoStack.Count > maxHistorySize)
            {
                  var tempStack = new Stack<ICommand>();
                  for (int i = 0; i < maxHistorySize; i++)
                  {
                        tempStack.Push(undoStack.Pop());
                  }
                  undoStack.Clear();
                  while (tempStack.Count > 0)
                  {
                        undoStack.Push(tempStack.Pop());
                  }
            }

            UpdateUIEvents();
      }

      /// <summary>
      /// Отменяет последнее действие
      /// </summary>
      public void Undo()
      {
            if (!CanUndo)
            {
                  Debug.LogWarning("⚠️ Нет команд для отмены");
                  return;
            }

            var command = undoStack.Pop();
            command.Undo();
            redoStack.Push(command);

            UpdateUIEvents();
      }

      /// <summary>
      /// Повторяет отмененное действие
      /// </summary>
      public void Redo()
      {
            if (!CanRedo)
            {
                  Debug.LogWarning("⚠️ Нет команд для повтора");
                  return;
            }

            var command = redoStack.Pop();
            command.Execute();
            undoStack.Push(command);

            UpdateUIEvents();
      }

      /// <summary>
      /// Очищает всю историю команд
      /// </summary>
      public void ClearHistory()
      {
            undoStack.Clear();
            redoStack.Clear();
            UpdateUIEvents();
            Debug.Log("🗑️ История команд очищена");
      }

      void UpdateUIEvents()
      {
            OnUndoAvailabilityChanged?.Invoke(CanUndo);
            OnRedoAvailabilityChanged?.Invoke(CanRedo);
      }

      /// <summary>
      /// Возвращает информацию о последней команде в стеке отмены
      /// </summary>
      public string GetLastUndoDescription()
      {
            return CanUndo ? undoStack.Peek().GetType().Name : "Нет команд";
      }

      /// <summary>
      /// Возвращает информацию о последней команде в стеке повтора
      /// </summary>
      public string GetLastRedoDescription()
      {
            return CanRedo ? redoStack.Peek().GetType().Name : "Нет команд";
      }
}
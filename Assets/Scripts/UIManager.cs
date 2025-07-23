using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Центральный менеджер пользовательского интерфейса
/// </summary>
public class UIManager : MonoBehaviour
{
      [Header("Dependencies")]
      public CommandManager commandManager;
      public PaintManager paintManager;

      [Header("Command Buttons")]
      public Button undoButton;
      public Button redoButton;
      public Button clearAllButton;

      [Header("PBR Sliders")]
      public Slider metallicSlider;
      public Slider smoothnessSlider;

      [Header("Recording UI")]
      public Button startRecordingButton;
      public Button stopRecordingButton;
      public GameObject recordingIndicator;

      [Header("Blend Mode")]
      public TMPro.TMP_Dropdown blendModeDropdown;

      /* // ВРЕМЕННО ОТКЛЮЧЕНО
      [Header("Dynamic Palettes")]
      public List<string> paletteAddresses = new List<string>();
      public GameObject colorButtonPrefab;
      public Transform paletteContainer;
      */

      private Color currentColor;
      private int currentBlendMode;
      private float currentMetallic = 0.1f;
      private float currentSmoothness = 0.8f;

      void Start()
      {
            if (commandManager != null)
            {
                  undoButton.onClick.AddListener(OnUndoClicked);
                  redoButton.onClick.AddListener(OnRedoClicked);
                  clearAllButton.onClick.AddListener(OnClearAllClicked);
            }

            if (metallicSlider != null)
            {
                  metallicSlider.onValueChanged.AddListener(OnMetallicChanged);
                  currentMetallic = metallicSlider.value;
            }
            if (smoothnessSlider != null)
            {
                  smoothnessSlider.onValueChanged.AddListener(OnSmoothnessChanged);
                  currentSmoothness = smoothnessSlider.value;
            }

            if (startRecordingButton != null)
            {
                  startRecordingButton.onClick.AddListener(OnStartRecordingClicked);
            }
            if (stopRecordingButton != null)
            {
                  stopRecordingButton.onClick.AddListener(OnStopRecordingClicked);
                  stopRecordingButton.gameObject.SetActive(false);
            }
            if (recordingIndicator != null)
            {
                  recordingIndicator.SetActive(false);
            }

            setupBlendModeDropdown();
            SetDefaultPaintColor();
      }

      public void OnColorButtonClicked(Color color)
      {
            SetPaintColor(color);
      }

      public void SetPaintColor(Color color)
      {
            currentColor = color;
      }

      public Color GetCurrentColor()
      {
            return currentColor;
      }

      public int GetCurrentBlendMode()
      {
            return currentBlendMode;
      }

      public float GetCurrentMetallic()
      {
            return currentMetallic;
      }

      public float GetCurrentSmoothness()
      {
            return currentSmoothness;
      }

      private void OnMetallicChanged(float value)
      {
            currentMetallic = value;
      }

      private void OnSmoothnessChanged(float value)
      {
            currentSmoothness = value;
      }

      public void OnUndoClicked()
      {
            if (commandManager == null) return;
            commandManager.Undo();
      }

      public void OnRedoClicked()
      {
            if (commandManager == null) return;
            commandManager.Redo();
      }

      public void OnClearAllClicked()
      {
            if (commandManager == null || paintManager == null) return;
            var command = new ClearAllPaintCommand(paintManager);
            commandManager.ExecuteCommand(command);
      }

      private void OnStartRecordingClicked()
      {
            /* // ВРЕМЕННО ОТКЛЮЧЕНО
            var recorder = FindObjectOfType<VideoRecordingManager>();
            if (recorder != null)
            {
                recorder.StartRecording();
                startRecordingButton.gameObject.SetActive(false);
                stopRecordingButton.gameObject.SetActive(true);
                if (recordingIndicator != null) recordingIndicator.SetActive(true);
            }
            */
            Debug.Log("Start Recording Clicked (Logic Disabled)");
      }

      private void OnStopRecordingClicked()
      {
            /* // ВРЕМЕННО ОТКЛЮЧЕНО
            var recorder = FindObjectOfType<VideoRecordingManager>();
            if (recorder != null)
            {
                recorder.StopRecording();
                startRecordingButton.gameObject.SetActive(true);
                stopRecordingButton.gameObject.SetActive(false);
                if (recordingIndicator != null) recordingIndicator.SetActive(false);
            }
            */
            Debug.Log("Stop Recording Clicked (Logic Disabled)");
      }

      private void SetDefaultPaintColor()
      {
            SetPaintColor(Color.red);
      }

      private void setupBlendModeDropdown()
      {
            if (blendModeDropdown == null) return;

            blendModeDropdown.ClearOptions();
            blendModeDropdown.AddOptions(new List<string> { "Overlay", "Multiply", "Soft Light" });
            blendModeDropdown.onValueChanged.AddListener(OnBlendModeChanged);
            currentBlendMode = 0;
      }

      private void OnBlendModeChanged(int index)
      {
            currentBlendMode = index;
      }
}
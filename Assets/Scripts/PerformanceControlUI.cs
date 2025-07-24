using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PerformanceControlUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button showWallsAndCeilingButton;
    [SerializeField] private Button showFurnishingButton;
    [SerializeField] private Button showAllButton;
    [SerializeField] private Slider maxProcessingTimeSlider;
    [SerializeField] private TextMeshProUGUI maxTimeLabel;
    [SerializeField] private Toggle autoOptimizationToggle;

    [Header("Quick Settings")]
    [SerializeField] private Button ultraFastButton;
    [SerializeField] private Button balancedButton;
    [SerializeField] private Button qualityButton;

    private SegmentationManager segmentationManager;

    private void Start()
    {
        segmentationManager = FindFirstObjectByType<SegmentationManager>();

        if (segmentationManager == null)
        {
            Debug.LogError("SegmentationManager not found! PerformanceControlUI will not work.");
            gameObject.SetActive(false);
            return;
        }

        SetupUI();
    }

    private void SetupUI()
    {
        // Setup buttons for Sentis 2.0 SegmentationManager
        if (showWallsAndCeilingButton != null)
        {
            showWallsAndCeilingButton.onClick.AddListener(SelectWallsAndCeiling);
        }

        if (showFurnishingButton != null)
        {
            showFurnishingButton.onClick.AddListener(SelectFurnishings);
        }

        if (showAllButton != null)
        {
            showAllButton.onClick.AddListener(SelectAllClasses);
        }

        // Setup slider
        if (maxProcessingTimeSlider != null)
        {
            maxProcessingTimeSlider.minValue = 50f;
            maxProcessingTimeSlider.maxValue = 500f;
            maxProcessingTimeSlider.value = 200f; // Default
            maxProcessingTimeSlider.onValueChanged.AddListener(OnMaxProcessingTimeChanged);
            UpdateMaxTimeLabel();
        }

        // Setup toggle
        if (autoOptimizationToggle != null)
        {
            autoOptimizationToggle.isOn = true; // Default enabled
            autoOptimizationToggle.onValueChanged.AddListener(OnAutoOptimizationChanged);
        }

        // Setup quick setting buttons
        if (ultraFastButton != null)
        {
            ultraFastButton.onClick.AddListener(ApplyUltraFastSettings);
        }

        if (balancedButton != null)
        {
            balancedButton.onClick.AddListener(ApplyBalancedSettings);
        }

        if (qualityButton != null)
        {
            qualityButton.onClick.AddListener(ApplyQualitySettings);
        }
    }

    private void SelectWallsAndCeiling()
    {
        segmentationManager.showAllClasses = false;
        segmentationManager.showWall = true;
        segmentationManager.showCeiling = true;
        segmentationManager.showFloor = true;
        segmentationManager.showChair = false;
        segmentationManager.showTable = false;
        segmentationManager.showDoor = false;
        Debug.Log("UI: Selected Walls, Ceiling, and Floor.");
    }

    private void SelectFurnishings()
    {
        segmentationManager.showAllClasses = false;
        segmentationManager.showWall = false;
        segmentationManager.showCeiling = false;
        segmentationManager.showFloor = false;
        segmentationManager.showChair = true;
        segmentationManager.showTable = true;
        segmentationManager.showDoor = true;
        Debug.Log("UI: Selected Furnishings (Chair, Table, Door).");
    }

    private void SelectAllClasses()
    {
        segmentationManager.showAllClasses = true;
        Debug.Log("UI: Selected All Classes.");
    }


    private void OnMaxProcessingTimeChanged(float value)
    {
        Debug.Log($"⚙️ Processing time setting: {value}ms (Sentis 2.0 auto-optimizes)");
        UpdateMaxTimeLabel();
    }

    private void OnAutoOptimizationChanged(bool enabled)
    {
        Debug.Log($"🔧 Auto optimization: {(enabled ? "Enabled" : "Disabled")} (Built into Sentis 2.0)");
    }

    private void UpdateMaxTimeLabel()
    {
        if (maxTimeLabel != null && maxProcessingTimeSlider != null)
        {
            maxTimeLabel.text = $"Max Processing: {maxProcessingTimeSlider.value:F0}ms";
        }
    }

    // Quick setting presets
    private void ApplyUltraFastSettings()
    {
        Debug.Log("Applying ULTRA FAST settings");

        SelectWallsAndCeiling(); // Show structural elements for speed

        if (maxProcessingTimeSlider != null)
        {
            maxProcessingTimeSlider.value = 100f;
            OnMaxProcessingTimeChanged(100f);
        }

        if (autoOptimizationToggle != null)
        {
            autoOptimizationToggle.isOn = true;
            OnAutoOptimizationChanged(true);
        }

        Debug.Log("✅ Ultra Fast mode applied: Structural elements, 100ms max processing, Auto-optimization ON");
    }

    private void ApplyBalancedSettings()
    {
        Debug.Log("Applying BALANCED settings");

        SelectAllClasses(); // Show everything

        if (maxProcessingTimeSlider != null)
        {
            maxProcessingTimeSlider.value = 200f;
            OnMaxProcessingTimeChanged(200f);
        }

        if (autoOptimizationToggle != null)
        {
            autoOptimizationToggle.isOn = true;
            OnAutoOptimizationChanged(true);
        }

        Debug.Log("✅ Balanced mode applied: All classes, 200ms max processing, Auto-optimization ON");
    }

    private void ApplyQualitySettings()
    {
        Debug.Log("Applying QUALITY settings");

        SelectAllClasses(); // Show everything

        if (maxProcessingTimeSlider != null)
        {
            maxProcessingTimeSlider.value = 400f;
            OnMaxProcessingTimeChanged(400f);
        }

        if (autoOptimizationToggle != null)
        {
            autoOptimizationToggle.isOn = false;
            OnAutoOptimizationChanged(false);
        }

        Debug.Log("✅ Quality mode applied: All classes, 400ms max processing, Auto-optimization OFF");
    }

    // Public methods for external control
    public void SetMaxProcessingTime(float timeMs)
    {
        if (maxProcessingTimeSlider != null)
        {
            maxProcessingTimeSlider.value = timeMs;
            OnMaxProcessingTimeChanged(timeMs);
        }
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PerformanceControlUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button showWallsAndCeilingButton;
    [SerializeField] private Button showFurnishingButton;
    [SerializeField] private Button showAllButton;
    [SerializeField] private Slider maxProcessingTimeSlider;
    [SerializeField] private TextMeshProUGUI maxTimeLabel;
    [SerializeField] private Toggle autoOptimizationToggle;

    [Header("Performance Controls")]
    [SerializeField] private Slider frameSkipSlider;
    [SerializeField] private TextMeshProUGUI frameSkipLabel;
    [SerializeField] private Slider minFrameIntervalSlider;
    [SerializeField] private TextMeshProUGUI minFrameIntervalLabel;
    [SerializeField] private Toggle gpuProcessingToggle;
    [SerializeField] private TextMeshProUGUI gpuProcessingLabel;

    [Header("Quick Settings")]
    [SerializeField] private Button ultraFastButton;
    [SerializeField] private Button balancedButton;
    [SerializeField] private Button qualityButton;

    [SerializeField] private TMP_Text resolutionText;
    [SerializeField] private TMP_Text fpsText;

    // Ссылка на наш новый менеджер
    private AsyncSegmentationManager segmentationManager;

    private float fpsUpdateTimer = 0.5f;
    private float lastFpsUpdateTime;

    private void Start()
    {
        // Ищем новый AsyncSegmentationManager
        segmentationManager = FindObjectOfType<AsyncSegmentationManager>();
        if (segmentationManager == null)
        {
            Debug.LogError("AsyncSegmentationManager not found! PerformanceControlUI will not work.");
            gameObject.SetActive(false);
            return;
        }

        SetupPerformanceControls();
        SetupQuickSettingsButtons();
        UpdateUI();
    }

    void Update()
    {
        if (Time.unscaledTime - lastFpsUpdateTime > fpsUpdateTimer)
        {
            UpdateFps();
            lastFpsUpdateTime = Time.unscaledTime;
        }
    }

    private void SetupPerformanceControls()
    {
        // Настройка слайдера пропуска кадров
        if (frameSkipSlider != null)
        {
            frameSkipSlider.minValue = 1;
            frameSkipSlider.maxValue = 10;
            frameSkipSlider.wholeNumbers = true;
            frameSkipSlider.onValueChanged.AddListener(OnFrameSkipChanged);
        }

        // Настройка слайдера минимального интервала кадров
        if (minFrameIntervalSlider != null)
        {
            minFrameIntervalSlider.minValue = 0.016f; // ~60 FPS
            minFrameIntervalSlider.maxValue = 0.1f;   // ~10 FPS
            minFrameIntervalSlider.onValueChanged.AddListener(OnMinFrameIntervalChanged);
        }

        // Настройка переключателя GPU обработки
        if (gpuProcessingToggle != null)
        {
            gpuProcessingToggle.onValueChanged.AddListener(OnGpuProcessingToggleChanged);
        }
    }

    private void SetupQuickSettingsButtons()
    {
        if (ultraFastButton != null)
            ultraFastButton.onClick.AddListener(() => SetQuickPreset(PerformancePreset.UltraFast));

        if (balancedButton != null)
            balancedButton.onClick.AddListener(() => SetQuickPreset(PerformancePreset.Balanced));

        if (qualityButton != null)
            qualityButton.onClick.AddListener(() => SetQuickPreset(PerformancePreset.Quality));
    }

    private void OnFrameSkipChanged(float value)
    {
        int frameSkip = Mathf.RoundToInt(value);
        SetFrameSkipRate(frameSkip);
        UpdateFrameSkipLabel(frameSkip);
    }

    private void OnMinFrameIntervalChanged(float value)
    {
        SetMinFrameInterval(value);
        UpdateMinFrameIntervalLabel(value);
    }

    private void OnGpuProcessingToggleChanged(bool useGpu)
    {
        SetGpuProcessing(!useGpu); // Инвертируем, так как в AsyncSegmentationManager useCpuArgmax
        UpdateGpuProcessingLabel(!useGpu);
    }

    private void UpdateFrameSkipLabel(int frameSkip)
    {
        if (frameSkipLabel != null)
        {
            frameSkipLabel.text = $"Пропуск кадров: каждый {frameSkip}-й";
        }
    }

    private void UpdateMinFrameIntervalLabel(float interval)
    {
        if (minFrameIntervalLabel != null)
        {
            float fps = 1.0f / interval;
            minFrameIntervalLabel.text = $"Макс. FPS: {fps:F0}";
        }
    }

    private void UpdateGpuProcessingLabel(bool useGpu)
    {
        if (gpuProcessingLabel != null)
        {
            gpuProcessingLabel.text = useGpu ? "GPU обработка" : "CPU обработка";
        }
    }

    private void UpdateFps()
    {
        if (fpsText != null)
        {
            float fps = 1.0f / Time.unscaledDeltaTime;
            fpsText.text = $"FPS: {fps:F1}";
        }
    }

    private void UpdateUI()
    {
        // Обновляем UI элементы текущими значениями из AsyncSegmentationManager
        if (frameSkipSlider != null)
        {
            int currentFrameSkip = GetFrameSkipRate();
            frameSkipSlider.value = currentFrameSkip;
            UpdateFrameSkipLabel(currentFrameSkip);
        }

        if (minFrameIntervalSlider != null)
        {
            float currentInterval = GetMinFrameInterval();
            minFrameIntervalSlider.value = currentInterval;
            UpdateMinFrameIntervalLabel(currentInterval);
        }

        if (gpuProcessingToggle != null)
        {
            bool useGpu = !GetUseCpuArgmax();
            gpuProcessingToggle.isOn = useGpu;
            UpdateGpuProcessingLabel(useGpu);
        }
    }

    public enum PerformancePreset
    {
        UltraFast,
        Balanced,
        Quality
    }

    public void SetQuickPreset(PerformancePreset preset)
    {
        switch (preset)
        {
            case PerformancePreset.UltraFast:
                SetFrameSkipRate(5);
                SetMinFrameInterval(0.1f); // 10 FPS
                SetGpuProcessing(true);
                Debug.Log("Применен пресет: Ультра быстро");
                break;

            case PerformancePreset.Balanced:
                SetFrameSkipRate(2);
                SetMinFrameInterval(0.033f); // 30 FPS
                SetGpuProcessing(true);
                Debug.Log("Применен пресет: Сбалансированно");
                break;

            case PerformancePreset.Quality:
                SetFrameSkipRate(1);
                SetMinFrameInterval(0.016f); // 60 FPS
                SetGpuProcessing(true);
                Debug.Log("Применен пресет: Качество");
                break;
        }

        UpdateUI();
    }

    // Методы для взаимодействия с AsyncSegmentationManager через прямой доступ
    private void SetFrameSkipRate(int value)
    {
        if (segmentationManager != null)
            segmentationManager.frameSkipRate = value;
    }

    private int GetFrameSkipRate()
    {
        return segmentationManager != null ? segmentationManager.frameSkipRate : 2;
    }

    private void SetMinFrameInterval(float value)
    {
        if (segmentationManager != null)
            segmentationManager.minFrameInterval = value;
    }

    private float GetMinFrameInterval()
    {
        return segmentationManager != null ? segmentationManager.minFrameInterval : 0.033f;
    }

    private void SetGpuProcessing(bool useGpu)
    {
        if (segmentationManager != null)
            segmentationManager.UseCpuArgmax = !useGpu; // Инвертируем
    }

    private bool GetUseCpuArgmax()
    {
        return segmentationManager != null ? segmentationManager.UseCpuArgmax : false;
    }
}
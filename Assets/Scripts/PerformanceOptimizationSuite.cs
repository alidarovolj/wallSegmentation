using UnityEngine;

// Создать GameObject "PerformanceOptimizationSuite" и добавить этот скрипт
public class PerformanceOptimizationSuite : MonoBehaviour
{
    [Header("Optimization Components")]
    public QuickDiagnostics diagnostics;
    public InstantPerformanceMonitor monitor;

    [Header("Info")]
    [TextArea(3, 5)]
    public string info = "CriticalPerformanceFix удален - GPU backend уже встроен в SegmentationManager с Sentis 2.0";

    [ContextMenu("Initialize Full Suite")]
    void InitializeOptimizationSuite()
    {
        if (GetComponent<QuickDiagnostics>() == null)
            gameObject.AddComponent<QuickDiagnostics>();
        if (GetComponent<InstantPerformanceMonitor>() == null)
            gameObject.AddComponent<InstantPerformanceMonitor>();

        diagnostics = GetComponent<QuickDiagnostics>();
        monitor = GetComponent<InstantPerformanceMonitor>();

        Debug.Log("🎯 Performance Optimization Suite initialized!");
        Debug.Log("✅ GPU backend уже встроен в SegmentationManager (Sentis 2.0)");
        Debug.Log("Press F1 for real-time monitor, F2 for diagnostics");
    }

    void Start()
    {
        // Automatically initialize if components are not assigned
        if (diagnostics == null || monitor == null)
        {
            InitializeOptimizationSuite();
        }
        Debug.Log("🚀 remaluxAR Performance Optimization Suite ready!");
        Debug.Log("🔧 Run 'Initialize Full Suite' from context menu to begin");
    }
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AlexMalyutinDev.RadianceCascades;

/// <summary>
/// Debug tool for visualizing and verifying adaptive ray scaling in Radiance Cascades
/// </summary>
[RequireComponent(typeof(Camera))]
public class RadianceCascadesDebug : MonoBehaviour
{
    [Header("Debug Visualization")]
    [Tooltip("Enable debug visualization overlay")]
    public bool showDebugOverlay = true;
    
    [Tooltip("Display ray scale values as colors")]
    public bool visualizeRayScale = false;
    
    [Tooltip("Log ray scale values to console")]
    public bool logToConsole = false;
    
    [Header("Comparison Mode")]
    [Tooltip("Compare adaptive vs fixed ray scaling")]
    public bool compareMode = false;
    
    [Tooltip("Fixed ray scale for comparison (when compareMode is enabled)")]
    [Range(0.01f, 2.0f)]
    public float fixedRayScale = 0.1f;

    private Camera _camera;
    private RadianceCascades _radianceCascadesSettings;
    private bool _lastAdaptiveState;
    private float _lastCascadeScaleFactor;
    private float _lastVarianceInfluence;

    private void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Debug.LogError("RadianceCascadesDebug: Camera component not found!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        // Get RadianceCascades volume settings
        var volumeStack = VolumeManager.instance.stack;
        _radianceCascadesSettings = volumeStack.GetComponent<RadianceCascades>();

        if (_radianceCascadesSettings == null)
        {
            if (logToConsole && Time.frameCount % 60 == 0) // Log once per second
            {
                Debug.LogWarning("RadianceCascadesDebug: RadianceCascades volume component not found!");
            }
            return;
        }

        // Check for parameter changes
        bool adaptiveEnabled = _radianceCascadesSettings.EnableAdaptiveRayScale.value;
        float cascadeFactor = _radianceCascadesSettings.CascadeScaleFactor.value;
        float varianceInfluence = _radianceCascadesSettings.VarianceInfluence.value;

        if (logToConsole)
        {
            if (adaptiveEnabled != _lastAdaptiveState || 
                Mathf.Abs(cascadeFactor - _lastCascadeScaleFactor) > 0.01f ||
                Mathf.Abs(varianceInfluence - _lastVarianceInfluence) > 0.01f)
            {
                LogCurrentSettings();
                _lastAdaptiveState = adaptiveEnabled;
                _lastCascadeScaleFactor = cascadeFactor;
                _lastVarianceInfluence = varianceInfluence;
            }
        }
    }

    private void LogCurrentSettings()
    {
        if (_radianceCascadesSettings == null) return;

        Debug.Log($"[RadianceCascades Debug] " +
                  $"Adaptive Ray Scale: {_radianceCascadesSettings.EnableAdaptiveRayScale.value}, " +
                  $"Cascade Scale Factor: {_radianceCascadesSettings.CascadeScaleFactor.value:F2}, " +
                  $"Variance Influence: {_radianceCascadesSettings.VarianceInfluence.value:F2}, " +
                  $"Ray Scale: {_radianceCascadesSettings.RayScale.value:F3}");
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || _radianceCascadesSettings == null) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;

        Rect rect = new Rect(10, 10, 400, 200);
        GUI.Box(rect, "");
        
        string info = "Radiance Cascades Debug\n" +
                     "======================\n" +
                     $"Rendering Type: {_radianceCascadesSettings.RenderingType.value}\n" +
                     $"Ray Scale: {_radianceCascadesSettings.RayScale.value:F3}\n" +
                     $"Adaptive Ray Scale: {(_radianceCascadesSettings.EnableAdaptiveRayScale.value ? "ENABLED" : "DISABLED")}\n";

        if (_radianceCascadesSettings.EnableAdaptiveRayScale.value)
        {
            info += $"Cascade Scale Factor: {_radianceCascadesSettings.CascadeScaleFactor.value:F2}\n" +
                   $"Variance Influence: {_radianceCascadesSettings.VarianceInfluence.value:F2}\n";
        }

        GUI.Label(rect, info, style);

        // Instructions
        Rect instructionRect = new Rect(10, 220, 400, 100);
        GUI.Box(instructionRect, "");
        string instructions = "Instructions:\n" +
                             "1. Toggle 'Enable Adaptive Ray Scale' in Volume Profile\n" +
                             "2. Adjust Cascade Scale Factor (0.5-3.0)\n" +
                             "3. Adjust Variance Influence (0.0-1.0)\n" +
                             "4. Check Frame Debugger for RC passes";
        GUI.Label(instructionRect, instructions, style);
    }

    /// <summary>
    /// Call this method to verify adaptive ray scaling is working
    /// </summary>
    [ContextMenu("Verify Adaptive Ray Scaling")]
    public void VerifyAdaptiveRayScaling()
    {
        if (_radianceCascadesSettings == null)
        {
            Debug.LogError("RadianceCascadesDebug: RadianceCascades settings not found!");
            return;
        }

        Debug.Log("=== Radiance Cascades Adaptive Ray Scaling Verification ===");
        Debug.Log($"Current Settings:");
        Debug.Log($"  - Adaptive Ray Scale: {_radianceCascadesSettings.EnableAdaptiveRayScale.value}");
        Debug.Log($"  - Base Ray Scale: {_radianceCascadesSettings.RayScale.value}");
        Debug.Log($"  - Cascade Scale Factor: {_radianceCascadesSettings.CascadeScaleFactor.value}");
        Debug.Log($"  - Variance Influence: {_radianceCascadesSettings.VarianceInfluence.value}");
        Debug.Log($"");
        Debug.Log($"Verification Steps:");
        Debug.Log($"1. Open Frame Debugger (Window > Analysis > Frame Debugger)");
        Debug.Log($"2. Look for RC passes: RC.MinMaxDepth, RC.VarianceDepth, RC.BlurredColor, RC.Render, RC.Combine");
        Debug.Log($"3. Toggle 'Enable Adaptive Ray Scale' and observe visual differences");
        Debug.Log($"4. Vary Cascade Scale Factor and Variance Influence to see changes");
        Debug.Log($"");
        Debug.Log($"Expected Behavior:");
        Debug.Log($"  - Higher cascade levels should have longer rays (coarser = longer)");
        Debug.Log($"  - High variance areas should have shorter rays");
        Debug.Log($"  - Visual quality should improve with adaptive scaling enabled");
    }
}


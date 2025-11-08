using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    // [MODIFIED] Enum for Off-Screen Fallback (Phase 1.3)
    public enum OffScreenFallbackMode
    {
        None,
        Ambient,
        EnvironmentCube
    }

    [VolumeComponentMenu(nameof(AlexMalyutinDev) + "/" + nameof(AlexMalyutinDev.RadianceCascades))]
    public sealed class RadianceCascades : VolumeComponent
    {
        [Header("Rendering Type")]
        public VolumeParameter<RenderingType> RenderingType = new();

        [Header("Direction First Settings")]
        public ClampedFloatParameter RayScale = new ClampedFloatParameter(0.1f, 0.01f, 2.0f);
        public BoolParameter UseSH = new BoolParameter(false, BoolParameter.DisplayType.EnumPopup);

        [Header("Quality Settings")]
        [Tooltip("Enable high precision radiance (RGBA16F vs RGBAHalf)")]
        public BoolParameter HighPrecisionRadiance = new BoolParameter(true);
        
        [Tooltip("Enable performance profiling")]
        public BoolParameter EnableProfiling = new BoolParameter(false);

        [Header("Adaptive Ray Scale")]
        [Tooltip("Enable adaptive ray scale based on cascade level and scene variance")]
        public BoolParameter EnableAdaptiveRayScale = new BoolParameter(true);
        [Tooltip("Cascade scaling factor (coarser cascades = longer rays)")]
        public ClampedFloatParameter CascadeScaleFactor = new ClampedFloatParameter(1.5f, 0.5f, 3.0f);
        [Tooltip("Variance influence on ray scale (high variance = shorter rays)")]
        public ClampedFloatParameter VarianceInfluence = new ClampedFloatParameter(0.3f, 0.0f, 1.0f);

        // [MODIFIED] Phase 1.3: Off-Screen Fallback (removed Extended Screen-Space)
        [Header("Off-Screen Fallback")]
        [Tooltip("What to use for light rays that travel off-screen. 'None' is fastest (original behavior).")]
        public VolumeParameter<OffScreenFallbackMode> OffScreenFallbackMode = new VolumeParameter<OffScreenFallbackMode>();
        
        // [NEW] Phase 3.1: Environment CubeMap Settings
        [Header("Phase 3: Environment CubeMap")]
        [Tooltip("Optional environment map (Cubemap) to use for off-screen fallback lighting.")]
        public CubemapParameter EnvironmentCubeMap = new CubemapParameter(null);
        [Tooltip("Intensity multiplier for the Environment CubeMap.")]
        public ClampedFloatParameter EnvironmentIntensity = new ClampedFloatParameter(1.0f, 0.0f, 2.0f);
        [Tooltip("How much the environment fallback should blend with the screen-space result (for smooth transitions).")]
        public ClampedFloatParameter EnvironmentFallbackWeight = new ClampedFloatParameter(0.3f, 0.0f, 1.0f);

        // [NEW] Phase 4.1: Adaptive Sampling Density Settings
        [Header("Phase 4: Adaptive Sampling Density")]
        [Tooltip("Enables variance-based ray *step count* adjustment (complements Adaptive Ray Scale, which affects ray *length*).")]
        public BoolParameter EnableAdaptiveSamplingDensity = new BoolParameter(false);
        [Tooltip("Minimum number of ray steps to take.")]
        public ClampedIntParameter MinRaySteps = new ClampedIntParameter(4, 1, 8);
        [Tooltip("Maximum number of ray steps to take.")]
        public ClampedIntParameter MaxRaySteps = new ClampedIntParameter(32, 8, 64);
        [Tooltip("How much scene variance influences the *step count*. High variance = more steps.")]
        public ClampedFloatParameter VarianceSamplingInfluence = new ClampedFloatParameter(0.5f, 0.0f, 1.0f);

        [Header("Debug Settings")]
        [Tooltip("Debug slice for 3D texture visualization (0-1)")]
        public ClampedFloatParameter DebugSlice = new ClampedFloatParameter(0.5f, 0f, 1f);
        
        [Tooltip("Show memory usage in console")]
        public BoolParameter ShowMemoryUsage = new BoolParameter(false);
    }
}

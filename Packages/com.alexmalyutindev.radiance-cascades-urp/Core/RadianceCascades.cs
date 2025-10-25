using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    [VolumeComponentMenu(nameof(AlexMalyutinDev) + "/" + nameof(AlexMalyutinDev.RadianceCascades))]
    public sealed class RadianceCascades : VolumeComponent
    {
        [Header("Rendering Type")]
        public VolumeParameter<RenderingType> RenderingType = new();

        [Header("Direction First Settings")]
        public ClampedFloatParameter RayScale = new ClampedFloatParameter(0.1f, 0.01f, 2.0f);
        public BoolParameter UseSH = new BoolParameter(false, BoolParameter.DisplayType.EnumPopup);

        [Header("RC 3D Probes Settings")]
        [Tooltip("Volume resolution: 64³ → 96³ → 128³ sweep")]
        public IntParameter VolumeResolution = new IntParameter(64);
        
        [Tooltip("Cascade count: 3 → 4 sweep")]
        public ClampedIntParameter CascadeCount = new ClampedIntParameter(3, 1, 4);
        
        [Tooltip("Enable β-aware propagation")]
        public BoolParameter EnableBetaPropagation = new BoolParameter(true);
        
        [Tooltip("Propagation radius for separable blur")]
        public ClampedIntParameter PropagationRadius = new ClampedIntParameter(1, 1, 3);
        
        [Tooltip("Enable front-to-back merging")]
        public BoolParameter EnableFrontToBackMerge = new BoolParameter(true);

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
        [Tooltip("Depth range influence on ray scale (larger scenes = longer rays)")]
        public ClampedFloatParameter DepthRangeInfluence = new ClampedFloatParameter(0.5f, 0.0f, 1.0f);

        [Header("Debug Settings")]
        [Tooltip("Debug slice for 3D texture visualization (0-1)")]
        public ClampedFloatParameter DebugSlice = new ClampedFloatParameter(0.5f, 0f, 1f);
        
        [Tooltip("Show memory usage in console")]
        public BoolParameter ShowMemoryUsage = new BoolParameter(false);
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    [VolumeComponentMenu(nameof(AlexMalyutinDev) + "/" + nameof(AlexMalyutinDev.RadianceCascades))]
    public sealed class RadianceCascades : VolumeComponent
    {
        public VolumeParameter<RenderingType> RenderingType = new();

        [Header("Direction First")]
        public VolumeParameter<UpscaleMode> UpscaleMode = new VolumeParameter<UpscaleMode>();
        
        [Header("Bilateral Settings")]
        public ClampedFloatParameter UpsampleTolerance = new ClampedFloatParameter(1e-5f, 1e-12f, 1.0f);
        public ClampedFloatParameter NoiseFilterStrength = new ClampedFloatParameter(0.99999f, 0.0f, 0.9999999f);
    }
}

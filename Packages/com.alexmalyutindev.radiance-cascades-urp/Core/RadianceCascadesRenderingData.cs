using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadesRenderingData
    {
        public RTHandle SceneVolume;
        public RTHandle MinMaxDepth;
        public Matrix4x4 WorldToVolume;

        public RTHandle SmoothedDepth;
        public RTHandle BlurredColorBuffer;
    }
}

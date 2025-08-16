using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadesRenderingData
    {
        public Vector2Int Cascade0Size;

        public RTHandle SceneVolume;
        public Matrix4x4 WorldToVolume;
        public RTHandle SmoothedDepth;
    }
}

using UnityEngine;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadeResources : ScriptableObject
    {
        public Material BlitMaterial;
        public ComputeShader RadianceCascades;
        public ComputeShader RadianceCascades3d;
        public ComputeShader RadianceCascadesDirectionalFirstCS;

        [Space]
        public Shader Voxelizator;
        public ComputeShader VoxelizatorCS;

        [Space]
        public Material HiZDepthMaterial;
        public Material MinMaxDepthMaterial;
        public Material SmoothedDepthMaterial;
    }
}

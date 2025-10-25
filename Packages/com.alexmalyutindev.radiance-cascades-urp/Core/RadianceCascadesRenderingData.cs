using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadesRenderingData
    {
        // Legacy fields
        public Vector2Int Cascade0Size;
        public RTHandle SceneVolume;
        public Matrix4x4 WorldToVolume;
        public RTHandle SmoothedDepth;

        // RC 3D Probes - Enhanced Implementation
        [Header("3D Volume Settings")]
        public int VolumeResolution = 64; // 64³ → 96³ → 128³ sweep
        public int CascadeCount = 3; // 3 → 4 sweep
        
        [Header("3D Textures")]
        public RTHandle RadianceVolume; // RGBA16F for radiance
        public RTHandle BetaVolume; // R8 for β (opacity)
        
        [Header("Volume Matrices")]
        public Matrix4x4 WorldToVolumeMatrix;
        public Matrix4x4 VolumeToWorldMatrix;
        
        [Header("Volume Bounds")]
        public Bounds VolumeBounds;
        public Vector3 VolumeCenter;
        public Vector3 VolumeSize;
        
        [Header("Performance Tracking")]
        public float VoxelizationTime;
        public float InjectionTime;
        public float PropagationTime;
        public float CascadeTime;
        public float CompositeTime;
        
        [Header("Memory Usage")]
        public long RadianceVolumeMemory;
        public long BetaVolumeMemory;
        public long TotalMemoryUsage;
        
        // Cache for preventing reallocation
        private int lastVolumeResolution = -1;
        private int lastCascadeCount = -1;
        
        public bool NeedsReallocation(int volumeResolution, int cascadeCount)
        {
            return lastVolumeResolution != volumeResolution || lastCascadeCount != cascadeCount;
        }
        
        public void UpdateCache(int volumeResolution, int cascadeCount)
        {
            lastVolumeResolution = volumeResolution;
            lastCascadeCount = cascadeCount;
        }
        
        public void CalculateMemoryUsage()
        {
            if (RadianceVolume?.rt != null)
            {
                var desc = RadianceVolume.rt.descriptor;
                RadianceVolumeMemory = desc.width * desc.height * desc.volumeDepth * 8; // RGBA16F = 8 bytes
            }
            
            if (BetaVolume?.rt != null)
            {
                var desc = BetaVolume.rt.descriptor;
                BetaVolumeMemory = desc.width * desc.height * desc.volumeDepth * 1; // R8 = 1 byte
            }
            
            TotalMemoryUsage = RadianceVolumeMemory + BetaVolumeMemory;
        }
    }
}

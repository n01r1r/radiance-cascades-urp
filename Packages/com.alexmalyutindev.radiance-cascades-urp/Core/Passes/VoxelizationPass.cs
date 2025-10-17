using System;
using AlexMalyutinDev.RadianceCascades.Voxelization;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
 // ✅ Unity 6 RG API
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class VoxelizationData : ContextItem
    {
        public UnityEngine.Rendering.RenderGraphModule.TextureHandle SceneVolume;
        public Matrix4x4 WorldToVolume;

        public override void Reset()
        {
            SceneVolume = UnityEngine.Rendering.RenderGraphModule.TextureHandle.nullHandle;
            WorldToVolume = Matrix4x4.identity;
        }
    }

    public class VoxelizationPass : ScriptableRenderPass, IDisposable
    {
        private readonly RadianceCascadesRenderingData _radianceCascadesRenderingData;
        private readonly Voxelizator _voxelizator;
        private readonly int _resolution = 128;

        public VoxelizationPass(
            RadianceCascadeResources resources,
            RadianceCascadesRenderingData radianceCascadesRenderingData
        )
        {
            _radianceCascadesRenderingData = radianceCascadesRenderingData;
            profilingSampler = new ProfilingSampler("Voxelization");
            _voxelizator = new Voxelizator(resources.Voxelizator, resources.VoxelizatorCS);
        }

        public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData)
        {
            Debug.Log("VoxelizationPass: RecordRenderGraph called");
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            VoxelizeScene(renderGraph, frameData, cameraData, resourceData);
        }

        private class PassData
        {
            public Voxelizator Voxelizator;
            public UniversalCameraData CameraData;
            public int Resolution;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle SceneVolume;
            public Matrix4x4 WorldToVolume;
        }

        private void VoxelizeScene(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData, UniversalCameraData cameraData, UniversalResourceData resourceData)
        {
            using var builder = renderGraph.AddComputePass<PassData>("Voxelization.VoxelizeScene", out var passData);
            builder.AllowPassCulling(false);

            passData.Voxelizator = _voxelizator;
            passData.CameraData = cameraData;
            passData.Resolution = _resolution;

            // Create scene volume texture using TextureDesc (Unity 6 RenderGraph API)
            // Fix: Proper 3D texture size clamping with GPU limits (keep modest 64-256 recommended)
            int max3D = SystemInfo.maxTexture3DSize;
            int res = Mathf.Clamp(_resolution, 16, Mathf.Min(256, max3D));  // Keep modest size
            
            var desc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(res, res)
            {
                name = "SceneVolume",
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                dimension = TextureDimension.Tex3D,
                slices = res,  // ✅ Unity 6: Use slices for 3D depth
                msaaSamples = MSAASamples.None,
                depthBufferBits = DepthBits.None,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false
            };
            passData.SceneVolume = renderGraph.CreateTexture(desc);  // ✅ Use persistent for inter-pass communication
            
            // Declare that this pass will write to the SceneVolume texture
            builder.UseTexture(passData.SceneVolume, UnityEngine.Rendering.RenderGraphModule.AccessFlags.Write);

            // Calculate world to volume matrix
            // For now, create a simple identity matrix - this would need proper calculation
            // based on camera position and volume bounds in a real implementation
            passData.WorldToVolume = Matrix4x4.identity;

            builder.SetRenderFunc<PassData>(static (data, context) =>
            {
                // For now, we'll create a simple placeholder implementation
                // The full voxelization would require adapting the Voxelizator class
                // to work with RenderGraph TextureHandle instead of RTHandle
                
                // Note: Volume clearing should be handled via LoadAction.Clear in the builder
                // For compute passes, we don't use ClearRenderTarget
                
                // TODO: Implement proper voxelization using compute shaders
                // This would involve dispatching compute shaders to populate the 3D volume
                // For now, we'll leave it as a placeholder
            });

            // Store the result in context for other passes to use
            var voxelizationData = frameData.GetOrCreate<VoxelizationData>();
            voxelizationData.SceneVolume = passData.SceneVolume;
            voxelizationData.WorldToVolume = passData.WorldToVolume;
        }

        public void Dispose()
        {
            _voxelizator?.Dispose();
        }
    }
}

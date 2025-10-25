using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.SmoothedDepth
{
    public class SmoothedDepthPass : ScriptableRenderPass
    {
        private static readonly int InputMipLevel = Shader.PropertyToID("_InputMipLevel");
        private static readonly int InputResolution = Shader.PropertyToID("_InputResolution");

        private readonly Material _material;
        private readonly RadianceCascadesRenderingData _radianceCascadesRenderingData;

        public SmoothedDepthPass(
            Material material,
            RadianceCascadesRenderingData radianceCascadesRenderingData
        )
        {
            profilingSampler = new ProfilingSampler(nameof(SmoothedDepthPass));
            _material = material;
            _radianceCascadesRenderingData = radianceCascadesRenderingData;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            RenderSmoothedDepth(renderGraph, frameData, cameraData, resourceData);
        }

        private class PassData
        {
            public Material Material;
            public UniversalCameraData CameraData;
            public TextureHandle FrameDepth;
            public TextureHandle SmoothedDepth;
            public Vector4 InputResolution;
        }

        private void RenderSmoothedDepth(RenderGraph renderGraph, ContextContainer frameData, UniversalCameraData cameraData, UniversalResourceData resourceData)
        {
            using var builder = renderGraph.AddRasterRenderPass<PassData>("SmoothedDepth.Render", out var passData);
            builder.AllowGlobalStateModification(true);

            passData.Material = _material;
            passData.CameraData = cameraData;

            // Use frame depth texture
            passData.FrameDepth = resourceData.activeDepthTexture;
            builder.UseTexture(passData.FrameDepth);

            // Create smoothed depth texture using TextureDesc (Unity 6 RenderGraph API)
            var desc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(cameraData.camera.pixelWidth >> 1, cameraData.camera.pixelHeight >> 1)
            {
                name = "SmoothedDepth",
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false
            };
            passData.SmoothedDepth = builder.CreateTransientTexture(desc);

            passData.InputResolution = new Vector4(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight);

            builder.SetRenderAttachment(passData.SmoothedDepth, 0);
            builder.SetRenderFunc<PassData>(static (data, context) =>
            {
                context.cmd.SetGlobalInteger(InputMipLevel, 0);
                context.cmd.SetGlobalVector(InputResolution, data.InputResolution);
                BlitUtils.BlitTexture(context.cmd, data.FrameDepth, data.Material, 0);
            });

            // Generate mipmaps
            GenerateMipmaps(renderGraph, frameData, passData.SmoothedDepth);
        }

        private class MipmapPassData
        {
            public Material Material;
            public TextureHandle SmoothedDepth;
            public Vector4 InputResolution;
        }

        private void GenerateMipmaps(RenderGraph renderGraph, ContextContainer frameData, TextureHandle smoothedDepth)
        {
            using var builder = renderGraph.AddRasterRenderPass<MipmapPassData>("SmoothedDepth.Mipmaps", out var passData);
            builder.AllowGlobalStateModification(true);

            passData.Material = _material;
            passData.SmoothedDepth = smoothedDepth;
            builder.UseTexture(passData.SmoothedDepth);

            var cameraData = frameData.Get<UniversalCameraData>();
            passData.InputResolution = new Vector4(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight);

            builder.SetRenderAttachment(passData.SmoothedDepth, 0);
            builder.SetRenderFunc<MipmapPassData>(static (data, context) =>
            {
                // Generate mipmaps
                var width = (int)data.InputResolution.x;
                var height = (int)data.InputResolution.y;
                
                for (int mipLevel = 1; mipLevel < 8; mipLevel++) // Assuming max 8 mip levels
                {
                    width >>= 1;
                    height >>= 1;
                    if (width <= 0 || height <= 0) break;
                    
                    context.cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                    context.cmd.SetGlobalInteger(InputMipLevel, mipLevel - 1);
                    BlitUtils.BlitTexture(context.cmd, data.SmoothedDepth, data.Material, 1);
                }
            });
        }
    }
}

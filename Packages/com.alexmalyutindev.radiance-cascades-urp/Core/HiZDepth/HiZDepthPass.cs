using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
 // ✅ Unity 6 RG API
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.HiZDepth
{
    public class HiZDepthPass : ScriptableRenderPass, IDisposable
    {
        private readonly Material _material;
        private readonly ComputeShader _hiZDepthCS;

        public HiZDepthPass(Material hiZDepthMaterial, ComputeShader hiZDepthCS)
        {
            profilingSampler = new ProfilingSampler("HiZDepthPass");
            _hiZDepthCS = hiZDepthCS;
            _material = hiZDepthMaterial;
        }

        public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            RenderHiZDepth(renderGraph, frameData, cameraData, resourceData);
        }

        private class PassData
        {
            public Material Material;
            public ComputeShader HiZDepthCS;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameDepth;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle HiZDepth;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle TempDepth;
            public Vector4 Resolution;
        }

        private void RenderHiZDepth(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData, UniversalCameraData cameraData, UniversalResourceData resourceData)
        {
            using var builder = renderGraph.AddRasterRenderPass<PassData>("HiZDepth.Render", out var passData);
            builder.AllowGlobalStateModification(true);

            passData.Material = _material;
            passData.HiZDepthCS = _hiZDepthCS;

            // Use frame depth texture
            passData.FrameDepth = resourceData.activeDepthTexture;
            builder.UseTexture(passData.FrameDepth);

            // Create Hi-Z depth texture using TextureDesc (Unity 6 RenderGraph API)
            var desc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(cameraData.camera.pixelWidth >> 1, cameraData.camera.pixelHeight >> 1)
            {
                name = "Hi-ZDepth",
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_UNorm,
                enableRandomWrite = true,
                useMipMap = true
            };
            passData.HiZDepth = builder.CreateTransientTexture(desc);

            // Create temp depth texture using TextureDesc (Unity 6 RenderGraph API)
            var tempDesc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(cameraData.camera.pixelWidth >> 1, cameraData.camera.pixelHeight >> 1)
            {
                name = "TempDepth",
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_UNorm,
                enableRandomWrite = true,
                useMipMap = false
            };
            passData.TempDepth = builder.CreateTransientTexture(tempDesc);

            passData.Resolution = new Vector4(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight);

            builder.SetRenderAttachment(passData.HiZDepth, 0);
            builder.SetRenderFunc<PassData>(static (data, context) =>
            {
                var width = (int)data.Resolution.x;
                var height = (int)data.Resolution.y;

                context.cmd.SetGlobalTexture("_InputDepth", data.FrameDepth);
                context.cmd.SetGlobalVector("_Resolution", new Vector4(width, height));
                context.cmd.SetGlobalInt("_MipLevel", 0);
                BlitUtils.Blit(context.cmd, data.Material, 0);
                
                // Generate mipmaps
                for (int i = 0; i < 8; i++) // Assuming max 8 mip levels
                {
                    width >>= 1;
                    height >>= 1;
                    if (width <= 0 || height <= 0) break;
                    
                    context.cmd.SetGlobalInt("_MipLevel", i);
                    BlitUtils.Blit(context.cmd, data.Material, 0);
                }
            });
        }

        public void Dispose() { }
    }
}

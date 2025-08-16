using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.VarianceDepth
{
    public class VarianceDepthData : ContextItem
    {
        public TextureHandle VarianceDepth;
        
        public override void Reset()
        {
            VarianceDepth = TextureHandle.nullHandle;
        }
    }
    
    public class VarianceDepthPass : ScriptableRenderPass
    {
        private const int DepthToMomentsPass = 0;
        private const int BlurVerticalPass = 1;
        private const int BlurHorizontalPass = 2;
        private readonly Material _material;
        private readonly RadianceCascadesRenderingData _radianceCascadesRenderingData;
        private RTHandle _tempBuffer;

        public VarianceDepthPass(Material material, RadianceCascadesRenderingData radianceCascadesRenderingData)
        {
            profilingSampler = new ProfilingSampler(nameof(VarianceDepthPass));
            _material = material;
            _radianceCascadesRenderingData = radianceCascadesRenderingData;
        }

        private class PassData
        {
            public TextureHandle FrameDepth;
            public TextureHandle TempBuffer;
            public TextureHandle VarianceDepth;
            public Material Material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var varianceDepthData = frameData.Create<VarianceDepthData>();

            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var frameDesc = cameraData.cameraTargetDescriptor;

            using var builder = renderGraph.AddUnsafePass<PassData>(nameof(VarianceDepthPass), out var passData);
            builder.AllowPassCulling(false);

            passData.Material = _material;

            passData.FrameDepth = resourceData.activeDepthTexture;
            builder.UseTexture(passData.FrameDepth);

            var desc = new TextureDesc(frameDesc.width >> 1, frameDesc.height >> 1)
            {
                name = "VarianceDepth",
                colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RGFloat, false),
            };

            passData.VarianceDepth = renderGraph.CreateTexture(desc);
            varianceDepthData.VarianceDepth = passData.VarianceDepth;
            builder.UseTexture(passData.VarianceDepth, AccessFlags.ReadWrite);

            desc.name = "Temp";
            passData.TempBuffer = builder.CreateTransientTexture(desc);

            builder.SetRenderFunc<PassData>(static (data, context) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                cmd.SetRenderTarget(data.VarianceDepth);
                BlitUtils.BlitTexture(cmd, data.FrameDepth, data.Material, DepthToMomentsPass);

                cmd.SetRenderTarget(data.TempBuffer);
                BlitUtils.BlitTexture(cmd, data.VarianceDepth, data.Material, BlurVerticalPass);

                cmd.SetRenderTarget(data.VarianceDepth);
                BlitUtils.BlitTexture(cmd, data.TempBuffer, data.Material, BlurHorizontalPass);
            });
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var desc = new RenderTextureDescriptor(
                cameraTextureDescriptor.width >> 1,
                cameraTextureDescriptor.height >> 1
            )
            {
                colorFormat = RenderTextureFormat.RGFloat,
                depthStencilFormat = GraphicsFormat.None,
                useMipMap = false,
                autoGenerateMips = false,
            };
            RenderingUtils.ReAllocateIfNeeded(
                ref _radianceCascadesRenderingData.VarianceDepth,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "VarianceDepth"
            );
            RenderingUtils.ReAllocateIfNeeded(
                ref _tempBuffer,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "Temp"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var depthBuffer = renderingData.cameraData.renderer.cameraDepthTargetHandle;
                cmd.SetRenderTarget(_radianceCascadesRenderingData.VarianceDepth);
                BlitUtils.BlitTexture(cmd, depthBuffer, _material, DepthToMomentsPass);
                cmd.SetRenderTarget(_tempBuffer);
                BlitUtils.BlitTexture(cmd, _radianceCascadesRenderingData.VarianceDepth, _material, BlurVerticalPass);
                cmd.SetRenderTarget(_radianceCascadesRenderingData.VarianceDepth);
                BlitUtils.BlitTexture(cmd, _tempBuffer, _material, BlurHorizontalPass);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}
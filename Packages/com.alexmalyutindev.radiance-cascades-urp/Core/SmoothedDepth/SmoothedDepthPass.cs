using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
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

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var desc = new RenderTextureDescriptor(
                cameraTextureDescriptor.width >> 1,
                cameraTextureDescriptor.height >> 1
            )
            {
                colorFormat = RenderTextureFormat.RFloat,
                depthStencilFormat = GraphicsFormat.None,
                useMipMap = true,
                autoGenerateMips = false,
            };
            RenderingUtils.ReAllocateIfNeeded(
                ref _radianceCascadesRenderingData.SmoothedDepth,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "SmoothedDepth"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                var depthBuffer = renderingData.cameraData.renderer.cameraDepthTargetHandle;
                int width = depthBuffer.rt.width;
                int height = depthBuffer.rt.height;

                cmd.SetRenderTarget(_radianceCascadesRenderingData.SmoothedDepth, 0, CubemapFace.Unknown);
                cmd.SetGlobalInteger(InputMipLevel, 0);
                cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                BlitUtils.BlitTexture(cmd, depthBuffer, _material, 0);

                for (int mipLevel = 1; mipLevel < _radianceCascadesRenderingData.SmoothedDepth.rt.mipmapCount; mipLevel++)
                {
                    width >>= 1;
                    height >>= 1;
                    cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                    cmd.SetGlobalInteger(InputMipLevel, mipLevel - 1);
                    cmd.SetRenderTarget(_radianceCascadesRenderingData.SmoothedDepth, mipLevel, CubemapFace.Unknown);
                    BlitUtils.BlitTexture(cmd, _radianceCascadesRenderingData.SmoothedDepth, _material, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}

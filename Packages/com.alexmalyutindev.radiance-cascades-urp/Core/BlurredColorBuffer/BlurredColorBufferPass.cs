using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.BlurredColorBuffer
{
    public class BlurredColorBufferPass : ScriptableRenderPass
    {
        private static readonly int InputMipLevel = Shader.PropertyToID("_InputMipLevel");
        private static readonly int InputResolution = Shader.PropertyToID("_InputResolution");

        private readonly Material _material;
        private readonly RadianceCascadesRenderingData _radianceCascadesRenderingData;

        public BlurredColorBufferPass(
            Material material,
            RadianceCascadesRenderingData radianceCascadesRenderingData
        )
        {
            profilingSampler = new ProfilingSampler(nameof(BlurredColorBufferPass));
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
                colorFormat = RenderTextureFormat.ARGB32,
                sRGB = true,
                depthStencilFormat = GraphicsFormat.None,
                useMipMap = true,
                autoGenerateMips = false,
            };
            RenderingUtils.ReAllocateIfNeeded(
                ref _radianceCascadesRenderingData.BlurredColorBuffer,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "BlurredColorBuffer"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                var colorBuffer = renderingData.cameraData.renderer.cameraColorTargetHandle;
                int width = colorBuffer.rt.width;
                int height = colorBuffer.rt.height;

                cmd.SetRenderTarget(_radianceCascadesRenderingData.BlurredColorBuffer, 0, CubemapFace.Unknown);
                cmd.SetGlobalInteger(InputMipLevel, 0);
                cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                BlitUtils.BlitTexture(cmd, colorBuffer, _material, 0);

                for (int mipLevel = 1; mipLevel < _radianceCascadesRenderingData.BlurredColorBuffer.rt.mipmapCount; mipLevel++)
                {
                    width >>= 1;
                    height >>= 1;
                    cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                    cmd.SetGlobalInteger(InputMipLevel, mipLevel - 1);
                    cmd.SetRenderTarget(_radianceCascadesRenderingData.BlurredColorBuffer, mipLevel, CubemapFace.Unknown);
                    BlitUtils.BlitTexture(cmd, _radianceCascadesRenderingData.BlurredColorBuffer, _material, 0);
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

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.MinMaxDepth
{
    public class MinMaxDepthPass : ScriptableRenderPass, IDisposable
    {
        private static readonly int InputMipLevel = Shader.PropertyToID("_InputMipLevel");
        private static readonly int InputResolution = Shader.PropertyToID("_InputResolution");
        private readonly Material _material;
        private RTHandle _minMaxDepth;

        public MinMaxDepthPass(Material minMaxDepthMaterial)
        {
            profilingSampler = new ProfilingSampler(nameof(MinMaxDepthPass));
            _material = minMaxDepthMaterial;
        }


        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var desc = new RenderTextureDescriptor(
                cameraTextureDescriptor.width >> 1,
                cameraTextureDescriptor.height >> 1
            )
            {
                colorFormat = RenderTextureFormat.RG32,
                depthStencilFormat = GraphicsFormat.None,
                useMipMap = true,
                autoGenerateMips = false,
            };
            RenderingUtils.ReAllocateIfNeeded(
                ref _minMaxDepth,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "MinMaxDepth"
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
    
                cmd.SetRenderTarget(_minMaxDepth, 0, CubemapFace.Unknown);
                cmd.SetGlobalInteger(InputMipLevel, 0);
                cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                BlitUtils.BlitTexture(cmd, depthBuffer, _material, 0);

                for (int mipLevel = 1; mipLevel < _minMaxDepth.rt.mipmapCount; mipLevel++)
                {
                    width >>= 1;
                    height >>= 1;
                    cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                    cmd.SetGlobalInteger(InputMipLevel, mipLevel - 1);
                    cmd.SetRenderTarget(_minMaxDepth, mipLevel, CubemapFace.Unknown);
                    BlitUtils.BlitTexture(cmd, _minMaxDepth, _material, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _minMaxDepth?.Release();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.HiZDepth
{
    public class HiZDepthPass : ScriptableRenderPass, IDisposable
    {
        private readonly Material _material;
        private RTHandle _hiZDepth;
        private RTHandle _tempDepth;
        private ComputeShader _hiZDepthCS;

        public HiZDepthPass(Material hiZDepthMaterial, ComputeShader hiZDepthCS)
        {
            profilingSampler = new ProfilingSampler("HiZDepthPass");
            _hiZDepthCS = hiZDepthCS;
            _material = hiZDepthMaterial;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var desc = new RenderTextureDescriptor(
                cameraTextureDescriptor.width >> 1,
                cameraTextureDescriptor.height >> 1
            )
            {
                colorFormat = RenderTextureFormat.R16,
                depthStencilFormat = GraphicsFormat.None,
                useMipMap = true,
            };
            RenderingUtils.ReAllocateIfNeeded(
                ref _hiZDepth,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "Hi-ZDepth"
            );

            desc.mipCount = 0;
            desc.useMipMap = false;
            RenderingUtils.ReAllocateIfNeeded(ref _tempDepth, desc);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var depth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            // TODO: Calculate Hi-Z Depth.

            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                // cmd.SetComputeTextureParam(_hiZDepthCS, 0, "_InputDepth", depth);
                // cmd.SetComputeTextureParam(_hiZDepthCS, 0, "_TargetDepth", _hiZDepth);
                // cmd.DispatchCompute(
                //     _hiZDepthCS,
                //     0,
                //     depth.rt.width / 8,
                //     depth.rt.height / 8,
                //     1
                // );


                var width = depth.rt.width;
                var height = depth.rt.height;

                cmd.SetRenderTarget(_tempDepth);
                cmd.SetGlobalTexture("_InputDepth", depth);
                cmd.SetGlobalVector("_Resolution", new Vector4(width, height));
                cmd.SetGlobalInt("_MipLevel", 0);
                BlitUtils.Blit(cmd, _material, 0);
                cmd.CopyTexture(_tempDepth, 0, 0, _hiZDepth, 0, 0);

                
                cmd.SetGlobalTexture("_InputDepth", _hiZDepth);
                
                for (int i = 0; i < _hiZDepth.rt.mipmapCount - 1; i++)
                {
                    width >>= 1;
                    height >>= 1;
                    cmd.SetGlobalInt("_MipLevel", i);
                    BlitUtils.Blit(cmd, _material, 0);
                    cmd.CopyTexture(
                        _tempDepth,
                        0,
                        0,
                        0,
                        0,
                        width >> 1,
                        height >> 1,
                        _hiZDepth,
                        0,
                        i + 1,
                        0,
                        0
                    );
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
            _tempDepth?.Release();
            _hiZDepth?.Release();
        }
    }
}

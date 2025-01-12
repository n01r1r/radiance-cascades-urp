using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.MinMaxDepth
{
    public class MinMaxDepthPass : ScriptableRenderPass, IDisposable
    {
        private const int MinMaxOriginalDepthPass = 0;
        private const int MinMaxDepthMipPass = 1;
        private const int CopyLevelPass = 2;

        private static readonly int InputMipLevel = Shader.PropertyToID("_InputMipLevel");
        private static readonly int InputResolution = Shader.PropertyToID("_InputResolution");
        private static readonly int Scale = Shader.PropertyToID("_Scale");
        
        private readonly Material _material;
        private readonly RadianceCascadesRenderingData _renderingData;
        
        private RTHandle _tempMinMaxDepth;

        public MinMaxDepthPass(Material minMaxDepthMaterial, RadianceCascadesRenderingData renderingData)
        {
            profilingSampler = new ProfilingSampler(nameof(MinMaxDepthPass));
            _material = minMaxDepthMaterial;
            _renderingData = renderingData;
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
                useMipMap = true,
                autoGenerateMips = false,
            };
            RenderingUtils.ReAllocateIfNeeded(
                ref _renderingData.MinMaxDepth,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "MinMaxDepth"
            );

            if (SystemInfo.graphicsDeviceType is  not (GraphicsDeviceType.Metal or GraphicsDeviceType.Vulkan))
            {
                desc.width >>= 1;
                desc.height >>= 1;
                desc.useMipMap = false;
                RenderingUtils.ReAllocateIfNeeded(
                    ref _tempMinMaxDepth,
                    desc,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: "MinMaxDepth2"
                );
            }
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

                cmd.SetRenderTarget(_renderingData.MinMaxDepth, 0, CubemapFace.Unknown);
                cmd.SetGlobalInteger(InputMipLevel, 0);
                cmd.SetGlobalFloat("_Scale", 1);
                cmd.SetGlobalVector(InputResolution, new Vector4(width, height));
                BlitUtils.BlitTexture(cmd, depthBuffer, _material, MinMaxOriginalDepthPass);

                for (int mipLevel = 1; mipLevel < _renderingData.MinMaxDepth.rt.mipmapCount; mipLevel++)
                {
                    width >>= 1;
                    height >>= 1;

                    cmd.SetGlobalVector(InputResolution, new Vector4(width, height));

                    if (SystemInfo.graphicsDeviceType is GraphicsDeviceType.Metal or GraphicsDeviceType.Vulkan)
                    {
                        cmd.SetGlobalInteger(InputMipLevel, mipLevel - 1);
                        cmd.SetRenderTarget(_renderingData.MinMaxDepth, mipLevel);
                        BlitUtils.BlitTexture(cmd, _renderingData.MinMaxDepth, _material, MinMaxDepthMipPass);
                    }
                    else
                    {
                        cmd.SetRenderTarget(_tempMinMaxDepth);
                        cmd.EnableScissorRect(new Rect(0, 0, width, height));
                        cmd.SetGlobalInteger(InputMipLevel, mipLevel - 1);
                        cmd.SetGlobalFloat(Scale, 1 << (mipLevel - 1));
                        BlitUtils.BlitTexture(cmd, _renderingData.MinMaxDepth, _material, 1);
                        cmd.DisableScissorRect();

                        cmd.SetRenderTarget(_renderingData.MinMaxDepth, mipLevel);
                        cmd.SetGlobalInteger(InputMipLevel, mipLevel - 1);
                        cmd.SetGlobalVector(InputResolution, new Vector4(width >> 1, height >> 1));
                        BlitUtils.BlitTexture(cmd, _tempMinMaxDepth, _material, CopyLevelPass);
                    }
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
            _renderingData.MinMaxDepth?.Release();
        }
    }
}

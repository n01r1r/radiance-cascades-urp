using System;
using AlexMalyutinDev.RadianceCascades.BlurredColorBuffer;
using AlexMalyutinDev.RadianceCascades.MinMaxDepth;
using AlexMalyutinDev.RadianceCascades.VarianceDepth;
using InternalBridge;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class DirectionFirstRCPass : ScriptableRenderPass, IDisposable
    {
        private readonly RadianceCascadesDirectionFirstCS _compute;
        private RTHandle _cascade0;
        private RTHandle _radianceSH;
        private RTHandle _intermediateBuffer;
        private RTHandle _intermediateBuffer2;

        private readonly Material _blitMaterial;
        private readonly RadianceCascadesRenderingData _renderingData;

        public DirectionFirstRCPass(
            RadianceCascadeResources resources,
            RadianceCascadesRenderingData renderingData
        )
        {
            profilingSampler = new ProfilingSampler("RadianceCascades.DirectionFirst");
            _compute = new RadianceCascadesDirectionFirstCS(resources.RadianceCascadesDirectionalFirstCS);
            // TODO: Make proper C# wrapper for Blit/Combine shader!
            _blitMaterial = resources.BlitMaterial;
            _renderingData = renderingData;
        }

        private class PassData
        {
            public RadianceCascadesDirectionFirstCS Compute;

            public Vector4 CascadesSizeTexel;
            public TextureHandle Cascades;
            public Vector4 RadianceSHSizeTexel;
            public TextureHandle RadianceSH;

            public UniversalCameraData CameraData;

            public TextureHandle FrameDepth;
            public TextureHandle BlurredColor;
            
            public TextureHandle MinMaxDepth;
            public Vector4 VarianceDepthSizeTexel;
            public TextureHandle VarianceDepth;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var minMaxDepthData = frameData.Get<MinMaxDepthData>();
            var varianceDepthData = frameData.Get<VarianceDepthData>();
            var blurredColorData = frameData.Get<BlurredColorData>();

            using var builder = renderGraph.AddComputePass<PassData>(nameof(DirectionFirstRCPass), out var passData);
            builder.AllowPassCulling(false);

            passData.CameraData = cameraData;

            passData.FrameDepth = resourceData.activeDepthTexture;
            builder.UseTexture(passData.FrameDepth);
            passData.MinMaxDepth = minMaxDepthData.MinMaxDepth;
            builder.UseTexture(passData.MinMaxDepth);

            passData.VarianceDepthSizeTexel = GetSizeTexel(varianceDepthData.VarianceDepth, renderGraph);
            passData.VarianceDepth = varianceDepthData.VarianceDepth;
            builder.UseTexture(passData.VarianceDepth);
            passData.BlurredColor = blurredColorData.BlurredColor;
            builder.UseTexture(passData.BlurredColor);

            passData.Compute = _compute;

            int cascadeWidth = 2048; // cameraTextureDescriptor.width; // 2048; // 
            int cascadeHeight = 1024; // cameraTextureDescriptor.height; // 1024; // 
            var desc = new TextureDesc(cascadeWidth, cascadeHeight)
            {
                name = "RadianceCascades",
                colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false),
                enableRandomWrite = true,
            };
            passData.CascadesSizeTexel = new Vector4(
                desc.width, desc.height,
                1.0f / desc.width, 1.0f / desc.height
            );
            passData.Cascades = builder.CreateTransientTexture(desc);

            desc.name = "RadianceSH";
            desc.width = cascadeWidth / 2;
            desc.height = cascadeHeight / 2;
            passData.RadianceSHSizeTexel = new Vector4(
                desc.width, desc.height,
                1.0f / desc.width, 1.0f / desc.height
            );
            passData.RadianceSH = builder.CreateTransientTexture(desc);

            builder.SetRenderFunc<PassData>(static (data, context) =>
            {
                data.Compute.RenderMerge(
                    context.cmd,
                    ref data.CameraData,
                    data.FrameDepth,
                    data.MinMaxDepth,
                    data.VarianceDepth,
                    data.VarianceDepthSizeTexel,
                    data.BlurredColor,
                    1.0f, // TODO: RayLength setting!
                    ref data.Cascades,
                    data.CascadesSizeTexel
                );
                
                // TODO: SH
                // TODO: Blit
            });
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // 512 => 512 / 8 = 64 probes in row
            // TODO: Allocate texture with dimension (screen.width, screen.height) * 2 
            int cascadeWidth = 2048; // cameraTextureDescriptor.width; // 2048; // 
            int cascadeHeight = 1024; // cameraTextureDescriptor.height; // 1024; // 
            var desc = new RenderTextureDescriptor(cascadeWidth, cascadeHeight)
            {
                colorFormat = RenderTextureFormat.ARGBFloat,
                sRGB = false,
                enableRandomWrite = true,
            };
            RenderingUtils.ReAllocateIfNeeded(ref _cascade0, desc, name: "RadianceCascades");

            desc = new RenderTextureDescriptor(cascadeWidth / 2, cascadeHeight / 2)
            {
                colorFormat = RenderTextureFormat.ARGBFloat,
                sRGB = false,
                enableRandomWrite = true,
            };
            RenderingUtils.ReAllocateIfNeeded(ref _radianceSH, desc, name: "RadianceSH");

            desc = new RenderTextureDescriptor(cameraTextureDescriptor.width / 2, cameraTextureDescriptor.height / 2)
            {
                colorFormat = RenderTextureFormat.ARGBFloat,
                sRGB = false,
            };
            RenderingUtils.ReAllocateIfNeeded(ref _intermediateBuffer, desc, name: "RadianceBuffer");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var radianceCascades = VolumeManager.instance.stack.GetComponent<RadianceCascades>();

            var renderer = renderingData.cameraData.renderer;
            var colorBuffer = renderer.cameraColorTargetHandle;
            var depthBuffer = renderer.cameraDepthTargetHandle;

            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // _compute.RenderMerge(
                //     cmd,
                //     ref renderingData.cameraData,
                //     depthBuffer,
                //     _renderingData.MinMaxDepth,
                //     _renderingData.VarianceDepth,
                //     _renderingData.BlurredColorBuffer,
                //     radianceCascades.RayScale.value,
                //     ref _cascade0
                // );

                if (!radianceCascades.UseSH.value)
                {
                    cmd.BeginSample("RadianceCascade.Combine");
                    {
                        cmd.SetRenderTarget(_intermediateBuffer);
                        cmd.SetGlobalTexture(ShaderIds.GBuffer3, renderer.GetGBuffer(3));
                        cmd.SetGlobalTexture(ShaderIds.MinMaxDepth, _renderingData.MinMaxDepth);
                        BlitUtils.BlitTexture(cmd, _cascade0, _blitMaterial, 2);

                        cmd.SetRenderTarget(colorBuffer, depthBuffer);
                        BlitUtils.BlitTexture(cmd, _intermediateBuffer, _blitMaterial, 3);
                    }
                    cmd.EndSample("RadianceCascade.Combine");
                }
                else
                {
                    // TODO: Combine into SH.
                    _compute.CombineSH(
                        cmd,
                        ref renderingData.cameraData,
                        _cascade0,
                        _renderingData.MinMaxDepth,
                        _renderingData.VarianceDepth,
                        _radianceSH
                    );

                    cmd.BeginSample("RadianceCascade.BlitSH");
                    cmd.SetRenderTarget(colorBuffer, depthBuffer);
                    cmd.SetGlobalMatrix("_ViewToWorld", renderingData.cameraData.GetViewMatrix().inverse);
                    cmd.SetGlobalTexture("_MinMaxDepth", _renderingData.MinMaxDepth);
                    BlitUtils.BlitTexture(cmd, _radianceSH, _blitMaterial, 4);
                    cmd.EndSample("RadianceCascade.BlitSH");
                }
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _cascade0?.Release();
        }

        private static Vector4 GetSizeTexel(TextureHandle texture, RenderGraph rg)
        {
            var desc = texture.GetDescriptor(rg);
            return new Vector4(
                desc.width, desc.height,
                1.0f / desc.width, 1.0f / desc.height
            );
        }
    }
}
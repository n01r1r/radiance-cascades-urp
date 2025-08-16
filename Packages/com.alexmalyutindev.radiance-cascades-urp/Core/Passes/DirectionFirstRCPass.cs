using System;
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
        private readonly Material _blitMaterial;

        public DirectionFirstRCPass(RadianceCascadeResources resources)
        {
            profilingSampler = new ProfilingSampler("RadianceCascades.DirectionFirst");
            _compute = new RadianceCascadesDirectionFirstCS(resources.RadianceCascadesDirectionalFirstCS);
            // TODO: Make proper C# wrapper for Blit/Combine shader!
            _blitMaterial = resources.BlitMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            RenderCascades(renderGraph, frameData, out var sh);
            CombineCascades(renderGraph, frameData, sh);
        }

        private class PassData
        {
            public RadianceCascadesDirectionFirstCS Compute;
            public float RayLength;

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

        private void RenderCascades(RenderGraph renderGraph, ContextContainer frameData, out TextureHandle radianceSH)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var minMaxDepthData = frameData.Get<MinMaxDepthData>();
            var varianceDepthData = frameData.Get<VarianceDepthData>();
            var blurredColorData = frameData.Get<BlurredColorData>();

            var settings = VolumeManager.instance.stack.GetComponent<RadianceCascades>();

            using var builder = renderGraph.AddComputePass<PassData>("RC.Render", out var passData);
            builder.AllowPassCulling(false);

            passData.CameraData = cameraData;
            passData.RayLength = settings.RayScale.value;

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
            passData.RadianceSH = renderGraph.CreateTexture(desc);
            builder.UseTexture(passData.RadianceSH, AccessFlags.Write);

            // TODO: Refactor!
            radianceSH = passData.RadianceSH;

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
                    data.RayLength,
                    ref data.Cascades,
                    data.CascadesSizeTexel
                );

                data.Compute.CombineSH(
                    context.cmd,
                    ref data.CameraData,
                    data.Cascades,
                    data.CascadesSizeTexel,
                    data.MinMaxDepth,
                    data.VarianceDepth,
                    ref data.RadianceSH,
                    data.RadianceSHSizeTexel
                );
            });
        }

        private class CombinePassData
        {
            public Material Material;
            public UniversalCameraData CameraData;

            public TextureHandle MinMaxDepth;
            public TextureHandle RadianceSH;

            public TextureHandle FrameColor;
            public TextureHandle FrameDepth;
            public TextureHandle FrameNormals;
        }

        private void CombineCascades(RenderGraph renderGraph, ContextContainer frameData, TextureHandle radianceSH)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var minMaxDepthData = frameData.Get<MinMaxDepthData>();

            using var builder = renderGraph.AddRasterRenderPass<CombinePassData>("RC.Combine", out var passData);
            builder.AllowGlobalStateModification(true);

            passData.Material = _blitMaterial;
            passData.CameraData = cameraData;

            passData.FrameColor = resourceData.gBuffer[0];
            builder.UseTexture(passData.FrameColor);
            passData.FrameNormals = resourceData.gBuffer[2];
            builder.UseTexture(passData.FrameNormals);
            passData.FrameDepth = resourceData.cameraDepth;
            builder.UseTexture(passData.FrameDepth);

            passData.RadianceSH = radianceSH;
            builder.UseTexture(passData.RadianceSH);

            passData.MinMaxDepth = minMaxDepthData.MinMaxDepth;
            builder.UseTexture(passData.MinMaxDepth);

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderFunc<CombinePassData>(static (data, context) =>
            {
                context.cmd.SetGlobalMatrix("_ViewToWorld", data.CameraData.GetViewMatrix().inverse);
                context.cmd.SetGlobalTexture("_MinMaxDepth", data.MinMaxDepth);

                context.cmd.SetGlobalTexture("_GBuffer0", data.FrameColor);
                context.cmd.SetGlobalTexture("_GBuffer2", data.FrameNormals);
                context.cmd.SetGlobalTexture("_CameraDepthTexture", data.FrameDepth);
                BlitUtils.BlitTexture(context.cmd, data.RadianceSH, data.Material, 4);
            });
        }

        public void Dispose() { }

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

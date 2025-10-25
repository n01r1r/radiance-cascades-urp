using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class BlurredColorData : ContextItem
    {
        public TextureHandle BlurredColor;

        public override void Reset()
        {
            BlurredColor = TextureHandle.nullHandle;
        }
    }

    public class BlurredColorBufferPass : ScriptableRenderPass
    {
        private static readonly int InputMipLevelId = Shader.PropertyToID("_InputMipLevel");
        private static readonly int InputResolutionId = Shader.PropertyToID("_InputResolution");
        private static readonly int OffsetDirectionId = Shader.PropertyToID("_OffsetDirection");

        private readonly Material _material;
        private readonly RadianceCascadesRenderingData _radianceCascadesRenderingData;
        private RTHandle _tempBlurBuffer;

        public BlurredColorBufferPass(
            Material material,
            RadianceCascadesRenderingData radianceCascadesRenderingData
        )
        {
            profilingSampler = new ProfilingSampler(nameof(BlurredColorBufferPass));
            _material = material;
            _radianceCascadesRenderingData = radianceCascadesRenderingData;
        }

        private class PassData
        {
            public TextureHandle FrameColor;
            public TextureHandle BlurredColorBuffer;
            public TextureHandle TempBuffer;
            public Material Material;

            public Vector4 InputResolution;
            public Vector4 TargetResolution;
            public int TargetMipsCount;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var blurredColorData = frameData.Create<BlurredColorData>();

            var frameDesc = cameraData.cameraTargetDescriptor;

            using var builder = renderGraph.AddUnsafePass<PassData>(nameof(BlurredColorBufferPass), out var passData);
            builder.AllowPassCulling(false);

            passData.Material = _material;

            passData.InputResolution = new Vector4(frameDesc.width, frameDesc.height);
            passData.FrameColor = resourceData.activeColorTexture;
            builder.UseTexture(passData.FrameColor);

            var targetWidth = 2 * _radianceCascadesRenderingData.Cascade0Size.x;
            var targetHeight = 2 * _radianceCascadesRenderingData.Cascade0Size.y;
            passData.TargetResolution = new Vector4(targetWidth, targetHeight);
            passData.TargetMipsCount = (int)Mathf.Log(targetHeight, 2);

            var desc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(targetWidth, targetHeight)
            {
                name = "BlurredColorBuffer",
                colorFormat = frameDesc.graphicsFormat,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = true,
                autoGenerateMips = false
            };
            passData.BlurredColorBuffer = renderGraph.CreateTexture(desc);
            builder.UseTexture(passData.BlurredColorBuffer, AccessFlags.ReadWrite);
            blurredColorData.BlurredColor = passData.BlurredColorBuffer;

            // Remove .name assignment as it's not supported in Unity 6
            desc.width >>= 1;
            desc.height >>= 1;
            desc.useMipMap = false;
            passData.TempBuffer = builder.CreateTransientTexture(desc);

            builder.SetRenderFunc<PassData>(static (data, context) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                cmd.SetGlobalInteger(InputMipLevelId, 0);
                cmd.SetGlobalVector(InputResolutionId, data.InputResolution);
                cmd.SetRenderTarget(data.BlurredColorBuffer, 0, CubemapFace.Unknown);
                BlitUtils.BlitTexture(cmd, data.FrameColor, data.Material, 0);

                var width = (int)data.TargetResolution.x;
                var height = (int)data.TargetResolution.y;

                // NOTE: Can't render into MipLevel+1 and read from MipLevel of the same image on DX!
                // Work around is to render blur in two taps:
                // horizontal blur into TempBuffer, then vertical blur into BlurredColorBuffer
                for (int mipLevel = 1; mipLevel < data.TargetMipsCount; mipLevel++)
                {
                    cmd.SetGlobalVector(InputResolutionId, new Vector4(width, height));

                    cmd.SetRenderTarget(data.TempBuffer, mipLevel - 1);
                    cmd.SetGlobalInteger(InputMipLevelId, mipLevel - 1);
                    cmd.SetGlobalVector(OffsetDirectionId, new Vector4(1, 0));
                    BlitUtils.BlitTexture(cmd, data.BlurredColorBuffer, data.Material, 3);

                    cmd.SetRenderTarget(data.BlurredColorBuffer, mipLevel);
                    cmd.SetGlobalInteger(InputMipLevelId, mipLevel - 1);
                    cmd.SetGlobalVector(OffsetDirectionId, new Vector4(0, 1));
                    BlitUtils.BlitTexture(cmd, data.TempBuffer, data.Material, 3);

                    width >>= 1;
                    height >>= 1;
                }
            });
        }
    }
}

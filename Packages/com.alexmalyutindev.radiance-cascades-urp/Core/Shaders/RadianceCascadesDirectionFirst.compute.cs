using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadesDirectionFirstCS
    {
        private readonly ComputeShader _compute;
        private readonly int _renderAndMergeKernel;
        private readonly int _combineSHKernel;
        private readonly LocalKeyword _bilinearKw;
        private readonly LocalKeyword _bilateralKw;

        public RadianceCascadesDirectionFirstCS(ComputeShader compute)
        {
            _compute = compute;
            _renderAndMergeKernel = _compute.FindKernel("RenderAndMergeCascade");
            _combineSHKernel = _compute.FindKernel("CombineSH");
        }

        public void RenderMerge(
            ComputeCommandBuffer cmd,
            ref UniversalCameraData cameraData,
            TextureHandle depth,
            TextureHandle minMaxDepth,
            TextureHandle varianceDepth,
            Vector4 varianceDepthSizeTexel,
            TextureHandle blurredColor,
            float rayScale,
            ref TextureHandle target,
            Vector4 targetSizeTexel
        )
        {
            var kernel = _renderAndMergeKernel;
            if (kernel < 0) return;

            cmd.BeginSample("RadianceCascade.RenderMerge");

            // TODO: Remove! Only for debug purpose!
            // cmd.SetRenderTarget(target);
            // cmd.ClearRenderTarget(false, true, Color.clear);

            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.DepthTexture, depth);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.MinMaxDepth, minMaxDepth);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.BlurredColor, blurredColor);

            cmd.SetComputeVectorParam(_compute, ShaderIds.VarianceDepthSize, varianceDepthSizeTexel);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.VarianceDepth, varianceDepth);

            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, targetSizeTexel);
            cmd.SetComputeTextureParam(_compute, kernel, "_RadianceCascades", target);

            cmd.SetComputeMatrixParam(_compute, "_WorldToView", cameraData.GetViewMatrix());
            cmd.SetComputeMatrixParam(_compute, "_ViewToWorld", cameraData.GetViewMatrix().inverse);
            cmd.SetComputeMatrixParam(_compute, "_ViewToHClip", cameraData.GetProjectionMatrix());

            cmd.SetComputeFloatParam(_compute, "_RayScale", rayScale);

            for (int cascadeLevel = 5; cascadeLevel >= 0; cascadeLevel--)
            {
                Vector4 probesCount = new Vector4(
                    Mathf.FloorToInt(targetSizeTexel.x / (8 * 1 << cascadeLevel)),
                    Mathf.FloorToInt(targetSizeTexel.y / (8 * 1 << cascadeLevel))
                );
                cmd.SetComputeVectorParam(_compute, "_ProbesCount", probesCount);

                cmd.SetComputeIntParam(_compute, "_CascadeLevel", cascadeLevel);

                _compute.GetKernelThreadGroupSizes(kernel, out var x, out var y, out _);
                // TODO: Spawn only one cascade size Y groups, make all latitudinal ray in one thread?
                cmd.DispatchCompute(
                    _compute,
                    kernel,
                    Mathf.CeilToInt(targetSizeTexel.x / 2 / x),
                    Mathf.CeilToInt(targetSizeTexel.y / (y * (1 << cascadeLevel))),
                    1
                );
            }

            cmd.EndSample("RadianceCascade.RenderMerge");
        }

        public void CombineSH(
            CommandBuffer cmd,
            ref CameraData cameraData,
            RTHandle cascades,
            RTHandle minMaxDepth,
            RTHandle varianceDepth,
            RTHandle radianceSH
        )
        {
            var kernel = _combineSHKernel;
            if (kernel < 0) return;

            cmd.BeginSample("RadianceCascade.CombineSH");

            // TODO: Remove! Only for debug purpose!
            cmd.SetRenderTarget(radianceSH);

            Vector4 probesCount = new Vector4(
                Mathf.FloorToInt(cascades.rt.width / 4),
                Mathf.FloorToInt(cascades.rt.height / 4)
            );
            cmd.SetComputeVectorParam(_compute, "_ProbesCount", probesCount);
            cmd.SetComputeMatrixParam(_compute, "_ViewToWorld", cameraData.GetViewMatrix().inverse);

            cmd.SetComputeTextureParam(_compute, kernel, "_RadianceCascades", cascades);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.MinMaxDepth, minMaxDepth);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.VarianceDepth, varianceDepth);
            cmd.SetComputeTextureParam(_compute, kernel, "_RadianceSH", radianceSH);


            int width = radianceSH.rt.width / 2;
            int height = radianceSH.rt.height / 2;
            cmd.DispatchCompute(_compute, kernel, width / 8, height / 4, 1);
            cmd.EndSample("RadianceCascade.CombineSH");
        }
    }
}

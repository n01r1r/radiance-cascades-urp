using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadesDirectionFirstCS
    {
        private readonly ComputeShader _compute;
        private readonly int _renderAndMergeKernel;
        private readonly LocalKeyword _bilinearKw;
        private readonly LocalKeyword _bilateralKw;

        public RadianceCascadesDirectionFirstCS(ComputeShader compute)
        {
            _compute = compute;
            _renderAndMergeKernel = _compute.FindKernel("RenderAndMergeCascade");
            // TODO: Fix keywords.
            // _bilinearKw = new LocalKeyword(_compute, "_UPSCALE_MODE_BILINEAR");
            // _bilateralKw = new LocalKeyword(_compute, "_UPSCALE_MODE_BILATERAL");
        }

        public void RenderMerge(
            CommandBuffer cmd,
            ref CameraData cameraData,
            RTHandle color,
            RTHandle depth,
            RTHandle minMaxDepth,
            RTHandle smoothedDepth,
            RTHandle blurredColor,
            ref RTHandle target
        )
        {
            var kernel = _renderAndMergeKernel;
            cmd.BeginSample("RadianceCascade.RenderMerge");

            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, Color.clear);

            var colorRT = color.rt;
            var colorTexelSize = new Vector4(
                1.0f / colorRT.width,
                1.0f / colorRT.height,
                colorRT.width,
                colorRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.ColorTextureTexelSize, colorTexelSize);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.ColorTexture, color);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.DepthTexture, depth);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.MinMaxDepth, minMaxDepth);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.SmoothedDepth, smoothedDepth);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.BlurredColor, blurredColor);

            var targetRT = target.rt;
            var cascadeBufferSize = new Vector4(
                targetRT.width,
                targetRT.height,
                1.0f / targetRT.width,
                1.0f / targetRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeBufferSize);
            cmd.SetComputeTextureParam(_compute, kernel, ShaderIds.OutCascade, target);

            // var cameraTransform = cameraData.camera.transform;
            // var rotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
            // cmd.SetComputeMatrixParam(_compute, "_WorldToView", CreateViewMatrix(cameraData.worldSpaceCameraPos, rotation));
            cmd.SetComputeMatrixParam(_compute, "_WorldToView", cameraData.GetViewMatrix());
            cmd.SetComputeMatrixParam(_compute, "_ViewToHClip", cameraData.GetGPUProjectionMatrix());

            // TODO: Move out of this scope!
            var settings = VolumeManager.instance.stack.GetComponent<RadianceCascades>();
            cmd.SetComputeFloatParam(_compute, "_UpsampleTolerance", settings.UpsampleTolerance.value);
            cmd.SetComputeFloatParam(_compute, "_NoiseFilterStrength", settings.NoiseFilterStrength.value);

            // TODO: Fix keywords.
            // cmd.SetKeyword(_compute, _bilinearKw, settings.UpscaleMode.value == UpscaleMode.Bilinear);
            // cmd.SetKeyword(_compute, _bilateralKw, settings.UpscaleMode.value == UpscaleMode.Bilateral);
            
            for (int cascadeLevel = 5; cascadeLevel >= 0; cascadeLevel--)
            {
                Vector4 probesCount = new Vector4(
                    Mathf.FloorToInt(cascadeBufferSize.x / (8 * 1 << cascadeLevel)),
                    Mathf.FloorToInt(cascadeBufferSize.y / (8 * 1 << cascadeLevel))
                );
                cmd.SetComputeVectorParam(_compute, "_ProbesCount", probesCount);

                cmd.SetComputeIntParam(_compute, "_CascadeLevel", cascadeLevel);

                _compute.GetKernelThreadGroupSizes(kernel, out var x, out var y, out _);
                cmd.DispatchCompute(
                    _compute,
                    kernel,
                    Mathf.CeilToInt(cascadeBufferSize.x / x),
                    Mathf.CeilToInt(cascadeBufferSize.y / (y * (1 << cascadeLevel))),
                    1
                );
            }

            cmd.EndSample("RadianceCascade.RenderMerge");
        }
        
        public static Matrix4x4 CreateViewMatrix(Vector3 position, Quaternion rotation)
        {
            var view = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            if (SystemInfo.usesReversedZBuffer)
            {
                view.m20 = -view.m20;
                view.m21 = -view.m21;
                view.m22 = -view.m22;
                view.m23 = -view.m23;
            }

            return view;
        }
    }
}

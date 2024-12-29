using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RCDirectionalFirstCS
    {
        private readonly ComputeShader _compute;
        private readonly int _renderKernel;
        private readonly int _mergKernel;

        public RCDirectionalFirstCS(ComputeShader compute)
        {
            _compute = compute;
            _renderKernel = _compute.FindKernel("RenderCascade");
            _mergKernel = _compute.FindKernel("MergeCascade");
        }

        public void Render(CommandBuffer cmd, RTHandle color, RTHandle depth, ref RTHandle target)
        {
            cmd.BeginSample("RadianceCascade.Render");

            cmd.SetRenderTarget(target);

            var colorRT = color.rt;
            var colorTexelSize = new Vector4(
                1.0f / colorRT.width,
                1.0f / colorRT.height,
                colorRT.width,
                colorRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.ColorTextureTexelSize, colorTexelSize);
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.ColorTexture, color);
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.DepthTexture, depth);

            var targetRT = target.rt;
            var cascadeBufferSize = new Vector4(
                targetRT.width,
                targetRT.height,
                1.0f / targetRT.width,
                1.0f / targetRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeBufferSize);

            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.OutCascade, target);

            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                targetRT.width / 8,
                targetRT.height / 8, // TODO: Spawn 4 times less threads for depth rays.
                1
            );

            cmd.EndSample("RadianceCascade.Render");
        }

        public void Merge(CommandBuffer cmd, ref RTHandle target)
        {
            cmd.BeginSample("RadianceCascade.Merge");

            var targetRT = target.rt;
            var cascadeBufferSize = new Vector4(
                targetRT.width,
                targetRT.height,
                1.0f / targetRT.width,
                1.0f / targetRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeBufferSize);
            cmd.SetComputeTextureParam(_compute, _mergKernel, ShaderIds.LowerCascade, target);
            // NOTE: Bind same buffer to sample from it, cus LowerCascade in RWTexture.
            cmd.SetComputeTextureParam(_compute, _mergKernel, ShaderIds.UpperCascade, target);

            cmd.SetRenderTarget(target);

            for (int upperCascadeLevelId = 5; upperCascadeLevelId > 0; upperCascadeLevelId--)
            {
                cmd.SetComputeFloatParam(_compute, "_LowerCascadeBottomCoord", targetRT.height >> (upperCascadeLevelId));
                cmd.SetComputeFloatParam(_compute, "_UpperCascadeBottomCoord", targetRT.height >> (upperCascadeLevelId + 1));

                var lowerCascadeAngleCount = 8 * (1 << (upperCascadeLevelId - 1));
                cmd.SetComputeFloatParam(_compute, "_LowerCascadeAnglesCount", lowerCascadeAngleCount);
                cmd.SetComputeFloatParam(_compute, "_UpperCascadeAnglesCount", lowerCascadeAngleCount * 2);

                cmd.SetComputeIntParam(_compute, ShaderIds.CascadeLevel, upperCascadeLevelId);
                cmd.DispatchCompute(
                    _compute,
                    _mergKernel,
                    targetRT.width / 8,
                    targetRT.height / (8 * (1 << upperCascadeLevelId)),
                    1
                );
            }

            cmd.EndSample("RadianceCascade.Merge");
        }
    }
}

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

            for (int upperCascadeLevelId = 4; upperCascadeLevelId > 0; upperCascadeLevelId--)
            {
                var lowerCascadeRect = new Vector4(
                    0,
                    targetRT.height * Mathf.Pow(2.0f, -upperCascadeLevelId),
                    targetRT.width,
                    targetRT.height / (16f * Mathf.Pow(2.0f, upperCascadeLevelId))
                );
                var lowerCascadeUVBottom = Mathf.Pow(0.5f, upperCascadeLevelId);
                cmd.SetComputeFloatParam(_compute, "_LowerCascadeUVBottom", lowerCascadeUVBottom);
                cmd.SetComputeFloatParam(_compute, "_UpperCascadeUVBottom", lowerCascadeUVBottom * 0.5f);

                cmd.SetComputeIntParam(_compute, ShaderIds.CascadeLevel, upperCascadeLevelId);
                cmd.DispatchCompute(
                    _compute,
                    _mergKernel,
                    targetRT.width / 8,
                    targetRT.height / (8 * 1 << upperCascadeLevelId),
                    1
                );
            }

            cmd.EndSample("RadianceCascade.Merge");
        }
    }
}

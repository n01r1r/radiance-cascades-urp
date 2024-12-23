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
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, Color.red);

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
        }

        public void Merge(CommandBuffer cmd, int upperCascadeLevel, ref RTHandle target)
        {
            var targetRT = target.rt;
            var cascadeBufferSize = new Vector4(
                targetRT.width,
                targetRT.height,
                1.0f / targetRT.width,
                1.0f / targetRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeBufferSize);
            cmd.SetComputeTextureParam(_compute, _mergKernel, ShaderIds.OutCascade, target);

            cmd.SetComputeIntParam(_compute, ShaderIds.CascadeLevel, upperCascadeLevel);
            cmd.DispatchCompute(
                _compute,
                _mergKernel,
                targetRT.width / 8,
                targetRT.height / (8 * 1 << upperCascadeLevel),
                1
            );
        }
    }
}

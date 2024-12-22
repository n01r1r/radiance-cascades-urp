using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RCDirectionalFirstCS
    {
        private readonly ComputeShader _compute;
        private readonly int _renderKernel;

        public RCDirectionalFirstCS(ComputeShader compute)
        {
            _compute = compute;
            _renderKernel = _compute.FindKernel("RenderCascade");
        }

        public void Render(CommandBuffer cmd, RTHandle color, RTHandle depth, ref RTHandle target)
        {
            var colorRT = color.rt;
            var colorTexelSize = new Vector4(
                1.0f / colorRT.width,
                1.0f / colorRT.height,
                colorRT.width,
                colorRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.ColorTextureTexelSize, colorTexelSize);
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.ColorTexture, color);

            var targetRT = target.rt;
            var cascadeBufferSize = new Vector4(
                targetRT.width,
                targetRT.height,
                1.0f / targetRT.width,
                1.0f / targetRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeBufferSize);

            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                colorRT.width / 8,
                colorRT.height / 8,
                1
            );
        }
    }
}

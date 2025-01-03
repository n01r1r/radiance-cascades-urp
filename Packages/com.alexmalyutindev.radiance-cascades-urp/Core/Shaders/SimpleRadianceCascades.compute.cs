using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class SimpleRadianceCascadesCS
    {
        private readonly ComputeShader _compute;
        private readonly int _mergeKernel;
        private readonly int _renderKernel;

        public SimpleRadianceCascadesCS(ComputeShader compute)
        {
            _compute = compute;
            _mergeKernel = _compute.FindKernel("MergeCascades");
            _renderKernel = _compute.FindKernel("RenderCascade");
        }

        public void RenderCascade(
            CommandBuffer cmd,
            RTHandle color,
            RTHandle depth,
            int probeSize,
            int cascadeLevel,
            RTHandle target
        )
        {
            var rt = target.rt;
            var depthRT = depth.rt;

            cmd.SetComputeFloatParam(_compute, "_ProbeSize", probeSize);
            cmd.SetComputeFloatParam(_compute, "_CascadeLevel", cascadeLevel);
            var cascadeSize = new Vector4(rt.width, rt.height, 1.0f / rt.width, 1.0f / rt.height);
            cmd.SetComputeVectorParam(_compute, "_CascadeBufferSize", cascadeSize);

            cmd.SetComputeVectorParam(
                _compute,
                "_ColorTexture_TexelSize",
                new Vector4(depthRT.width, depthRT.height, 1.0f / depthRT.width, 1.0f / depthRT.height)
            );

            cmd.SetComputeTextureParam(_compute, _renderKernel, "_ColorTexture", color);
            cmd.SetComputeTextureParam(_compute, _renderKernel, "_DepthTexture", depth);

            // Output
            cmd.SetComputeTextureParam(_compute, _renderKernel, "_OutCascade", target);
            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                rt.width / 8,
                rt.height / 8,
                1
            );
        }

        public void MergeCascades(
            CommandBuffer cmd,
            RTHandle lower,
            RTHandle upper,
            int lowerCascadeLevel
        )
        {
            var rt = lower.rt;

            var cascadeSize = new Vector4(rt.width, rt.height, 1.0f / rt.width, 1.0f / rt.height);
            cmd.SetComputeVectorParam(_compute, "_CascadeBufferSize", cascadeSize);

            cmd.SetComputeFloatParam(_compute, "_LowerCascadeLevel", lowerCascadeLevel);

            cmd.SetComputeTextureParam(_compute, _mergeKernel, "_LowerCascade", lower);
            cmd.SetComputeTextureParam(_compute, _mergeKernel, "_UpperCascade", upper);

            cmd.DispatchCompute(
                _compute,
                _mergeKernel,
                rt.width / 8,
                rt.height / 8,
                1
            );
        }
    }
}

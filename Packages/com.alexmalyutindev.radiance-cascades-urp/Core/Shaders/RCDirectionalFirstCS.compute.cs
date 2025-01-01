using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RCDirectionalFirstCS
    {
        private readonly ComputeShader _compute;
        private readonly int _renderKernel;
        private readonly int _mergeKernel;

        public RCDirectionalFirstCS(ComputeShader compute)
        {
            _compute = compute;
            _renderKernel = _compute.FindKernel("RenderCascade");
            _mergeKernel = _compute.FindKernel("MergeCascade");
        }

        public void Render(
            CommandBuffer cmd,
            RTHandle color,
            RTHandle depth,
            RTHandle normals,
            RTHandle minMaxDepth,
            ref RTHandle target
        )
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
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.NormalsTexture, normals);
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.MinMaxDepth, minMaxDepth);

            var targetRT = target.rt;
            var cascadeBufferSize = new Vector4(
                targetRT.width,
                targetRT.height,
                1.0f / targetRT.width,
                1.0f / targetRT.height
            );
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeBufferSize);

            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.OutCascade, target);

            _compute.GetKernelThreadGroupSizes(_renderKernel, out var groupSizeX, out var groupSizeY, out _);
            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                targetRT.width / (int) groupSizeX,
                targetRT.height / (int) groupSizeY, // TODO: Spawn 4 times less threads for depth rays.
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
            cmd.SetComputeTextureParam(_compute, _mergeKernel, ShaderIds.LowerCascade, target);
            // NOTE: Bind same buffer to sample from it, cus LowerCascade in RWTexture.
            cmd.SetComputeTextureParam(_compute, _mergeKernel, ShaderIds.UpperCascade, target);

            cmd.SetRenderTarget(target);

            for (int lowerCascadeLevelId = 4; lowerCascadeLevelId >= 0; lowerCascadeLevelId--)
            {
                cmd.SetComputeFloatParam(
                    _compute,
                    "_LowerCascadeBottomCoord",
                    targetRT.height >> (lowerCascadeLevelId + 1)
                );
                cmd.SetComputeFloatParam(
                    _compute,
                    "_UpperCascadeBottomCoord",
                    targetRT.height >> (lowerCascadeLevelId + 2)
                );

                var lowerCascadeAngleCount = 8 * (1 << lowerCascadeLevelId);
                cmd.SetComputeFloatParam(_compute, "_LowerCascadeAnglesCount", lowerCascadeAngleCount);
                cmd.SetComputeFloatParam(_compute, "_UpperCascadeAnglesCount", lowerCascadeAngleCount * 2);

                cmd.SetComputeIntParam(_compute, ShaderIds.LowerCascadeLevel, lowerCascadeLevelId + 1);
                
                _compute.GetKernelThreadGroupSizes(_mergeKernel, out var x, out var y, out _);
                cmd.DispatchCompute(
                    _compute,
                    _mergeKernel,
                    targetRT.width / (int) x,
                    targetRT.height / ((int) y * (2 << lowerCascadeLevelId)),
                    1
                );
            }

            cmd.EndSample("RadianceCascade.Merge");
        }
    }
}

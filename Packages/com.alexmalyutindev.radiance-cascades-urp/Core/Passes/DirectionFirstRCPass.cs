using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class DirectionFirstRCPass : ScriptableRenderPass, IDisposable
    {
        private readonly RCDirectionalFirstCS _compute;
        private RTHandle _cascade0;

        public DirectionFirstRCPass(RadianceCascadeResources resources)
        {
            profilingSampler = new ProfilingSampler("RadianceCascades.DirectionFirst");
            _compute = new RCDirectionalFirstCS(resources.RadianceCascadesDirectionalFirstCS);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 512 => 512 / 8 = 64 probes in row
            var desc = new RenderTextureDescriptor(512 * 2, 256 * 2)
            {
                sRGB = false,
                enableRandomWrite = true,
            };
            RenderingUtils.ReAllocateIfNeeded(ref _cascade0, desc, name: "Cascade0");
            var renderer = renderingData.cameraData.renderer;

            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                _compute.Render(
                    cmd,
                    renderer.cameraColorTargetHandle,
                    renderer.cameraDepthTargetHandle,
                    ref _cascade0
                );

                cmd.BeginSample("Merge");
                _compute.Merge(cmd, 4, ref _cascade0);
                _compute.Merge(cmd, 3, ref _cascade0);
                _compute.Merge(cmd, 2, ref _cascade0);
                _compute.Merge(cmd, 1, ref _cascade0);
                cmd.EndSample("Merge");

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _cascade0?.Release();
        }
    }
}

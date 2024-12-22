using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RCDirectionalFirstPass : ScriptableRenderPass, IDisposable
    {
        private RCDirectionalFirstCS _compute;
        private RTHandle _cascade0;

        public RCDirectionalFirstPass(RadianceCascadeResources resources)
        {
            _compute = new RCDirectionalFirstCS(resources.RadianceCascadesDirectionalFirstCS);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var desc = new RenderTextureDescriptor(512, 256)
            {
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

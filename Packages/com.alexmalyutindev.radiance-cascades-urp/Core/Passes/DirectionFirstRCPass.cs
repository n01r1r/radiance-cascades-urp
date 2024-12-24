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
        private Material _blitMaterial;

        public DirectionFirstRCPass(RadianceCascadeResources resources)
        {
            profilingSampler = new ProfilingSampler("RadianceCascades.DirectionFirst");
            _compute = new RCDirectionalFirstCS(resources.RadianceCascadesDirectionalFirstCS);
            // TODO: Make proper C# wrapper for Blit/Combine shader!
            _blitMaterial = resources.BlitMaterial;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 512 => 512 / 8 = 64 probes in row
            var desc = new RenderTextureDescriptor(512 * 4, 256 * 4)
            {
                colorFormat = RenderTextureFormat.ARGBFloat,
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

                _compute.Merge(cmd, ref _cascade0);
                
                cmd.BeginSample("RadianceCascade.Combine");
                cmd.SetRenderTarget(renderer.cameraColorTargetHandle);
                BlitUtils.BlitTexture(cmd,_cascade0, _blitMaterial, 2);
                cmd.EndSample("RadianceCascade.Combine");

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

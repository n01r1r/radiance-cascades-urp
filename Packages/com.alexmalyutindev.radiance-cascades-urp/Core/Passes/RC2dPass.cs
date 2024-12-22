using System;
using AlexMalyutinDev.RadianceCascades;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RC2dPass : ScriptableRenderPass, IDisposable
{
    private const int CascadesCount = 5;
    private static readonly string[] CascadeNames = GenNames("_Cascade", CascadesCount);
    private static Vector2Int[] Resolutions =
    {
        new(32 * 16, 32 * 9), // 256x144 probes0
        new(32 * 10, 32 * 6), // 160x96 probes0
        new(32 * 7, 32 * 4), // 112x64 probes0
        new(32 * 4, 32 * 3), // 64x48 probes0
        new(32 * 3, 32 * 2), // 48x32 probes0
    };

    private readonly ProfilingSampler _profilingSampler;
    private readonly Material _blit;
    private readonly ComputeShader _radianceCascadesCs;
    private readonly RadianceCascadeCS _radianceCascadeCs;
    private readonly bool _showDebugPreview;

    private readonly RTHandle[] _cascades = new RTHandle[CascadesCount];


    public RC2dPass(
        RadianceCascadeResources resources,
        bool showDebugView
    )
    {
        _profilingSampler = new ProfilingSampler(nameof(RC2dPass));
        _radianceCascadeCs = new RadianceCascadeCS(resources.RadianceCascades);
        _showDebugPreview = showDebugView;
        _blit = resources.BlitMaterial;

        // BUG: Configuring with Depth and Color buffer dependency will cause to additional
        // resolve of this buffers before RadianceCascadesPass
        // ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // TODO: Resolution settings?
        var desc = new RenderTextureDescriptor(
            Resolutions[0].x,
            Resolutions[0].y
        )
        {
            colorFormat = RenderTextureFormat.ARGBHalf,
            enableRandomWrite = true,
        };

        for (int i = 0; i < _cascades.Length; i++)
        {
            RenderingUtils.ReAllocateIfNeeded(
                ref _cascades[i],
                desc,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp,
                name: CascadeNames[i]
            );
        }
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();

        var colorTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
        var depthTexture = renderingData.cameraData.renderer.cameraDepthTargetHandle;

        var colorTextureRT = colorTexture.rt;
        if (colorTextureRT == null)
        {
            return;
        }

        using (new ProfilingScope(cmd, _profilingSampler))
        {
            RenderCascades(renderingData, cmd, colorTexture, depthTexture);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }

    private void RenderCascades(
        RenderingData renderingData,
        CommandBuffer cmd,
        RTHandle colorTexture,
        RTHandle depthTexture
    )
    {
        var sampleKey = "RenderCascades";
        cmd.BeginSample(sampleKey);
        {
            for (int level = 0; level < _cascades.Length; level++)
            {
                // TODO: Use Hi-Z Depth
                _radianceCascadeCs.RenderCascade(
                    cmd,
                    colorTexture,
                    depthTexture,
                    2 << level,
                    level,
                    _cascades[level]
                );
            }
        }
        cmd.EndSample(sampleKey);

        if (_showDebugPreview)
        {
            PreviewCascades(cmd, _cascades);
        }

        sampleKey = "MergeCascades";
        cmd.BeginSample(sampleKey);
        {
            for (int level = _cascades.Length - 1; level > 0; level--)
            {
                var lowerLevel = level - 1;
                _radianceCascadeCs.MergeCascades(
                    cmd,
                    _cascades[lowerLevel],
                    _cascades[level],
                    lowerLevel
                );
            }
        }
        cmd.EndSample(sampleKey);

        if (_showDebugPreview)
        {
            PreviewCascades(cmd, _cascades, 1.0f);
        }

        sampleKey = "Combine";
        cmd.BeginSample(sampleKey);
        {
            cmd.SetRenderTarget(colorTexture, depthTexture);
            // TODO: Do blit into intermediate buffer with bilinear filter, then blit onto the screen
            BlitUtils.BlitTexture(cmd, _cascades[0], _blit, 0);
        }
        cmd.EndSample(sampleKey);
    }

    private void PreviewCascades(CommandBuffer cmd, RTHandle[] rtHandles, float offset = 0.0f)
    {
        cmd.BeginSample("Preview");

        const float scale = 1f / 8f;
        for (int i = 0; i < rtHandles.Length; i++)
        {
            Blitter.BlitQuad(
                cmd,
                rtHandles[i],
                new Vector4(1, 1f, 0, 0),
                new Vector4(scale, scale, scale * offset, 1.0f - scale * (i + 1)),
                0,
                false
            );
        }

        cmd.EndSample("Preview");
    }


    public void Dispose()
    {
        for (int i = 0; i < _cascades.Length; i++)
        {
            _cascades[i]?.Release();
        }
    }

    private static string[] GenNames(string name, int n)
    {
        var names = new string[n];
        for (int i = 0; i < n; i++)
        {
            names[i] = name + i;
        }

        return names;
    }
}

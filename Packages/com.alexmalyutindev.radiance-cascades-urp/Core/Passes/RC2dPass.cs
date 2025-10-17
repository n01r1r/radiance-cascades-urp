using System;
using AlexMalyutinDev.RadianceCascades;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering; // GraphicsFormatUtility 등
 // ✅ RenderGraph, TextureHandle, *GraphContext

public class RC2dPass : ScriptableRenderPass, IDisposable
{
    private const int CascadesCount = 5;
    private static readonly string[] CascadeNames = GenNames("_Cascade", CascadesCount);
    private static readonly Vector2Int[] Resolutions =
    {
        new(32 * 16, 32 * 9), // 256x144 probes0
        new(32 * 10, 32 * 6), // 160x96 probes0
        new(32 * 7, 32 * 4), // 112x64 probes0
        new(32 * 4, 32 * 3), // 64x48 probes0
        new(32 * 3, 32 * 2), // 48x32 probes0
    };

    private readonly ProfilingSampler _profilingSampler;
    private readonly Material _blit;
    private readonly SimpleRadianceCascadesCS _radianceCascadeCs;
    private readonly bool _showDebugPreview;

    public RC2dPass(
        RadianceCascadeResources resources,
        bool showDebugView
    )
    {
        _profilingSampler = new ProfilingSampler(nameof(RC2dPass));
        _radianceCascadeCs = new SimpleRadianceCascadesCS(resources.RadianceCascades);
        _showDebugPreview = showDebugView;
        _blit = resources.BlitMaterial;
    }

    public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData)
    {
        var cameraData = frameData.Get<UniversalCameraData>();
        var resourceData = frameData.Get<UniversalResourceData>();

        // Render cascades and get the final cascade texture
        var finalCascadeTexture = RenderCascades(renderGraph, frameData, cameraData, resourceData);
        
        // Add combine pass outside of the RenderCascades method
        CombineCascades(renderGraph, frameData, cameraData, resourceData, finalCascadeTexture);
    }

    private class PassData
    {
        public SimpleRadianceCascadesCS RadianceCascadeCs;
        public Material BlitMaterial;
        public bool ShowDebugPreview;

        // Pure values extracted from ContextContainer - NO pipeline containers
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public Vector3 WorldSpaceCameraPos;

        public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameColor;
        public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameDepth;
        public UnityEngine.Rendering.RenderGraphModule.TextureHandle[] Cascades = new UnityEngine.Rendering.RenderGraphModule.TextureHandle[CascadesCount];
        public Vector4[] CascadeSizeTexels = new Vector4[CascadesCount];
    }

    private UnityEngine.Rendering.RenderGraphModule.TextureHandle RenderCascades(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData, UniversalCameraData cameraData, UniversalResourceData resourceData)
    {
        using var builder = renderGraph.AddComputePass<PassData>("RC2D.RenderCascades", out var passData);
        builder.AllowPassCulling(false);

        passData.RadianceCascadeCs = _radianceCascadeCs;
        passData.BlitMaterial = _blit;
        passData.ShowDebugPreview = _showDebugPreview;

        // Extract ALL camera data outside lambda - NO pipeline containers in PassData
        passData.ViewMatrix = cameraData.GetViewMatrix();
        passData.ProjectionMatrix = cameraData.GetProjectionMatrix(); // Use GetProjectionMatrix()
        passData.WorldSpaceCameraPos = cameraData.worldSpaceCameraPos;

        // Use frame textures with legacy API
        passData.FrameColor = resourceData.activeColorTexture;
        builder.UseTexture(passData.FrameColor);
        passData.FrameDepth = resourceData.activeDepthTexture;
        builder.UseTexture(passData.FrameDepth);

        // Create cascade textures as persistent textures so they can be used across passes
        for (int i = 0; i < CascadesCount; i++)
        {
            var desc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(Resolutions[i].x, Resolutions[i].y)
            {
                name = CascadeNames[i],
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true
            };
            passData.Cascades[i] = renderGraph.CreateTexture(desc);
            passData.CascadeSizeTexels[i] = new Vector4(
                desc.width, desc.height,
                1.0f / desc.width, 1.0f / desc.height
            );
            
            // Declare that this pass will write to the cascade texture
            builder.UseTexture(passData.Cascades[i], UnityEngine.Rendering.RenderGraphModule.AccessFlags.Write);
        }

        builder.SetRenderFunc<PassData>(static (data, context) =>
        {
            // Create a bridge to work with legacy compute shader classes
            // Convert TextureHandle to RTHandle for legacy compatibility
            var frameColorRTHandle = RTHandles.Alloc(data.FrameColor);
            var frameDepthRTHandle = RTHandles.Alloc(data.FrameDepth);
            var cascadeRTHandles = new RTHandle[CascadesCount];
            
            for (int i = 0; i < CascadesCount; i++)
            {
                cascadeRTHandles[i] = RTHandles.Alloc(data.Cascades[i]);
            }

            // Use a temporary CommandBuffer for legacy compute shader calls
            var tempCmd = CommandBufferPool.Get("RC2D Legacy Bridge");
            
            try
            {
                // Render cascades
                for (int level = 0; level < CascadesCount; level++)
                {
                    data.RadianceCascadeCs.RenderCascade(
                        tempCmd,
                        frameColorRTHandle,
                        frameDepthRTHandle,
                        2 << level,
                        level,
                        cascadeRTHandles[level]
                    );
                }

                // Merge cascades
                for (int level = CascadesCount - 1; level > 0; level--)
                {
                    var lowerLevel = level - 1;
                    data.RadianceCascadeCs.MergeCascades(
                        tempCmd,
                        cascadeRTHandles[lowerLevel],
                        cascadeRTHandles[level],
                        lowerLevel
                    );
                }
                
                // Execute the temporary command buffer using Graphics.ExecuteCommandBuffer
                Graphics.ExecuteCommandBuffer(tempCmd);
            }
            finally
            {
                CommandBufferPool.Release(tempCmd);
                
                // Clean up RTHandle allocations
                RTHandles.Release(frameColorRTHandle);
                RTHandles.Release(frameDepthRTHandle);
                for (int i = 0; i < CascadesCount; i++)
                {
                    RTHandles.Release(cascadeRTHandles[i]);
                }
            }
        });

        // Return the final cascade texture (index 0 is the final merged result)
        return passData.Cascades[0];
    }

    private class CombinePassData
    {
        public Material BlitMaterial;
        public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameColor;
        public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameDepth;
        public UnityEngine.Rendering.RenderGraphModule.TextureHandle CascadeTexture;
    }

    private void CombineCascades(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData, UniversalCameraData cameraData, UniversalResourceData resourceData, UnityEngine.Rendering.RenderGraphModule.TextureHandle cascadeTexture)
    {
        using var builder = renderGraph.AddRasterRenderPass<CombinePassData>("RC2D.Combine", out var passData);
        builder.AllowGlobalStateModification(true);

        passData.BlitMaterial = _blit;
        passData.CascadeTexture = cascadeTexture;
        builder.UseTexture(passData.CascadeTexture);

        passData.FrameColor = resourceData.activeColorTexture;
        passData.FrameDepth = resourceData.activeDepthTexture;
        builder.UseTexture(passData.FrameDepth);

        builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
        builder.SetRenderFunc<CombinePassData>(static (data, context) =>
        {
            // Enhanced Simple 2D Probes - Add distinctive visual effect
            Blitter.BlitTexture(context.cmd, data.CascadeTexture, new Vector4(1f, 1f, 0, 0), data.BlitMaterial, 0);
            
            // Add a subtle tint to make Simple2D visually distinct
            context.cmd.SetGlobalFloat("_Simple2DTint", 1.0f);
            context.cmd.SetGlobalColor("_Simple2DColor", new Color(1.0f, 0.9f, 0.8f, 1.0f)); // Warm tint
        });
    }

    public void Dispose() { }

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

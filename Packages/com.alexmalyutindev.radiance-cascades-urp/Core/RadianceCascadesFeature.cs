using AlexMalyutinDev.RadianceCascades;
using AlexMalyutinDev.RadianceCascades.MinMaxDepth;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesFeature : ScriptableRendererFeature
{
    public RadianceCascadeResources Resources;

    public bool showDebugView;

    private RC2dPass _rc2dPass;
    private RadianceCascades3dPass _radianceCascadesPass3d;
    private DirectionFirstRCPass _directionFirstRcPass;
    private VoxelizationPass _voxelizationPass;

    private MinMaxDepthPass _minMaxDepthPass;

    private RadianceCascadesRenderingData _radianceCascadesRenderingData;

    public override void Create()
    {
        _radianceCascadesRenderingData = new RadianceCascadesRenderingData();

        _rc2dPass = new RC2dPass(Resources, showDebugView)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };

        _voxelizationPass = new VoxelizationPass(Resources, _radianceCascadesRenderingData)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingShadows,
        };
        _radianceCascadesPass3d = new RadianceCascades3dPass(Resources, _radianceCascadesRenderingData)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };

        _minMaxDepthPass = new MinMaxDepthPass(Resources.MinMaxDepthMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingGbuffer
        };
        _directionFirstRcPass = new DirectionFirstRCPass(Resources)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isPreviewCamera)
        {
            return;
        }

        var volume = VolumeManager.instance.stack.GetComponent<RadianceCascades>();
        var renderType = volume.RenderType.value;
        if (!volume.active || renderType == RenderType.None)
        {
            return;
        }

        if (renderType == RenderType.Simple2d)
        {
            renderer.EnqueuePass(_rc2dPass);
        }
        else if (renderType == RenderType.HemisphereProbes3d)
        {
            renderer.EnqueuePass(_voxelizationPass);
            renderer.EnqueuePass(_radianceCascadesPass3d);
        }
        else if (renderType == RenderType.DirectionalFirst2d)
        {
            renderer.EnqueuePass(_minMaxDepthPass);
            renderer.EnqueuePass(_directionFirstRcPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        _rc2dPass?.Dispose();
        _radianceCascadesPass3d?.Dispose();
        _voxelizationPass?.Dispose();
        _minMaxDepthPass?.Dispose();
    }
}

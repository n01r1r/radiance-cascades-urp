using AlexMalyutinDev.RadianceCascades.MinMaxDepth;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
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

            _minMaxDepthPass = new MinMaxDepthPass(Resources.MinMaxDepthMaterial, _radianceCascadesRenderingData)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingGbuffer
            };
            _directionFirstRcPass = new DirectionFirstRCPass(Resources, _radianceCascadesRenderingData)
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
            var renderType = volume.RenderingType.value;
            if (!volume.active || renderType == RenderingType.None)
            {
                return;
            }

            if (renderType == RenderingType.Simple2d)
            {
                renderer.EnqueuePass(_rc2dPass);
            }
            else if (renderType == RenderingType.HemisphereProbes3d)
            {
                renderer.EnqueuePass(_voxelizationPass);
                renderer.EnqueuePass(_radianceCascadesPass3d);
            }
            else if (renderType == RenderingType.DirectionalFirst2d)
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
}

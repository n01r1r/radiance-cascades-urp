using AlexMalyutinDev.RadianceCascades.SmoothedDepth;
using UnityEngine;
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
        private RC3dProbesPass _rc3dProbesPass; // New enhanced 3D Probes pass
        private DirectionFirstRCPass _directionFirstRcPass;
        private VoxelizationPass _voxelizationPass;

        private MinMaxDepthPass _minMaxDepthPass;
        private SmoothedDepthPass _smoothedDepthPass;
        private VarianceDepthPass _varianceDepthPass;
        private BlurredColorBufferPass _blurredColorBufferPass;

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

            // Enhanced RC 3D Probes Pass
            _rc3dProbesPass = new RC3dProbesPass(Resources, _radianceCascadesRenderingData)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingShadows
            };

            // Direction First Passes
            _minMaxDepthPass = new MinMaxDepthPass(Resources.MinMaxDepthMaterial, _radianceCascadesRenderingData)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingGbuffer
            };
            _smoothedDepthPass = new SmoothedDepthPass(Resources.SmoothedDepthMaterial, _radianceCascadesRenderingData)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingGbuffer
            };
            _varianceDepthPass = new VarianceDepthPass(Resources.VarianceDepthMaterial, _radianceCascadesRenderingData)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingGbuffer
            };
            _blurredColorBufferPass = new BlurredColorBufferPass(
                Resources.BlurredColorBufferMaterial,
                _radianceCascadesRenderingData
            )
            {
                renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
            };
            _directionFirstRcPass = new DirectionFirstRCPass(Resources)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Unity 6: Enqueue RenderGraph passes here
            var volume = VolumeManager.instance.stack.GetComponent<RadianceCascades>();
            if (volume == null || !volume.active)
            {
                return;
            }
            
            var renderType = volume.RenderingType.value;
            if (renderType == RenderingType.None)
            {
                return;
            }
            
            Debug.Log($"RadianceCascades: Enqueuing passes for rendering type: {renderType}");

            // TODO: Refactor render target size! Only used in MinMaxDepthPass and BlurredColorBufferPass!
            _radianceCascadesRenderingData.Cascade0Size = new Vector2Int(2048 / 8, 1024 / 8);

            if (renderType == RenderingType.Simple2dProbes)
            {
                Debug.Log("RadianceCascades: Enqueuing Simple2D Probes passes");
                renderer.EnqueuePass(_rc2dPass);
            }
            else if (renderType == RenderingType.CubeMapProbes)
            {
                Debug.Log("RadianceCascades: Enqueuing CubeMap Probes passes");
                renderer.EnqueuePass(_voxelizationPass);
                renderer.EnqueuePass(_radianceCascadesPass3d);
            }
            else if (renderType == RenderingType.DirectionFirstProbes)
            {
                Debug.Log("RadianceCascades: Enqueuing Direction-First Probes passes");
                renderer.EnqueuePass(_minMaxDepthPass);
                renderer.EnqueuePass(_varianceDepthPass);
                renderer.EnqueuePass(_blurredColorBufferPass);
                renderer.EnqueuePass(_directionFirstRcPass);
            }
            else if (renderType == RenderingType.Probes3D)
            {
                Debug.Log("RadianceCascades: Enqueuing Enhanced RC 3D Probes passes");
                renderer.EnqueuePass(_rc3dProbesPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _rc2dPass?.Dispose();
            _radianceCascadesPass3d?.Dispose();
            _rc3dProbesPass?.Dispose();
            _voxelizationPass?.Dispose();
            _minMaxDepthPass?.Dispose();
        }
    }
}

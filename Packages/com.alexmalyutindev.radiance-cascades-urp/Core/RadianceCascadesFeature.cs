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

        // Only DirectionFirst Probes - removed all other rendering types
        private DirectionFirstRCPass _directionFirstRcPass;
        private MinMaxDepthPass _minMaxDepthPass;
        private SmoothedDepthPass _smoothedDepthPass;
        private VarianceDepthPass _varianceDepthPass;
        private BlurredColorBufferPass _blurredColorBufferPass;

        private RadianceCascadesRenderingData _radianceCascadesRenderingData;

        public override void Create()
        {
            _radianceCascadesRenderingData = new RadianceCascadesRenderingData();

            // Direction First Passes only
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
            
            Debug.Log($"RadianceCascades: Enqueuing DirectionFirst Probes passes");

            // TODO: Refactor render target size! Only used in MinMaxDepthPass and BlurredColorBufferPass!
            _radianceCascadesRenderingData.Cascade0Size = new Vector2Int(2048 / 8, 1024 / 8);

            // Only DirectionFirst Probes - perfect for APV comparison
            if (renderType == RenderingType.DirectionFirstProbes)
            {
                Debug.Log("RadianceCascades: Enqueuing Direction-First Probes passes");
                renderer.EnqueuePass(_minMaxDepthPass);
                renderer.EnqueuePass(_varianceDepthPass);
                renderer.EnqueuePass(_blurredColorBufferPass);
                renderer.EnqueuePass(_directionFirstRcPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            // Only dispose passes that implement IDisposable
            _minMaxDepthPass?.Dispose();
            _directionFirstRcPass?.Dispose();
            // SmoothedDepthPass, VarianceDepthPass, BlurredColorBufferPass don't implement IDisposable
        }
    }
}

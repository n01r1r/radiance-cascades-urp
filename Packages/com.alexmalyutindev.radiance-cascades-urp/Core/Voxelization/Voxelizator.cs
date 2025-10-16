using UnityEngine;
using UnityEngine.Rendering;

namespace RadianceCascades.Voxelization
{
    public class Voxelizator : MonoBehaviour
    {
        [Header("Voxelization Settings")]
        public float voxelSize = 0.1f;
        public int maxVoxels = 1024 * 1024;
        public LayerMask cullingMask = -1;
        
        private Camera voxelCamera;
        private RenderTexture[] voxelTextures = new RenderTexture[3];
        
        private void Awake()
        {
            SetupVoxelCamera();
            SetupVoxelTextures();
        }
        
        private void SetupVoxelCamera()
        {
            GameObject cameraGO = new GameObject("VoxelCamera");
            cameraGO.transform.SetParent(transform);
            voxelCamera = cameraGO.AddComponent<Camera>();
            
            voxelCamera.orthographic = true;
            voxelCamera.nearClipPlane = 0.1f;
            voxelCamera.farClipPlane = 20f;
            voxelCamera.cullingMask = cullingMask;
            voxelCamera.enabled = false;
        }
        
        private void SetupVoxelTextures()
        {
            int resolution = 512;
            
            for (int i = 0; i < 3; i++)
            {
                voxelTextures[i] = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
                voxelTextures[i].enableRandomWrite = true;
                voxelTextures[i].Create();
            }
        }
        
        public void Voxelize(CommandBuffer cmd, RadianceCascades.Core.RadianceCascadeResources resources, RadianceCascades.Core.RadianceCascadesRenderingData renderingData)
        {
            if (resources.voxelizatorCompute == null)
                return;
                
            // Clear voxel data buffer
            cmd.SetComputeBufferParam(resources.voxelizatorCompute, 0, "_VoxelDataBuffer", resources.voxelDataBuffer);
            cmd.DispatchCompute(resources.voxelizatorCompute, 0, 1, 1, 1);
            
            // Render from 3 directions
            Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.up };
            Vector3[] positions = { Vector3.back * 10, Vector3.left * 10, Vector3.down * 10 };
            
            for (int i = 0; i < 3; i++)
            {
                RenderDirection(cmd, directions[i], positions[i], i, resources, renderingData);
            }
            
            // Aggregate voxel data
            int aggregateKernel = resources.voxelizatorCompute.FindKernel("VoxelAggregate");
            cmd.SetComputeBufferParam(resources.voxelizatorCompute, aggregateKernel, "_VoxelDataBuffer", resources.voxelDataBuffer);
            cmd.SetComputeTextureParam(resources.voxelizatorCompute, aggregateKernel, "_SceneVolume", renderingData.sceneVolume);
            cmd.SetComputeIntParam(resources.voxelizatorCompute, "_VolumeResolution", renderingData.sceneVolumeResolution);
            
            int groups = (renderingData.sceneVolumeResolution + 3) / 4;
            cmd.DispatchCompute(resources.voxelizatorCompute, aggregateKernel, groups, groups, groups);
        }
        
        private void RenderDirection(CommandBuffer cmd, Vector3 direction, Vector3 position, int index, RadianceCascades.Core.RadianceCascadeResources resources, RadianceCascades.Core.RadianceCascadesRenderingData renderingData)
        {
            // Setup camera
            voxelCamera.transform.position = position;
            voxelCamera.transform.LookAt(position + direction);
            voxelCamera.orthographicSize = 10f;
            
            // Render to voxel texture
            cmd.SetRenderTarget(voxelTextures[index]);
            cmd.ClearRenderTarget(true, true, Color.black);
            
            // Render scene using Unity 6 compatible API
            var cullingResults = new ScriptableCullingParameters();
            if (voxelCamera.TryGetCullingParameters(out cullingResults))
            {
                // Use ScriptableRenderContext instead of RenderSingleCamera
                var context = new ScriptableRenderContext();
                var cullingResults2 = context.Cull(ref cullingResults);
                
                // Draw opaque objects with Unity 6 compatible shader tag
                var sortingSettings = new SortingSettings(voxelCamera) { criteria = SortingCriteria.CommonOpaque };
                var drawingSettings = new DrawingSettings(ShaderTagId.none, sortingSettings);
                drawingSettings.SetShaderPassName(0, new ShaderTagId("UniversalForward"));
                var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                
                cmd.DrawRenderers(cullingResults2, ref drawingSettings, ref filteringSettings);
            }
            
            // Process voxel data
            int processKernel = resources.voxelizatorCompute.FindKernel("VoxelProcess");
            cmd.SetComputeTextureParam(resources.voxelizatorCompute, processKernel, "_VoxelTexture", voxelTextures[index]);
            cmd.SetComputeBufferParam(resources.voxelizatorCompute, processKernel, "_VoxelDataBuffer", resources.voxelDataBuffer);
            cmd.SetComputeIntParam(resources.voxelizatorCompute, "_Direction", index);
            cmd.SetComputeMatrixParam(resources.voxelizatorCompute, "_WorldToVolume", renderingData.worldToSceneVolume);
            
            cmd.DispatchCompute(resources.voxelizatorCompute, processKernel, 64, 64, 1);
        }
        
        private void OnDestroy()
        {
            if (voxelTextures != null)
            {
                for (int i = 0; i < voxelTextures.Length; i++)
                {
                    if (voxelTextures[i] != null)
                    {
                        voxelTextures[i].Release();
                        voxelTextures[i] = null;
                    }
                }
            }
        }
    }
}

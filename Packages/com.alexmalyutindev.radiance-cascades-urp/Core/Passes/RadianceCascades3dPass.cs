using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RadianceCascades.Core;

namespace RadianceCascades.Passes
{
    public class RadianceCascades3dPass : ScriptableRenderPass
    {
        private RadianceCascadeResources resources;
        private RadianceCascadesRenderingData renderingData;
        private RadianceCascades.Core.RadianceCascades volumeComponent;
        
        public void Setup(RadianceCascadeResources resources, RadianceCascadesRenderingData renderingData, RadianceCascades.Core.RadianceCascades volumeComponent)
        {
            this.resources = resources;
            this.renderingData = renderingData;
            this.volumeComponent = volumeComponent;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (resources?.radianceCascades3dCompute == null)
                return;
                
            CommandBuffer cmd = CommandBufferPool.Get("RadianceCascades3D");
            
            var compute = resources.radianceCascades3dCompute;
            
            // Set global properties
            cmd.SetGlobalMatrix(ShaderIds._View, renderingData.cameraData.camera.worldToCameraMatrix);
            cmd.SetGlobalMatrix(ShaderIds._ViewProjection, renderingData.cameraData.camera.projectionMatrix * renderingData.cameraData.camera.worldToCameraMatrix);
            cmd.SetGlobalMatrix(ShaderIds._InvViewProjection, (renderingData.cameraData.camera.projectionMatrix * renderingData.cameraData.camera.worldToCameraMatrix).inverse);
            cmd.SetGlobalMatrix(ShaderIds._WorldToSceneVolume, renderingData.worldToSceneVolume);
            cmd.SetGlobalMatrix(ShaderIds._InvWorldToSceneVolume, renderingData.invWorldToSceneVolume);
            
            // Unity 6 compatible camera target access
            var renderer = renderingData.cameraData.renderer;
            var colorTarget = renderer.GetOpaqueTexture();
            var depthTarget = renderer.GetDepthTexture();
            
            cmd.SetGlobalTexture(ShaderIds._ColorTexture, colorTarget);
            cmd.SetGlobalTexture(ShaderIds._DepthTexture, depthTarget);
            cmd.SetGlobalTexture(ShaderIds._SceneVolume, renderingData.sceneVolume);
            
            // 1. Clear all cascades
            ExecuteClear(cmd, compute);
            
            // 2. Inject into cascade 0
            ExecuteInject(cmd, compute, 0);
            
            // 3. Propagate cascade 0
            ExecutePropagate(cmd, compute, 0);
            
            // 4. Downsample cascades
            for (int i = 0; i < renderingData.cascadeCount - 1; i++)
            {
                ExecuteDownsample(cmd, compute, i, i + 1);
            }
            
            // 5. Propagate all cascades
            for (int i = 0; i < renderingData.cascadeCount; i++)
            {
                ExecutePropagate(cmd, compute, i);
            }
            
            // 6. Composite
            ExecuteComposite(cmd, compute);
            
            // 7. Final composite with base color
            ExecuteFinalComposite(cmd);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        private void ExecuteClear(CommandBuffer cmd, ComputeShader compute)
        {
            int kernel = compute.FindKernel("RC3D_Clear");
            
            for (int i = 0; i < renderingData.cascadeCount; i++)
            {
                cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeLobeIds[i], renderingData.cascadeLobes[i]);
                cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeBetaIds[i], renderingData.cascadeBetas[i]);
            }
            
            cmd.SetComputeIntParam(compute, ShaderIds._CascadeLevel, renderingData.cascadeCount);
            
            int size = renderingData.cascadeLobes[0].width;
            cmd.DispatchCompute(compute, kernel, (size + 7) / 8, (size + 7) / 8, 1);
        }
        
        private void ExecuteInject(CommandBuffer cmd, ComputeShader compute, int cascadeLevel)
        {
            int kernel = compute.FindKernel("RC3D_Inject");
            
            cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeLobeIds[cascadeLevel], renderingData.cascadeLobes[cascadeLevel]);
            cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeBetaIds[cascadeLevel], renderingData.cascadeBetas[cascadeLevel]);
            cmd.SetComputeIntParam(compute, kernel, ShaderIds._CascadeLevel, cascadeLevel);
            
            int size = renderingData.cascadeLobes[cascadeLevel].width;
            cmd.DispatchCompute(compute, kernel, (size + 7) / 8, (size + 7) / 8, 1);
        }
        
        private void ExecutePropagate(CommandBuffer cmd, ComputeShader compute, int cascadeLevel)
        {
            // X propagation
            int kernelX = compute.FindKernel("RC3D_PropagateX");
            cmd.SetComputeTextureParam(compute, kernelX, ShaderIds.CascadeLobeIds[cascadeLevel], renderingData.cascadeLobes[cascadeLevel]);
            cmd.SetComputeTextureParam(compute, kernelX, ShaderIds.CascadeBetaIds[cascadeLevel], renderingData.cascadeBetas[cascadeLevel]);
            cmd.SetComputeIntParam(compute, kernelX, ShaderIds._CascadeLevel, cascadeLevel);
            
            int size = renderingData.cascadeLobes[cascadeLevel].width;
            cmd.DispatchCompute(compute, kernelX, (size + 7) / 8, (size + 7) / 8, 1);
            
            // Y propagation
            int kernelY = compute.FindKernel("RC3D_PropagateY");
            cmd.SetComputeTextureParam(compute, kernelY, ShaderIds.CascadeLobeIds[cascadeLevel], renderingData.cascadeLobes[cascadeLevel]);
            cmd.SetComputeTextureParam(compute, kernelY, ShaderIds.CascadeBetaIds[cascadeLevel], renderingData.cascadeBetas[cascadeLevel]);
            cmd.SetComputeIntParam(compute, kernelY, ShaderIds._CascadeLevel, cascadeLevel);
            
            cmd.DispatchCompute(compute, kernelY, (size + 7) / 8, (size + 7) / 8, 1);
            
            // Z propagation
            int kernelZ = compute.FindKernel("RC3D_PropagateZ");
            cmd.SetComputeTextureParam(compute, kernelZ, ShaderIds.CascadeLobeIds[cascadeLevel], renderingData.cascadeLobes[cascadeLevel]);
            cmd.SetComputeTextureParam(compute, kernelZ, ShaderIds.CascadeBetaIds[cascadeLevel], renderingData.cascadeBetas[cascadeLevel]);
            cmd.SetComputeIntParam(compute, kernelZ, ShaderIds._CascadeLevel, cascadeLevel);
            
            cmd.DispatchCompute(compute, kernelZ, (size + 7) / 8, (size + 7) / 8, 1);
        }
        
        private void ExecuteDownsample(CommandBuffer cmd, ComputeShader compute, int srcLevel, int dstLevel)
        {
            int kernel = compute.FindKernel("RC3D_Downsample");
            
            cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeLobeIds[srcLevel], renderingData.cascadeLobes[srcLevel]);
            cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeBetaIds[srcLevel], renderingData.cascadeBetas[srcLevel]);
            cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeLobeIds[dstLevel], renderingData.cascadeLobes[dstLevel]);
            cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeBetaIds[dstLevel], renderingData.cascadeBetas[dstLevel]);
            
            cmd.SetComputeIntParam(compute, kernel, ShaderIds._CascadeLevel, dstLevel);
            
            int size = renderingData.cascadeLobes[dstLevel].width;
            cmd.DispatchCompute(compute, kernel, (size + 7) / 8, (size + 7) / 8, 1);
        }
        
        private void ExecuteComposite(CommandBuffer cmd, ComputeShader compute)
        {
            int kernel = compute.FindKernel("RC3D_Composite");
            
            for (int i = 0; i < renderingData.cascadeCount; i++)
            {
                cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeLobeIds[i], renderingData.cascadeLobes[i]);
                cmd.SetComputeTextureParam(compute, kernel, ShaderIds.CascadeBetaIds[i], renderingData.cascadeBetas[i]);
            }
            
            cmd.SetComputeTextureParam(compute, kernel, ShaderIds._EIndirect, renderingData.eIndirect);
            cmd.SetComputeIntParam(compute, kernel, ShaderIds._CascadeLevel, renderingData.cascadeCount);
            
            int width = renderingData.eIndirect.width;
            int height = renderingData.eIndirect.height;
            cmd.DispatchCompute(compute, kernel, (width + 7) / 8, (height + 7) / 8, 1);
        }
        
        private void ExecuteFinalComposite(CommandBuffer cmd)
        {
            if (resources.rcCompositeMaterial == null)
                return;
                
            // Unity 6 compatible camera target access for final composite
            var renderer = renderingData.cameraData.renderer;
            var colorTarget = renderer.GetOpaqueTexture();
            
            cmd.SetGlobalTexture(ShaderIds._BaseColorTex, colorTarget);
            cmd.SetGlobalTexture(ShaderIds._EIndirect, renderingData.eIndirect);
            
            BlitUtils.Blit(cmd, colorTarget, colorTarget, resources.rcCompositeMaterial);
        }
    }
}

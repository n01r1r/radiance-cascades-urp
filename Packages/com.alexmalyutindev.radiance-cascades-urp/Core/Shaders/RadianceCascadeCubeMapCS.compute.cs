using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadeCubeMapCS
    {
        private readonly ComputeShader _compute;
        private readonly int _renderKernel;
        private readonly int _mergeKernel;

        public RadianceCascadeCubeMapCS(ComputeShader compute)
        {
            _compute = compute;
            _renderKernel = _compute.FindKernel("RenderCascade");
            _mergeKernel = _compute.FindKernel("MergeCascade");
        }

        // Overload for RenderGraph with extracted matrices (NO pipeline containers)
        public void RenderCascade(
            CommandBuffer cmd,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            RadianceCascadesRenderingData radianceCascadesRenderingData,
            RTHandle color,
            RTHandle depth,
            int probeSize,
            int cascadeLevel,
            RTHandle target
        )
        {
            var rt = target.rt;
            var depthRT = depth.rt;

            cmd.SetComputeFloatParam(_compute, ShaderIds.ProbeSize, probeSize);
            cmd.SetComputeFloatParam(_compute, ShaderIds.CascadeLevel, cascadeLevel);
            var cascadeSize = new Vector4(rt.width, rt.height, 1.0f / rt.width, 1.0f / rt.height);
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeSize);

            var viewProj = viewMatrix * projectionMatrix;
            cmd.SetComputeMatrixParam(_compute, ShaderIds.View, viewMatrix);
            cmd.SetComputeMatrixParam(_compute, ShaderIds.ViewProjection, viewProj);
            cmd.SetComputeMatrixParam(_compute, ShaderIds.InvViewProjection, viewProj.inverse);

            cmd.SetComputeVectorParam(
                _compute,
                ShaderIds.ColorTextureTexelSize,
                new Vector4(depthRT.width, depthRT.height, 1.0f / depthRT.width, 1.0f / depthRT.height)
            );

            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.ColorTexture, color);
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.DepthTexture, depth);

            cmd.SetComputeMatrixParam(_compute, "_WorldToSceneVolume", radianceCascadesRenderingData.WorldToVolume);
            cmd.SetComputeMatrixParam(_compute, "_InvWorldToSceneVolume", radianceCascadesRenderingData.WorldToVolume.inverse);
            
            // Unity 6 RenderGraph: Use TextureHandle directly, no .rt access
            // Set default texel size for SceneVolume (will be overridden by shader if needed)
            var sceneVolumeTexelSize = new Vector4(1.0f / 128f, 1.0f / 128f, 128f, 128f);
            cmd.SetComputeVectorParam(_compute, "_SceneVolume_TexelSize", sceneVolumeTexelSize);
            
            // Bind SceneVolume TextureHandle directly to compute shader
            cmd.SetComputeTextureParam(
                _compute,
                _renderKernel,
                ShaderIds.SceneVolume,
                radianceCascadesRenderingData.SceneVolume
            );

            // Output
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.OutCascade, target);
            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                rt.width / 8,
                rt.height / 8,
                1
            );
        }

        // Overload for RenderGraph with UniversalCameraData (DEPRECATED - use matrix version)
        public void RenderCascade(
            CommandBuffer cmd,
            UniversalCameraData cameraData,
            RadianceCascadesRenderingData radianceCascadesRenderingData,
            RTHandle color,
            RTHandle depth,
            int probeSize,
            int cascadeLevel,
            RTHandle target
        )
        {
            var rt = target.rt;
            var depthRT = depth.rt;

            cmd.SetComputeFloatParam(_compute, ShaderIds.ProbeSize, probeSize);
            cmd.SetComputeFloatParam(_compute, ShaderIds.CascadeLevel, cascadeLevel);
            var cascadeSize = new Vector4(rt.width, rt.height, 1.0f / rt.width, 1.0f / rt.height);
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeSize);

            var view = cameraData.GetViewMatrix();
            var proj = cameraData.GetGPUProjectionMatrix();

            var viewProj = view * proj;
            cmd.SetComputeMatrixParam(_compute, ShaderIds.View, view);
            cmd.SetComputeMatrixParam(_compute, ShaderIds.ViewProjection, viewProj);
            cmd.SetComputeMatrixParam(_compute, ShaderIds.InvViewProjection, viewProj.inverse);

            cmd.SetComputeVectorParam(
                _compute,
                ShaderIds.ColorTextureTexelSize,
                new Vector4(depthRT.width, depthRT.height, 1.0f / depthRT.width, 1.0f / depthRT.height)
            );

            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.ColorTexture, color);
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.DepthTexture, depth);

            cmd.SetComputeMatrixParam(_compute, "_WorldToSceneVolume", radianceCascadesRenderingData.WorldToVolume);
            cmd.SetComputeMatrixParam(_compute, "_InvWorldToSceneVolume", radianceCascadesRenderingData.WorldToVolume.inverse);
            var volumeRT = radianceCascadesRenderingData.SceneVolume.rt;
            var sceneVolumeTexelSize = new Vector4(
                1.0f / volumeRT.width,
                1.0f / volumeRT.height,
                volumeRT.width,
                volumeRT.height
            );
            cmd.SetComputeVectorParam(_compute, "_SceneVolume_TexelSize", sceneVolumeTexelSize);
            cmd.SetComputeTextureParam(
                _compute,
                _renderKernel,
                ShaderIds.SceneVolume,
                radianceCascadesRenderingData.SceneVolume
            );


            // Output
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.OutCascade, target);
            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                rt.width / 8,
                rt.height / 8,
                1
            );
        }

        // Legacy overload for compatibility
        public void RenderCascade(
            CommandBuffer cmd,
            ref RenderingData renderingData,
            RadianceCascadesRenderingData radianceCascadesRenderingData,
            RTHandle color,
            RTHandle depth,
            int probeSize,
            int cascadeLevel,
            RTHandle target
        )
        {
            var rt = target.rt;
            var depthRT = depth.rt;

            cmd.SetComputeFloatParam(_compute, ShaderIds.ProbeSize, probeSize);
            cmd.SetComputeFloatParam(_compute, ShaderIds.CascadeLevel, cascadeLevel);
            var cascadeSize = new Vector4(rt.width, rt.height, 1.0f / rt.width, 1.0f / rt.height);
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeSize);


            var view = renderingData.cameraData.GetViewMatrix();
            var proj = renderingData.cameraData.GetGPUProjectionMatrix();

            var viewProj = view * proj;
            cmd.SetComputeMatrixParam(_compute, ShaderIds.View, view);
            cmd.SetComputeMatrixParam(_compute, ShaderIds.ViewProjection, viewProj);
            cmd.SetComputeMatrixParam(_compute, ShaderIds.InvViewProjection, viewProj.inverse);

            cmd.SetComputeVectorParam(
                _compute,
                ShaderIds.ColorTextureTexelSize,
                new Vector4(depthRT.width, depthRT.height, 1.0f / depthRT.width, 1.0f / depthRT.height)
            );

            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.ColorTexture, color);
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.DepthTexture, depth);

            cmd.SetComputeMatrixParam(_compute, "_WorldToSceneVolume", radianceCascadesRenderingData.WorldToVolume);
            cmd.SetComputeMatrixParam(_compute, "_InvWorldToSceneVolume", radianceCascadesRenderingData.WorldToVolume.inverse);
            var volumeRT = radianceCascadesRenderingData.SceneVolume.rt;
            var sceneVolumeTexelSize = new Vector4(
                1.0f / volumeRT.width,
                1.0f / volumeRT.height,
                volumeRT.width,
                volumeRT.height
            );
            cmd.SetComputeVectorParam(_compute, "_SceneVolume_TexelSize", sceneVolumeTexelSize);
            cmd.SetComputeTextureParam(
                _compute,
                _renderKernel,
                ShaderIds.SceneVolume,
                radianceCascadesRenderingData.SceneVolume
            );


            // Output
            cmd.SetComputeTextureParam(_compute, _renderKernel, ShaderIds.OutCascade, target);
            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                rt.width / 8,
                rt.height / 8,
                1
            );
        }

        // Legacy overload for CommandBuffer and RTHandle
        public void MergeCascades(
            CommandBuffer cmd,
            RTHandle lower,
            RTHandle upper,
            int lowerCascadeLevel
        )
        {
            var rt = lower.rt;

            var cascadeSize = new Vector4(rt.width, rt.height, 1.0f / rt.width, 1.0f / rt.height);
            cmd.SetComputeVectorParam(_compute, ShaderIds.CascadeBufferSize, cascadeSize);

            cmd.SetComputeFloatParam(_compute, "_LowerCascadeLevel", lowerCascadeLevel);

            cmd.SetComputeTextureParam(_compute, _mergeKernel, ShaderIds.LowerCascade, lower);
            cmd.SetComputeTextureParam(_compute, _mergeKernel, ShaderIds.UpperCascade, upper);

            cmd.DispatchCompute(
                _compute,
                _mergeKernel,
                rt.width / 8,
                rt.height / 8,
                1
            );
        }
    }
}

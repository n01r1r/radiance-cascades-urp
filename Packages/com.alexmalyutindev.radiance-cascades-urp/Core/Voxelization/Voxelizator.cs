using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades.Voxelization
{
    public class Voxelizator : IDisposable
    {
        private const RenderTextureMemoryless RenderTextureMemorylessAll =
            RenderTextureMemoryless.Color |
            RenderTextureMemoryless.Depth |
            RenderTextureMemoryless.MSAA;

        public static readonly int DummyID = Shader.PropertyToID(nameof(DummyID));
        public static readonly int Resolution = Shader.PropertyToID(nameof(Resolution));
        private const float Extend = 10;

        private readonly Shader _shader;
        private readonly ShaderTagId _shaderTagId;

        private RenderTextureDescriptor _dummyDesc;

        // Buffer to store all appeared voxels
        private ComputeBuffer _rawVoxelBuffer;
        private readonly VoxelizatorCompute _voxelizatorCompute;

        public Voxelizator(Shader voxelizatorShader, ComputeShader voxelizatorCompute)
        {
            _shaderTagId = new ShaderTagId("UniversalGBuffer");
            _shader = voxelizatorShader;

            _dummyDesc = new RenderTextureDescriptor()
            {
                colorFormat = RenderTextureFormat.R8,
                dimension = TextureDimension.Tex2D,
                memoryless = RenderTextureMemorylessAll,
                msaaSamples = 1,
                sRGB = false,
            };

            _voxelizatorCompute = new VoxelizatorCompute(voxelizatorCompute);
        }

        public void Prepare(int resolution)
        {
            _dummyDesc.width = _dummyDesc.height = resolution;
            _voxelizatorCompute.Prepare(resolution);

            // NOTE: Not sure what maximum size it should be.
            var volumeResolution = resolution * resolution * resolution;
            ReAllocateIfNeeded(ref _rawVoxelBuffer, volumeResolution, VoxelData.Size, ComputeBufferType.Append);
        }

        public void VoxelizeGeometry(
            CommandBuffer cmd,
            ref ScriptableRenderContext context,
            ref RenderingData renderingData,
            int resolution,
            ref RTHandle targetVolume
        )
        {
            var cameraData = renderingData.cameraData;

            // Prepare
            var drawingSettings = RenderingUtils.CreateDrawingSettings(
                _shaderTagId,
                ref renderingData,
                SortingCriteria.None
            );
            drawingSettings.overrideShader = _shader;
            drawingSettings.overrideShaderPassIndex = 0;
            drawingSettings.perObjectData = PerObjectData.None;

            var rendererListParams = new RendererListParams(
                renderingData.cullResults, // TODO: Culler camera
                drawingSettings,
                FilteringSettings.defaultValue
            );


            var volumeDesc = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGB32)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution,
                enableRandomWrite = true,
                depthBufferBits = 0,
            };
            // TODO: Clear Volume!
            RenderingUtils.ReAllocateHandleIfNeeded(ref targetVolume, volumeDesc);
            _voxelizatorCompute.ClearTexture3d(cmd, targetVolume);


            // Rendering
            cmd.GetTemporaryRT(DummyID, _dummyDesc);
            cmd.SetRenderTarget(DummyID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            cmd.SetRandomWriteTarget(1, _rawVoxelBuffer);
            cmd.SetGlobalInt(Resolution, resolution);

            cmd.SetGlobalTexture("_Volume", targetVolume);

            var volumeCenter = renderingData.cameraData.worldSpaceCameraPos;
            var forward = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var right = Quaternion.LookRotation(Vector3.right, Vector3.up);
            var top = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            // Forward projection.
            var view = CreateViewMatrix(volumeCenter, forward);
            var proj = CreateBoxProjection(Extend); // TODO
            cmd.SetViewProjectionMatrices(view, proj);
            cmd.SetGlobalInt("Axis", 0);

            var rendererList = context.CreateRendererList(ref rendererListParams);
            cmd.DrawRendererList(rendererList);

            if (!SystemInfo.supportsGeometryShaders)
            {
                cmd.BeginSample("RightProjection");
                {
                    view = CreateViewMatrix(volumeCenter, right);
                    cmd.SetViewMatrix(view);
                    cmd.SetGlobalInt("Axis", 1);

                    rendererList = context.CreateRendererList(ref rendererListParams);
                    cmd.DrawRendererList(rendererList);
                }
                cmd.EndSample("RightProjection");


                cmd.BeginSample("TopProjection");
                {
                    view = CreateViewMatrix(volumeCenter, top);
                    cmd.SetViewMatrix(view);
                    cmd.SetGlobalInt("Axis", 2);

                    rendererList = context.CreateRendererList(ref rendererListParams);
                    cmd.DrawRendererList(rendererList);
                }
                cmd.EndSample("TopProjection");
            }

            cmd.ClearRandomWriteTargets();
            cmd.ReleaseTemporaryRT(DummyID);

            // NOTE: Restore matrices
            cmd.SetViewProjectionMatrices(cameraData.GetViewMatrix(), cameraData.GetProjectionMatrix());

            return;
            // TODO: Separate hardcore voxelization.
            // _voxelizatorCompute.Dispatch(cmd, resolution, _rawVoxelBuffer, targetVolume);
        }

        public static void ReAllocateIfNeeded(
            ref ComputeBuffer buffer,
            int count,
            int stride,
            ComputeBufferType type = ComputeBufferType.Default,
            ComputeBufferMode usage = ComputeBufferMode.Immutable
        )
        {
            if (buffer == null || buffer.count != count)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(count, stride, type, usage);
            }
        }

        public static Matrix4x4 CreateWorldToVolumeMatrix(ref RenderingData renderingData, int resolution)
        {
            // TODO: Make better volume bounds placing.
            return CreateBoxProjection(Extend) *
                CreateViewMatrix(renderingData.cameraData.worldSpaceCameraPos, Quaternion.identity);
        }

        public static Matrix4x4 CreateViewMatrix(Vector3 position, Quaternion rotation)
        {
            var view = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            if (SystemInfo.usesReversedZBuffer)
            {
                view.m20 = -view.m20;
                view.m21 = -view.m21;
                view.m22 = -view.m22;
                view.m23 = -view.m23;
            }

            return view;
        }

        public static Matrix4x4 CreateBoxProjection(float extend)
        {
            return Matrix4x4.Ortho(-extend, extend, -extend, extend, -extend, extend);
        }

        public void CleanUp()
        {
            // BUG: Clean this will cause incorrect behaviour while executing CommandBuffer!
            // _voxelizatorCompute.CleanUp();
        }

        public void Dispose()
        {
            _voxelizatorCompute?.Dispose();
            _rawVoxelBuffer?.Dispose();
        }
    }
}

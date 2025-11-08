using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
 // ✅ RenderGraph, TextureHandle, *GraphContext

namespace AlexMalyutinDev.RadianceCascades
{
    public class DirectionFirstRCPass : ScriptableRenderPass, IDisposable
    {
        private readonly RadianceCascadesDirectionFirstCS _compute;
        private readonly Material _blitMaterial;
        
        // [MODIFIED] Shader keywords (removed _ViewportCropScaleOffset and k_ExtendedScreenSpace)
        private static readonly int _UpsampleTolerance = Shader.PropertyToID("_UpsampleTolerance");
        private static readonly int _NoiseFilterStrength = Shader.PropertyToID("_NoiseFilterStrength");
        private static readonly int _EnvironmentCubeMap = Shader.PropertyToID("_EnvironmentCubeMap");
        private static readonly int _EnvironmentIntensity = Shader.PropertyToID("_EnvironmentIntensity");
        private static readonly int _EnvironmentFallbackWeight = Shader.PropertyToID("_EnvironmentFallbackWeight");
        private static readonly int _MinRaySteps = Shader.PropertyToID("_MinRaySteps");
        private static readonly int _MaxRaySteps = Shader.PropertyToID("_MaxRaySteps");
        private static readonly int _VarianceSamplingInfluence = Shader.PropertyToID("_VarianceSamplingInfluence");
        
        // [NEW] Fallback ambient light color property ID
        private static readonly int _AmbientSkyColor = Shader.PropertyToID("_AmbientSkyColor");
        
        private const string k_DepthGuidedUpsampling = "_DEPTH_GUIDED_UPSAMPLING";
        private const string k_OffscreenFallbackEnv = "OFFSCREEN_FALLBACK_ENV";
        private const string k_OffscreenFallbackAmbient = "OFFSCREEN_FALLBACK_AMBIENT";

        public DirectionFirstRCPass(RadianceCascadeResources resources)
        {
            profilingSampler = new ProfilingSampler("RadianceCascades.DirectionFirst");
            _compute = new RadianceCascadesDirectionFirstCS(resources.RadianceCascadesDirectionalFirstCS);
            // TODO: Make proper C# wrapper for Blit/Combine shader!
            _blitMaterial = resources.BlitMaterial;
        }

        public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData)
        {
            RenderCascades(renderGraph, frameData, out var sh);
            CombineCascades(renderGraph, frameData, sh);
        }

        private class PassData
        {
            public RadianceCascadesDirectionFirstCS Compute;
            public float RayLength;

            public Vector4 CascadesSizeTexel;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle Cascades;
            public Vector4 RadianceSHSizeTexel;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle RadianceSH;

            public UniversalCameraData CameraData;

            public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameDepth;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle BlurredColor;

            public UnityEngine.Rendering.RenderGraphModule.TextureHandle MinMaxDepth;
            public Vector4 VarianceDepthSizeTexel;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle VarianceDepth;
            
            // [MODIFIED] Pass settings from Volume (removed BaseWidth, BaseHeight)
            public RadianceCascades Settings;
        }

        private void RenderCascades(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData, out UnityEngine.Rendering.RenderGraphModule.TextureHandle radianceSH)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var minMaxDepthData = frameData.Get<MinMaxDepthData>();
            var varianceDepthData = frameData.Get<VarianceDepthData>();
            var blurredColorData = frameData.Get<BlurredColorData>();

            var settings = VolumeManager.instance.stack.GetComponent<RadianceCascades>();

            using var builder = renderGraph.AddComputePass<PassData>("RC.Render", out var passData);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true); // [NEW] Required for SetGlobalTexture, SetGlobalFloat, EnableKeyword, etc.

            passData.CameraData = cameraData;
            passData.RayLength = settings.RayScale.value;
            passData.Settings = settings; // [NEW] Pass settings to render func

            passData.FrameDepth = resourceData.activeDepthTexture;
            builder.UseTexture(passData.FrameDepth);
            passData.MinMaxDepth = minMaxDepthData.MinMaxDepth;
            builder.UseTexture(passData.MinMaxDepth);

            passData.VarianceDepthSizeTexel = GetSizeTexel(varianceDepthData.VarianceDepth, renderGraph);
            passData.VarianceDepth = varianceDepthData.VarianceDepth;
            builder.UseTexture(passData.VarianceDepth);
            passData.BlurredColor = blurredColorData.BlurredColor;
            builder.UseTexture(passData.BlurredColor);

            passData.Compute = _compute;

            // [MODIFIED] Removed Extended Screen-Space logic, use fixed size
            int cascadeWidth = 2048;
            int cascadeHeight = 1024;
            
            var desc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(cascadeWidth, cascadeHeight)
            {
                name = "RadianceCascades",
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat,
                enableRandomWrite = true
            };
            passData.CascadesSizeTexel = new Vector4(
                desc.width, desc.height,
                1.0f / desc.width, 1.0f / desc.height
            );
            passData.Cascades = builder.CreateTransientTexture(desc);

            // SH buffer size (half of cascade buffer)
            desc.width = cascadeWidth / 2;
            desc.height = cascadeHeight / 2;
            passData.RadianceSHSizeTexel = new Vector4(
                desc.width, desc.height,
                1.0f / desc.width, 1.0f / desc.height
            );
            passData.RadianceSH = renderGraph.CreateTexture(desc);
            builder.UseTexture(passData.RadianceSH, UnityEngine.Rendering.RenderGraphModule.AccessFlags.Write);

            // TODO: Refactor!
            radianceSH = passData.RadianceSH;

            builder.SetRenderFunc<PassData>(static (data, context) =>
            {
                var settings = data.Settings;
                
                // [NEW FIX] Compute shader doesn't have access to unity_AmbientSky, so pass it from C#
                context.cmd.SetGlobalVector(_AmbientSkyColor, RenderSettings.ambientSkyColor.linear);
                
                // [NEW] Phase 1.3 & 3.3: Set Environment/Fallback uniforms
                var fallbackMode = settings.OffScreenFallbackMode.value;
                var computeShader = data.Compute.GetComputeShader();
                var kwEnv = new LocalKeyword(computeShader, k_OffscreenFallbackEnv);
                var kwAmbient = new LocalKeyword(computeShader, k_OffscreenFallbackAmbient);
                
                if (fallbackMode == OffScreenFallbackMode.EnvironmentCube)
                    context.cmd.EnableKeyword(computeShader, kwEnv);
                else
                    context.cmd.DisableKeyword(computeShader, kwEnv);
                    
                if (fallbackMode == OffScreenFallbackMode.Ambient)
                    context.cmd.EnableKeyword(computeShader, kwAmbient);
                else
                    context.cmd.DisableKeyword(computeShader, kwAmbient);
                
                if (fallbackMode == OffScreenFallbackMode.EnvironmentCube && settings.EnvironmentCubeMap.value != null)
                {
                    // ComputeCommandBuffer.SetGlobalTexture only accepts TextureHandle, not regular Texture
                    // For Cubemap assets, we use Shader.SetGlobalTexture which works immediately
                    // This is acceptable since environment maps are typically set once per frame
                    Shader.SetGlobalTexture("_EnvironmentCubeMap", settings.EnvironmentCubeMap.value);
                    context.cmd.SetGlobalFloat(_EnvironmentIntensity, settings.EnvironmentIntensity.value);
                    context.cmd.SetGlobalFloat(_EnvironmentFallbackWeight, settings.EnvironmentFallbackWeight.value);
                }
                
                // [NEW] Pass _ViewToWorld for environment map sampling
                context.cmd.SetGlobalMatrix("_ViewToWorld", data.CameraData.GetViewMatrix().inverse);
                
                // [NEW] Phase 4.2: Set Adaptive Sampling Density uniforms
                context.cmd.SetGlobalInt("_EnableAdaptiveSamplingDensity", settings.EnableAdaptiveSamplingDensity.value ? 1 : 0);
                context.cmd.SetGlobalInt(_MinRaySteps, settings.MinRaySteps.value);
                context.cmd.SetGlobalInt(_MaxRaySteps, settings.MaxRaySteps.value);
                context.cmd.SetGlobalFloat(_VarianceSamplingInfluence, settings.VarianceSamplingInfluence.value);

                // This RenderMerge call will implicitly use the globals & keywords set above
                data.Compute.RenderMerge(
                    context.cmd,
                    ref data.CameraData,
                    data.FrameDepth,
                    data.MinMaxDepth,
                    data.VarianceDepth,
                    data.VarianceDepthSizeTexel,
                    data.BlurredColor,
                    data.RayLength,
                    ref data.Cascades,
                    data.CascadesSizeTexel
                );

                data.Compute.CombineSH(
                    context.cmd,
                    ref data.CameraData,
                    data.Cascades,
                    data.CascadesSizeTexel,
                    data.MinMaxDepth,
                    data.VarianceDepth,
                    ref data.RadianceSH,
                    data.RadianceSHSizeTexel
                );
            });
        }

        private class CombinePassData
        {
            public Material Material;
            public UniversalCameraData CameraData;

            public UnityEngine.Rendering.RenderGraphModule.TextureHandle MinMaxDepth;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle RadianceSH;

            public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameColor;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameDepth;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle FrameNormals;
            
            // [MODIFIED] Pass settings from Volume (removed RadianceSHSizeTexel, BaseWidth, BaseHeight)
            public RadianceCascades Settings;
        }

        private void CombineCascades(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData, UnityEngine.Rendering.RenderGraphModule.TextureHandle radianceSH)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var minMaxDepthData = frameData.Get<MinMaxDepthData>();
            
            // [NEW] Need settings for upsampling and cropping
            var settings = VolumeManager.instance.stack.GetComponent<RadianceCascades>();

            using var builder = renderGraph.AddRasterRenderPass<CombinePassData>("RC.Combine", out var passData);
            builder.AllowGlobalStateModification(true);

            passData.Material = _blitMaterial;
            passData.CameraData = cameraData;
            passData.Settings = settings; // [NEW]

            passData.FrameColor = resourceData.gBuffer[0];
            builder.UseTexture(passData.FrameColor);
            passData.FrameNormals = resourceData.gBuffer[2];
            builder.UseTexture(passData.FrameNormals);
            passData.FrameDepth = resourceData.cameraDepth;
            builder.UseTexture(passData.FrameDepth);

            passData.RadianceSH = radianceSH;
            builder.UseTexture(passData.RadianceSH);

            passData.MinMaxDepth = minMaxDepthData.MinMaxDepth;
            builder.UseTexture(passData.MinMaxDepth);

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderFunc<CombinePassData>(static (data, context) =>
            {
                var settings = data.Settings;
                
                // [NEW] Phase 2.2: Set Depth-Guided Upsampling uniforms and keyword
                if (data.Material != null && data.Material.shader != null)
                {
                    var kwDepthGuided = new LocalKeyword(data.Material.shader, k_DepthGuidedUpsampling);
                    if (settings.EnableDepthGuidedUpsampling.value)
                        context.cmd.EnableKeyword(data.Material, kwDepthGuided);
                    else
                        context.cmd.DisableKeyword(data.Material, kwDepthGuided);
                    context.cmd.SetGlobalFloat(_UpsampleTolerance, settings.UpsampleTolerance.value);
                    context.cmd.SetGlobalFloat(_NoiseFilterStrength, settings.NoiseFilterStrength.value);
                }

                context.cmd.SetGlobalMatrix("_ViewToWorld", data.CameraData.GetViewMatrix().inverse);
                context.cmd.SetGlobalTexture("_MinMaxDepth", data.MinMaxDepth);

                context.cmd.SetGlobalTexture("_GBuffer0", data.FrameColor);
                context.cmd.SetGlobalTexture("_GBuffer2", data.FrameNormals);
                context.cmd.SetGlobalTexture("_CameraDepthTexture", data.FrameDepth);
                
                if (data.Material != null)
                {
                    BlitUtils.BlitTexture(context.cmd, data.RadianceSH, data.Material, 4); // Use Pass 4 ("BlitSH")
                }
                else
                {
                    Debug.LogError("RadianceCascades: BlitMaterial is null! Check RadianceCascadeResources asset.");
                }
            });
        }

        public void Dispose() { }

        private static Vector4 GetSizeTexel(UnityEngine.Rendering.RenderGraphModule.TextureHandle texture, UnityEngine.Rendering.RenderGraphModule.RenderGraph rg)
        {
            var desc = texture.GetDescriptor(rg);
            return new Vector4(
                desc.width, desc.height,
                1.0f / desc.width, 1.0f / desc.height
            );
        }
    }
}

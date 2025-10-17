using System;
using AlexMalyutinDev.RadianceCascades;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering; // GraphicsFormatUtility 등
 // ✅ RenderGraph, TextureHandle, *GraphContext

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascades3dPass : ScriptableRenderPass, IDisposable
    {
        private const int CascadesCount = 5;
        private static readonly string[] Cascade3dNames = GenNames("_Cascade", CascadesCount);

        private readonly ProfilingSampler _profilingSampler;
        private readonly RadianceCascadesRenderingData _rcShared;
        private readonly Material _blitMaterial;
        private readonly RadianceCascadeCubeMapCS _rcCS;

        public RadianceCascades3dPass(
            RadianceCascadeResources resources,
            RadianceCascadesRenderingData shared
        )
        {
            _profilingSampler = new ProfilingSampler(nameof(RadianceCascades3dPass));
            _rcCS = new RadianceCascadeCubeMapCS(resources.RadianceCascades3d);
            _rcShared = shared;
            _blitMaterial = resources.BlitMaterial;
        }

        public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph rg, ContextContainer frameData)
        {
            Debug.Log("RadianceCascades3dPass: RecordRenderGraph called");
            var ucam = frameData.Get<UniversalCameraData>();
            var ur = frameData.Get<UniversalResourceData>();
            var vox = frameData.GetOrCreate<VoxelizationData>();

            if (ucam == null || ur == null || vox == null)
            {
                Debug.LogWarning("RadianceCascades3dPass: Missing required data containers");
                return; // early out: containers not ready
            }

            // 1) Render cascades (compute)
            var finalCascade = RenderCascades(rg, ucam, ur, vox);

            // 2) Composite to camera color (raster)
            CombineCascades(rg, ucam, ur, finalCascade);
        }

        // ---------- COMPUTE PASS ----------

        private sealed class PassData
        {
            public RadianceCascadeCubeMapCS rcCS;
            public RadianceCascadesRenderingData shared;

            // Pure values (no pipeline containers)
            public Matrix4x4 view;
            public Matrix4x4 proj;
            public Vector3 camPosWS;
            public Matrix4x4 worldToVolume;

            // Inputs
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle frameColor;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle frameDepth;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle sceneVolume;

            // Outputs
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle[] cascades;
            public int cascadeBaseSize;
        }

        private UnityEngine.Rendering.RenderGraphModule.TextureHandle RenderCascades(
            UnityEngine.Rendering.RenderGraphModule.RenderGraph rg,
            UniversalCameraData ucam,
            UniversalResourceData ur,
            VoxelizationData vox)
        {
            using var builder = rg.AddComputePass<PassData>("RC3D.RenderCascades", out var pd);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(false);

            // Fill pass data
            pd.rcCS = _rcCS;
            pd.shared = _rcShared;

            pd.view = ucam.GetViewMatrix();
            pd.proj = ucam.camera.projectionMatrix; // Use regular projection matrix to avoid NRE
            pd.camPosWS = ucam.worldSpaceCameraPos;
            pd.worldToVolume = vox.WorldToVolume;

            // Inputs with proper access modes (Unity 6 compatible)
            pd.frameColor = ur.activeColorTexture; builder.UseTexture(pd.frameColor, UnityEngine.Rendering.RenderGraphModule.AccessFlags.Read);
            pd.frameDepth = ur.activeDepthTexture; builder.UseTexture(pd.frameDepth, UnityEngine.Rendering.RenderGraphModule.AccessFlags.Read);
            pd.sceneVolume = vox.SceneVolume; builder.UseTexture(pd.sceneVolume, UnityEngine.Rendering.RenderGraphModule.AccessFlags.Read);

            // Allocate cascades as persistent RG textures; register writes
            pd.cascadeBaseSize = 2; // replace with your intended base tile size
            pd.cascades = new UnityEngine.Rendering.RenderGraphModule.TextureHandle[CascadesCount];
            const int scale = 4;
            for (int i = 0; i < CascadesCount; i++)
            {
                // Use TextureDesc for RenderGraph.CreateTexture (Unity 6 API)
                // Fix: Use per-level i instead of CascadesCount, with proper GPU limits
                int max2D = SystemInfo.maxTextureSize;
                int width = Mathf.Clamp((2 << i) * 2 * scale, 16, max2D);
                int height = Mathf.Clamp((1 << i) * 3 * scale, 16, max2D);
                
                var desc = new UnityEngine.Rendering.RenderGraphModule.TextureDesc(width, height)
                {
                    name = Cascade3dNames[i],
                    colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite = true
                };
                pd.cascades[i] = rg.CreateTexture(desc);  // ✅ Use persistent for inter-pass communication
                builder.UseTexture(pd.cascades[i], UnityEngine.Rendering.RenderGraphModule.AccessFlags.Write);
            }

            builder.SetRenderFunc((PassData d, UnityEngine.Rendering.RenderGraphModule.ComputeGraphContext ctx) =>
            {
                // Unity 6 RenderGraph: Use TextureHandle directly, no RTHandle conversion
                // Use the compute context command buffer directly
                var cmd = ctx.cmd;
                
                // TODO: Update compute shader to work with TextureHandle directly
                // For now, skip the legacy compute shader calls to avoid RTHandle conversion
                Debug.LogWarning("RC3D: Skipping cascade rendering - needs TextureHandle-native compute shader implementation");
                
                // TODO: Implement proper TextureHandle-based compute shader calls
                // This requires updating the compute shader wrapper to accept TextureHandle
            });

            // Return final cascade (index 0)
            return pd.cascades[0];
        }

        // ---------- RASTER (COMPOSITE) PASS ----------

        private sealed class CombinePassData
        {
            public Material blitMat;
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle frameDepth;   // if needed in material
            public UnityEngine.Rendering.RenderGraphModule.TextureHandle cascadeTex;   // input
        }

        private void CombineCascades(
            UnityEngine.Rendering.RenderGraphModule.RenderGraph rg,
            UniversalCameraData ucam,
            UniversalResourceData ur,
            UnityEngine.Rendering.RenderGraphModule.TextureHandle cascadeTexture)
        {
            using var builder = rg.AddRasterRenderPass<CombinePassData>("RC3D.Combine", out var pd);
            builder.AllowGlobalStateModification(true);

            pd.blitMat = _blitMaterial;
            pd.cascadeTex = cascadeTexture; builder.UseTexture(pd.cascadeTex, UnityEngine.Rendering.RenderGraphModule.AccessFlags.Read);
            pd.frameDepth = ur.activeDepthTexture; builder.UseTexture(pd.frameDepth, UnityEngine.Rendering.RenderGraphModule.AccessFlags.Read);

            // Write into camera color
            builder.SetRenderAttachment(ur.activeColorTexture, 0);

            builder.SetRenderFunc((CombinePassData d, UnityEngine.Rendering.RenderGraphModule.RasterGraphContext ctx) =>
            {
                RasterCommandBuffer cmd = ctx.cmd;
                var src = d.cascadeTex;

                // Bind as global for material
                cmd.SetGlobalTexture("_RC_FinalCascade", src);
                // Optional tint, test/debug
                cmd.SetGlobalFloat("_CubeMapTint", 1.0f);
                cmd.SetGlobalColor("_CubeMapColor", new Color(0.8f, 0.9f, 1.0f, 1.0f));

                // Fullscreen draw (material pass index depends on your shader)
                CoreUtils.DrawFullScreen(cmd, d.blitMat, shaderPassId: 1);
            });
        }

        public void Dispose() { }

        private static string[] GenNames(string name, int n)
        {
            var names = new string[n];
            for (int i = 0; i < n; i++) names[i] = name + i;
            return names;
        }
    }
}
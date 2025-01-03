using UnityEngine;

namespace AlexMalyutinDev.RadianceCascades
{
    public static class ShaderIds
    {
        public static readonly int ProbeSize = Shader.PropertyToID("_ProbeSize");
        public static readonly int CascadeLevel = Shader.PropertyToID("_CascadeLevel");
        public static readonly int LowerCascadeLevel = Shader.PropertyToID("_LowerCascadeLevel");
        public static readonly int CascadeBufferSize = Shader.PropertyToID("_CascadeBufferSize");

        public static readonly int LowerCascade = Shader.PropertyToID("_LowerCascade");
        public static readonly int UpperCascade = Shader.PropertyToID("_UpperCascade");
        
        public static readonly int View = Shader.PropertyToID("_View");
        public static readonly int ViewProjection = Shader.PropertyToID("_ViewProjection");
        public static readonly int InvViewProjection = Shader.PropertyToID("_InvViewProjection");

        public static readonly int ColorTexture = Shader.PropertyToID("_ColorTexture");
        public static readonly int ColorTextureTexelSize = Shader.PropertyToID("_ColorTexture_TexelSize");
        
        public static readonly int DepthTexture = Shader.PropertyToID("_DepthTexture");
        public static readonly int NormalsTexture = Shader.PropertyToID("_NormalsTexture");
        public static readonly int MinMaxDepth = Shader.PropertyToID("_MinMaxDepth");

        public static readonly int SceneVolume = Shader.PropertyToID("_SceneVolume");

        public static readonly int OutCascade = Shader.PropertyToID("_OutCascade");
    }
}

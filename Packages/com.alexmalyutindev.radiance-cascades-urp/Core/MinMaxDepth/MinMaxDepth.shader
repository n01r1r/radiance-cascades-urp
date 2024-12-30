Shader "Hidden/MinMaxDepth"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "DownSampleDepthMinMax2x2"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            Texture2D<float> _BlitTexture;
            float4 _BlitTexture_TexelSize;
            float2 InputResolution;
            int _InputMipLevel;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy * 2 - 1, 0, 1);
                output.uv = input.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
                #endif
                return output;
            }

            float2 Fragment(Varyings input) : SV_TARGET
            {
                int2 coord = input.uv * (InputResolution - 1);

                float a = LOAD_TEXTURE2D_LOD(_BlitTexture, coord, _InputMipLevel);
                float b = LOAD_TEXTURE2D_LOD(_BlitTexture, coord + int2(1, 0), _InputMipLevel);
                float c = LOAD_TEXTURE2D_LOD(_BlitTexture, coord + int2(0, 1), _InputMipLevel);
                float d = LOAD_TEXTURE2D_LOD(_BlitTexture, coord + int2(1, 1), _InputMipLevel);

                return float2(
                    min(min(a, b), min(c, d)),
                    max(max(a, b), max(c, d))
                );
            }
            ENDHLSL
        }
    }
}
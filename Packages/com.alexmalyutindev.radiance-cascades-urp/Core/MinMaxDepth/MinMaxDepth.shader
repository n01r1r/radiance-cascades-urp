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
            Name "DownSampleDepthMinMax2x2_SingleChannel"
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #define SINGLE_CHANNEL
            #include "Common.hlsl"

            float2 Fragment(Varyings input) : SV_TARGET
            {
                int2 coord = input.uv * _Scale * (_InputResolution - 1);
                return LoadDepthMinMax(coord, _InputMipLevel);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DownSampleDepthMinMax2x2"
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Common.hlsl"

            float2 Fragment(Varyings input) : SV_TARGET
            {
                int2 coord = input.uv * _Scale * (_InputResolution - 1);
                return LoadDepthMinMax(coord, _InputMipLevel);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CopyLevel"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Common.hlsl"

            float2 Fragment(Varyings input) : SV_TARGET
            {
                int2 coord = input.uv * (_InputResolution);
                return LOAD_TEXTURE2D_LOD(_BlitTexture, coord, 0).rg;
            }
            ENDHLSL
        }
    }
}
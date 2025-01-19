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
                // coord = floor(input.positionCS.xy) * 2;
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
                // coord = floor(input.positionCS.xy) * 2;
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
                int2 coord = input.positionCS.xy;
                return LOAD_TEXTURE2D_LOD(_BlitTexture, coord, 0).rg;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthToMixMaxDepth"
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma target 2.0
            #pragma editor_sync_compilation

            #define SINGLE_CHANNEL
            #include "Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            float2 _TargetResolution;

            float2 Fragment(Varyings input) : SV_TARGET
            {
                float2 minMaxDepth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, input.uv, 0).rr;
                return minMaxDepth;
                
                int2 range = floor(_BlitTexture_TexelSize.zw / _TargetResolution.xy);
                int2 coords = input.positionCS.xy * range;
                float2 minMaxDepth0 = LOAD_TEXTURE2D(_BlitTexture, coords).rr;
                for (int x = 1; x < range.x; x++)
                {
                    for (int y = 1; y < range.y; y++)
                    {
                        float depth = LOAD_TEXTURE2D(_BlitTexture, coords + int2(x, y)).r;
                        minMaxDepth0 = float2(
                            min(minMaxDepth0.x, depth),
                            max(minMaxDepth0.y, depth)
                        );
                    }
                }
                
                return minMaxDepth0;

                for (int x = 1; x < range.x; x++)
                {
                    for (int y = 1; y < range.y; y++)
                    {
                        float2 uv = input.uv + float2(x, y) * _BlitTexture_TexelSize.xy;
                        float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
                        minMaxDepth = float2(
                            min(minMaxDepth.x, depth),
                            max(minMaxDepth.y, depth)
                        );
                    }
                }
                return minMaxDepth;
            }
            ENDHLSL
        }
    }
}
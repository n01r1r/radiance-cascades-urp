Shader "Hidden/SmoothedDepth"
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
            Name "DownSampleDepthBlurred"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D<float> _BlitTexture;
            float4 _BlitTexture_TexelSize;
            float2 _InputResolution;
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

            inline float SampleDepth01(float2 uv, int lod)
            {
                #if UNITY_REVERSED_Z
                return 1.0f - SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, lod);
                #else
                return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, lod);
                #endif
            }

            float2 Fragment(Varyings input) : SV_TARGET
            {
                float4 offset = float4(_BlitTexture_TexelSize.xy, -_BlitTexture_TexelSize.xy) * 0.5f;

                float depth01 = SampleDepth01(input.uv + offset.xy, _InputMipLevel);
                depth01 += SampleDepth01(input.uv + offset.xw, _InputMipLevel);
                depth01 += SampleDepth01(input.uv + offset.zy, _InputMipLevel);
                depth01 += SampleDepth01(input.uv + offset.zw, _InputMipLevel);
                depth01 *= 0.25f;

                return depth01;
            }
            ENDHLSL
        }

        Pass
        {
            Name "MipMapDepthBlurred"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D<float> _BlitTexture;
            float4 _BlitTexture_TexelSize;
            float2 _InputResolution;
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

            inline float SampleDepth01(float2 uv, int lod)
            {
                return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, lod);
            }

            float2 Fragment(Varyings input) : SV_TARGET
            {
                float4 offset = float4(_BlitTexture_TexelSize.xy, -_BlitTexture_TexelSize.xy) * 0.5f;

                float depth01 = SampleDepth01(input.uv + offset.xy, _InputMipLevel);
                depth01 += SampleDepth01(input.uv + offset.xw, _InputMipLevel);
                depth01 += SampleDepth01(input.uv + offset.zy, _InputMipLevel);
                depth01 += SampleDepth01(input.uv + offset.zw, _InputMipLevel);
                depth01 *= 0.25f;

                return depth01;
            }
            ENDHLSL
        }
    }
}
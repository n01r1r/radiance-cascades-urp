Shader "Hidden/VarianceDepth"
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
            Name "DepthToMomentsBlurH"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D<float> _BlitTexture;
            float4 _BlitTexture_TexelSize;

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
                float2 offset = float2(_BlitTexture_TexelSize.x * 1.5f, 0.0f);
                float2 depth2 = 0.0f;
                for (int x = -2; x <= 2; x++)
                {
                    float depthRaw = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv + offset * x).r;
                    float depth = LinearEyeDepth(depthRaw, _ZBufferParams);

                    depth2 += float2(0.2f * depth, 0.2f * depth * depth);
                }
                return depth2;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthMomentsBlurV"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D<float2> _BlitTexture;
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
                float2 offset = float2(0.0f, _BlitTexture_TexelSize.y * 1.0f);
                float2 depth2 = 0.0f;
                for (int y = -2; y <= 2; y++)
                {
                    float2 depthMoments = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv + offset * y).rg;
                    depth2 += depthMoments * 0.2f;
                }
                return depth2;
            }
            ENDHLSL
        }
    }
}
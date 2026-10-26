Shader "Custom/Dissolve"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _NoiseTex ("Noise", 2D) = "white" {}
        _Dissolve ("Dissolve", Range(0,1)) = 0.5
        _EdgeWidth ("Edge Width", Range(0,1)) = 0.1
        _AnimationSpeed ("Animation Speed", Range(0,2)) = 0.1
        [HDR] _EdgeColor ("Edge Color", Color) = (0, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"="AlphaTest"
        }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_NoiseTex);
        SAMPLER(sampler_NoiseTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _NoiseTex_ST;
            float _Dissolve;
            float _EdgeWidth;
            float _AnimationSpeed;
            half4 _EdgeColor;
        CBUFFER_END

        float SampleDissolveNoise(float2 uv)
        {
            float2 uv_shifted = uv + _Time.y * _AnimationSpeed;
            float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv_shifted).r;
            clip(noise - _Dissolve);
            return step(noise, _EdgeWidth + _Dissolve);
        }
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _NoiseTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float edge = SampleDissolveNoise(IN.uv);
                return lerp(_BaseColor, _EdgeColor, edge);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode"="ShadowCaster"
            }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                float3 lightDir = _LightDirection;
                #endif

                // bias zapobiega "shadow acne" (paskom cienia na samym obiekcie)
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionHCS = positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _NoiseTex);
                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target
            {
                SampleDissolveNoise(IN.uv);
                return 0;
            }
            ENDHLSL
        }
    }
}
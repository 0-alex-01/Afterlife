Shader "Afterlife/True Cloud Island"
{
    Properties
    {
        _NoiseTex ("TrueClouds Noise", 2D) = "white" {}
        _CloudTint ("Cloud Tint", Color) = (0.86, 0.92, 0.96, 1)
        _CloudShadow ("Cloud Shadow", Color) = (0.34, 0.43, 0.52, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _Cutoff ("Soft Cutoff", Range(0, 1)) = 0.34
        _Softness ("Edge Softness", Range(0.02, 0.5)) = 0.18
        _WindSpeed ("Wind Speed", Float) = 0.018
        _Motion ("Surface Motion", Range(0, 1)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TrueCloudIsland"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _CloudTint;
                half4 _CloudShadow;
                half _Opacity;
                half _Cutoff;
                half _Softness;
                half _WindSpeed;
                half _Motion;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float wave = sin(positionWS.x * 0.075 + positionWS.z * 0.061 + _Time.y * 0.32);
                positionWS += normalWS * wave * _Motion;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _NoiseTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 driftA = float2(_Time.y * _WindSpeed, _Time.y * _WindSpeed * 0.37);
                float2 driftB = float2(-_Time.y * _WindSpeed * 0.41, _Time.y * _WindSpeed * 0.72);
                half noiseA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * 1.45 + driftA).r;
                half noiseB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * 3.1 + driftB).g;
                half density = noiseA * 0.72 + noiseB * 0.28;
                half alpha = smoothstep(_Cutoff, _Cutoff + _Softness, density) * _Opacity;

                Light mainLight = GetMainLight();
                half lighting = saturate(dot(normalize(input.normalWS), mainLight.direction) * 0.38 + 0.62);
                half heightGlow = saturate((input.normalWS.y + 1.0) * 0.5);
                half3 color = lerp(_CloudShadow.rgb, _CloudTint.rgb, saturate(lighting * 0.72 + heightGlow * 0.28));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

Shader "Afterlife/Decay Veins"
{
    Properties
    {
        _BaseColor ("Ash Base", Color) = (0.075, 0.058, 0.045, 1)
        _RotColor ("Rot Veins", Color) = (0.72, 0.22, 0.035, 1)
        _RestoredColor ("Restored Memory", Color) = (0.18, 0.88, 0.76, 1)
        _VeinScale ("Vein Scale", Float) = 2.8
        _PulseSpeed ("Pulse Speed", Float) = 1.25
        _Metallic ("Metallic", Range(0, 1)) = 0.18
        _Smoothness ("Smoothness", Range(0, 1)) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DecayForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RotColor;
                half4 _RestoredColor;
                half _VeinScale;
                half _PulseSpeed;
                half _Metallic;
                half _Smoothness;
            CBUFFER_END

            float _DecayRestoration;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS * _VeinScale;
                float veinA = abs(sin(p.x * 0.71 + sin(p.z * 0.43) + _Time.y * 0.08));
                float veinB = abs(sin(p.z * 0.62 - p.y * 0.35 + cos(p.x * 0.31)));
                half veins = 1.0 - smoothstep(0.055, 0.19, min(veinA, veinB));
                half pulse = 0.62 + sin(_Time.y * _PulseSpeed + p.y * 0.18) * 0.38;
                half3 veinColor = lerp(_RotColor.rgb, _RestoredColor.rgb, saturate(_DecayRestoration));

                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 diffuse = _BaseColor.rgb * (0.28 + ndl * 0.72) * mainLight.color;
                half3 emission = veinColor * veins * (1.4 + pulse * 2.2);
                emission += _RestoredColor.rgb * saturate(_DecayRestoration - 0.7) * 0.22;
                return half4(diffuse + emission, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}

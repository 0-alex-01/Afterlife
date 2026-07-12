Shader "Afterlife/Portal Oval Membrane"
{
    Properties
    {
        _BaseMap ("Membrane Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _EdgeSoftness ("Oval Edge Softness", Range(0.001, 0.15)) = 0.025
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _EdgeSoftness;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float oval = dot(centered, centered);
                clip(1.0 - oval);

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, oval);
                color.rgb *= lerp(0.72, 1.0, edge);
                return color;
            }
            ENDHLSL
        }
    }
}

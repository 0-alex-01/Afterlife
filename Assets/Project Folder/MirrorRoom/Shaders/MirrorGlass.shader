Shader "Custom/MirrorGlass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.02, 0.025, 0.35)
        _RimColor ("Rim Color", Color) = (0.25, 0.69, 0.80, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 4
        _RimStrength ("Rim Strength", Range(0, 5)) = 1
        _RippleColor ("Ripple Color", Color) = (0.25, 0.69, 0.80, 1)
        _RippleCenter ("Ripple Center (uv xy)", Vector) = (0.5, 0.5, 0, 0)
        _RippleProgress ("Ripple Progress", Range(0, 1)) = 0
        _RippleWidth ("Ripple Width", Float) = 0.08

        _SplitProgress ("Split Progress", Range(0, 1)) = 0
        [HDR] _SplitColorA ("Split Color A (left)", Color) = (1.0, 0.72, 0.28, 1)
        [HDR] _SplitColorB ("Split Color B (right)", Color) = (1.0, 0.12, 0.14, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float4 _RippleColor;
                float4 _RippleCenter;
                float _RippleProgress;
                float _RippleWidth;
                float _SplitProgress;
                float4 _SplitColorA;
                float4 _SplitColorB;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vni = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS = vni.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Faint fresnel sheen so the pane reads as reflective glass.
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;

                float3 color = _BaseColor.rgb + _RimColor.rgb * rim;
                float alpha = _BaseColor.a + rim * 0.5;

                // Expanding concentric touch ripple in UV space.
                if (_RippleProgress > 0.0001 && _RippleProgress < 1.0)
                {
                    float d = distance(input.uv, _RippleCenter.xy);
                    float ring = abs(d - _RippleProgress);
                    float band = 1.0 - saturate(ring / max(_RippleWidth, 1e-4));
                    band = smoothstep(0.0, 1.0, band);
                    // Fade out as the ripple expands.
                    float fade = 1.0 - _RippleProgress;
                    float intensity = band * fade;
                    color += _RippleColor.rgb * intensity * 1.5;
                    alpha = saturate(alpha + intensity * 0.6);
                }

                // JUDGEMENT: vertical split into two glowing halves with a dark seam.
                if (_SplitProgress > 0.0001)
                {
                    float3 halfColor = (input.uv.x < 0.5) ? _SplitColorA.rgb : _SplitColorB.rgb;
                    color = lerp(color, halfColor, saturate(_SplitProgress) * 0.85);
                    // Soft dark seam down the centre.
                    float seam = smoothstep(0.0, 0.025, abs(input.uv.x - 0.5));
                    color *= lerp(0.15, 1.0, seam);
                    alpha = saturate(alpha + _SplitProgress * 0.5);
                }

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}

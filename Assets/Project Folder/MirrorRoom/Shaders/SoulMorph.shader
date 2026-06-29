Shader "Custom/SoulMorph"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.05, 0.18, 0.22, 1)
        _RimColor ("Rim Color", Color) = (0.25, 0.69, 0.80, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 5)) = 2
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 0.6, 0.2, 1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.5)) = 0.06
        _DissolveScale ("Dissolve Noise Scale", Float) = 8
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0
        _GlitchBands ("Glitch Bands", Float) = 18
        _BodilessAmount ("Bodiless Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float _Alpha;
                float _EmissionStrength;
                float _DissolveAmount;
                float4 _DissolveEdgeColor;
                float _DissolveEdgeWidth;
                float _DissolveScale;
                float _GlitchAmount;
                float _GlitchBands;
                float _BodilessAmount;
            CBUFFER_END

            // Simple inline hash noise (no external textures).
            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise on 3D position.
            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(i + float3(0, 0, 0));
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posOS = input.positionOS.xyz;

                // Chaos: stepped horizontal-band X offset driven by time.
                if (_GlitchAmount > 0.0001)
                {
                    float band = floor(posOS.y * _GlitchBands);
                    float t = _Time.y * 12.0;
                    float stepped = hash13(float3(band, floor(t), 0.0)) - 0.5;
                    posOS.x += stepped * _GlitchAmount * 0.15;
                }

                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                VertexNormalInputs vni = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS = vni.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Fresnel rim glow.
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;

                // Dissolve: procedural value noise on world position.
                float noise = valueNoise(input.positionWS * _DissolveScale);
                if (_DissolveAmount > 0.0001)
                {
                    clip(noise - _DissolveAmount);
                }
                float edge = 0.0;
                if (_DissolveAmount > 0.0001)
                {
                    edge = 1.0 - saturate((noise - _DissolveAmount) / max(_DissolveEdgeWidth, 1e-4));
                    edge = saturate(edge);
                }

                // Base body contribution + rim.
                float3 bodyCol = _BaseColor.rgb;
                float3 rimCol = _RimColor.rgb * rim;

                // Chaos RGB channel split / magenta-cyan offset on the rim.
                if (_GlitchAmount > 0.0001)
                {
                    float g = _GlitchAmount;
                    float jit = frac(_Time.y * 23.0 + input.positionWS.y * 4.0) - 0.5;
                    float3 magenta = float3(1.0, 0.0, 1.0);
                    float3 cyan = float3(0.0, 1.0, 1.0);
                    rimCol += rim * g * 0.6 * lerp(cyan, magenta, saturate(jit + 0.5));
                }

                float3 color = bodyCol + rimCol;
                color += _DissolveEdgeColor.rgb * edge * 2.0;
                color *= _EmissionStrength;

                float alpha = _Alpha * (0.4 + rim);
                alpha += edge * 1.5;

                // Light Form: push toward soft additive white bloom, reduce body.
                if (_BodilessAmount > 0.0001)
                {
                    float b = _BodilessAmount;
                    float3 whiteGlow = (rim + 0.25) * float3(1.0, 0.98, 0.95);
                    color = lerp(color, whiteGlow * _EmissionStrength, b);
                    alpha = lerp(alpha, saturate(rim + 0.35), b);
                }

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}

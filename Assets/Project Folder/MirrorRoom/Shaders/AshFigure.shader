Shader "Custom/AshFigure"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.32, 0.32, 0.34, 1)
        _CrackColor ("Crack / Glow Color", Color) = (0.25, 0.69, 0.80, 1)
        _GlowAmount ("Glow Amount", Range(0, 4)) = 0
        _CrackScale ("Crack Noise Scale", Float) = 6
        _CrackDensity ("Crack Density", Range(0,1)) = 0.55
        _RimColor ("Rim Color", Color) = (0.5, 0.55, 0.6, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (0.9, 0.75, 0.45, 1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.5)) = 0.08
        _DissolveScale ("Dissolve Noise Scale", Float) = 5
        _RiseAmount ("Fragment Rise", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float4 _CrackColor;
                float _GlowAmount;
                float _CrackScale;
                float _CrackDensity;
                float4 _RimColor;
                float _RimPower;
                float _DissolveAmount;
                float4 _DissolveEdgeColor;
                float _DissolveEdgeWidth;
                float _DissolveScale;
                float _RiseAmount;
            CBUFFER_END

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i + float3(0,0,0));
                float n100 = hash13(i + float3(1,0,0));
                float n010 = hash13(i + float3(0,1,0));
                float n110 = hash13(i + float3(1,1,0));
                float n001 = hash13(i + float3(0,0,1));
                float n101 = hash13(i + float3(1,0,1));
                float n011 = hash13(i + float3(0,1,1));
                float n111 = hash13(i + float3(1,1,1));
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
                // Fragments gently rise as they dissolve
                if (_RiseAmount > 0.0001)
                {
                    float n = valueNoise(posOS * 3.0);
                    posOS.y += _RiseAmount * n;
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

                // Dissolve clip
                float dnoise = valueNoise(input.positionWS * _DissolveScale);
                if (_DissolveAmount > 0.0001)
                    clip(dnoise - _DissolveAmount);
                float dedge = 0.0;
                if (_DissolveAmount > 0.0001)
                    dedge = saturate(1.0 - saturate((dnoise - _DissolveAmount) / max(_DissolveEdgeWidth, 1e-4)));

                // Simple lambert lighting from main light
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction)) * 0.6 + 0.4;
                float3 lit = _BaseColor.rgb * (mainLight.color * ndotl + 0.15);

                // Cracks: thin bright veins from noise
                float cnoise = valueNoise(input.positionWS * _CrackScale);
                float crackMask = smoothstep(_CrackDensity, _CrackDensity + 0.08, cnoise);
                float3 crackGlow = _CrackColor.rgb * crackMask * _GlowAmount;

                // Rim
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                float3 rimCol = _RimColor.rgb * rim * (0.3 + _GlowAmount * 0.5);

                float3 color = lit + crackGlow + rimCol;
                color += _DissolveEdgeColor.rgb * dedge * 3.0;

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster so figures cast shadows / receive depth
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AttributesS { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct VaryingsS { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float _DissolveAmount;
            float _DissolveScale;

            float hashS(float3 p){ p=frac(p*0.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z);}            

            VaryingsS vertShadow(AttributesS input)
            {
                VaryingsS o;
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                return o;
            }
            half4 fragShadow(VaryingsS input) : SV_Target
            {
                if (_DissolveAmount > 0.0001)
                {
                    float n = hashS(floor(input.positionWS * _DissolveScale));
                    clip(n - _DissolveAmount);
                }
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

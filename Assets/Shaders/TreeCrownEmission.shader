Shader "TreeRevival/CrownEmission"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        [HDR]_EmissionColor("Emission Color", Color) = (0,0,0,1)
        _CrownStartY("Crown Fade Start (WORLD Y)", Float) = 3.5
        _CrownEndY("Crown Fade End (WORLD Y)", Float) = 4.5
        _CanopyRadiusStart("Canopy Radius Start (local XY)", Float) = 0.08
        _CanopyRadiusEnd("Canopy Radius End (local XY)", Float) = 0.18
        _Smoothness("Smoothness", Range(0,1)) = 0.2
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                float _CrownStartY;
                float _CrownEndY;
                float _CanopyRadiusStart;
                float _CanopyRadiusEnd;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float crownMask : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                // 1. Height-mask (World space Y) to keep roots/lower trunk dark.
                float heightMask = smoothstep(_CrownStartY, _CrownEndY, OUT.positionWS.y);

                // 2. Radial-mask (Local space XY) to keep the central trunk column dark.
                // The trunk axis of the mesh is local Z, so distance from axis is length(local XY).
                float r = length(IN.positionOS.xy);
                float radialMask = smoothstep(_CanopyRadiusStart, _CanopyRadiusEnd, r);

                // 3. High-Canopy Blend: at the very top-center (World Y > 5.5), there is no main trunk anymore,
                // only branch tips. We fade out the radial exclusion so the top-center branches can glow fully.
                float topBlend = smoothstep(5.0, 7.5, OUT.positionWS.y);
                float combinedRadial = lerp(radialMask, 1.0, topBlend);

                OUT.crownMask = heightMask * combinedRadial;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Screen-space dither fade to avoid any transparency/sorting glitches (Schlieren)
                const float dither[16] = {
                    0.0625, 0.5625, 0.1875, 0.6875,
                    0.8125, 0.3125, 0.9375, 0.4375,
                    0.25,   0.75,   0.125,  0.625,
                    1.0,    0.5,    0.875,  0.375
                };
                int ditherX = int(fmod(IN.positionHCS.x, 4.0));
                int ditherY = int(fmod(IN.positionHCS.y, 4.0));
                float threshold = dither[ditherX + ditherY * 4];

                // Clip pixels based on alpha fade value (_BaseColor.a)
                clip(_BaseColor.a - threshold);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = baseTex * _BaseColor;

                SurfaceData surf = (SurfaceData)0;
                surf.albedo = albedo.rgb;
                surf.alpha = 1.0; // rendered as opaque
                surf.metallic = _Metallic;
                surf.smoothness = _Smoothness;
                surf.emission = _EmissionColor.rgb * IN.crownMask;
                surf.occlusion = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.bakedGI = SampleSH(inputData.normalWS);

                half4 color = UniversalFragmentPBR(inputData, surf);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}

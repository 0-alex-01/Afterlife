// Der "See der Seelen" — endlose schwarze Wasserfläche des Mirrorrooms.
// Unlit URP: tiefdunkle Basis, Fresnel-Schimmer zum Horizont, wandernde
// Glitzerpunkte, expandierende Schritt-Wellenringe (_Ripples, vom
// SoulLakeSurface-Skript gefüttert) und weiche Licht-Reflexe der treibenden
// Seelenlichter (_Glows). Nebel lässt Wasser und Himmel nahtlos verschmelzen.
Shader "Custom/SoulLake"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.008, 0.012, 0.022, 1)
        _SheenColor ("Horizon Sheen Color", Color) = (0.10, 0.16, 0.26, 1)
        _SheenPower ("Sheen Power", Range(0.5, 12)) = 5
        _SheenStrength ("Sheen Strength", Range(0, 3)) = 1.1

        _SparkleColor ("Sparkle Color", Color) = (0.45, 0.62, 0.85, 1)
        _SparkleScale ("Sparkle Scale", Float) = 3.0
        _SparkleSpeed ("Sparkle Speed", Float) = 0.05
        _SparkleThreshold ("Sparkle Threshold", Range(0.5, 0.99)) = 0.86
        _SparkleStrength ("Sparkle Strength", Range(0, 4)) = 1.4

        _RippleColor ("Ripple Color", Color) = (0.35, 0.55, 0.80, 1)
        _RippleSpeed ("Ripple Expansion (m/s)", Float) = 1.6
        _RippleWidth ("Ripple Ring Width (m)", Float) = 0.22
        _RippleLife ("Ripple Lifetime (s)", Float) = 3.5
        _RippleStrength ("Ripple Strength", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_RIPPLES 16
            #define MAX_GLOWS 24

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
                float  fogFactor  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _SheenColor;
                float _SheenPower;
                float _SheenStrength;
                float4 _SparkleColor;
                float _SparkleScale;
                float _SparkleSpeed;
                float _SparkleThreshold;
                float _SparkleStrength;
                float4 _RippleColor;
                float _RippleSpeed;
                float _RippleWidth;
                float _RippleLife;
                float _RippleStrength;
            CBUFFER_END

            // Von SoulLakeSurface pro Frame gesetzt (nicht serialisiert):
            // _Ripples[i] = (worldX, worldZ, startZeit, Stärke)
            // _Glows[i]   = (worldX, worldZ, Radius, Intensität), Farbe separat.
            float4 _Ripples[MAX_RIPPLES];
            float  _RippleCount;
            float4 _Glows[MAX_GLOWS];
            float4 _GlowColors[MAX_GLOWS];
            float  _GlowCount;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.fogFactor = ComputeFogFactor(vpi.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float2 wpos = input.positionWS.xz;

                // Tiefe Basis + Fresnel-Schimmer Richtung Horizont.
                float grazing = 1.0 - saturate(viewDir.y);
                float sheen = pow(grazing, _SheenPower) * _SheenStrength;
                float3 col = _DeepColor.rgb + _SheenColor.rgb * sheen;

                // Wandernde Glitzerpunkte: kleine weiche Lichtpunkte in den
                // Rasterzellen (nicht die ganze Zelle), zwei gegenläufige Ebenen.
                float t = _Time.y * _SparkleSpeed;
                float2 g1 = wpos * _SparkleScale + t * float2(3.7, 2.1);
                float n1 = hash21(floor(g1));
                float2 f1 = frac(g1) - 0.5;
                float p1 = saturate(1.0 - dot(f1, f1) * 16.0);
                float s1 = step(_SparkleThreshold, n1) * p1 * (0.5 + 0.5 * sin(_Time.y * 1.5 + n1 * 40.0));

                float2 g2 = wpos * (_SparkleScale * 2.3) - t * float2(2.3, 3.9);
                float n2 = hash21(floor(g2));
                float2 f2 = frac(g2) - 0.5;
                float p2 = saturate(1.0 - dot(f2, f2) * 16.0);
                float s2 = step(_SparkleThreshold + 0.03, n2) * p2 * (0.5 + 0.5 * sin(_Time.y * 2.3 + n2 * 40.0));

                // Zum Horizont hin funkeln (grazing²), direkt vor den Füßen kaum.
                col += _SparkleColor.rgb * (s1 + s2 * 0.6) * _SparkleStrength
                     * (0.05 + 0.95 * grazing * grazing);

                // Schritt-Wellen: expandierende, ausklingende Ringe.
                int rippleCount = min((int)_RippleCount, MAX_RIPPLES);
                for (int i = 0; i < rippleCount; i++)
                {
                    float4 r = _Ripples[i];
                    float age = _Time.y - r.z;
                    if (age < 0.0 || age > _RippleLife) continue;
                    float radius = age * _RippleSpeed;
                    float d = distance(wpos, r.xy);
                    float ring = 1.0 - saturate(abs(d - radius) / _RippleWidth);
                    ring *= ring;
                    float fade = 1.0 - saturate(age / _RippleLife);
                    col += _RippleColor.rgb * ring * fade * fade * r.w * _RippleStrength;
                }

                // Weiche Licht-Reflexe der Seelenlichter (leicht zum Betrachter gestreckt).
                int glowCount = min((int)_GlowCount, MAX_GLOWS);
                for (int j = 0; j < glowCount; j++)
                {
                    float4 g = _Glows[j];
                    float d = distance(wpos, g.xy) / max(g.z, 0.01);
                    float glow = saturate(1.0 - d);
                    glow *= glow * glow;
                    col += _GlowColors[j].rgb * glow * g.w;
                }

                col = MixFog(col, input.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

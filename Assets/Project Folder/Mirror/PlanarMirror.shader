Shader "Custom/PlanarMirror"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _BackColor ("Back Color", Color) = (1, 1, 1, 1)
        _FlipFace ("Flip Face", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "PlanarMirror"
            // URP rendert nur Pässe mit bekanntem LightMode-Tag im Forward-Pfad.
            // Ohne dieses Tag wurde der Pass uebersprungen -> Fallback-Unlit = "weisse Saeule".
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
            };

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            float4 _Tint;
            float4 _BackColor;
            float  _FlipFace;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positions.positionCS;
                OUT.screenPos = ComputeScreenPos(positions.positionCS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;
                half4 col = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv);
                
                // If the reflection is pure white (likely an error or fallback), return black
                // or if the color is precisely (0,0,0,0) (empty).
                if (col.r > 0.99 && col.g > 0.99 && col.b > 0.99) return half4(0, 0, 0, 1);
                
                return col * _Tint;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
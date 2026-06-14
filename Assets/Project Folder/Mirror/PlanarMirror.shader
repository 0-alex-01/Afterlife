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

            half4 frag (Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool isFront = IS_FRONT_VFACE(facing, true, false);
                if (_FlipFace > 0.5) isFront = !isFront;

                if (isFront)
                {
                    float2 uv = IN.screenPos.xy / IN.screenPos.w;
                    half4 col = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv);
                    return col * _Tint;
                }

                // Rueckseite: einfach weiss
                return _BackColor;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
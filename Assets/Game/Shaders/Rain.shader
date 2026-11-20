// QF/Rain — unlit alpha-blended line segments (THREE.LineSegments + LineBasicMaterial).
Shader "QF/Rain"
{
    Properties
    {
        _Color ("Colour (linear rgb, a = opacity)", Vector) = (0.667, 0.702, 0.722, 0.3)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Rain"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Assets/Game/Shaders/QFCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fog = QFFogFactor(positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 col = QFApplyFog(_Color.rgb, i.fog);
                return half4(col, _Color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

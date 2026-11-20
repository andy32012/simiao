// QF/Glow — camera-facing sprite (THREE.Sprite + SpriteMaterial{map: glowTex, AdditiveBlending}).
// Mesh: unit quad centred on the origin (planeGeo(1,1)). World size = the object's world scale.
Shader "QF/Glow"
{
    Properties
    {
        _MainTex ("Glow profile (alpha)", 2D) = "white" {}
        _Color ("Colour (linear rgb, a = opacity)", Vector) = (1, 1, 1, 1)
        _Fog ("Fog (0/1)", Float) = 1
        _Additive ("Additive", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Assets/Game/Shaders/QFCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Fog;
                float _Additive;
                float _DstBlend;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  fog        : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float4x4 m = GetObjectToWorldMatrix();
                float3 center = float3(m._m03, m._m13, m._m23);
                float2 scale = float2(length(float3(m._m00, m._m10, m._m20)), length(float3(m._m01, m._m11, m._m21)));
                float3 positionVS = TransformWorldToView(center);
                positionVS.xy += input.positionOS.xy * scale;
                o.positionCS = TransformWViewToHClip(positionVS);
                o.uv = input.uv;
                o.fog = QFFogFactor(center) * saturate(_Fog);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 col = tex.rgb * _Color.rgb;
                half alpha = tex.a * _Color.a;
                // additive: glow stays faintly visible through the fog; alpha blended: fade to the fog colour
                half3 addCol = col * (1.0 - i.fog * 0.85);
                half3 mixCol = QFApplyFog(col, i.fog);
                col = lerp(mixCol, addCol, saturate(_Additive));
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

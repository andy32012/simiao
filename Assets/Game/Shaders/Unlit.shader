// QF/Unlit — MeshBasicMaterial. Opaque by default; Render.cs basicMat() switches blend / zwrite / queue for transparent & additive.
Shader "QF/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour (linear rgb, a = opacity)", Vector) = (1, 1, 1, 1)
        _Fog ("Fog (0/1)", Float) = 1
        _Additive ("Additive (fog fades to black)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Game/Shaders/QFCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float _Fog;
            float _Additive;
            float _Cull;
            float _SrcBlend;
            float _DstBlend;
            float _ZWrite;
        CBUFFER_END

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

        #include "Assets/Game/Shaders/QFPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex QFVert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(QFVaryings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 col = tex.rgb * _Color.rgb;
                half alpha = tex.a * _Color.a;
                float f = QFFogFactor(i.positionWS) * saturate(_Fog);
                col = lerp(QFApplyFog(col, f), col * (1.0 - f), saturate(_Additive));
                return half4(col, alpha);
            }
            ENDHLSL
        }

        // Depth passes are only executed for materials in the opaque queue range.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex QFDepthVert
            #pragma fragment QFDepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex QFDepthVert
            #pragma fragment QFDepthNormalsFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    FallBack Off
}

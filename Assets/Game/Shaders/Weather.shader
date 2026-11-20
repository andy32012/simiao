// QF/Weather — MeshStandardMaterial + weatherize() (index.html 432-460): moss / grime / wetness / indoor darkness.
Shader "QF/Weather"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        [NoScaleOffset] _BumpMap ("Normal map", 2D) = "bump" {}
        _Roughness ("Roughness", Range(0, 1)) = 0.93
        _Metallic ("Metallic", Range(0, 1)) = 0
        _BaseColor ("Base colour (linear)", Vector) = (1, 1, 1, 1)
        _Emission ("Emission (linear)", Vector) = (0, 0, 0, 0)
        _Tint ("Tint (linear)", Vector) = (1, 1, 1, 1)
        _Tint2 ("Tint 2 (linear)", Vector) = (1, 1, 1, 1)
        _T2 ("Tint 2 amount", Float) = 0
        _T2Y ("Tint 2 height range (xy)", Vector) = (0, 1, 0, 0)
        _Moss ("Moss", Float) = 0.4
        _Gray ("Gray", Float) = 0
        _Amb ("Ambient multiplier", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Game/Shaders/QFCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseColor;
            float4 _Emission;
            float4 _Tint;
            float4 _Tint2;
            float4 _T2Y;
            float _T2;
            float _Moss;
            float _Gray;
            float _Amb;
            float _Roughness;
            float _Metallic;
            float _Cull;
        CBUFFER_END

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        #define QF_FLIP_BACKFACE
        #include "Assets/Game/Shaders/QFPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex QFVert
            #pragma fragment Frag
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing

            half4 Frag(QFVaryings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 Nv = normalize(i.normalWS);
                float wUp = Nv.y;                         // JS: world normal y of the (unflipped) vertex normal
                float3 wp = QFThreePos(i.positionWS);     // vWPos

                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _BaseColor.rgb;

                float wN1 = lerp(QF_NOISE(float2(wp.x + wp.z * 0.6, wp.y * 1.2 + wp.z * 0.3) * 0.085).r,
                                 QF_NOISE(wp.xz * 0.11).r, saturate(wUp));
                float wN2 = QF_NOISE(wp.xz * 0.53 + float2(wp.y * 0.37, wp.y * 0.21)).g;
                float wLum = dot(c, half3(0.3, 0.59, 0.11));
                float wT2 = _T2 * smoothstep(0.42, 0.62, wN1 * 0.55 + wN2 * 0.25 + (1.0 - smoothstep(_T2Y.x, _T2Y.y, wp.y)) * 0.75 - 0.1);
                c = lerp(c, wLum.xxx, _Gray) * lerp(_Tint.rgb, _Tint2.rgb, wT2);
                float wStreak = QF_NOISE(float2((wp.x + wp.z) * 0.33, wp.y * 0.035)).b;
                c *= 0.7 + 0.3 * wStreak;
                float wM = smoothstep(0.44, 0.64, wUp * 0.36 + wN1 * 0.72 + _Moss - 0.52);
                wM = max(wM, smoothstep(0.58, 0.82, wN2) * _Moss * 0.7 * step(-0.35, wUp));
                half3 wMc = lerp(half3(0.018, 0.032, 0.01), half3(0.07, 0.105, 0.03), wN2);
                c = lerp(c, wMc, wM * 0.85 * step(0.001, _Moss));

                float3 N = Nv * IS_FRONT_VFACE(face, 1.0, -1.0);
            #ifdef _NORMALMAP
                N = QFPerturbNormal(N, i.positionWS, i.uv, QFUnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv)));
            #endif

                QFSurface s;
                s.albedo = c;
                s.normalWS = N;
                s.roughness = lerp(_Roughness, 0.3, _QFWet * 0.65 * (0.45 + 0.55 * saturate(wUp)));
                s.metallic = _Metallic;
                s.emission = _Emission.rgb;
                s.amb = _Amb;
                s.wrap = 0;
                s.specScale = 1;

                half3 col = QFShade(s, i.positionWS, i.positionCS);
                col = QFApplyFog(col, QFFogFactor(i.positionWS));
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex QFShadowVert
            #pragma fragment QFShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

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

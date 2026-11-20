// QF/Foliage — alpha-tested, double-sided, wind-swayed cards (fir crowns, bamboo leaves, ferns, grass).
// Port of MeshStandardMaterial{alphaTest, DoubleSide} + swayMat() (index.html 1175-1185).
Shader "QF/Foliage"
{
    Properties
    {
        _MainTex ("Albedo (alpha = cutout)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.3
        _Color ("Tint (linear)", Vector) = (1, 1, 1, 1)
        _Gain ("Gain", Float) = 1
        _Roughness ("Roughness", Range(0, 1)) = 0.9
        _SwayAmp ("Sway amplitude", Float) = 0.05
        _SwayByY ("Sway weight: 0 = uv.y^2, 1 = height", Float) = 0
        _Wrap ("Diffuse wrap (0 = Lambert)", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Assets/Game/Shaders/QFCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float _Cutoff;
            float _Gain;
            float _Roughness;
            float _SwayAmp;
            float _SwayByY;
            float _Wrap;
        CBUFFER_END

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

        // JS: swPh = instanceMatrix[3].x*0.37 + instanceMatrix[3].z*0.23 (three-space translation of the instance)
        float3 QFSway(float3 p, float2 uv)
        {
            float4x4 m = GetObjectToWorldMatrix();
            float swPh = m._m03 * 0.37 + (-m._m23) * 0.23;
            float swW = lerp(uv.y * uv.y, saturate(p.y * 2.5), saturate(_SwayByY)) * _SwayAmp;
            float pz3 = -p.z;   // object-space z in three.js
            p.y += sin(_QFTime * 1.3 + swPh + p.x * 0.4) * swW;
            p.x += sin(_QFTime * 0.9 + swPh * 1.7 + pz3 * 0.3) * swW * 0.6;
            return p;
        }

        #define QF_DEFORM(p, uv) QFSway(p, uv)
        #define QF_ALPHATEST
        #include "Assets/Game/Shaders/QFPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex QFVert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing

            half4 Frag(QFVaryings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - _Cutoff);

                QFSurface s;
                s.albedo = tex.rgb * _Color.rgb * _Gain;
                s.normalWS = normalize(i.normalWS);     // as authored (pushed upwards), no back-face flip
                s.roughness = _Roughness;
                s.metallic = 0;
                s.emission = 0;
                s.amb = 1;
                s.wrap = _Wrap;
                s.specScale = 0.25;

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
            Cull Off

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
            Cull Off

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
            Cull Off

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

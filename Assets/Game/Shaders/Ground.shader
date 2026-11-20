// QF/Ground — terrain splat material (index.html 1120-1136).
// Vertex colour = splat: r moss, g path (mud), b stone, a brightness. Mesh uv = (x*0.21, z*0.21).
Shader "QF/Ground"
{
    Properties
    {
        _MainTex ("Forest floor albedo", 2D) = "white" {}
        [NoScaleOffset] _BumpMap ("Forest floor normal", 2D) = "bump" {}
        [NoScaleOffset] _MossTex ("Moss albedo", 2D) = "white" {}
        [NoScaleOffset] _MossN ("Moss normal", 2D) = "bump" {}
        [NoScaleOffset] _MudTex ("Mud albedo", 2D) = "white" {}
        [NoScaleOffset] _MudN ("Mud normal", 2D) = "bump" {}
        [NoScaleOffset] _StoneTex ("Stone albedo", 2D) = "white" {}
        [NoScaleOffset] _StoneN ("Stone normal", 2D) = "bump" {}
        _Roughness ("Roughness", Range(0, 1)) = 0.96
        _NormalStrength ("Normal strength (0 = flat, no normal maps needed)", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Game/Shaders/QFCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Roughness;
            float _NormalStrength;
        CBUFFER_END

        TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MossTex);  SAMPLER(sampler_MossTex);
        TEXTURE2D(_MossN);
        TEXTURE2D(_MudTex);   SAMPLER(sampler_MudTex);
        TEXTURE2D(_MudN);
        TEXTURE2D(_StoneTex); SAMPLER(sampler_StoneTex);
        TEXTURE2D(_StoneN);

        #define QF_USE_VCOLOR
        #include "Assets/Game/Shaders/QFPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
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
                float2 uv = i.uv;
                float4 splat = i.color;

                float gNz = QF_NOISE(uv * 0.085).r;
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half3 gMac = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv * 0.173 + 0.31).rgb;
                c = lerp(c, gMac, 0.42);
                float gWm = smoothstep(0.38, 0.66, splat.x + (gNz - 0.5) * 0.7);
                float gWp = smoothstep(0.30, 0.70, splat.y + (gNz - 0.5) * 0.4);
                float gWs = smoothstep(0.35, 0.65, splat.z + (gNz - 0.5) * 0.3);
                c = lerp(c, SAMPLE_TEXTURE2D(_MossTex, sampler_MossTex, uv * 0.83).rgb * 0.8, gWm);
                c = lerp(c, lerp(c * 0.8, SAMPLE_TEXTURE2D(_MudTex, sampler_MudTex, uv * 1.21).rgb * 2.4, 0.6), gWp * 0.9);
                c = lerp(c, SAMPLE_TEXTURE2D(_StoneTex, sampler_StoneTex, uv * 1.37).rgb * 0.9, gWs);
                c *= 0.4 + splat.w * 0.9;

                // blend the four tangent-space normals, then perturb
                float3 nT = QFUnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv));
                nT = lerp(nT, QFUnpackNormal(SAMPLE_TEXTURE2D(_MossN, sampler_MossTex, uv * 0.83)), gWm);
                nT = lerp(nT, QFUnpackNormal(SAMPLE_TEXTURE2D(_MudN, sampler_MudTex, uv * 1.21)), gWp);
                nT = lerp(nT, QFUnpackNormal(SAMPLE_TEXTURE2D(_StoneN, sampler_StoneTex, uv * 1.37)), gWs);
                nT.xy *= _NormalStrength;

                float3 Nv = normalize(i.normalWS);
                float3 N = QFPerturbNormal(Nv, i.positionWS, uv, nT);

                QFSurface s;
                s.albedo = c;
                s.normalWS = N;
                s.roughness = lerp(_Roughness, 0.2, _QFWet * (0.3 + 0.65 * gWp));
                s.metallic = 0;
                s.emission = 0;
                s.amb = 1;
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
            Cull Back

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
            Cull Back

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
            Cull Back

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

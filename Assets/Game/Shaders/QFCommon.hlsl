// Shared code for the QF/* shaders: globals, custom fog, hemisphere ambient, light loop, normal mapping without tangents.
#ifndef QF_COMMON_INCLUDED
#define QF_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---- globals set by G.renderApply() (all colours linear) ----
float4 _QFFogColor;
float  _QFFogDensity;
float4 _QFHemiSky;      // pre-multiplied by hemi intensity
float4 _QFHemiGround;   // pre-multiplied by hemi intensity
float  _QFTime;
float  _QFWet;
TEXTURE2D(_QFNoise); SAMPLER(sampler_QFNoise);

// used by the shadow caster pass (set by URP ShadowUtils)
float3 _LightDirection;
float3 _LightPosition;

#define QF_NOISE(uv) SAMPLE_TEXTURE2D(_QFNoise, sampler_QFNoise, uv)

// three.js world position (z mirrored) so world-space patterns match the original
float3 QFThreePos(float3 positionWS) { return float3(positionWS.x, positionWS.y, -positionWS.z); }

// THREE.FogExp2: 1 - exp(-(density*d)^2), radial distance
float QFFogFactor(float3 positionWS)
{
    float d = distance(positionWS, _WorldSpaceCameraPos.xyz) * _QFFogDensity;
    return saturate(1.0 - exp(-d * d));
}
half3 QFApplyFog(half3 col, float f) { return lerp(col, _QFFogColor.rgb, f); }

half3 QFAmbient(half3 n) { return lerp(_QFHemiGround.rgb, _QFHemiSky.rgb, 0.5 + 0.5 * n.y); }

// Works for plain RGB normal maps (alpha = 1), Unity "Normal map" imports (DXT5nm: x in A, y in G) and ASTC/RGB encoded ones.
float3 QFUnpackNormal(float4 p)
{
    p.a *= p.r;
    float3 n;
    n.xy = p.ag * 2.0 - 1.0;
    n.z = sqrt(max(1e-4, 1.0 - saturate(dot(n.xy, n.xy))));
    return n;
}
// raw xy of a normal texel (before *2-1), same encoding rules as above
float2 QFNormalXY(float4 p) { p.a *= p.r; return p.ag; }

// Screen-space cotangent frame (like three.js perturbNormal2Arb): no vertex tangents needed, sign-corrected so it is
// independent of handedness, winding and render-target flips.
float3 QFPerturbNormal(float3 N, float3 positionWS, float2 uv, float3 nTS)
{
    float3 dp1 = ddx(positionWS), dp2 = ddy(positionWS);
    float2 duv1 = ddx(uv), duv2 = ddy(uv);
    float3 dp2perp = cross(dp2, N), dp1perp = cross(N, dp1);
    float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
    float3 B = dp2perp * duv1.y + dp1perp * duv2.y;
    float sgn = dot(dp1, dp2perp) < 0.0 ? -1.0 : 1.0;
    float invmax = sgn * rsqrt(max(max(dot(T, T), dot(B, B)), 1e-20));
    return normalize(T * (nTS.x * invmax) + B * (nTS.y * invmax) + N * nTS.z);
}

struct QFSurface
{
    half3 albedo;      // linear
    half3 normalWS;
    half  roughness;
    half  metallic;
    half3 emission;
    half  amb;         // multiplies the ambient term only
    half  wrap;        // 0 = Lambert, >0 = wrapped diffuse (foliage)
    half  specScale;
};

half3 QFLightTerm(Light light, QFSurface s, half3 V, half3 diffuseColor, half3 F0)
{
    half ndl = dot(s.normalWS, light.direction);
    half diff = saturate((ndl + s.wrap) / (1.0 + s.wrap));
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);
    // URP style minimalist GGX
    float3 H = SafeNormalize(float3(light.direction) + float3(V));
    float NoH = saturate(dot(float3(s.normalWS), H));
    float LoH = saturate(dot(float3(light.direction), H));
    float a = max((float)s.roughness * (float)s.roughness, 0.008);
    float a2 = a * a;
    float d = NoH * NoH * (a2 - 1.0) + 1.00001;
    float specTerm = min(a2 / (d * d * max(0.1, LoH * LoH) * (a * 4.0 + 2.0)), 60.0);
    return (diffuseColor * diff + F0 * (specTerm * saturate(ndl) * s.specScale)) * radiance;
}

// positionCS = the fragment's SV_POSITION
half3 QFShade(QFSurface s, float3 positionWS, float4 positionCS)
{
    half3 V = GetWorldSpaceNormalizeViewDir(positionWS);

    InputData inputData = (InputData)0;     // the name "inputData" is required by LIGHT_LOOP_BEGIN
    inputData.positionWS = positionWS;
    inputData.normalWS = s.normalWS;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.shadowMask = half4(1, 1, 1, 1);
    half4 shadowMask = half4(1, 1, 1, 1);
    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, half(1.0));

    half3 diffuseColor = s.albedo * (1.0 - s.metallic);
    half3 F0 = lerp(half3(0.04, 0.04, 0.04), s.albedo, s.metallic);

    // hemisphere ambient (no SH, no reflection probes) + a faint sky reflection so metals are not pitch black
    half3 col = QFAmbient(s.normalWS) * diffuseColor * (s.amb * ao.indirectAmbientOcclusion);
    half3 R = reflect(-V, s.normalWS);
    col += QFAmbient(R) * F0 * ((1.0 - s.roughness) * s.amb * ao.indirectAmbientOcclusion);

    Light mainLight = GetMainLight(inputData, shadowMask, ao);
    col += QFLightTerm(mainLight, s, V, diffuseColor, F0);

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_FORWARD_PLUS
    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        Light dirLight = GetAdditionalLight(dirIndex, inputData, shadowMask, ao);
        col += QFLightTerm(dirLight, s, V, diffuseColor, F0);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
        col += QFLightTerm(light, s, V, diffuseColor, F0);
    LIGHT_LOOP_END
#endif

    col += s.emission;
    return col;
}

#endif

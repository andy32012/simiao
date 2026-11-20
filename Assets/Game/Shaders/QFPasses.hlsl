// Shared vertex stage + ShadowCaster / DepthOnly / DepthNormals passes for the lit QF shaders.
// Before including, a shader must have declared _MainTex (+ sampler_MainTex), _MainTex_ST and may define:
//   QF_DEFORM(posOS, uv)   object-space vertex deformation (default: none)
//   QF_ALPHATEST           clip against _Cutoff using _MainTex alpha
//   QF_USE_VCOLOR          pass the vertex colour through
//   QF_FLIP_BACKFACE       flip the normal on back faces in the DepthNormals pass
#ifndef QF_PASSES_INCLUDED
#define QF_PASSES_INCLUDED

#ifndef QF_DEFORM
#define QF_DEFORM(p, uv) (p)
#endif

struct QFAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
#ifdef QF_USE_VCOLOR
    float4 color      : COLOR;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct QFVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
#ifdef QF_USE_VCOLOR
    float4 color      : TEXCOORD3;
#endif
};

struct QFDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
};

void QFClip(float2 uv)
{
#ifdef QF_ALPHATEST
    clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a - _Cutoff);
#endif
}

QFVaryings QFVert(QFAttributes input)
{
    QFVaryings o;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 posOS = QF_DEFORM(input.positionOS.xyz, input.uv);
    o.positionWS = TransformObjectToWorld(posOS);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    o.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
#ifdef QF_USE_VCOLOR
    o.color = input.color;
#endif
    return o;
}

// ---------- ShadowCaster ----------
QFDepthVaryings QFShadowVert(QFAttributes input)
{
    QFDepthVaryings o;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 posOS = QF_DEFORM(input.positionOS.xyz, input.uv);
    float3 positionWS = TransformObjectToWorld(posOS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    o.positionCS = ApplyShadowClamping(positionCS);
    o.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    o.normalWS = normalWS;
    return o;
}
half4 QFShadowFrag(QFDepthVaryings i) : SV_Target
{
    QFClip(i.uv);
    return 0;
}

// ---------- DepthOnly / DepthNormals ----------
QFDepthVaryings QFDepthVert(QFAttributes input)
{
    QFDepthVaryings o;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 posOS = QF_DEFORM(input.positionOS.xyz, input.uv);
    o.positionCS = TransformObjectToHClip(posOS);
    o.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return o;
}
half4 QFDepthFrag(QFDepthVaryings i) : SV_Target
{
    QFClip(i.uv);
    return i.positionCS.z;
}
half4 QFDepthNormalsFrag(QFDepthVaryings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    QFClip(i.uv);
    float3 n = normalize(i.normalWS);
#ifdef QF_FLIP_BACKFACE
    n *= IS_FRONT_VFACE(face, 1.0, -1.0);
#endif
    return half4(n, 0.0);
}

#endif

// Shared helpers for the VAROGO lightning warning shaders.
// _WarnProgress: 0 -> 1 over the warning time (set per renderer by LightningStrike via MaterialPropertyBlock).
// When it is negative (default, no script), the shaders loop the whole sequence by themselves for previewing.
#ifndef VAROGO_LIGHTNING_WARN_COMMON
#define VAROGO_LIGHTNING_WARN_COMMON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define TAU 6.2831853

float WarnTime(float overrideT) { return overrideT >= 0.0 ? overrideT : _Time.y; }

// timeline: cloud crack + arcane sigil 0.00-0.78 | yellow target rings 0.52-1.00
float WarnK(float progress, float t)
{
    if (progress >= 0.0) return saturate(progress);
    return saturate(frac(t / 2.6) * 1.25);       // preview loop: 2.1 s sequence + short hold
}

float Hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }
float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

float VNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), u.x), u.y);
}

float Fbm(float2 p)
{
    float s = 0.0, a = 0.5;
    for (int k = 0; k < 4; k++) { s += VNoise(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
    return s;
}

// anti-aliased ring at radius c with half width w
float RingAA(float r, float c, float w)
{
    float aa = fwidth(r) * 1.2;
    return 1.0 - smoothstep(w - aa, w + aa, abs(r - c));
}

// anti-aliased line distance mask
float LineAA(float d, float w)
{
    float aa = fwidth(d) * 1.2;
    return 1.0 - smoothstep(w - aa, w + aa, d);
}

// Camera-facing quad around the object origin; size comes from the object scale (x, y).
float4 BillboardCS(float3 positionOS)
{
    float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
    float sx = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
    float sy = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
    float3 right = UNITY_MATRIX_V[0].xyz;
    float3 up = UNITY_MATRIX_V[1].xyz;
    float3 posWS = centerWS + right * positionOS.x * sx + up * positionOS.y * sy;
    return TransformWorldToHClip(posWS);
}

#endif

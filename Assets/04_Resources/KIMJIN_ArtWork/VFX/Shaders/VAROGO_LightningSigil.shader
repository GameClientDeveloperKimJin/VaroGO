// VAROGO lightning warning - ground circle (URP, unlit, additive). Put on a flat Quad (X 90).
// Stage 1: violet/cyan arcane sigil draws itself in a sweep (rings, rune ticks, hexagram) and slowly turns.
// Stage 2: sigil turns into bright yellow target rings that rush inward onto the strike radius.
Shader "VAROGO/LightningSigil"
{
    Properties
    {
        _ArcaneColor ("Arcane Color", Color) = (0.72, 0.38, 1.0, 1)
        _EdgeColor   ("Arcane Edge Color", Color) = (0.35, 0.85, 1.0, 1)
        _TargetColor ("Target Ring Color", Color) = (1.0, 0.88, 0.25, 1)
        _StrikeRadius ("Strike Radius (0-1 of quad)", Range(0.1,1)) = 0.43
        _Glow        ("Glow", Range(0,4)) = 1.7
        _Intensity   ("Intensity", Range(0,5)) = 1
        _WarnProgress ("Warn Progress (-1 = preview loop)", Float) = -1
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "VAROGO_LightningWarnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ArcaneColor, _EdgeColor, _TargetColor;
                half _StrikeRadius, _Glow, _Intensity;
                float _WarnProgress, _TimeOverride;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WarnTime(_TimeOverride);
                float k = WarnK(_WarnProgress, t);
                float s2 = smoothstep(0.0, 0.06, k) * (1.0 - smoothstep(0.62, 0.78, k));
                float s3 = smoothstep(0.52, 0.66, k);
                if (s2 + s3 <= 0.001) return 0;

                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float spin = t * 0.25;
                float ang = atan2(p.y, p.x);
                float a01 = frac((ang + spin) / TAU + 0.25);
                float reveal = saturate(k / 0.35);
                float sweep = step(a01, reveal * 1.02);

                // ---- arcane sigil ----
                half edge = 0, arc = 0;
                edge += RingAA(r, 0.95, 0.014);
                edge += RingAA(r, 0.86, 0.008);
                arc  += RingAA(r, 0.60, 0.010);
                arc  += RingAA(r, 0.30, 0.008);
                // rune ticks between outer rings
                float runeA = frac((ang + spin) / TAU * 36.0);
                arc += step(0.55, runeA) * step(runeA, 0.85) * step(0.885, r) * step(r, 0.925) * step(0.5, Hash11(floor((ang + spin) / TAU * 36.0)) + 0.3);
                // hexagram: 6 lines tangent to circle radius 0.30, clipped to the 0.60 ring
                for (int n = 0; n < 6; n++)
                {
                    float an = -spin * 1.5 + n * 1.0472;
                    float2 d = float2(cos(an), sin(an));
                    float dist = abs(dot(p, float2(-d.y, d.x)) - 0.30);
                    arc += LineAA(dist, 0.008) * step(abs(dot(p, d)), 0.52) * step(r, 0.61);
                    // small circles at the star points
                    float2 pt = d * 0.60;
                    edge += RingAA(length(p - pt), 0.05, 0.006);
                }
                half sigil = saturate(arc + edge) * sweep;
                half halo = RingAA(r, 0.90, 0.10) * 0.18 + (1.0 - smoothstep(0.0, 0.95, r)) * 0.08;
                half3 colS = (_ArcaneColor.rgb * saturate(arc) + _EdgeColor.rgb * saturate(edge)) * sweep + _ArcaneColor.rgb * halo;
                half aS = saturate(sigil + halo) * s2;

                // ---- target rings (stage 3) ----
                float R = _StrikeRadius;
                half tr = RingAA(r, R, 0.022) * (0.85 + 0.15 * sin(t * 18.0));
                tr += RingAA(r, R + 0.06, 0.006) * 0.7;
                for (int m = 0; m < 3; m++)
                {
                    float rr = lerp(1.0, R, frac(t * 1.3 + m / 3.0));
                    tr += RingAA(r, rr, 0.010) * saturate((rr - R) / (1.0 - R)) * 0.9;
                }
                tr += (1.0 - smoothstep(0.0, R, r)) * 0.18;           // hot fill inside the strike radius
                half3 colT = _TargetColor.rgb * tr;
                half aT = saturate(tr) * s3;

                half3 col = (colS * s2 + colT * s3) * _Glow;
                half a = saturate(aS + aT) * step(r, 1.0);
                return half4(col * _Intensity, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

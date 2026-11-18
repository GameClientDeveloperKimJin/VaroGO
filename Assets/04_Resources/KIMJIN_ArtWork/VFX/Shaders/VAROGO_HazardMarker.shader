// VAROGO flight obstacle warning (URP, unlit, additive). Two looks in one shader:
//   _Mode 0 = ground target marker: flat Quad (X 90) at the impact point. Red dashed ring, rings rushing inward,
//             four chevrons closing in on the center, hot center that blinks faster near impact.
//   _Mode 1 = "!" alert sign: camera-facing Quad above the marker. Rounded warning triangle with "!".
// _WarnProgress 0 -> 1 over the warning time (MaterialPropertyBlock from the hazard script); -1 = preview loop.
Shader "VAROGO/HazardMarker"
{
    Properties
    {
        [Enum(Marker,0,Alert,1)] _Mode ("Mode", Float) = 0
        _MainColor   ("Main Color", Color) = (1.0, 0.25, 0.12, 1)
        _HotColor    ("Hot Color", Color) = (1.0, 0.75, 0.3, 1)
        _DarkColor   ("Sign Fill Color (Alert)", Color) = (0.45, 0.05, 0.03, 1)
        _TargetRadius ("Target Radius (0-1)", Range(0.1, 0.9)) = 0.5
        _Glow        ("Glow", Range(0, 6)) = 2.0
        _Alpha       ("Fade", Range(0, 1)) = 1
        _WarnProgress ("Warn Progress (-1 = preview loop)", Float) = -1
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "VAROGO_LightningWarnCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MainColor, _HotColor, _DarkColor;
                half _TargetRadius, _Glow, _Alpha;
                float _Mode, _WarnProgress, _TimeOverride;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = _Mode > 0.5 ? BillboardCS(v.positionOS.xyz) : TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float2 Rot(float2 p, float a) { float c = cos(a), s = sin(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            // distance to segment
            float Seg(float2 p, float2 a, float2 b) { float2 pa = p - a, ba = b - a; float h = saturate(dot(pa, ba) / dot(ba, ba)); return length(pa - ba * h); }

            half4 Marker(float2 p, float t, float k)
            {
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float R = _TargetRadius;
                float blink = 0.65 + 0.35 * step(0.5, frac(t * lerp(3.0, 12.0, k)));
                half add = 0, hot = 0;

                float dash = step(0.4, frac(ang / TAU * 24.0 + t * 0.4));
                add += RingAA(r, 0.95, 0.018) * dash;
                add += RingAA(r, 0.88, 0.006) * 0.7;
                // rings rushing inward, faster near impact
                for (int n = 0; n < 3; n++)
                {
                    float rr = lerp(0.95, R, frac(t * lerp(0.8, 2.4, k) + n / 3.0));
                    add += RingAA(r, rr, 0.012) * saturate((rr - R) / (0.95 - R)) * 0.9;
                }
                // four chevrons closing in
                float cr = lerp(0.85, R + 0.08, k);
                float2 q = Rot(p, t * 0.6);
                for (int m = 0; m < 4; m++)
                {
                    float2 qq = Rot(q, m * 1.5707963);
                    float2 tip = float2(cr - 0.07, 0);
                    float d = min(Seg(qq, tip, tip + float2(0.16, 0.13)), Seg(qq, tip, tip + float2(0.16, -0.13)));
                    hot += LineAA(d, 0.026);
                }
                // target ring + hot center
                hot += RingAA(r, R, 0.02) * blink;
                half centre = (1.0 - smoothstep(0.0, R, r)) * (0.25 + 0.35 * k) * blink;
                half3 col = _MainColor.rgb * (saturate(add) + centre) + _HotColor.rgb * saturate(hot);
                half a = saturate(saturate(add) + saturate(hot) + centre) * step(r, 1.0);
                col *= _Glow * lerp(0.6, 1.3, k);
                return half4(col * a, a * 0.6);   // mostly additive look
            }

            half4 Alert(float2 p, float t, float k)
            {
                float bob = 1.0 + 0.08 * sin(t * lerp(6.0, 16.0, k));
                p /= bob;
                // rounded triangle (point up)
                float2 a = float2(0, 0.78), b = float2(-0.82, -0.62), c = float2(0.82, -0.62);
                float e = min(min(Seg(p, a, b), Seg(p, b, c)), Seg(p, c, a));
                float2 n0 = normalize(float2(-(b - a).y, (b - a).x)), n1 = normalize(float2(-(c - b).y, (c - b).x)), n2 = normalize(float2(-(a - c).y, (a - c).x));
                bool inside = dot(p - a, n0) >= 0 && dot(p - b, n1) >= 0 && dot(p - c, n2) >= 0;
                float sd = inside ? -e : e;
                half fill = 1.0 - smoothstep(-0.02, 0.0, sd - 0.06);
                half rim = 1.0 - smoothstep(0.03, 0.05, abs(sd - 0.03));
                half glow = saturate(1.0 - (sd - 0.06) / 0.3) * step(0.0, sd - 0.06) * 0.6;
                // "!" mark
                float bar = Seg(p, float2(0, 0.35), float2(0, -0.12));
                float dot_ = length(p - float2(0, -0.36));
                half mark = max(1.0 - smoothstep(0.075, 0.1, bar), 1.0 - smoothstep(0.085, 0.11, dot_));
                half blink = 0.7 + 0.3 * step(0.5, frac(t * lerp(3.0, 10.0, k)));
                half4 o = 0;
                o.rgb = _DarkColor.rgb * fill; o.a = fill * 0.9;
                o.rgb = lerp(o.rgb, half3(1, 0.97, 0.9) * 1.6, mark * fill); o.a = max(o.a, mark * fill);
                o.rgb += (_HotColor.rgb * rim * 1.6 + _MainColor.rgb * glow) * blink * _Glow * 0.6;
                o.a = saturate(o.a + rim * 0.9);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WarnTime(_TimeOverride);
                float k = WarnK(_WarnProgress, t);
                float appear = smoothstep(0.0, 0.08, k);
                float2 p = i.uv * 2.0 - 1.0;
                half4 o = _Mode > 0.5 ? Alert(p, t, k) : Marker(p, t, k);
                return o * appear * _Alpha;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

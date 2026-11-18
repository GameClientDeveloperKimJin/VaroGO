// VAROGO rune ring (URP, unlit, additive). Put on a Quad in front of the stone ring of the dungeon gate.
// Draws a band of glowing violet rune glyphs around the circle, a light wave runs around the ring,
// thin guide circles inside / outside, soft glow. _Alpha follows the vortex fade (MaterialPropertyBlock).
Shader "VAROGO/RuneRing"
{
    Properties
    {
        _RuneColor  ("Rune Color", Color) = (0.78, 0.45, 1.0, 1)
        _LineColor  ("Guide Line Color", Color) = (0.55, 0.4, 1.0, 1)
        _Radius     ("Band Center Radius (0-1)", Range(0.3, 0.98)) = 0.84
        _Width      ("Band Width", Range(0.02, 0.3)) = 0.09
        _Count      ("Rune Count", Float) = 28
        _Spin       ("Spin (turns per second)", Float) = -0.01
        _WaveSpeed  ("Light Wave Speed", Float) = 0.35
        _Glow       ("Glow", Range(0, 6)) = 2.2
        _Alpha      ("Fade (0 hidden - 1 full)", Range(0, 1)) = 1
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-40" "IgnoreProjector"="True" }
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
                half4 _RuneColor, _LineColor;
                half _Radius, _Width, _Glow, _Alpha;
                float _Count, _Spin, _WaveSpeed, _TimeOverride;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            float Seg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // procedural glyph in cell space q (-1..1), built from strokes chosen by the cell id
            float Glyph(float2 q, float id)
            {
                float d = 9.0;
                d = min(d, Seg(q, float2(0, -0.8), float2(0, 0.8)));                                   // stem
                if (Hash11(id + 1.1) > 0.4) d = min(d, Seg(q, float2(-0.5, 0.55), float2(0.5, 0.55)));  // top bar
                if (Hash11(id + 2.3) > 0.5) d = min(d, Seg(q, float2(-0.5, -0.1), float2(0.0, 0.35))); // left diagonal
                if (Hash11(id + 3.7) > 0.5) d = min(d, Seg(q, float2(0.5, -0.1), float2(0.0, 0.35)));  // right diagonal
                if (Hash11(id + 4.9) > 0.6) d = min(d, abs(length(q - float2(0, -0.35)) - 0.3));       // small circle
                if (Hash11(id + 6.1) > 0.55) d = min(d, Seg(q, float2(-0.5, -0.65), float2(0.5, -0.65)));// bottom bar
                if (Hash11(id + 7.3) > 0.7) d = min(d, Seg(q, float2(-0.45, 0.8), float2(0.45, -0.8)));  // slash
                return d;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WarnTime(_TimeOverride);
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float a = frac(atan2(p.y, p.x) / TAU + 0.5 + t * _Spin);

                float rb = (r - _Radius) / _Width;                     // -1..1 across the band
                float cell = a * _Count;
                float id = floor(cell);
                float2 q = float2((frac(cell) - 0.5) * 2.0 * (_Radius * TAU / _Count) / (2.0 * _Width) * 1.1, rb * 1.15);

                float gd = Glyph(q, id);
                float stroke = 1.0 - smoothstep(0.07, 0.13, gd);
                float glowS = saturate(1.0 - gd / 0.6) * 0.35;
                float inBand = step(abs(rb), 1.0);

                // light wave running around the ring + per-rune flicker
                float wave = 0.5 + 0.5 * cos((a - t * _WaveSpeed) * TAU * 2.0);
                wave = 0.35 + 0.65 * pow(wave, 3.0);
                float flick = 0.8 + 0.2 * step(0.5, Hash11(id * 3.3 + floor(t * 8.0)));

                // guide circles
                float lines = RingAA(r, _Radius + _Width * 1.25, 0.004) + RingAA(r, _Radius - _Width * 1.25, 0.004);
                lines += RingAA(r, _Radius + _Width * 1.45, 0.0025) * 0.6;

                half3 col = _RuneColor.rgb * (stroke + glowS) * inBand * wave * flick;
                col += _LineColor.rgb * lines * (0.5 + 0.5 * wave);
                col += _RuneColor.rgb * saturate(1.0 - abs(rb) * 0.9) * 0.08 * wave;   // faint band haze
                half aOut = saturate(max(max(stroke, glowS) * inBand, lines) + 0.0);
                return half4(col * _Glow * _Alpha, aOut * _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

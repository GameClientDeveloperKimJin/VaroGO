// VAROGO lobby portal ground magic circle (URP, unlit, additive glow). Put on a flat Quad.
// Procedural rings, rune ticks and a star, slowly rotating and pulsing.
Shader "VAROGO/MagicCircle"
{
    Properties
    {
        _GoldColor   ("Ring Color", Color) = (1.0, 0.80, 0.38, 1)
        _TealColor   ("Inner Color", Color) = (0.30, 0.95, 0.90, 1)
        _Glow        ("Glow Strength", Range(0,4)) = 1.4
        _Spin        ("Spin Speed", Float) = 0.08
        _Pulse       ("Pulse Amount", Range(0,1)) = 0.25
        _Intensity   ("Intensity (flash)", Range(0,5)) = 1
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GoldColor, _TealColor;
                half _Glow, _Pulse, _Intensity;
                float _Spin, _TimeOverride;
            CBUFFER_END

            float WTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            float ring(float r, float c, float w) { return saturate(1.0 - abs(r - c) / w); }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WTime();
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float a1 = atan2(p.y, p.x) + t * _Spin * 6.2832;      // outer turns one way
                float a2 = atan2(p.y, p.x) - t * _Spin * 9.0;         // inner turns the other way

                float g = 0;
                g += ring(r, 0.95, 0.025);
                g += ring(r, 0.86, 0.015);
                g += ring(r, 0.58, 0.018);
                // rune ticks between the two outer rings
                float ticks = step(0.75, frac(a1 * 48.0 / 6.2832)) * step(0.87, r) * step(r, 0.94);
                float marks = step(0.92, frac(a1 * 12.0 / 6.2832 + 0.04)) * step(0.60, r) * step(r, 0.85);
                g += ticks * 0.8 + marks;

                // 6-point star from two triangles (distance to lines), inner ring
                float star = 0;
                for (int k = 0; k < 6; k++)
                {
                    float ang = a2 + k * 1.0472;
                    float2 d = float2(cos(ang), sin(ang));
                    float dist = abs(dot(p, float2(-d.y, d.x)) );
                    float along = dot(p, d);
                    star += saturate(1.0 - abs(dist - 0.29) / 0.012) * step(abs(along), 0.5) * step(r, 0.58);
                }
                half tealPart = saturate(star + ring(r, 0.3, 0.02) + ring(r, 0.12, 0.05) * 0.6);

                float pulse = 1.0 + sin(t * 2.2) * _Pulse;
                half3 col = (_GoldColor.rgb * saturate(g) + _TealColor.rgb * tealPart) * _Glow * pulse * _Intensity;
                // soft inner glow
                col += _TealColor.rgb * saturate(1.0 - r) * 0.12 * _Glow * _Intensity;
                half a = saturate(max(saturate(g), tealPart) + saturate(1.0 - r) * 0.15) * step(r, 1.0);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

// VAROGO lobby portal surface: spinning swirl inside a soft oval (URP, unlit, transparent).
// Put on a Quad over the portal face. UV (0..1) maps to the oval.
// _Intensity can be raised by code for the entrance flash (1 = normal).
Shader "VAROGO/PortalSwirl"
{
    Properties
    {
        _CoreColor   ("Center Color", Color) = (0.92, 1.0, 1.0, 1)
        _InnerColor  ("Swirl Color", Color)  = (0.25, 0.85, 0.85, 1)
        _OuterColor  ("Outer Color", Color)  = (0.10, 0.45, 0.60, 1)
        _RimColor    ("Rim Glow Color", Color) = (1.0, 0.82, 0.40, 1)
        _Arms        ("Swirl Arms", Float) = 3
        _Twist       ("Twist", Float) = 3.5
        _Speed       ("Spin Speed", Float) = 0.6
        _Contrast    ("Swirl Contrast", Range(0,1)) = 0.55
        _RimWidth    ("Rim Width", Range(0,0.3)) = 0.08
        _EdgeSoft    ("Edge Softness", Range(0.01,0.3)) = 0.06
        _Alpha       ("Opacity", Range(0,1)) = 0.92
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
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _InnerColor, _OuterColor, _RimColor;
                float _Arms, _Twist, _Speed;
                half _Contrast, _RimWidth, _EdgeSoft, _Alpha, _Intensity;
                float _TimeOverride;
            CBUFFER_END

            float WTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), u.x), lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WTime();
                float2 p = i.uv * 2.0 - 1.0;          // -1..1 oval space
                float r = length(p);
                float ang = atan2(p.y, p.x);

                // swirl bands spiralling into the center
                float spiral = ang * _Arms + log(max(r, 1e-3)) * _Twist * 2.0 + t * _Speed * 6.2832;
                float bands = sin(spiral) * 0.5 + 0.5;
                float n = vnoise(float2(ang * 2.0 + t * 0.4, r * 6.0 - t * 1.5));
                float s = saturate(lerp(0.5, bands, _Contrast) + (n - 0.5) * 0.35);

                half3 col = lerp(_OuterColor.rgb, _InnerColor.rgb, saturate(s * 1.2 - r * 0.3 + 0.2));
                col = lerp(col, _CoreColor.rgb, saturate(1.0 - r * 2.2) * 0.9);           // bright core
                // small twinkles
                float tw = step(0.985, hash21(floor(p * 28.0 + float2(0, floor(t * 3.0)))));
                col += tw * 0.6 * (1.0 - r);

                // oval mask + golden rim
                half mask = 1.0 - smoothstep(1.0 - _EdgeSoft, 1.0, r);
                half rim = smoothstep(1.0 - _RimWidth - _EdgeSoft, 1.0 - _EdgeSoft * 0.5, r) * mask;
                col = lerp(col, _RimColor.rgb, rim * 0.8);

                col *= _Intensity;
                half a = mask * _Alpha;
                a = saturate(a * lerp(1.0, 1.3, saturate(_Intensity - 1.0)));
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

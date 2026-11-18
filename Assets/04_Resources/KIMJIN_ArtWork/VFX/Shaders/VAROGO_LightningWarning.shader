// VAROGO lightning warning circle (URP, unlit, premultiplied alpha). Put on a flat Quad (X 90).
// Cartoon hazard rim (rotating yellow / dark stripes with outline), counter-rotating dash ring,
// ripples rushing inward ("charging"), amber fill and a pulsing bolt icon in the middle.
// Fully time driven: LightningStrike.cs only scales / blinks the parent, no script values needed.
Shader "VAROGO/LightningWarning"
{
    Properties
    {
        _IconTex     ("Center Icon (RGBA)", 2D) = "white" {}
        _HotColor    ("Hot Color (stripes, ripples)", Color) = (1.0, 0.86, 0.18, 1)
        _DarkColor   ("Dark Color (stripes, outline)", Color) = (0.42, 0.13, 0.04, 1)
        _FillColor   ("Fill Color", Color) = (1.0, 0.55, 0.08, 1)
        _FillAlpha   ("Fill Alpha", Range(0,1)) = 0.28
        _StripeCount ("Rim Stripe Count", Float) = 18
        _Spin        ("Spin Speed", Float) = 0.35
        _RippleSpeed ("Ripple Speed", Float) = 1.6
        _Glow        ("Glow Strength", Range(0,4)) = 1.6
        _Intensity   ("Intensity", Range(0,5)) = 1
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_IconTex); SAMPLER(sampler_IconTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _IconTex_ST;
                half4 _HotColor, _DarkColor, _FillColor;
                half _FillAlpha, _Glow, _Intensity;
                float _StripeCount, _Spin, _RippleSpeed, _TimeOverride;
            CBUFFER_END

            float WTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            // anti-aliased band [a, b] on value x
            float band(float x, float a, float b)
            {
                float w = fwidth(x) * 1.2;
                return smoothstep(a - w, a + w, x) * (1.0 - smoothstep(b - w, b + w, x));
            }

            // "over" compositing in premultiplied space
            void over(inout half4 dst, half3 c, half a) { dst.rgb = c * a + dst.rgb * (1.0 - a); dst.a = a + dst.a * (1.0 - a); }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WTime();
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x) / 6.2832 + 0.5;          // 0..1
                float beat = 0.5 + 0.5 * sin(t * 9.0);               // fast heartbeat
                half4 o = 0;

                // outer soft glow halo
                half halo = saturate(1.0 - abs(r - 0.86) / 0.14);
                halo *= halo;
                o.rgb += _HotColor.rgb * halo * 0.55 * _Glow * (0.7 + 0.3 * beat);
                o.a   += halo * 0.25;

                // amber fill, brighter toward rim
                half fill = band(r, -1.0, 0.76) * lerp(0.45, 1.0, saturate(r / 0.76));
                over(o, _FillColor.rgb * (0.9 + 0.3 * beat), fill * _FillAlpha);

                // ripples rushing inward (3 rings)
                for (int k = 0; k < 3; k++)
                {
                    float rr = frac(-t * _RippleSpeed * 0.5 + k / 3.0) * 0.72;
                    half rip = band(r, rr - 0.018, rr + 0.018) * saturate(rr / 0.72) ;
                    over(o, _HotColor.rgb * 1.2, rip * 0.85);
                }

                // counter-rotating dash ring
                float dash = step(0.45, frac((ang + t * _Spin * 0.6) * 24.0));
                over(o, _HotColor.rgb * 1.15, band(r, 0.585, 0.635) * dash);

                // hazard rim: dark outline + rotating diagonal stripes
                half rimOut = band(r, 0.735, 0.955);
                over(o, _DarkColor.rgb, rimOut);
                float sCoord = ang * _StripeCount + (r - 0.76) * 3.0 - t * _Spin;
                half stripe = step(0.5, frac(sCoord));
                half3 rimCol = lerp(_DarkColor.rgb * 1.6, _HotColor.rgb * (1.15 + 0.25 * beat), stripe);
                over(o, rimCol, band(r, 0.765, 0.925));
                // thin bright lip on the inner edge
                over(o, _HotColor.rgb * 1.5, band(r, 0.745, 0.765) * 0.9);

                // center icon, pulsing
                float s = 0.30 * (1.0 + 0.10 * beat);
                float2 iuv = p / (2.0 * s) + 0.5;
                half inIcon = step(0.0, iuv.x) * step(iuv.x, 1.0) * step(0.0, iuv.y) * step(iuv.y, 1.0);
                half4 ic = SAMPLE_TEXTURE2D(_IconTex, sampler_IconTex, saturate(iuv));
                over(o, ic.rgb * (1.0 + 0.35 * beat), ic.a * inIcon);

                o.rgb *= _Intensity;
                o.a = saturate(o.a);
                o *= step(r, 1.0);
                return o;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

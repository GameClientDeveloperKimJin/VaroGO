// VAROGO flight vortex = dungeon entrance (URP, unlit, premultiplied alpha). Put on a Quad facing the player.
// One disc does everything:
//   inner tunnel : fake-depth cloud tunnel pulled inward, navy -> violet -> magenta -> white-cyan core
//   outer arms   : spiral storm-cloud arms that fade into the sky
//   lightning    : thin arcs flickering along the arms
//   debris       : small rock chips spiralling into the tunnel
// FlightAtmosphere scales and spins the parent; the shader only adds inward flow, so keep the spin moderate.
// Fog is ignored on purpose so the vortex reads through the dark storm fog.
Shader "VAROGO/FlightVortex"
{
    Properties
    {
        _DeepColor    ("Cloud Dark Color", Color) = (0.07, 0.07, 0.2, 1)
        _MidColor     ("Cloud Violet Color", Color) = (0.42, 0.2, 0.85, 1)
        _HotColor     ("Inner Magenta Color", Color) = (0.95, 0.38, 1.0, 1)
        _CoreColor    ("Core Color", Color) = (0.85, 0.97, 1.0, 1)
        _BoltColor    ("Lightning Color", Color) = (0.8, 0.75, 1.0, 1)
        _ChipColor    ("Debris Color", Color) = (0.2, 0.17, 0.24, 1)
        _TunnelRadius ("Tunnel Radius (0-1)", Range(0.2, 0.7)) = 0.42
        _Arms         ("Spiral Arms", Float) = 5
        _Twist        ("Spiral Twist", Float) = 1.6
        _Flow         ("Inward Flow Speed", Float) = 0.6
        _CoreGlow     ("Core Glow", Range(0, 6)) = 2.5
        _BoltAmount   ("Lightning Amount", Range(0, 1)) = 0.55
        _Intensity    ("Intensity", Range(0, 4)) = 1
        _Alpha        ("Fade (0 hidden - 1 full)", Range(0, 1)) = 1
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" "IgnoreProjector"="True" }
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
                half4 _DeepColor, _MidColor, _HotColor, _CoreColor, _BoltColor, _ChipColor;
                half _TunnelRadius, _CoreGlow, _BoltAmount, _Intensity, _Alpha;
                float _Arms, _Twist, _Flow, _TimeOverride;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            // seamless-in-angle cloud noise: a = angle (0-1), z = radial / depth coordinate
            float CloudNoise(float a, float z, float freq)
            {
                float2 c = float2(cos(a * TAU), sin(a * TAU)) * freq;
                return Fbm(c + float2(z, -z * 0.73));
            }

            void Over(inout half4 dst, half3 c, half a) { dst.rgb = c * a + dst.rgb * (1.0 - a); dst.a = a + dst.a * (1.0 - a); }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WarnTime(_TimeOverride);
                float2 p = i.uv * 2.0 - 1.0;
                float r = max(length(p), 1e-4);
                float a = atan2(p.y, p.x) / TAU + 0.5;
                float R0 = _TunnelRadius;
                half4 o = 0;

                // ---------- outer spiral arms ----------
                float s = a * _Arms - log(r) * _Twist * 1.4 - t * _Flow * 0.5;
                float arm = 0.5 + 0.5 * cos(s * TAU);
                arm = smoothstep(0.1, 0.9, arm);
                float cl = CloudNoise(a + log(r) * 0.25, r * 3.0 - t * _Flow * 0.6, 3.2);
                float cl2 = CloudNoise(a + log(r) * 0.4, r * 7.0 - t * _Flow, 5.0);
                float dens = arm * 0.55 + cl * 0.6 + cl2 * 0.35 - 0.25;
                half armA = smoothstep(0.15, 0.6, dens) * (1.0 - smoothstep(0.72, 1.0, r)) * smoothstep(R0 - 0.06, R0 + 0.02, r);
                float inner = 1.0 - saturate((r - R0) / (1.0 - R0));          // 1 near the tunnel, 0 at the edge
                half3 armCol = lerp(_DeepColor.rgb, _MidColor.rgb, saturate(cl * 0.9 + inner * 0.7));
                armCol += _HotColor.rgb * inner * inner * arm * 0.8;          // magenta light spilling out of the tunnel
                armCol += _MidColor.rgb * smoothstep(0.5, 0.75, cl2) * 0.45;     // lit cloud tops
                Over(o, armCol, armA);

                // ---------- inner tunnel (fake depth) ----------
                float d = R0 / r;                                               // 1 at the rim -> large at the center
                float za = a + d * _Twist * 0.12 - t * 0.05;
                float zz = d * 0.9 - t * _Flow * 1.6;
                float tc = CloudNoise(za, zz, 1.6);
                float bands = 0.5 + 0.5 * sin((za * _Arms + d * 0.8 - t * _Flow) * TAU);
                float deep = saturate((d - 1.0) / 4.0);
                half3 tunCol = lerp(_DeepColor.rgb, _MidColor.rgb, saturate(deep * 2.2));
                tunCol = lerp(tunCol, _HotColor.rgb, saturate(deep * 2.2 - 1.0));
                tunCol *= lerp(0.6, 1.5, smoothstep(0.3, 0.8, tc * 0.8 + bands * 0.35));
                half tunA = 1.0 - smoothstep(R0 - 0.015, R0 + 0.025, r);
                Over(o, tunCol, tunA);

                // rim light around the tunnel mouth
                half rim = RingAA(r, R0, 0.012) * (0.6 + 0.4 * sin(a * TAU * 6.0 + t * 3.0));
                o.rgb += _HotColor.rgb * rim * 1.2;
                o.rgb += _HotColor.rgb * saturate(1.0 - abs(r - R0) / 0.09) * 0.25;

                // bright core
                half core = exp(-r * 14.0) * _CoreGlow + exp(-r * 40.0) * _CoreGlow;
                o.rgb += _CoreColor.rgb * core;
                o.a = saturate(o.a + core * 0.5);

                // ---------- lightning along the arms ----------
                float armId = floor(s);
                float fs = frac(s);
                float jitter = (VNoise(float2(r * 18.0, armId * 3.1 + floor(t * 12.0))) - 0.5) * 0.18;
                float lineD = abs(fs - 0.5 - jitter) * r * 2.0;
                float on = step(1.0 - _BoltAmount * 0.6, Hash11(armId * 7.13 + floor(t * 7.0)));
                half bolt = LineAA(lineD, 0.005) * on * smoothstep(R0, R0 + 0.06, r) * (1.0 - smoothstep(0.55, 0.85, r));
                half boltGlow = saturate(1.0 - lineD / 0.045) * on * smoothstep(R0, R0 + 0.06, r) * (1.0 - smoothstep(0.55, 0.85, r));
                o.rgb += _BoltColor.rgb * (bolt * 2.5 + boltGlow * 0.6);
                o.a = saturate(o.a + bolt * 0.5);

                // ---------- debris spiralling in ----------
                for (int n = 0; n < 18; n++)
                {
                    float h = Hash11(n * 3.71 + 0.5);
                    float ph = frac(t * (0.08 + Hash11(n * 1.3) * 0.07) + h);
                    float rr = lerp(0.92, R0 * 0.55, ph * ph);
                    float aa = h * TAU + ph * 4.0;
                    float2 c = float2(cos(aa), sin(aa)) * rr;
                    float2 q = p - c;
                    float rot = t * (1.5 + h * 2.0) + n;
                    q = float2(cos(rot) * q.x - sin(rot) * q.y, sin(rot) * q.x + cos(rot) * q.y);
                    float size = lerp(0.02, 0.05, Hash11(n + 4.2)) * lerp(0.4, 1.0, rr);
                    half chip = 1.0 - smoothstep(size * 0.8, size, abs(q.x) * 0.8 + abs(q.y) * 1.3);
                    chip *= smoothstep(0.0, 0.1, ph) * (1.0 - smoothstep(0.85, 1.0, ph));
                    half3 cc = _ChipColor.rgb + _HotColor.rgb * 0.25 * inner;     // lit by the vortex light
                    Over(o, cc, chip);
                }

                o.rgb *= _Intensity;
                o.a = saturate(o.a) * step(r, 1.0);
                return o * _Alpha;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

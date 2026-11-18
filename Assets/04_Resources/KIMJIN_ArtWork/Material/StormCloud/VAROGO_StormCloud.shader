// VAROGO storm cloud (URP, opaque, soft toon lighting + electric glow).
// For the VARCO StormCloud model. Lumps slowly boil (vertex wobble), the underside glows violet and
// thin electric veins flicker through it. LightningStrike drives it via MaterialPropertyBlock:
//   _WarnProgress : -1 idle, 0 -> 1 during the warning (veins brighten and flicker faster)
//   _StrikeFlash  : 0 -> 1 when the bolt fires (whole cloud lights up)
// Without a script it just idles, so it can also be used as a plain decorative storm cloud.
Shader "VAROGO/StormCloud"
{
    Properties
    {
        _BaseMap      ("Base Color", 2D) = "white" {}
        _Tint         ("Tint", Color) = (1, 1, 1, 1)
        _ShadowColor  ("Shadow Color", Color) = (0.22, 0.2, 0.38, 1)
        _RimColor     ("Rim Color", Color) = (0.7, 0.68, 0.95, 1)
        _RimPower     ("Rim Power", Range(0.5, 8)) = 3
        _GlowColor    ("Underside Glow Color", Color) = (0.55, 0.3, 1.0, 1)
        _VeinColor    ("Electric Vein Color", Color) = (0.65, 0.75, 1.0, 1)
        _VeinScale    ("Vein Scale", Float) = 6
        _VeinWidth    ("Vein Sharpness", Range(2, 40)) = 14
        _IdleGlow     ("Idle Glow", Range(0, 2)) = 0.35
        _WarnGlow     ("Warning Glow (max)", Range(0, 6)) = 2.2
        _FlashGlow    ("Strike Flash", Range(0, 10)) = 3.5
        _Boil         ("Boil Amount (object units)", Range(0, 0.05)) = 0.012
        _BoilSpeed    ("Boil Speed", Float) = 0.35
        _WarnProgress ("Warn Progress (-1 idle)", Float) = -1
        _StrikeFlash  ("Strike Flash (0-1)", Range(0, 1)) = 0
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Tint, _ShadowColor, _RimColor, _GlowColor, _VeinColor;
            half _RimPower, _VeinWidth, _IdleGlow, _WarnGlow, _FlashGlow;
            float _VeinScale, _Boil, _BoilSpeed, _WarnProgress, _StrikeFlash, _TimeOverride;
        CBUFFER_END

        float CTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }
        float H31(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
        float N3(float3 p)
        {
            float3 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(H31(i), H31(i + float3(1,0,0)), f.x), lerp(H31(i + float3(0,1,0)), H31(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(H31(i + float3(0,0,1)), H31(i + float3(1,0,1)), f.x), lerp(H31(i + float3(0,1,1)), H31(i + float3(1,1,1)), f.x), f.y), f.z);
        }
        float3 Boil(float3 pos, float3 nrm)
        {
            float t = CTime() * _BoilSpeed;
            float n = N3(pos * 3.0 + float3(t, t * 0.7, -t * 0.5)) - 0.5;
            return pos + nrm * n * _Boil * 2.0;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = Boil(v.positionOS.xyz, v.normalOS);
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.positionOS = v.positionOS.xyz;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = CTime();
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _Tint.rgb;
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                // soft toon light
                Light ml = GetMainLight();
                half nl = dot(n, ml.direction) * 0.5 + 0.5;
                half ramp = smoothstep(0.35, 0.75, nl);
                half3 lit = lerp(_ShadowColor.rgb, ml.color, ramp);
                half3 col = base * (lit * 0.85 + unity_AmbientSky.rgb * 0.6);
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                col += _RimColor.rgb * rim * 0.35 * ramp;

                // ---- electricity ----
                float k = _WarnProgress;
                float warn = k >= 0.0 ? saturate(k) : 0.0;
                float rate = lerp(6.0, 22.0, warn);
                // patchy strobe: each 1/3-unit cell flickers on its own
                float3 cell = floor(i.positionOS * 3.0);
                float strobe = step(lerp(0.78, 0.35, warn), H31(cell + floor(t * rate)));
                float under = saturate(-n.y * 1.3 + 0.15);
                half purple = saturate((base.b - base.g) * 5.0 + 0.2);

                float3 q = i.positionOS * _VeinScale + float3(0, t * 0.25, t * 0.15);
                float ridge = 1.0 - abs(N3(q) * 2.0 - 1.0);
                ridge *= 1.0 - abs(N3(q * 2.1 + 5.3) * 2.0 - 1.0) * 0.5;
                half vein = pow(saturate(ridge), _VeinWidth);

                half level = _IdleGlow + warn * _WarnGlow;
                half3 glow = _GlowColor.rgb * under * purple * (0.35 + 0.65 * strobe) * level * 0.6;
                glow += _VeinColor.rgb * vein * under * strobe * level * 1.6;
                // strike: the whole cloud lights up from inside, strongest underneath
                glow += (_GlowColor.rgb * 0.6 + _VeinColor.rgb * vein * 2.0) * _StrikeFlash * _FlashGlow * (0.35 + under);

                col += glow;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertD
            #pragma fragment fragD
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vertD(A v) : SV_POSITION { return TransformObjectToHClip(Boil(v.positionOS.xyz, v.normalOS)); }
            half fragD() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

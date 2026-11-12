// VAROGO magic crystal platform (URP, transparent, simple lit + glow).
// The inner light pattern is projected in WORLD space (triplanar), so a platform
// can be scaled to any size in the level without the pattern stretching.
// VARCO base color is used only as a grey detail (rune band, facets); VARCO normal adds shape.
Shader "VAROGO/MagicCrystal"
{
    Properties
    {
        [Header(Color)]
        _Tint          ("Crystal Color", Color) = (0.55, 0.92, 0.90, 1)
        _GlowColor     ("Glow Color", Color) = (0.55, 1.0, 0.95, 1)
        _Alpha         ("Opacity", Range(0,1)) = 0.8
        _Brightness    ("Brightness", Range(0,2)) = 0.9

        [Header(VARCO Detail)]
        _BaseMap       ("Detail (VARCO Base Color)", 2D) = "white" {}
        _DetailStrength("Detail Strength (runes, facets)", Range(0,1)) = 0.6
        [Normal] _NormalMap ("Normal Map (VARCO)", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0,2)) = 0.8

        [Header(Inner Light (world space))]
        _CausticScale  ("Pattern Size (m)", Float) = 1.6
        _CausticStrength("Pattern Strength", Range(0,2)) = 0.7
        _CausticSpeed  ("Pattern Speed", Float) = 0.25

        [Header(Glow)]
        _RimStrength   ("Edge Glow", Range(0,3)) = 1.0
        _RimPower      ("Edge Glow Width (higher = thinner)", Range(0.5,8)) = 2.5
        _PulseSpeed    ("Pulse Speed", Float) = 1.2
        _PulseAmount   ("Pulse Amount", Range(0,1)) = 0.25
        _Emission      ("Base Glow", Range(0,2)) = 0.35

        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

        // depth prepass so the slab never shows its own back faces through itself
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 v(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 f() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _NormalMap_ST;
                half4 _Tint, _GlowColor;
                half _Alpha, _Brightness, _DetailStrength, _NormalStrength;
                float _CausticScale; half _CausticStrength; float _CausticSpeed;
                half _RimStrength, _RimPower; float _PulseSpeed; half _PulseAmount, _Emission;
                float _TimeOverride;
            CBUFFER_END

            float WTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), u.x), lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
            }
            // soft caustic-like bright veins
            float caustic(float2 p, float t)
            {
                float a = vnoise(p + float2(t, t * 0.7));
                float b = vnoise(p * 1.7 - float2(t * 0.6, -t * 0.4) + 5.2);
                float v = 1.0 - abs(a - b) * 2.2;
                return saturate(v * v * v);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = n.normalWS; o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WTime();
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                half3 bitan = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w;
                half3 N = normalize(nTS.x * i.tangentWS.xyz + nTS.y * bitan + nTS.z * i.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                // triplanar world-space inner light (does not stretch with object scale)
                float3 w = abs(normalize(i.normalWS)); w = pow(w, 4.0); w /= (w.x + w.y + w.z);
                float3 P = i.positionWS / _CausticScale; float ct = t * _CausticSpeed;
                float c = caustic(P.zy, ct) * w.x + caustic(P.xz, ct) * w.y + caustic(P.xy, ct) * w.z;

                // VARCO detail as grey (runes / facets)
                half detail = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb, half3(0.299, 0.587, 0.114));
                half detailMul = lerp(1.0, saturate(detail * 1.6), _DetailStrength);

                Light L = GetMainLight();
                half ndl = saturate(dot(N, L.direction) * 0.5 + 0.5);
                half3 ambient = SampleSH(N);
                half3 col = _Tint.rgb * detailMul * (ambient + L.color * ndl * 0.6) * _Brightness;

                float pulse = 1.0 + sin(t * _PulseSpeed * 6.2832 * 0.25) * _PulseAmount;
                half fres = pow(1.0 - saturate(dot(N, V)), _RimPower);
                half3 glow = _GlowColor.rgb * (_Emission + c * _CausticStrength + fres * _RimStrength) * pulse;
                col += glow * detailMul;

                half3 H = normalize(L.direction + V);
                col += L.color * pow(saturate(dot(N, H)), 96.0) * 0.5;

                half a = saturate(_Alpha + fres * 0.25 + c * 0.1);
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

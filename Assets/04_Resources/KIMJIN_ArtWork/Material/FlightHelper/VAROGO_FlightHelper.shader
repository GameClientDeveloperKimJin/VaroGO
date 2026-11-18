// VAROGO flight helper (URP, opaque, soft toon light). For the VARCO FlightHelper wizard model.
// Mask texture (T_FlightHelper_Emission): R = eyes, G = wand star, B = gold trim.
//   eyes glow cyan and blink every few seconds, the wand star pulses, gold trim gets a slow shimmer wave,
//   the cloak hem sways (vertex wobble, stronger toward the bottom), violet rim light.
// The VARCO FBX root is rotated X 270, so object-space Z is "up" for the sway.
Shader "VAROGO/FlightHelper"
{
    Properties
    {
        _BaseMap      ("Base Color", 2D) = "white" {}
        _MaskMap      ("Emission Mask (R eyes, G star, B trim)", 2D) = "black" {}
        _ShadowColor  ("Shadow Color", Color) = (0.32, 0.26, 0.5, 1)
        _RimColor     ("Rim Color", Color) = (0.75, 0.55, 1.0, 1)
        _EyeColor     ("Eye Glow Color", Color) = (0.3, 0.95, 1.0, 1)
        _StarColor    ("Star Glow Color", Color) = (1.0, 0.85, 0.35, 1)
        _EyeGlow      ("Eye Glow", Range(0, 8)) = 3
        _StarGlow     ("Star Glow", Range(0, 8)) = 3
        _TrimShimmer  ("Trim Shimmer", Range(0, 2)) = 0.35
        _BlinkEvery   ("Blink Every (s)", Float) = 3.7
        _Sway         ("Cloak Sway (object units)", Range(0, 0.08)) = 0.025
        _SwaySpeed    ("Sway Speed", Float) = 2.2
        _HeightMin    ("Hem Height (object Z)", Float) = -0.5
        _HeightMax    ("Top Height (object Z)", Float) = 0.5
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _ShadowColor, _RimColor, _EyeColor, _StarColor;
            half _EyeGlow, _StarGlow, _TrimShimmer;
            float _BlinkEvery, _Sway, _SwaySpeed, _HeightMin, _HeightMax, _TimeOverride;
        CBUFFER_END
        float HTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }
        float3 SwayPos(float3 p)
        {
            float t = HTime() * _SwaySpeed;
            float low = saturate(1.0 - (p.z - _HeightMin) / max(0.01, (_HeightMax - _HeightMin) * 0.55)); // 1 at hem, 0 above mid
            low *= low;
            p.x += sin(t + p.y * 6.0) * _Sway * low;
            p.y += cos(t * 0.8 + p.x * 5.0) * _Sway * 0.7 * low;
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; float fog : TEXCOORD3; float3 positionOS : TEXCOORD4; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = SwayPos(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float t = HTime();
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv).rgb;
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light ml = GetMainLight();
                half nl = dot(n, ml.direction) * 0.5 + 0.5;
                half ramp = smoothstep(0.3, 0.7, nl);
                half3 col = base * (lerp(_ShadowColor.rgb, ml.color, ramp) * 0.85 + unity_AmbientSky.rgb * 0.5);
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0);
                col += _RimColor.rgb * rim * 0.45;
                if (!front) col *= 0.35;                       // inside of the cloak stays dark

                // eyes: glow + blink (quick dip every _BlinkEvery seconds)
                float bt = frac(t / _BlinkEvery);
                half closed = 1.0 - smoothstep(0.03, 0.045, bt);           // ~0.15 s dark at the start of each cycle
                half blink = lerp(1.0, 0.05, closed);
                col += _EyeColor.rgb * mask.r * _EyeGlow * blink;
                // wand star pulse
                col += _StarColor.rgb * mask.g * _StarGlow * (0.75 + 0.25 * sin(t * 4.0));
                // gold trim shimmer wave sweeping upward
                half wave = pow(saturate(sin(i.positionOS.z * 9.0 - t * 2.5) * 0.5 + 0.5), 8.0);
                col += _StarColor.rgb * mask.b * wave * _TrimShimmer;

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
            Cull Off
            HLSLPROGRAM
            #pragma vertex vertD
            #pragma fragment fragD
            float4 vertD(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(SwayPos(positionOS.xyz)); }
            half fragD() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

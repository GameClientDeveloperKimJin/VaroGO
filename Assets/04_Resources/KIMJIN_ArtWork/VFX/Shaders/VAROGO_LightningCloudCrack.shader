// VAROGO lightning warning: storm cloud crack above the strike point
// (URP, unlit, premultiplied alpha, camera facing). Put on a Quad high above; size = transform scale.
// A dark boiling cloud gathers, lit from inside by flickering violet/blue crack lightning that gets stronger
// until the strike.
Shader "VAROGO/LightningCloudCrack"
{
    Properties
    {
        _CrackTex    ("Crack (white, alpha)", 2D) = "black" {}
        _CloudColor  ("Cloud Color", Color) = (0.11, 0.11, 0.19, 1)
        _LitColor    ("Cloud Lit Color", Color) = (0.45, 0.30, 0.75, 1)
        _CrackColor  ("Crack Glow Color", Color) = (0.55, 0.55, 1.0, 1)
        _CloudAlpha  ("Cloud Alpha", Range(0,1)) = 0.9
        _Glow        ("Glow", Range(0,6)) = 2.2
        _Intensity   ("Intensity", Range(0,5)) = 1
        _WarnProgress ("Warn Progress (-1 = preview loop)", Float) = -1
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" "IgnoreProjector"="True" }
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

            TEXTURE2D(_CrackTex); SAMPLER(sampler_CrackTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _CrackTex_ST;
                half4 _CloudColor, _LitColor, _CrackColor;
                half _CloudAlpha, _Glow, _Intensity;
                float _WarnProgress, _TimeOverride;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = BillboardCS(v.positionOS.xyz); o.uv = v.uv; return o; }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WarnTime(_TimeOverride);
                float k = WarnK(_WarnProgress, t);
                float gather = smoothstep(0.0, 0.25, k);
                if (gather <= 0.001) return 0;
                float power = saturate((k - 0.10) / 0.90);             // crack strength builds to the strike

                float2 p = i.uv * 2.0 - 1.0;
                p.y *= 1.6;                                             // wide, flat cloud
                float r = length(p);

                // boiling cloud
                float n = Fbm(p * 1.8 + float2(t * 0.15, -t * 0.08));
                n += Fbm(p * 3.7 - float2(t * 0.25, 0.0)) * 0.4;
                float shape = smoothstep(0.35, 0.85, n * 1.05 - r * 0.75 + 0.45 * gather);
                half cloudA = shape * _CloudAlpha * gather;

                // crack lightning: texture, slowly rotating, strobing
                float flickSeed = floor(t * 16.0);
                half strobe = step(0.45 - power * 0.35, Hash11(flickSeed)) * (0.6 + 0.4 * Hash11(flickSeed + 3.1));
                half pulse = 0.35 + 0.65 * strobe;
                float a1 = t * 0.05;
                float2 p0 = i.uv * 2.0 - 1.0;
                float2 cu = float2(cos(a1) * p0.x - sin(a1) * p0.y, sin(a1) * p0.x + cos(a1) * p0.y);
                half crack = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, cu * 0.5 + 0.5).a;
                crack *= step(r, 1.0) * (0.25 + power);

                // inner light of the cloud around the crack
                half inner = (1.0 - smoothstep(0.0, 0.75, r)) * shape;
                half3 litCloud = lerp(_CloudColor.rgb, _LitColor.rgb, saturate(inner * (0.3 + power * pulse)));
                half3 col = litCloud * cloudA;
                col += (_CrackColor.rgb * crack + half3(1, 1, 1) * crack * crack) * pulse * _Glow * gather;
                return half4(col * _Intensity, saturate(cloudA));
            }
            ENDHLSL
        }
    }
    FallBack Off
}

// VAROGO sticky sap puddle (URP, transparent). Procedural, no textures.
// Put on a flat box/quad: only upward faces draw. The puddle edge is an irregular
// blob shape cut from the object's XZ footprint, so stretching the object keeps a soft edge.
Shader "VAROGO/SapPuddle"
{
    Properties
    {
        [Header(Color)]
        _EdgeColor    ("Edge Color (thin)", Color)   = (0.93, 0.62, 0.16, 1)
        _CoreColor    ("Core Color (thick)", Color)  = (0.55, 0.27, 0.05, 1)
        _GlowColor    ("Inner Glow Color", Color)    = (1.00, 0.80, 0.35, 1)
        _Opacity      ("Opacity", Range(0,1)) = 0.92

        [Header(Shape)]
        _EdgeSoft     ("Edge Softness (m)", Range(0.01,0.5)) = 0.08
        _EdgeWobble   ("Edge Wobble (m)", Range(0,0.3)) = 0.05
        _CornerRadius ("Corner Radius (m)", Range(0,1)) = 0.2
        _WobbleScale  ("Wobble Size (m)", Float) = 1.2

        [Header(Surface)]
        _BlobScale    ("Blob Size (m)", Float) = 0.9
        _FlowSpeed    ("Ooze Speed", Float) = 0.05
        _SpecStrength ("Highlight Strength", Range(0,3)) = 1.4
        _SpecSharpness("Highlight Sharpness", Range(8,512)) = 90
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EdgeColor, _CoreColor, _GlowColor;
                half  _Opacity, _EdgeSoft, _EdgeWobble, _CornerRadius;
                float _WobbleScale, _BlobScale, _FlowSpeed;
                half  _SpecStrength, _SpecSharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half   fog        : TEXCOORD3;
            };

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), u.x), lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
            }
            float fbm(float2 p) { return vnoise(p) * 0.6 + vnoise(p * 2.1 + 7.3) * 0.28 + vnoise(p * 4.3 + 1.7) * 0.12; }

            float height(float2 xz)
            {
                float t = _Time.y * _FlowSpeed;
                return fbm(xz / _BlobScale + float2(t, -t * 0.6)) * 0.65 + fbm(xz / (_BlobScale * 0.45) - float2(t * 0.8, t)) * 0.35;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // top faces only
                clip(i.normalWS.y - 0.5);

                float2 xz = i.positionWS.xz;

                // Puddle edge measured in METERS from the object's footprint (unit cube/quad: -0.5..0.5),
                // so the sap fills the whole trigger area no matter how the object is stretched.
                float2 objScale = float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m02_m12_m22));
                float2 halfSize = objScale * 0.5;
                float r = min(_CornerRadius, min(halfSize.x, halfSize.y));
                float2 q = abs(i.positionOS.xz) * objScale - (halfSize - r);
                float inside = -(length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r);   // m from the border, >0 inside
                float wob = fbm(xz / _WobbleScale + 13.7) * _EdgeWobble;               // only eats inward a little
                float edge = smoothstep(0.0, _EdgeSoft, inside - wob);
                clip(edge - 0.01);

                // thick/thin look
                float h = height(xz);
                float thick = saturate(edge * 1.4) * (0.55 + 0.45 * h);
                half3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, smoothstep(0.25, 0.9, thick));
                col += _GlowColor.rgb * smoothstep(0.62, 0.9, h) * 0.18 * edge;

                // glossy highlight from a fake bumpy normal
                float e = 0.05;
                float hx = height(xz + float2(e, 0)) - h;
                float hz = height(xz + float2(0, e)) - h;
                half3 n = normalize(half3(-hx * 6.0, 1.0, -hz * 6.0));
                Light mainLight = GetMainLight();
                half3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndl = saturate(dot(n, mainLight.direction)) * 0.5 + 0.5;
                half3 hv = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(n, hv)), _SpecSharpness) * _SpecStrength;
                half fres = pow(1.0 - saturate(dot(n, viewDir)), 3.0);

                col = col * ndl * (mainLight.color * 0.6 + 0.5) + spec * mainLight.color + fres * _GlowColor.rgb * 0.25;

                half alpha = saturate(edge * 1.6) * _Opacity;
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

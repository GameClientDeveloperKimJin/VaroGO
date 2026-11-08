// VAROGO ravine floor water (URP, transparent). Procedural ripples, no textures.
// Uses the camera depth texture for shallow/deep color and shoreline foam
// (PC_RPAsset has Depth Texture on). Turn "Use Depth" off if a platform has no depth texture.
Shader "VAROGO/RiverWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor  ("Shallow Color", Color) = (0.22, 0.52, 0.56, 1)
        _DeepColor     ("Deep Color", Color)    = (0.06, 0.24, 0.31, 1)
        _DepthRange    ("Depth Range (m)", Float) = 2.5
        _Opacity       ("Opacity (deep)", Range(0,1)) = 0.92
        _SkyColor      ("Reflection Color", Color) = (0.62, 0.80, 0.88, 1)
        _Reflection    ("Reflection Strength", Range(0,1)) = 0.35

        [Header(Ripples)]
        _RippleScale   ("Ripple Size (m)", Float) = 3.0
        _RippleStrength("Ripple Strength", Range(0,1)) = 0.35
        _FlowDir       ("Flow Direction (xz)", Vector) = (0, -1, 0, 0)
        _FlowSpeed     ("Flow Speed (m/s)", Float) = 0.6
        _SpecStrength  ("Sun Highlight Strength", Range(0,3)) = 0.9
        _SpecSharpness ("Sun Highlight Sharpness", Range(16,512)) = 160

        [Header(Shore Foam)]
        [Toggle(_USE_DEPTH)] _UseDepth ("Use Depth (shore foam, depth color)", Float) = 1
        _FoamColor     ("Foam Color", Color) = (0.88, 0.94, 0.95, 1)
        _FoamWidth     ("Foam Width (m)", Float) = 0.6
        _FoamAmount    ("Foam Amount", Range(0,1)) = 0.7

        [HideInInspector] _TimeOverride ("Time Override (preview capture, <0 = off)", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" "IgnoreProjector"="True" }

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
            #pragma shader_feature_local _USE_DEPTH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float _DepthRange;
                half  _Opacity, _Reflection;
                float _RippleScale; half _RippleStrength;
                float4 _FlowDir; float _FlowSpeed;
                half  _SpecStrength, _SpecSharpness;
                float _FoamWidth; half _FoamAmount;
                float _TimeOverride;
            CBUFFER_END

            float WTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
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
            float fbm(float2 p) { return vnoise(p) * 0.55 + vnoise(p * 2.1 + 7.3) * 0.3 + vnoise(p * 4.3 + 1.7) * 0.15; }

            // height field -> normal by finite differences
            float ripple(float2 xz, float t)
            {
                float2 dir = normalize(_FlowDir.xy + 1e-5);
                float2 p1 = xz / _RippleScale + dir * t * _FlowSpeed / _RippleScale;
                float2 p2 = xz / (_RippleScale * 0.55) * float2(1.0, 1.3) - dir.yx * t * _FlowSpeed * 0.7 / _RippleScale + 3.1;
                return fbm(p1) * 0.6 + fbm(p2) * 0.4;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WTime();
                float2 xz = i.positionWS.xz;

                // --- ripple normal ---
                float e = 0.15;
                float h  = ripple(xz, t);
                float hx = ripple(xz + float2(e, 0), t);
                float hz = ripple(xz + float2(0, e), t);
                float s = _RippleStrength * 2.5;
                half3 N = normalize(half3(-(hx - h) / e * s, 1.0, -(hz - h) / e * s));
                // only ripple the top face; sides keep their own normal
                N = lerp(normalize(i.normalWS), N, saturate(i.normalWS.y));

                // --- water depth below the surface ---
                half depth01 = 1.0;   // 1 = deep
                half shore = 0.0;
                #if defined(_USE_DEPTH)
                    float2 suv = i.screenPos.xy / i.screenPos.w;
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float surfEye = i.screenPos.w;
                    float dRaw = sceneEye - surfEye;
                    // Scene depth in front of the water surface is impossible (we are a transparent
                    // surface drawn after opaques). It means this camera has no valid depth texture
                    // (e.g. editor screenshot / preview camera) -> fall back to "deep, no foam".
                    if (dRaw > -0.05)
                    {
                        float d = max(0.0, dRaw);
                        depth01 = saturate(d / _DepthRange);
                        float foamN = vnoise(xz * 1.6 + t * 0.4) * 0.6 + vnoise(xz * 4.0 - t * 0.6) * 0.4;
                        shore = saturate(1.0 - d / max(_FoamWidth, 1e-3));
                        shore = step(1.0 - _FoamAmount, shore * (0.55 + foamN * 0.9)) * shore;
                    }
                #endif

                half3 baseCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                // --- lighting ---
                Light L = GetMainLight();
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 ambient = SampleSH(half3(0,1,0));
                half ndl = saturate(dot(N, L.direction) * 0.5 + 0.5);
                half3 col = baseCol * (ambient * 0.8 + L.color * ndl * 0.55);

                half fres = pow(1.0 - saturate(dot(N, V)), 4.0);
                col = lerp(col, _SkyColor.rgb * (ambient + L.color * 0.3), fres * _Reflection + _Reflection * 0.15);

                half3 Hh = normalize(L.direction + V);
                col += L.color * pow(saturate(dot(N, Hh)), _SpecSharpness) * _SpecStrength;

                col = lerp(col, _FoamColor.rgb * (ambient + L.color * 0.6), shore);

                half alpha = lerp(0.55, _Opacity, depth01);
                alpha = max(alpha, shore);
                alpha = saturate(alpha + fres * 0.2);

                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

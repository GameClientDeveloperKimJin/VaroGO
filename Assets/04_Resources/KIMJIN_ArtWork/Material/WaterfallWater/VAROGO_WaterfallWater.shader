// VAROGO waterfall water for the VARCO waterfall model (URP, transparent, simple lit).
// Shape/normal detail comes from the VARCO mesh + normal map (mesh UVs).
// Color comes from a flow texture projected in world space (across = object X, down = world Y)
// and scrolled downward, so the water keeps falling regardless of the VARCO UV layout.
Shader "VAROGO/WaterfallWater"
{
    Properties
    {
        [Header(Flow Texture)]
        _FlowTex       ("Flow Texture", 2D) = "white" {}
        _TileWidth     ("Tile Width (m)", Float) = 4
        _TileHeight    ("Tile Height (m)", Float) = 8
        _FlowSpeed     ("Flow Speed (m/s)", Float) = 3.5
        _SecondLayer   ("Second Layer Mix", Range(0,1)) = 0.45
        _Tint          ("Tint", Color) = (1,1,1,1)

        [Header(Shape Detail (VARCO))]
        [Normal] _NormalMap ("Normal Map (VARCO)", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 0.8

        [Header(Foam)]
        _FoamColor     ("Foam Color", Color) = (0.90, 0.95, 0.96, 1)
        _ObjHeight     ("Model Height (object units)", Float) = 0.993
        _BottomFoam    ("Bottom Foam Height (0-1)", Range(0,0.5)) = 0.10
        _TopFoam       ("Top Lip Foam (0-0.3)", Range(0,0.3)) = 0.06

        [Header(Look)]
        _Alpha         ("Opacity", Range(0,1)) = 0.85
        _Smoothness    ("Highlight Sharpness", Range(8,256)) = 64
        _SpecStrength  ("Highlight Strength", Range(0,2)) = 0.5
        _FresnelColor  ("Edge Color", Color) = (0.62, 0.82, 0.85, 1)
        _FresnelPower  ("Edge Width (higher = thinner)", Range(0.5,8)) = 3
        _Brightness    ("Brightness", Range(0,2)) = 1

        [HideInInspector] _TimeOverride ("Time Override (preview capture, <0 = off)", Float) = -1
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

            TEXTURE2D(_FlowTex);   SAMPLER(sampler_FlowTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _FlowTex_ST, _NormalMap_ST;
                float  _TileWidth, _TileHeight, _FlowSpeed, _SecondLayer;
                half4  _Tint, _FoamColor, _FresnelColor;
                half   _NormalStrength;
                float  _ObjHeight;
                half   _BottomFoam, _TopFoam;
                half   _Alpha, _Smoothness, _SpecStrength, _FresnelPower, _Brightness;
                float  _TimeOverride;
            CBUFFER_END

            float WTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  tangentWS  : TEXCOORD3;
                float  height01   : TEXCOORD4;
                float  across     : TEXCOORD5;
                half   fog        : TEXCOORD6;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv;
                // VARCO model: object Z is up (pivot at bottom)
                o.height01 = saturate(v.positionOS.z / _ObjHeight);
                // across = distance along the object's X axis in world meters
                float3 objRight = normalize(UNITY_MATRIX_M._m00_m10_m20);
                float3 objPos = UNITY_MATRIX_M._m03_m13_m23;
                o.across = dot(p.positionWS - objPos, objRight);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WTime();

                // --- flowing color (world projected, scrolls down) ---
                float2 fuv = float2(i.across / _TileWidth, i.positionWS.y / _TileHeight);
                float2 uvA = fuv + float2(0.0, t * _FlowSpeed / _TileHeight);
                float2 uvB = fuv * float2(1.37, 0.81) + float2(0.31, t * _FlowSpeed * 1.45 / _TileHeight);
                half4 a = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, uvA);
                half4 b = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, uvB);
                half3 albedo = lerp(a.rgb, max(a.rgb, b.rgb), _SecondLayer) * _Tint.rgb;
                half foamTex = saturate(max(a.a, b.a * _SecondLayer) - 0.7) / 0.3;

                // --- foam at the bottom (impact) and top lip ---
                float h = i.height01;
                half bottom = 1.0 - smoothstep(0.0, _BottomFoam, h - foamTex * 0.04);
                half top = smoothstep(1.0 - _TopFoam, 1.0, h);
                half foam = saturate(bottom + top * 0.7);
                albedo = lerp(albedo, _FoamColor.rgb, foam);

                // --- normal (VARCO detail) ---
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                half3 bitan = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w;
                half3 N = normalize(nTS.x * i.tangentWS.xyz + nTS.y * bitan + nTS.z * i.normalWS);

                // --- lighting (soft, matches map lighting) ---
                Light L = GetMainLight();
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndl = saturate(dot(N, L.direction) * 0.5 + 0.5);      // wrapped diffuse
                half3 ambient = SampleSH(N);
                half3 col = albedo * (ambient + L.color * ndl * 0.75);
                half3 H = normalize(L.direction + V);
                col += L.color * pow(saturate(dot(N, H)), _Smoothness) * _SpecStrength * (1.0 - foam);
                half fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                col = lerp(col, _FresnelColor.rgb * (ambient + L.color * 0.5), fres * 0.5);
                col *= _Brightness;

                half alpha = saturate(lerp(_Alpha, 1.0, max(foam, foamTex * 0.6)) + fres * 0.15);
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

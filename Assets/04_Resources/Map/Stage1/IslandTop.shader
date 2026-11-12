// 섬 윗면: 잔디 2종 + 이끼 + 숲 바닥을 월드 위치 노이즈로 섞는다 (URP Lit 조명)
Shader "VAROGO/IslandTop"
{
    Properties
    {
        _Grass1 ("잔디 1", 2D) = "white" {}
        _Grass2 ("잔디 2", 2D) = "white" {}
        _GrassN ("잔디 노멀", 2D) = "bump" {}
        _Moss ("이끼", 2D) = "white" {}
        _MossN ("이끼 노멀", 2D) = "bump" {}
        _Ground ("숲 바닥", 2D) = "white" {}
        _GroundN ("숲 바닥 노멀", 2D) = "bump" {}
        _Tint ("색", Color) = (1, 1, 1, 1)
        _Tile ("텍스처 한 장 크기 (m)", Float) = 4
        _GroundTile ("숲 바닥 한 장 크기 (m)", Float) = 2
        _PatchSize ("무늬 크기 (m)", Float) = 12
        _Grass2Amount ("잔디 2 비율", Range(0, 1)) = 0.5
        _MossAmount ("이끼 비율", Range(0, 1)) = 0.3
        _GroundAmount ("숲 바닥 비율", Range(0, 1)) = 0.2
        _Smoothness ("매끈함", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Grass1_ST, _Grass2_ST, _GrassN_ST, _Moss_ST, _MossN_ST, _Ground_ST, _GroundN_ST;
            half4 _Tint;
            float _Tile, _GroundTile, _PatchSize;
            half _Grass2Amount, _MossAmount, _GroundAmount, _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Grass1); SAMPLER(sampler_Grass1);
            TEXTURE2D(_Grass2);
            TEXTURE2D(_GrassN); SAMPLER(sampler_GrassN);
            TEXTURE2D(_Moss);
            TEXTURE2D(_MossN);
            TEXTURE2D(_Ground);
            TEXTURE2D(_GroundN);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p) { return Noise(p) * 0.65 + Noise(p * 2.13 + 17.3) * 0.35; }
            // 비율 a만큼 덮이도록 노이즈를 부드럽게 자른다
            float Mask(float n, float a) { return smoothstep(1 - a - 0.08, 1 - a + 0.08, n); }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 uv = i.positionWS.xz / _Tile;
                float2 guv = i.positionWS.xz / _GroundTile;
                float2 p = i.positionWS.xz / _PatchSize;

                half3 g1 = SAMPLE_TEXTURE2D(_Grass1, sampler_Grass1, uv).rgb;
                half3 g2 = SAMPLE_TEXTURE2D(_Grass2, sampler_Grass1, uv * 0.93 + 0.37).rgb;
                half3 ms = SAMPLE_TEXTURE2D(_Moss, sampler_Grass1, uv).rgb;
                half3 gr = SAMPLE_TEXTURE2D(_Ground, sampler_Grass1, guv).rgb;
                half3 nG = UnpackNormal(SAMPLE_TEXTURE2D(_GrassN, sampler_GrassN, uv));
                half3 nM = UnpackNormal(SAMPLE_TEXTURE2D(_MossN, sampler_GrassN, uv));
                half3 nF = UnpackNormal(SAMPLE_TEXTURE2D(_GroundN, sampler_GrassN, guv));

                float w2 = Mask(Fbm(p + 3.1), _Grass2Amount);
                float wm = Mask(Fbm(p * 1.7 + 41.7), _MossAmount);
                float wf = Mask(Fbm(p * 2.6 + 87.3), _GroundAmount);

                half3 albedo = lerp(g1, g2, w2);
                half3 nT = nG;
                albedo = lerp(albedo, ms, wm); nT = lerp(nT, nM, wm);
                albedo = lerp(albedo, gr, wf); nT = lerp(nT, nF, wf);
                albedo *= _Tint.rgb;

                // 윗면 기준 접선 공간 (x → 접선, z → 종접선)
                float3 N = normalize(i.normalWS);
                float3 T = normalize(cross(N, float3(0, 0, 1)));
                float3 B = cross(T, N);
                float3 normalWS = normalize(T * nT.x + B * nT.y + N * nT.z);

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.alpha = 1;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.normalTS = half3(0, 0, 1);

                half4 c = UniversalFragmentPBR(input, s);
                c.rgb = MixFog(c.rgb, input.fogCoord);
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, _LightDirection));
                #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(Attributes v) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(v); return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}

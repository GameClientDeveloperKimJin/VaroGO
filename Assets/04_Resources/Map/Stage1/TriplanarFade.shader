// TriplanarLit의 반투명 버전. 스크립트가 _BaseColor(알파 포함)·_EmissionColor를 MaterialPropertyBlock으로 바꿔도 동작한다
// (RevealablePlatform 페이드, Z4ExpiryWarning 깜빡임, Checkpoint 색 표시)
Shader "VAROGO/TriplanarFade"
{
    Properties
    {
        _BaseMap ("색 텍스처", 2D) = "white" {}
        _BumpMap ("노멀", 2D) = "bump" {}
        [MainColor] _BaseColor ("색 (알파 = 투명도)", Color) = (1, 1, 1, 1)
        _EmissionColor ("발광색", Color) = (0, 0, 0, 1)
        _Tile ("텍스처 한 장 크기 (m)", Float) = 3
        _Smoothness ("매끈함", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BumpMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            float _Tile;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);

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

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 N = normalize(i.normalWS);
                float3 w = pow(abs(N), 4);
                w /= (w.x + w.y + w.z + 1e-5);
                float3 p = i.positionWS / _Tile;

                half3 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy).rgb;
                half3 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz).rgb;
                half3 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy).rgb;
                half3 albedo = (cx * w.x + cy * w.y + cz * w.z) * _BaseColor.rgb;

                half3 tx = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, p.zy));
                half3 ty = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, p.xz));
                half3 tz = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, p.xy));
                float3 nx = float3(0, tx.y, tx.x) * sign(N.x);
                float3 ny = float3(ty.x, 0, ty.y) * sign(N.y);
                float3 nz = float3(tz.x, tz.y, 0) * sign(N.z);
                float3 normalWS = normalize(N + nx * w.x + ny * w.y + nz * w.z);

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
                s.alpha = _BaseColor.a;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.emission = _EmissionColor.rgb;
                s.normalTS = half3(0, 0, 1);

                half4 c = UniversalFragmentPBR(input, s);
                c.rgb = MixFog(c.rgb, input.fogCoord);
                return c;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}

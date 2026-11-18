// VAROGO lightning warning beam (URP, unlit, additive). Put on vertical Quads (cross two of them).
// Soft yellow column, bright at the bottom and fading upward, with streaks sliding down
// toward the strike point so the danger spot reads from the chase camera.
Shader "VAROGO/LightningWarnBeam"
{
    Properties
    {
        _Color       ("Color", Color) = (1.0, 0.82, 0.25, 1)
        _CoreColor   ("Core Color", Color) = (1.0, 0.97, 0.8, 1)
        _Alpha       ("Alpha", Range(0,1)) = 0.45
        _FadeTop     ("Fade Power (top)", Range(0.2,6)) = 1.6
        _StreakSpeed ("Streak Speed", Float) = 2.2
        _Intensity   ("Intensity", Range(0,5)) = 1
        [HideInInspector] _TimeOverride ("Time Override", Float) = -1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _CoreColor;
                half _Alpha, _FadeTop, _Intensity;
                float _StreakSpeed, _TimeOverride;
            CBUFFER_END

            float WTime() { return _TimeOverride >= 0.0 ? _TimeOverride : _Time.y; }

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            half4 frag(Varyings i) : SV_Target
            {
                float t = WTime();
                float u = i.uv.x, v = i.uv.y;
                float across = sin(u * 3.14159);                 // 0 at edges, 1 in middle
                half edge = pow(saturate(across), 1.5);
                half core = pow(saturate(across), 10.0);
                half vert = pow(saturate(1.0 - v), _FadeTop) * smoothstep(0.0, 0.02, v);

                // streaks falling toward the ground point
                float st = frac(v * 6.0 + t * _StreakSpeed + sin(u * 9.0) * 0.15);
                half streak = smoothstep(0.0, 0.08, st) * (1.0 - smoothstep(0.10, 0.45, st));
                half beat = 0.75 + 0.25 * sin(t * 9.0);

                half3 col = lerp(_Color.rgb, _CoreColor.rgb, core) * (1.0 + streak * 0.8) * beat;
                half a = (edge * 0.6 + core * 0.6 + streak * edge * 0.5) * vert * _Alpha;
                return half4(col * _Intensity, saturate(a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}

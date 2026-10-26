// A waterfall pouring down a mountainside: a ribbon lying on the rock, streaked with white water
// running down it. Its band of the music sets how fast it runs and how white it is, and swells it
// wider; it spreads and foams where it lands. Lit by the world's sun like the land, and hazed with it.
Shader "VRMusic/SimFall"
{
    Properties
    {
        _Color ("Water", Color) = (0.92, 0.96, 1, 1)
        _Speed ("Speed (m/s)", Float) = 14
        _SpeedByBand ("Faster With The Band", Float) = 18
        _StreakLength ("Streak Length (m)", Float) = 22
        _Band ("Band (0 lowest, 1 highest)", Range(0, 1)) = 0.08
        _Swell ("Wider With The Band", Range(0, 1)) = 0.35
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _FogScale ("Fog", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SimFall"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Speed;
                float _SpeedByBand;
                float _StreakLength;
                float _Band;
                float _Swell;
                float _Opacity;
                float _FogScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;       // the direction across the ribbon
                float2 uv : TEXCOORD0;          // across (−1 to 1), metres down
                float2 uv2 : TEXCOORD1;         // half its width, its whole length
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float2 size : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float level = GR_Band(_Band);
                float3 across = TransformObjectToWorldDir(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz) + across * (input.uv.x * input.uv2.x * _Swell * level);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.size = input.uv2;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float level = GR_Band(_Band);
                float down = input.uv.y;
                float x = input.uv.x;

                // Streaks running down, each column its own speed so the water doesn't move as a sheet.
                float column = floor((x * 0.5 + 0.5) * 9.0);
                float jitter = GR_Hash11(column * 3.7);
                float flow = down - _GR_Clock * (_Speed + _SpeedByBand * level) * (0.8 + 0.4 * jitter);
                float streak = GR_Noise(float2(x * 4.5, flow / max(_StreakLength, 0.1)));
                streak = 0.45 + 0.75 * smoothstep(0.35, 0.8, streak);

                float edge = 1.0 - smoothstep(0.55, 1.0, abs(x) + (GR_Noise(float2(x * 3.0, flow * 0.05)) - 0.5) * 0.3);
                float foot = smoothstep(0.85, 1.0, down / max(input.size.y, 1.0));
                float top = smoothstep(0.0, 12.0, down);
                float alpha = edge * top * streak * _Opacity * (0.65 + 0.35 * level) * (1.0 + foot);

                float3 col = _Color.rgb * lerp(_GR_ShadowColor.rgb, _GR_SunColor.rgb, 0.7);
                col *= 1.0 - 0.45 * GR_CloudShadow(input.positionWS);
                col += _Color.rgb * level * level * 0.25 * _GR_Intensity;
                col = GR_ApplyFog(col, input.positionWS, _FogScale);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}

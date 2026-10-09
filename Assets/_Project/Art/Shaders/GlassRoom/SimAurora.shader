// An aurora curtain: a strip hung in an arc across the night sky, its vertical rays the spectrum.
// Each ray along the curtain is one band — the bass in the middle of the curtain, the treble toward
// its ends — and reaches up and brightens with its band's level. The curtain's folds sway on their
// own and ripple harder with the mids, and a beat sends a surge of light out from the middle along
// the lower edge. Green low, violet where it reaches high, the way real aurorae colour.
//
// The same shader draws the curtain's mirror image in the lake below, dimmer and wavering with the
// bass; that copy ignores depth and is clipped to the water by the stencil the water writes.
Shader "VRMusic/SimAurora"
{
    Properties
    {
        [HDR] _ColorLow ("Lower Colour", Color) = (0.15, 1, 0.45, 1)
        [HDR] _ColorHigh ("Upper Colour", Color) = (0.8, 0.25, 0.95, 1)
        _Brightness ("Brightness", Range(0, 4)) = 1.2
        _Rest ("Reach At Silence", Range(0, 1)) = 0.3
        _Sway ("Fold Sway (m)", Float) = 70
        _BandRange ("Bands (from, to)", Vector) = (0, 1, 0, 0)
        _Reflect ("Is Reflection", Range(0, 1)) = 0

        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        _StencilRef ("Stencil Reference", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend One One
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
        }

        Pass
        {
            Name "SimAurora"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorLow;
                float4 _ColorHigh;
                float _Brightness;
                float _Rest;
                float _Sway;
                float4 _BandRange;
                float _Reflect;
                float _ZTest;
                float _StencilComp;
                float _StencilRef;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS.xyz;
                float2 radial = normalize(pos.xz + 1e-4);
                float u = input.uv.x;
                float v = input.uv.y;

                // Folds: slow travelling waves along the curtain, livelier with the mids, more at the top.
                float t = _GR_Clock;
                float sway = sin(u * 9.0 + t * 0.35) * 0.6 + sin(u * 23.0 - t * 0.6) * 0.4 * (0.4 + 1.2 * _GR_Music.y);
                sway += (GR_Noise1(u * 14.0 + t * 0.15) - 0.5) * 0.8;
                pos.xz += radial * sway * _Sway * (0.35 + 0.65 * v);

                output.positionWS = TransformObjectToWorld(pos);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;

                if (_Reflect > 0.5)
                    u += (sin(v * 60.0 + _GR_Clock * 2.2) + (GR_Noise1(v * 23.0 - _GR_Clock) - 0.5)) * 0.001 * (1.0 + 2.0 * _GR_Music.x);

                // Bass in the middle of the curtain, treble at its ends.
                float fromMiddle = abs(u - 0.5) * 2.0;
                float level = GR_Band(lerp(_BandRange.x, _BandRange.y, fromMiddle));

                float reach = _Rest + (1.0 - _Rest) * level;
                float rays = GR_Noise1(u * 170.0 + _GR_Clock * 0.25) * 0.6 + GR_Noise1(u * 41.0 - _GR_Clock * 0.12) * 0.4;
                rays = 0.25 + 0.75 * rays * rays;

                float lower = smoothstep(0.0, 0.05, v);
                float upper = 1.0 - smoothstep(reach * 0.45, reach, v);
                float ends = smoothstep(0.0, 0.08, u) * smoothstep(1.0, 0.92, u);
                float shape = lower * upper * ends * rays * (1.0 + 1.6 * exp(-v * 22.0));

                // A beat surges out from the middle along the lower edge.
                float since = _GR_Beat.z;
                float d = fromMiddle - since * 0.9;
                float surge = exp(-d * d * 60.0) * _GR_Beat.y * exp(-since * 1.4) * exp(-v * 4.0);

                float3 col = lerp(_ColorLow.rgb, _ColorHigh.rgb, smoothstep(0.15, 0.95, v / max(reach, 0.05)));
                float bright = (0.3 + 0.9 * level + 0.9 * surge) * _Brightness * _GR_Intensity;
                bright *= _Reflect > 0.5 ? 0.4 * (0.75 + 0.25 * sin(v * 140.0 + _GR_Clock * 3.0)) : 1.0;

                return half4(col * shape * bright, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

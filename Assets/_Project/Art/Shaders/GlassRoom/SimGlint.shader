// A point of light outside — a pier lamp, a firework spark — as an additive round glow, coloured per
// particle and brightened through _Color. The visualizer's LaserBeam does the same for the room; this
// one also takes depth and stencil settings, so the same glows can be drawn a second time mirrored
// under the sea, ignoring depth and clipped to the water by the stencil the sea writes — their
// reflections — exactly as the aurora's are.
Shader "VRMusic/SimGlint"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 1, 1, 1)
        _Softness ("Edge Softness", Range(0.5, 8)) = 2.2
        _Core ("Core Heat", Range(0, 1)) = 0.4
        _Waver ("Waver (reflections)", Range(0, 1)) = 0

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
            Name "SimGlint"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Softness;
                float _Core;
                float _Waver;
                float _ZTest;
                float _StencilComp;
                float _StencilRef;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // A reflection breaks up into wavering horizontal slivers, as light on moving water does.
                if (_Waver > 0.001)
                    uv.x += sin(uv.y * 40.0 + _GR_Clock * 3.0) * 0.06 * _Waver;

                float across = saturate(length(uv - 0.5) * 2.0);
                float profile = pow(1.0 - across, _Softness);

                if (_Waver > 0.001)
                    profile *= 1.0 - _Waver * 0.6 * step(0.5, frac(uv.y * 9.0 - _GR_Clock * 0.7));

                float3 col = _Color.rgb * input.color.rgb;
                float peak = max(col.r, max(col.g, col.b));
                col = col * profile + peak * pow(profile, 3.0) * _Core;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

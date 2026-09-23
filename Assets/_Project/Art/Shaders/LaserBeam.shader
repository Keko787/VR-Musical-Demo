// Additive glow for the 3D visualizer: a beam with a hot core and soft edges when it is on a
// LineRenderer (the profile runs across the line's V), or a radial spot when the material has
// Radial on (the profile runs out from the centre of a quad). Colour is HDR and is set per beam
// through a MaterialPropertyBlock, so brightness is carried in the colour, not a separate slider.
//
// No depth write and additive blending, so beams crossing each other simply add up, and nothing
// in the volume needs sorting.
Shader "VRMusic/LaserBeam"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 0.2, 0.2, 1)
        _Softness ("Edge Softness", Range(0.5, 8)) = 2.5
        _Core ("Core Heat", Range(0, 1)) = 0.6
        [Toggle(_RADIAL)] _Radial ("Radial (spot)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _RADIAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Softness;
                float _Core;
                float _Radial;
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
                #if defined(_RADIAL)
                    float across = saturate(length(input.uv - 0.5) * 2.0);
                #else
                    float across = saturate(abs(input.uv.y * 2.0 - 1.0));
                #endif

                float profile = pow(1.0 - across, _Softness);
                float3 col = _Color.rgb * input.color.rgb;

                // The middle of a bright beam reads white however saturated its colour is.
                float peak = max(col.r, max(col.g, col.b));
                float core = pow(profile, 3.0) * _Core;
                col = col * profile + peak * core;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

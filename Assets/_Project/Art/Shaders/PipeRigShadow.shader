// A soft round shadow for the pipe rig's balls, drawn on the floor screen: alpha-blended dark,
// fading from the middle of the quad to nothing at its edge, so no texture is needed. Opacity is
// carried in the colour's alpha and set per ball through a MaterialPropertyBlock.
Shader "VRMusic/SoftShadow"
{
    Properties
    {
        _Color ("Colour", Color) = (0, 0, 0, 0.5)
        _Softness ("Edge Softness", Range(0.5, 6)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Shadow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Softness;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float across = saturate(length(input.uv - 0.5) * 2.0);
                float alpha = pow(1.0 - across, _Softness) * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

// Firefly — URP particle shader for the Animal Orchestra's fireflies: a hot core and a soft halo,
// drawn from the billboard's UVs alone, additive.
//
// The particle colour carries everything: rgb is the light's colour and alpha its brightness, which
// FireflySwarm sets per fly every frame for the blink. Additive needs no sorting, which matters when
// four CAVE cameras each draw the same swarm, and a fly that is off is simply black — invisible.
Shader "VRMusic/Firefly"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Core ("Core sharpness", Range(4, 60)) = 22
        _Halo ("Halo strength", Range(0, 1)) = 0.35
        _Intensity ("Intensity", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        LOD 100

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Firefly"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Core;
                half _Halo;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float r2 = dot(p, p);

                // A sharp core that reads as a point of light, and a wide halo that fades to nothing
                // well inside the quad, so the billboard's square edge never shows.
                half core = exp(-r2 * _Core);
                half halo = exp(-r2 * 4.0) * _Halo * saturate(1.0 - r2);

                half3 rgb = _Tint.rgb * IN.color.rgb * IN.color.a * (core + halo) * _Intensity;
                return half4(rgb, 0.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Particles/Unlit"
}

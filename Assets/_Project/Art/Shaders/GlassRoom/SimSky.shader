// The sky over the glass room's outside worlds, drawn on a dome round the room. Everything about it
// — the gradient, the sun, cirrus, stars, the moon — comes from the active SimWorld through the
// shared globals, so one dome serves every world. The sun's glow swells a little with the music.
Shader "VRMusic/SimSky"
{
    Properties
    {
        _Exposure ("Exposure", Range(0, 2)) = 1
        _SunSwell ("Sun Swell With Loudness", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Skybox" }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "SimSky"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
                float _SunSwell;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.positionWS - _WorldSpaceCameraPos);
                float3 col = GR_Sky(dir, 1.0 + _SunSwell * _GR_Music.w);
                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

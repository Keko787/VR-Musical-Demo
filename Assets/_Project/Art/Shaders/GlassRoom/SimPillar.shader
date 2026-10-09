// A pillar of light falling from the sky to the ground, the way it does in the film the room is
// drawn from. One quad, turned about its own vertical axis to face whichever window camera draws
// it, so from the room it is always a column. Its width breathes with the bass and its light with
// the loudness (both through _Width/_Brightness from LightPillar); light streams down it; and a
// beat sends a bright band falling from the sky to the foot, where the ground ring sets off.
Shader "VRMusic/SimPillar"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.8, 0.95, 1.25, 1)
        _Width ("Width (m)", Float) = 16
        _Height ("Height (m)", Float) = 3400
        _Flare ("Flare At The Foot", Float) = 1.4
        _Softness ("Edge Softness", Range(0.5, 8)) = 1.6
        _Core ("Core Heat", Range(0, 2)) = 0.8
        _Brightness ("Brightness", Range(0, 4)) = 1
        _Stream ("Stream Speed", Float) = 0.12
        _FallSeconds ("Beat Fall (s)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SimPillar"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Width;
                float _Height;
                float _Flare;
                float _Softness;
                float _Core;
                float _Brightness;
                float _Stream;
                float _FallSeconds;
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
                float3 foot = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 up = normalize(TransformObjectToWorldDir(float3(0.0, 1.0, 0.0)));
                float3 toEye = _WorldSpaceCameraPos - foot;
                float3 across = normalize(cross(up, toEye));

                float metres = input.uv.y * _Height;
                float width = _Width * (1.0 + _Flare * exp(-metres / 40.0));
                float3 pos = foot + across * (input.uv.x - 0.5) * width + up * metres;

                output.positionWS = pos;
                output.positionCS = TransformWorldToHClip(pos);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float across = saturate(abs(input.uv.x * 2.0 - 1.0));
                float profile = pow(1.0 - across, _Softness);
                float core = pow(profile, 8.0) * _Core;

                float v = input.uv.y;
                float metres = v * _Height;
                float along = exp(-metres / (_Height * 0.55)) * 0.75 + 0.25;
                along *= smoothstep(1.0, 0.7, v);

                float stream = GR_Noise(float2(input.uv.x * 9.0, v * 14.0 + _GR_Clock * _Stream * 14.0));
                stream = 0.7 + 0.6 * stream;

                // The beat falls from the top of the sky to the foot.
                float since = _GR_Beat.z;
                float at = 1.0 - since / max(_FallSeconds, 0.05);
                float fall = exp(-pow((v - at) * 9.0, 2.0)) * step(since, _FallSeconds * 1.5) * _GR_Beat.y;

                float3 col = _Color.rgb * (profile * along * stream * _Brightness + core * along);
                col += float3(1.0, 1.0, 1.0) * fall * profile * 1.5;
                return half4(col * _GR_Intensity, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

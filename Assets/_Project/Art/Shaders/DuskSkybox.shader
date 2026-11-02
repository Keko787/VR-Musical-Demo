// Dusk skybox — the sky behind the Animal Orchestra: the moment after sunset, when starlings gather
// and fireflies come out.
//
// Everything is a function of the view direction, so it needs no textures and is right for every
// CAVE eye at once: a gradient from deep indigo overhead through violet to a warm horizon, a glow
// where the sun went down (behind the front wall, so the elephant and the flock stand against the
// brightest part of the sky), a few thin cloud streaks lit from below, and stars fading in overhead.
//
// The sky is the stage's backdrop and nothing more: kept a little darker than the animals so they
// read in front of it, and not much darker than that, because a projector's black is grey and a sky
// much darker reads as murk. Stars are sized in pixels so they stay pinpoints on every screen.
Shader "VRMusic/Dusk Skybox"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.030, 0.040, 0.150, 1)
        _UpperColor ("Upper sky", Color) = (0.180, 0.110, 0.330, 1)
        _HorizonColor ("Horizon", Color) = (0.700, 0.330, 0.330, 1)
        _NadirColor ("Below the horizon", Color) = (0.020, 0.030, 0.050, 1)
        _HorizonHeight ("Horizon band height", Range(0.02, 0.6)) = 0.18
        _GlowColor ("Sunset glow", Color) = (1.000, 0.560, 0.250, 1)
        _GlowDirection ("Sunset direction", Vector) = (0.15, -0.04, 1.0, 0)
        _GlowWidth ("Sunset glow width", Range(1, 32)) = 5
        _GlowStrength ("Sunset glow strength", Range(0, 3)) = 0.9
        _CloudColor ("Cloud", Color) = (0.950, 0.520, 0.500, 1)
        _CloudStrength ("Cloud strength", Range(0, 1)) = 0.4
        _StarDensity ("Star density", Range(20, 400)) = 150
        _StarBrightness ("Star brightness", Range(0, 3)) = 1.1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _UpperColor;
                half4 _HorizonColor;
                half4 _NadirColor;
                half _HorizonHeight;
                half4 _GlowColor;
                float4 _GlowDirection;
                half _GlowWidth;
                half _GlowStrength;
                half4 _CloudColor;
                half _CloudStrength;
                half _StarDensity;
                half _StarBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Clouds(float3 p)
            {
                return 0.55 * Noise(p) + 0.30 * Noise(p * 2.03 + 7.1) + 0.15 * Noise(p * 4.11 + 3.7);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.direction = IN.positionOS.xyz;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 d = normalize(IN.direction);
                float pixel = length(fwidth(d));
                float e = d.y;
                float up = saturate(e);

                // The gradient: warm at the horizon, violet above it, indigo overhead.
                half3 color = lerp(_HorizonColor.rgb, _UpperColor.rgb, smoothstep(0.0, _HorizonHeight, up));
                color = lerp(color, _ZenithColor.rgb, smoothstep(_HorizonHeight * 0.8, 0.85, up));
                color = lerp(color, _NadirColor.rgb, smoothstep(0.0, 0.3, saturate(-e)));

                // Where the sun went down: brightest at the horizon in that direction, spreading
                // along it and fading upward.
                float3 sun = normalize(_GlowDirection.xyz);
                float toward = saturate(dot(d, sun) * 0.5 + 0.5);
                half glow = pow(toward, _GlowWidth) * exp(-abs(e - sun.y) * 5.0);
                color += _GlowColor.rgb * glow * _GlowStrength;

                // Thin cloud streaks just above the horizon, stretched sideways and lit by the glow.
                float lift = max(e + 0.08, 0.04);
                float3 cloudPoint = float3(d.x / lift * 0.9, 0.0, d.z / lift * 0.9) * float3(0.6, 1.0, 2.2);
                cloudPoint.x += _Time.y * 0.004;
                half band = smoothstep(0.0, 0.06, e) * (1.0h - smoothstep(0.22, 0.45, e));
                half cloud = smoothstep(0.52, 0.78, Clouds(cloudPoint)) * band * _CloudStrength;
                color = lerp(color, _CloudColor.rgb * (0.35h + 0.9h * saturate(glow * 2.0h + 0.15h)), cloud);

                // Stars: one candidate per cell of a grid on the sphere, fading in with height and
                // out behind the clouds. Sized in pixels, so they stay points on every screen.
                float3 cell = floor(d * _StarDensity);
                float h = Hash(cell);
                float3 jitter = float3(Hash(cell + 11.3), Hash(cell + 27.1), Hash(cell + 41.9)) - 0.5;
                float3 centre = normalize((cell + 0.5 + jitter * 0.6) / _StarDensity);
                float distance = length(d - centre);
                half star = step(0.965, h) * (1.0h - smoothstep(pixel * 0.6, pixel * 1.6, distance));
                star *= lerp(0.4h, 1.0h, Hash(cell + 3.3)) * smoothstep(0.15, 0.55, e) * (1.0h - cloud);
                color += star * _StarBrightness;

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

// The water of the Sky Pool, after the collage the world comes from: it is not water but sky. Looking
// into the pool you look down into a blue sky with white clouds drifting in it, as if the pool were a
// hole through the sea to the sky on the other side.
//
// The music pours into the pool at its near end, under the springboard, and drifts away down it as
// cloud. The spectrogram is laid along the pool, newest at the near end and older further down, so a
// loud moment thickens the clouds where it is and then floats off toward the far end with them. Pitch
// runs across the pool: the bass down the middle, the treble along the walls.
//
// The surface is real enough to ripple: a little in the wind, more with the bass, and in rings from
// where a diver off the springboard would go in, one train on each beat (the world's beat rings). The
// rings bend the sky beneath as they pass and catch the light.
//
// The pool's shape (_Pool) comes from SimPool, which hands it over each frame from the same numbers
// that build the platform.
Shader "VRMusic/SimSkyPool"
{
    Properties
    {
        _SkyNear ("Sky Beneath, Looking Down", Color) = (0.22, 0.48, 0.9, 1)
        _SkyFar ("Sky Beneath, Toward The Far End", Color) = (0.1, 0.25, 0.6, 1)
        _CloudLit ("Cloud", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade", Color) = (0.66, 0.74, 0.9, 1)
        _CloudScale ("Cloud Size (m)", Float) = 7
        _Cover ("Cloud Cover At Rest", Range(0, 1)) = 0.4
        _CoverByMusic ("Cloud Added By The Music", Range(0, 1)) = 0.5
        _Deep ("Deeper Cloud", Range(0, 1)) = 0.45
        _FlowSpeed ("Drift Down The Pool (m/s)", Float) = 2.5
        _Ripple ("Wind Ripple (cm)", Float) = 1.2
        _RingRipple ("Beat Ripple (cm)", Float) = 4
        _RingWavelength ("Beat Ripple Wavelength (m)", Float) = 0.8
        _Refraction ("Refraction (m)", Float) = 1
        _Reflectivity ("Reflectivity", Range(0, 1)) = 0.25
        _Sheen ("Light On The Rings", Range(0, 2)) = 0.35
        _EdgeShade ("Shade Along The Walls", Range(0, 1)) = 0.3
        _FogScale ("Fog", Range(0, 2)) = 1
        _Pool ("Pool (half width, near end, far end, music from)", Vector) = (5, 2.4, 52.4, 2.4)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite On

        Pass
        {
            Name "SimSkyPool"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyNear;
                float4 _SkyFar;
                float4 _CloudLit;
                float4 _CloudShade;
                float _CloudScale;
                float _Cover;
                float _CoverByMusic;
                float _Deep;
                float _FlowSpeed;
                float _Ripple;
                float _RingRipple;
                float _RingWavelength;
                float _Refraction;
                float _Reflectivity;
                float _Sheen;
                float _EdgeShade;
                float _FogScale;
                float4 _Pool;
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

            /// The wind's ripples, 0–1.5: two drifting octaves of noise.
            float Wind(float2 xz)
            {
                float t = _GR_Clock;
                return GR_Noise(xz * 1.3 + float2(t * 0.35, t * 0.2)) + 0.5 * GR_Noise(xz * 2.9 - float2(t * 0.25, -t * 0.4));
            }

            /// The beat's ripples at a point: up to four trains spreading from where the diver went in.
            float Rings(float2 xzWS)
            {
                float r = length(xzWS - _GR_RingOrigin.xz);
                float speed = max(_GR_RingColor.a, 0.1);
                float k = 2.0 * GR_PI / max(_RingWavelength, 0.05);
                float sum = 0.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    // A ring may be set to start a moment from now (the world's ring delay): nothing yet.
                    float age = _GR_Clock - _GR_RingTimes[i];
                    float front = max(age, 0.0) * speed;
                    float x = r - front;
                    float width = 0.4 + front * 0.12;                     // the train lengthens as it spreads
                    float fade = step(0.0, age) * exp(-max(age, 0.0) * 1.4) * rsqrt(1.0 + front * 0.5);
                    sum += sin(x * k) * exp(-x * x / (width * width)) * fade * _GR_RingPower[i];
                }

                return sum;
            }

            /// The music at a point on the water: the band its distance from the middle stands for, as it
            /// was when this water was at the near end. Fades in under the board and out toward the far end.
            float Music(float2 xz)
            {
                float t = saturate(abs(xz.x) / max(_Pool.x, 0.1));
                float age = (xz.y - _Pool.w) / max(_FlowSpeed, 0.1);
                float held = max(_GR_Spec.y, 0.01);
                float alive = smoothstep(0.0, 0.5, age) * (1.0 - smoothstep(held * 0.65, held, age));

                // Neighbouring bands and slices blended, so the music's clouds have no creases.
                float d = 1.0 / max(_GR_BandCount - 1.0, 1.0);
                float a = max(age, 0.0);
                float level = GR_SpecAt(t, a) * 0.4
                            + (GR_SpecAt(t - d, a) + GR_SpecAt(t + d, a)) * 0.15
                            + (GR_SpecAt(t, max(a - 0.15, 0.0)) + GR_SpecAt(t, a + 0.15)) * 0.15;
                return level * alive;
            }

            /// Cloud, 0–1: warped noise, so the clouds billow rather than blot.
            float Cloud(float2 q)
            {
                float2 warp = float2(GR_Fbm(q * 0.45 + 5.2), GR_Fbm(q * 0.45 + 9.7)) - 0.5;
                return GR_Fbm(q + warp * 1.4);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz - _GR_Origin.xz;
                float3 toEye = _WorldSpaceCameraPos - input.positionWS;
                float dist = length(toEye);
                float3 V = toEye / max(dist, 0.001);

                // The surface, as slopes: wind ripples (more with the bass, smoothed away with distance)
                // and the beat's rings.
                float e = 0.04;
                float wind = _Ripple * 0.01 * (0.4 + 1.2 * _GR_Music.x) / (1.0 + dist * 0.04);
                float w0 = Wind(xz);
                float wx = Wind(xz + float2(e, 0.0));
                float wz = Wind(xz + float2(0.0, e));
                float r0 = Rings(input.positionWS.xz);
                float rx = Rings(input.positionWS.xz + float2(e, 0.0));
                float rz = Rings(input.positionWS.xz + float2(0.0, e));
                float2 ringSlope = float2(rx - r0, rz - r0) / e * (_RingRipple * 0.01);
                float2 slope = float2(wx - w0, wz - w0) / e * wind + ringSlope;
                float3 N = normalize(float3(-slope.x, 1.0, -slope.y));

                // The sky beneath: blue looking down into it, deeper toward the far end.
                float3 col = lerp(_SkyFar.rgb, _SkyNear.rgb, smoothstep(0.02, 0.6, V.y));

                // Everything below is seen through the moving surface, bent by its slopes.
                float2 bent = xz - slope * _Refraction;

                // A deeper, slower deck of cloud, faint and blue: the sky beneath has depth.
                float2 qd = float2(bent.x, bent.y - _FlowSpeed * 0.55 * _GR_Clock) / (_CloudScale * 0.5) + 31.7;
                float deep = smoothstep(0.56, 0.74, Cloud(qd)) * _Deep;
                col = lerp(col, lerp(col, _CloudShade.rgb, 0.65), deep);

                // The clouds, thickened by the music and drifting down the pool with it.
                float2 q = float2(bent.x, bent.y - _FlowSpeed * _GR_Clock) / max(_CloudScale, 0.1);
                float n = Cloud(q);
                float cover = saturate(_Cover + _CoverByMusic * Music(xz));
                float threshold = lerp(0.75, 0.3, cover);
                float density = smoothstep(threshold, threshold + 0.16, n);

                // Lit from above by the world's sun: brighter on the slopes toward it and in the thick of a cloud.
                float2 toSun = normalize(_GR_SunDir.xz + 1e-4) * 0.08;
                float lit = saturate(0.55 + (n - Cloud(q + toSun)) * 5.0 + (n - threshold) * 1.5);
                col = lerp(col, lerp(_CloudShade.rgb, _CloudLit.rgb, lit), density);

                // In the shade of the walls along their foot. The far end is an infinity edge: no wall.
                float wall = min(_Pool.x - abs(xz.x), xz.y - _Pool.y);
                col *= 1.0 - _EdgeShade * (1.0 - smoothstep(0.0, 0.5, wall));

                // A little of the real sky on the surface, more at a glancing angle, and light on the rings.
                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(N, V)), 5.0);
                col = lerp(col, GR_Sky(R, 0.0), saturate(fresnel * _Reflectivity));
                col += _GR_Horizon.rgb * saturate(length(ringSlope) * 2.0) * _Sheen * _GR_Intensity;

                col = GR_ApplyFog(col, input.positionWS, _FogScale);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

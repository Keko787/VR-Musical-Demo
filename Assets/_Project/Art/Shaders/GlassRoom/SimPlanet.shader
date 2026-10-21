// A planet or a star hanging in the sky of a space world, drawn on one quad that turns to face the
// room's eye. The quad is turned toward the eye's position rather than any camera's axes, so the three
// window cameras, which share that eye, all see the same disc, ring and lighting at the seams.
//
// A planet is a banded sphere lit from the world's sun, with an atmosphere at its limb and, if it
// has one, a ring of thin ringlets — and the ring is the spectrum: round it, each stretch is a band,
// bass at the front, and it brightens with its band like a circular meter. The ring's far half hides
// behind the planet and its near half crosses in front.
//
// A star is a burning disc with a corona whose flames reach further with the bass, and a beat throws
// a ring of flare out from its edge.
//
// Premultiplied alpha: the disc is solid, the glows add light.
Shader "VRMusic/SimPlanet"
{
    Properties
    {
        _Star ("Is A Star", Range(0, 1)) = 0
        _Radius ("Radius (m)", Float) = 800
        _Extent ("Quad Half-Size (radii)", Float) = 2.4

        [Header(Planet)]
        _ColorA ("Bands A", Color) = (0.3, 0.6, 0.95, 1)
        _ColorB ("Bands B", Color) = (0.12, 0.3, 0.75, 1)
        _Bands ("Band Count", Float) = 7
        [HDR] _Atmosphere ("Atmosphere", Color) = (0.4, 0.8, 1.4, 1)
        _NightColor ("Night Side", Color) = (0.03, 0.05, 0.14, 1)

        [Header(Ring)]
        _RingInner ("Ring Inner (radii)", Float) = 0
        _RingOuter ("Ring Outer (radii)", Float) = 0
        _RingTilt ("Ring Tilt (minor over major)", Range(0.02, 1)) = 0.22
        _RingAngle ("Ring Angle (degrees)", Float) = -18
        [HDR] _RingColor ("Ring Colour", Color) = (0.85, 0.95, 1.3, 1)
        _RingLines ("Ringlets", Float) = 3

        [Header(Star)]
        [HDR] _StarColor ("Star", Color) = (1.6, 1.1, 0.35, 1)
        [HDR] _CoronaColor ("Corona", Color) = (1.4, 0.55, 0.15, 1)
        _Corona ("Corona Reach (radii)", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SimPlanet"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Star;
                float _Radius;
                float _Extent;
                float4 _ColorA;
                float4 _ColorB;
                float _Bands;
                float4 _Atmosphere;
                float4 _NightColor;
                float _RingInner;
                float _RingOuter;
                float _RingTilt;
                float _RingAngle;
                float4 _RingColor;
                float _RingLines;
                float4 _StarColor;
                float4 _CoronaColor;
                float _Corona;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc : TEXCOORD0;      // where on the quad, in radii from the centre
                float3 light : TEXCOORD1;     // the sun, in the quad's frame (x right, y up, z toward the eye)
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 toward = normalize(_WorldSpaceCameraPos - centre);
                float3 right = normalize(cross(toward, float3(0.0, 1.0, 0.0)));
                float3 up = cross(right, toward);

                float2 corner = (input.uv - 0.5) * 2.0 * _Extent;
                float3 position = centre + (right * corner.x + up * corner.y) * _Radius;

                output.positionWS = position;
                output.positionCS = TransformWorldToHClip(position);
                output.disc = corner;
                output.light = float3(dot(_GR_SunDir.xyz, right), dot(_GR_SunDir.xyz, up), dot(_GR_SunDir.xyz, toward));
                return output;
            }

            float4 Over(float4 top, float4 under)
            {
                return top + under * (1.0 - top.a);
            }

            float4 Planet(float2 p, float3 light)
            {
                float r = length(p);
                float4 result = 0.0;

                // The ring, in its own tilted frame.
                float4 nearRing = 0.0;
                float4 farRing = 0.0;
                if (_RingOuter > _RingInner)
                {
                    float angle = radians(_RingAngle);
                    float2 q = float2(cos(angle) * p.x + sin(angle) * p.y, -sin(angle) * p.x + cos(angle) * p.y);
                    float rr = length(float2(q.x, q.y / max(_RingTilt, 0.02)));
                    float across = (rr - _RingInner) / max(_RingOuter - _RingInner, 0.001);

                    if (across > 0.0 && across < 1.0)
                    {
                        float ringlets = pow(abs(sin(across * GR_PI * max(_RingLines, 1.0))), 10.0);
                        float soft = sin(across * GR_PI);

                        // Round the ring is the spectrum: the front of the ring the bass, the back the treble.
                        float around = atan2(q.y / max(_RingTilt, 0.02), q.x) / (2.0 * GR_PI) + 0.25;
                        float t = abs(frac(around) * 2.0 - 1.0);
                        float level = GR_Band(1.0 - t);
                        float bright = 0.3 + 1.4 * level * level * _GR_Intensity;

                        float coverage = saturate(ringlets * 0.9 + soft * 0.15);
                        float3 colour = _RingColor.rgb * _GR_PlanetTint.rgb * bright;
                        float4 ring = float4(colour * coverage, coverage * 0.85);
                        if (q.y < 0.0)
                            nearRing = ring;
                        else
                            farRing = ring;
                    }
                }

                result = farRing;

                if (r < 1.0)
                {
                    float3 n = float3(p, sqrt(1.0 - r * r));
                    float lit = smoothstep(-0.12, 0.25, dot(n, normalize(light)));

                    float swirl = GR_Noise(p * 3.0 + float2(_GR_Clock * 0.01, 0.0)) - 0.5;
                    float stripes = sin((n.y + swirl * 0.12) * _Bands * GR_PI) * 0.5 + 0.5;
                    float3 surface = lerp(_ColorA.rgb, _ColorB.rgb, stripes) * _GR_PlanetTint.rgb;
                    surface *= 0.8 + 0.2 * n.z;                                  // limb darkening

                    float3 colour = lerp(_NightColor.rgb, surface, lit);
                    colour += _Atmosphere.rgb * pow(1.0 - n.z, 3.0) * (0.25 + 0.75 * lit) * (0.85 + 0.3 * _GR_Music.w);
                    result = float4(colour, 1.0);
                }
                else
                {
                    // A thin atmosphere glowing just past the limb, on the lit side most.
                    float side = saturate(dot(normalize(float3(p, 0.0)), normalize(light)) * 0.5 + 0.5);
                    float halo = exp(-(r - 1.0) * 14.0) * (0.3 + 0.7 * side);
                    result = Over(result, float4(_Atmosphere.rgb * halo * 0.6, 0.0));
                }

                return Over(nearRing, result);
            }

            float4 Star(float2 p)
            {
                float r = length(p);
                float bass = _GR_Music.x;
                float4 result = 0.0;

                if (r < 1.0)
                {
                    // A burning surface: granules drifting, darker spots, brighter toward the middle.
                    float3 n = float3(p, sqrt(1.0 - r * r));
                    float granules = GR_Fbm(p * 4.0 + float2(_GR_Clock * 0.03, -_GR_Clock * 0.02));
                    float spots = smoothstep(0.62, 0.75, GR_Fbm(p * 2.2 + 11.0));
                    float3 colour = _StarColor.rgb * _GR_StarTint.rgb * (0.75 + 0.35 * granules) * (1.0 - 0.45 * spots);
                    colour *= 0.7 + 0.3 * n.z;
                    colour += _CoronaColor.rgb * pow(1.0 - n.z, 4.0) * 1.2;
                    result = float4(colour, 1.0);
                }
                else
                {
                    // The corona: flames round the edge, reaching further with the bass.
                    float angle = atan2(p.y, p.x);
                    float flames = GR_Noise1(angle * 9.0 + _GR_Clock * 0.25) * 0.6 + GR_Noise1(angle * 23.0 - _GR_Clock * 0.4) * 0.4;
                    float reach = _Corona * (0.55 + 0.9 * bass) * (0.6 + 0.8 * flames);
                    float glow = exp(-(r - 1.0) / max(reach, 0.01) * 2.5);

                    // A beat throws a ring of flare out from the edge.
                    float since = _GR_Beat.z;
                    float front = 1.0 + since * 1.1;
                    float flare = exp(-abs(r - front) * 14.0) * _GR_Beat.y * exp(-since * 2.2);

                    float3 colour = (_CoronaColor.rgb * glow * (0.8 + 0.5 * bass) + _StarColor.rgb * flare) * _GR_StarTint.rgb;
                    result = float4(colour * _GR_Intensity, 0.0);
                }

                return result;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // No haze: these hang beyond any air, and haze on premultiplied colour would tint the
                // quad's empty corners.
                float4 colour = _Star > 0.5 ? Star(input.disc) : Planet(input.disc, input.light);
                return half4(colour);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

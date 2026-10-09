// The sea in front of the beach. Swells roll in parallel to the shore, a little uneven along it,
// growing toward the sand and dying in the last few metres, and the bass swells them: the louder the
// low end, the bigger the sea. Each swell runs up the sand as it arrives, and a beat sends the surf
// surging up the whole beach at once, so the beat breaks on the shore the moment it is heard.
//
// The water is deep blue offshore and green-blue in the shallows, reflects the world's sky, and
// glitters along the path toward the sun with the treble. Foam laces the waterline, the breaking
// crests and the leading edge of a surge.
//
// Height is absolute: the mesh only supplies where, the shader decides how high. It knows where the
// sand is from the beach's own formula (_Shore, set by SimOcean from the beach's SimSurface), which
// is a plain slope near the waterline for exactly this reason. Like the lake, it marks itself in the
// stencil buffer, so mirrored glows (the pier lamps, the fireworks) can be drawn on it as reflections.
Shader "VRMusic/SimOcean"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.03, 0.12, 0.22, 1)
        _Shallow ("Shallow", Color) = (0.12, 0.5, 0.55, 1)
        _FoamColor ("Foam", Color) = (1, 0.96, 0.92, 1)
        _Swell ("Swell At Rest (m)", Float) = 0.3
        _SwellByBass ("Swell Added By The Bass (m)", Float) = 0.9
        _Wavelength ("Wavelength (m)", Float) = 26
        _Speed ("Wave Speed (m/s)", Float) = 6
        _Swash ("Run-Up Of Each Swell (m)", Float) = 0.12
        _Surge ("Surge On The Beat (m)", Float) = 0.3
        _SurgeReach ("Surge Reach Out To Sea (m)", Float) = 30
        _Reflectivity ("Reflectivity", Range(0, 1)) = 0.75
        _Glitter ("Glitter", Range(0, 4)) = 1.5
        _Ripple ("Ripple", Range(0, 2)) = 0.5
        _FoamWidth ("Shore Foam (m of water)", Float) = 0.12
        _FogScale ("Fog", Range(0, 2)) = 1
        _Shore ("Shore (distance, slope, bay radius, sea level)", Vector) = (7, 0.05, 900, -1.2)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite On

        // Marks the visible sea, so the lamps' and fireworks' reflections are drawn on it and nowhere else.
        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            Name "SimOcean"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Deep;
                float4 _Shallow;
                float4 _FoamColor;
                float _Swell;
                float _SwellByBass;
                float _Wavelength;
                float _Speed;
                float _Swash;
                float _Surge;
                float _SurgeReach;
                float _Reflectivity;
                float _Glitter;
                float _Ripple;
                float _FoamWidth;
                float _FogScale;
                float4 _Shore;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 local : TEXCOORD2;
                float crest : TEXCOORD3;
            };

            /// Metres out to sea from the waterline: negative on the sand.
            float Offshore(float2 xz)
            {
                return xz.y - (_Shore.x + xz.x * xz.x / (2.0 * max(_Shore.z, 1.0)));
            }

            /// The beach's height (from the sea level) at a point: the same formula SimSurface uses near the waterline.
            float Sand(float d)
            {
                return d >= 0.0
                    ? -10.0 * (1.0 - exp(-_Shore.y * d / 10.0))
                    : -_Shore.y * d;
            }

            /// The surf's surge on the beat: up over a quarter second, back down over a second or so.
            float SurgeEnvelope()
            {
                float since = _GR_Beat.z;
                return smoothstep(0.0, 0.25, since) * exp(-max(since - 0.25, 0.0) * 1.6) * _GR_Beat.y;
            }

            /// Height of the sea above sea level at a point, and how much of a breaking crest is there.
            float Sea(float2 xz, out float crest)
            {
                float d = Offshore(xz);
                float t = _GR_Clock;
                float amp = _Swell + _SwellByBass * _GR_Music.x;
                float k = 2.0 * GR_PI / max(_Wavelength, 1.0);

                // Swells come in parallel to the shore, crests a little bent along it.
                float bend = (GR_Noise(float2(xz.x * 0.012, 0.5)) - 0.5) * 8.0;
                float phase = k * (d + bend + _Speed * t);
                float swell = exp(1.6 * (cos(phase) - 1.0));            // sharp crests, long troughs
                float phase2 = 0.53 * k * (0.9 * d + 0.25 * xz.x + 0.9 * _Speed * t) + 1.7;
                float swell2 = exp(1.4 * (cos(phase2) - 1.0));
                float chop = GR_Noise(xz / 6.0 + float2(t * 0.3, -t * 0.5)) - 0.5;

                // Growing toward the shore, gone in the last few metres.
                float shoal = (1.0 + 0.6 * exp(-max(d, 0.0) / 25.0)) * smoothstep(0.0, 6.0, d);
                float h = (swell * 0.75 + swell2 * 0.35 - 0.3) * amp * shoal + chop * 0.08;
                crest = swell * shoal * saturate(amp);

                // At the waterline: each swell runs up the sand as it arrives, and the beat surges it.
                float arrive = exp(1.6 * (cos(k * (bend + _Speed * t)) - 1.0));
                float near = 1.0 - smoothstep(0.0, _SurgeReach, d + 10.0);
                h += near * (arrive * _Swash * (0.5 + amp) + _Surge * SurgeEnvelope());
                return h;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float2 xz = world.xz - _GR_Origin.xz;

                float crest, unused;
                float h = Sea(xz, crest);
                float e = max(0.5, length(xz) * 0.01);
                float hx = Sea(xz + float2(e, 0.0), unused);
                float hz = Sea(xz + float2(0.0, e), unused);

                float3 position = float3(world.x, _GR_Origin.y + _Shore.w + h, world.z);
                output.positionWS = position;
                output.positionCS = TransformWorldToHClip(position);
                output.normalWS = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));
                output.local = xz;
                output.crest = crest;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.local;
                float t = _GR_Clock;
                float3 toEye = _WorldSpaceCameraPos - input.positionWS;
                float dist = length(toEye);
                float3 V = toEye / max(dist, 0.001);

                // Fine ripples on top of the swell, flattening with distance.
                float2 q = p / 3.0;
                float r0 = GR_Noise(q + float2(t * 0.2, t * 0.31));
                float rx = GR_Noise(q + float2(0.05, 0.0) + float2(t * 0.2, t * 0.31));
                float rz = GR_Noise(q + float2(0.0, 0.05) + float2(t * 0.2, t * 0.31));
                float ripple = _Ripple / (1.0 + dist * 0.01);
                float3 N = normalize(input.normalWS + float3(-(rx - r0), 0.0, -(rz - r0)) * 20.0 * ripple);

                // How deep the water is here, from the beach's formula.
                float d = Offshore(p);
                float depth = (input.positionWS.y - _GR_Origin.y - _Shore.w) - Sand(d);

                float3 lit = lerp(_GR_ShadowColor.rgb, _GR_SunColor.rgb, 0.55);
                float3 water = lerp(_Shallow.rgb, _Deep.rgb, saturate(depth / 5.0)) * lit;

                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 col = lerp(water, GR_Sky(R, 0.0), saturate(fresnel * _Reflectivity + 0.08));

                // The glitter path toward the sun: sparkles that the treble sets dancing.
                float toward = saturate(dot(R, _GR_SunDir.xyz));
                float sparkle = GR_Noise(p * 0.9 + float2(t * 0.7, t * 0.4)) * GR_Noise(p * 2.3 - float2(t * 0.5, -t * 0.3));
                sparkle = smoothstep(0.35, 0.7, sparkle);
                col += _GR_SunColor.rgb * (pow(toward, 400.0) * sparkle * _Glitter * (0.4 + 1.6 * _GR_Music.z) * 6.0
                    + pow(toward, 25.0) * 0.25) * _GR_Intensity;

                // Foam: lace at the waterline, the breaking crests, and the front of a surge.
                float lace = smoothstep(0.3, 0.7, GR_Noise(p * 1.4 + float2(t * 0.25, -t * 0.4)));
                float edge = 1.0 - smoothstep(0.0, _FoamWidth, depth);
                float breaking = smoothstep(0.7, 0.95, input.crest) * smoothstep(45.0, 12.0, d) * smoothstep(1.0, 5.0, d);
                float surge = SurgeEnvelope() * (1.0 - smoothstep(0.0, _FoamWidth * 4.0, depth)) * step(d, 12.0);
                float foam = saturate(edge * (0.55 + 0.45 * lace) + breaking * lace + surge * (0.6 + 0.4 * lace));
                col = lerp(col, _FoamColor.rgb * (lit + 0.15), foam);

                col = GR_ApplyFog(col, input.positionWS, _FogScale);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

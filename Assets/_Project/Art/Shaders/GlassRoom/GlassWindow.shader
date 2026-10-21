// A wall of the glass room: the view out, which is a camera's picture of the world outside drawn
// flat on the CAVE screen, and the glass in front of it — a faint tint, a haze that thickens toward
// the floor, soft streaks of light, and the pendant lamps reflected in it.
//
// The view is flat on purpose. A picture on the screen surface looks the same from anywhere in the
// room, so the outside never skews for the people without glasses, and the three walls' pictures
// are taken from one fixed eye so they meet at the corners as one panorama.
//
// Between worlds the view breaks into tiles that shrink away into the simulation's blank page, in a
// wave that runs round the room from the back of the left wall to the back of the right, and the
// next world builds back out of tiles behind it. _Dissolve runs 0 → 1 going out and 1 → 2 coming in.
//
// Between a world's variants the change is quicker and rougher: a glitch, like a datamosh. Rows of the
// picture shear and smear sideways, blocks pixelate, and the colour channels part; the look swaps
// underneath at its worst. _Glitch is its strength, 0 to 1.
Shader "VRMusic/GlassWindow"
{
    Properties
    {
        _MainTex ("View", 2D) = "white" {}
        _WallSize ("Wall Size (m)", Vector) = (3.556, 2.2225, 0, 0)
        _Exposure ("Exposure", Range(0, 2)) = 1
        _Tint ("Glass Tint", Color) = (0.86, 0.95, 1, 1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.12
        _HazeColor ("Haze Colour", Color) = (0.93, 0.96, 1, 1)
        _Haze ("Haze", Range(0, 1)) = 0.06
        _Streaks ("Streaks", Range(0, 1)) = 0.05
        _ReflColor ("Lamp Reflection", Color) = (1, 0.72, 0.45, 1)
        _Dissolve ("Dissolve", Range(0, 2)) = 0
        _CellSize ("Tile Size (m)", Float) = 0.11
        _Sweep ("Sweep (offset m, perimeter m)", Vector) = (0, 8.3312, 0, 0)
        _VoidColor ("Blank Page", Color) = (0.8, 0.88, 0.97, 1)
        _EdgeColor ("Tile Edge", Color) = (1, 1, 1, 1)
        _Glitch ("Glitch", Range(0, 1)) = 0
        _GlitchSeed ("Glitch Seed", Float) = 0
        _GlitchStep ("Glitch Steps (0 smooth, 1 stepped)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite On

        Pass
        {
            Name "GlassWindow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _WallSize;
                float _Exposure;
                float4 _Tint;
                float _TintAmount;
                float4 _HazeColor;
                float _Haze;
                float _Streaks;
                float4 _ReflColor;
                float _Dissolve;
                float _CellSize;
                float4 _Sweep;
                float4 _VoidColor;
                float4 _EdgeColor;
                float _Glitch;
                float _GlitchSeed;
                float _GlitchStep;
            CBUFFER_END

            // Per window, from WindowView: where each lamp is reflected (u, v), its blob radius in
            // metres, and its strength.
            float4 _Lamps[4];

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
                float2 uv = input.uv;
                float2 m = uv * _WallSize.xy;            // metres across and up this wall

                float shown = 1.0;
                float edge = 0.0;
                float grid = 0.0;
                float2 sampleUV = uv;

                if (_Dissolve > 0.0001)
                {
                    float2 cellF = m / _CellSize;
                    float2 cell = floor(cellF);
                    float2 local = frac(cellF);

                    // Where this tile sits in the wave: round the room, a little random, the floor first.
                    float sweep = (_Sweep.x + (cell.x + 0.5) * _CellSize) / max(_Sweep.y, 0.01);
                    float jitter = GR_Hash12(cell + float2(_Sweep.x * 13.7, 3.1));
                    float order = sweep * 0.68 + jitter * 0.22 + uv.y * 0.1;

                    float gone = _Dissolve <= 1.0
                        ? saturate((_Dissolve * 1.3 - order) / 0.2)
                        : 1.0 - saturate(((_Dissolve - 1.0) * 1.3 - order) / 0.2);

                    // A tile in flight shows its own colour as a mosaic, shrinking away with a bright rim.
                    float size = 1.0 - gone;
                    float2 q = abs(local - 0.5);
                    float extent = max(q.x, q.y);
                    float inside = step(extent, size * 0.5);
                    float inFlight = step(0.001, gone) * step(gone, 0.999);

                    sampleUV = lerp(uv, (cell + 0.5) * _CellSize / _WallSize.xy, inFlight);
                    edge = inFlight * inside * smoothstep(size * 0.5 - 0.1, size * 0.5, extent);
                    shown = inside;

                    float rule = 1.0 - smoothstep(0.0, 0.035, min(min(local.x, 1.0 - local.x), min(local.y, 1.0 - local.y)));
                    grid = rule * gone;
                }

                // The glitch between variants: rows shear and smear, blocks pixelate, channels part.
                float split = 0.0;
                if (_Glitch > 0.001)
                {
                    float g = _Glitch;

                    // Stepped, the pattern jumps six times a second; smooth (low intensity), it holds still.
                    float t = _GlitchSeed + (_GlitchStep > 0.5 ? floor(_GR_Clock * 6.0) : 0.0);

                    float rowSize = lerp(0.22, 0.05, g);
                    float row = floor(m.y / rowSize + GR_Hash11(floor(m.y / 0.4) + t) * 2.0);
                    float moved = step(1.0 - 0.75 * g, GR_Hash12(float2(row, t)));
                    float shift = (GR_Hash12(float2(row, t + 3.7)) - 0.5) * 0.35 * g * moved;
                    float2 gm = m + float2(shift * _WallSize.x, 0.0);

                    // Smear: in some moved rows each stretch of the picture drags its left edge across.
                    float stretch = 0.18 + 0.5 * GR_Hash12(float2(row, t + 9.1));
                    float smear = moved * step(0.45, GR_Hash12(float2(row, t + 5.3)));
                    gm.x = lerp(gm.x, floor(gm.x / stretch) * stretch + 0.02, smear * g);

                    // Blocks, as it peaks.
                    float block = 0.07 * smoothstep(0.35, 1.0, g);
                    if (block > 0.001)
                        gm = (floor(gm / block) + 0.5) * block;

                    sampleUV = gm / _WallSize.xy;
                    split = 0.012 * g;
                }

                float3 view;
                if (split > 0.0)
                {
                    view.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV + float2(split, 0.0)).r;
                    view.g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).g;
                    view.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV - float2(split, 0.0)).b;
                }
                else
                {
                    view = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).rgb;
                }
                view *= _Exposure;
                float3 blank = _VoidColor.rgb * (1.0 + 0.15 * grid);
                float3 col = lerp(blank, view, shown) + _EdgeColor.rgb * edge * 0.5 * _VoidColor.a;

                // The glass.
                col = lerp(col, col * _Tint.rgb, _TintAmount);
                col = lerp(col, _HazeColor.rgb, _Haze * (0.5 + 0.5 * saturate(1.2 - uv.y * 1.4)));

                float streak = sin((m.x * 0.8 + m.y * 0.45) * 2.6 + 0.6) * 0.5 + 0.5;
                streak = smoothstep(0.82, 1.0, streak) * (0.6 + 0.4 * sin(m.x * 0.7 + 1.3));
                col += streak * _Streaks;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float4 lamp = _Lamps[i];
                    float2 d = (uv - lamp.xy) * _WallSize.xy;
                    float r = max(lamp.z, 0.01);
                    col += _ReflColor.rgb * lamp.w * exp(-dot(d, d) / (r * r));
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

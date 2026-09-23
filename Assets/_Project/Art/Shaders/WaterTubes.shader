// The 2D visualizer's wall: a row of glass tubes with water in them, drawn entirely in the
// fragment shader on one quad per CAVE screen. The C# side (WaterTubePanel) does the water's
// motion and hands over per-tube arrays: the level, the slosh of the surface, a glow, a colour.
// Everything else — the glass, the meniscus, the depth shading, the caustics, the bubbles, the
// colour wash behind — is computed here from those and the time.
//
// Unlit and opaque on purpose. It is flat content on a projection surface; lighting it would only
// make the four screens disagree.
Shader "VRMusic/WaterTubes"
{
    Properties
    {
        _PanelSize ("Panel Size (m)", Vector) = (3.556, 2.2225, 0, 0)
        _TubeCount ("Tube Count", Float) = 24
        _TubeFill ("Tube Fill", Range(0.2, 1)) = 0.7
        _EndMargin ("End Margin (m)", Float) = 0.08
        _Opacity ("Tube Opacity", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 2)) = 1
        _WashColor ("Wash Colour", Color) = (0.01, 0.02, 0.08, 1)
        _Beat ("Beat", Range(0, 1)) = 0
        _Bass ("Bass", Range(0, 1)) = 0
        _GlassColor ("Glass Colour", Color) = (0.6, 0.85, 1, 1)
        _GlassStrength ("Glass Strength", Range(0, 1)) = 0.35
        _Bubbles ("Bubbles", Range(0, 1)) = 0.75
        _Ripple ("Ripple", Range(0, 1)) = 0.6
        _Caustics ("Caustics", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "WaterTubes"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_TUBES 64

            CBUFFER_START(UnityPerMaterial)
                float4 _PanelSize;
                float _TubeCount;
                float _TubeFill;
                float _EndMargin;
                float _Opacity;
                float _Intensity;
                float4 _WashColor;
                float _Beat;
                float _Bass;
                float4 _GlassColor;
                float _GlassStrength;
                float _Bubbles;
                float _Ripple;
                float _Caustics;
            CBUFFER_END

            // Per tube, from WaterTubePanel through a MaterialPropertyBlock.
            float _Level[MAX_TUBES];
            float _Slosh[MAX_TUBES];
            float _Glow[MAX_TUBES];
            float4 _Color[MAX_TUBES];

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

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float count = max(1.0, _TubeCount);
                float tubeF = input.uv.x * count;
                int idx = clamp((int)floor(tubeF), 0, MAX_TUBES - 1);
                float lx = frac(tubeF);

                // Tube geometry in metres, centred on this tube's cell, so the tubes come out round
                // on a quad that is 3.5 m by 2.2 m.
                float cellW = _PanelSize.x / count;
                float H = _PanelSize.y;
                float radius = cellW * 0.5 * _TubeFill;
                float halfLen = max(0.0, H * 0.5 - _EndMargin - radius);
                float tubeLen = 2.0 * (halfLen + radius);

                float2 pm = float2((lx - 0.5) * cellW, (input.uv.y - 0.5) * H);
                float2 q = float2(pm.x, pm.y - clamp(pm.y, -halfLen, halfLen));
                float d = length(q) - radius;               // capsule distance, negative inside

                float t = _Time.y;
                float level = saturate(_Level[idx]);
                float slosh = _Slosh[idx];
                float glow = saturate(_Glow[idx]);
                float3 water = _Color[idx].rgb;
                float3 deep = water * float3(0.2, 0.28, 0.5);
                float3 foam = lerp(water, float3(1.0, 1.0, 1.0), 0.65);

                // The wash behind the tubes: a dark colour the C# side already lifted with the bass
                // and the beat, a touch lighter toward the top, plus the water's glow leaking out
                // through the glass.
                float3 bg = _WashColor.rgb * (0.7 + 0.3 * input.uv.y);
                float leak = exp(-max(d, 0.0) / max(radius * 0.9, 1e-3));
                bg += water * leak * (0.25 * level + 0.5 * glow) * 0.5 * _Opacity;

                // Where the surface is: the level, tilted and bulged by the slosh, with small
                // travelling ripples that get busier when the water is moving.
                float xr = pm.x / max(radius, 1e-4);        // -1 … 1 across the tube
                float bottom = -halfLen - radius;
                float surfaceY = bottom + tubeLen * level;
                surfaceY += slosh * radius * (xr + 0.35 * cos(xr * 3.14159));
                float activity = 0.25 + 3.0 * abs(slosh) + glow;
                float ripple = _Ripple * radius * 0.05 * activity;
                surfaceY += ripple * (0.6 * sin(xr * 6.0 + t * 7.0 + idx) + 0.4 * sin(xr * 11.0 - t * 4.3 + idx * 1.7));

                float depth = surfaceY - pm.y;              // positive under water
                float cyl = sqrt(saturate(1.0 - xr * xr));  // round shading across the tube

                // Empty glass above the water: faintly tinted, mostly wash.
                float3 glass = _GlassColor.rgb * 0.06 + bg * 0.6;

                // The water: lighter and more saturated at the surface, darker with depth, shaded
                // round, with caustic shimmer that brightens as the level rises.
                float shade = 0.5 + 0.5 * cyl;
                float3 body = lerp(water, deep, saturate(depth / max(tubeLen * 0.7, 1e-3))) * shade;
                float c1 = sin(pm.x * 90.0 + t * 2.3 + idx) * sin(pm.y * 70.0 - t * 1.9);
                float c2 = sin((pm.x + pm.y) * 55.0 - t * 3.1 + idx * 0.5);
                body += water * _Caustics * (0.35 * saturate(c1) + 0.2 * saturate(c2)) * (0.4 + level) * shade;

                // Bubbles: a few per tube, each on its own loop up the glass, drawn as a bright ring
                // and only while it is under the surface.
                [unroll]
                for (int b = 0; b < 4; b++)
                {
                    float hb = hash11(idx * 7.13 + b * 3.71 + 1.0);
                    float speed = 0.1 + 0.16 * hash11(hb * 91.7);
                    float bx = (hb * 2.0 - 1.0) * radius * 0.6;
                    float by = bottom + frac(t * speed + hash11(hb * 3.3 + 17.0)) * tubeLen;
                    float br = radius * (0.06 + 0.08 * hash11(hb * 5.5));
                    float bd = length(pm - float2(bx, by)) - br;
                    float ring = 1.0 - smoothstep(0.0, 0.0025, abs(bd));
                    float inside = 1.0 - smoothstep(-br, 0.0, bd);
                    float visible = step(by + br, surfaceY) * step(b + 0.5, _Bubbles * 4.0 + 0.5);
                    body += (foam * ring * 0.8 + water * inside * 0.25) * visible;
                }

                // The meniscus: a bright line at the surface that flashes on a rise and on the beat.
                float meniscus = 1.0 - smoothstep(0.0, radius * 0.14, abs(depth));
                float3 surfaceCol = foam * (1.1 + 2.5 * glow + 1.5 * _Beat);

                float underwater = smoothstep(-0.0015, 0.0015, depth);
                float3 tube = lerp(glass, body, underwater);
                tube = lerp(tube, surfaceCol, meniscus * 0.9);

                // Glass: a rim that catches the wash at the edges and a specular streak down one side.
                float rim = 1.0 - smoothstep(0.0, radius * 0.24, -d);
                tube += _GlassColor.rgb * rim * _GlassStrength * (0.4 + 0.6 * cyl) * (0.5 + 0.5 * underwater);
                float sx = (xr + 0.45) / 0.16;              // squared by hand: pow() of a negative base is NaN on DX11
                float streak = exp(-sx * sx);
                tube += _GlassColor.rgb * streak * _GlassStrength * 0.45;

                float mask = 1.0 - smoothstep(0.0, 0.004, d);   // anti-aliased tube edge
                float3 col = lerp(bg, tube, mask * _Opacity);
                col *= _Intensity;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

// The visualizer's second 2D show: the CAVE's screens as one sheet of rainbow liquid that the music
// drops things into. Drawn on one quad per screen, a few millimetres in front of the water panels,
// see-through, so it lies over their dark wash alone or over the tubes when the two are layered.
//
// The C# side (RippleSurface) owns the drops: where each landed, how far its rings have spread, how
// high they still stand, and its colour. It hands each panel the drops' centres in that panel's own
// metres, unfolded across the corners of the room, so a ring dropped on the floor climbs the wall
// at the same speed it crossed the floor and never breaks at a seam. Everything else is here: the
// height field, its relief lit by a lamp inside the room (the 2.5D part), the glints on the flanks,
// and the colour, a rainbow gradient laid across the whole room that each ring walks through, crest
// by crest.
//
// Lit by nothing in the scene, and lit from a fixed lamp and a fixed eye rather than the camera, so
// every eye of every viewer sees the same picture: it is surface content, like the tubes.
Shader "VRMusic/RippleSurface"
{
    Properties
    {
        _PanelSize ("Panel Size (m)", Vector) = (3.556, 2.2225, 0, 0)
        _PanelCentre ("Panel Centre (room)", Vector) = (0, 1.11, 1.11, 0)
        _PanelRight ("Panel Right (room)", Vector) = (1, 0, 0, 0)
        _PanelUp ("Panel Up (room)", Vector) = (0, 1, 0, 0)
        _LightPos ("Lamp (room)", Vector) = (0, 2.6, -0.7, 0)
        _EyePos ("Eye (room)", Vector) = (0, 1.6, -0.3, 0)
        _HueField ("Hue Field (dir / span, turn)", Vector) = (0.14, 0.08, 0.06, 0)
        _Ambient ("Ambient (swell, lift, time)", Vector) = (0.25, 1, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 2)) = 1
        _Relief ("Relief", Range(0, 0.2)) = 0.05
        _Sheen ("Sheen", Range(0, 1)) = 0.06
        _RingGlow ("Ring Glow", Range(0, 3)) = 1
        _Glint ("Glint", Range(0, 3)) = 0.9
        _GlintPower ("Glint Power", Range(4, 256)) = 40
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.22
        _HueSpread ("Hue Step Per Crest", Range(0, 0.5)) = 0.18
        _HueByHeight ("Hue By Height", Range(0, 0.5)) = 0.08
        _Saturation ("Saturation", Range(0, 1)) = 0.9
    }

    SubShader
    {
        // Before the menu, the sign and the volume's shows, all of which stand in front of it.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        // Premultiplied: a crest adds light, a trough's shade darkens what is under it.
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "RippleSurface"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_DROPS 16
            #define TAU 6.28318530718

            CBUFFER_START(UnityPerMaterial)
                float4 _PanelSize;
                float4 _PanelCentre;
                float4 _PanelRight;
                float4 _PanelUp;
                float4 _LightPos;
                float4 _EyePos;
                float4 _HueField;
                float4 _Ambient;
                float _Opacity;
                float _Intensity;
                float _Relief;
                float _Sheen;
                float _RingGlow;
                float _Glint;
                float _GlintPower;
                float _BaseAlpha;
                float _HueSpread;
                float _HueByHeight;
                float _Saturation;
            CBUFFER_END

            // Per drop, from RippleSurface through a MaterialPropertyBlock.
            float _DropCount;
            float4 _DropPos[MAX_DROPS];     // xy: the centre on this panel, in its metres; zw: a second way round, or far away
            float4 _DropWave[MAX_DROPS];    // x: radius the front has reached, y: height now, z: wavelength, w: hue

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

            // A rainbow with soft edges between its hues, so it reads as a gradient, not as bands.
            float3 Rainbow(float hue, float saturation)
            {
                float3 rgb = saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                rgb = rgb * rgb * (3.0 - 2.0 * rgb);
                return lerp(float3(1.0, 1.0, 1.0), rgb, saturation);
            }

            // One long, slow wave of the swell, in room space so it runs on unbroken round the corners.
            void Swell(float3 p, float3 k, float w, float phase, float a, float3 right, float3 up, inout float h, inout float2 g)
            {
                float s, c;
                sincos(dot(k, p) - w * _Ambient.z + phase, s, c);
                h += a * s;
                g += a * c * float2(dot(k, right), dot(k, up));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 pm = (input.uv - 0.5) * _PanelSize.xy;          // metres across and up this panel
                float3 right = _PanelRight.xyz;
                float3 up = _PanelUp.xyz;
                float3 normal = cross(up, right);                       // into the room
                float3 p = _PanelCentre.xyz + right * pm.x + up * pm.y; // the same point in the room

                // The swell: the surface is never quite still. Four waves at odd angles, so they do
                // not lock into a grid.
                float h = 0.0;
                float rings = 0.0;                                      // the drops' part of the height
                float2 g = float2(0.0, 0.0);
                float swell = _Ambient.x;
                Swell(p, float3(5.1, 2.3, 3.4), 0.55, 0.0, swell * 0.40, right, up, h, g);
                Swell(p, float3(-3.6, 4.4, 5.6), 0.43, 1.7, swell * 0.30, right, up, h, g);
                Swell(p, float3(7.9, -2.6, -1.4), 0.71, 4.1, swell * 0.20, right, up, h, g);
                Swell(p, float3(-1.2, -6.8, 8.3), 0.37, 2.9, swell * 0.18, right, up, h, g);
                float swellHeight = h;

                // The drops. Each is a packet of rings: a sharp leading crest, and behind it a train
                // that grows longer as it spreads, the way a real drop's rings multiply.
                float2 hueVec = float2(0.0, 0.0);
                float weight = 0.0;
                int count = (int)_DropCount;

                [loop]
                for (int i = 0; i < MAX_DROPS; i++)
                {
                    if (i >= count)
                        break;

                    float4 pos = _DropPos[i];
                    float4 wave = _DropWave[i];

                    // The nearer of the two ways round, for a drop on the wall facing this one.
                    float2 d1 = pm - pos.xy;
                    float2 d2 = pm - pos.zw;
                    float r1 = dot(d1, d1);
                    float r2 = dot(d2, d2);
                    float2 d = r1 <= r2 ? d1 : d2;
                    float r = sqrt(min(r1, r2));

                    float lambda = wave.z;
                    float x = r - wave.x;                               // ahead of the front if positive
                    float trail = lambda * (1.0 + 1.5 * saturate(wave.x));  // a few rings, never the whole room
                    float q = x > 0.0 ? x / (0.35 * lambda) : x / trail;
                    if (q * q > 9.0)
                        continue;

                    float envelope = exp(-q * q);
                    float k = TAU / lambda;
                    float s, c;
                    sincos(k * x, s, c);

                    float amp = wave.y * envelope;
                    h += amp * c;                                       // a crest at the front
                    rings += amp * c;
                    g += (d / max(r, 1e-4)) * (-amp * k * s);

                    // Each crest behind the front is a step further round the wheel: rainbow rings.
                    float hue = wave.w - _HueSpread * x / lambda;
                    float2 hv;
                    sincos(hue * TAU, hv.y, hv.x);
                    hueVec += hv * amp;
                    weight += amp;
                }

                // The colour: the gradient across the room where the surface is quiet, the rings'
                // own colours where they are running, mixed on the colour wheel so two rings that
                // cross blend through the hues between them, and nudged by the height, so a crest and
                // its trough are never quite the same colour.
                float bgHue = dot(p, _HueField.xyz) + _HueField.w;
                float2 bg;
                sincos(bgHue * TAU, bg.y, bg.x);
                const float bgWeight = 0.5;
                float2 v = bg * bgWeight + hueVec;
                float agree = saturate(length(v) / (bgWeight + weight));
                float hue = atan2(v.y, v.x) / TAU + h * _HueByHeight;
                float3 rainbow = Rainbow(hue, _Saturation * lerp(0.55, 1.0, agree));

                // The relief: the height field's slope tilts the surface, a lamp inside the room lights
                // it, and the flanks that face the lamp catch a glint seen from a fixed eye.
                float3 n = normalize(float3(-g * _Relief, 1.0));        // x across, y up, z into the room
                float3 toLamp = normalize(_LightPos.xyz - p);
                float3 toEye = normalize(_EyePos.xyz - p);
                float3 l = float3(dot(toLamp, right), dot(toLamp, up), dot(toLamp, normal));
                float3 e = float3(dot(toEye, right), dot(toEye, up), dot(toEye, normal));
                float level = max(l.z, 0.2);                            // how a still surface is lit
                float shade = clamp(saturate(dot(n, l)) / level, 0.2, 2.2);
                float slope = saturate(length(g) * _Relief * 3.0);
                float glint = pow(saturate(dot(n, normalize(l + e))), _GlintPower) * slope;

                // How strongly rings are running here, and where in a ring this is: 1 on a crest, −1 in a
                // trough, whatever the ring's height, so the glow follows the crests and the troughs go dark.
                float activity = saturate(weight * 1.6);
                float phase = rings / max(weight, 1e-3);
                float crest = saturate(phase);
                float trough = saturate(-phase);

                float glow = _Sheen * _Ambient.y * (0.75 + 0.8 * saturate(swellHeight))
                           + _RingGlow * activity * (0.12 + 0.95 * crest * crest);
                float3 col = rainbow * glow * shade;
                col += glint * _Glint * lerp(rainbow, float3(1.0, 1.0, 1.0), 0.55) * (0.35 + activity);

                float alpha = saturate(_BaseAlpha + 0.75 * activity + 0.25 * trough * activity + 0.5 * glint);
                col *= _Intensity;

                return half4(col * _Opacity, alpha * _Opacity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

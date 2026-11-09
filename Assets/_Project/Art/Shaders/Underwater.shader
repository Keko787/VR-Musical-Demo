// The visualizer's underwater show: the room's screens as the sea round someone standing on the
// seabed. Drawn on one quad per screen, a few millimetres in front of the water panels, mostly opaque.
//
// The light is worked out from the point in the room rather than from the panel, so it is unbroken
// at the seams. The caustic net — the bright lines the waves overhead focus the sunlight into — is an
// animated cell pattern on a plane, carried down along the sun's slant: the floor cuts straight
// across it, a wall cuts nearly along it, which draws it out into shafts. The broad beams, the blue
// deepening downward, the sand and its ripples, and the plankton are worked out the same way. Every
// term is written in h (up a wall) and bed (out across the floor), both 0 where floor meets wall, so
// it is the same on both sides of that seam. The C# side (Underwater) moves the bubbles, the fish and
// the kelp, which live on the walls in u, a coordinate that runs round them from the open end of the
// left wall across the front to the open end of the right.
Shader "VRMusic/Underwater"
{
    Properties
    {
        _PanelSize ("Panel Size (m)", Vector) = (3.556, 2.2225, 0, 0)
        _PanelCentre ("Panel Centre (room)", Vector) = (0, 1.11, 1.11, 0)
        _PanelRight ("Panel Right (room)", Vector) = (1, 0, 0, 0)
        _PanelUp ("Panel Up (room)", Vector) = (0, 1, 0, 0)
        _Room ("Room (half width, front, height)", Vector) = (1.778, 1.111, 2.2225, 0)
        _Sun ("Sun Slant (x, z per metre down)", Vector) = (0.16, 0.1, 0, 0)
        _Phase ("Phase (current, plankton, kelp, time)", Vector) = (0, 0, 0, 0)
        _Music ("Music (light, glitter, sway, lean)", Vector) = (1, 0, 0.05, 0)
        _Shallow ("Water High", Color) = (0.07, 0.38, 0.46, 1)
        _Deep ("Water Deep", Color) = (0, 0.07, 0.17, 1)
        _LightColour ("Sunlight", Color) = (0.75, 1, 0.95, 1)
        _Sand ("Sand", Color) = (0.62, 0.56, 0.42, 1)
        _KelpColour ("Kelp", Color) = (0.3, 0.34, 0.08, 1)
        _Plankton ("Plankton Glow", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 2)) = 1
        _Caustics ("Caustics", Range(0, 3)) = 1
        _CausticCell ("Caustic Cell (m)", Range(0.1, 1)) = 0.34
        _CausticLine ("Caustic Line Width (cells)", Range(0.005, 0.2)) = 0.04
        _WallSoftness ("Caustic Softening Up The Walls", Range(1, 8)) = 3.5
        _Beams ("Light Beams", Range(0, 2)) = 0.6
        _Surface ("Surface Shimmer", Range(0, 2)) = 0.8
        _Snow ("Plankton", Range(0, 2)) = 0.7
        _SandRipples ("Sand Ripples", Range(0, 0.5)) = 0.14
        _Wobble ("Refraction Wobble (m)", Range(0, 0.1)) = 0.025
        _BedFade ("Seabed Fade (m)", Range(0.05, 1.5)) = 0.45
        _WaterAlpha ("Water Alpha", Range(0, 1)) = 0.92
    }

    SubShader
    {
        // Under the lava lamp and the ripples, so either can be layered over the sea.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-70" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Underwater"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_BUBBLES 64
            #define MAX_FISH 32
            #define MAX_KELP 12

            // The plankton sink by an offset that Underwater wraps every 30 m (k_SnowWrap); each layer's
            // rows repeat over the same distance — 30 m in 6 cm rows, and 1.6 × 30 m in 10 cm rows.
            #define SNOW_ROWS_NEAR 500.0
            #define SNOW_ROWS_FAR 480.0

            CBUFFER_START(UnityPerMaterial)
                float4 _PanelSize;
                float4 _PanelCentre;
                float4 _PanelRight;
                float4 _PanelUp;
                float4 _Room;
                float4 _Sun;
                float4 _Phase;
                float4 _Music;
                float4 _Shallow;
                float4 _Deep;
                float4 _LightColour;
                float4 _Sand;
                float4 _KelpColour;
                float _Plankton;
                float _Opacity;
                float _Intensity;
                float _Caustics;
                float _CausticCell;
                float _CausticLine;
                float _WallSoftness;
                float _Beams;
                float _Surface;
                float _Snow;
                float _SandRipples;
                float _Wobble;
                float _BedFade;
                float _WaterAlpha;
            CBUFFER_END

            // From Underwater through a MaterialPropertyBlock, in wall coordinates (u round the walls, y up).
            float _BubbleCount;
            float4 _Bubbles[MAX_BUBBLES];   // xy: the centre, z: radius, w: opacity
            float _FishCount;
            float4 _Fish[MAX_FISH];         // xy: the middle, zw: the way it faces, shortened as it turns end on
            float4 _FishInfo[MAX_FISH];     // x: length, y: the tail's swing −1…1, z: silver flash 0…1, w: haze 0…1
            float _KelpCount;
            float4 _Kelp[MAX_KELP];         // x: the root, y: height, z: phase, w: the stalk's half width

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float3 a = frac(p.xyx * float3(123.34, 234.34, 345.65));
                a += dot(a, a + 34.45);
                return frac(float2(a.x * a.y, a.y * a.z));
            }

            float Noise(float2 x)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                float2 w = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, w.x), lerp(c, d, w.x), w.y);
            }

            // How far q is from the nearest border of an animated cell pattern, in cells: the caustic
            // lines are where it is small. Each cell's point circles about the middle of its cell.
            float CellBorder(float2 q, float phase)
            {
                float2 cell = floor(q);
                float2 f = frac(q);
                float d1 = 8.0;
                float d2 = 8.0;

                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 o = float2(i, j);
                        float2 h = Hash22(cell + o);
                        float2 at = o + 0.5 + 0.4 * sin(phase * (0.6 + 0.8 * h) + 6.2832 * h);
                        float2 to = at - f;
                        float d = dot(to, to);
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }
                }
                return sqrt(d2) - sqrt(d1);
            }

            // A caustic line: bright on the border and falling away either side, a little wider in red than
            // in blue, the way real ones split the light. Past a pixel it is widened, never thinned, and
            // keeps its brightness overall, so it does not shimmer far off.
            float3 Lines(float e, float width, float fw)
            {
                float3 drawn = max(width * float3(1.2, 1.0, 0.85), fw);
                return width * drawn / (drawn * drawn + e * e);
            }

            // One layer of plankton: a speck in some of the cells of a grid, each twinkling. The grid's rows
            // repeat every periodRows, so the sinking offset can wrap without the specks jumping.
            float Plankton(float2 s, float cellSize, float periodRows, float density, float radius, float px, float t, float glitter)
            {
                float2 g = s / cellSize;
                float2 cell = floor(g);
                cell.y -= periodRows * floor(cell.y / periodRows);
                float2 h = Hash22(cell + 17.0);
                float2 at = 0.2 + 0.6 * Hash22(cell + 41.3);
                float d = length(frac(g) - at) * cellSize;
                float speck = saturate((radius + 0.7 * px - d) / (1.4 * px));
                float twinkle = 0.55 + 0.45 * sin(t * (1.5 + 4.0 * h.y) + h.x * 40.0);
                return step(h.x, density) * speck * lerp(0.35, 1.6 * twinkle, glitter);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 pm = (input.uv - 0.5) * _PanelSize.xy;
                float3 right = _PanelRight.xyz;
                float3 up = _PanelUp.xyz;
                float3 normal = cross(up, right);
                float3 p = _PanelCentre.xyz + right * pm.x + up * pm.y;
                float t = _Phase.w;
                float height = _Room.z;
                float px = max(length(fwidth(pm)), 1e-4) * 0.7;      // about a pixel, in metres

                // The seabed or a wall. Round the walls, u is metres from the middle of the front wall:
                // out along the left wall to its open end one way, along the right the other.
                bool seabed = normal.y > 0.7;
                float u = p.x;
                if (normal.x > 0.5)
                    u = -_Room.x - (_Room.y - p.z);
                else if (normal.x < -0.5)
                    u = _Room.x + (_Room.y - p.z);

                // h: up the walls, 0 at their foot. bed: out across the seabed, 0 at the foot of each wall
                // (the back is open) and on the walls.
                float h = seabed ? 0.0 : saturate(p.y / height);
                float toWall = min(min(p.x + _Room.x, _Room.x - p.x), _Room.y - p.z);
                float bed = seabed ? 1.0 - exp(-max(toWall, 0.0) / _BedFade) : 0.0;

                // The water's slow wobble bends what is seen through it a little.
                float3 wobble = _Wobble * float3(
                    sin(p.y * 3.1 + p.z * 2.3 + t * 0.7) + 0.5 * sin(p.z * 7.3 - p.x * 5.1 + t * 1.3),
                    0.0,
                    sin(p.x * 2.7 - p.y * 3.4 + t * 0.6) + 0.5 * sin(p.x * 6.9 + p.y * 5.7 - t * 1.1));
                float3 pw = p + wobble;

                // The caustics: two animated cell patterns on the plane under the waves, carried down along
                // the sun's slant and wandering with the current. Sharp on the seabed, softer up the walls.
                // Each pattern is warped a little first, so the borders curve the way real ones do instead of
                // running straight from corner to corner.
                float phase = _Phase.x;
                float2 under = pw.xz - _Sun.xy * pw.y + 0.6 * float2(sin(phase * 0.021), sin(phase * 0.017 + 1.0));
                float2 q1 = under / _CausticCell;
                q1 += 0.28 * float2(sin(q1.y * 1.9 + phase * 0.31), sin(q1.x * 2.3 - phase * 0.27));
                float e1 = CellBorder(q1, phase);
                float2 turned = float2(under.x * 0.8 - under.y * 0.6, under.x * 0.6 + under.y * 0.8);
                float2 q2 = turned / (_CausticCell * 0.62) + 17.3;
                q2 += 0.28 * float2(sin(q2.y * 2.1 - phase * 0.23), sin(q2.x * 1.7 + phase * 0.35));
                float e2 = CellBorder(q2, phase * 1.3 + 3.0);
                float width = _CausticLine * lerp(1.0, _WallSoftness, h);
                float3 c1 = Lines(e1, width, fwidth(e1));
                float3 c2 = Lines(e2, width * 1.1, fwidth(e2));
                float3 caustic = 0.5 * c1 + 0.4 * c2 + 1.3 * c1 * c2;

                // Broad beams down through the water, slanting with the sun and swaying slowly: strongest high
                // on the walls, and where they reach the seabed, brighter patches.
                float2 beamAt = under * 1.3 + float2(sin(phase * 0.05 + 1.3), cos(phase * 0.043)) * 0.35;
                float beam = smoothstep(0.4, 0.85, 0.65 * Noise(beamAt) + 0.35 * Noise(beamAt * 2.3 + 5.1));

                // The water, blue deepening downward; the seabed, sand in ripples fading into the blue toward the walls.
                float3 sunlight = _LightColour.rgb * _Music.x;
                float3 water = lerp(_Deep.rgb, _Shallow.rgb, pow(h, 0.85));
                float ripple = sin(dot(p.xz, float2(0.8, 0.6)) * 21.0 + 1.3 * sin(dot(p.xz, float2(-0.5, 0.86)) * 6.0));
                float3 sand = _Sand.rgb * 0.38 * (1.0 + _SandRipples * ripple);
                float3 col = lerp(water, sand, bed);

                float causticGain = lerp(lerp(0.3, 0.13, h), 1.0, bed);
                float beamGain = lerp(0.12 + 0.3 * h, 0.2, bed);
                float3 tone = lerp(float3(1.0, 1.0, 1.0), _Sand.rgb * 1.5, bed);
                float3 light = sunlight * (caustic * (_Caustics * causticGain * (1.0 + 0.25 * ripple * bed)) * tone + beam * (_Beams * beamGain));
                col += light;
                float lit = dot(light, float3(0.3, 0.5, 0.2));          // how brightly lit this spot is, for what stands in it

                if (!seabed)
                {
                    float y = p.y;

                    // Fish, farthest first: dark shapes against the blue, hazier the farther, silver as they turn.
                    int fishCount = (int)_FishCount;
                    [loop]
                    for (int i = 0; i < MAX_FISH; i++)
                    {
                        if (i >= fishCount)
                            break;

                        float4 fish = _Fish[i];
                        float4 info = _FishInfo[i];
                        float size = info.x;
                        float2 d = float2(u - fish.x, y - fish.y);
                        if (dot(d, d) > 0.36 * size * size)
                            continue;

                        float shown = max(length(fish.zw), 0.05);
                        float2 dir = fish.zw / shown;
                        float lx = dot(d, dir) / shown;
                        float ly = d.y * dir.x - d.x * dir.y;
                        float rear = saturate(-lx / (0.5 * size));
                        ly -= info.y * 0.09 * size * rear * rear;

                        // A body deepest a third of the way back from a rounded nose, narrowing to the root of
                        // the tail, and a forked tail behind.
                        float fromNose = (0.42 * size - lx) / (0.75 * size);
                        float halfHeight = 0.13 * size * sin(3.14159 * pow(saturate(fromNose), 0.6));
                        float body = saturate((halfHeight - abs(ly)) / px + 0.5) * saturate(fromNose * 0.75 * size / px + 0.5);
                        float back = -lx - 0.33 * size;
                        float outer = 0.025 * size + 0.9 * back;
                        float notch = 1.4 * (back - 0.08 * size);
                        float tail = saturate((outer - abs(ly)) / px + 0.5) * saturate((abs(ly) - notch) / px + 0.5)
                                   * saturate(back / px + 0.5) * saturate((0.17 * size - back) / px + 0.5);
                        float shape = max(body, tail);
                        if (shape <= 0.0)
                            continue;

                        float belly = saturate(-d.y / (0.16 * size));
                        float3 fishColour = lerp(_Deep.rgb * 0.3 + _Shallow.rgb * 0.12 * belly, col, info.w)
                                          + sunlight * (info.z * 0.55 * (1.0 - 0.5 * info.w));
                        col = lerp(col, fishColour, shape);
                    }

                    // Kelp: tall stalks from the foot of the walls, swaying with the current and leaning over with
                    // the bass, long ribbon blades streaming up off them, each with a little float at its root.
                    // Lit through, the way kelp glows where the sun comes down on it.
                    int kelpCount = (int)_KelpCount;
                    float sway = _Music.z;
                    float lean = _Music.w;
                    [loop]
                    for (int k = 0; k < MAX_KELP; k++)
                    {
                        if (k >= kelpCount)
                            break;

                        float4 kelp = _Kelp[k];
                        if (abs(u - kelp.x) > 0.6 || y > kelp.y + 0.3)
                            continue;

                        float up01 = saturate(y / kelp.y);
                        float bend = (sway * sin(_Phase.z + kelp.z + y * 1.7) + lean) * up01 * up01;
                        float stalkHalf = kelp.w * (1.0 - 0.5 * up01);
                        float stalk = saturate((stalkHalf - abs(u - kelp.x - bend)) / px + 0.5) * saturate((kelp.y - y) / px + 0.5);

                        // A blade every 13 cm, alternating sides, streaming up and out and curling at the tip;
                        // a point can only be on the blade of its own span or of the two below.
                        float blades = 0.0;
                        float j0 = floor(y / 0.13);
                        [unroll]
                        for (int b = 0; b < 3; b++)
                        {
                            float j = j0 - b;
                            float ya = (j + 0.5) * 0.13;
                            if (ya < 0.1 || ya > kelp.y - 0.04)
                                continue;

                            float sa = ya / kelp.y;
                            float swing = sway * sin(_Phase.z + kelp.z + ya * 1.7);
                            float ax = kelp.x + (swing + lean) * sa * sa;
                            float slope = sway * 1.7 * cos(_Phase.z + kelp.z + ya * 1.7) * sa * sa + 2.0 * (swing + lean) * sa / kelp.y;
                            float side = frac(j * 0.5) < 0.25 ? 1.0 : -1.0;
                            float angle = side * (0.34 + 0.12 * sin(_Phase.z * 1.3 + kelp.z + j)) + slope;
                            float2 along = float2(sin(angle), cos(angle));
                            float2 d = float2(u - ax, y - ya);
                            float bladeLength = lerp(0.3, 0.17, sa) * (0.85 + 0.3 * Hash21(float2(j, kelp.z)));
                            float x = dot(d, along) / bladeLength;
                            float curl = (0.05 * sin(_Phase.z * 1.7 + kelp.z + j * 2.1) + 0.5 * lean) * x * x;
                            float across = abs(d.x * along.y - d.y * along.x - curl);
                            float bladeHalf = 0.017 * pow(sin(3.14159 * saturate(x)), 0.6) * (1.0 - 0.35 * saturate(x))
                                            * (1.0 + 0.12 * sin(x * 38.0 + _Phase.z * 2.0));
                            float blade = saturate((bladeHalf - across) / px + 0.5) * step(0.0, x) * step(x, 1.0);
                            float2 bulb = d - along * 0.022;
                            float bladder = saturate((0.011 - length(bulb)) / px + 0.5);
                            blades = max(blades, max(blade, bladder));
                        }

                        float shape = max(stalk, blades);
                        float3 kelpColour = _KelpColour.rgb * (0.5 + 2.2 * lit) * (0.75 + 0.35 * up01);
                        col = lerp(col, kelpColour, shape * 0.92);
                    }

                    // Bubbles: a bright rim, a highlight up toward the light and a faint glint underneath.
                    int bubbleCount = (int)_BubbleCount;
                    float glow = 0.0;
                    float cover = 0.0;
                    [loop]
                    for (int n = 0; n < MAX_BUBBLES; n++)
                    {
                        if (n >= bubbleCount)
                            break;

                        float4 bubble = _Bubbles[n];
                        float r = bubble.z;
                        float2 d = float2(u - bubble.x, y - bubble.y);
                        if (abs(d.x) > r * 1.3 + px || abs(d.y) > r * 1.3 + px)
                            continue;

                        float squash = sin(t * 11.0 + n * 1.7) * saturate((r - 0.01) / 0.02) * 0.12;
                        float2 q = d / (r * float2(1.0 + squash, 1.0 - squash));
                        float rho = length(q);
                        float inside = saturate((1.0 - rho) * r / px + 0.5);
                        float rim = inside * smoothstep(0.55, 1.0, rho);
                        float spot = smoothstep(0.34, 0.08, length(q - float2(-0.34, 0.38)));
                        float glint = smoothstep(0.3, 0.05, length(q - float2(0.28, -0.52))) * 0.35;
                        glow += (rim * 0.9 + inside * 0.12 + spot * 1.1 + glint) * bubble.w;
                        cover = max(cover, inside * bubble.w);
                    }
                    float3 bubbleColour = lerp(sunlight, float3(1.0, 1.0, 1.0), 0.4) * (0.45 + 0.55 * h);
                    col = col * (1.0 - 0.2 * cover) + bubbleColour * glow;

                    // The underside of the surface, just above the walls: a bright, wavering band that glitters with the highs.
                    // Its glints are where the two caustic patterns cross.
                    float edge = height - 0.12 + 0.03 * sin(u * 5.3 + t * 0.9) + 0.018 * sin(u * 12.7 - t * 1.7);
                    float band = smoothstep(edge - 0.035, edge + 0.025, y);
                    float glints = c1.g * c2.g * 6.0;
                    col += (_Shallow.rgb * 0.7 + sunlight * (0.25 + 0.7 * c1.g + glints * (0.3 + 1.2 * _Music.y))) * (band * _Surface);
                }

                // Plankton everywhere, nearest of all: sinking down the walls, drifting over the seabed, in two
                // layers at different paces. At night it glows by itself, in drifting patches.
                float2 drift = 0.25 * float2(sin(phase * 0.031), cos(phase * 0.023));
                float2 s1 = seabed ? p.xz + drift : float2(u, p.y + _Phase.y) + drift;
                float2 s2 = seabed ? p.xz + drift * 1.6 + 3.3 : float2(u + 3.3, p.y + 1.6 * _Phase.y) + drift * 1.6;
                float density = lerp(0.2, 0.32, _Plankton);
                float specks = Plankton(s1, 0.06, SNOW_ROWS_NEAR, density, 0.0022, px, t, _Music.y)
                             + 0.7 * Plankton(s2, 0.1, SNOW_ROWS_FAR, density, 0.0035, px, t + 7.0, _Music.y);
                float patches = smoothstep(0.4, 0.8, Noise(float2(u, p.y + p.z) * 1.4 + float2(t * 0.05, -t * 0.03)));
                float3 plankton = lerp(sunlight * 0.5, float3(0.35, 1.0, 0.95) * 1.4, _Plankton);
                float planktonGain = lerp(0.5 + 0.5 * h, 0.08 + 2.7 * patches, _Plankton);
                col += plankton * (specks * _Snow * planktonGain * (seabed ? 0.6 : 1.0));

                col *= _Intensity;
                return half4(col * _Opacity, _WaterAlpha * _Opacity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

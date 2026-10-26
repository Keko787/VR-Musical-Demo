// Solid things in the outside worlds — the canyon floor and its rocks, mountains, trees, cumulus
// towers, the voxel cubes — painted like an anime background: cel-lit by the world's sun with
// violet shadows, hazed with distance, and coloured by where they are rather than by a texture.
//
// Colour comes from world height and slope: Strata lays horizontal bands of rock colour up a cliff
// (with a cap colour on anything flat), Biome paints Norway's ground by height above the water and
// slope (a shore, fields, forest, heath above the tree line, bare rock), Snow whitens high, flat-ish
// ground. Vertex colour tints
// that, and alpha below 1 lets it take over — a vertex (or a particle) with alpha 0 is its own colour
// and glows with it, which is how a voxel cube that has broken away turns from rock into light.
// (Below 1, not above 0: a mesh with no colours reads as opaque white, and must stay painted.)
//
// The music reaches the land three ways: a seam of light at the top of each stratum that follows
// one band (bass at the bottom of the cliff, treble at the top), a glow per vertex keyed to a band
// through the second UV channel (a cumulus tower lit from inside), and the beat rings running across
// the ground. A material with Billow also swells each puff of a cloud with that same band, by a share
// of the puff's radius, which the cloud carries in its third UV channel.
Shader "VRMusic/SimLand"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _CelSoft ("Cel Softness", Range(0.01, 1)) = 0.1

        [Header(Strata)]
        _Strata ("Strata", Range(0, 1)) = 0
        _StrataScale ("Stratum Height (m)", Float) = 14
        _StrataA ("Stratum A", Color) = (0.62, 0.27, 0.15, 1)
        _StrataB ("Stratum B", Color) = (0.48, 0.18, 0.12, 1)
        _StrataC ("Stratum C", Color) = (0.82, 0.6, 0.43, 1)
        _StrataD ("Stratum D", Color) = (0.72, 0.38, 0.25, 1)
        _TopColor ("Cap Colour", Color) = (0.78, 0.55, 0.4, 1)
        _TopAmount ("Cap Amount", Range(0, 1)) = 1

        [Header(Biome)]
        _Biome ("Biome", Range(0, 1)) = 0
        _WaterLevel ("Water Level (m)", Float) = 0
        _ShoreColor ("Shore (alpha amount)", Color) = (0.62, 0.6, 0.55, 1)
        _ShoreWidth ("Shore Height (m)", Float) = 4
        _FieldColor ("Fields (alpha amount)", Color) = (0.45, 0.68, 0.22, 1)
        _FieldLine ("Fields Up To (m above water)", Float) = 150
        _ForestColor ("Forest", Color) = (0.12, 0.27, 0.12, 1)
        _TreeLine ("Tree Line (m above water)", Float) = 600
        _HeathColor ("Heath", Color) = (0.42, 0.48, 0.24, 1)
        _RockColor ("Rock", Color) = (0.45, 0.45, 0.46, 1)
        _RockPatches ("Rock Through The Heath", Range(0, 2)) = 0.6
        _Surf ("Surf", Range(0, 1)) = 0
        _SurfHeight ("Surf Height (m)", Float) = 2.5
        _CloudShade ("Cloud Shadows", Range(0, 1)) = 0

        [Header(Snow)]
        _Snow ("Snow", Range(0, 1)) = 0
        _SnowLine ("Snow Line (m)", Float) = 250
        _SnowColor ("Snow Colour", Color) = (0.9, 0.94, 1, 1)
        _SnowGlint ("Snow Glints With The Treble", Range(0, 4)) = 0

        [Header(Detail)]
        _Detail ("Detail", Range(0, 1)) = 0.3
        _DetailScale ("Detail Scale (m)", Float) = 40

        [Header(Music)]
        [HDR] _GlowColor ("Glow Colour", Color) = (1, 0.75, 0.4, 1)
        _StrataGlow ("Strata Seam Glow", Range(0, 4)) = 0
        _StrataBands ("Strata Per Band", Float) = 1
        _BandGlow ("Band Glow (UV2)", Range(0, 4)) = 0
        _Billow ("Billow With The Band (share of a puff's radius)", Range(0, 0.5)) = 0
        [HDR] _EmissionColor ("Vertex Colour Glow", Color) = (1, 1, 1, 1)
        _Rim ("Rim Light Toward The Sun", Range(0, 2)) = 0.25
        _Rings ("Beat Rings", Range(0, 4)) = 0

        [Header(Shore)]
        _WetDarken ("Wet Sand Darkening", Range(0, 1)) = 0
        _WetLevel ("Wet Line Height (m)", Float) = 0
        _WetBand ("Wet Band (m)", Float) = 0.4

        [Header(Sway)]
        _Sway ("Sway (m at full weight)", Float) = 0
        _SwayWind ("Wind Direction", Vector) = (0, 0, -1, 0)
        _SwaySource ("Sway With (0 mids, 1 bass)", Range(0, 1)) = 0

        [Header(Paint)]
        _PaletteSlot ("World Palette Slot (-1 off)", Float) = -1

        _FogScale ("Fog", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite On

        Pass
        {
            Name "SimLand"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _CelSoft;
                float _Strata;
                float _StrataScale;
                float4 _StrataA;
                float4 _StrataB;
                float4 _StrataC;
                float4 _StrataD;
                float4 _TopColor;
                float _TopAmount;
                float _Biome;
                float _WaterLevel;
                float4 _ShoreColor;
                float _ShoreWidth;
                float4 _FieldColor;
                float _FieldLine;
                float4 _ForestColor;
                float _TreeLine;
                float4 _HeathColor;
                float4 _RockColor;
                float _RockPatches;
                float _Surf;
                float _SurfHeight;
                float _CloudShade;
                float _Snow;
                float _SnowLine;
                float4 _SnowColor;
                float _SnowGlint;
                float _Detail;
                float _DetailScale;
                float4 _GlowColor;
                float _StrataGlow;
                float _StrataBands;
                float _BandGlow;
                float _Billow;
                float4 _EmissionColor;
                float _Rings;
                float _Rim;
                float _WetDarken;
                float _WetLevel;
                float _WetBand;
                float _Sway;
                float4 _SwayWind;
                float _SwaySource;
                float _PaletteSlot;
                float _FogScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv2 : TEXCOORD1;
                float2 uv3 : TEXCOORD2;     // sway: weight, phase; or a puff's radius. Read only when the material sways or billows.
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float2 band : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                // A cloud's puffs swelling with the band that lights them, each by a share of its own
                // radius, so a small bulge grows no more than a big puff does.
                if (_Billow > 0.001 && input.uv2.y > 0.001)
                {
                    float level = GR_Band(input.uv2.x);
                    output.positionWS += output.normalWS * (input.uv3.x * _Billow * level * level * input.uv2.y);
                }

                // Fronds and the like swaying in the wind, harder with the mids. Weight 0 is rooted.
                if (_Sway > 0.001)
                {
                    float weight = input.uv3.x;
                    float phase = input.uv3.y;
                    float3 wind = normalize(float3(_SwayWind.x, 0.0, _SwayWind.z) + 1e-4);
                    float3 across = float3(-wind.z, 0.0, wind.x);
                    float gust = 0.35 + 1.3 * lerp(_GR_Music.y, _GR_Music.x, _SwaySource);
                    float along = sin(_GR_Clock * 1.7 + phase + weight * 1.3) * 0.6 + 0.4;
                    float side = sin(_GR_Clock * 2.9 + phase * 1.7 + weight * 2.1);
                    output.positionWS += (wind * along + across * side * 0.35) * (_Sway * gust * weight);
                }

                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.band = input.uv2;
                return output;
            }

            float3 StratumColour(float index)
            {
                float r = GR_Hash11(index * 1.37 + 0.5);
                return r < 0.3 ? _StrataA.rgb : r < 0.55 ? _StrataB.rgb : r < 0.8 ? _StrataC.rgb : _StrataD.rgb;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                N = IS_FRONT_VFACE(face, N, -N);

                float3 p = input.positionWS - _GR_Origin.xyz;
                float flat = smoothstep(0.72, 0.92, N.y);

                float3 albedo = _BaseColor.rgb;

                // Painted from the world variant's palette, so a glitch to another variant repaints it.
                if (_PaletteSlot > -0.5)
                    albedo *= _GR_Palette[clamp((int)(_PaletteSlot + 0.5), 0, 3)].rgb;
                float3 emission = 0.0;

                // Norway, by height above the water and slope: forest up to a ragged tree line and heath
                // above it, a patchwork of fields on the level ground low down, bare rock wherever it is
                // steep and showing through the heath, a strip of shingle along the water.
                if (_Biome > 0.001)
                {
                    float above = p.y - _WaterLevel;
                    float n = GR_Noise(p.xz * 0.004);
                    float mottle = 0.82 + 0.36 * GR_Fbm(p.xz * 0.03);
                    float treeLine = _TreeLine + (n - 0.5) * 180.0 + (N.y - 0.8) * 120.0;
                    float3 ground = lerp(_ForestColor.rgb, _HeathColor.rgb, smoothstep(treeLine - 50.0, treeLine + 50.0, above)) * mottle;

                    // Fields: level ground below the field line, cut into strips each its own green (one in
                    // six cut for hay).
                    float level = smoothstep(0.94, 0.985, N.y) * (1.0 - smoothstep(_FieldLine - 25.0, _FieldLine + 25.0, above));
                    float2 strip = float2(p.x * 0.8 + p.z * 0.6, p.z * 0.8 - p.x * 0.6) / float2(55.0, 22.0);
                    float pick = GR_Hash12(floor(strip + GR_Noise(p.xz * 0.01) * 0.6));
                    float3 field = _FieldColor.rgb * (0.82 + 0.36 * pick);
                    field = lerp(field, field * float3(1.35, 1.2, 0.55), step(0.83, pick));
                    ground = lerp(ground, field, level * step(0.25, GR_Noise(p.xz * 0.006 + 4.0)) * _FieldColor.a);

                    float steep = 1.0 - smoothstep(0.36, 0.62, N.y);       // forest clings to the fjord walls; only the cliffs are bare
                    float outcrop = smoothstep(0.55, 0.8, GR_Fbm(p.xz * 0.012 + 7.0)) * _RockPatches * smoothstep(-80.0, 80.0, above - _TreeLine);
                    float3 rock = _RockColor.rgb * (0.8 + 0.35 * GR_Noise(float2(p.y * 0.04, (p.x + p.z) * 0.012)));
                    ground = lerp(ground, rock, saturate(steep + outcrop));

                    float shore = 1.0 - smoothstep(_ShoreWidth * 0.5, _ShoreWidth, above + (GR_Noise(p.xz * 0.05) - 0.5) * _ShoreWidth);
                    ground = lerp(ground, _ShoreColor.rgb * mottle, shore * _ShoreColor.a);

                    albedo *= lerp(1.0, ground, _Biome);
                }

                if (_Strata > 0.001)
                {
                    float wobble = (GR_Noise(p.xz * 0.008) - 0.5) * 0.7 + (GR_Noise(p.xz * 0.05 + 3.3) - 0.5) * 0.15;
                    float h = p.y / max(_StrataScale, 0.1) + wobble;
                    float index = floor(h);
                    float f = frac(h);

                    float3 rock = StratumColour(index);
                    rock *= 0.9 + 0.1 * GR_Noise(float2(p.y * 1.7, (p.x + p.z) * 0.04));                 // fine bedding
                    rock *= 1.0 - 0.18 * (1.0 - flat) * GR_Noise(float2((p.x - p.z) * 0.12, p.y * 0.015)); // weathering streaks
                    float3 cap = _TopColor.rgb * (0.8 + 0.3 * GR_Fbm(p.xz * 0.012) + 0.1 * GR_Noise(p.xz * 0.08));
                    albedo *= lerp(rock, cap, flat * _TopAmount) * _Strata + (1.0 - _Strata);

                    // A seam of light along the top of each stratum, its brightness one band's level.
                    float seam = smoothstep(0.9, 0.985, f) * (1.0 - flat);
                    float level = GR_BandIndex(index * _StrataBands);
                    emission += _GlowColor.rgb * seam * level * level * _StrataGlow;
                }

                if (_Snow > 0.001)
                {
                    float snowLine = _SnowLine + (GR_Noise(p.xz * 0.006) - 0.5) * 140.0;
                    float snow = smoothstep(snowLine - 25.0, snowLine + 25.0, p.y) * smoothstep(0.3, 0.6, N.y);
                    albedo = lerp(albedo, _SnowColor.rgb, snow * _Snow);

                    // Glints in the snow, a scatter of crystals catching the sun as the hi-hats play.
                    if (_SnowGlint > 0.001)
                    {
                        float2 cell = floor(p.xz * 1.5 + p.y * 0.3);
                        float h = GR_Hash12(cell);
                        float twinkle = saturate(sin(_GR_Clock * (3.0 + 6.0 * h) + h * 40.0) * 2.0 - 1.0);
                        emission += _SnowColor.rgb * step(0.94, h) * twinkle * snow * _SnowGlint * _GR_Music.z * _GR_Music.z;
                    }
                }

                if (_Detail > 0.001)
                    albedo *= 1.0 - _Detail * 0.5 + _Detail * GR_Noise(p.xz / max(_DetailScale, 0.1) + p.y * 0.02);

                // Sand the surf reaches is darker: a ragged band just above the waterline.
                if (_WetDarken > 0.001)
                {
                    float reach = _WetLevel + _WetBand * (0.7 + 0.6 * GR_Noise(p.xz * 0.15));
                    albedo *= 1.0 - _WetDarken * (1.0 - smoothstep(reach - 0.1, reach, p.y));
                }

                // Vertex colour tints; below full alpha it takes over — the colour becomes its own, and glows.
                float own = 1.0 - input.color.a;
                albedo = lerp(albedo * input.color.rgb, input.color.rgb, own);
                emission += _EmissionColor.rgb * input.color.rgb * own;

                // A band of the music lighting this vertex from inside (UV2: band position, weight).
                if (input.band.y > 0.001)
                {
                    float level = GR_Band(input.band.x);
                    emission += _GlowColor.rgb * level * level * input.band.y * _BandGlow;
                }

                float3 light = GR_Light(N, _CelSoft);

                // The shadows of the clouds overhead, sliding over the land as the deck drifts.
                if (_CloudShade > 0.001)
                    light = lerp(light, lerp(_GR_ShadowColor.rgb, light, 0.4), GR_CloudShadow(input.positionWS) * _CloudShade);

                // Surf: a ragged white line at the water's edge, thrown higher up the shore by each beat.
                if (_Surf > 0.001)
                {
                    float reach = _SurfHeight * (0.6 + 1.6 * _GR_Beat.x + 0.6 * _GR_Music.x);
                    float above = p.y - _WaterLevel + (GR_Noise(p.xz * 0.08 + _GR_Clock * 0.4) - 0.5) * reach * 0.6;
                    float foam = (1.0 - smoothstep(reach * 0.35, reach, above)) * step(-1.0, above);
                    albedo = lerp(albedo, float3(0.95, 0.97, 1.0), foam * _Surf);
                }

                float3 col = albedo * light;

                // A painted rim where an edge turns away from the eye toward the sun: backlit rock and
                // cloud get a bright outline instead of going flat.
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);
                float rim = pow(1.0 - saturate(dot(N, V)), 3.0) * saturate(dot(-V, _GR_SunDir.xyz) * 0.7 + 0.3);
                col += _GR_SunColor.rgb * albedo * rim * _Rim;

                col += (emission + _GR_RingColor.rgb * GR_Rings(input.positionWS) * _Rings) * _GR_Intensity;
                col = GR_ApplyFog(col, input.positionWS, _FogScale);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

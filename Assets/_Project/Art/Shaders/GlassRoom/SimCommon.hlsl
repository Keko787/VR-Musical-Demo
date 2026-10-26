// What every surface of the glass room's outside worlds shares: the music and the sky, set once a
// frame as shader globals by Simulation (the music) and the active SimWorld (the sky), and the
// noise the worlds are painted with. Include after URP's Core.hlsl.
//
// Everything outside is drawn by its own shaders rather than URP's lit ones. The worlds are seen
// only through the window cameras, a long way from the room; lighting them from the scene's lights
// would tie the outside's look to the room's lamps, and a painted, cel-shaded look is closer to the
// film the room is drawn from than physically based shading is.
#ifndef VRMUSIC_SIM_COMMON_INCLUDED
#define VRMUSIC_SIM_COMMON_INCLUDED

#define GR_MAX_BANDS 32
#define GR_PI 3.14159265

// ------------------------------------------------------------------ the music (Simulation)

float _GR_Bands[GR_MAX_BANDS];  // smoothed band levels, 0–1, low to high
float _GR_BandCount;
float4 _GR_Music;               // bass, mid, high, loudness
float4 _GR_Beat;                // pulse, strength, seconds since the last beat, idle
float _GR_Intensity;            // 1, or the low-intensity scale
float _GR_Clock;                // seconds, the director's clock
float4 _GR_Origin;              // the outside's origin in world space: the glass room's floor centre

// Beat rings: up to four shockwaves running out across the ground from one point.
float4 _GR_RingOrigin;          // xyz world
float4 _GR_RingTimes;           // clock time each ring set off
float4 _GR_RingPower;
float4 _GR_RingColor;           // rgb, a = speed in m/s

// A spectrogram: band across, time down, written a slice at a time into a ring.
TEXTURE2D(_GR_Spectrogram);
SAMPLER(sampler_GR_Spectrogram);
float4 _GR_Spec;                // x: v of "now", y: seconds held, z: wave speed m/s, w: inner radius m

// ------------------------------------------------------------------ the sky (SimWorld)

float4 _GR_SunDir;              // toward the sun; w = cos of the disc's angular radius (above 1 hides the disc)
float4 _GR_SunColor;
float4 _GR_ShadowColor;
float4 _GR_Zenith;
float4 _GR_Horizon;
float4 _GR_Ground;
float4 _GR_Fog;                 // x: distance for 63% fog, y: most fog, z: how much the sun warms the haze toward it
float4 _GR_Sky;                 // x: clouds, y: stars, z: milky way, w: cloud drift
float4 _GR_Moon;                // xyz toward the moon; w = cos of its angular radius (0 = no moon)
float4 _GR_CloudColor;          // what the cirrus is lit to: white by day, pink and orange at sunset

// A deck of heaped cloud over the world, and the shadows it throws on the land. The music opens it.
float4 _GR_Clouds;              // x: cover, y: how dark the thick of it is (a storm), z: metres across a cloud, w: height of the deck
float4 _GR_CloudMotion;         // xy: drift m/s, z: how far the loudness opens the clouds, w: how dark their shadows are

// Lightning inside the cloud on a strong beat: xyz toward it, w how bright it is now.
float4 _GR_Flash;

// Mist lying in a band of height, as cloud does along a cliff top.
float4 _GR_Mist;                // x: bottom, y: top (metres above the room's floor), z: density per km, w: how much the bass thickens it

// Space (a world's variant): the nebula across the sky, the paint the flora takes, the planets' tints.
float4 _GR_Nebula;              // x: amount, y: swirl scale, zw: where in the noise this variant's swirls are
float4 _GR_NebulaA;
float4 _GR_NebulaB;
float4 _GR_NebulaC;
float4 _GR_Palette[4];          // ground and grass, light leaves, dark leaves, rocks and tubes
float4 _GR_PlanetTint;
float4 _GR_StarTint;

// ------------------------------------------------------------------ noise

float GR_Hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float GR_Hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 GR_Hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float GR_Noise1(float x)
{
    float i = floor(x);
    float f = frac(x);
    float u = f * f * (3.0 - 2.0 * f);
    return lerp(GR_Hash11(i), GR_Hash11(i + 1.0), u);
}

float GR_Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = GR_Hash12(i);
    float b = GR_Hash12(i + float2(1.0, 0.0));
    float c = GR_Hash12(i + float2(0.0, 1.0));
    float d = GR_Hash12(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Four octaves, 0–1.
float GR_Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        sum += amp * GR_Noise(p);
        p = p * 2.03 + 17.1;
        amp *= 0.5;
    }
    return sum / 0.9375;
}

// ------------------------------------------------------------------ reading the music

/// A band by index, interpolated between neighbours.
float GR_BandIndex(float i)
{
    float n = max(_GR_BandCount, 1.0);
    i = clamp(i, 0.0, n - 1.0);
    int lo = (int)floor(i);
    int hi = min(lo + 1, (int)n - 1);
    return lerp(_GR_Bands[lo], _GR_Bands[hi], i - lo);
}

/// A band by position along the spectrum: 0 the lowest, 1 the highest.
float GR_Band(float t)
{
    return GR_BandIndex(saturate(t) * (max(_GR_BandCount, 1.0) - 1.0));
}

/// The spectrogram: band <paramref name="t"/> (0 lowest, 1 highest) as it was <paramref name="age"/> seconds ago.
float GR_SpecAt(float t, float age)
{
    float n = max(_GR_BandCount, 1.0);
    float u = (saturate(t) * (n - 1.0) + 0.5) / GR_MAX_BANDS;
    float v = _GR_Spec.x - age / max(_GR_Spec.y, 0.01);
    return SAMPLE_TEXTURE2D_LOD(_GR_Spectrogram, sampler_GR_Spectrogram, float2(u, v), 0).r;
}

/// The beat rings at a point: shockwaves running out across the ground from the ring origin.
float GR_Rings(float3 positionWS)
{
    float r = length(positionWS.xz - _GR_RingOrigin.xz);
    float speed = max(_GR_RingColor.a, 1.0);
    float sum = 0.0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float age = _GR_Clock - _GR_RingTimes[i];
        float front = age * speed;
        float width = 5.0 + front * 0.035;      // a ring spreads as it travels
        float ring = exp(-abs(r - front) / width);
        ring *= step(0.0, age) * exp(-age * 0.6);
        sum += ring * _GR_RingPower[i];
    }

    return sum;
}

// ------------------------------------------------------------------ the sky

/// The cloud deck over a point of it (metres across the outside, from the room): x how much cloud
/// is there, 0–1, y how thick it is. The louder the music, the further the clouds open.
float2 GR_CloudDeck(float2 xz)
{
    float2 q = (xz - _GR_CloudMotion.xy * _GR_Clock) / max(_GR_Clouds.z, 1.0);
    float warp = GR_Fbm(q * 0.45 + 3.1);
    float d = GR_Fbm(q + warp * 0.9) * 0.75 + GR_Fbm(q * 2.7 - warp * 1.3 + 7.7) * 0.25;
    float cover = _GR_Clouds.x * (1.0 - _GR_CloudMotion.z * saturate(_GR_Music.w));
    float edge = 1.0 - cover * 0.75 - 0.12;
    return float2(smoothstep(edge, edge + 0.08, d), saturate((d - edge) / 0.22));
}

/// How much a cloud overhead shades a point of the land, 0–1: the deck looked up at along the sun.
float GR_CloudShadow(float3 positionWS)
{
    if (_GR_Clouds.x <= 0.001 || _GR_CloudMotion.w <= 0.001)
        return 0.0;

    float3 p = positionWS - _GR_Origin.xyz;
    float3 toSun = _GR_SunDir.xyz;
    float t = max(0.0, _GR_Clouds.w - p.y) / max(toSun.y, 0.08);
    return GR_CloudDeck(p.xz + toSun.xz * t).x * _GR_CloudMotion.w;
}

/// Sky colour in a direction: the gradient, a glow along the horizon toward the sun, cirrus, the
/// night's stars and milky way, the moon, and the sun. The dome draws it, and the water reflects it.
float3 GR_Sky(float3 dir, float sunGain)
{
    float y = dir.y;
    float3 col = y >= 0.0
        ? lerp(_GR_Horizon.rgb, _GR_Zenith.rgb, pow(saturate(y), 0.45))
        : lerp(_GR_Horizon.rgb, _GR_Ground.rgb, saturate(-y * 5.0));

    float sd = dot(dir, _GR_SunDir.xyz);
    float nearHorizon = 1.0 - saturate(abs(y) * 2.5);
    col += _GR_SunColor.rgb * pow(saturate(sd), 5.0) * 0.18 * nearHorizon;

    // A nebula: billowing clouds of three colours across the whole sky, glowing a little brighter
    // with the loudness. Laid down before the stars, so the brightest of it drowns them out.
    if (_GR_Nebula.x > 0.001)
    {
        // Noise on the three planes through the direction, blended by facing: no seam anywhere
        // round the sky, which an angle-based lookup would put on one of the walls.
        float3 w = pow(abs(dir), 4.0);
        w /= w.x + w.y + w.z;
        float3 p = dir * (_GR_Nebula.y * 2.0) + float3(_GR_Nebula.zw, _GR_Nebula.z - _GR_Nebula.w);
        float warp = GR_Fbm(p.yz * 0.7 + 1.3) * w.x + GR_Fbm(p.zx * 0.7 + 1.3) * w.y + GR_Fbm(p.xy * 0.7 + 1.3) * w.z - 0.5;
        float3 pa = p + warp * 1.6 + _GR_Clock * 0.003;
        float3 pb = p * 1.9 - warp * 2.2 + 4.1;
        float a = GR_Fbm(pa.yz) * w.x + GR_Fbm(pa.zx) * w.y + GR_Fbm(pa.xy) * w.z;
        float b = GR_Fbm(pb.yz) * w.x + GR_Fbm(pb.zx) * w.y + GR_Fbm(pb.xy) * w.z;
        float3 cloud = lerp(_GR_NebulaA.rgb, _GR_NebulaB.rgb, smoothstep(0.35, 0.7, b));
        cloud = lerp(cloud, _GR_NebulaC.rgb, smoothstep(0.62, 0.85, a) * 0.8);
        float density = smoothstep(0.35, 0.85, a) * (0.6 + 0.4 * b);
        col += cloud * density * _GR_Nebula.x * (0.75 + 0.35 * _GR_Music.w) * smoothstep(-0.15, 0.1, y);
    }

    // Stars, twinkling harder with the treble.
    if (_GR_Sky.y > 0.001 && y > -0.05)
    {
        float2 sph = float2(atan2(dir.z, dir.x), asin(clamp(y, -1.0, 1.0)));
        float2 g = sph * (150.0 / GR_PI);
        float2 cell = floor(g);
        float2 h = GR_Hash22(cell);
        float present = step(0.78, GR_Hash12(cell + 7.31));
        float2 at = cell + 0.2 + 0.6 * h;
        float size = 0.07 + 0.13 * GR_Hash12(cell + 3.17);
        float star = present * saturate(1.0 - length(g - at) / size);
        float twinkle = 0.6 + 0.4 * sin(_GR_Clock * (1.3 + 3.5 * h.x) + h.y * 6.283);
        twinkle = lerp(1.0, twinkle, 0.35 + 0.65 * saturate(_GR_Music.z));
        float bright = (0.35 + 1.3 * h.y * h.y) * (0.85 + 0.5 * _GR_Music.z);
        // Only where the sky is dark enough: at dusk the stars come out overhead, not over the sunset.
        float dark = saturate(1.0 - dot(col, float3(0.3, 0.59, 0.11)) * 3.0);
        col += star * star * bright * twinkle * smoothstep(-0.02, 0.12, y) * dark * _GR_Sky.y;

        // The milky way: a soft band of light along a great circle.
        float3 axis = normalize(float3(0.32, 0.55, -0.77));
        float off = dot(dir, axis);
        float band = exp(-off * off * 22.0);
        float dust = GR_Fbm(sph * float2(3.0, 5.0) + 2.1);
        col += float3(0.55, 0.58, 0.8) * band * (0.25 + 0.75 * dust) * 0.12 * _GR_Sky.z * smoothstep(0.0, 0.2, y);
    }

    // The moon.
    if (_GR_Moon.w > 0.0)
    {
        float md = dot(dir, _GR_Moon.xyz);
        float disc = smoothstep(_GR_Moon.w, _GR_Moon.w + 0.00004, md);
        col += float3(0.95, 0.96, 1.0) * disc * 1.4;
        col += float3(0.25, 0.3, 0.45) * pow(saturate(md), 80.0) * 0.6;
    }

    // Cirrus: stretched noise on a plane over the world, thinning toward the horizon.
    if (_GR_Sky.x > 0.001 && y > 0.0)
    {
        float2 uv = dir.xz / (y + 0.08);
        uv = uv * 0.85 + float2(1.0, 0.4) * (_GR_Clock * _GR_Sky.w);
        float c = GR_Fbm(uv * float2(0.6, 1.7) + 3.7);
        c += 0.35 * GR_Fbm(uv * 3.1 + 11.0) - 0.15;
        float cover = smoothstep(1.0 - _GR_Sky.x, 1.3 - _GR_Sky.x * 0.6, c) * smoothstep(0.0, 0.2, y);
        float3 cloud = lerp(_GR_Horizon.rgb, _GR_CloudColor.rgb, 0.8) * (0.8 + 0.2 * saturate(sd));
        cloud += _GR_SunColor.rgb * pow(saturate(sd), 4.0) * 0.3;
        col = lerp(col, cloud, cover * 0.8);
    }

    // Heaped cloud: the deck seen from the eye, bright where the sun catches it, darker in the
    // thick of it (storm-dark in a storm), thinning into the haze toward the horizon.
    if (_GR_Clouds.x > 0.001 && y > 0.0)
    {
        float3 eye = _WorldSpaceCameraPos - _GR_Origin.xyz;
        float t = max(1.0, _GR_Clouds.w - eye.y) / max(y, 0.015);
        float2 deck = GR_CloudDeck(eye.xz + dir.xz * t);
        float3 bright = _GR_CloudColor.rgb * (0.92 + 0.2 * pow(saturate(sd), 3.0)) + _GR_SunColor.rgb * pow(saturate(sd), 12.0) * 0.4;
        float3 dark = lerp(bright * 0.78, _GR_ShadowColor.rgb * 0.55 + _GR_Horizon.rgb * 0.15, _GR_Clouds.y);
        float3 cloud = lerp(bright, dark, smoothstep(0.1, 0.45, deck.y) * 0.6 + smoothstep(0.5, 0.9, deck.y) * 0.4);
        cloud = lerp(_GR_Horizon.rgb, cloud, 0.35 + 0.65 * smoothstep(0.0, 0.3, y));
        float fade = smoothstep(0.0, 0.06, y) * exp(-t / 60000.0);

        // Lightning lights the cloud from inside round where it struck, and faintly all over.
        float flash = _GR_Flash.w * (pow(saturate(dot(dir, _GR_Flash.xyz)), 12.0) * 1.6 + 0.12);
        cloud += float3(0.85, 0.88, 1.0) * flash * (0.4 + 0.6 * deck.y);
        col = lerp(col, cloud, deck.x * fade);
    }

    // The sun, last, so it shines through thin cloud.
    float disc = smoothstep(_GR_SunDir.w, _GR_SunDir.w + 0.00015, sd);
    col += _GR_SunColor.rgb * (disc * 4.0 + pow(saturate(sd), 600.0) * 0.8 + pow(saturate(sd), 50.0) * 0.2) * sunGain;

    return col;
}

/// Aerial perspective: distance hazes toward the horizon colour, warmer looking toward the sun.
float3 GR_ApplyFog(float3 col, float3 positionWS, float amount)
{
    float3 d = positionWS - _WorldSpaceCameraPos;
    float dist = length(d);
    float3 dir = d / max(dist, 0.001);
    float toward = saturate(dot(dir, _GR_SunDir.xyz));
    float3 haze = _GR_Horizon.rgb + _GR_SunColor.rgb * pow(toward, 8.0) * _GR_Fog.z;
    float f = (1.0 - exp(-dist / max(_GR_Fog.x, 1.0))) * _GR_Fog.y * amount;
    col = lerp(col, haze, saturate(f));

    // Mist in its band of height: as much as the line of sight runs through the band, in wisps
    // drifting across the world, thicker with the bass.
    if (_GR_Mist.z > 0.001)
    {
        float y0 = _WorldSpaceCameraPos.y - _GR_Origin.y;
        float y1 = positionWS.y - _GR_Origin.y;
        float lo = min(y0, y1);
        float hi = max(y0, y1);
        float through = max(0.0, min(hi, _GR_Mist.y) - max(lo, _GR_Mist.x));
        float inside = hi - lo > 1.0 ? through / (hi - lo) : step(_GR_Mist.x, y1) * step(y1, _GR_Mist.y);
        float2 at = positionWS.xz - _GR_Origin.xz;
        float wisps = 0.4 + 1.2 * GR_Fbm(at * 0.0012 + float2(_GR_Clock * 0.012, _GR_Clock * 0.004));
        float density = _GR_Mist.z * (1.0 + _GR_Mist.w * _GR_Music.x) * wisps;
        float mist = 1.0 - exp(-dist * 0.001 * inside * density);
        float3 white = lerp(_GR_Horizon.rgb, _GR_CloudColor.rgb, 0.5) * (0.85 + 0.15 * toward);
        col = lerp(col, white, saturate(mist) * amount);
    }

    return col;
}

/// Cel lighting: lit by the sun above a soft terminator, the world's shadow colour below it, and a
/// little fill from the sky overhead. The shadow colour carries the anime look — blue-violet, not grey.
float3 GR_Light(float3 normalWS, float softness)
{
    float ndl = dot(normalWS, _GR_SunDir.xyz);
    float lit = smoothstep(-softness, softness, ndl - 0.05);
    float3 light = lerp(_GR_ShadowColor.rgb, _GR_SunColor.rgb, lit);
    light += _GR_Zenith.rgb * 0.12 * saturate(normalWS.y * 0.5 + 0.5);
    light += float3(0.8, 0.85, 1.0) * (_GR_Flash.w * 0.12);
    return light;
}

#endif

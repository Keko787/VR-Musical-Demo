using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>What a <see cref="SimSurface"/> is the ground of.</summary>
    public enum SurfaceProfile
    {
        /// <summary>Level: water, or a plane a shader lifts (the cloud sea).</summary>
        Flat,

        /// <summary>A desert canyon floor, rolling a little, walled far off by terraced rims.</summary>
        DesertFloor,

        /// <summary>A lake bed rising at the shore into a ring of ridged mountains.</summary>
        MountainRing,

        /// <summary>
        /// A sandy bay: the sea in front, the shore curving seaward either side of the room, the beach
        /// rising gently inland into dunes. Near the waterline it is a plain slope with no noise, so
        /// the sea's shader can work out where the sand is and wash up it.
        /// </summary>
        Beach,

        /// <summary>
        /// Norway: a mass of ridged mountains cut by glacial valleys and fjords along paths, with sharp
        /// peaks, hollows for lakes and tarns, a coast where the land drops into the sea, and a level
        /// spot under the room with nothing rising steeply in front of it.
        /// </summary>
        Fjordland,
    }

    /// <summary>
    /// A glacial valley or a fjord for <see cref="SurfaceProfile.Fjordland"/>: a path whose points
    /// carry the floor's height (y), a flat floor and walls rising to the mountains either side. A
    /// floor below the water level is a fjord.
    /// </summary>
    [System.Serializable]
    public struct Valley
    {
        [Tooltip("The valley's line: x and z across the ground, y the floor's height there.")]
        public Vector3[] Path;

        [Tooltip("Metres from the line to where the walls meet the mountains.")]
        public float Width;

        [Range(0.02f, 0.9f), Tooltip("How much of that is flat floor before the walls start.")]
        public float Flat;

        [Tooltip("How the walls rise: 1 straight, higher steep at the foot and easing toward the top, the shape glaciers leave.")]
        public float Steep;
    }

    /// <summary>
    /// The ground of an outside world: a disc of rings round the glass room, generated when the world
    /// first wakes. Its vertices are densest under the windows and thin out toward the horizon, which
    /// is where a fixed viewpoint needs them.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimSurface : MonoBehaviour
    {
        [SerializeField] SurfaceProfile m_Profile;

        [SerializeField, Tooltip("Metres from the room to the first ring.")]
        float m_InnerRadius = 2f;

        [SerializeField, Tooltip("Metres to the last ring. Inside the sky dome.")]
        float m_OuterRadius = 8200f;

        [SerializeField, Range(2, 400)] int m_Rings = 220;
        [SerializeField, Range(3, 720)] int m_Segments = 400;
        [SerializeField] int m_Seed = 1;

        [Header("Shape")]
        [SerializeField, Tooltip("Height of the open ground (the canyon floor, the lake bed), in this object's space.")]
        float m_Base = -7f;

        [SerializeField, Tooltip("How high the far walls rise: the canyon rim, the mountains.")]
        float m_Rim = 300f;

        [SerializeField, Tooltip("Metres out to where the far walls start: the canyon rim, the lake shore.")]
        float m_RimRadius = 2700f;

        [SerializeField, Tooltip("Metres over which the far walls rise.")]
        float m_RimWidth = 900f;

        [SerializeField, Tooltip("Metres of steps the canyon rim is cut into. 0 for none.")]
        float m_Terraces = 38f;

        [SerializeField, Tooltip("A hollow in the ground (x, z, radius, depth) for a pool to sit in.")]
        Vector4 m_Basin;

        [SerializeField, Tooltip("Radius about the room kept level at the base height, under its pedestal.")]
        float m_LevelAround = 10f;

        [Header("Beach")]
        [SerializeField, Tooltip("Metres from the room to the waterline straight ahead. For a beach, the base height is the sea level " +
            "and the rim height is the dunes'.")]
        float m_ShoreDistance = 7f;

        [SerializeField, Tooltip("Rise of the sand per metre inland, and the sea bed's fall per metre out, at the waterline.")]
        float m_BeachSlope = 0.05f;

        [SerializeField, Tooltip("Radius of the bay: the shore bends seaward either side of the room along a circle this big.")]
        float m_BayRadius = 900f;

        [Header("Fjordland")]
        [SerializeField, Tooltip("Glacial valleys and fjords cut through the mountains. For Fjordland, the base and rim heights and the " +
            "rim width are the mountains' foot, their height and the size of their ridges.")]
        Valley[] m_Valleys;

        [SerializeField, Tooltip("Sharp peaks standing out of the mountains: x, z, height of the summit, radius of its foot.")]
        Vector4[] m_Peaks;

        [SerializeField, Tooltip("Tarns and lakes: x, z, radius, depth. Each fills to just below the lowest point of its rim, and its " +
            "water is drawn on the Tarn Water mesh.")]
        Vector4[] m_Tarns;

        [SerializeField, Tooltip("Where the tarns' water is drawn: a child with the water material. Its mesh is made here.")]
        MeshFilter m_TarnWater;

        [SerializeField, Tooltip("A coast: x, z of a point on the coastline, the bearing of the sea from it (degrees, 0 ahead, 90 right), " +
            "and metres over which the land falls into the sea. Zero width for no coast.")]
        Vector4 m_Coast;

        [SerializeField, Tooltip("A bay: the sea reaching into the land in a circle (x, z, radius), and metres over which its shore " +
            "rises. Zero radius for none.")]
        Vector4 m_Bay;

        [SerializeField, Tooltip("Height of the sea bed past the coast.")]
        float m_SeaFloor = -60f;

        [SerializeField, Tooltip("A height the mountains are cut off at, leaving level tops above sheer walls. 0 for none.")]
        float m_Plateau;

        [SerializeField, Tooltip("Height of the ground under the room.")]
        float m_Pad;

        [SerializeField, Tooltip("A summit round the room: radius, and metres it falls by its edge. Keeps the ground the room stands on " +
            "broad. Zero radius for none.")]
        Vector2 m_Summit;

        [SerializeField, Tooltip("Within this many metres of the room, nothing rises above the pad plus the view slope times the distance " +
            "past the summit, so the view through the glass stays open. 0 for no limit.")]
        float m_ViewRadius;

        [SerializeField] float m_ViewSlope;

        [SerializeField, Tooltip("Metres out at which the land starts rising toward the horizon, and how far it has risen at the edge, " +
            "so the far mountains hide the end of the ground.")]
        Vector2 m_Horizon;

        /// <summary>
        /// The beach's waterline, for the sea's shader: (metres to the shore straight ahead, slope, bay
        /// radius, sea level), in this object's space.
        /// </summary>
        public Vector4 ShoreParams => new(m_ShoreDistance, m_BeachSlope, m_BayRadius, m_Base);

        /// <summary>Each tarn's water level, worked out from its rim when the surface generates.</summary>
        float[] m_TarnLevels;

        void Awake() => Generate();

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
            if (m_TarnWater != null && m_TarnWater.sharedMesh != null && (m_TarnWater.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(m_TarnWater.sharedMesh);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            if (m_Profile == SurfaceProfile.Fjordland)
                FillTarns();

            var mesh = SimMeshes.PolarGrid($"{name}_Surface", m_InnerRadius, m_OuterRadius, m_Rings, m_Segments, Height);

            // A flat plane may be lifted by its shader (the cloud sea's swells); keep it on screen when it is.
            if (m_Profile == SurfaceProfile.Flat)
                SimMeshes.Inflate(mesh, 80f);

            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);

            if (m_Profile == SurfaceProfile.Fjordland && m_TarnWater != null)
                SimMeshes.Assign(m_TarnWater, TarnWaterMesh());
        }

        /// <summary>Ground height at a point, in this object's space. Puffs ask this to stand trees on the ground.</summary>
        public float HeightAt(float x, float z)
        {
            if (m_Profile == SurfaceProfile.Fjordland && m_TarnLevels == null)
                FillTarns();
            return Height(x, z, Mathf.Sqrt(x * x + z * z));
        }

        float Height(float x, float z, float r)
        {
            switch (m_Profile)
            {
                case SurfaceProfile.DesertFloor: return Desert(x, z, r);
                case SurfaceProfile.MountainRing: return Mountains(x, z, r);
                case SurfaceProfile.Beach: return Beach(x, z);
                case SurfaceProfile.Fjordland: return Fjordland(x, z, r);
                case SurfaceProfile.Flat: return HorizonLift(r);
                default: return 0f;
            }
        }

        /// <summary>
        /// Water seen from high up: past the horizon radius the plane curves up toward the eye, so
        /// from a summit the sea runs on to the horizon instead of stopping short of it. Out there it
        /// is all haze, and nobody can see that it bends.
        /// </summary>
        float HorizonLift(float r)
        {
            if (m_Horizon.y == 0f || r <= m_Horizon.x)
                return 0f;
            float t = (r - m_Horizon.x) / Mathf.Max(1f, m_OuterRadius - m_Horizon.x);
            return m_Horizon.y * t * t;
        }

        // ================================================================== Fjordland

        /// <summary>
        /// Norway. The ground is built in layers, each working on the last: a mass of ridged mountains,
        /// cut off at the plateau if there is one; valleys and fjords carved along their paths; the coast
        /// and the bay dropping it into the sea; the limit that keeps the view open; horn peaks raised
        /// over all that, so a headland or a sea stack can stand out of the water and a spire can stand
        /// beside the room; the summit the room stands on; the tarns; and the level pad under the room.
        /// </summary>
        float Fjordland(float x, float z, float r)
        {
            float h = Tarns(x, z, Shape(x, z, r));
            return Mathf.Lerp(m_Pad, h, SimMeshes.Smooth01((r - m_LevelAround) / (m_LevelAround * 2f + 1f)));
        }

        float Shape(float x, float z, float r)
        {
            float s = m_Seed * 9.13f;
            float size = Mathf.Max(50f, m_RimWidth);

            float ridge = SimMeshes.Ridged(x / size + s, z / size, 5);
            float lumps = SimMeshes.Fbm(x / (size * 0.3f), z / (size * 0.3f) + s, 4);
            float h = m_Base + m_Rim * (0.3f + 0.75f * ridge + 0.3f * (lumps - 0.5f));

            // Cut off level, the rock below the cut left standing as walls.
            if (m_Plateau != 0f && h > m_Plateau)
                h = m_Plateau + (h - m_Plateau) * 0.06f + (lumps - 0.5f) * 12f;

            h += HorizonLift(r);

            if (m_Valleys != null)
            {
                float wobble = 1f + 0.3f * (SimMeshes.Fbm(x / 650f + s, z / 650f - s, 3) - 0.5f);
                foreach (var valley in m_Valleys)
                    h = Carve(valley, x, z, h, wobble);
            }

            if (m_Coast.w > 0f)
            {
                float bearing = m_Coast.z * Mathf.Deg2Rad;
                float d = (x - m_Coast.x) * Mathf.Sin(bearing) + (z - m_Coast.y) * Mathf.Cos(bearing);
                d += (SimMeshes.Fbm(x / 800f + s, z / 800f, 3) - 0.5f) * m_Coast.w * 1.5f;
                h = Mathf.Lerp(h, m_SeaFloor, Mathf.Pow(SimMeshes.Smooth01(d / m_Coast.w + 0.15f), 0.7f));
            }

            if (m_Bay.z > 0f)
            {
                float d = Mathf.Sqrt((x - m_Bay.x) * (x - m_Bay.x) + (z - m_Bay.y) * (z - m_Bay.y));
                float t = (m_Bay.z - d) / Mathf.Max(1f, m_Bay.w);
                h = Mathf.Lerp(h, m_SeaFloor, Mathf.Pow(SimMeshes.Smooth01(t), 0.6f));
            }

            h = ViewLimit(h, r, lumps);

            if (m_Peaks != null)
            {
                for (int i = 0; i < m_Peaks.Length; i++)
                {
                    var peak = m_Peaks[i];
                    float dx = x - peak.x, dz = z - peak.y;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d >= peak.w * 1.3f)
                        continue;

                    // Arêtes: the foot reaches further in some directions than others, so the horn has
                    // ridges running off it rather than being a cone.
                    float reach = peak.w * (0.7f + 0.6f * SimMeshes.Around(Mathf.Atan2(dx, dz), 1.9f, i + m_Seed));
                    float k = Mathf.Clamp01(1f - d / reach);
                    float crags = 0.9f + 0.2f * SimMeshes.Ridged(x / 120f + i, z / 120f, 3);
                    float horn = h + (peak.z - h) * Mathf.Pow(k, 1.7f) * crags;
                    h = Mathf.Max(h, horn);
                }
            }

            // The summit: a dome round the room, falling away to its edge and fading out past it.
            if (m_Summit.x > 0f && r < m_Summit.x * 1.6f)
            {
                float q = r / m_Summit.x;
                float dome = m_Pad - m_Summit.y * q * q + (lumps - 0.5f) * m_Summit.y * 0.3f * q;
                float weight = 1f - SimMeshes.Smooth01((r - m_Summit.x) / (m_Summit.x * 0.6f));
                h += Mathf.Max(0f, dome - h) * weight;
            }

            return h;
        }

        /// <summary>
        /// One valley cut into the ground: flat floor, walls steep at the foot and rounding off into the
        /// mountains at the top — the U a glacier leaves. Its floor follows the heights along its path,
        /// so one valley can be a fjord at its mouth and a lake further up.
        /// </summary>
        static float Carve(Valley valley, float x, float z, float h, float wobble)
        {
            if (valley.Path == null || valley.Path.Length == 0)
                return h;

            float width = Mathf.Max(1f, valley.Width) * wobble;
            var (d, floor) = ToPath(valley.Path, x, z);
            float t = d / width;
            if (t >= 1f)
                return h;

            float u = Mathf.Clamp01((t - valley.Flat) / Mathf.Max(0.01f, 1f - valley.Flat));
            float rise = Mathf.Pow(SimMeshes.Smooth01(u), 1f / Mathf.Max(0.2f, valley.Steep));
            return Mathf.Min(h, Mathf.Lerp(floor, h, rise));
        }

        /// <summary>Metres from a point to the nearest point of a path, and the path's height there.</summary>
        static (float distance, float height) ToPath(Vector3[] path, float x, float z)
        {
            if (path.Length == 1)
                return (Mathf.Sqrt((x - path[0].x) * (x - path[0].x) + (z - path[0].z) * (z - path[0].z)), path[0].y);

            float best = float.MaxValue;
            float height = path[0].y;
            for (int i = 0; i < path.Length - 1; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                float abx = b.x - a.x, abz = b.z - a.z;
                float t = Mathf.Clamp01(((x - a.x) * abx + (z - a.z) * abz) / Mathf.Max(1e-4f, abx * abx + abz * abz));
                float px = a.x + abx * t - x, pz = a.z + abz * t - z;
                float d2 = px * px + pz * pz;
                if (d2 < best)
                {
                    best = d2;
                    height = Mathf.Lerp(a.y, b.y, t);
                }
            }

            return (Mathf.Sqrt(best), height);
        }

        /// <summary>
        /// Nothing near the room rises above the pad plus the view slope times the distance from the
        /// edge of the summit (or the pad, without one), and never much lower than the mountains' foot;
        /// past the view radius the limit lifts. A falling slope makes the room's summit a mountain top,
        /// whatever the mountains round it do, without digging a moat round it.
        /// </summary>
        float ViewLimit(float h, float r, float lumps)
        {
            if (m_ViewRadius <= 0f)
                return h;

            float limit = m_Pad + m_ViewSlope * Mathf.Max(0f, r - Mathf.Max(m_LevelAround, m_Summit.x));
            limit = Mathf.Max(limit, m_Base + m_Rim * 0.25f * (lumps - 0.3f));
            limit += SimMeshes.Smooth01((r - m_ViewRadius * 0.7f) / (m_ViewRadius * 0.3f)) * 5000f;

            // A smooth minimum, so the limit leaves no crease where it starts to bite.
            const float k = 25f;
            float m = Mathf.Clamp01(0.5f + 0.5f * (limit - h) / k);
            return Mathf.Lerp(limit, h, m) - k * m * (1f - m);
        }

        /// <summary>The ground with the tarns scooped out of it: a dish under each one's water, rising to a shore just above it.</summary>
        float Tarns(float x, float z, float h)
        {
            if (m_Tarns == null || m_TarnLevels == null)
                return h;

            for (int i = 0; i < m_Tarns.Length && i < m_TarnLevels.Length; i++)
            {
                var tarn = m_Tarns[i];
                float radius = Mathf.Max(1f, tarn.z);
                float t = Mathf.Sqrt((x - tarn.x) * (x - tarn.x) + (z - tarn.y) * (z - tarn.y)) / radius;
                if (t >= 1f)
                    continue;

                float level = m_TarnLevels[i];
                if (t < k_TarnWater)
                {
                    float q = t / k_TarnWater;
                    h = level - Mathf.Max(1f, tarn.w) * (1f - q * q) - 0.3f;
                }
                else
                {
                    float shore = SimMeshes.Smooth01((t - k_TarnWater) / (1f - k_TarnWater));
                    h = Mathf.Lerp(level + 0.4f, Mathf.Max(h, level + 0.4f), shore);
                }
            }

            return h;
        }

        /// <summary>The share of a tarn's radius that is water; the rest is the shore rising out of it.</summary>
        const float k_TarnWater = 0.6f;

        /// <summary>Each tarn fills to a little below the lowest point round its rim, so its water never stands above the ground beside it.</summary>
        void FillTarns()
        {
            m_TarnLevels = new float[m_Tarns?.Length ?? 0];
            for (int i = 0; i < m_TarnLevels.Length; i++)
            {
                var tarn = m_Tarns[i];
                float lowest = float.MaxValue;
                for (int k = 0; k < 24; k++)
                {
                    float a = k / 24f * Mathf.PI * 2f;
                    float px = tarn.x + Mathf.Sin(a) * tarn.z * k_TarnWater;
                    float pz = tarn.y + Mathf.Cos(a) * tarn.z * k_TarnWater;
                    float pr = Mathf.Sqrt(px * px + pz * pz);
                    lowest = Mathf.Min(lowest, Shape(px, pz, pr));
                }

                m_TarnLevels[i] = lowest - 1f;
            }
        }

        /// <summary>A flat disc of water for each tarn, at its level, in this object's space.</summary>
        Mesh TarnWaterMesh()
        {
            var b = new SimMeshes.Builder();
            const int sides = 40;
            for (int i = 0; i < m_TarnLevels.Length; i++)
            {
                var tarn = m_Tarns[i];
                float radius = tarn.z * k_TarnWater;
                var centre = transform.TransformPoint(new Vector3(tarn.x, m_TarnLevels[i], tarn.y));
                centre = m_TarnWater.transform.InverseTransformPoint(centre);
                int middle = b.Add(centre, Color.white);
                int first = b.Count;
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    b.Add(centre + new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius), Color.white);
                }

                for (int k = 0; k < sides; k++)
                    b.Triangle(middle, first + k, first + k + 1);
            }

            return b.ToMesh($"{name}_Tarns");
        }

        float Beach(float x, float z)
        {
            float shore = m_ShoreDistance + x * x / (2f * Mathf.Max(1f, m_BayRadius));
            float d = z - shore;                    // metres out to sea; negative inland

            // Under the sea the bed shelves away and levels off ten metres down, leaving the waterline
            // at the same slope as the beach.
            if (d >= 0f)
                return m_Base - 10f * (1f - Mathf.Exp(-m_BeachSlope * d / 10f));

            float h = m_Base - m_BeachSlope * d;

            // Ripples and dunes, kept off the swash zone so the sea's shader knows exactly where the sand is.
            float s = m_Seed * 5.3f;
            float inland = SimMeshes.Smooth01((-d - 10f) / 30f);
            h += inland * (SimMeshes.Fbm(x / 9f + s, z / 9f, 2) - 0.5f) * 0.3f;
            float dunes = SimMeshes.Smooth01((-d - 30f) / 60f);
            h += dunes * m_Rim * (0.4f + 0.8f * SimMeshes.Fbm(x / 120f, z / 120f + s, 3));
            return h;
        }

        float Desert(float x, float z, float r)
        {
            float s = m_Seed * 13.7f;
            float roll = (SimMeshes.Fbm(x / 380f + s, z / 380f, 4) - 0.5f) * 9f;
            float rubble = (SimMeshes.Fbm(x / 70f, z / 70f + s, 3) - 0.5f) * 2.2f;
            float h = m_Base + roll + rubble;

            // The far rim: a wall of terraced cliffs round the canyon, nearer in some directions than others.
            float angle = Mathf.Atan2(x, z);
            float wobble = (SimMeshes.Around(angle, 1.8f, m_Seed) - 0.5f) * m_RimWidth * 1.6f;
            float t = SimMeshes.Smooth01((r - (m_RimRadius + wobble)) / Mathf.Max(1f, m_RimWidth));
            float rim = t * m_Rim * (0.65f + 0.7f * SimMeshes.Fbm(x / 900f, z / 900f + s + 7f, 3));
            h += SimMeshes.Terrace(rim, m_Terraces);

            if (m_Basin.z > 0f)
            {
                float d = Mathf.Sqrt((x - m_Basin.x) * (x - m_Basin.x) + (z - m_Basin.y) * (z - m_Basin.y));
                float hollow = 1f - SimMeshes.Smooth01((d - m_Basin.z * 0.6f) / (m_Basin.z * 0.6f));
                h = Mathf.Lerp(h, m_Base - m_Basin.w, hollow);
            }

            return Mathf.Lerp(m_Base, h, SimMeshes.Smooth01((r - m_LevelAround) / (m_LevelAround * 3f + 1f)));
        }

        float Mountains(float x, float z, float r)
        {
            float s = m_Seed * 7.9f;
            float angle = Mathf.Atan2(x, z);
            float shore = m_RimRadius + (SimMeshes.Around(angle, 2.2f, m_Seed) - 0.5f) * m_RimRadius * 0.5f;

            // The lake bed shelves up to the waterline at the shore...
            float bed = Mathf.Lerp(m_Base, 0.6f, SimMeshes.Smooth01((r - shore + 140f) / 140f));

            // ...and the mountains rise behind it, ridged, higher the further out.
            float t = SimMeshes.Smooth01((r - shore) / Mathf.Max(1f, m_RimWidth));
            float ridge = SimMeshes.Ridged(x / 1100f + s, z / 1100f, 5);
            float lumps = SimMeshes.Fbm(x / 350f, z / 350f + s, 3);
            float mountains = t * m_Rim * (0.3f + 0.95f * ridge + 0.15f * lumps) * (0.75f + 0.25f * t);

            return bed + mountains;
        }
    }
}

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

        /// <summary>
        /// The beach's waterline, for the sea's shader: (metres to the shore straight ahead, slope, bay
        /// radius, sea level), in this object's space.
        /// </summary>
        public Vector4 ShoreParams => new(m_ShoreDistance, m_BeachSlope, m_BayRadius, m_Base);

        void Awake() => Generate();

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            var mesh = SimMeshes.PolarGrid($"{name}_Surface", m_InnerRadius, m_OuterRadius, m_Rings, m_Segments, Height);

            // A flat plane may be lifted by its shader (the cloud sea's swells); keep it on screen when it is.
            if (m_Profile == SurfaceProfile.Flat)
                SimMeshes.Inflate(mesh, 80f);

            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);
        }

        /// <summary>Ground height at a point, in this object's space. Puffs ask this to stand trees on the ground.</summary>
        public float HeightAt(float x, float z) => Height(x, z, Mathf.Sqrt(x * x + z * z));

        float Height(float x, float z, float r)
        {
            switch (m_Profile)
            {
                case SurfaceProfile.DesertFloor: return Desert(x, z, r);
                case SurfaceProfile.MountainRing: return Mountains(x, z, r);
                case SurfaceProfile.Beach: return Beach(x, z);
                default: return 0f;
            }
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

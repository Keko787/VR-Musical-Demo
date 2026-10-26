using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>What a cluster of puffs makes.</summary>
    public enum PuffKind
    {
        /// <summary>Round anime trees: a few balls of foliage each, scattered over an area.</summary>
        Trees,

        /// <summary>One towering cumulus per cluster, its puffs stacked and narrowing upward.</summary>
        Cumulus,

        /// <summary>Rounded boulders, one squashed ball each, sunk a little into the ground.</summary>
        Boulders,

        /// <summary>Spruces: tiers of flattened puffs narrowing to a point on a dark trunk, scattered like trees.</summary>
        Spruces,
    }

    /// <summary>One group of puffs: where, how many, how big, what colour, and which part of the music lights it.</summary>
    [System.Serializable]
    public struct PuffCluster
    {
        public Vector3 Centre;

        [Tooltip("Trees: half-extents of the area they are scattered over (x, z). Cumulus: base radius (x) and height (y).")]
        public Vector2 Spread;

        [Tooltip("Trees: how many trees. Cumulus: how many puffs in the tower.")]
        public int Count;

        [Tooltip("Metres: a tree's crown radius, or the tower's base puff radius.")]
        public float Size;

        public Color Colour;

        [Tooltip("Which band lights it from inside, 0 the lowest to 1 the highest.")]
        public float Band;

        [Tooltip("How strongly that band lights it. 0 for nothing.")]
        public float Glow;

        [Tooltip("Stand each tree on the ground surface rather than at the cluster's height.")]
        public bool OnGround;
    }

    /// <summary>
    /// Trees and cloud towers, built out of balls: each puff is a small sphere, cel shading turns a
    /// heap of them into the blobby foliage and cumulus of a painted background, and the whole lot is
    /// one mesh. A puff can be lit from inside by a band of the music; a cumulus puff also carries its
    /// radius (third UV channel), so a material that billows can swell it with that band.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimPuffs : MonoBehaviour
    {
        [SerializeField] PuffKind m_Kind;
        [SerializeField] PuffCluster[] m_Clusters;
        [SerializeField] int m_Seed = 1;

        [SerializeField, Tooltip("The ground trees stand on when a cluster asks for it.")]
        SimSurface m_Ground;

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
            if (m_Clusters == null)
                return;

            var random = new System.Random(m_Seed);
            float Range(float a, float c) => a + (c - a) * (float)random.NextDouble();

            var b = new SimMeshes.Builder();
            foreach (var cluster in m_Clusters)
            {
                var band = new Vector2(cluster.Band, cluster.Glow);
                if (m_Kind == PuffKind.Trees)
                    Trees(b, cluster, band, Range);
                else if (m_Kind == PuffKind.Boulders)
                    Boulders(b, cluster, band, Range);
                else if (m_Kind == PuffKind.Spruces)
                    Spruces(b, cluster, band, Range);
                else
                    Cumulus(b, cluster, band, Range);
            }

            var mesh = b.ToMesh($"{name}_Puffs");
            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);
        }

        void Trees(SimMeshes.Builder b, PuffCluster cluster, Vector2 band, System.Func<float, float, float> range)
        {
            for (int i = 0; i < cluster.Count; i++)
            {
                // Scattered over an ellipse, denser toward the middle.
                float angle = range(0f, Mathf.PI * 2f);
                float reach = Mathf.Sqrt(range(0f, 1f));
                var at = cluster.Centre + new Vector3(Mathf.Cos(angle) * cluster.Spread.x * reach, 0f,
                    Mathf.Sin(angle) * cluster.Spread.y * reach);

                if (cluster.OnGround && m_Ground != null)
                {
                    var world = transform.TransformPoint(at);
                    var local = m_Ground.transform.InverseTransformPoint(world);
                    local.y = m_Ground.HeightAt(local.x, local.z);
                    at = transform.InverseTransformPoint(m_Ground.transform.TransformPoint(local));
                }

                float s = cluster.Size * range(0.65f, 1.3f);
                var shade = cluster.Colour * range(0.8f, 1.15f);
                shade.a = 1f;
                var light = Color.Lerp(shade, Color.white, 0.12f);
                light.a = 1f;

                SimMeshes.Icosphere(b, at + new Vector3(0f, s * 0.9f, 0f), new Vector3(s, s * 0.85f, s), shade, band);
                SimMeshes.Icosphere(b, at + new Vector3(s * 0.6f, s * 0.7f, range(-0.3f, 0.3f) * s),
                    new Vector3(s, s * 0.85f, s) * 0.7f, shade, band);
                SimMeshes.Icosphere(b, at + new Vector3(-s * 0.55f, s * 0.75f, range(-0.3f, 0.3f) * s),
                    new Vector3(s, s * 0.85f, s) * 0.72f, shade, band);
                SimMeshes.Icosphere(b, at + new Vector3(range(-0.2f, 0.2f) * s, s * 1.55f, 0f),
                    new Vector3(s, s * 0.85f, s) * 0.62f, light, band);
            }
        }

        void Boulders(SimMeshes.Builder b, PuffCluster cluster, Vector2 band, System.Func<float, float, float> range)
        {
            for (int i = 0; i < cluster.Count; i++)
            {
                float angle = range(0f, Mathf.PI * 2f);
                float reach = Mathf.Sqrt(range(0f, 1f));
                var at = cluster.Centre + new Vector3(Mathf.Cos(angle) * cluster.Spread.x * reach, 0f,
                    Mathf.Sin(angle) * cluster.Spread.y * reach);

                if (cluster.OnGround && m_Ground != null)
                {
                    var local = m_Ground.transform.InverseTransformPoint(transform.TransformPoint(at));
                    local.y = m_Ground.HeightAt(local.x, local.z);
                    at = transform.InverseTransformPoint(m_Ground.transform.TransformPoint(local));
                }

                float s = cluster.Size * range(0.5f, 1.4f);
                var radii = new Vector3(s * range(0.9f, 1.4f), s * range(0.5f, 0.75f), s * range(0.9f, 1.3f));
                var colour = cluster.Colour * range(0.85f, 1.1f);
                colour.a = 1f;
                SimMeshes.Icosphere(b, at + Vector3.up * (radii.y * 0.45f), radii, colour, band);
            }
        }

        void Spruces(SimMeshes.Builder b, PuffCluster cluster, Vector2 band, System.Func<float, float, float> range)
        {
            for (int i = 0; i < cluster.Count; i++)
            {
                float angle = range(0f, Mathf.PI * 2f);
                float reach = Mathf.Sqrt(range(0f, 1f));
                var at = cluster.Centre + new Vector3(Mathf.Cos(angle) * cluster.Spread.x * reach, 0f,
                    Mathf.Sin(angle) * cluster.Spread.y * reach);

                if (cluster.OnGround && m_Ground != null)
                {
                    var local = m_Ground.transform.InverseTransformPoint(transform.TransformPoint(at));
                    local.y = m_Ground.HeightAt(local.x, local.z);
                    at = transform.InverseTransformPoint(m_Ground.transform.TransformPoint(local));
                }

                // Size is the tree's height; it is about a third as wide at the foot of its crown.
                float height = cluster.Size * range(0.7f, 1.3f);
                var shade = cluster.Colour * range(0.8f, 1.1f);
                shade.a = 1f;
                var trunk = new Color(0.22f, 0.16f, 0.12f, 1f);
                SimMeshes.Icosphere(b, at + Vector3.up * (height * 0.1f), new Vector3(height * 0.04f, height * 0.12f, height * 0.04f), trunk, Vector2.zero);

                const int tiers = 4;
                for (int t = 0; t < tiers; t++)
                {
                    float up = t / (float)(tiers - 1);
                    float radius = height * 0.19f * (1f - 0.75f * up);
                    var tier = Color.Lerp(shade, Color.Lerp(shade, Color.white, 0.1f), up);
                    tier.a = 1f;
                    SimMeshes.Icosphere(b, at + Vector3.up * (height * (0.22f + 0.62f * up)),
                        new Vector3(radius, height * 0.17f * (1f - 0.35f * up), radius), tier, band);
                }
            }
        }

        void Cumulus(SimMeshes.Builder b, PuffCluster cluster, Vector2 band, System.Func<float, float, float> range)
        {
            float height = Mathf.Max(1f, cluster.Spread.y);
            float width = Mathf.Max(1f, cluster.Spread.x);
            int count = Mathf.Max(1, cluster.Count);

            for (int i = 0; i < count; i++)
            {
                // Heights at random rather than in order, so the tower is a heap, not a stack.
                float up = Mathf.Pow(range(0f, 1f), 0.85f);
                float radius = cluster.Size * (1f - 0.55f * up) * range(0.6f, 1.25f);
                float spread = width * (0.75f - 0.5f * up);
                float angle = range(0f, Mathf.PI * 2f);
                float reach = Mathf.Sqrt(range(0f, 1f)) * spread;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                var at = cluster.Centre + outward * reach + Vector3.up * (height * up + radius * 0.4f);

                // The lower puffs a shade greyer, the way a cloud's underside is.
                var colour = Color.Lerp(cluster.Colour * 0.86f, cluster.Colour, up);
                colour.a = 1f;

                SimMeshes.Icosphere(b, at, new Vector3(radius, radius * 0.92f, radius), colour, band, new Vector2(radius, 0f));

                // A smaller puff bulging out of the bigger one: the cauliflower edge cumulus have.
                if (range(0f, 1f) < 0.6f)
                {
                    float small = radius * range(0.35f, 0.55f);
                    var bulge = at + (outward * range(0.6f, 0.9f) + Vector3.up * range(0.1f, 0.6f)).normalized * (radius * 0.85f);
                    SimMeshes.Icosphere(b, bulge, Vector3.one * small, colour, band, new Vector2(small, 0f));
                }
            }
        }
    }
}

using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>What a <see cref="SimFlora"/> grows.</summary>
    public enum FloraKind
    {
        /// <summary>Tufts of grass, their tips lit by a band of the treble.</summary>
        Grass,

        /// <summary>Plants of big broad leaves on arching stems, like the film's tropical foliage.</summary>
        Broadleaf,

        /// <summary>Spiky plants of long narrow blades.</summary>
        Blades,
    }

    /// <summary>One patch of flora: where, how many, how big.</summary>
    [System.Serializable]
    public struct FloraPatch
    {
        public Vector3 Centre;

        [Tooltip("Half-extents of the area (x, z).")]
        public Vector2 Spread;

        [Tooltip("Tufts of grass, or plants.")]
        public int Count;

        [Tooltip("Smallest and largest, metres: a blade's height, or a leaf's length.")]
        public Vector2 Size;
    }

    /// <summary>
    /// Grass and leaves for an alien meadow, in one mesh per kind. Everything carries sway weights for
    /// the land shader (more toward the tips), and grass tips carry a band of the treble in the second
    /// UV channel, so the meadow glints as the hi-hats play. Colour comes from the material's palette
    /// slot — the world variant's paint — with a little variation baked into the vertex colours.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimFlora : MonoBehaviour
    {
        [SerializeField] FloraKind m_Kind;
        [SerializeField] FloraPatch[] m_Patches;

        [SerializeField, Tooltip("The ground the flora grows on.")]
        SimSurface m_Ground;

        [SerializeField] int m_Seed = 1;

        [SerializeField, Tooltip("Grass: the part of the spectrum its tips glow with (0 lowest, 1 highest).")]
        Vector2 m_TipBands = new(0.55f, 1f);

        [SerializeField, Tooltip("Grass: how strongly the tips glow at full level.")]
        float m_TipGlow = 1f;

        [SerializeField, Tooltip("Nothing grows inside this box round the room's middle (half-sizes, metres): it would stand inside the glass.")]
        Vector3 m_KeepOut = new(2.1f, 3f, 1.6f);

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
            if (m_Patches == null)
                return;

            var random = new System.Random(m_Seed);
            float Range(float a, float b) => a + (b - a) * (float)random.NextDouble();

            var b = new SimMeshes.Builder();
            foreach (var patch in m_Patches)
            {
                for (int i = 0; i < patch.Count; i++)
                {
                    float angle = Range(0f, Mathf.PI * 2f);
                    float reach = Mathf.Sqrt(Range(0f, 1f));
                    var at = patch.Centre + new Vector3(Mathf.Cos(angle) * patch.Spread.x * reach, 0f, Mathf.Sin(angle) * patch.Spread.y * reach);
                    if (Mathf.Abs(at.x) < m_KeepOut.x && Mathf.Abs(at.z) < m_KeepOut.z)
                        continue;

                    at.y = GroundAt(at);
                    float size = Range(patch.Size.x, patch.Size.y);
                    float phase = Range(0f, 6.283f);

                    switch (m_Kind)
                    {
                        case FloraKind.Grass:
                            Tuft(b, at, size, phase, Range(m_TipBands.x, m_TipBands.y), Range);
                            break;
                        case FloraKind.Broadleaf:
                            Plant(b, at, size, phase, Range, Mathf.RoundToInt(Range(5f, 9f)), 0.22f, 0.55f, 0f);
                            break;
                        default:
                            Plant(b, at, size, phase, Range, Mathf.RoundToInt(Range(10f, 16f)), 0.05f, 0.85f, 0.3f);
                            break;
                    }
                }
            }

            SimMeshes.Assign(GetComponent<MeshFilter>(), b.ToMesh($"{name}_{m_Kind}"));
        }

        float GroundAt(Vector3 local)
        {
            if (m_Ground == null)
                return local.y;

            var world = transform.TransformPoint(local);
            var onGround = m_Ground.transform.InverseTransformPoint(world);
            onGround.y = m_Ground.HeightAt(onGround.x, onGround.z);
            return transform.InverseTransformPoint(m_Ground.transform.TransformPoint(onGround)).y;
        }

        /// <summary>A tuft of five bent blades, darker at the root, their tips holding a treble band.</summary>
        void Tuft(SimMeshes.Builder b, Vector3 at, float height, float phase, float band, System.Func<float, float, float> range)
        {
            for (int k = 0; k < 5; k++)
            {
                float azimuth = range(0f, Mathf.PI * 2f);
                var outward = new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth));
                var side = new Vector3(-outward.z, 0f, outward.x);
                float h = height * range(0.65f, 1.15f);
                float lean = range(0.15f, 0.55f);
                float width = h * 0.09f;
                var root = at + outward * range(0f, 0.08f) * height;

                var mid = root + Vector3.up * (h * 0.55f) + outward * (h * lean * 0.35f);
                var tip = root + Vector3.up * h + outward * (h * lean);
                var shade = range(0.85f, 1.1f);
                var dark = new Color(0.6f * shade, 0.6f * shade, 0.6f * shade, 1f);
                var light = new Color(1.15f * shade, 1.15f * shade, 1.15f * shade, 1f);

                int a = b.Add(root - side * width, dark, Vector2.zero, Vector2.zero, new Vector2(0f, phase));
                int c = b.Add(root + side * width, dark, Vector2.zero, Vector2.zero, new Vector2(0f, phase));
                int d = b.Add(mid - side * (width * 0.6f), Color.Lerp(dark, light, 0.6f), Vector2.zero,
                    new Vector2(band, m_TipGlow * 0.25f), new Vector2(0.4f, phase));
                int e = b.Add(mid + side * (width * 0.6f), Color.Lerp(dark, light, 0.6f), Vector2.zero,
                    new Vector2(band, m_TipGlow * 0.25f), new Vector2(0.4f, phase));
                int f = b.Add(tip, light, Vector2.zero, new Vector2(band, m_TipGlow), new Vector2(1f, phase));

                b.Triangle(a, d, c);
                b.Triangle(c, d, e);
                b.Triangle(d, f, e);
            }
        }

        /// <summary>
        /// A plant: leaves arching out from its base at every angle, each a blade folded down its
        /// midrib. <paramref name="width"/> is the leaf's widest as a fraction of its length; a little
        /// <paramref name="upright"/> makes them stand up before they arch over.
        /// </summary>
        void Plant(SimMeshes.Builder b, Vector3 at, float length, float phase, System.Func<float, float, float> range, int leaves,
            float width, float rise, float upright)
        {
            for (int k = 0; k < leaves; k++)
            {
                float azimuth = (k + range(-0.35f, 0.35f)) / leaves * Mathf.PI * 2f;
                float l = length * range(0.7f, 1.1f);
                Leaf(b, at, azimuth, l, width * l, rise * range(0.7f, 1.2f), upright, phase + k * 0.9f, range(0.8f, 1.15f));
            }
        }

        void Leaf(SimMeshes.Builder b, Vector3 root, float azimuth, float length, float width, float rise, float upright, float phase, float shade)
        {
            const int segments = 12;
            var outward = new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth));
            var side = Vector3.Cross(Vector3.up, outward).normalized;

            int start = b.Count;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                // Up from the base, then out and over.
                var spine = root + Vector3.up * (length * (upright * u + rise * u - 0.75f * u * u))
                                 + outward * (length * (0.25f + 0.75f * u) * u);

                // Floored at 0: the float Mathf.PI is a hair over π, so at the tip the sine is a hair
                // below zero, and a fractional power of that is NaN, which takes the whole mesh's bounds with it.
                float w = width * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f))), 0.9f);
                var fold = Vector3.up * (w * 0.35f);
                var colour = new Color(shade * (0.75f + 0.35f * u), shade * (0.75f + 0.35f * u), shade * (0.75f + 0.35f * u), 1f);
                var sway = new Vector2(u, phase);

                b.Add(spine + side * w - fold, colour, Vector2.zero, Vector2.zero, sway);
                b.Add(spine, Color.Lerp(colour, Color.white, 0.08f), Vector2.zero, Vector2.zero, sway);
                b.Add(spine - side * w - fold, colour, Vector2.zero, Vector2.zero, sway);
            }

            for (int i = 0; i < segments; i++)
            {
                int l = start + i * 3;
                b.Triangle(l, l + 1, l + 3);
                b.Triangle(l + 1, l + 4, l + 3);
                b.Triangle(l + 1, l + 2, l + 4);
                b.Triangle(l + 2, l + 5, l + 4);
            }
        }
    }
}

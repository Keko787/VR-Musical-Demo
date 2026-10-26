using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>What a <see cref="SimProps"/> makes.</summary>
    public enum PropKind
    {
        /// <summary>A village: wooden houses in red, white and ochre, scattered over the level ground in an area.</summary>
        Houses,

        /// <summary>A fish farm: rings of floating pipe in a grid on the water, a lamp on each that lights with its band.</summary>
        FishFarm,

        /// <summary>A cruise ship at anchor, white decks over a dark hull, its windows lit along the spectrum.</summary>
        Ship,
    }

    /// <summary>
    /// The things people have put in a Norwegian landscape, all in one mesh on the land material and
    /// painted by vertex colour: a village's houses, a fish farm's rings, a cruise ship. From a summit
    /// they are specks of colour by the water, which is what makes the fjord read at its true size.
    /// Lamps carry a band of the music in the second UV channel, for a material with band glow.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimProps : MonoBehaviour
    {
        [SerializeField] PropKind m_Kind;

        [SerializeField, Tooltip("Houses: the middle of the village, on the ground. Fish farm and ship: where it floats, in this object's space.")]
        Vector3 m_Centre;

        [SerializeField, Tooltip("Houses: half-extents of the village (x, z). Fish farm: rings across and along.")]
        Vector2 m_Spread = new(150f, 80f);

        [SerializeField, Tooltip("Houses: how many. Fish farm: rings. Ship: ignored.")]
        int m_Count = 20;

        [SerializeField, Tooltip("Metres: a house's length, a ring's diameter, the ship's length.")]
        float m_Size = 9f;

        [SerializeField, Tooltip("Degrees the farm's grid or the ship is turned, clockwise from straight ahead.")]
        float m_Yaw;

        [SerializeField, Tooltip("Houses stand only above this height, on the ground's own scale: clear of the water.")]
        float m_WaterLevel;

        [SerializeField] int m_Seed = 1;

        [SerializeField, Tooltip("The ground houses stand on.")]
        SimSurface m_Ground;

        static readonly Color[] k_Walls =
        {
            new(0.62f, 0.13f, 0.1f), new(0.62f, 0.13f, 0.1f), new(0.5f, 0.1f, 0.09f), new(0.93f, 0.92f, 0.88f),
            new(0.93f, 0.92f, 0.88f), new(0.85f, 0.65f, 0.28f), new(0.3f, 0.28f, 0.27f),
        };

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
            var b = new SimMeshes.Builder();
            var random = new System.Random(m_Seed);
            float Range(float a, float c) => a + (c - a) * (float)random.NextDouble();

            switch (m_Kind)
            {
                case PropKind.Houses: Houses(b, Range); break;
                case PropKind.FishFarm: FishFarm(b); break;
                case PropKind.Ship: Ship(b); break;
            }

            SimMeshes.Assign(GetComponent<MeshFilter>(), b.ToMesh($"{name}_{m_Kind}"));
        }

        void Houses(SimMeshes.Builder b, System.Func<float, float, float> range)
        {
            int placed = 0;
            for (int attempt = 0; attempt < m_Count * 8 && placed < m_Count; attempt++)
            {
                float angle = range(0f, Mathf.PI * 2f);
                float reach = Mathf.Sqrt(range(0f, 1f));
                float x = m_Centre.x + Mathf.Cos(angle) * m_Spread.x * reach;
                float z = m_Centre.z + Mathf.Sin(angle) * m_Spread.y * reach;

                float y = m_Centre.y;
                if (m_Ground != null)
                {
                    // Only on level ground above the water: a house on a cliff or in the fjord is wrong.
                    float h = Ground(x, z);
                    float slope = Mathf.Max(Mathf.Abs(Ground(x + 6f, z) - Ground(x - 6f, z)), Mathf.Abs(Ground(x, z + 6f) - Ground(x, z - 6f))) / 12f;
                    if (h < m_WaterLevel + 1.5f || slope > 0.3f)
                        continue;
                    y = h;
                }

                float length = m_Size * range(0.75f, 1.4f);
                float width = length * range(0.55f, 0.7f);
                float wall = width * range(0.5f, 0.8f);
                var turn = Quaternion.Euler(0f, range(0f, 180f), 0f);
                var colour = k_Walls[(int)range(0f, k_Walls.Length - 0.001f)];
                var roof = range(0f, 1f) < 0.7f ? new Color(0.2f, 0.19f, 0.2f) : new Color(0.38f, 0.42f, 0.25f);

                // Sunk a little so a slope doesn't leave a house standing on a corner.
                var foot = new Vector3(x, y - wall * 0.3f, z);
                OrientedBox(b, foot + Vector3.up * (wall * 0.65f), new Vector3(width, wall * 1.3f, length), turn, colour);
                Roof(b, foot + Vector3.up * (wall * 1.3f), width * 1.12f, length * 1.08f, width * 0.42f, turn, roof);
                placed++;
            }
        }

        float Ground(float x, float z)
        {
            var world = transform.TransformPoint(new Vector3(x, 0f, z));
            var local = m_Ground.transform.InverseTransformPoint(world);
            local.y = m_Ground.HeightAt(local.x, local.z);
            return transform.InverseTransformPoint(m_Ground.transform.TransformPoint(local)).y;
        }

        void FishFarm(SimMeshes.Builder b)
        {
            int across = Mathf.Max(1, Mathf.RoundToInt(m_Spread.x));
            int along = Mathf.Max(1, Mathf.RoundToInt(m_Spread.y));
            var turn = Quaternion.Euler(0f, m_Yaw, 0f);
            float pitch = m_Size * 1.25f;
            var pipe = new Color(0.1f, 0.1f, 0.11f);
            int rings = across * along;

            for (int i = 0; i < across; i++)
            {
                for (int j = 0; j < along; j++)
                {
                    var offset = new Vector3((i - (across - 1) * 0.5f) * pitch, 0f, (j - (along - 1) * 0.5f) * pitch);
                    var centre = m_Centre + turn * offset;
                    Torus(b, centre, m_Size * 0.5f, m_Size * 0.025f, pipe);

                    // A lamp on the ring, lit by its own band: the rings make an equaliser on the water.
                    float band = rings > 1 ? (i * along + j) / (float)(rings - 1) : 0.5f;
                    var lamp = centre + turn * new Vector3(0f, m_Size * 0.06f, -m_Size * 0.5f);
                    SimMeshes.Icosphere(b, lamp, Vector3.one * (m_Size * 0.07f), new Color(1f, 0.95f, 0.8f, 1f), new Vector2(band, 1f));
                }
            }

            // The feed barge at one end.
            var barge = m_Centre + turn * new Vector3(0f, m_Size * 0.15f, -(along * 0.5f + 0.4f) * pitch);
            OrientedBox(b, barge, new Vector3(m_Size * 0.5f, m_Size * 0.3f, m_Size * 0.35f), turn, new Color(0.85f, 0.85f, 0.82f));
        }

        void Ship(SimMeshes.Builder b)
        {
            var turn = Quaternion.Euler(0f, m_Yaw, 0f);
            float length = m_Size;
            float beam = length * 0.13f;
            var hull = new Color(0.12f, 0.16f, 0.25f);
            var white = new Color(0.95f, 0.95f, 0.93f);

            OrientedBox(b, m_Centre + Vector3.up * (beam * 0.15f), new Vector3(beam, beam * 0.5f, length * 0.86f), turn, hull);
            Roof(b, m_Centre + turn * new Vector3(0f, -beam * 0.1f, length * 0.43f), beam, length * 0.14f, beam * 0.5f,
                turn * Quaternion.Euler(0f, 0f, 180f), hull);

            // Decks stacked and set back, each a row of lit windows along the spectrum.
            const int decks = 5;
            for (int d = 0; d < decks; d++)
            {
                float up = beam * (0.45f + 0.22f * d);
                float deck = length * (0.78f - 0.07f * d);
                var centre = m_Centre + turn * new Vector3(0f, up, -length * 0.02f * d);
                OrientedBox(b, centre, new Vector3(beam * (0.98f - 0.04f * d), beam * 0.2f, deck), turn, white);

                const int windows = 10;
                for (int w = 0; w < windows; w++)
                {
                    float along = (w / (float)(windows - 1) - 0.5f) * deck * 0.9f;
                    float band = (w + d * 0.5f) / (windows + decks * 0.5f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var window = centre + turn * new Vector3(side * beam * (0.49f - 0.02f * d), 0f, along);
                        OrientedBox(b, window, new Vector3(beam * 0.03f, beam * 0.06f, deck * 0.05f), turn, new Color(1f, 0.85f, 0.55f, 1f),
                            new Vector2(band, 1f));
                    }
                }
            }

            var funnel = m_Centre + turn * new Vector3(0f, beam * 1.75f, -length * 0.22f);
            OrientedBox(b, funnel, new Vector3(beam * 0.25f, beam * 0.45f, beam * 0.5f), turn, white);
            OrientedBox(b, funnel + Vector3.up * (beam * 0.25f), new Vector3(beam * 0.26f, beam * 0.08f, beam * 0.51f), turn, hull);
        }

        static readonly Vector3[] k_Corners =
        {
            new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
            new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
        };

        static readonly int[][] k_Faces =
        {
            new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
            new[] { 3, 7, 6, 2 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 },
        };

        /// <summary>A turned box, each face its own vertices so the edges stay sharp.</summary>
        static void OrientedBox(SimMeshes.Builder b, Vector3 centre, Vector3 size, Quaternion turn, Color colour, Vector2 band = default)
        {
            var half = size * 0.5f;
            foreach (var face in k_Faces)
            {
                int start = b.Count;
                foreach (int corner in face)
                    b.Add(centre + turn * Vector3.Scale(k_Corners[corner], half), colour, Vector2.zero, band);
                b.Triangle(start, start + 1, start + 2);
                b.Triangle(start, start + 2, start + 3);
            }
        }

        /// <summary>A pitched roof: two slopes meeting along the ridge and a gable at each end, standing on <paramref name="foot"/>.</summary>
        static void Roof(SimMeshes.Builder b, Vector3 foot, float width, float length, float height, Quaternion turn, Color colour)
        {
            Vector3 P(float x, float y, float z) => foot + turn * new Vector3(x, y, z);
            float w = width * 0.5f, l = length * 0.5f;
            var a = P(-w, 0f, -l); var c = P(w, 0f, -l); var d = P(w, 0f, l); var e = P(-w, 0f, l);
            var r0 = P(0f, height, -l); var r1 = P(0f, height, l);

            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int s = b.Count;
                b.Add(p0, colour); b.Add(p1, colour); b.Add(p2, colour); b.Add(p3, colour);
                b.Triangle(s, s + 1, s + 2);
                b.Triangle(s, s + 2, s + 3);
            }

            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int s = b.Count;
                b.Add(p0, colour); b.Add(p1, colour); b.Add(p2, colour);
                b.Triangle(s, s + 1, s + 2);
            }

            Quad(a, r0, r1, e);
            Quad(c, d, r1, r0);
            Tri(a, c, r0);
            Tri(d, e, r1);
        }

        /// <summary>A ring of pipe lying flat on the water.</summary>
        static void Torus(SimMeshes.Builder b, Vector3 centre, float radius, float tube, Color colour)
        {
            const int around = 32, sides = 6;
            int start = b.Count;
            for (int i = 0; i < around; i++)
            {
                float a = i / (float)around * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int j = 0; j < sides; j++)
                {
                    float t = j / (float)sides * Mathf.PI * 2f;
                    b.Add(centre + outward * (radius + Mathf.Cos(t) * tube) + Vector3.up * (Mathf.Sin(t) * tube), colour);
                }
            }

            for (int i = 0; i < around; i++)
                b.Band(start + i * sides, start + (i + 1) % around * sides, sides);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// Gulls wheeling over the sea, each on its own slow circle, banked into the turn. They glide
    /// while the middle of the music is quiet and beat their wings, climbing a little, as the mids come
    /// up: a verse full of voices and guitars sets the sky flapping, and a breakdown lets it glide.
    /// </summary>
    /// <remarks>
    /// One small mesh, rebuilt every frame: a body, a tail and two jointed wings to a gull, on the land
    /// material, white with grey wings and black tips. Each circle stays well clear of the room.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimGulls : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;
        [SerializeField, Range(1, 24)] int m_Count = 6;

        [SerializeField, Tooltip("Nearest and furthest, metres from the room to the middle of a gull's circle.")]
        Vector2 m_Distance = new(24f, 110f);

        [SerializeField, Tooltip("Degrees round from straight ahead, either side, that circles are centred within.")]
        float m_Spread = 110f;

        [SerializeField, Tooltip("Lowest and highest, degrees above the horizon from the room, of the middle of a circle.")]
        Vector2 m_Elevation = new(5f, 15f);

        [SerializeField, Tooltip("Radius of a gull's circle, metres, smallest and largest.")]
        Vector2 m_Circle = new(8f, 30f);

        [SerializeField, Tooltip("Metres a second a gull flies.")]
        float m_Speed = 8f;

        [SerializeField, Tooltip("Wingspan, metres.")]
        float m_Span = 1.5f;

        [SerializeField, Tooltip("Wingbeats a second.")]
        float m_Wingbeat = 2.6f;

        [SerializeField, Tooltip("Metres a gull climbs when the mids are full.")]
        float m_Climb = 4f;

        [SerializeField] int m_Seed = 3;

        [Header("Colours")]
        [SerializeField] Color m_Body = new(0.96f, 0.96f, 0.95f);
        [SerializeField] Color m_Wing = new(0.72f, 0.75f, 0.8f);
        [SerializeField] Color m_Tip = new(0.12f, 0.12f, 0.14f);

        /// <summary>Half the span of the gull as it is drawn here, before scaling to the wingspan.</summary>
        const float k_HalfSpan = 0.88f;

        struct Gull
        {
            public Vector3 Centre;
            public float Radius;
            public float Turn;
            public float Angle;
            public float Beat;
            public float Rate;
            public float Eager;
            public float Bob;
        }

        Gull[] m_Gulls;
        Mesh m_Mesh;
        float m_Flap;

        readonly List<Vector3> m_Vertices = new();
        readonly List<Color> m_Colours = new();
        readonly List<Vector2> m_Uv2 = new();
        readonly List<int> m_Triangles = new();

        void Awake()
        {
            var random = new System.Random(m_Seed);
            float Range(float a, float c) => a + (c - a) * (float)random.NextDouble();

            m_Gulls = new Gull[Mathf.Max(1, m_Count)];
            for (int i = 0; i < m_Gulls.Length; i++)
            {
                float azimuth = Range(-m_Spread, m_Spread) * Mathf.Deg2Rad;
                float distance = Range(m_Distance.x, m_Distance.y);
                float elevation = Range(m_Elevation.x, m_Elevation.y) * Mathf.Deg2Rad;
                float radius = Mathf.Max(3f, Mathf.Min(Range(m_Circle.x, m_Circle.y), distance - 15f));

                m_Gulls[i] = new Gull
                {
                    Centre = new Vector3(Mathf.Sin(azimuth) * distance, 1.6f + Mathf.Tan(elevation) * distance, Mathf.Cos(azimuth) * distance),
                    Radius = radius,
                    Turn = random.NextDouble() < 0.5 ? -1f : 1f,
                    Angle = Range(0f, Mathf.PI * 2f),
                    Beat = Range(0f, Mathf.PI * 2f),
                    Rate = Range(0.85f, 1.15f),
                    Eager = Range(0.55f, 1f),
                    Bob = Range(0f, Mathf.PI * 2f),
                };
            }

            m_Mesh = SimMeshes.NewMesh($"{name}_Gulls", m_Gulls.Length * 48);
            m_Mesh.MarkDynamic();
            SimMeshes.Assign(GetComponent<MeshFilter>(), m_Mesh);
        }

        void OnDestroy() => SimMeshes.Destroy(m_Mesh);

        void Update()
        {
            if (m_Gulls == null || m_Mesh == null)
                return;

            float dt = Time.deltaTime;
            float time = Time.time;

            // The mids, eased again: wings answer the music's middle over half a second, not a frame.
            float mid = m_Simulation != null ? m_Simulation.Mid : 0f;
            float target = Mathf.Clamp01((mid - 0.15f) * 1.8f);
            m_Flap += (target - m_Flap) * (1f - Mathf.Exp(-dt / 0.5f));

            m_Vertices.Clear();
            m_Colours.Clear();
            m_Uv2.Clear();
            m_Triangles.Clear();

            float scale = m_Span / (2f * k_HalfSpan);
            for (int i = 0; i < m_Gulls.Length; i++)
            {
                ref var gull = ref m_Gulls[i];
                gull.Angle += gull.Turn * m_Speed / gull.Radius * dt;
                gull.Beat += dt * m_Wingbeat * gull.Rate * Mathf.PI * 2f;
                float flap = m_Flap * gull.Eager;

                var round = new Vector3(Mathf.Sin(gull.Angle), 0f, Mathf.Cos(gull.Angle));
                var position = gull.Centre + round * gull.Radius +
                               Vector3.up * (Mathf.Sin(time * 0.37f + gull.Bob) * 0.8f + m_Climb * flap);
                var heading = new Vector3(Mathf.Cos(gull.Angle), 0f, -Mathf.Sin(gull.Angle)) * gull.Turn;

                // Banked into the turn: up leans toward the middle of the circle.
                var up = (Vector3.up - round * 0.35f).normalized;
                var rotation = Quaternion.LookRotation(heading, up);

                // Gliding, the wings hold a shallow M; flapping, the hand follows the arm a beat behind.
                float amplitude = Mathf.Lerp(0.05f, 0.7f, flap);
                float shoulder = 0.18f + amplitude * Mathf.Sin(gull.Beat);
                float wrist = -0.35f - amplitude * 0.6f * Mathf.Sin(gull.Beat - 0.9f);
                AddGull(position, rotation, scale, shoulder, wrist);
            }

            m_Mesh.Clear();
            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.SetColors(m_Colours);
            m_Mesh.SetUVs(1, m_Uv2);
            m_Mesh.SetTriangles(m_Triangles, 0);
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateBounds();
        }

        /// <summary>One gull, built facing +z with its wings raised by <paramref name="shoulder"/> and the hands bent by <paramref name="wrist"/>, radians.</summary>
        void AddGull(Vector3 at, Quaternion rotation, float scale, float shoulder, float wrist)
        {
            Vector3 P(Vector3 v) => at + rotation * (v * scale);

            // The body: a spindle from beak to tail.
            int beak = Vertex(P(new Vector3(0f, 0.01f, 0.32f)), m_Body);
            int top = Vertex(P(new Vector3(0f, 0.07f, 0.04f)), m_Body);
            int right = Vertex(P(new Vector3(0.07f, 0f, 0.04f)), m_Body);
            int belly = Vertex(P(new Vector3(0f, -0.06f, 0.04f)), m_Body);
            int left = Vertex(P(new Vector3(-0.07f, 0f, 0.04f)), m_Body);
            int tail = Vertex(P(new Vector3(0f, 0.02f, -0.3f)), m_Body);
            Triangle(beak, top, right);
            Triangle(beak, right, belly);
            Triangle(beak, belly, left);
            Triangle(beak, left, top);
            Triangle(tail, right, top);
            Triangle(tail, belly, right);
            Triangle(tail, left, belly);
            Triangle(tail, top, left);

            Quad(P(new Vector3(-0.04f, 0.02f, -0.24f)), P(new Vector3(0.04f, 0.02f, -0.24f)),
                P(new Vector3(0.09f, 0.02f, -0.42f)), P(new Vector3(-0.09f, 0.02f, -0.42f)), m_Body);

            // The wings: shoulder to wrist, wrist to hand, and the black tip swept back.
            foreach (float side in new[] { -1f, 1f })
            {
                var arm = new Vector3(side * Mathf.Cos(shoulder), Mathf.Sin(shoulder), 0f);
                var hand = new Vector3(side * Mathf.Cos(shoulder + wrist), Mathf.Sin(shoulder + wrist), 0f);

                var lead = new Vector3(side * 0.05f, 0.02f, 0.1f);
                var trail = new Vector3(side * 0.05f, 0.02f, -0.12f);
                var wristLead = lead + arm * 0.32f;
                var wristTrail = trail + arm * 0.32f + new Vector3(0f, 0f, 0.02f);
                var handLead = wristLead + hand * 0.27f + new Vector3(0f, 0f, -0.05f);
                var handTrail = wristTrail + hand * 0.27f + new Vector3(0f, 0f, 0.02f);
                var tip = wristLead + hand * 0.51f + new Vector3(0f, 0f, -0.16f);

                Quad(P(lead), P(wristLead), P(wristTrail), P(trail), m_Wing);
                Quad(P(wristLead), P(handLead), P(handTrail), P(wristTrail), m_Wing);
                Triangle(Vertex(P(handLead), m_Tip), Vertex(P(tip), m_Tip), Vertex(P(handTrail), m_Tip));
            }
        }

        int Vertex(Vector3 position, Color colour)
        {
            colour.a = 1f;
            m_Vertices.Add(position);
            m_Colours.Add(colour);
            m_Uv2.Add(Vector2.zero);
            return m_Vertices.Count - 1;
        }

        void Triangle(int a, int b, int c)
        {
            m_Triangles.Add(a);
            m_Triangles.Add(b);
            m_Triangles.Add(c);
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
        {
            int i = Vertex(a, colour);
            Vertex(b, colour);
            Vertex(c, colour);
            Vertex(d, colour);
            Triangle(i, i + 1, i + 2);
            Triangle(i, i + 2, i + 3);
        }
    }
}

using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The top of a mesa made of cubes, coming apart with the music — the film's world dissolving
    /// into blocks, turned into an equaliser. Each column of cubes across the mesa is one band, bass
    /// at the left as seen from the room; as a band rises, cubes break off the top of its column, pale
    /// and glowing, and float up and turn, the louder the higher; as it falls they settle back into
    /// the rock. A beat throws the free ones a little higher.
    /// </summary>
    /// <remarks>
    /// The cubes are a particle system written by hand (<see cref="ManualParticles"/>) drawing the
    /// built-in cube mesh, on the land material, so a settled cube is coloured by the strata at its
    /// height like the rock under it, and a free one (particle alpha 0) is its own light colour.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class VoxelMesa : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Range(4, 48), Tooltip("Columns across the mesa: one band each.")]
        int m_Columns = 24;

        [SerializeField, Range(1, 12), Tooltip("Cubes deep, away from the room.")]
        int m_Rows = 4;

        [SerializeField, Range(1, 12), Tooltip("Cubes tall.")]
        int m_Layers = 4;

        [SerializeField, Tooltip("Metres along a cube's edge.")]
        float m_Cube = 8.5f;

        [SerializeField, Range(0f, 0.3f), Tooltip("Gap between cubes, as a fraction of a cube, so the blocks read.")]
        float m_Gap = 0.06f;

        [SerializeField, Tooltip("Metres the top cube of a column rises at full level.")]
        float m_Lift = 70f;

        [SerializeField, Range(0f, 1f), Tooltip("How much the cubes of one column differ in how far they rise.")]
        float m_Ragged = 0.5f;

        [SerializeField, Tooltip("Degrees a second a free cube turns.")]
        float m_Spin = 45f;

        [SerializeField, Tooltip("Metres a second a beat throws a free cube upward, at full strength.")]
        float m_BeatKick = 14f;

        [SerializeField, Tooltip("How quickly a cube answers its column: the spring's angular frequency.")]
        float m_Response = 3.5f;

        [SerializeField] Color m_FreeColour = new(0.93f, 0.9f, 1f);

        struct Cube
        {
            public Vector3 Home;
            public int Column;
            public int FromTop;
            public float Rand;
            public Vector3 Axis;
            public float Lift;
            public float Velocity;
            public float Angle;
        }

        ParticleSystem m_System;
        ParticleSystemRenderer m_Renderer;
        ParticleSystem.Particle[] m_Particles;
        Cube[] m_Cubes;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            ManualParticles.Configure(m_System, m_Renderer, true);

            var main = m_System.main;
            main.startRotation3D = true;
            m_Renderer.renderMode = ParticleSystemRenderMode.Mesh;
            m_Renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            m_Renderer.alignment = ParticleSystemRenderSpace.Local;
            m_Renderer.enableGPUInstancing = false;

            Allocate();
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        void Allocate()
        {
            int count = m_Columns * m_Rows * m_Layers;
            ManualParticles.Reserve(m_System, count);
            m_Particles = new ParticleSystem.Particle[count];
            m_Cubes = new Cube[count];

            var random = new System.Random(77);
            int i = 0;
            for (int column = 0; column < m_Columns; column++)
                for (int row = 0; row < m_Rows; row++)
                    for (int layer = 0; layer < m_Layers; layer++)
                    {
                        float x = (column - (m_Columns - 1) * 0.5f) * m_Cube;
                        float z = (row - (m_Rows - 1) * 0.5f) * m_Cube;
                        float y = (layer + 0.5f) * m_Cube;

                        m_Cubes[i] = new Cube
                        {
                            Home = new Vector3(x, y, z),
                            Column = column,
                            FromTop = m_Layers - 1 - layer,
                            Rand = (float)random.NextDouble(),
                            Axis = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f,
                                (float)random.NextDouble() - 0.5f).normalized,
                        };

                        m_Particles[i].startLifetime = ManualParticles.Lifetime;
                        m_Particles[i].remainingLifetime = ManualParticles.Lifetime;
                        i++;
                    }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Cubes == null)
                return;

            bool beat = m_Simulation != null && m_Simulation.Beat;
            float strength = m_Simulation != null ? m_Simulation.BeatStrength : 0f;
            float w = m_Response;
            float size = m_Cube * (1f - m_Gap);
            float time = Time.time;

            for (int i = 0; i < m_Cubes.Length; i++)
            {
                ref var cube = ref m_Cubes[i];
                float level = m_Simulation != null ? m_Simulation.Band(cube.Column / (float)Mathf.Max(1, m_Columns - 1)) : 0f;

                // How far past this cube the column's level reaches: above zero, it has broken free.
                float freed = level * m_Layers - cube.FromTop - cube.Rand * 0.35f;
                float free = Mathf.Clamp01(freed);
                float target = freed > 0f ? freed * (m_Lift / m_Layers) * (1f + m_Ragged * (cube.Rand - 0.5f)) : 0f;

                // A critically damped spring, so a cube eases up and settles back without bouncing.
                float accel = w * w * (target - cube.Lift) - 2f * w * cube.Velocity;
                cube.Velocity += accel * dt;
                if (beat && free > 0.2f)
                    cube.Velocity += m_BeatKick * strength * (0.5f + cube.Rand);
                cube.Lift = Mathf.Max(0f, cube.Lift + cube.Velocity * dt);

                float away = Mathf.Clamp01(cube.Lift / (m_Lift * 0.25f));
                cube.Angle += m_Spin * away * (0.4f + cube.Rand) * dt;
                if (away <= 0.001f)
                    cube.Angle = Mathf.MoveTowardsAngle(cube.Angle, 0f, 90f * dt);

                var drift = new Vector3(Mathf.Sin(cube.Rand * 6.283f + time * 0.3f), 0f, Mathf.Cos(cube.Rand * 4.1f + time * 0.23f));
                var p = m_Particles[i];
                p.position = cube.Home + Vector3.up * cube.Lift + drift * (cube.Lift * 0.12f);
                p.rotation3D = cube.Axis * cube.Angle;
                p.startSize3D = Vector3.one * (size * (1f - 0.3f * away));

                var colour = Color.Lerp(Color.white, m_FreeColour, away);
                colour.a = 1f - away;
                p.startColor = colour;
                p.remainingLifetime = ManualParticles.Lifetime;
                m_Particles[i] = p;
            }

            m_System.SetParticles(m_Particles, m_Particles.Length);
        }
    }
}

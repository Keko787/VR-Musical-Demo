using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// Fireworks over the sea at dusk, set off by the music. A strong beat bursts a shell — on the
    /// beat, not after a launch, so the bang and the light land together — and its colour says which
    /// part of the music made it: warm reds and golds when the bass leads, greens when the mids do,
    /// blues and violets for the treble. The sparks fly out, slow, fall and flicker out, and each
    /// burst is mirrored in the water below.
    /// </summary>
    /// <remarks>
    /// Shells only follow the analyser's beat, which is capped at three a second, and no shell bursts
    /// sooner than <see cref="m_MinInterval"/> after the last — longer in low intensity. Each burst is
    /// a small bright patch of sky, never a flash of the whole wall.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class SimFireworks : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Tooltip("The bursts mirrored under the sea, drawn on the water only.")]
        ParticleSystem m_Reflections;

        [Header("Where")]
        [SerializeField, Tooltip("Metres across (x) and deep (z) of the patch of sky shells burst in, centred on this object, which is at sea level.")]
        Vector2 m_Area = new(500f, 300f);

        [SerializeField, Tooltip("Lowest and highest burst, metres above the sea.")]
        Vector2 m_Altitude = new(110f, 230f);

        [Header("When")]
        [SerializeField, Range(0f, 1f), Tooltip("How strong a beat must be to set a shell off.")]
        float m_MinStrength = 0.4f;

        [SerializeField, Tooltip("Fewest seconds between bursts.")]
        float m_MinInterval = 0.6f;

        [SerializeField] float m_LowIntensityInterval = 1.4f;

        [Header("Bursts")]
        [SerializeField, Range(1, 12), Tooltip("Shells alight at once; the oldest goes out when another is needed.")]
        int m_Shells = 6;

        [SerializeField, Range(20, 400)] int m_Sparks = 140;
        [SerializeField, Tooltip("Metres a second the sparks leave the burst.")] float m_Speed = 38f;
        [SerializeField, Tooltip("Seconds a burst lasts.")] float m_Life = 2.4f;
        [SerializeField] float m_SparkSize = 2.6f;
        [SerializeField] float m_Gravity = 9f;
        [SerializeField] float m_Drag = 1.6f;
        [SerializeField, ColorUsage(false, true)] Color m_Glow = new(2f, 2f, 2f);

        struct Shell
        {
            public Vector3 Centre;
            public float Born;
            public Color First;
            public Color Second;
            public float Scale;
            public Quaternion Turn;
        }

        static readonly int k_Color = Shader.PropertyToID("_Color");

        ParticleSystem m_System;
        ParticleSystem.Particle[] m_Particles;
        ParticleSystem.Particle[] m_Mirrored;
        Vector3[] m_Directions;
        Shell[] m_Bursts;
        MaterialPropertyBlock m_Block;
        System.Random m_Random = new(42);
        float m_LastBurst = -999f;
        int m_Next;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Block = new MaterialPropertyBlock();
            int count = m_Shells * m_Sparks;

            ManualParticles.Configure(m_System, GetComponent<ParticleSystemRenderer>());
            ManualParticles.Reserve(m_System, count);
            m_Particles = Fresh(count);

            if (m_Reflections != null)
            {
                ManualParticles.Configure(m_Reflections, m_Reflections.GetComponent<ParticleSystemRenderer>(), true);
                ManualParticles.Reserve(m_Reflections, count);
            }
            m_Mirrored = Fresh(count);

            // Spark directions evenly round a sphere; each shell turns the set by its own rotation.
            m_Directions = new Vector3[m_Sparks];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < m_Sparks; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / m_Sparks;
                float r = Mathf.Sqrt(1f - y * y);
                m_Directions[i] = new Vector3(Mathf.Cos(golden * i) * r, y, Mathf.Sin(golden * i) * r);
            }

            m_Bursts = new Shell[m_Shells];
            for (int i = 0; i < m_Bursts.Length; i++)
                m_Bursts[i].Born = -999f;
        }

        static ParticleSystem.Particle[] Fresh(int count)
        {
            var particles = new ParticleSystem.Particle[count];
            for (int i = 0; i < count; i++)
            {
                particles[i].startLifetime = ManualParticles.Lifetime;
                particles[i].remainingLifetime = ManualParticles.Lifetime;
            }
            return particles;
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
            if (m_Reflections != null && !m_Reflections.isPlaying)
                m_Reflections.Play();
        }

        float Range(float a, float b) => a + (b - a) * (float)m_Random.NextDouble();

        void Update()
        {
            if (m_Particles == null)
                return;

            float now = Time.time;
            bool low = m_Simulation != null && m_Simulation.LowIntensity;
            float interval = low ? m_LowIntensityInterval : m_MinInterval;

            if (m_Simulation != null && m_Simulation.Beat && m_Simulation.BeatStrength >= m_MinStrength &&
                now - m_LastBurst >= interval)
                Burst(now);

            for (int s = 0; s < m_Bursts.Length; s++)
            {
                var shell = m_Bursts[s];
                float age = now - shell.Born;
                bool alight = age >= 0f && age < m_Life;
                float fade = alight ? Mathf.Pow(1f - age / m_Life, 1.5f) : 0f;
                float travel = (1f - Mathf.Exp(-m_Drag * age)) / m_Drag * m_Speed * shell.Scale;
                float fall = 0.5f * m_Gravity * age * age * 0.6f;
                float flash = alight ? 1f + 1.5f * Mathf.Exp(-age * 12f) : 0f;

                for (int k = 0; k < m_Sparks; k++)
                {
                    int i = s * m_Sparks + k;
                    var p = m_Particles[i];

                    if (!alight)
                    {
                        p.startSize = 0f;
                        p.remainingLifetime = ManualParticles.Lifetime;
                        m_Particles[i] = p;
                        var hidden = m_Mirrored[i];
                        hidden.startSize3D = Vector3.zero;
                        hidden.remainingLifetime = ManualParticles.Lifetime;
                        m_Mirrored[i] = hidden;
                        continue;
                    }

                    var direction = shell.Turn * m_Directions[k];
                    var position = shell.Centre + direction * travel + Vector3.down * fall;

                    // Late in its life a spark flickers as it burns out.
                    float flicker = age > m_Life * 0.55f ? 0.55f + 0.45f * Mathf.Sin(now * 31f + k * 2.3f) : 1f;
                    var colour = (k % 3 == 0 ? shell.Second : shell.First) * (fade * flicker * flash);
                    float size = m_SparkSize * shell.Scale * (0.6f + 0.4f * fade);

                    p.position = position;
                    p.startSize = size;
                    p.startColor = colour;
                    p.remainingLifetime = ManualParticles.Lifetime;
                    m_Particles[i] = p;

                    var m = m_Mirrored[i];
                    m.position = new Vector3(position.x, -position.y, position.z);
                    m.startSize3D = new Vector3(size * 0.8f, size * 2.6f, 1f);
                    m.startColor = colour * 0.4f;
                    m.remainingLifetime = ManualParticles.Lifetime;
                    m_Mirrored[i] = m;
                }
            }

            float intensity = m_Simulation != null ? m_Simulation.Intensity : 1f;
            Write(m_System, m_Particles, m_Glow * intensity);
            Write(m_Reflections, m_Mirrored, m_Glow * intensity);
        }

        /// <summary>A shell bursts now, its colours from whichever part of the music leads.</summary>
        void Burst(float now)
        {
            m_LastBurst = now;
            float bass = m_Simulation.Bass, mid = m_Simulation.Mid, high = m_Simulation.High;

            Color first, second;
            if (bass >= mid && bass >= high)
            {
                first = new Color(1f, 0.25f, 0.12f);
                second = new Color(1f, 0.72f, 0.2f);
            }
            else if (mid >= high)
            {
                first = new Color(0.35f, 1f, 0.4f);
                second = new Color(1f, 0.85f, 0.3f);
            }
            else
            {
                first = new Color(0.35f, 0.5f, 1f);
                second = new Color(0.85f, 0.45f, 1f);
            }

            m_Bursts[m_Next] = new Shell
            {
                Centre = new Vector3(Range(-0.5f, 0.5f) * m_Area.x, Range(m_Altitude.x, m_Altitude.y), Range(-0.5f, 0.5f) * m_Area.y),
                Born = now,
                First = first,
                Second = second,
                Scale = 0.7f + 0.6f * m_Simulation.BeatStrength,
                Turn = Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f)),
            };
            m_Next = (m_Next + 1) % m_Bursts.Length;
        }

        void Write(ParticleSystem system, ParticleSystem.Particle[] particles, Color colour)
        {
            if (system == null)
                return;

            system.SetParticles(particles, particles.Length);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_Color, colour);
            renderer.SetPropertyBlock(m_Block);
        }
    }
}

using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// Shooting stars across a space world's sky, set off by strong beats: a streak of light falling
    /// across the stars and burning out in under a second. Rate-limited on top of the analyser's own
    /// beat cap, and rarer in low intensity.
    /// </summary>
    /// <remarks>
    /// Drawn as stretched billboards — the particle's velocity sets the streak's length and direction,
    /// and the renderer works that out per camera, so a meteor crossing from one wall to the next
    /// stays one straight streak.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class SimMeteors : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Range(0f, 1f), Tooltip("How strong a beat must be to send a meteor across.")]
        float m_MinStrength = 0.5f;

        [SerializeField, Tooltip("Fewest seconds between meteors.")]
        float m_MinInterval = 0.8f;

        [SerializeField] float m_LowIntensityInterval = 2f;
        [SerializeField, Range(1, 16)] int m_Max = 6;

        [SerializeField, Tooltip("Metres from the room they streak across at.")]
        float m_Distance = 2500f;

        [SerializeField, Tooltip("Degrees above the horizon they start from.")]
        Vector2 m_Elevation = new(14f, 30f);

        [SerializeField, Tooltip("Degrees either side of straight ahead they start within.")]
        float m_Spread = 75f;

        [SerializeField, Tooltip("Metres a second.")] float m_Speed = 1400f;
        [SerializeField, Tooltip("Seconds a meteor lasts.")] float m_Life = 0.7f;
        [SerializeField, Tooltip("Metres across the streak.")] float m_Width = 6f;
        [SerializeField, ColorUsage(false, true)] Color m_Colour = new(1.6f, 1.6f, 2f);

        struct Meteor
        {
            public Vector3 Start;
            public Vector3 Velocity;
            public float Born;
        }

        static readonly int k_Color = Shader.PropertyToID("_Color");

        ParticleSystem m_System;
        ParticleSystemRenderer m_Renderer;
        ParticleSystem.Particle[] m_Particles;
        Meteor[] m_Meteors;
        MaterialPropertyBlock m_Block;
        System.Random m_Random = new(17);
        float m_Last = -999f;
        int m_Next;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            m_Block = new MaterialPropertyBlock();
            ManualParticles.Configure(m_System, m_Renderer);
            m_Renderer.renderMode = ParticleSystemRenderMode.Stretch;
            m_Renderer.velocityScale = 0.08f;
            m_Renderer.lengthScale = 1f;
            ManualParticles.Reserve(m_System, m_Max);

            m_Particles = new ParticleSystem.Particle[m_Max];
            m_Meteors = new Meteor[m_Max];
            for (int i = 0; i < m_Max; i++)
            {
                m_Meteors[i].Born = -999f;
                m_Particles[i].startLifetime = ManualParticles.Lifetime;
                m_Particles[i].remainingLifetime = ManualParticles.Lifetime;
            }
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        float Range(float a, float b) => a + (b - a) * (float)m_Random.NextDouble();

        void Update()
        {
            if (m_Particles == null)
                return;

            float now = Time.time;
            bool low = m_Simulation != null && m_Simulation.LowIntensity;
            if (m_Simulation != null && m_Simulation.Beat && m_Simulation.BeatStrength >= m_MinStrength &&
                now - m_Last >= (low ? m_LowIntensityInterval : m_MinInterval))
                Launch(now);

            for (int i = 0; i < m_Meteors.Length; i++)
            {
                var meteor = m_Meteors[i];
                float age = now - meteor.Born;
                float fade = age >= 0f && age < m_Life ? Mathf.Clamp01(age / 0.08f) * Mathf.Pow(1f - age / m_Life, 1.5f) : 0f;

                var p = m_Particles[i];
                p.position = meteor.Start + meteor.Velocity * Mathf.Max(age, 0f);
                p.velocity = meteor.Velocity;
                p.startSize = fade > 0f ? m_Width : 0f;
                p.startColor = Color.white * fade;
                p.remainingLifetime = ManualParticles.Lifetime;
                m_Particles[i] = p;
            }

            m_System.SetParticles(m_Particles, m_Particles.Length);
            float intensity = m_Simulation != null ? m_Simulation.Intensity : 1f;
            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_Color, m_Colour * intensity);
            m_Renderer.SetPropertyBlock(m_Block);
        }

        /// <summary>A meteor starts high in the sky ahead and falls away to one side.</summary>
        void Launch(float now)
        {
            m_Last = now;
            float azimuth = Range(-m_Spread, m_Spread) * Mathf.Deg2Rad;
            float elevation = Range(m_Elevation.x, m_Elevation.y) * Mathf.Deg2Rad;
            float c = Mathf.Cos(elevation);
            var start = new Vector3(Mathf.Sin(azimuth) * c, Mathf.Sin(elevation), Mathf.Cos(azimuth) * c) * m_Distance;

            var across = new Vector3(Mathf.Cos(azimuth), 0f, -Mathf.Sin(azimuth)) * (m_Random.NextDouble() < 0.5 ? -1f : 1f);
            var direction = (across + Vector3.down * Range(0.3f, 0.7f)).normalized;

            m_Meteors[m_Next] = new Meteor { Start = start, Velocity = direction * m_Speed, Born = now };
            m_Next = (m_Next + 1) % m_Meteors.Length;
        }
    }
}

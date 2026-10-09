using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>Which way motes drift.</summary>
    public enum MoteMotion
    {
        /// <summary>Up: sparks round the light pillar, flecks lifting off the cloud sea.</summary>
        Rise,

        /// <summary>Down: snow.</summary>
        Fall,
    }

    /// <summary>
    /// Specks of light drifting through a box of the world: the treble made visible. Each mote is a
    /// glint whose brightness is the treble's level and which twinkles on its own clock, so hi-hats
    /// and cymbals sparkle across the landscape while the bass moves the big things.
    /// </summary>
    /// <remarks>
    /// Written by hand into a particle system (<see cref="ManualParticles"/>) on the visualizer's
    /// additive radial glow. Motes inside the glass room's own volume are hidden: the room is the
    /// window cameras' eye, and a fleck there would be drawn on the outside of glass it is inside.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class SimMotes : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;
        [SerializeField] MoteMotion m_Motion;
        [SerializeField, Range(1, 8000)] int m_Count = 600;

        [SerializeField, Tooltip("The box the motes drift in, metres: centred on this object across, rising from it.")]
        Vector3 m_Volume = new(500f, 400f, 500f);

        [SerializeField, Tooltip("Metres a second up or down.")]
        float m_Speed = 6f;

        [SerializeField, Tooltip("Metres of sideways wander.")]
        float m_Sway = 3f;

        [SerializeField, Tooltip("Metres across a mote.")]
        float m_Size = 1.6f;

        [SerializeField, ColorUsage(false, true)] Color m_Colour = new(1.5f, 1.4f, 1.1f);

        [SerializeField, Range(0f, 1f), Tooltip("Brightness with no treble.")]
        float m_Rest = 0.15f;

        [SerializeField, Tooltip("Brightness added at full treble.")]
        float m_ByTreble = 1.2f;

        [SerializeField, Range(0f, 1f)] float m_Twinkle = 0.6f;

        [SerializeField, Tooltip("The glass room's place outside, whose volume the motes stay out of.")]
        Transform m_Room;

        [SerializeField] Vector3 m_RoomHalfSize = new(2.2f, 1.5f, 1.6f);
        [SerializeField] Vector3 m_RoomCentre = new(0f, 1.15f, -0.08f);

        static readonly int k_Color = Shader.PropertyToID("_Color");

        ParticleSystem m_System;
        ParticleSystemRenderer m_Renderer;
        ParticleSystem.Particle[] m_Particles;
        Vector4[] m_Seeds;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            m_Block = new MaterialPropertyBlock();
            ManualParticles.Configure(m_System, m_Renderer);
            ManualParticles.Reserve(m_System, m_Count);

            m_Particles = new ParticleSystem.Particle[m_Count];
            m_Seeds = new Vector4[m_Count];
            var random = new System.Random(m_Count * 31 + 7);
            for (int i = 0; i < m_Count; i++)
            {
                m_Seeds[i] = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(),
                    (float)random.NextDouble());
                m_Particles[i].startLifetime = ManualParticles.Lifetime;
                m_Particles[i].remainingLifetime = ManualParticles.Lifetime;
            }
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        void Update()
        {
            if (m_Particles == null)
                return;

            float time = Time.time;
            float treble = m_Simulation != null ? m_Simulation.High : 0f;
            float intensity = m_Simulation != null ? m_Simulation.Intensity : 1f;
            float glow = (m_Rest + m_ByTreble * treble) * intensity;
            float height = Mathf.Max(0.01f, m_Volume.y);
            bool exclude = m_Room != null;

            for (int i = 0; i < m_Particles.Length; i++)
            {
                var seed = m_Seeds[i];
                float speed = m_Speed * (0.6f + 0.8f * seed.w);
                float travel = Mathf.Repeat(seed.y * height + speed * time, height);
                float y = m_Motion == MoteMotion.Rise ? travel : height - travel;
                float x = (seed.x - 0.5f) * m_Volume.x + Mathf.Sin(time * 0.4f + seed.z * 6.283f) * m_Sway;
                float z = (seed.z - 0.5f) * m_Volume.z + Mathf.Cos(time * 0.33f + seed.x * 6.283f) * m_Sway;
                var position = new Vector3(x, y, z);

                // Fade in and out at the ends of the box rather than popping.
                float f = y / height;
                float ends = Mathf.Clamp01(f * 8f) * Mathf.Clamp01((1f - f) * 8f);
                float twinkle = 1f - m_Twinkle * (0.5f + 0.5f * Mathf.Sin(time * (2f + 5f * seed.w) + seed.y * 30f));

                float size = m_Size * (0.6f + 0.8f * seed.x);
                if (exclude)
                {
                    var inRoom = m_Room.InverseTransformPoint(transform.TransformPoint(position)) - m_RoomCentre;
                    if (Mathf.Abs(inRoom.x) < m_RoomHalfSize.x && Mathf.Abs(inRoom.y) < m_RoomHalfSize.y &&
                        Mathf.Abs(inRoom.z) < m_RoomHalfSize.z)
                        size = 0f;
                }

                var p = m_Particles[i];
                p.position = position;
                p.startSize = size;
                p.startColor = Color.white * (ends * twinkle);
                p.remainingLifetime = ManualParticles.Lifetime;
                m_Particles[i] = p;
            }

            m_System.SetParticles(m_Particles, m_Particles.Length);

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_Color, m_Colour * glow);
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}

using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// The fire in the grate, burning to the music: a row of flame tongues whose heights are the
    /// spectrum — bass in the middle, treble at the sides, the way the walls lay it out in the
    /// visualizer — roaring higher as the room is possessed, throwing embers on the beat, and
    /// turning ghost-blue while the ghost is out.
    /// </summary>
    /// <remarks>
    /// The flames are hand-written billboard particles, stretched tall, on the additive glow
    /// material; the fire light's flicker is slow noise, a few percent either way, as a real fire's
    /// is — not a strobe.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class HauntedFireplace : MonoBehaviour
    {
        [SerializeField] Haunting m_Haunting;
        [SerializeField] Light m_Light;

        [Header("Flames")]
        [SerializeField, Tooltip("Metres across the grate the tongues spread over.")]
        float m_Width = 0.5f;

        [SerializeField, Range(3, 15)] int m_Tongues = 9;
        [SerializeField] float m_TongueWidth = 0.15f;

        [SerializeField, Tooltip("A tongue's height when its band is silent. A fire that goes out between songs is a dead room.")]
        float m_RestHeight = 0.28f;

        [SerializeField, Tooltip("Its height at full level.")]
        float m_FullHeight = 0.55f;

        [SerializeField, Tooltip("Flame colour up a tongue, base to tip.")]
        Gradient m_Fire;

        [SerializeField, Tooltip("The same, while the ghost is out.")]
        Gradient m_GhostFire;

        [Header("Embers")]
        [SerializeField, Range(0, 128)] int m_Embers = 48;
        [SerializeField] float m_EmberSize = 0.012f;
        [SerializeField] float m_EmberLife = 1.6f;

        [Header("Light")]
        [SerializeField] Color m_LightColour = new(1f, 0.55f, 0.22f);
        [SerializeField] Color m_GhostLightColour = new(0.35f, 0.75f, 1f);

        const int k_Layers = 4;
        static readonly int k_Color = Shader.PropertyToID("_Color");

        ParticleSystem m_System;
        ParticleSystemRenderer m_Renderer;
        ParticleSystem.Particle[] m_Particles;
        MaterialPropertyBlock m_Block;
        float m_LightIntensity;
        float[] m_Seeds;

        Vector3[] m_EmberPosition;
        Vector3[] m_EmberVelocity;
        float[] m_EmberAge;
        int m_NextEmber;
        System.Random m_Random;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            ManualParticles.Configure(m_System, m_Renderer, true);

            int count = m_Tongues * k_Layers + m_Embers;
            ManualParticles.Reserve(m_System, count);
            m_Particles = new ParticleSystem.Particle[count];
            m_Block = new MaterialPropertyBlock();
            m_Random = new System.Random(GetInstanceID());

            m_Seeds = new float[m_Tongues];
            for (int i = 0; i < m_Tongues; i++)
                m_Seeds[i] = (float)m_Random.NextDouble() * 50f;

            m_EmberPosition = new Vector3[m_Embers];
            m_EmberVelocity = new Vector3[m_Embers];
            m_EmberAge = new float[m_Embers];
            for (int i = 0; i < m_Embers; i++)
                m_EmberAge[i] = float.MaxValue;

            if (Palettes.IsUnset(m_Fire))
                m_Fire = Make(new Color(0.75f, 0.08f, 0f), new Color(0.95f, 0.3f, 0.02f), new Color(1f, 0.62f, 0.12f));
            if (Palettes.IsUnset(m_GhostFire))
                m_GhostFire = Make(new Color(0f, 0.12f, 0.9f), new Color(0.05f, 0.4f, 1f), new Color(0.35f, 0.8f, 1f));

            m_LightIntensity = m_Light != null ? m_Light.intensity : 1f;
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        static Gradient Make(Color a, Color b, Color c)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.5f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f || m_Haunting == null)
                return;

            float time = Time.time;
            float possession = m_Haunting.Possession;
            float ghost = Mathf.SmoothStep(0f, 1f, m_Haunting.Reveal);
            float roar = 0.75f + 0.5f * possession;
            float centre = (m_Tongues - 1) * 0.5f;
            int p = 0;

            for (int i = 0; i < m_Tongues; i++)
            {
                float fromMiddle = centre > 0f ? Mathf.Abs(i - centre) / centre : 0f;
                float level = m_Haunting.Band(fromMiddle);
                float flicker = Mathf.PerlinNoise(time * 3.1f, m_Seeds[i]);
                float height = (m_RestHeight + (m_FullHeight - m_RestHeight) * level) * roar * (0.8f + 0.4f * flicker);

                // Tongues further out are shorter, so the fire has a crown in the middle.
                height *= 1f - 0.35f * fromMiddle;

                float x = (centre > 0f ? (i - centre) / centre : 0f) * m_Width * 0.5f;
                x += 0.015f * (Mathf.PerlinNoise(time * 1.7f, m_Seeds[i] + 9f) - 0.5f);

                for (int layer = 0; layer < k_Layers; layer++, p++)
                {
                    float up = layer / (float)(k_Layers - 1);
                    float width = m_TongueWidth * (1f - 0.55f * up) * (0.85f + 0.3f * flicker);
                    float tall = height * (0.7f - 0.25f * up);
                    var colour = Color.Lerp(m_Fire.Evaluate(up), m_GhostFire.Evaluate(up), ghost);
                    float lick = 0.02f * Mathf.Sin(time * 7f + m_Seeds[i] + layer) * up;

                    ref var particle = ref m_Particles[p];
                    // Rising from the logs rather than behind them.
                    particle.position = new Vector3(x + lick, height * (0.28f + 0.55f * up), 0f);
                    particle.startSize3D = new Vector3(width, tall, 1f);
                    // Kept well under white: the layers add up, and a ghost fire summed to white is not blue.
                    particle.startColor = colour * (0.35f + 0.25f * level) * (1f - 0.3f * up);
                    particle.remainingLifetime = ManualParticles.Lifetime;
                    particle.startLifetime = ManualParticles.Lifetime;
                }
            }

            UpdateEmbers(dt, ghost, ref p);

            m_System.SetParticles(m_Particles, p);
            m_Block.SetColor(k_Color, Color.white);
            m_Renderer.SetPropertyBlock(m_Block);

            if (m_Light != null)
            {
                float glow = 0.75f + 0.45f * m_Haunting.Level(Listen.Loudness) * roar;
                float waver = 1f + 0.12f * (Mathf.PerlinNoise(time * 2.3f, 3.7f) - 0.5f) * 2f;
                m_Light.intensity = m_LightIntensity * glow * waver;
                m_Light.color = Color.Lerp(m_LightColour, m_GhostLightColour, ghost);
            }
        }

        /// <summary>Sparks thrown up on the beat once the room is possessed, drifting and fading as they rise.</summary>
        void UpdateEmbers(float dt, float ghost, ref int p)
        {
            if (m_Embers == 0)
                return;

            if (m_Haunting.Beat && m_Haunting.Possession > 0.2f)
            {
                int burst = 2 + Mathf.RoundToInt(6f * m_Haunting.BeatStrength * m_Haunting.Possession);
                for (int b = 0; b < burst; b++)
                {
                    int e = m_NextEmber;
                    m_NextEmber = (m_NextEmber + 1) % m_Embers;
                    m_EmberAge[e] = 0f;
                    m_EmberPosition[e] = new Vector3(((float)m_Random.NextDouble() - 0.5f) * m_Width * 0.7f, 0.05f, 0f);
                    m_EmberVelocity[e] = new Vector3(((float)m_Random.NextDouble() - 0.5f) * 0.25f,
                        0.35f + 0.5f * (float)m_Random.NextDouble(), -0.1f * (float)m_Random.NextDouble());
                }
            }

            for (int e = 0; e < m_Embers; e++, p++)
            {
                ref var particle = ref m_Particles[p];
                particle.remainingLifetime = ManualParticles.Lifetime;
                particle.startLifetime = ManualParticles.Lifetime;

                if (m_EmberAge[e] > m_EmberLife)
                {
                    particle.startSize3D = Vector3.zero;
                    continue;
                }

                m_EmberAge[e] += dt;
                m_EmberVelocity[e] += new Vector3(Mathf.Sin(m_EmberAge[e] * 5f + e) * 0.3f, -0.15f, 0f) * dt;
                m_EmberPosition[e] += m_EmberVelocity[e] * dt;

                float life = 1f - m_EmberAge[e] / m_EmberLife;
                var colour = Color.Lerp(new Color(1f, 0.5f, 0.1f), new Color(0.4f, 0.8f, 1f), ghost) * life;
                particle.position = m_EmberPosition[e];
                particle.startSize3D = new Vector3(m_EmberSize, m_EmberSize, 1f) * (0.5f + 0.5f * life);
                particle.startColor = colour;
            }
        }
    }
}

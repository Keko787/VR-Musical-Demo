using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A wooden pier running out to sea from the beach, with a lamp on each side every few metres —
    /// and the lamps are an equaliser nobody built. Each pair is one band, the bass at the shore end
    /// and the treble far out, so as the music plays the lamps along the pier brighten and dim like
    /// a meter laid out in perspective; each lamp's reflection quivers on the water under it.
    /// </summary>
    /// <remarks>
    /// The deck, pilings, railings and lamp posts are one mesh generated when the world wakes, on the
    /// land material. The glows are two hand-written particle systems (<see cref="ManualParticles"/>):
    /// one at the lamp heads, and one mirrored under the sea, drawn on the water only through the
    /// stencil the sea writes.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimPier : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Tooltip("The lamps' glows.")]
        ParticleSystem m_Glows;

        [SerializeField, Tooltip("Their reflections, mirrored under the sea.")]
        ParticleSystem m_Reflections;

        [Header("Shape")]
        [SerializeField, Tooltip("Metres along this object's +z from the shore end to the far end.")]
        float m_Length = 260f;

        [SerializeField] float m_Width = 3f;

        [SerializeField, Tooltip("Height of the deck above this object's origin, which is at sea level.")]
        float m_Deck = 1.4f;

        [SerializeField, Tooltip("Metres along the pier at which it leaves the sand for the sea. No reflections before it.")]
        float m_WaterFrom = 20f;

        [SerializeField] float m_PilingDepth = 12f;
        [SerializeField] float m_PilingSpacing = 6f;

        [Header("Lamps")]
        [SerializeField, Range(2, 48), Tooltip("Lamp pairs along the pier: one band each, the lowest at the shore.")]
        int m_LampPairs = 24;

        [SerializeField] float m_LampHeight = 3.2f;
        [SerializeField, Tooltip("Metres across a lamp's glow at full level.")] float m_GlowSize = 1.8f;
        [SerializeField, ColorUsage(false, true)] Color m_LampColour = new(1.6f, 1.1f, 0.6f);
        [SerializeField, Range(0f, 1f), Tooltip("How bright a lamp is with nothing in its band.")] float m_Rest = 0.12f;

        [Header("Colours")]
        [SerializeField] Color m_Wood = new(0.46f, 0.33f, 0.22f);
        [SerializeField] Color m_DarkWood = new(0.26f, 0.19f, 0.14f);
        [SerializeField] Color m_Iron = new(0.13f, 0.13f, 0.14f);

        static readonly int k_Color = Shader.PropertyToID("_Color");

        Vector3[] m_Heads;
        ParticleSystem.Particle[] m_GlowParticles;
        ParticleSystem.Particle[] m_ReflectionParticles;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            Generate();
            m_Block = new MaterialPropertyBlock();
            Prepare(m_Glows, out m_GlowParticles);
            Prepare(m_Reflections, out m_ReflectionParticles);
        }

        void OnEnable()
        {
            if (m_Glows != null && !m_Glows.isPlaying)
                m_Glows.Play();
            if (m_Reflections != null && !m_Reflections.isPlaying)
                m_Reflections.Play();
        }

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }

        void Prepare(ParticleSystem system, out ParticleSystem.Particle[] particles)
        {
            particles = new ParticleSystem.Particle[m_Heads.Length];
            if (system == null)
                return;

            ManualParticles.Configure(system, system.GetComponent<ParticleSystemRenderer>(), true);
            ManualParticles.Reserve(system, particles.Length);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].startLifetime = ManualParticles.Lifetime;
                particles[i].remainingLifetime = ManualParticles.Lifetime;
            }
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            var b = new SimMeshes.Builder();
            float half = m_Width * 0.5f;
            float middle = m_Length * 0.5f;

            // Deck, with a darker beam along each edge.
            SimMeshes.Box(b, new Vector3(0f, m_Deck, middle), new Vector3(m_Width, 0.22f, m_Length), m_Wood);
            SimMeshes.Box(b, new Vector3(-half, m_Deck - 0.15f, middle), new Vector3(0.25f, 0.3f, m_Length), m_DarkWood);
            SimMeshes.Box(b, new Vector3(half, m_Deck - 0.15f, middle), new Vector3(0.25f, 0.3f, m_Length), m_DarkWood);

            // Pilings down into the sea bed, a pair at every span.
            float pilingHeight = m_Deck + m_PilingDepth;
            for (float z = 0f; z <= m_Length; z += m_PilingSpacing)
                foreach (float x in new[] { -half + 0.2f, half - 0.2f })
                    SimMeshes.Box(b, new Vector3(x, m_Deck - pilingHeight * 0.5f, z), new Vector3(0.35f, pilingHeight, 0.35f), m_DarkWood);

            // Railings: a top rail each side and a post every half span.
            float rail = m_Deck + 1f;
            SimMeshes.Box(b, new Vector3(-half + 0.05f, rail, middle), new Vector3(0.1f, 0.1f, m_Length), m_Wood);
            SimMeshes.Box(b, new Vector3(half - 0.05f, rail, middle), new Vector3(0.1f, 0.1f, m_Length), m_Wood);
            for (float z = 0f; z <= m_Length; z += m_PilingSpacing * 0.5f)
                foreach (float x in new[] { -half + 0.05f, half - 0.05f })
                    SimMeshes.Box(b, new Vector3(x, m_Deck + 0.5f, z), new Vector3(0.09f, 1f, 0.09f), m_Wood);

            // Lamp posts, a pair at each step out; the heads are where the glows go.
            m_Heads = new Vector3[m_LampPairs * 2];
            for (int i = 0; i < m_LampPairs; i++)
            {
                float z = (i + 0.5f) / m_LampPairs * m_Length;
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? -half - 0.05f : half + 0.05f;
                    SimMeshes.Box(b, new Vector3(x, m_Deck + m_LampHeight * 0.5f, z), new Vector3(0.12f, m_LampHeight, 0.12f), m_Iron);
                    var head = new Vector3(x, m_Deck + m_LampHeight + 0.15f, z);
                    SimMeshes.Box(b, head + Vector3.up * 0.2f, new Vector3(0.35f, 0.08f, 0.35f), m_Iron);
                    m_Heads[i * 2 + s] = head;
                }
            }

            SimMeshes.Assign(GetComponent<MeshFilter>(), b.ToMesh($"{name}_Pier"));
        }

        void Update()
        {
            if (m_Heads == null)
                return;

            float intensity = m_Simulation != null ? m_Simulation.Intensity : 1f;
            float time = Time.time;

            for (int i = 0; i < m_Heads.Length; i++)
            {
                int pair = i / 2;
                float level = m_Simulation != null ? m_Simulation.Band(pair / (float)Mathf.Max(1, m_LampPairs - 1)) : 0f;
                float bright = m_Rest + (1f - m_Rest) * level * level;
                float size = m_GlowSize * (0.55f + 0.45f * level);
                var head = m_Heads[i];

                var glow = m_GlowParticles[i];
                glow.position = head;
                glow.startSize3D = new Vector3(size, size, size);
                glow.startColor = Color.white * bright;
                glow.remainingLifetime = ManualParticles.Lifetime;
                m_GlowParticles[i] = glow;

                // Mirrored under the sea, stretched down the water and quivering. None over the sand.
                var reflection = m_ReflectionParticles[i];
                bool overWater = head.z > m_WaterFrom;
                float waver = 0.85f + 0.15f * Mathf.Sin(time * 2.7f + i * 1.3f);
                reflection.position = new Vector3(head.x, -head.y, head.z);
                reflection.startSize3D = new Vector3(size * 0.7f, size * 3.5f * waver, 1f);
                reflection.startColor = Color.white * (overWater ? bright * 0.5f : 0f);
                reflection.remainingLifetime = ManualParticles.Lifetime;
                m_ReflectionParticles[i] = reflection;
            }

            Write(m_Glows, m_GlowParticles, m_LampColour * intensity);
            Write(m_Reflections, m_ReflectionParticles, m_LampColour * intensity);
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

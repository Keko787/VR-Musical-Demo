using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A probe drifting in a space world's sky: a green dish, a gold body, two dark solar panels and a
    /// mast, tumbling slowly. The beacon at the top of its mast blinks on the beat — the analyser's,
    /// capped like every flash outside — so the satellites keep time across the sky.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimSatellite : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Tooltip("A particle system, child of this one, for the beacon's light.")]
        ParticleSystem m_Beacon;

        [SerializeField, Tooltip("Metres to a unit: the probe is built about five units across.")]
        float m_Scale = 4f;

        [SerializeField, Tooltip("Degrees a second it turns about each axis.")]
        Vector3 m_Tumble = new(2f, 5f, 1f);

        [SerializeField] Color m_Dish = new(0.32f, 0.8f, 0.5f);
        [SerializeField] Color m_Body = new(0.82f, 0.63f, 0.3f);
        [SerializeField] Color m_Panel = new(0.08f, 0.1f, 0.22f);
        [SerializeField] Color m_Frame = new(0.72f, 0.74f, 0.78f);
        [SerializeField, ColorUsage(false, true)] Color m_BeaconColour = new(2f, 0.5f, 0.35f);

        static readonly int k_Color = Shader.PropertyToID("_Color");

        Vector3 m_Tip;
        float m_Flash;
        ParticleSystem.Particle[] m_Particle = new ParticleSystem.Particle[1];
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            Generate();
            m_Block = new MaterialPropertyBlock();

            if (m_Beacon != null)
            {
                ManualParticles.Configure(m_Beacon, m_Beacon.GetComponent<ParticleSystemRenderer>());
                ManualParticles.Reserve(m_Beacon, 1);
                m_Particle[0].startLifetime = ManualParticles.Lifetime;
            }
        }

        void OnEnable()
        {
            if (m_Beacon != null && !m_Beacon.isPlaying)
                m_Beacon.Play();
        }

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
            float s = m_Scale;

            // The dish: a shallow bowl opening forward, a rim, and a feed out to its focus.
            const int rings = 5;
            const int sides = 20;
            float radius = 1.6f * s;
            float depth = 0.45f * s;
            for (int k = 0; k <= rings; k++)
            {
                float r = radius * k / rings;
                float z = depth * (r / radius) * (r / radius);
                var shade = Color.Lerp(m_Dish * 0.85f, m_Dish, k / (float)rings);
                shade.a = 1f;
                for (int j = 0; j < sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    b.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z), shade);
                }
            }
            int dish = b.Count - (rings + 1) * sides;
            for (int k = 0; k < rings; k++)
                b.Band(dish + k * sides, dish + (k + 1) * sides, sides);
            SimMeshes.Box(b, new Vector3(0f, 0f, depth * 0.9f), new Vector3(0.08f * s, 0.08f * s, depth * 1.8f), m_Frame);
            SimMeshes.Box(b, new Vector3(0f, 0f, depth * 1.8f), Vector3.one * (0.22f * s), m_Frame);

            // The body behind it, the panels out either side, a mast on top.
            float back = -0.9f * s;
            SimMeshes.Box(b, new Vector3(0f, 0f, back), new Vector3(1.2f, 1.2f, 1.6f) * s, m_Body);
            SimMeshes.Box(b, new Vector3(0f, 0f, back), new Vector3(5.2f, 0.12f, 0.12f) * s, m_Frame);
            foreach (float x in new[] { -1f, 1f })
            {
                SimMeshes.Box(b, new Vector3(x * 2.8f * s, 0f, back), new Vector3(3.2f, 0.06f, 1.3f) * s, m_Panel);
                SimMeshes.Box(b, new Vector3(x * 2.8f * s, -0.04f * s, back), new Vector3(3.32f, 0.03f, 1.42f) * s, m_Frame);
            }

            SimMeshes.Box(b, new Vector3(0f, 1.4f * s, back), new Vector3(0.06f, 1.6f, 0.06f) * s, m_Frame);
            m_Tip = new Vector3(0f, 2.25f * s, back);

            var mesh = b.ToMesh($"{name}_Probe");
            mesh.RecalculateBounds();
            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            transform.localRotation *= Quaternion.Euler(m_Tumble * dt);

            if (m_Simulation != null && m_Simulation.Beat)
                m_Flash = 0.4f + 0.6f * m_Simulation.BeatStrength;
            m_Flash *= Mathf.Exp(-dt / 0.22f);

            if (m_Beacon == null)
                return;

            float intensity = m_Simulation != null ? m_Simulation.Intensity : 1f;
            var p = m_Particle[0];
            p.position = m_Tip;
            p.startSize = m_Scale * (0.5f + 0.9f * m_Flash);
            p.startColor = Color.white * (0.15f + 0.85f * m_Flash);
            p.remainingLifetime = ManualParticles.Lifetime;
            m_Particle[0] = p;
            m_Beacon.SetParticles(m_Particle, 1);

            var renderer = m_Beacon.GetComponent<ParticleSystemRenderer>();
            renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_Color, m_BeaconColour * intensity);
            renderer.SetPropertyBlock(m_Block);
        }
    }
}

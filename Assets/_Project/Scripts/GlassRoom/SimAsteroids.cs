using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A field of asteroids drifting round the room, from boulders floating just past the glass to
    /// mountains of rock far off in the sky — and each one dances to its own part of the music. The
    /// lower an asteroid hangs, the lower its band: the ones near the horizon lift with the bass, the
    /// high ones with the treble, so the field rises and turns in layers as the music moves through
    /// the spectrum. The whole field drifts slowly round the room as if it were in orbit.
    /// </summary>
    /// <remarks>
    /// The rocks are a hand-written particle system (<see cref="ManualParticles"/>) drawing one chunky,
    /// flat-faceted rock mesh generated here, on the land material, so they cel-shade like the rest of
    /// the world.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class SimAsteroids : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;
        [SerializeField, Range(10, 2000)] int m_Count = 420;

        [SerializeField, Tooltip("Nearest and furthest, metres from the room.")]
        Vector2 m_Distance = new(25f, 900f);

        [SerializeField, Tooltip("Lowest and highest, degrees above the horizon.")]
        Vector2 m_Elevation = new(-2f, 40f);

        [SerializeField, Tooltip("Degrees round from straight ahead, either side.")]
        float m_Spread = 150f;

        [SerializeField, Tooltip("Metres across a rock at the nearest and at the furthest.")]
        Vector2 m_Size = new(0.6f, 30f);

        [SerializeField, Tooltip("How far a rock lifts at full level, as a fraction of its distance.")]
        float m_Lift = 0.05f;

        [SerializeField, Tooltip("Radians a second the field drifts round the room.")]
        float m_Drift = 0.004f;

        [SerializeField, Tooltip("Degrees a second a rock turns at rest, and the extra at full level.")]
        Vector2 m_Spin = new(6f, 60f);

        [SerializeField] Color m_Rock = new(0.5f, 0.47f, 0.52f);
        [SerializeField] int m_Seed = 5;

        struct Rock
        {
            public float Azimuth;
            public float Elevation;
            public float Distance;
            public float Size;
            public float Band;
            public Vector3 Axis;
            public float Angle;
            public float Lift;
            public float Shade;
        }

        ParticleSystem m_System;
        ParticleSystem.Particle[] m_Particles;
        Rock[] m_Rocks;
        Mesh m_Mesh;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            var renderer = GetComponent<ParticleSystemRenderer>();
            ManualParticles.Configure(m_System, renderer, true);

            var main = m_System.main;
            main.startRotation3D = true;
            m_Mesh = RockMesh(m_Seed);
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = m_Mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.enableGPUInstancing = false;

            ManualParticles.Reserve(m_System, m_Count);
            m_Particles = new ParticleSystem.Particle[m_Count];
            m_Rocks = new Rock[m_Count];

            var random = new System.Random(m_Seed);
            float Range(float a, float b) => a + (b - a) * (float)random.NextDouble();

            for (int i = 0; i < m_Count; i++)
            {
                // More of them near, so the near field is busy and the far one a scatter of big rocks.
                float near = Mathf.Pow(Range(0f, 1f), 1.6f);
                float elevation = Range(m_Elevation.x, m_Elevation.y);
                m_Rocks[i] = new Rock
                {
                    Azimuth = Range(-m_Spread, m_Spread) * Mathf.Deg2Rad,
                    Elevation = elevation * Mathf.Deg2Rad,
                    Distance = Mathf.Lerp(m_Distance.x, m_Distance.y, near),
                    Size = Mathf.Lerp(m_Size.x, m_Size.y, near) * Range(0.5f, 1.4f),
                    Band = Mathf.Clamp01(Mathf.InverseLerp(m_Elevation.x, m_Elevation.y, elevation) + Range(-0.12f, 0.12f)),
                    Axis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)).normalized,
                    Angle = Range(0f, 360f),
                    Shade = Range(0.75f, 1.1f),
                };

                m_Particles[i].startLifetime = ManualParticles.Lifetime;
                m_Particles[i].remainingLifetime = ManualParticles.Lifetime;
            }
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        void OnDestroy()
        {
            if (m_Mesh != null)
                SimMeshes.Destroy(m_Mesh);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Rocks == null)
                return;

            float drift = Time.time * m_Drift;
            float ease = 1f - Mathf.Exp(-dt / 0.25f);

            for (int i = 0; i < m_Rocks.Length; i++)
            {
                ref var rock = ref m_Rocks[i];
                float level = m_Simulation != null ? m_Simulation.Band(rock.Band) : 0f;
                rock.Lift += (level - rock.Lift) * ease;
                rock.Angle += (m_Spin.x + m_Spin.y * rock.Lift) * dt;

                float azimuth = rock.Azimuth + drift * (1f + rock.Band);
                float c = Mathf.Cos(rock.Elevation);
                var direction = new Vector3(Mathf.Sin(azimuth) * c, Mathf.Sin(rock.Elevation), Mathf.Cos(azimuth) * c);

                var p = m_Particles[i];
                p.position = direction * rock.Distance + Vector3.up * (rock.Distance * m_Lift * rock.Lift);
                p.rotation3D = rock.Axis * rock.Angle;
                p.startSize3D = Vector3.one * rock.Size;
                var colour = m_Rock * rock.Shade;
                colour.a = 1f;
                p.startColor = colour;
                p.remainingLifetime = ManualParticles.Lifetime;
                m_Particles[i] = p;
            }

            m_System.SetParticles(m_Particles, m_Particles.Length);
        }

        /// <summary>
        /// One rock: a sphere pushed in and out by noise, then faceted — every triangle its own
        /// vertices — so it shades in flat planes like the cartoon asteroids it is drawn after.
        /// </summary>
        static Mesh RockMesh(int seed)
        {
            var sphere = new SimMeshes.Builder();
            SimMeshes.Icosphere(sphere, Vector3.zero, Vector3.one * 0.5f, Color.white, Vector2.zero);

            for (int i = 0; i < sphere.Vertices.Count; i++)
            {
                var v = sphere.Vertices[i];
                float bump = SimMeshes.Fbm(v.x * 3f + seed, v.y * 3f + v.z * 2f, 3) - 0.5f;
                sphere.Vertices[i] = Vector3.Scale(v * (1f + bump * 0.7f), new Vector3(1f, 0.8f, 0.9f));
            }

            var faceted = new SimMeshes.Builder();
            for (int t = 0; t < sphere.Triangles.Count; t += 3)
            {
                int a = faceted.Add(sphere.Vertices[sphere.Triangles[t]], Color.white);
                int b = faceted.Add(sphere.Vertices[sphere.Triangles[t + 1]], Color.white);
                int c = faceted.Add(sphere.Vertices[sphere.Triangles[t + 2]], Color.white);
                faceted.Triangle(a, b, c);
            }

            return faceted.ToMesh("Asteroid");
        }
    }
}

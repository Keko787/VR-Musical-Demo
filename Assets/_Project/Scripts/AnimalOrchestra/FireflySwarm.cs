using UnityEngine;
using UnityEngine.Rendering;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The fireflies: the high-end texture of a song, and the room's idle state between songs. Each fly
    /// drifts round a home point — most of them along the walls, some low over the water, a few in the
    /// open room — and blinks on its own phase. The texture stem's high band sets how many are lit and
    /// how fast they blink; a glint in the part makes a handful flash together. Between songs every fly
    /// comes on and they blink slowly, which fills the room while the next song loads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Driven by hand through <see cref="ParticleSystem.SetParticles(ParticleSystem.Particle[], int)"/>
    /// rather than simulated: the blink has to follow the stem, and a particle system's own colour over
    /// lifetime cannot read one. The system is only the renderer.
    /// </para>
    /// <para>
    /// Photosensitivity: flies blink on independent phases, so the room never flashes as a whole, and no
    /// fly blinks faster than the conductor's <see cref="Performer.MaxFlashHz"/>, which low intensity
    /// mode lowers.
    /// </para>
    /// </remarks>
    public sealed class FireflySwarm : Performer
    {
        [SerializeField, Tooltip("The particle system that draws the flies. Built by Build.")]
        ParticleSystem m_System;

        [SerializeField, Min(1), Tooltip("How many flies there are; all of them are lit between songs.")]
        int m_Count = 260;

        [SerializeField, Tooltip("The room box the flies live in, room frame. Set by Build.")]
        RoomBox m_Box;

        [SerializeField, Tooltip("Where the viewer's eyes are by default, room frame. Flies keep out of a ball round it: " +
            "a billboard right in front of the eyes reads as a flat card.")]
        Vector3 m_ViewerEye = new(0f, 1.6f, -0.35f);

        [Header("Look")]
        [SerializeField] Color m_Colour = new(0.80f, 1.00f, 0.35f);

        [SerializeField, Tooltip("Some flies drift toward this, for variety.")]
        Color m_WarmColour = new(1.00f, 0.80f, 0.30f);

        [SerializeField] Vector2 m_Size = new(0.022f, 0.036f);

        [Header("Blink")]
        [SerializeField, Tooltip("Blinks per second at rest and at the top of the high band, before the flash cap.")]
        Vector2 m_BlinkRate = new(0.35f, 2.2f);

        [SerializeField, Range(0f, 1f), Tooltip("Share of flies lit while a song plays with no high end at all.")]
        float m_MinimumLit = 0.2f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Glow between blinks, so a lit fly never vanishes completely.")]
        float m_Ember = 0.08f;

        [SerializeField, Min(0), Tooltip("Pitch at and above which a texture note is a glint, not a shaker hit.")]
        int m_GlintPitch = 84;

        ParticleSystem.Particle[] m_Particles;
        Vector3[] m_Home;
        Vector3[] m_DriftHz;
        Vector3[] m_DriftPhase;
        float[] m_DriftRadius;
        float[] m_Phase;
        float[] m_RateJitter;
        float[] m_Rank;
        float[] m_Visible;
        float[] m_Flash;
        Color[] m_Tint;
        float[] m_Size01;

        int m_LastGlint = -1;

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            m_Box = kit.Box;
            m_ViewerEye = kit.ViewerEye;

            var go = kit.Node(transform, "Fireflies", Vector3.zero).gameObject;
            m_System = go.AddComponent<ParticleSystem>();
            ConfigureSystem(m_System, kit.Firefly, m_Count);
        }

        /// <summary>
        /// A particle system that only renders: no emission, no motion, particles that never die, and the
        /// CAVE's billboard alignment. The swarm writes every particle itself.
        /// </summary>
        internal static void ConfigureSystem(ParticleSystem system, Material material, int count)
        {
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 1e5f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            // Four CAVE cameras disagree about whether a system is in view; never let one pause it.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = false;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Facing, not View: a view-aligned billboard turns with each camera's view plane, and the
            // CAVE's cameras have four of them, so a particle on a seam would be drawn at two angles.
            renderer.alignment = ParticleSystemRenderSpace.Facing;
        }

        // ================================================================== runtime

        void Awake()
        {
            int n = Mathf.Max(1, m_Count);
            m_Particles = new ParticleSystem.Particle[n];
            m_Home = new Vector3[n];
            m_DriftHz = new Vector3[n];
            m_DriftPhase = new Vector3[n];
            m_DriftRadius = new float[n];
            m_Phase = new float[n];
            m_RateJitter = new float[n];
            m_Rank = new float[n];
            m_Visible = new float[n];
            m_Flash = new float[n];
            m_Tint = new Color[n];
            m_Size01 = new float[n];

            var random = new System.Random(1729);
            for (int i = 0; i < n; i++)
            {
                m_Home[i] = HomePoint(random);
                m_DriftHz[i] = new Vector3(Range(random, 0.05f, 0.16f), Range(random, 0.07f, 0.2f), Range(random, 0.05f, 0.16f));
                m_DriftPhase[i] = new Vector3(Range(random, 0f, 1f), Range(random, 0f, 1f), Range(random, 0f, 1f));
                m_DriftRadius[i] = Range(random, 0.08f, 0.22f);
                m_Phase[i] = Range(random, 0f, 1f);
                m_RateJitter[i] = Range(random, 0.7f, 1.3f);
                m_Rank[i] = Range(random, 0f, 1f);
                m_Tint[i] = Color.Lerp(m_Colour, m_WarmColour, Range(random, 0f, 1f) * Range(random, 0f, 1f));
                m_Size01[i] = Range(random, 0f, 1f);

                m_Particles[i].startLifetime = 1e5f;
                m_Particles[i].remainingLifetime = 1e5f;
                m_Particles[i].position = m_Home[i];
                m_Particles[i].startSize = Mathf.Lerp(m_Size.x, m_Size.y, m_Size01[i]);
                m_Particles[i].startColor = new Color32(0, 0, 0, 0);
            }
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        /// <summary>
        /// Where a fly lives. Most hug a wall — a fly against a screen is at zero parallax and reads for
        /// everyone in the room, glasses or not — some hang low over the water, and a few drift in the
        /// open room for the tracked viewer. None at height in the back half, where there is no screen
        /// to show them.
        /// </summary>
        Vector3 HomePoint(System.Random random)
        {
            var box = m_Box;
            float pick = (float)random.NextDouble();

            if (pick < 0.5f)
            {
                float inset = Range(random, 0.05f, 0.35f);
                float y = Range(random, 0.25f, box.Height - 0.25f);
                float wall = (float)random.NextDouble();
                if (wall < 0.3f)
                    return new Vector3(box.LeftX + inset, y, Range(random, box.BackZ + 0.1f, box.FrontZ - 0.1f));
                if (wall < 0.6f)
                    return new Vector3(box.RightX - inset, y, Range(random, box.BackZ + 0.1f, box.FrontZ - 0.1f));
                return new Vector3(Range(random, box.LeftX + 0.15f, box.RightX - 0.15f), y, box.FrontZ - inset);
            }

            if (pick < 0.75f)
                return new Vector3(Range(random, box.LeftX + 0.2f, box.RightX - 0.2f), Range(random, 0.12f, 0.55f),
                    Range(random, box.BackZ + 0.15f, box.FrontZ - 0.15f));

            for (int attempt = 0; ; attempt++)
            {
                var p = new Vector3(Range(random, box.LeftX + 0.3f, box.RightX - 0.3f), Range(random, 0.4f, 1.95f),
                    Range(random, -0.6f, box.FrontZ - 0.2f));
                if ((p - m_ViewerEye).sqrMagnitude > 0.45f * 0.45f || attempt > 8)
                    return p;
            }
        }

        protected override void Perform(float deltaTime)
        {
            if (m_System == null || m_Particles == null)
                return;

            int n = m_Particles.Length;
            float high = Level(Band.High);
            float time = Clock;

            // How many are lit: all of them between songs, and while a song plays, as many as its
            // high end asks for. A song with no texture part keeps a sparse few.
            float lit = Live ? Mathf.Lerp(m_MinimumLit, 1f, Mathf.Clamp01(high)) : 0.35f;
            lit = Mathf.Max(lit, Idleness);

            float rate = Live ? Mathf.Lerp(m_BlinkRate.x, m_BlinkRate.y, Mathf.Clamp01(high)) : m_BlinkRate.x;

            TriggerGlints();

            float intensity = Intensity;
            float flashDecay = Mathf.Pow(0.5f, deltaTime / 0.18f);

            for (int i = 0; i < n; i++)
            {
                float target = m_Rank[i] < lit ? 1f : 0f;
                m_Visible[i] = Motion.Damp(m_Visible[i], target, 0.6f, deltaTime);

                float hz = Mathf.Min(rate * m_RateJitter[i], MaxFlashHz);
                m_Phase[i] = Mathf.Repeat(m_Phase[i] + hz * deltaTime, 1f);

                // A blink is a short swell and fade, not a square flash: sin cubed, positive half only.
                float s = Mathf.Max(0f, Mathf.Sin(m_Phase[i] * 2f * Mathf.PI));
                float blink = m_Ember + (1f - m_Ember) * s * s * s;

                m_Flash[i] *= flashDecay;
                float brightness = Mathf.Clamp01(blink + m_Flash[i]) * m_Visible[i] * intensity;

                var hz3 = m_DriftHz[i];
                var ph = m_DriftPhase[i];
                float r = m_DriftRadius[i];
                var offset = new Vector3(
                    Mathf.Sin((time * hz3.x + ph.x) * 2f * Mathf.PI) * r,
                    Mathf.Sin((time * hz3.y + ph.y) * 2f * Mathf.PI) * r * 0.6f,
                    Mathf.Sin((time * hz3.z + ph.z) * 2f * Mathf.PI) * r);

                ref var particle = ref m_Particles[i];
                particle.position = m_Home[i] + offset;
                particle.remainingLifetime = 1e5f;
                particle.startColor = new Color(m_Tint[i].r, m_Tint[i].g, m_Tint[i].b, Mathf.Clamp01(brightness));
            }

            m_System.SetParticles(m_Particles, n);
        }

        /// <summary>
        /// A glint — a high texture note — lights a handful of flies at once. Which ones is hashed from
        /// the note's index, so a replay of the song lights the same ones.
        /// </summary>
        void TriggerGlints()
        {
            if (!TryLast(out var note, out int index) || index == m_LastGlint)
                return;

            m_LastGlint = index;
            if (note.Pitch < m_GlintPitch)
                return;

            int n = m_Particles.Length;
            int start = (int)(Motion.Hash01(index * 31 + 7) * n);
            float strength = Mathf.Lerp(0.5f, 1f, note.Velocity);
            for (int k = 0; k < 6; k++)
            {
                int i = (start + k * 37) % n;
                if (m_Visible[i] > 0.2f)
                    m_Flash[i] = Mathf.Max(m_Flash[i], strength);
            }
        }

        protected override void OnSongChanged() => m_LastGlint = -1;
    }
}

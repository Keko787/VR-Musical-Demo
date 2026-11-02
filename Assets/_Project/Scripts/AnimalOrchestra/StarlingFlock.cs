using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The starlings: the arpeggio or synth lead, as a murmuration in the sky beyond the front wall,
    /// above the elephant. The flock's centre sweeps across the wall over eight bars and rises and falls
    /// with the pitch of each note, so the flock traces the melody over time; every note sends a ripple of
    /// light through it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each bird is a damped spring pulled toward the centre plus its own place in a slowly turning,
    /// breathing formation, with its own stiffness. The stiff birds keep up and the soft ones lag, so
    /// when the centre jumps to a new pitch the flock stretches, folds and catches up in a wave — the
    /// look of a murmuration, at the cost of one spring per bird rather than a boids neighbourhood search.
    /// </para>
    /// <para>
    /// The flock flies outside the room, seen through the front wall as through a window. Beyond the wall
    /// the band can sit higher than the wall's top edge — a line from the eye climbs as it goes — but the
    /// further out it flies, the more of the band the eye has to look over the elephant to see, so the
    /// heights are set for a viewer of 1.5–1.75 m at the default spot. The birds never come inside the
    /// glass: a bird crossing the wall would jump from one picture into the room.
    /// </para>
    /// </remarks>
    public sealed class StarlingFlock : Performer
    {
        [SerializeField, Tooltip("The particle system that draws the birds. Built by Build.")]
        ParticleSystem m_System;

        [SerializeField, Min(1)] int m_Count = 150;

        [Header("Band (room frame)")]
        [SerializeField, Tooltip("Lowest and highest the flock's centre goes, metres. The pitch maps onto this. Above " +
            "the elephant's head as the viewer sees it, below the top of the front wall's window.")]
        Vector2 m_Height = new(2.27f, 2.5f);

        [SerializeField, Tooltip("How far either side of the wall's centre the flock sweeps, metres.")]
        float m_Sweep = 1.6f;

        [SerializeField, Tooltip("Beats per sweep across the wall and back.")]
        float m_SweepBeats = 32f;

        [SerializeField, Tooltip("How far beyond the front wall the flock flies, metres: behind the elephant.")]
        float m_Distance = 1.2f;

        [SerializeField, Tooltip("Nearest any bird may come to the front wall, metres, from outside.")]
        float m_WallClearance = 0.3f;

        [SerializeField, Tooltip("The room box, room frame. Set by Build.")]
        RoomBox m_Box;

        [Header("Formation")]
        [SerializeField, Tooltip("Half-size of the formation, metres: across, up, out from the wall.")]
        Vector3 m_Spread = new(0.6f, 0.15f, 0.15f);

        [SerializeField, Tooltip("Spring stiffness range, per bird. The spread between them is what makes the flock wave.")]
        Vector2 m_Stiffness = new(5f, 16f);

        [SerializeField, Range(0.1f, 2f), Tooltip("Damping, as a fraction of critical.")]
        float m_Damping = 0.65f;

        [SerializeField, Tooltip("How fast the centre follows the pitch: halfway to each new note in this many seconds.")]
        float m_PitchHalfLife = 0.08f;

        [Header("Look")]
        [SerializeField, Tooltip("Wingspan, metres. Bigger than a starling: they fly a metre and more beyond the glass.")]
        Vector2 m_Size = new(0.07f, 0.095f);

        [SerializeField, Range(0f, 1f), Tooltip("Strongest flash a note sends through the flock.")]
        float m_FlashStrength = 0.55f;

        [SerializeField, Tooltip("How fast a note's ripple crosses the flock, metres per second.")]
        float m_RippleSpeed = 4f;

        [SerializeField, Tooltip("Turn the birds the other way if they fly backwards in the CAVE (particle rotation sign).")]
        bool m_FlipHeading;

        ParticleSystem.Particle[] m_Particles;
        Vector3[] m_Position;
        Vector3[] m_Velocity;
        Vector3[] m_Formation;
        float[] m_Stiff;
        float[] m_Flap;
        float[] m_FlapHz;
        float[] m_GlideSeed;
        float[] m_Size01;

        Vector3 m_Centre;
        float m_CentreY;
        bool m_Started;

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            m_Box = kit.Box;

            var go = kit.Node(transform, "Starlings", Vector3.zero).gameObject;
            m_System = go.AddComponent<ParticleSystem>();
            FireflySwarm.ConfigureSystem(m_System, kit.Starling, m_Count);
        }

        // ================================================================== runtime

        void Awake()
        {
            int n = Mathf.Max(1, m_Count);
            m_Particles = new ParticleSystem.Particle[n];
            m_Position = new Vector3[n];
            m_Velocity = new Vector3[n];
            m_Formation = new Vector3[n];
            m_Stiff = new float[n];
            m_Flap = new float[n];
            m_FlapHz = new float[n];
            m_GlideSeed = new float[n];
            m_Size01 = new float[n];

            var random = new System.Random(5150);
            for (int i = 0; i < n; i++)
            {
                // Uniform in a ball, then squashed into the formation's ellipsoid; denser at the core.
                Vector3 p;
                do
                {
                    p = new Vector3(Next(random) * 2f - 1f, Next(random) * 2f - 1f, Next(random) * 2f - 1f);
                } while (p.sqrMagnitude > 1f);

                m_Formation[i] = Vector3.Scale(p * Mathf.Lerp(0.6f, 1f, Next(random)), m_Spread);
                m_Stiff[i] = Mathf.Lerp(m_Stiffness.x, m_Stiffness.y, Next(random));
                m_Flap[i] = Next(random);
                m_FlapHz[i] = Mathf.Lerp(6f, 9f, Next(random));
                m_GlideSeed[i] = Next(random) * 10f;
                m_Size01[i] = Next(random);

                m_Particles[i].startLifetime = 1e5f;
                m_Particles[i].remainingLifetime = 1e5f;
                m_Particles[i].startSize = Mathf.Lerp(m_Size.x, m_Size.y, m_Size01[i]);
            }
        }

        static float Next(System.Random random) => (float)random.NextDouble();

        protected override void Perform(float deltaTime)
        {
            if (m_System == null || m_Particles == null)
                return;

            UpdateCentre(deltaTime);

            if (!m_Started)
            {
                for (int i = 0; i < m_Position.Length; i++)
                    m_Position[i] = m_Centre + m_Formation[i];
                m_Started = true;
            }

            float time = Clock;

            // The formation turns slowly about the wall's normal and breathes, so even a still flock churns.
            float turn = Mathf.Sin(time * 0.21f) * 0.6f;
            float breathe = 1f + 0.18f * Mathf.Sin(time * 0.53f);
            var formationTurn = Quaternion.Euler(0f, 0f, turn * Mathf.Rad2Deg * 0.5f);

            // The ripple a note sends through the flock, crossing it from the leading side.
            float flashAge = float.MaxValue;
            float flashScale = 0f;
            if (TryLast(out var note, out int index))
            {
                flashAge = (float)(SongTime - note.Time);
                double gap = index > 0 ? note.Time - Notes[index - 1].Time : 1.0;
                float noteHz = gap > 1e-3 ? (float)(1.0 / gap) : 100f;
                flashScale = m_FlashStrength * Mathf.Lerp(0.5f, 1f, note.Velocity) * Mathf.Min(1f, MaxFlashHz / noteHz) * Intensity;
            }

            // Springs integrated in fixed sub-steps: stiff birds at the Editor's 14 FPS would overshoot
            // in one step.
            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / (1f / 90f)), 1, 8);
            float h = deltaTime / steps;
            var box = m_Box;
            float heading = m_FlipHeading ? -1f : 1f;

            for (int i = 0; i < m_Position.Length; i++)
            {
                var target = m_Centre + formationTurn * (m_Formation[i] * breathe);
                float k = m_Stiff[i];
                float c = 2f * Mathf.Sqrt(k) * m_Damping;

                var p = m_Position[i];
                var v = m_Velocity[i];
                for (int s = 0; s < steps; s++)
                {
                    v += ((target - p) * k - v * c) * h;
                    p += v * h;
                }

                // Kept outside the glass and inside the front window as the default eye sees it.
                p.x = Mathf.Clamp(p.x, box.LeftX - 0.4f, box.RightX + 0.4f);
                p.y = Mathf.Clamp(p.y, m_Height.x - 0.5f, m_Height.y + 0.5f);
                p.z = Mathf.Max(p.z, box.FrontZ + m_WallClearance);
                m_Position[i] = p;
                m_Velocity[i] = v;

                // Wingbeats faster when the bird is working; every so often it glides with wings level.
                float speed = v.magnitude;
                bool glide = Mathf.Sin(time * 0.7f + m_GlideSeed[i]) > 0.55f && speed < 0.6f;
                if (glide)
                    m_Flap[i] = Mathf.MoveTowards(m_Flap[i], 0.25f, deltaTime * 2f);
                else
                    m_Flap[i] = Mathf.Repeat(m_Flap[i] + deltaTime * m_FlapHz[i] * Mathf.Lerp(0.6f, 1.2f, Mathf.Clamp01(speed)), 1f);

                // The note's ripple reaches this bird once it has crossed the flock to it.
                float across = (m_Formation[i].x / Mathf.Max(0.01f, m_Spread.x) + 1f) * 0.5f;
                float delay = across * (2f * m_Spread.x) / Mathf.Max(0.1f, m_RippleSpeed);
                float flash = flashScale * Motion.Pulse(flashAge - delay, 0.02f, 0.07f);

                ref var particle = ref m_Particles[i];
                particle.position = p;
                particle.remainingLifetime = 1e5f;
                particle.rotation = heading * Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
                particle.startColor = new Color(Mathf.Clamp01(flash), m_Flap[i], 0f, 1f);
            }

            m_System.SetParticles(m_Particles, m_Particles.Length);
        }

        /// <summary>
        /// Where the flock is heading: across the wall on a slow sweep, and up and down with the pitch of
        /// the last note. Between songs, or in a song with no arpeggio, it wheels about the band's middle.
        /// </summary>
        void UpdateCentre(float deltaTime)
        {
            var box = m_Box;
            float mid = (m_Height.x + m_Height.y) * 0.5f;
            float targetY;
            float x;

            if (Live)
            {
                x = box.CentreX + m_Sweep * Mathf.Sin((float)(Beats / Mathf.Max(1f, m_SweepBeats)) * 2f * Mathf.PI);
                targetY = TryLast(out var note, out _)
                    ? Mathf.Lerp(m_Height.x, m_Height.y, Motion.PitchNorm(note.Pitch, Notes.MinPitch, Notes.MaxPitch))
                    : mid;
            }
            else
            {
                float t = Clock;
                x = box.CentreX + m_Sweep * 0.8f * Mathf.Sin(t * 0.12f);
                targetY = mid + (m_Height.y - m_Height.x) * 0.3f * Mathf.Sin(t * 0.31f);
            }

            m_CentreY = m_Started ? Motion.Damp(m_CentreY, targetY, m_PitchHalfLife, deltaTime) : targetY;
            var target = new Vector3(x, m_CentreY, box.FrontZ + m_Distance);
            m_Centre = m_Started ? Motion.Damp(m_Centre, target, 0.05f, deltaTime) : target;
        }
    }
}

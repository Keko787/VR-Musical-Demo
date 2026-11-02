using System;
using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The bass line: a bullfrog on a mossy stump at waist height, left of the viewer. Every bass note
    /// blows its throat sac up — bigger for a harder note — and it sinks back over the note and after,
    /// while the body gives a small squash on the hit. A sac that swells with each pluck is the clearest
    /// picture of a low note there is: you can almost see the air move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the sac follows the note, not only the stem.</b> The plan maps throat inflation to note
    /// velocity: a bass line's accents are in its velocities, and a stem's loudness smears consecutive
    /// plucks into one swell. The sac's size is computed from the last note's start, length and velocity
    /// at the song time — so it starts growing on the frame the note sounds — and the stem's level only
    /// sets a floor under it, for a song played from a stem without MIDI.
    /// </para>
    /// <para>
    /// The sac and the body are children of one pivot at the frog's feet, so the squash keeps it sitting
    /// on the stump rather than sinking into it.
    /// </para>
    /// </remarks>
    public sealed class BullfrogPerformer : Performer
    {
        // Sac size at full inflation, as a multiple of its resting size.
        static readonly Vector3 k_FullSac = new(2.4f, 2.7f, 2.4f);

        [Header("Throat")]
        [SerializeField, Range(0f, 2f), Tooltip("How far a full-velocity note inflates the sac: 1 is about two and a " +
            "half times its resting size.")]
        float m_Inflation = 1f;

        [SerializeField, Range(0.005f, 0.2f), Tooltip("Seconds from the note to a full sac.")]
        float m_Attack = 0.05f;

        [SerializeField, Range(0f, 1f), Tooltip("What fraction of the sac is still full when the note ends.")]
        float m_Sustain = 0.55f;

        [SerializeField, Range(0.02f, 1f), Tooltip("After the note ends, seconds for the sac to lose half its air.")]
        float m_ReleaseHalfLife = 0.15f;

        [Header("Body")]
        [SerializeField, Range(0f, 0.3f), Tooltip("How much a full-velocity note squashes the body, as a fraction of its height.")]
        float m_Squash = 0.09f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Frog;
        [SerializeField] Transform m_Throat;
        [SerializeField] CreatureTint m_Sac;
        [SerializeField] Transform[] m_Eyes;

        float m_Live, m_Inflate;

        // ================================================================== body

        public override void Build(CreatureKit kit)
        {
            var bark = new Color(0.36f, 0.24f, 0.16f);
            var wood = new Color(0.62f, 0.48f, 0.32f);
            var moss = new Color(0.30f, 0.52f, 0.20f);
            var mossDark = new Color(0.20f, 0.38f, 0.14f);
            var skin = new Color(0.36f, 0.64f, 0.24f);
            var spot = new Color(0.18f, 0.38f, 0.14f);
            var mouth = new Color(0.14f, 0.26f, 0.10f);
            var sac = new Color(0.97f, 0.88f, 0.60f);
            var gold = new Color(0.95f, 0.72f, 0.16f);
            var black = new Color(0.05f, 0.05f, 0.06f);

            // The stump: bark, a pale cut face, a mossy cap, and roots spreading at the floor.
            kit.Solid(transform, "Stump", kit.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.36f, 0.38f, 0.36f), bark);
            kit.Solid(transform, "Stump_Face", kit.Cylinder, new Vector3(0f, 0.765f, 0f), new Vector3(0.34f, 0.01f, 0.34f), wood);
            kit.Solid(transform, "Moss_Cap", kit.Cylinder, new Vector3(0f, 0.778f, 0f), new Vector3(0.30f, 0.012f, 0.30f), moss);
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + 20f) * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Sin(a) * 0.16f, 0.77f, Mathf.Cos(a) * 0.16f);
                kit.Ball(transform, $"Moss_{i}", at, new Vector3(0.09f, 0.04f, 0.07f), i % 2 == 0 ? moss : mossDark,
                    new Vector3(0f, i * 72f + 20f, 0f));
            }

            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f + 45f;
                float a = yaw * Mathf.Deg2Rad;
                kit.Ball(transform, $"Root_{i}", new Vector3(Mathf.Sin(a) * 0.17f, 0.03f, Mathf.Cos(a) * 0.17f),
                    new Vector3(0.09f, 0.07f, 0.16f), bark, new Vector3(0f, yaw, 0f));
            }

            // The frog, on the moss, facing +Z. Everything under one pivot at its feet so it squashes in place.
            m_Frog = kit.Node(transform, "Frog", new Vector3(0f, 0.79f, 0f));
            kit.Ball(m_Frog, "Body", new Vector3(0f, 0.075f, -0.03f), new Vector3(0.24f, 0.14f, 0.24f), skin);
            kit.Ball(m_Frog, "Head", new Vector3(0f, 0.09f, 0.08f), new Vector3(0.22f, 0.11f, 0.16f), skin);
            kit.Ball(m_Frog, "Mouth", new Vector3(0f, 0.079f, 0.148f), new Vector3(0.17f, 0.012f, 0.03f), mouth);

            var spots = new[]
            {
                new Vector3(-0.05f, 0.14f, -0.05f), new Vector3(0.06f, 0.138f, -0.02f), new Vector3(0f, 0.145f, -0.09f),
                new Vector3(-0.08f, 0.12f, -0.11f), new Vector3(0.085f, 0.12f, -0.1f),
            };
            for (int i = 0; i < spots.Length; i++)
                kit.Ball(m_Frog, $"Spot_{i}", spots[i], new Vector3(0.05f, 0.015f, 0.045f), spot, new Vector3(0f, i * 40f, 0f));

            // Folded hind legs and broad feet, and front legs propping the chest up.
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                kit.Ball(m_Frog, "Thigh_" + side, new Vector3(0.12f * s, 0.05f, -0.08f), new Vector3(0.10f, 0.08f, 0.16f), skin,
                    new Vector3(0f, 20f * s, 0f));
                kit.Ball(m_Frog, "Foot_" + side, new Vector3(0.15f * s, 0.01f, -0.02f), new Vector3(0.07f, 0.02f, 0.10f), spot,
                    new Vector3(0f, 25f * s, 0f));

                var arm = kit.Limb(m_Frog, "Arm_" + side, new Vector3(0.08f * s, 0.07f, 0.11f), new Vector3(-10f, 0f, 15f * s),
                    0.07f, 0.035f, skin);
                kit.Ball(arm, "Hand", new Vector3(0f, -0.07f, 0.01f), new Vector3(0.05f, 0.015f, 0.05f), spot);
            }

            // Bulging eyes on top of the head: gold iris, a dark slit of a pupil, a glint.
            m_Eyes = new Transform[2];
            for (int e = 0; e < 2; e++)
            {
                float s = e == 0 ? -1f : 1f;
                var eye = kit.Node(m_Frog, e == 0 ? "Eye_L" : "Eye_R", new Vector3(0.06f * s, 0.15f, 0.10f), new Vector3(-15f, 18f * s, 0f));
                kit.Ball(eye, "Socket", Vector3.zero, Vector3.one * 0.065f, skin);
                kit.Ball(eye, "Iris", new Vector3(0f, 0.004f, 0.022f), new Vector3(0.05f, 0.05f, 0.03f), gold);
                kit.Ball(eye, "Pupil", new Vector3(0f, 0.004f, 0.037f), new Vector3(0.032f, 0.01f, 0.008f), black);
                kit.Ball(eye, "Glint", new Vector3(0.008f * s, 0.016f, 0.036f), Vector3.one * 0.008f, Color.white);
                m_Eyes[e] = eye;
            }

            // The sac hangs under the chin; scaling its pivot swells it down and forward, out of the head.
            m_Throat = kit.Node(m_Frog, "Throat", new Vector3(0f, 0.05f, 0.12f));
            m_Sac = kit.Lit(m_Throat, "Sac", kit.Sphere, new Vector3(0f, -0.005f, 0.02f), new Vector3(0.09f, 0.06f, 0.07f), sac,
                new Color(1.0f, 0.70f, 0.30f) * 1.4f);
            m_Sac.Glow = 0f;
        }

        // ================================================================== performance

        protected override void Perform(float deltaTime)
        {
            if (m_Frog == null)
                return;

            m_Live = Motion.Damp(m_Live, Live ? 1f : 0f, 0.3f, deltaTime);

            // Throat: shaped from the last note at the song time, a floor from the stem, and a slow
            // breath when the part is silent.
            float trigger = 0f;
            if (TryLast(out var note, out _))
                trigger = note.Velocity * SacShape(SongTime - note.Time, note.Duration);

            float breathe = (0.10f + 0.10f * (0.5f + 0.5f * Motion.Wave(Clock, 0.35f))) * (1f - m_Live);
            float target = Mathf.Max(Mathf.Max(trigger, Level() * 0.45f), breathe) * m_Inflation;

            // Only enough smoothing to merge a note that starts while the last is still deflating.
            m_Inflate = Motion.Damp(m_Inflate, target, 0.012f, deltaTime);
            m_Throat.localScale = Vector3.one + (k_FullSac - Vector3.one) * m_Inflate;
            m_Sac.Glow = m_Inflate * m_Inflate * 1.6f * Intensity;

            // Body: a hop of anticipation, then a squash on the hit.
            float windup = 0f, strike = 0f, velocity = 1f;
            if (TryHit(0.08f, 0.2f, out double tau, out float lead, out var hit, out _))
            {
                Motion.Hit(tau, lead, 0.2f, out windup, out strike);
                velocity = hit.Velocity;
            }

            float squash = m_Squash * strike * velocity * Gain;
            float idleBreath = 0.012f * Motion.Wave(Clock, 0.35f) * (1f - m_Live);
            float height = 1f + 0.035f * windup - squash + idleBreath;
            float width = 1f - 0.015f * windup + 0.6f * squash;
            m_Frog.localScale = new Vector3(width, height, width);

            // A blink every few seconds.
            float blink = Blink();
            foreach (var eye in m_Eyes)
                eye.localScale = new Vector3(1f, blink, 1f);
        }

        /// <summary>
        /// The sac's fullness <paramref name="since"/> seconds after a note that lasts
        /// <paramref name="duration"/>: up over the attack, easing down to the sustain level by the
        /// note's end, then losing its air.
        /// </summary>
        float SacShape(double since, double duration)
        {
            if (since < 0.0)
                return 0f;

            float attack = Mathf.Clamp01((float)(since / m_Attack));
            if (since < duration)
                return attack * Mathf.Lerp(1f, m_Sustain, (float)(since / Math.Max(1e-3, duration)));

            return attack * m_Sustain * Mathf.Pow(0.5f, (float)(since - duration) / m_ReleaseHalfLife);
        }

        static float Blink()
        {
            const float period = 3.7f;
            const float length = 0.14f;
            float t = Clock + 1.3f;
            int cycle = Mathf.FloorToInt(t / period);
            float phase = t - cycle * period;
            if (Motion.Hash01(cycle + 7) < 0.3f || phase >= length)
                return 1f;

            return 1f - 0.85f * Mathf.Sin(phase / length * Mathf.PI);
        }
    }
}

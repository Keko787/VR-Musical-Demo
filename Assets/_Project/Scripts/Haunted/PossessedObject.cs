using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>How a piece of furniture moves when it is possessed.</summary>
    public enum Movement
    {
        /// <summary>Jumps on the beat and falls back under gravity with a thud. The coffee table.</summary>
        Hop,

        /// <summary>Rocks on curved runners, rolling without slipping. The rocking chair.</summary>
        Roll,

        /// <summary>A pendulum, rocked by the level and kicked by the beat. The chandelier.</summary>
        Swing,

        /// <summary>Kicked off the wall on the beat and bangs back against it. The picture frames.</summary>
        Tilt,

        /// <summary>Held ajar by the level, flung open by the beat, slams shut. The cabinet doors.</summary>
        Flap,

        /// <summary>Slides out along an axis with the level. The books.</summary>
        Slide,

        /// <summary>Shakes in place, faster and harder with the level. The teacups.</summary>
        Rattle,

        /// <summary>Turns about an axis at a rate set by the level. The clock hands.</summary>
        Spin,
    }

    /// <summary>What lifts a possessed thing off where it rests, on top of whatever motion it has.</summary>
    public enum LevitateOn
    {
        /// <summary>It stays where it is.</summary>
        Nothing,

        /// <summary>The room's possession: it rises as the song builds. The tea set, the books.</summary>
        Possession,

        /// <summary>The ghosts showing themselves, or being summoned. The rocking chair.</summary>
        Reveal,
    }

    /// <summary>
    /// A rigid thing the haunting moves: every piece of furniture that hops, rocks, swings, bangs,
    /// flaps, slides, rattles or spins. The motion is procedural — a spring, a pendulum or a body
    /// under gravity driven by the music — so nothing is keyframed and nothing is rigged, and the
    /// exaggeration hides the timing faults realism would expose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The moving transform's origin is the pivot: the hinge of a door, the nail a frame hangs
    /// from, the centre of a rocking chair's runners. The builder places it there, so every turning
    /// motion is a rotation about the body's own origin.
    /// </para>
    /// <para>
    /// On top of its motion a thing can <b>levitate</b>: lifted toward <see cref="m_FloatTo"/> as its
    /// cue — the room's possession, or the ghosts' reveal — passes from <see cref="m_FloatFrom"/> to
    /// <see cref="m_FloatFull"/>, bobbing and slowly tumbling while it is up, and settling back as
    /// the cue falls. The motion carries on in the air: a teacup trembles, the chair still rocks.
    /// </para>
    /// <para>
    /// The other way round, a thing can go <b>still</b> past <see cref="m_StillFrom"/>: its motion
    /// fades back to rest. The bookcase shudders until its books float out, then stops.
    /// </para>
    /// </remarks>
    public class PossessedObject : Possessable
    {
        [Header("Motion")]
        [SerializeField] Movement m_Motion = Movement.Rattle;

        [SerializeField, Tooltip("The part that moves. Its origin is the pivot. Empty moves this transform.")]
        Transform m_Body;

        [SerializeField, Tooltip("In the body's own space: the hinge or pivot axis for a turning motion, the direction of travel for " +
            "Hop and Slide.")]
        Vector3 m_Axis = Vector3.right;

        [SerializeField, Tooltip("Metres for Hop, Slide and Rattle; degrees for Roll, Swing, Tilt and Flap; degrees per second for Spin.")]
        float m_Amount = 5f;

        [SerializeField, Tooltip("Natural frequency in Hz: how fast it rocks, swings or springs back. For Rattle, how fast it shakes.")]
        float m_Frequency = 1f;

        [SerializeField, Range(0.02f, 1.5f), Tooltip("Damping ratio of the spring or pendulum. Low keeps it going after a kick.")]
        float m_Damping = 0.3f;

        [SerializeField, Tooltip("How hard a beat kicks it, as a multiple of Amount.")]
        float m_BeatKick = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("Chance a given beat kicks it, so objects on the same band do not move as one.")]
        float m_BeatChance = 0.7f;

        [SerializeField, Tooltip("Roll only: radius of the runners, metres. The body's origin sits at their centre.")]
        float m_RollRadius = 0.85f;

        [SerializeField, Range(0f, 1f), Tooltip("Hop, Tilt and Flap: the speed that survives hitting the floor, the wall or the frame. Low is a thud.")]
        float m_Restitution = 0.25f;

        [SerializeField, Tooltip("Seeds this object's own randomness, so identical objects do not move identically.")]
        int m_Seed;

        [Header("Levitation")]
        [SerializeField, Tooltip("What lifts it into the air: nothing, the room's possession, or the ghosts showing themselves.")]
        LevitateOn m_FloatOn = LevitateOn.Nothing;

        [SerializeField, Range(0f, 1f), Tooltip("Cue (possession or reveal) at which it starts to lift.")]
        float m_FloatFrom = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Cue at which it is as far up and out as it goes.")]
        float m_FloatFull = 1f;

        [SerializeField, Tooltip("Where it floats to at full lift, metres from where it rests, in its parent's axes: up, and out into " +
            "the room for a book.")]
        Vector3 m_FloatTo = new(0f, 0.2f, 0f);

        [SerializeField, Tooltip("Metres it bobs up and down while it floats.")]
        float m_FloatBob = 0.02f;

        [SerializeField, Tooltip("Degrees it slowly tips and turns while it floats.")]
        float m_FloatTumble = 10f;

        [Header("Stillness")]
        [SerializeField, Range(0f, 1f), Tooltip("Possession above which its motion fades away and it goes still, over the next 0.08. " +
            "The bookcase stops shuddering as its books float out. 1 never.")]
        float m_StillFrom = 1f;

        const float k_Gravity = 9.81f;
        const float k_LiftRise = 1.5f, k_LiftFall = 2.5f;
        const float k_StillOver = 0.08f;

        Vector3 m_RestPosition;
        Quaternion m_RestRotation;
        Vector3 m_UnitAxis;
        float m_X;          // displacement: metres or degrees
        float m_V;          // its rate
        float m_Phase;      // the pumped swing's phase
        float m_Swing;      // the pumped swing's amplitude
        float m_Angle;      // Spin
        float m_Jolt;       // Rattle
        float m_Lift;       // levitation, 0 resting to 1 fully up
        float m_NoiseSeed;
        System.Random m_Random;

        void Awake()
        {
            if (m_Body == null)
                m_Body = transform;

            m_RestPosition = m_Body.localPosition;
            m_RestRotation = m_Body.localRotation;
            m_UnitAxis = m_Axis.sqrMagnitude > 1e-6f ? m_Axis.normalized : Vector3.right;
            m_Random = new System.Random(m_Seed != 0 ? m_Seed : GetInstanceID());
            m_NoiseSeed = (float)m_Random.NextDouble() * 100f;
            m_Phase = (float)m_Random.NextDouble() * Mathf.PI * 2f;
        }

        float Omega => 2f * Mathf.PI * Mathf.Max(0.05f, m_Frequency);

        protected override void Animate(float dt)
        {
            bool kick = m_Haunting.Beat && Woken > 0.1f && m_Random.NextDouble() < m_BeatChance;
            float strength = kick ? 0.4f + 0.6f * m_Haunting.BeatStrength : 0f;

            // Every motion writes its pose from rest, or leaves it there; levitation goes on top.
            m_Body.localPosition = m_RestPosition;
            m_Body.localRotation = m_RestRotation;

            switch (m_Motion)
            {
                case Movement.Hop:
                    Hop(dt, strength);
                    break;
                case Movement.Roll:
                case Movement.Swing:
                    Pendulum(dt, strength);
                    break;
                case Movement.Tilt:
                case Movement.Flap:
                    Hinge(dt, strength);
                    break;
                case Movement.Slide:
                    Slide(dt, strength);
                    break;
                case Movement.Rattle:
                    Rattle(dt, strength);
                    break;
                case Movement.Spin:
                    m_Angle = Mathf.Repeat(m_Angle + m_Amount * Level * dt, 360f);
                    m_Body.localRotation = m_RestRotation * Quaternion.AngleAxis(m_Angle, m_UnitAxis);
                    break;
            }

            BeStill();
            Levitate(dt);
        }

        /// <summary>
        /// Past <see cref="m_StillFrom"/> the motion fades back to rest, as if whatever was shaking it
        /// had moved on — for the bookcase, into the books floating out of it. Possession is already
        /// smoothed, so this eases in and out without a jump.
        /// </summary>
        void BeStill()
        {
            if (m_StillFrom >= 1f)
                return;

            float still = Mathf.InverseLerp(m_StillFrom, m_StillFrom + k_StillOver, m_Haunting.Possession);
            if (still <= 0f)
                return;

            still = still * still * (3f - 2f * still);
            m_Body.localPosition = Vector3.Lerp(m_Body.localPosition, m_RestPosition, still);
            m_Body.localRotation = Quaternion.Slerp(m_Body.localRotation, m_RestRotation, still);
        }

        /// <summary>
        /// Lifts the body toward <see cref="m_FloatTo"/> as its cue rises — slowly, and slower still
        /// coming down — with a bob and a slow tumble while it is up.
        /// </summary>
        void Levitate(float dt)
        {
            if (m_FloatOn == LevitateOn.Nothing)
                return;

            float cue = m_FloatOn == LevitateOn.Reveal ? m_Haunting.Reveal : m_Haunting.Possession;
            float target = Mathf.InverseLerp(m_FloatFrom, Mathf.Max(m_FloatFrom + 0.01f, m_FloatFull), cue);
            m_Lift = Mathf.MoveTowards(m_Lift, target, dt / (target > m_Lift ? k_LiftRise : k_LiftFall));
            if (m_Lift <= 0f)
                return;

            float lift = m_Lift * m_Lift * (3f - 2f * m_Lift);
            float t = Time.time;
            float bob = m_FloatBob * Mathf.Sin(t * 1.3f + m_NoiseSeed) * lift;
            m_Body.localPosition += m_FloatTo * lift + Vector3.up * bob;

            float tumble = m_FloatTumble * lift;
            var drift = Quaternion.Euler(Noise(t * 0.35f, 31f) * tumble, Noise(t * 0.25f, 37f) * tumble * 1.5f, Noise(t * 0.3f, 41f) * tumble);
            m_Body.localRotation = drift * m_Body.localRotation;
        }

        public override void Nudge(float strength)
        {
            switch (m_Motion)
            {
                case Movement.Hop:
                    m_V += Mathf.Sqrt(2f * k_Gravity * m_Amount * strength);
                    break;
                case Movement.Rattle:
                    m_Jolt = Mathf.Max(m_Jolt, strength);
                    break;
                case Movement.Spin:
                    m_Angle += 90f * strength;
                    break;
                default:
                    m_V += m_Amount * m_BeatKick * strength * Omega;
                    break;
            }
        }

        /// <summary>A body under gravity: the beat throws it up, the floor stops it with a thud and a small bounce.</summary>
        void Hop(float dt, float kick)
        {
            if (kick > 0f && m_X < m_Amount * 0.3f)
            {
                float height = m_Amount * m_BeatKick * kick * (0.4f + 0.6f * Level);
                m_V = Mathf.Max(m_V, Mathf.Sqrt(2f * k_Gravity * height));
            }

            m_V -= k_Gravity * dt;
            m_X += m_V * dt;
            if (m_X <= 0f)
            {
                m_X = 0f;
                m_V = m_V < -0.05f ? -m_V * m_Restitution : 0f;
            }

            // A shiver while it is awake, so a loud passage without a kick still reads as possessed.
            float t = Time.time * 9f;
            float shiver = 0.004f * Level;
            var jitter = new Vector3(Noise(t, 0f), 0f, Noise(t, 5f)) * shiver;

            m_Body.localPosition = m_RestPosition + m_RestRotation * (m_UnitAxis * m_X) + jitter;
            m_Body.localRotation = m_RestRotation * Quaternion.Euler(Noise(t, 9f) * 60f * shiver, 0f, Noise(t, 13f) * 60f * shiver);
        }

        /// <summary>
        /// A swing pumped to an amplitude set by the level, at the object's natural frequency, with a
        /// damped spring on top that the beat kicks. Roll also moves the body along the floor by the
        /// arc its runners roll through, so the chair rocks rather than pivots.
        /// </summary>
        void Pendulum(float dt, float kick)
        {
            float w = Omega;
            m_Swing = Mathf.MoveTowards(m_Swing, m_Amount * Level, m_Amount * dt / 0.8f);
            m_Phase += w * dt;

            m_V += (-w * w * m_X - 2f * m_Damping * w * m_V) * dt;
            m_X += m_V * dt;
            if (kick > 0f)
                m_V += m_Amount * m_BeatKick * kick * w;

            float angle = m_Swing * Mathf.Sin(m_Phase) + m_X;
            angle = Mathf.Clamp(angle, -m_Amount * 2.5f, m_Amount * 2.5f);
            m_Body.localRotation = m_RestRotation * Quaternion.AngleAxis(angle, m_UnitAxis);

            if (m_Motion == Movement.Roll)
            {
                // Rolling without slipping: the centre travels the arc length the runners turn through.
                var along = Vector3.Cross(m_UnitAxis, Vector3.up);
                m_Body.localPosition = m_RestPosition + m_RestRotation * (along * (m_RollRadius * angle * Mathf.Deg2Rad));
            }
        }

        /// <summary>
        /// A spring on a one-sided hinge. It opens with the level (a door held ajar; a frame stays
        /// flat), the beat throws it open, and it bangs back against its stop.
        /// </summary>
        void Hinge(float dt, float kick)
        {
            float w = Omega;
            float target = m_Motion == Movement.Flap ? m_Amount * 0.5f * Level : 0f;

            m_V += (w * w * (target - m_X) - 2f * m_Damping * w * m_V) * dt;
            m_X += m_V * dt;
            if (kick > 0f)
                m_V += m_Amount * m_BeatKick * kick * w;

            if (m_X < 0f)
            {
                m_X = 0f;
                if (m_V < 0f)
                    m_V = -m_V * m_Restitution;
            }
            else if (m_X > m_Amount * 1.6f)
            {
                m_X = m_Amount * 1.6f;
                m_V = Mathf.Min(m_V, 0f);
            }

            m_Body.localRotation = m_RestRotation * Quaternion.AngleAxis(m_X, m_UnitAxis);
        }

        /// <summary>A spring toward a displacement set by the level, never behind where it started.</summary>
        void Slide(float dt, float kick)
        {
            float w = Omega;
            m_V += (w * w * (m_Amount * Level - m_X) - 2f * m_Damping * w * m_V) * dt;
            m_X += m_V * dt;
            if (kick > 0f)
                m_V += m_Amount * m_BeatKick * kick * w * 0.3f;

            if (m_X < 0f)
            {
                m_X = 0f;
                m_V = Mathf.Max(m_V, 0f);
            }

            m_Body.localPosition = m_RestPosition + m_RestRotation * (m_UnitAxis * m_X);
        }

        /// <summary>Shakes on smooth noise, harder with the level; a beat adds a jolt that dies away.</summary>
        void Rattle(float dt, float kick)
        {
            m_Jolt = Mathf.Max(m_Jolt * Mathf.Exp(-dt / 0.15f), kick);

            float t = Time.time * Mathf.Max(0.1f, m_Frequency);
            float size = m_Amount * (Level + m_Jolt * 1.5f);
            var offset = new Vector3(Noise(t, 0f), Mathf.Abs(Noise(t, 3f)), Noise(t, 7f)) * size;
            float degrees = size * 700f;

            m_Body.localPosition = m_RestPosition + m_RestRotation * offset;
            m_Body.localRotation = m_RestRotation * Quaternion.Euler(Noise(t, 11f) * degrees, Noise(t, 17f) * degrees * 2f,
                Noise(t, 23f) * degrees);
        }

        /// <summary>Smooth noise in −1…1, offset by this object's seed so no two objects share it.</summary>
        float Noise(float t, float channel) => (Mathf.PerlinNoise(t, m_NoiseSeed + channel * 7.1f) - 0.5f) * 2f;
    }
}

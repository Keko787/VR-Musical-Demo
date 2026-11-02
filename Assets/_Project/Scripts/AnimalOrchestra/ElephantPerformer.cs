using UnityEngine;
using VRShootingGallery.TrackingDemo;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The kick drum: an elephant beyond the front wall, facing the room, that stomps a front foot on every
    /// kick. The foot lifts in the moment before and lands on the note, the body drops onto it, and the
    /// water under the foot takes the blow — the ring runs in under the wall and across the floor toward
    /// the viewer, which is what ties the front wall to the floor in the plan's room layout. Feet
    /// alternate, so four on the floor reads as a march rather than one foot hammering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the lift comes from the next note and the landing from the last.</b> Both are functions of
    /// the song clock — the time to the coming kick and the time since the last one — evaluated
    /// separately, so a foot lifting for the next kick never interrupts the body still settling from the
    /// previous one, and the slam lands on the note's frame however the frame rate goes.
    /// </para>
    /// <para>
    /// The stem's low band only moves the ears, a continuous signal on top of the triggers. Strong accents
    /// (a full-velocity kick, no more often than every few seconds) curl the trunk up into a loop — a
    /// trumpet — which marks the downbeats of phrases without being another thing that hits on every beat.
    /// </para>
    /// </remarks>
    public sealed class ElephantPerformer : Performer
    {
        // Rest pose, local: +Z faces the room, origin on the floor under the elephant's middle.
        static readonly Vector3 k_BodyRest = new(0f, 0.62f, -0.08f);
        const float k_HipX = 0.27f;
        const float k_HipY = 0.64f;
        const float k_HipZ = 0.1f;
        const float k_EarRest = 12f;

        // Trunk joints, root to tip: lengths, thickness, the hanging pose and the curl added for a trumpet
        // (degrees about X; negative swings forward and up). The curl is heaviest at the tip, so the trunk
        // rolls up from the end into a loop rather than swinging out into the room as a straight pole.
        static readonly float[] k_TrunkLength = { 0.13f, 0.12f, 0.11f, 0.10f, 0.08f };
        static readonly float[] k_TrunkThickness = { 0.16f, 0.135f, 0.11f, 0.09f, 0.075f };
        static readonly float[] k_TrunkRest = { -6f, 2f, 0f, -8f, -14f };
        static readonly float[] k_TrunkCurl = { -20f, -55f, -85f, -100f, -100f };

        static readonly Color k_Skin = new(0.55f, 0.58f, 0.72f);
        static readonly Color k_SkinDark = new(0.45f, 0.47f, 0.61f);
        static readonly Color k_Leg = new(0.52f, 0.55f, 0.69f);
        static readonly Color k_EarInner = new(0.88f, 0.62f, 0.72f);
        static readonly Color k_Ivory = new(0.96f, 0.93f, 0.84f);
        static readonly Color k_Eye = new(0.08f, 0.07f, 0.12f);
        static readonly Color k_Pad = new(0.42f, 0.42f, 0.53f);
        static readonly Color k_PadGlow = new(1.6f, 0.95f, 0.4f);

        [Header("Stomp")]
        [SerializeField, Tooltip("Seconds from the start of a lift to the foot landing on the kick. The plan's rule is " +
            "under 0.2 s, so a move cannot drift far from its note.")]
        float m_Lead = 0.18f;

        [SerializeField, Tooltip("Seconds for the body to settle after a stomp.")]
        float m_Recover = 0.35f;

        [SerializeField, Tooltip("How high a foot lifts for a full-velocity kick, metres.")]
        float m_LiftHeight = 0.17f;

        [SerializeField, Tooltip("How far the body drops onto the landing foot, metres.")]
        float m_Dip = 0.035f;

        [SerializeField, Tooltip("How far the body rolls toward the landing foot, degrees.")]
        float m_Tilt = 3.5f;

        [Header("Splash")]
        [SerializeField, Tooltip("Depth of the dish a full-velocity stomp presses into the water, metres.")]
        float m_SplashAmplitude = 0.03f;

        [SerializeField, Tooltip("Radius of that dish, metres — about a foot's width, so the ring starts at the foot.")]
        float m_SplashRadius = 0.18f;

        [Header("Trunk")]
        [SerializeField, Range(0f, 1f), Tooltip("Kicks at or above this velocity are accents, and curl the trunk up.")]
        float m_AccentVelocity = 0.95f;

        [SerializeField, Tooltip("Fewest seconds between two trumpets, so the accent marks phrases, not every bar.")]
        float m_TrumpetSpacing = 4f;

        [Header("Rig (set by Build)")]
        [SerializeField] RippleFloor m_Floor;
        [SerializeField] Transform m_Body;
        [SerializeField] Transform m_Torso;
        [SerializeField] Transform m_Head;
        [SerializeField] Transform m_EarLeft, m_EarRight;
        [SerializeField] Transform m_FootLeft, m_FootRight;
        [SerializeField] CreatureTint m_PadLeft, m_PadRight;
        [SerializeField] Transform[] m_Trunk = new Transform[0];

        int m_LastFired = -1;
        double m_TrumpetTime = -100.0;
        float m_Seed;
        float m_LiveBlend;
        float m_Lean, m_Settle, m_Curl;
        float m_EarPhase;

        void Awake() => m_Seed = Motion.Hash01(GetInstanceID()) * 100f;

        protected override void OnSongChanged()
        {
            m_LastFired = -1;
            m_TrumpetTime = -100.0;
        }

        // ================================================================== perform

        protected override void Perform(float deltaTime)
        {
            if (m_Body == null)
                return;

            float clock = Clock + m_Seed;
            m_LiveBlend = Motion.Damp(m_LiveBlend, Live ? 1f : 0f, 0.5f, deltaTime);

            FireImpacts();

            // ---- feet: the coming kick's foot lifts; the last kick's foot is down and the body settles onto it
            float liftLeft = 0f, liftRight = 0f, lean = 0f, settle = 0f, flash = 0f;
            bool landed = TryLast(out var last, out int lastIndex);

            if (TryNext(out var next, out int nextIndex))
            {
                double gap = landed ? next.Time - last.Time : double.MaxValue;
                float lead = (float)System.Math.Min(m_Lead, gap * 0.75);
                Motion.Hit(SongTime - next.Time, lead, m_Recover, out float windup, out float strike);

                float raised = windup * (1f - strike);
                float lift = m_LiftHeight * (0.55f + 0.45f * next.Velocity) * raised;
                float side = Side(nextIndex);
                if (side < 0f)
                    liftLeft = lift;
                else
                    liftRight = lift;

                // Weight shifts off the foot that is coming up.
                lean -= side * raised * 0.6f;
            }

            bool leftLanded = false;
            if (landed)
            {
                Motion.Hit(SongTime - last.Time, m_Lead, m_Recover, out _, out float down);
                settle = down * last.Velocity;
                lean += Side(lastIndex) * settle;
                leftLanded = Side(lastIndex) < 0f;

                double gap = lastIndex > 0 ? last.Time - Notes[lastIndex - 1].Time : double.MaxValue;
                flash = Motion.Pulse(SongTime - last.Time, 0f, 0.08f) * last.Velocity * FlashCap(gap);
            }

            // A slow weight shift from foot to foot while nothing plays.
            lean += (1f - m_LiveBlend) * 0.5f * Motion.Wave(clock, 0.07f);

            // Light smoothing only where a value can jump: the hand-over from one kick's settle to the
            // next kick's weight shift. The feet themselves are left exact.
            m_Lean = Motion.Damp(m_Lean, lean, 0.04f, deltaTime);
            m_Settle = Motion.Damp(m_Settle, settle, 0.025f, deltaTime);

            PoseFeet(liftLeft, liftRight, flash, leftLanded);
            PoseBody(clock);
            PoseEars(deltaTime);
            PoseTrunk(clock, deltaTime);
        }

        /// <summary>
        /// Splashes the water once per kick, on the first frame at or after its note, under the foot that
        /// lands on it. Kicks a stall skipped over by more than a quarter second get no late splash.
        /// </summary>
        void FireImpacts()
        {
            if (!Live)
                return;

            int last = Notes.LastAtOrBefore(SongTime);
            for (int i = m_LastFired + 1; i <= last; i++)
            {
                var note = Notes[i];
                if (SongTime - note.Time > 0.25)
                    continue;

                if (note.Velocity >= m_AccentVelocity && note.Time - m_TrumpetTime >= m_TrumpetSpacing)
                    m_TrumpetTime = note.Time;

                var foot = Side(i) < 0f ? m_FootLeft : m_FootRight;
                if (m_Floor != null && foot != null)
                {
                    // The hip is straight over the pad at the moment of landing; Splash drops the point
                    // onto the sheet.
                    m_Floor.Splash(foot.position, m_SplashAmplitude * note.Velocity * Mathf.Clamp(Intensity, 0.3f, 1.5f),
                        m_SplashRadius);
                }
            }

            if (last > m_LastFired)
                m_LastFired = last;
        }

        void PoseFeet(float liftLeft, float liftRight, float flash, bool leftLanded)
        {
            // Mostly straight up — an elephant's stomp is a lift and a drop, not a kick — with a little
            // forward swing so the leg reads as bending.
            float swing = 12f / Mathf.Max(0.01f, m_LiftHeight);

            m_FootLeft.localPosition = new Vector3(-k_HipX, k_HipY + liftLeft * 0.8f, k_HipZ);
            m_FootLeft.localRotation = Quaternion.Euler(-swing * liftLeft, 0f, 0f);
            m_FootRight.localPosition = new Vector3(k_HipX, k_HipY + liftRight * 0.8f, k_HipZ);
            m_FootRight.localRotation = Quaternion.Euler(-swing * liftRight, 0f, 0f);

            float glow = flash * Intensity;
            if (m_PadLeft != null)
                m_PadLeft.Glow = leftLanded ? glow : 0f;
            if (m_PadRight != null)
                m_PadRight.Glow = leftLanded ? 0f : glow;
        }

        void PoseBody(float clock)
        {
            float breathe = Motion.Wave(clock, 0.2f);
            m_Body.localPosition = k_BodyRest + Vector3.down * (m_Dip * m_Settle) + Vector3.up * (0.006f * breathe);

            // Rolling about Z: positive tips the top toward −X, so the lean is negated to roll toward its side.
            m_Body.localRotation = Quaternion.Euler(1.5f * m_Settle, 0f, -m_Tilt * m_Lean);

            if (m_Torso != null)
                m_Torso.localScale = new Vector3(1.1f, 0.85f * (1f + 0.012f * breathe), 0.44f);

            if (m_Head != null)
            {
                float nod = Mathf.Lerp(1.5f * Motion.Wave(clock, 0.2f), 2.5f * Motion.Wave((float)Beats, 1f), m_LiveBlend);
                float pitch = nod + 4f * m_Settle - 10f * m_Curl;
                float yaw = 4f * (1f - m_LiveBlend) * Motion.Wave(clock, 0.045f);
                m_Head.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        void PoseEars(float deltaTime)
        {
            if (m_EarLeft == null || m_EarRight == null)
                return;

            // An accumulated phase rather than clock × rate, so the flapping speeds up with the low band
            // without its phase jumping.
            float low = Level(Band.Low);
            m_EarPhase = Mathf.Repeat(m_EarPhase + deltaTime * (0.4f + 0.7f * low), 1f);

            float amplitude = 6f + 14f * low;
            float angle = k_EarRest + amplitude * (0.5f + 0.5f * Mathf.Sin(m_EarPhase * 2f * Mathf.PI)) + 8f * m_Settle;

            // Positive about Y swings the left ear's outer edge toward the room, negative the right's.
            m_EarLeft.localRotation = Quaternion.Euler(0f, angle, 0f);
            m_EarRight.localRotation = Quaternion.Euler(0f, -angle, 0f);
        }

        void PoseTrunk(float clock, float deltaTime)
        {
            float curl = 0f;
            if (Live)
            {
                double since = SongTime - m_TrumpetTime;
                if (since >= 0.0)
                    curl = since < 0.3 ? Motion.Smooth01((float)since / 0.3f) : 1f - Motion.Smooth01((float)(since - 0.5) / 0.9f);
            }

            m_Curl = Motion.Damp(m_Curl, curl, 0.05f, deltaTime);

            for (int i = 0; i < m_Trunk.Length; i++)
            {
                var joint = m_Trunk[i];
                if (joint == null)
                    continue;

                // A wave running down the trunk: idle it drifts on its own, playing it swings on the half note.
                float idleSway = 7f * Motion.Wave(clock, 0.16f, -i * 0.1f);
                float liveSway = 5f * Motion.Wave((float)Beats, 0.5f, -i * 0.08f);
                float sway = Mathf.Lerp(idleSway, liveSway, m_LiveBlend) * (1f - 0.7f * m_Curl);

                joint.localRotation = Quaternion.Euler(k_TrunkRest[i] + m_Curl * k_TrunkCurl[i], 0f, sway);
            }
        }

        static float Side(int index) => (index & 1) == 0 ? -1f : 1f;

        /// <summary>Scales a per-note flash down when notes come faster than the room's flash cap allows.</summary>
        float FlashCap(double gap) => Mathf.Clamp01((float)gap * MaxFlashHz);

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            m_Floor = kit.Floor;

            // Hind legs: behind the front pair and mostly hidden; they only fill the gap under the body.
            for (int s = -1; s <= 1; s += 2)
                kit.Solid(transform, s < 0 ? "Hind_Leg_L" : "Hind_Leg_R", kit.Capsule, new Vector3(0.33f * s, 0.3f, -0.17f),
                    new Vector3(0.25f, 0.3f, 0.25f), k_SkinDark);

            m_FootLeft = BuildFrontLeg(kit, -1f, out m_PadLeft);
            m_FootRight = BuildFrontLeg(kit, 1f, out m_PadRight);

            m_Body = kit.Node(transform, "Body", k_BodyRest);
            m_Torso = kit.Ball(m_Body, "Torso", new Vector3(0f, 0.28f, 0f), new Vector3(1.1f, 0.85f, 0.44f), k_Skin).transform;
            kit.Ball(m_Body, "Belly", new Vector3(0f, 0.12f, 0.06f), new Vector3(0.8f, 0.5f, 0.34f), k_Skin);

            m_Head = kit.Node(m_Body, "Head", new Vector3(0f, 0.5f, 0.18f));
            kit.Ball(m_Head, "Skull", Vector3.zero, new Vector3(0.56f, 0.52f, 0.42f), k_Skin);
            kit.Ball(m_Head, "Brow", new Vector3(0f, 0.18f, 0.03f), new Vector3(0.44f, 0.3f, 0.32f), k_Skin);

            m_EarLeft = BuildEar(kit, -1f);
            m_EarRight = BuildEar(kit, 1f);

            for (int s = -1; s <= 1; s += 2)
            {
                kit.Ball(m_Head, "Eye", new Vector3(0.14f * s, 0.07f, 0.19f), new Vector3(0.075f, 0.075f, 0.05f), k_Eye);
                kit.Ball(m_Head, "Eye_Glint", new Vector3(0.127f * s, 0.087f, 0.212f), new Vector3(0.024f, 0.024f, 0.016f), Color.white);

                // Forward, down and a little outward.
                kit.Spike(m_Head, "Tusk", new Vector3(0.12f * s, -0.14f, 0.15f), new Vector3(0.065f, 0.2f, 0.065f), k_Ivory,
                    new Vector3(115f, 15f * s, 0f));
            }

            BuildTrunk(kit);
        }

        Transform BuildFrontLeg(CreatureKit kit, float s, out CreatureTint pad)
        {
            // The joint is at the hip, so a lift can swing the leg as well as raise it.
            var hip = kit.Node(transform, s < 0f ? "Front_Leg_L" : "Front_Leg_R", new Vector3(k_HipX * s, k_HipY, k_HipZ));
            kit.Solid(hip, "Leg", kit.Capsule, new Vector3(0f, -k_HipY * 0.5f, 0f), new Vector3(0.25f, k_HipY * 0.5f, 0.25f), k_Leg);

            // The pad glows on the landing: the stomp's visible cause, readable from the back of the room.
            pad = kit.Lit(hip, "Pad", kit.Cylinder, new Vector3(0f, -k_HipY + 0.02f, 0.01f), new Vector3(0.28f, 0.02f, 0.28f),
                k_Pad, k_PadGlow);
            pad.Glow = 0f;

            for (int n = -1; n <= 1; n++)
                kit.Ball(hip, "Toenail", new Vector3(0.075f * n, -k_HipY + 0.045f, 0.125f - 0.02f * Mathf.Abs(n)),
                    new Vector3(0.065f, 0.055f, 0.04f), k_Ivory);

            return hip;
        }

        Transform BuildEar(CreatureKit kit, float s)
        {
            // Pivot at the ear's inner edge, against the head, so a flap swings the whole fan.
            var pivot = kit.Node(m_Head, s < 0f ? "Ear_L" : "Ear_R", new Vector3(0.22f * s, 0.02f, -0.02f),
                new Vector3(0f, -k_EarRest * s, 0f));
            kit.Ball(pivot, "Ear_Outer", new Vector3(0.25f * s, -0.03f, 0f), new Vector3(0.5f, 0.6f, 0.07f), k_Skin);
            kit.Ball(pivot, "Ear_Inner", new Vector3(0.24f * s, -0.04f, 0.025f), new Vector3(0.38f, 0.46f, 0.04f), k_EarInner);
            return pivot;
        }

        void BuildTrunk(CreatureKit kit)
        {
            m_Trunk = new Transform[k_TrunkLength.Length];

            var parent = kit.Node(m_Head, "Trunk", new Vector3(0f, -0.13f, 0.15f));
            var at = Vector3.zero;
            for (int i = 0; i < k_TrunkLength.Length; i++)
            {
                var joint = kit.Limb(parent, $"Trunk_{i}", at, new Vector3(k_TrunkRest[i], 0f, 0f), k_TrunkLength[i],
                    k_TrunkThickness[i], k_Skin);
                m_Trunk[i] = joint;
                parent = joint;
                at = new Vector3(0f, -k_TrunkLength[i], 0f);
            }

            kit.Ball(parent, "Trunk_Tip", at, new Vector3(0.085f, 0.05f, 0.085f), k_SkinDark);
        }
    }
}

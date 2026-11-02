using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The choir: three wolves sitting on a rocky ridge beyond the right wall, howling toward the moon. One wolf per
    /// harmony voice — while a voice sounds, its wolf howls, head raised higher the higher the note, jaw
    /// opening with the stem's loudness — so a chord change reads as heads shifting against each other,
    /// and a three-part harmony is three wolves at three heights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Voices to wolves.</b> Each frame the sounding notes are sorted by pitch and dealt to the wolves
    /// back to front: the lowest voice to the wolf furthest from the moon, the highest to the one nearest.
    /// One voice goes to the middle wolf, two to the outer pair, and a fourth or fifth voice shares the
    /// lowest, middle and highest — three wolves, never a fourth that appears from nowhere. Choir parts
    /// are voice-led, so voices seldom cross and a wolf keeps its voice through a phrase.
    /// </para>
    /// <para>
    /// The pitch is placed within the part's own range (the track's lowest to highest note), so any song's
    /// choir uses the whole height of the howl.
    /// </para>
    /// </remarks>
    public sealed class WolfPackPerformer : Performer
    {
        const int k_Wolves = 3;

        // The ridge: overlapping rocks along the wall, beyond it. Local x ≤ 0 is the side facing the room;
        // the wolves sit on the tops of rocks 1, 3 and 5.
        static readonly Vector3[] k_RockCentre =
        {
            new(-0.24f, 0.20f, -0.80f), new(-0.27f, 0.30f, -0.50f), new(-0.25f, 0.24f, -0.24f), new(-0.27f, 0.36f, 0.00f),
            new(-0.25f, 0.25f, 0.25f), new(-0.27f, 0.32f, 0.50f), new(-0.24f, 0.21f, 0.78f),
        };

        static readonly Vector3[] k_RockSize =
        {
            new(0.46f, 0.50f, 0.30f), new(0.52f, 0.62f, 0.42f), new(0.48f, 0.52f, 0.40f), new(0.52f, 0.72f, 0.44f),
            new(0.48f, 0.54f, 0.40f), new(0.52f, 0.64f, 0.42f), new(0.46f, 0.46f, 0.32f),
        };

        static readonly int[] k_WolfRock = { 1, 3, 5 };

        // The moon is far out over the water, ahead of the wolves and low in the sky, so it is seen through
        // the front of the right wall just above their heads. A sphere rather than a disc: it reads as round
        // from every eye, and at this distance its stereo depth is close to the sky's.
        static readonly Vector3 k_MoonCentre = new(5.0f, 2.2f, 2.5f);
        const float k_MoonDiameter = 1.4f;

        static readonly Color[] k_RockColor = { new(0.30f, 0.27f, 0.36f), new(0.27f, 0.25f, 0.33f), new(0.33f, 0.29f, 0.38f) };
        static readonly Color k_Fur = new(0.45f, 0.50f, 0.60f);
        static readonly Color k_FurLight = new(0.75f, 0.78f, 0.85f);
        static readonly Color k_Nose = new(0.10f, 0.09f, 0.13f);
        static readonly Color k_Mouth = new(0.22f, 0.10f, 0.14f);
        static readonly Color k_Eye = new(0.85f, 0.60f, 0.20f);
        static readonly Color k_EyeGlow = new(1.0f, 0.62f, 0.18f);
        static readonly Color k_Moon = new(1.0f, 0.94f, 0.78f, 0.85f);
        static readonly Color k_MoonGlow = new(0.85f, 0.75f, 0.52f);
        static readonly Color k_Halo = new(1.0f, 0.85f, 0.6f, 0.06f);
        static readonly Color k_HaloGlow = new(0.30f, 0.22f, 0.13f);

        [Header("Howl")]
        [SerializeField, Tooltip("Neck lift at the top of the choir's range, degrees. The bottom of the range lifts " +
            "55% of this, so even the lowest voice is clearly a howl.")]
        float m_NeckLift = 55f;

        [SerializeField, Tooltip("Extra head tilt at a full howl, degrees, on top of the neck.")]
        float m_HeadLift = 18f;

        [SerializeField, Tooltip("How far the jaw opens at full loudness, degrees.")]
        float m_JawOpen = 30f;

        [SerializeField, Tooltip("Seconds for a head to get halfway to its new height — slow enough to read as a " +
            "howl rising, fast enough to follow a chord change.")]
        float m_HowlHalfLife = 0.15f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform[] m_Wolves = new Transform[0];
        [SerializeField] Transform[] m_Necks = new Transform[0];
        [SerializeField] Transform[] m_Heads = new Transform[0];
        [SerializeField] Transform[] m_Jaws = new Transform[0];
        [SerializeField] Transform[] m_EarsLeft = new Transform[0];
        [SerializeField] Transform[] m_EarsRight = new Transform[0];
        [SerializeField] Transform[] m_Tails = new Transform[0];
        [SerializeField] CreatureTint[] m_Eyes = new CreatureTint[0];
        [SerializeField] CreatureTint m_Moon;
        [SerializeField] float[] m_SeatHeight = new float[0];

        readonly List<Note> m_Active = new();
        readonly int[] m_VoicePitch = new int[k_Wolves];
        readonly float[] m_Howl = new float[k_Wolves];
        readonly float[] m_Jaw = new float[k_Wolves];

        float m_Seed;
        float m_MoonGlow = 1f;

        void Awake() => m_Seed = Motion.Hash01(GetInstanceID()) * 100f;

        // ================================================================== perform

        protected override void Perform(float deltaTime)
        {
            if (m_Necks.Length < k_Wolves)
                return;

            AssignVoices();

            float level = Level();
            float clock = Clock + m_Seed;

            for (int w = 0; w < k_Wolves; w++)
            {
                int pitch = m_VoicePitch[w];
                bool singing = pitch >= 0;

                float howl = singing ? 0.55f + 0.45f * Motion.PitchNorm(pitch, Notes.MinPitch, Notes.MaxPitch) : 0f;
                float jaw = singing ? Mathf.Clamp01(0.25f + 1.1f * level) : 0f;
                m_Howl[w] = Motion.Damp(m_Howl[w], howl, m_HowlHalfLife, deltaTime);
                m_Jaw[w] = Motion.Damp(m_Jaw[w], jaw, 0.07f, deltaTime);

                PoseWolf(w, clock);
            }

            // The moon brightens a little with the choir, never more than a slow swell.
            m_MoonGlow = Motion.Damp(m_MoonGlow, 0.9f + 0.5f * level, 0.4f, deltaTime);
            if (m_Moon != null)
                m_Moon.Glow = m_MoonGlow * Intensity;
        }

        /// <summary>Deals the sounding notes, lowest first, to the wolves back to front. −1 is silent.</summary>
        void AssignVoices()
        {
            for (int w = 0; w < k_Wolves; w++)
                m_VoicePitch[w] = -1;

            ActiveNotes(m_Active);
            int count = m_Active.Count;
            if (count == 0)
                return;

            // Insertion sort by pitch: a handful of notes, and List.Sort with a comparison allocates.
            for (int i = 1; i < count; i++)
            {
                var note = m_Active[i];
                int j = i - 1;
                while (j >= 0 && m_Active[j].Pitch > note.Pitch)
                {
                    m_Active[j + 1] = m_Active[j];
                    j--;
                }

                m_Active[j + 1] = note;
            }

            switch (count)
            {
                case 1:
                    m_VoicePitch[1] = m_Active[0].Pitch;
                    break;
                case 2:
                    m_VoicePitch[0] = m_Active[0].Pitch;
                    m_VoicePitch[2] = m_Active[1].Pitch;
                    break;
                default:
                    m_VoicePitch[0] = m_Active[0].Pitch;
                    m_VoicePitch[1] = m_Active[count / 2].Pitch;
                    m_VoicePitch[2] = m_Active[count - 1].Pitch;
                    break;
            }
        }

        void PoseWolf(int w, float clock)
        {
            float howl = m_Howl[w];
            float quiet = 1f - howl;
            float phase = w * 0.37f;

            // Idle: looks about, ears twitch now and then, tail sweeps. Howling: the look settles forward.
            float lookYaw = 40f * (Mathf.PerlinNoise(clock * 0.12f, w * 7.3f) - 0.5f) * quiet;
            float lookPitch = 10f * (Mathf.PerlinNoise(clock * 0.09f, w * 3.1f + 20f) - 0.5f) * quiet;

            m_Necks[w].localRotation = Quaternion.Euler(lookPitch - m_NeckLift * howl, lookYaw, 0f);

            if (m_Heads[w] != null)
                m_Heads[w].localRotation = Quaternion.Euler(4f * quiet - m_HeadLift * howl, 0f, 6f * Motion.Wave(clock, 0.05f, phase) * quiet);

            if (m_Jaws[w] != null)
                m_Jaws[w].localRotation = Quaternion.Euler(m_JawOpen * m_Jaw[w], 0f, 0f);

            if (m_EarsLeft[w] != null)
                m_EarsLeft[w].localRotation = Quaternion.Euler(-10f, 0f, 15f + Twitch(clock, w * 2));
            if (m_EarsRight[w] != null)
                m_EarsRight[w].localRotation = Quaternion.Euler(-10f, 0f, -15f - Twitch(clock, w * 2 + 1));

            if (m_Tails[w] != null)
                m_Tails[w].localRotation = Quaternion.Euler(70f, 14f * Motion.Wave(clock, 0.3f + 0.25f * howl, phase), 0f);

            // Breathing, and a rise in the chest when howling.
            if (m_Wolves[w] != null && w < m_SeatHeight.Length)
            {
                var p = m_Wolves[w].localPosition;
                p.y = m_SeatHeight[w] + 0.004f * Motion.Wave(clock, 0.3f, phase) + 0.015f * howl;
                m_Wolves[w].localPosition = p;
            }

            if (m_Eyes.Length >= 2 * (w + 1))
            {
                float glow = (0.5f + 0.9f * howl) * Intensity;
                if (m_Eyes[2 * w] != null)
                    m_Eyes[2 * w].Glow = glow;
                if (m_Eyes[2 * w + 1] != null)
                    m_Eyes[2 * w + 1].Glow = glow;
            }
        }

        /// <summary>
        /// An ear's flick, degrees: every few seconds, by chance, a quick swing out and back. Deterministic
        /// per cycle, so it costs nothing to keep and never repeats in step across the wolves.
        /// </summary>
        static float Twitch(float clock, int ear)
        {
            const float rate = 0.27f;
            float phase = clock * rate + ear * 0.61f;
            int cycle = Mathf.FloorToInt(phase);
            float seconds = (phase - cycle) / rate;
            if (seconds > 0.18f || Motion.Hash01(cycle * 13 + ear * 5) < 0.6f)
                return 0f;

            return 25f * Mathf.Sin(Mathf.PI * seconds / 0.18f);
        }

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            for (int i = 0; i < k_RockCentre.Length; i++)
                kit.Ball(transform, $"Ridge_Rock_{i}", k_RockCentre[i], k_RockSize[i], k_RockColor[i % k_RockColor.Length],
                    new Vector3(0f, 8f * (i % 3 - 1), 0f));

            // Boulders along the foot of the ridge, so it sits on the water rather than ending in a straight line.
            kit.Ball(transform, "Ridge_Boulder_0", new Vector3(-0.44f, 0.07f, -0.35f), new Vector3(0.16f, 0.14f, 0.2f), k_RockColor[1]);
            kit.Ball(transform, "Ridge_Boulder_1", new Vector3(-0.44f, 0.09f, 0.05f), new Vector3(0.18f, 0.18f, 0.22f), k_RockColor[2]);
            kit.Ball(transform, "Ridge_Boulder_2", new Vector3(-0.45f, 0.06f, 0.38f), new Vector3(0.14f, 0.12f, 0.18f), k_RockColor[0]);

            // The moon, with a faint halo round it.
            kit.Glowing(transform, "Moon_Halo", kit.Sphere, k_MoonCentre, Vector3.one * (k_MoonDiameter * 1.5f), k_Halo, k_HaloGlow);
            m_Moon = kit.Glowing(transform, "Moon", kit.Sphere, k_MoonCentre, Vector3.one * k_MoonDiameter, k_Moon, k_MoonGlow);

            m_Wolves = new Transform[k_Wolves];
            m_Necks = new Transform[k_Wolves];
            m_Heads = new Transform[k_Wolves];
            m_Jaws = new Transform[k_Wolves];
            m_EarsLeft = new Transform[k_Wolves];
            m_EarsRight = new Transform[k_Wolves];
            m_Tails = new Transform[k_Wolves];
            m_Eyes = new CreatureTint[k_Wolves * 2];
            m_SeatHeight = new float[k_Wolves];

            for (int w = 0; w < k_Wolves; w++)
            {
                int rock = k_WolfRock[w];
                float seat = k_RockCentre[rock].y + k_RockSize[rock].y * 0.5f - 0.02f;
                m_SeatHeight[w] = seat;

                // Facing ahead (+Z), toward the moon, turned a quarter toward the room so the viewer sees them in profile.
                m_Wolves[w] = kit.Node(transform, $"Wolf_{w}", new Vector3(k_RockCentre[rock].x, seat, k_RockCentre[rock].z),
                    new Vector3(0f, -25f, 0f));
                BuildWolf(kit, w);
            }
        }

        void BuildWolf(CreatureKit kit, int w)
        {
            var wolf = m_Wolves[w];

            kit.Ball(wolf, "Haunch", new Vector3(0f, 0.09f, -0.06f), new Vector3(0.26f, 0.2f, 0.3f), k_Fur);
            kit.Ball(wolf, "Chest", new Vector3(0f, 0.26f, 0.04f), new Vector3(0.2f, 0.36f, 0.22f), k_Fur, new Vector3(10f, 0f, 0f));
            kit.Ball(wolf, "Ruff", new Vector3(0f, 0.29f, 0.12f), new Vector3(0.15f, 0.24f, 0.1f), k_FurLight, new Vector3(10f, 0f, 0f));

            for (int s = -1; s <= 1; s += 2)
            {
                kit.Ball(wolf, "Hind_Paw", new Vector3(0.09f * s, 0.02f, 0.07f), new Vector3(0.075f, 0.045f, 0.12f), k_FurLight);
                kit.Limb(wolf, "Foreleg", new Vector3(0.06f * s, 0.3f, 0.1f), new Vector3(-6f, 0f, 0f), 0.29f, 0.06f, k_Fur);
                kit.Ball(wolf, "Fore_Paw", new Vector3(0.06f * s, 0.02f, 0.14f), new Vector3(0.07f, 0.045f, 0.1f), k_FurLight);
            }

            // Tail: back along the rock, tip curling up.
            // The tail's fur runs 3 cm past the tip's joint, so the pale tip grows out of it.
            m_Tails[w] = kit.Limb(wolf, "Tail", new Vector3(0f, 0.07f, -0.19f), new Vector3(70f, 0f, 0f), 0.2f, 0.07f, k_Fur);
            kit.Limb(m_Tails[w], "Tail_Tip", new Vector3(0f, -0.17f, 0f), new Vector3(20f, 0f, 0f), 0.13f, 0.06f, k_FurLight);

            // Neck → head → jaw: the howl lifts the neck and tilts the head, the jaw opens with the sound.
            var neck = kit.Node(wolf, "Neck", new Vector3(0f, 0.4f, 0.07f));
            kit.Solid(neck, "Neck_Fur", kit.Capsule, new Vector3(0f, 0.06f, 0.01f), new Vector3(0.12f, 0.08f, 0.12f), k_Fur, new Vector3(15f, 0f, 0f));
            m_Necks[w] = neck;

            var head = kit.Node(neck, "Head", new Vector3(0f, 0.14f, 0.03f));
            m_Heads[w] = head;
            kit.Ball(head, "Skull", Vector3.zero, new Vector3(0.16f, 0.15f, 0.19f), k_Fur);
            kit.Ball(head, "Cheeks", new Vector3(0f, -0.02f, 0.03f), new Vector3(0.15f, 0.1f, 0.12f), k_FurLight);
            kit.Ball(head, "Snout", new Vector3(0f, -0.015f, 0.12f), new Vector3(0.08f, 0.07f, 0.16f), k_FurLight);
            kit.Ball(head, "Nose", new Vector3(0f, 0.005f, 0.2f), new Vector3(0.04f, 0.03f, 0.035f), k_Nose);
            kit.Ball(head, "Mouth", new Vector3(0f, -0.04f, 0.1f), new Vector3(0.06f, 0.03f, 0.12f), k_Mouth);

            var jaw = kit.Node(head, "Jaw", new Vector3(0f, -0.045f, 0.03f));
            kit.Ball(jaw, "Jaw_Fur", new Vector3(0f, -0.012f, 0.075f), new Vector3(0.065f, 0.035f, 0.14f), k_FurLight);
            m_Jaws[w] = jaw;

            for (int s = -1; s <= 1; s += 2)
            {
                var ear = kit.Node(head, s < 0 ? "Ear_L" : "Ear_R", new Vector3(0.045f * s, 0.06f, -0.03f), new Vector3(-10f, 0f, -15f * s));
                kit.Spike(ear, "Ear_Fur", Vector3.zero, new Vector3(0.05f, 0.09f, 0.035f), k_Fur);
                if (s < 0)
                    m_EarsLeft[w] = ear;
                else
                    m_EarsRight[w] = ear;

                var eye = kit.Lit(head, "Eye", kit.Sphere, new Vector3(0.042f * s, 0.025f, 0.075f), new Vector3(0.028f, 0.022f, 0.02f), k_Eye, k_EyeGlow);
                eye.Glow = 0.5f;
                m_Eyes[2 * w + (s < 0 ? 0 : 1)] = eye;
            }
        }
    }
}

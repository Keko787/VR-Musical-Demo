using UnityEngine;
using VRShootingGallery.TrackingDemo;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The drops: a tiger sitting on a crag beyond the right wall, behind and above the wolves, who roars when
    /// a song lifts into its chorus — Mizzou's tiger, saved for the biggest moments. Before the drop it
    /// crouches and draws breath; on it, it rears up, turns to the viewer and roars, jaws wide and eyes
    /// lit, holds it, and settles. The rest of the time it sits and watches: the room, the wolves, the moon.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a part of its own.</b> A roar is a rare, big event, which is what a drop is: a built-in song has
    /// one on the downbeat of each chorus the energy jumps into (about two a song), and a music-folder track
    /// one wherever the pipe rig's score hears a crash. Sharing a part would have it roaring on every kick.
    /// </para>
    /// <para>
    /// <b>Why behind the wolves.</b> The right wall's window had room above the wolves' heads and between
    /// them, and a tiger there reads as the top of the pecking order: it is on the highest rock, its head
    /// above theirs. It sits further out than the ridge so the wolves stay in front, and low enough that its
    /// raised head stays under the top of the right wall from every test eye.
    /// </para>
    /// </remarks>
    public sealed class TigerPerformer : Performer
    {
        /// <summary>Height of the crag's top, where the tiger sits.</summary>
        const float k_CragTop = 0.95f;

        static readonly Color[] k_Rock = { new(0.30f, 0.27f, 0.36f), new(0.27f, 0.25f, 0.33f), new(0.33f, 0.29f, 0.38f) };

        [Header("Roar")]
        [SerializeField, Range(0.1f, 1.5f), Tooltip("Seconds of windup before the drop: it crouches and draws breath.")]
        float m_Windup = 0.7f;

        [SerializeField, Range(0.3f, 3f), Tooltip("Seconds the roar is held at full.")]
        float m_Hold = 1.3f;

        [SerializeField, Range(0.2f, 2f), Tooltip("Seconds it takes to settle after the roar.")]
        float m_Settle = 0.9f;

        [SerializeField, Range(0f, 70f), Tooltip("How wide the jaws open at a full roar, degrees.")]
        float m_JawOpen = 50f;

        [SerializeField, Range(0f, 60f), Tooltip("How far the neck and head rear up on the roar, degrees.")]
        float m_Rear = 30f;

        [SerializeField, Range(0f, 60f), Tooltip("Furthest the head turns to face the viewer, degrees.")]
        float m_MaxYaw = 50f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Body;
        [SerializeField] Transform m_Chest;
        [SerializeField] Transform m_Neck;
        [SerializeField] Transform m_Head;
        [SerializeField] Transform m_Jaw;
        [SerializeField] Transform m_EarLeft;
        [SerializeField] Transform m_EarRight;
        [SerializeField] Transform m_Tail;
        [SerializeField] Transform m_TailTip;
        [SerializeField] CreatureTint[] m_Eyes = new CreatureTint[0];
        [SerializeField] Transform m_Room;
        [SerializeField] Vector3 m_ViewerEye = new(0f, 1.6f, -0.35f);

        float m_Seed, m_Yaw, m_Pitch;

        void Awake() => m_Seed = Motion.Hash01(GetInstanceID()) * 100f;

        // ================================================================== perform

        protected override void Perform(float deltaTime)
        {
            if (m_Head == null)
                return;

            float clock = Clock + m_Seed;

            // The roar's shape from the time to the drop: a crouch through the windup, a fast attack, the hold,
            // then the settle. Shaped from the clock each frame, never accumulated, like every hit here.
            float crouch = 0f, roar = 0f, strength = 1f;
            if (TryHit(m_Windup, m_Hold + m_Settle, out double tau, out float lead, out var note, out _))
            {
                strength = 0.6f + 0.4f * note.Velocity;
                float t = (float)tau;
                if (t < 0f)
                    crouch = Motion.Smooth01((t + lead) / Mathf.Max(0.01f, lead));
                else
                {
                    roar = t < 0.08f ? t / 0.08f : t < m_Hold ? 1f : 1f - Motion.Smooth01((t - m_Hold) / m_Settle);
                    crouch = Mathf.Max(0f, 1f - t / 0.12f);
                }
            }

            // The gaze: about the scene when sitting, on the viewer when roaring.
            LookAtViewer(out float viewerYaw, out float viewerPitch);
            float idleYaw = 50f * (Mathf.PerlinNoise(clock * 0.07f, 3.1f) - 0.5f);
            float idlePitch = 10f * (Mathf.PerlinNoise(clock * 0.05f, 7.7f) - 0.5f);
            float attend = Mathf.Max(roar, crouch * 0.6f);
            m_Yaw = Motion.Damp(m_Yaw, Mathf.Lerp(idleYaw, viewerYaw, attend), 0.25f, deltaTime);
            m_Pitch = Motion.Damp(m_Pitch, Mathf.Lerp(idlePitch, viewerPitch, attend), 0.25f, deltaTime);

            float breathe = Motion.Wave(clock, 0.22f);
            float shake = roar * Motion.Wave(clock, 11f);

            m_Body.localPosition = new Vector3(0f, k_CragTop - 0.04f * crouch + 0.03f * roar, 0f);
            m_Chest.localRotation = Quaternion.Euler(6f * crouch - 9f * roar * strength, 0f, 0f);
            m_Chest.localScale = new Vector3(1f + 0.012f * breathe + 0.05f * roar, 1f, 1f + 0.012f * breathe + 0.05f * roar);

            m_Neck.localRotation = Quaternion.Euler(14f * crouch - 0.6f * m_Rear * roar * strength + m_Pitch * 0.5f, m_Yaw * 0.6f, 0f);
            m_Head.localRotation = Quaternion.Euler(10f * crouch - 0.4f * m_Rear * roar * strength + m_Pitch * 0.5f + 1.5f * shake,
                m_Yaw * 0.4f, 4f * Motion.Wave(clock, 0.04f) * (1f - roar));

            // Jaws: shut, a snarl through the windup, wide on the roar with a shudder in it.
            m_Jaw.localRotation = Quaternion.Euler(10f * crouch + (m_JawOpen * strength + 3f * shake) * roar, 0f, 0f);

            // Ears back for the crouch and the roar; an idle twitch otherwise.
            float back = Mathf.Max(crouch, roar);
            if (m_EarLeft != null)
                m_EarLeft.localRotation = Quaternion.Euler(-35f * back, 0f, 12f + 25f * back + Twitch(clock, 0));
            if (m_EarRight != null)
                m_EarRight.localRotation = Quaternion.Euler(-35f * back, 0f, -12f - 25f * back - Twitch(clock, 1));

            // The tail sweeps slowly, lashing through the roar.
            if (m_Tail != null)
                m_Tail.localRotation = Quaternion.Euler(95f - 25f * roar, 20f * Motion.Wave(clock, 0.15f + 0.9f * roar), 0f);
            if (m_TailTip != null)
                m_TailTip.localRotation = Quaternion.Euler(-60f + 15f * Motion.Wave(clock, 0.3f, 0.2f), 0f, 0f);

            // The eyes light on the roar: one slow swell per drop, well under any flash cap.
            foreach (var eye in m_Eyes)
                if (eye != null)
                    eye.Glow = (0.35f + 1.3f * roar) * Intensity;
        }

        /// <summary>The yaw and pitch, in the chest's frame, that put the head on the viewer — the tracked head, or the default eye.</summary>
        void LookAtViewer(out float yaw, out float pitch)
        {
            if (!TrackedHead.IsTracked || !TrackedHead.TryGetPose(out var target, out _))
                target = m_Room != null ? m_Room.TransformPoint(m_ViewerEye) : m_ViewerEye;

            var direction = m_Chest.InverseTransformDirection(target - m_Head.position);
            float flat = new Vector2(direction.x, direction.z).magnitude;
            yaw = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -m_MaxYaw, m_MaxYaw);
            pitch = Mathf.Clamp(-Mathf.Atan2(direction.y, flat) * Mathf.Rad2Deg, -25f, 25f);
        }

        /// <summary>An ear's flick now and then, degrees; deterministic per cycle.</summary>
        static float Twitch(float clock, int ear)
        {
            const float rate = 0.21f;
            float phase = clock * rate + ear * 0.53f;
            int cycle = Mathf.FloorToInt(phase);
            float seconds = (phase - cycle) / rate;
            if (seconds > 0.2f || Motion.Hash01(cycle * 17 + ear * 3) < 0.55f)
                return 0f;

            return 20f * Mathf.Sin(Mathf.PI * seconds / 0.2f);
        }

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            m_Room = kit.Room;
            m_ViewerEye = kit.ViewerEye;

            // The crag: three stacked rocks and two at its foot, standing in the water.
            kit.Ball(transform, "Crag_Base", new Vector3(0f, 0.28f, 0f), new Vector3(1.0f, 0.56f, 1.2f), k_Rock[0]);
            kit.Ball(transform, "Crag_Middle", new Vector3(0.04f, 0.6f, -0.04f), new Vector3(0.82f, 0.5f, 0.98f), k_Rock[1], new Vector3(0f, 12f, 0f));
            kit.Ball(transform, "Crag_Top", new Vector3(0f, 0.85f, 0f), new Vector3(0.84f, 0.24f, 0.9f), k_Rock[2], new Vector3(0f, -8f, 0f));
            kit.Ball(transform, "Crag_Boulder_0", new Vector3(-0.42f, 0.12f, 0.38f), new Vector3(0.3f, 0.24f, 0.3f), k_Rock[1]);
            kit.Ball(transform, "Crag_Boulder_1", new Vector3(0.38f, 0.1f, -0.5f), new Vector3(0.28f, 0.2f, 0.3f), k_Rock[2]);

            // Sitting upright on top, like the cat on the rock in every picture of one: haunches down, front
            // legs straight, chest up. Everything above the crag moves with the body; the front of it — chest,
            // forelegs, neck and head — rears on its own pivot at the hips.
            m_Body = kit.Node(transform, "Body", new Vector3(0f, k_CragTop, 0f));
            var haunchCentre = new Vector3(0f, 0.2f, -0.18f);
            var haunchSize = new Vector3(0.5f, 0.42f, 0.55f);
            kit.Ball(m_Body, "Haunch", haunchCentre, haunchSize, Tigers.Orange);
            Tigers.Stripes(kit, m_Body, "Haunch", haunchCentre, haunchSize * 0.5f, 0.05f, 0.016f,
                (-0.02f, 60f, 125f), (0.07f, 80f, 150f), (0.14f, 65f, 115f));
            foreach (float s in new[] { -1f, 1f })
                kit.Ball(m_Body, s < 0f ? "Hind_Foot_L" : "Hind_Foot_R", new Vector3(0.2f * s, 0.04f, 0.04f), new Vector3(0.13f, 0.08f, 0.24f), Tigers.Orange);

            m_Chest = kit.Node(m_Body, "Chest", new Vector3(0f, 0.22f, -0.02f));
            var torso = kit.Node(m_Chest, "Torso", new Vector3(0f, 0.3f, 0.12f), new Vector3(18f, 0f, 0f));
            var torsoSize = new Vector3(0.4f, 0.68f, 0.44f);
            kit.Ball(torso, "Torso_Fur", Vector3.zero, torsoSize, Tigers.Orange);
            kit.Ball(torso, "Chest_White", new Vector3(0f, -0.04f, 0.14f), new Vector3(0.26f, 0.5f, 0.2f), Tigers.White);
            Tigers.Stripes(kit, torso, "Torso", Vector3.zero, torsoSize * 0.5f, 0.045f, 0.016f,
                (-0.2f, 70f, 140f), (-0.1f, 55f, 125f), (0.0f, 75f, 150f), (0.1f, 60f, 120f), (0.2f, 80f, 135f));

            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                var leg = kit.Limb(m_Chest, "Foreleg_" + side, new Vector3(0.11f * s, 0.42f, 0.24f), Vector3.zero, 0.6f, 0.12f, Tigers.Orange);
                kit.Ball(leg, "Fore_Paw", new Vector3(0f, -0.62f, 0.04f), new Vector3(0.15f, 0.08f, 0.18f), Tigers.White);
                kit.Ball(leg, "Leg_Stripe", new Vector3(0f, -0.3f, -0.01f), new Vector3(0.124f, 0.03f, 0.124f), Tigers.Stripe);
            }

            m_Neck = kit.Node(m_Chest, "Neck", new Vector3(0f, 0.6f, 0.22f));
            kit.Ball(m_Neck, "Neck_Fur", new Vector3(0f, 0.06f, 0.03f), new Vector3(0.28f, 0.32f, 0.28f), Tigers.Orange);

            // A big cat's head is big: scaled up as a whole, so the parts below keep their places on it.
            m_Head = kit.Node(m_Neck, "Head", new Vector3(0f, 0.17f, 0.08f));
            m_Head.localScale = Vector3.one * 1.25f;
            BuildHead(kit);

            // The tail lies back along the crag, its tip curling up.
            m_Tail = kit.Limb(m_Body, "Tail", new Vector3(0.12f, 0.06f, -0.42f), new Vector3(95f, 20f, 0f), 0.42f, 0.075f, Tigers.Orange);
            kit.Ball(m_Tail, "Tail_Stripe", new Vector3(0f, -0.28f, 0f), new Vector3(0.08f, 0.03f, 0.08f), Tigers.Stripe);
            m_TailTip = kit.Limb(m_Tail, "Tail_Tip", new Vector3(0f, -0.4f, 0f), new Vector3(-60f, 0f, 0f), 0.3f, 0.065f, Tigers.Orange);
            kit.Ball(m_TailTip, "Tail_Stripe", new Vector3(0f, -0.12f, 0f), new Vector3(0.07f, 0.025f, 0.07f), Tigers.Stripe);
            kit.Ball(m_TailTip, "Tail_End", new Vector3(0f, -0.29f, 0f), new Vector3(0.075f, 0.09f, 0.075f), Tigers.Stripe);
        }

        void BuildHead(CreatureKit kit)
        {
            kit.Ball(m_Head, "Skull", Vector3.zero, new Vector3(0.34f, 0.3f, 0.32f), Tigers.Orange);
            kit.Ball(m_Head, "Muzzle", new Vector3(0f, -0.06f, 0.15f), new Vector3(0.18f, 0.11f, 0.16f), Tigers.White);
            kit.Ball(m_Head, "Nose", new Vector3(0f, -0.02f, 0.23f), new Vector3(0.07f, 0.04f, 0.05f), Tigers.Nose);
            m_Eyes = new CreatureTint[2];

            // Inside, hidden while the jaws are shut: the dark of the mouth and the upper fangs.
            kit.Ball(m_Head, "Mouth", new Vector3(0f, -0.1f, 0.11f), new Vector3(0.14f, 0.06f, 0.16f), Tigers.Mouth);

            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                kit.Ball(m_Head, "Ruff_" + side, new Vector3(0.12f * s, -0.075f, 0.05f), new Vector3(0.1f, 0.12f, 0.13f), Tigers.White,
                    new Vector3(0f, 0f, -30f * s));
                kit.Ball(m_Head, "Cheek_Stripe_" + side, new Vector3(0.16f * s, -0.02f, 0.0f), new Vector3(0.02f, 0.11f, 0.035f), Tigers.Stripe,
                    new Vector3(0f, 0f, 25f * s));
                kit.Ball(m_Head, "Brow_" + side, new Vector3(0.07f * s, 0.075f, 0.13f), new Vector3(0.07f, 0.03f, 0.04f), Tigers.White);

                var eye = kit.Lit(m_Head, "Eye_" + side, kit.Sphere, new Vector3(0.075f * s, 0.035f, 0.135f), new Vector3(0.055f, 0.042f, 0.03f),
                    Tigers.Eye, Tigers.EyeGlow);
                eye.Glow = 0.35f;
                kit.Ball(m_Head, "Pupil_" + side, new Vector3(0.077f * s, 0.035f, 0.149f), new Vector3(0.014f, 0.034f, 0.01f), Tigers.Stripe);
                m_Eyes[s < 0f ? 0 : 1] = eye;

                var ear = kit.Node(m_Head, "Ear_" + side, new Vector3(0.12f * s, 0.13f, -0.03f), new Vector3(0f, 0f, -12f * s));
                kit.Ball(ear, "Ear_Fur", new Vector3(0f, 0.03f, 0f), new Vector3(0.1f, 0.1f, 0.035f), Tigers.Orange);
                kit.Ball(ear, "Ear_Inner", new Vector3(0f, 0.025f, 0.012f), new Vector3(0.06f, 0.06f, 0.02f), Tigers.White);
                if (s < 0f)
                    m_EarLeft = ear;
                else
                    m_EarRight = ear;

                kit.Spike(m_Head, "Fang_Upper_" + side, new Vector3(0.04f * s, -0.095f, 0.19f), new Vector3(0.024f, 0.055f, 0.024f), Tigers.White,
                    new Vector3(180f, 0f, 0f));
            }

            // Forehead stripes.
            for (int i = 0; i < 3; i++)
                kit.Ball(m_Head, $"Forehead_Stripe_{i}", new Vector3((i - 1) * 0.045f, 0.125f - 0.01f * Mathf.Abs(i - 1), 0.09f),
                    new Vector3(0.022f, 0.022f, 0.09f), Tigers.Stripe, new Vector3(-30f, 0f, (i - 1) * 18f));

            m_Jaw = kit.Node(m_Head, "Jaw", new Vector3(0f, -0.1f, 0.05f));
            kit.Ball(m_Jaw, "Jaw_Fur", new Vector3(0f, -0.025f, 0.09f), new Vector3(0.15f, 0.06f, 0.18f), Tigers.White);
            kit.Ball(m_Jaw, "Tongue", new Vector3(0f, 0.004f, 0.08f), new Vector3(0.09f, 0.025f, 0.12f), Tigers.Tongue);
            foreach (float s in new[] { -1f, 1f })
                kit.Spike(m_Jaw, s < 0f ? "Fang_Lower_L" : "Fang_Lower_R", new Vector3(0.035f * s, 0f, 0.16f),
                    new Vector3(0.018f, 0.038f, 0.018f), Tigers.White);
        }
    }

    /// <summary>What the two tigers share: their colours, and stripes laid on a body's curve.</summary>
    static class Tigers
    {
        public static readonly Color Orange = new(0.93f, 0.50f, 0.13f);
        public static readonly Color White = new(0.97f, 0.94f, 0.88f);
        public static readonly Color Stripe = new(0.11f, 0.08f, 0.08f);
        public static readonly Color Nose = new(0.62f, 0.33f, 0.36f);
        public static readonly Color Mouth = new(0.30f, 0.06f, 0.08f);
        public static readonly Color Tongue = new(0.90f, 0.42f, 0.50f);
        public static readonly Color Eye = new(0.95f, 0.76f, 0.22f);
        public static readonly Color EyeGlow = new(1.0f, 0.70f, 0.16f);

        /// <summary>
        /// Stripes on both flanks of an ellipsoid (<paramref name="centre"/>, <paramref name="radii"/>, axis-aligned in
        /// <paramref name="parent"/>'s frame). Each band is at a height on the body and runs round it from one azimuth
        /// to another — 0° the front, 180° the back — made of short overlapping pieces, each lying on the curve where
        /// it is and a shade inside the surface, so the band follows the body round rather than standing off it.
        /// Each band tapers at its ends.
        /// </summary>
        public static void Stripes(CreatureKit kit, Transform parent, string name, Vector3 centre, Vector3 radii, float width,
            float thickness, params (float height, float from, float to)[] bands)
        {
            const float step = 15f;
            int n = 0;
            foreach (var (height, from, to) in bands)
            {
                float across = Mathf.Sqrt(Mathf.Max(0f, 1f - height * height / (radii.y * radii.y)));
                int pieces = Mathf.Max(1, Mathf.CeilToInt((to - from) / step));
                for (int i = 0; i <= pieces; i++)
                {
                    float azimuth = Mathf.Lerp(from, to, i / (float)pieces);
                    float taper = Mathf.Lerp(0.45f, 1f, Mathf.Sin(Mathf.PI * i / pieces));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float a = azimuth * Mathf.Deg2Rad;
                        var local = new Vector3(radii.x * across * Mathf.Sin(a) * side, height, radii.z * across * Mathf.Cos(a));
                        var normal = new Vector3(local.x / (radii.x * radii.x), local.y / (radii.y * radii.y), local.z / (radii.z * radii.z)).normalized;
                        var round = Vector3.Cross(Vector3.up, normal);
                        if (round.sqrMagnitude < 1e-6f)
                            continue;

                        // Long enough to overlap the next piece by about a third.
                        float radius = Mathf.Lerp(radii.x, radii.z, Mathf.Abs(Mathf.Cos(a))) * across;
                        float length = 1.35f * step * Mathf.Deg2Rad * radius;
                        var rotation = Quaternion.LookRotation(round.normalized, normal);
                        kit.Ball(parent, $"{name}_Stripe_{n++}", centre + local - normal * (thickness * 0.3f),
                            new Vector3(width * taper, thickness, length), Stripe, rotation.eulerAngles);
                    }
                }
            }
        }
    }
}

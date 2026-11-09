using System.Collections.Generic;
using UnityEngine;
using VRShootingGallery.TrackingDemo;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The lead singer: a tiger cub sitting on a rock front and centre, a metre from the tracked viewer and
    /// facing them — Mizzou's tiger, young. It sings with its whole face: the jaw drops with the vocal
    /// stem's loudness, showing a dark mouth, a pink tongue and its little fangs; the head tips back as it
    /// sings out; the ears prick up. On a held note it throws both front paws up and out, tips its head
    /// further back and quivers with vibrato. Between phrases it shuts its mouth, bobs to the beat, swishes
    /// its tail, blinks, and watches the viewer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It sings the same part as the parrot, a duet: the vocal. Like the parrot's beak, the jaw follows the
    /// stem's envelope, which closes on the breaths between words where a melody's notes do not, with a
    /// floor under it so it shuts fully between phrases even over a mixed track's bleed. The paws and the
    /// head thrown back are note-driven: a held note is a fact the envelope cannot tell from a loud short one.
    /// </para>
    /// <para>
    /// A cub's proportions are what make it read as one at this size: a head nearly as wide as its body,
    /// big paws, round ears, big eyes. And a mouth is easier to see on a cat than on a bird: a jaw that drops
    /// shows its inside from the front, where a beak only parts.
    /// </para>
    /// </remarks>
    public sealed class TigerCubPerformer : Performer
    {
        const float k_RockTop = 0.47f;

        static readonly Color[] k_Rock = { new(0.33f, 0.30f, 0.38f), new(0.29f, 0.27f, 0.35f) };

        [Header("Voice")]
        [SerializeField, Range(0f, 60f), Tooltip("How far the jaw drops at full voice, degrees.")]
        float m_JawOpen = 38f;

        [SerializeField, Range(0f, 0.6f), Tooltip("Vocal level that counts as silence: the mouth shuts fully below it.")]
        float m_VoiceFloor = 0.15f;

        [SerializeField, Range(0.005f, 0.2f), Tooltip("Smoothing on the jaw, seconds to halve the gap. Short, so it closes between words.")]
        float m_JawHalfLife = 0.03f;

        [SerializeField, Range(0f, 45f), Tooltip("How far the head tips back at full voice, degrees.")]
        float m_HeadLift = 18f;

        [SerializeField, Range(0f, 45f), Tooltip("How much further it tips back on a held note, degrees.")]
        float m_HeldLift = 14f;

        [SerializeField, Range(0f, 120f), Tooltip("How far the front paws lift on a held note, degrees.")]
        float m_PawsUp = 80f;

        [SerializeField, Range(0f, 10f), Tooltip("Vibrato on a held note: how far the jaw and the head quiver, degrees.")]
        float m_Vibrato = 4f;

        [SerializeField, Range(0f, 1f), Tooltip("A note counts as held once it has sounded this long, seconds.")]
        float m_HeldAfter = 0.2f;

        [SerializeField, Range(0.1f, 2f), Tooltip("Only notes at least this long count as held, seconds.")]
        float m_HeldMinimum = 0.4f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Body;
        [SerializeField] Transform m_Chest;
        [SerializeField] Transform m_Neck;
        [SerializeField] Transform m_Head;
        [SerializeField] Transform m_Jaw;
        [SerializeField] Transform m_ShoulderLeft;
        [SerializeField] Transform m_ShoulderRight;
        [SerializeField] Transform m_EarLeft;
        [SerializeField] Transform m_EarRight;
        [SerializeField] Transform m_Tail;
        [SerializeField] Transform[] m_Eyes = new Transform[0];
        [SerializeField] Transform m_Room;
        [SerializeField] Vector3 m_ViewerEye = new(0f, 1.6f, -0.35f);

        readonly List<Note> m_Active = new();

        float m_Live, m_Open, m_Held, m_Lift, m_Paws, m_Yaw, m_Pitch;

        // ================================================================== perform

        protected override void Perform(float deltaTime)
        {
            if (m_Head == null)
                return;

            m_Live = Motion.Damp(m_Live, Live ? 1f : 0f, 0.3f, deltaTime);
            float level = Mathf.Clamp01(Level());
            float voice = Motion.Smooth01((level - m_VoiceFloor) / Mathf.Max(0.05f, 0.8f - m_VoiceFloor)) * m_Live;
            m_Held = Motion.Damp(m_Held, HeldNote() ? 1f : 0f, 0.12f, deltaTime);
            float vibrato = m_Held * Motion.Wave(Clock, 5.5f);

            // Jaw: drops with the voice; a little idle mew now and then so a quiet cub is not a statue.
            float openTarget = voice * m_JawOpen + (1f - m_Live) * IdleMew();
            m_Open = Motion.Damp(m_Open, openTarget, m_JawHalfLife, deltaTime);
            m_Jaw.localRotation = Quaternion.Euler(m_Open + m_Vibrato * vibrato * voice, 0f, 0f);

            // Front paws: up and out on a held note — a singer's arms.
            m_Paws = Motion.Damp(m_Paws, m_Held * m_Live, 0.1f, deltaTime);
            float wave = 6f * m_Paws * Motion.Wave(Clock, 1.1f);
            if (m_ShoulderLeft != null)
                m_ShoulderLeft.localRotation = Quaternion.Euler(-m_PawsUp * m_Paws, 0f, -28f * m_Paws - wave);
            if (m_ShoulderRight != null)
                m_ShoulderRight.localRotation = Quaternion.Euler(-m_PawsUp * m_Paws, 0f, 28f * m_Paws + wave);

            // Body: sways over two beats, the chest puffs with the voice, and leans back a little with the paws up.
            float beat = (float)(Beats - System.Math.Floor(Beats));
            float sway = Mathf.Lerp(1.5f, 4.5f, m_Live) * Mathf.Sin((float)(Beats * System.Math.PI));
            float puff = 1f + 0.012f * Motion.Wave(Clock, 0.45f) + 0.05f * voice;
            m_Body.localRotation = Quaternion.Euler(0f, 0f, sway);
            m_Chest.localRotation = Quaternion.Euler(-8f * m_Paws, 0f, 0f);
            m_Chest.localScale = new Vector3(puff, 1f, puff);

            // Head: looks at the viewer and nods on the beat; tips back as it sings out, further on a held note.
            Gaze(deltaTime, out float yaw, out float pitchToViewer);
            m_Lift = Motion.Damp(m_Lift, voice * m_HeadLift + m_Held * m_HeldLift * m_Live, 0.12f, deltaTime);
            float nod = Mathf.Lerp(2f, 6f, m_Live) * (0.5f - 0.5f * Mathf.Cos(beat * 2f * Mathf.PI)) * (1f - 0.7f * m_Held);
            float roll = 8f * Mathf.Sin(Clock * 0.31f) * (1f - 0.6f * m_Live) - sway + m_Vibrato * 0.5f * vibrato;
            float headPitch = Mathf.Max(-55f, pitchToViewer * 0.5f + nod - m_Lift);
            m_Neck.localRotation = Quaternion.Euler(pitchToViewer * 0.5f, yaw * 0.5f, 0f);
            m_Head.localRotation = Quaternion.Euler(headPitch, yaw * 0.5f, roll);

            // Ears prick up and forward as it sings; an idle flick otherwise.
            float prick = Mathf.Max(voice, m_Held);
            if (m_EarLeft != null)
                m_EarLeft.localRotation = Quaternion.Euler(-12f * prick, 0f, 15f - 10f * prick + Flick(0));
            if (m_EarRight != null)
                m_EarRight.localRotation = Quaternion.Euler(-12f * prick, 0f, -15f + 10f * prick - Flick(1));

            // Tail swishes on the beat, a lazy curl when idle.
            if (m_Tail != null)
                m_Tail.localRotation = Quaternion.Euler(100f, 25f * Mathf.Sin((float)(Beats * System.Math.PI)) * Mathf.Lerp(0.4f, 1f, m_Live), 0f);

            // Blinks.
            float blink = Blink();
            foreach (var eye in m_Eyes)
                if (eye != null)
                    eye.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.12f, blink), 1f);
        }

        /// <summary>True while the current vocal note is long and has sounded long enough to count as held.</summary>
        bool HeldNote()
        {
            ActiveNotes(m_Active);
            foreach (var note in m_Active)
                if (note.Duration >= m_HeldMinimum && SongTime - note.Time >= m_HeldAfter)
                    return true;

            return false;
        }

        /// <summary>Turns toward the viewer's head in the chest's frame, clamped, so it turns its head and not its whole body round.</summary>
        void Gaze(float deltaTime, out float yaw, out float pitch)
        {
            if (!TrackedHead.IsTracked || !TrackedHead.TryGetPose(out var target, out _))
                target = m_Room != null ? m_Room.TransformPoint(m_ViewerEye) : m_ViewerEye;

            var direction = m_Chest.InverseTransformDirection(target - m_Head.position);
            float flat = new Vector2(direction.x, direction.z).magnitude;
            float targetYaw = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -60f, 60f);
            float targetPitch = Mathf.Clamp(-Mathf.Atan2(direction.y, flat) * Mathf.Rad2Deg, -40f, 30f);
            m_Yaw = Motion.Damp(m_Yaw, targetYaw, 0.15f, deltaTime);
            m_Pitch = Motion.Damp(m_Pitch, targetPitch, 0.15f, deltaTime);
            yaw = m_Yaw;
            pitch = m_Pitch;
        }

        /// <summary>A small open-and-shut every few seconds, skipped on some cycles: a cub mewing to itself.</summary>
        static float IdleMew()
        {
            const float period = 5.3f;
            int cycle = Mathf.FloorToInt(Clock / period);
            if (Motion.Hash01(cycle + 41) < 0.5f)
                return 0f;

            float t = Clock - cycle * period;
            return t < 0.45f ? 16f * Mathf.Sin(t / 0.45f * Mathf.PI) : 0f;
        }

        /// <summary>A blink every few seconds, 0 open to 1 shut.</summary>
        static float Blink()
        {
            const float period = 3.7f;
            int cycle = Mathf.FloorToInt(Clock / period);
            float t = Clock - cycle * period - 1.5f * Motion.Hash01(cycle + 7);
            return t >= 0f && t < 0.14f ? Mathf.Sin(t / 0.14f * Mathf.PI) : 0f;
        }

        static float Flick(int ear)
        {
            const float rate = 0.23f;
            float phase = Clock * rate + ear * 0.47f;
            int cycle = Mathf.FloorToInt(phase);
            float seconds = (phase - cycle) / rate;
            if (seconds > 0.18f || Motion.Hash01(cycle * 11 + ear * 7) < 0.6f)
                return 0f;

            return 18f * Mathf.Sin(Mathf.PI * seconds / 0.18f);
        }

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            m_Room = kit.Room;
            m_ViewerEye = kit.ViewerEye;

            // Its rock: two stones, the top one flat enough to sit on.
            kit.Ball(transform, "Rock_Base", new Vector3(0f, 0.17f, 0f), new Vector3(0.52f, 0.34f, 0.48f), k_Rock[0]);
            kit.Ball(transform, "Rock_Top", new Vector3(0.01f, 0.38f, -0.01f), new Vector3(0.44f, 0.2f, 0.42f), k_Rock[1], new Vector3(0f, 15f, 0f));

            // Sitting up: haunches down, front legs straight, a big head. +Z faces the viewer.
            m_Body = kit.Node(transform, "Body", new Vector3(0f, k_RockTop, 0f));
            var haunchCentre = new Vector3(0f, 0.1f, -0.06f);
            var haunchSize = new Vector3(0.26f, 0.2f, 0.26f);
            kit.Ball(m_Body, "Haunch", haunchCentre, haunchSize, Tigers.Orange);
            Tigers.Stripes(kit, m_Body, "Haunch", haunchCentre, haunchSize * 0.5f, 0.025f, 0.01f, (-0.01f, 70f, 130f), (0.05f, 90f, 150f));
            foreach (float s in new[] { -1f, 1f })
                kit.Ball(m_Body, s < 0f ? "Hind_Foot_L" : "Hind_Foot_R", new Vector3(0.09f * s, 0.025f, 0.07f), new Vector3(0.08f, 0.05f, 0.12f), Tigers.Orange);

            m_Chest = kit.Node(m_Body, "Chest", new Vector3(0f, 0.1f, 0f));
            var torso = kit.Node(m_Chest, "Torso", new Vector3(0f, 0.12f, 0.03f), new Vector3(8f, 0f, 0f));
            var torsoSize = new Vector3(0.2f, 0.27f, 0.2f);
            kit.Ball(torso, "Torso_Fur", Vector3.zero, torsoSize, Tigers.Orange);
            kit.Ball(torso, "Chest_White", new Vector3(0f, -0.01f, 0.07f), new Vector3(0.13f, 0.19f, 0.08f), Tigers.White);
            Tigers.Stripes(kit, torso, "Torso", Vector3.zero, torsoSize * 0.5f, 0.022f, 0.01f, (-0.07f, 70f, 135f), (0.0f, 60f, 120f),
                (0.07f, 80f, 140f));

            // Front legs on shoulder pivots, so a held note can lift both paws.
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                var shoulder = kit.Node(m_Chest, "Shoulder_" + side, new Vector3(0.06f * s, 0.2f, 0.08f));
                var leg = kit.Limb(shoulder, "Foreleg", Vector3.zero, Vector3.zero, 0.29f, 0.068f, Tigers.Orange);
                kit.Ball(leg, "Paw", new Vector3(0f, -0.3f, 0.02f), new Vector3(0.085f, 0.06f, 0.09f), Tigers.White);
                kit.Ball(leg, "Leg_Stripe", new Vector3(0f, -0.14f, -0.005f), new Vector3(0.07f, 0.018f, 0.07f), Tigers.Stripe);
                if (s < 0f)
                    m_ShoulderLeft = shoulder;
                else
                    m_ShoulderRight = shoulder;
            }

            m_Neck = kit.Node(m_Chest, "Neck", new Vector3(0f, 0.25f, 0.05f));
            kit.Ball(m_Neck, "Neck_Fur", new Vector3(0f, 0.03f, 0.01f), new Vector3(0.15f, 0.14f, 0.14f), Tigers.Orange);
            m_Head = kit.Node(m_Neck, "Head", new Vector3(0f, 0.1f, 0.02f));
            BuildHead(kit);

            // The tail curls round the rock's top.
            m_Tail = kit.Limb(m_Body, "Tail", new Vector3(0.06f, 0.04f, -0.18f), new Vector3(100f, 25f, 0f), 0.19f, 0.045f, Tigers.Orange);
            var tip = kit.Limb(m_Tail, "Tail_Tip", new Vector3(0f, -0.18f, 0f), new Vector3(-55f, 0f, 0f), 0.13f, 0.04f, Tigers.Orange);
            kit.Ball(m_Tail, "Tail_Stripe", new Vector3(0f, -0.11f, 0f), new Vector3(0.05f, 0.016f, 0.05f), Tigers.Stripe);
            kit.Ball(tip, "Tail_End", new Vector3(0f, -0.12f, 0f), new Vector3(0.045f, 0.055f, 0.045f), Tigers.Stripe);
        }

        void BuildHead(CreatureKit kit)
        {
            kit.Ball(m_Head, "Skull", Vector3.zero, new Vector3(0.24f, 0.21f, 0.21f), Tigers.Orange);
            kit.Ball(m_Head, "Muzzle", new Vector3(0f, -0.045f, 0.09f), new Vector3(0.115f, 0.07f, 0.09f), Tigers.White);
            kit.Ball(m_Head, "Nose", new Vector3(0f, -0.014f, 0.135f), new Vector3(0.038f, 0.024f, 0.026f), Tigers.Nose);

            // Inside, hidden while it is shut: the dark of the mouth and two little fangs.
            kit.Ball(m_Head, "Mouth", new Vector3(0f, -0.062f, 0.07f), new Vector3(0.075f, 0.035f, 0.075f), Tigers.Mouth);

            m_Eyes = new Transform[2];
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                kit.Ball(m_Head, "Cheek_" + side, new Vector3(0.062f * s, -0.048f, 0.06f), new Vector3(0.075f, 0.065f, 0.07f), Tigers.White);
                kit.Ball(m_Head, "Cheek_Stripe_" + side, new Vector3(0.11f * s, -0.01f, 0.01f), new Vector3(0.012f, 0.07f, 0.022f), Tigers.Stripe,
                    new Vector3(0f, 0f, 25f * s));
                kit.Ball(m_Head, "Brow_" + side, new Vector3(0.05f * s, 0.06f, 0.085f), new Vector3(0.045f, 0.02f, 0.025f), Tigers.White);

                // Big eyes for a cub: a dark eye with a glint, on its own node so it can blink.
                var eye = kit.Node(m_Head, "Eye_" + side, new Vector3(0.048f * s, 0.022f, 0.088f));
                kit.Ball(eye, "Eye_Dark", Vector3.zero, new Vector3(0.04f, 0.045f, 0.026f), Tigers.Stripe);
                kit.Ball(eye, "Eye_Glint", new Vector3(0.007f * s, 0.01f, 0.011f), Vector3.one * 0.012f, Color.white);
                m_Eyes[s < 0f ? 0 : 1] = eye;

                var ear = kit.Node(m_Head, "Ear_" + side, new Vector3(0.085f * s, 0.09f, -0.01f), new Vector3(0f, 0f, 15f * -s));
                kit.Ball(ear, "Ear_Fur", new Vector3(0f, 0.02f, 0f), new Vector3(0.07f, 0.07f, 0.025f), Tigers.Orange);
                kit.Ball(ear, "Ear_Inner", new Vector3(0f, 0.018f, 0.009f), new Vector3(0.04f, 0.04f, 0.014f), Tigers.White);
                if (s < 0f)
                    m_EarLeft = ear;
                else
                    m_EarRight = ear;

                kit.Spike(m_Head, "Fang_" + side, new Vector3(0.022f * s, -0.066f, 0.115f), new Vector3(0.013f, 0.024f, 0.013f), Tigers.White,
                    new Vector3(180f, 0f, 0f));
            }

            for (int i = 0; i < 3; i++)
                kit.Ball(m_Head, $"Forehead_Stripe_{i}", new Vector3((i - 1) * 0.03f, 0.085f - 0.008f * Mathf.Abs(i - 1), 0.055f),
                    new Vector3(0.014f, 0.014f, 0.06f), Tigers.Stripe, new Vector3(-35f, 0f, (i - 1) * 18f));

            m_Jaw = kit.Node(m_Head, "Jaw", new Vector3(0f, -0.06f, 0.03f));
            kit.Ball(m_Jaw, "Jaw_Fur", new Vector3(0f, -0.014f, 0.055f), new Vector3(0.082f, 0.032f, 0.095f), Tigers.White);
            kit.Ball(m_Jaw, "Tongue", new Vector3(0f, 0.004f, 0.05f), new Vector3(0.05f, 0.016f, 0.065f), Tigers.Tongue);
        }
    }
}

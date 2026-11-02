using System.Collections.Generic;
using UnityEngine;
using VRShootingGallery.TrackingDemo;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The lead vocalist: a macaw-like parrot on a tall perch at eye level, nearest the front wall, so the
    /// vocals and the elephant behind it share one view. Its lower beak drops with the vocal stem's
    /// loudness, its crest fans up with it, and on a held note it spreads its wings — the three things a
    /// singer's face and arms do. Between phrases it bobs to the beat and watches the tracked viewer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the beak follows the stem, not the notes.</b> Lip sync is judged on the consonants and the
    /// breaths between words, which a melody's notes do not carry; the stem's smoothed envelope does, so
    /// the beak closes on every gap the voice makes. The wings are the one note-driven part: a held note
    /// is a musical fact the envelope cannot tell from a loud short one.
    /// </para>
    /// <para>
    /// <b>Why it looks at the viewer.</b> It is the one animal within arm's reach at eye height, and a
    /// head that follows the tracked glasses is the most direct proof the room knows where they are. The
    /// head counter-rotates against the body's sway, the way a bird holds its head still. With the
    /// tracker offline it looks at the viewer's default position instead, so it never stares at nothing.
    /// </para>
    /// </remarks>
    public sealed class ParrotPerformer : Performer
    {
        static readonly float[] k_CrestSpread = { -20f, -10f, 0f, 10f, 20f };

        [Header("Voice")]
        [SerializeField, Range(0f, 60f), Tooltip("How far the lower beak drops at full vocal level, degrees.")]
        float m_BeakOpen = 32f;

        [SerializeField, Range(0.005f, 0.2f), Tooltip("Smoothing on the beak, seconds to halve the gap. Short, so " +
            "it closes on the gaps between words.")]
        float m_BeakHalfLife = 0.025f;

        [SerializeField, Range(0f, 1f), Tooltip("A note counts as held once it has sounded this long, seconds.")]
        float m_HeldAfter = 0.2f;

        [SerializeField, Range(0.1f, 2f), Tooltip("Only notes at least this long spread the wings, seconds.")]
        float m_HeldMinimum = 0.4f;

        [SerializeField, Range(0f, 90f), Tooltip("How far the wings swing out on a held note, degrees.")]
        float m_WingFlare = 72f;

        [Header("Gaze")]
        [SerializeField, Range(0f, 90f), Tooltip("Furthest the head turns left or right to follow the viewer, degrees.")]
        float m_MaxYaw = 60f;

        [SerializeField, Range(0f, 60f), Tooltip("Furthest the head tips up or down to follow the viewer, degrees.")]
        float m_MaxPitch = 30f;

        [SerializeField, Range(0.02f, 1f), Tooltip("Smoothing on the gaze, seconds to halve the gap.")]
        float m_GazeHalfLife = 0.15f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Body;
        [SerializeField] Transform m_Head;
        [SerializeField] Transform m_Jaw;
        [SerializeField] Transform m_LeftWing;
        [SerializeField] Transform m_RightWing;
        [SerializeField] Transform m_Tail;
        [SerializeField] Transform[] m_Crest;
        [SerializeField] Transform m_Room;
        [SerializeField] Vector3 m_ViewerEye = new(0f, 1.6f, -0.35f);

        readonly List<Note> m_Active = new();

        float m_Live, m_Open, m_Raise, m_Flare, m_Yaw, m_Pitch;

        // ================================================================== body

        public override void Build(CreatureKit kit)
        {
            var wood = new Color(0.46f, 0.31f, 0.19f);
            var darkWood = new Color(0.30f, 0.20f, 0.13f);
            var red = new Color(0.86f, 0.09f, 0.11f);
            var belly = new Color(0.96f, 0.36f, 0.16f);
            var yellow = new Color(1.00f, 0.80f, 0.12f);
            var blue = new Color(0.10f, 0.36f, 0.92f);
            var white = new Color(0.96f, 0.95f, 0.92f);
            var ivory = new Color(0.93f, 0.90f, 0.80f);
            var charcoal = new Color(0.16f, 0.16f, 0.18f);
            var grey = new Color(0.45f, 0.45f, 0.48f);

            m_Room = kit.Room;
            m_ViewerEye = kit.ViewerEye;

            // The stand: a weighted base, a pole and a crossbar, so it reads as furniture rather than a
            // bird floating at eye level.
            kit.Solid(transform, "Stand_Base", kit.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(0.26f, 0.012f, 0.26f), darkWood);
            kit.Solid(transform, "Stand_Pole", kit.Cylinder, new Vector3(0f, 0.65f, 0f), new Vector3(0.03f, 0.65f, 0.03f), wood);
            kit.Solid(transform, "Perch", kit.Cylinder, new Vector3(0f, 1.3f, 0f), new Vector3(0.03f, 0.17f, 0.03f), wood,
                new Vector3(0f, 0f, 90f));
            kit.Ball(transform, "Perch_End_L", new Vector3(-0.17f, 1.3f, 0f), Vector3.one * 0.04f, darkWood);
            kit.Ball(transform, "Perch_End_R", new Vector3(0.17f, 1.3f, 0f), Vector3.one * 0.04f, darkWood);

            // Parrot frame: on top of the bar, +Z toward the viewer.
            var parrot = kit.Node(transform, "Parrot", new Vector3(0f, 1.315f, 0f));

            // Feet grip the bar: a pad on top and a toe either side of it.
            foreach (float x in new[] { -0.03f, 0.03f })
            {
                string side = x < 0f ? "L" : "R";
                kit.Ball(parrot, "Foot_" + side, new Vector3(x, 0.005f, 0f), new Vector3(0.03f, 0.02f, 0.035f), grey);
                kit.Ball(parrot, "Toe_Front_" + side, new Vector3(x, -0.012f, 0.018f), new Vector3(0.012f, 0.022f, 0.012f), grey);
                kit.Ball(parrot, "Toe_Back_" + side, new Vector3(x, -0.012f, -0.018f), new Vector3(0.012f, 0.022f, 0.012f), grey);
            }

            // Everything above the feet sways together; the head is under it so it can counter the sway.
            m_Body = kit.Node(parrot, "Body", Vector3.zero);
            kit.Ball(m_Body, "Torso", new Vector3(0f, 0.13f, -0.005f), new Vector3(0.15f, 0.24f, 0.14f), red, new Vector3(15f, 0f, 0f));
            kit.Ball(m_Body, "Belly", new Vector3(0f, 0.11f, 0.035f), new Vector3(0.12f, 0.17f, 0.08f), belly, new Vector3(12f, 0f, 0f));

            m_Tail = kit.Node(m_Body, "Tail", new Vector3(0f, 0.04f, -0.05f), new Vector3(28f, 0f, 0f));
            kit.Ball(m_Tail, "Tail_Red", new Vector3(0f, -0.10f, 0f), new Vector3(0.055f, 0.20f, 0.02f), red);
            kit.Ball(m_Tail, "Tail_Blue", new Vector3(0f, -0.20f, -0.002f), new Vector3(0.045f, 0.10f, 0.018f), blue);

            m_LeftWing = BuildWing(kit, m_Body, "Wing_L", -1f, red, yellow, blue);
            m_RightWing = BuildWing(kit, m_Body, "Wing_R", 1f, red, yellow, blue);

            m_Head = kit.Node(m_Body, "Head", new Vector3(0f, 0.235f, 0.02f));
            kit.Ball(m_Head, "Skull", new Vector3(0f, 0.04f, 0.005f), new Vector3(0.11f, 0.11f, 0.12f), red);
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                kit.Ball(m_Head, "Face_" + side, new Vector3(0.040f * s, 0.035f, 0.030f), new Vector3(0.035f, 0.055f, 0.06f), white);
                // Set into the front of the white patch, so the eye is framed by it from the front and the side.
                kit.Ball(m_Head, "Eye_" + side, new Vector3(0.047f * s, 0.045f, 0.050f), Vector3.one * 0.02f, charcoal);
                kit.Ball(m_Head, "Eye_Glint_" + side, new Vector3(0.051f * s, 0.050f, 0.057f), Vector3.one * 0.007f, Color.white);
            }

            // Hooked upper beak, pale with a dark tip; the lower beak hangs from its own hinge.
            kit.Spike(m_Head, "Beak_Upper", new Vector3(0f, 0.03f, 0.06f), new Vector3(0.045f, 0.07f, 0.04f), ivory,
                new Vector3(118f, 0f, 0f));
            kit.Spike(m_Head, "Beak_Hook", new Vector3(0f, 0.005f, 0.11f), new Vector3(0.02f, 0.03f, 0.02f), charcoal,
                new Vector3(170f, 0f, 0f));
            m_Jaw = kit.Node(m_Head, "Jaw", new Vector3(0f, 0.012f, 0.055f));
            kit.Spike(m_Jaw, "Beak_Lower", Vector3.zero, new Vector3(0.035f, 0.045f, 0.03f), charcoal, new Vector3(100f, 0f, 0f));

            // Crest: five feathers on their own pivots, lying back until the voice lifts them.
            m_Crest = new Transform[k_CrestSpread.Length];
            for (int i = 0; i < m_Crest.Length; i++)
            {
                float x = (i - (m_Crest.Length - 1) * 0.5f) * 0.012f;
                var pivot = kit.Node(m_Head, $"Crest_{i}", new Vector3(x, 0.085f, -0.01f), new Vector3(-65f, 0f, k_CrestSpread[i]));
                kit.Ball(pivot, "Feather", new Vector3(0f, 0.035f, 0f), new Vector3(0.016f, 0.075f, 0.012f),
                    i == m_Crest.Length / 2 ? yellow : red);
                m_Crest[i] = pivot;
            }
        }

        /// <summary>
        /// One wing on a shoulder pivot: a red shoulder, yellow coverts and blue flight feathers, hanging
        /// along the body side and swept back. <paramref name="side"/> is −1 for the left.
        /// </summary>
        static Transform BuildWing(CreatureKit kit, Transform body, string name, float side, Color red, Color yellow, Color blue)
        {
            var wing = kit.Node(body, name, new Vector3(0.072f * side, 0.20f, -0.01f), FoldedWing(side));
            kit.Ball(wing, "Shoulder", new Vector3(0f, -0.01f, 0.005f), new Vector3(0.03f, 0.05f, 0.06f), red);
            kit.Ball(wing, "Coverts", new Vector3(0f, -0.05f, 0f), new Vector3(0.025f, 0.11f, 0.08f), yellow);
            kit.Ball(wing, "Flight", new Vector3(0.002f * side, -0.13f, -0.01f), new Vector3(0.02f, 0.17f, 0.07f), blue);
            return wing;
        }

        static Vector3 FoldedWing(float side) => new(20f, 0f, 6f * side);

        // ================================================================== performance

        protected override void Perform(float deltaTime)
        {
            if (m_Head == null)
                return;

            m_Live = Motion.Damp(m_Live, Live ? 1f : 0f, 0.3f, deltaTime);
            float level = Mathf.Clamp01(Level());
            float idle = 1f - m_Live;

            // Beak: the stem's envelope, plus a little idle chatter so a silent parrot is not a statue.
            float openTarget = level * m_BeakOpen + idle * IdleChatter();
            m_Open = Motion.Damp(m_Open, openTarget, m_BeakHalfLife, deltaTime);
            m_Jaw.localRotation = Quaternion.Euler(m_Open, 0f, 0f);

            // Crest: lifts and fans with the voice.
            m_Raise = Motion.Damp(m_Raise, Mathf.Clamp01(level * 1.3f), 0.08f, deltaTime);
            for (int i = 0; i < m_Crest.Length; i++)
            {
                float pitch = Mathf.Lerp(-65f, -18f, m_Raise);
                float spread = k_CrestSpread[i % k_CrestSpread.Length] * (1f + 0.8f * m_Raise);
                m_Crest[i].localRotation = Quaternion.Euler(pitch, 0f, spread);
            }

            // Wings: out while a long note is being held, folded otherwise; an idle ruffle now and then.
            float flareTarget = HeldNote() ? 1f : 0f;
            flareTarget = Mathf.Max(flareTarget, idle * IdleRuffle());
            m_Flare = Motion.Damp(m_Flare, flareTarget, 0.1f, deltaTime);
            float flutter = m_Flare * 5f * Motion.Wave(Clock, 7f);
            PoseWing(m_LeftWing, -1f, flutter);
            PoseWing(m_RightWing, 1f, flutter);

            // Body: sways side to side over two beats; the tail answers it.
            float beat = (float)(Beats - System.Math.Floor(Beats));
            float sway = Mathf.Lerp(1.2f, 4f, m_Live) * Mathf.Sin((float)(Beats * System.Math.PI));
            float breathe = 1f + 0.015f * Motion.Wave(Clock, 0.4f);
            m_Body.localRotation = Quaternion.Euler(0f, 0f, sway);
            m_Body.localScale = new Vector3(breathe, 1f, breathe);
            m_Tail.localRotation = Quaternion.Euler(28f + 3f * level, 0f, -sway * 1.5f);

            // Head: looks at the viewer, nods on the beat, tilts curiously when idle.
            Gaze(deltaTime, out float yaw, out float pitchToViewer);
            float nod = Mathf.Lerp(1.5f, 5f, m_Live) * (0.5f - 0.5f * Mathf.Cos(beat * 2f * Mathf.PI));
            float roll = 10f * Mathf.Sin(Clock * 0.37f) * (1f - 0.6f * m_Live) - sway;
            m_Head.localRotation = Quaternion.Euler(pitchToViewer + nod, yaw, roll);
        }

        /// <summary>True while the current vocal note is long and has already sounded long enough to count as held.</summary>
        bool HeldNote()
        {
            ActiveNotes(m_Active);
            foreach (var note in m_Active)
                if (note.Duration >= m_HeldMinimum && SongTime - note.Time >= m_HeldAfter)
                    return true;

            return false;
        }

        void PoseWing(Transform wing, float side, float flutter)
        {
            var folded = FoldedWing(side);
            float x = Mathf.Lerp(folded.x, 0f, m_Flare);
            float z = folded.z + (m_WingFlare * m_Flare + flutter) * side;
            wing.localRotation = Quaternion.Euler(x, 0f, z);
        }

        /// <summary>
        /// Turns toward the viewer's head in the body's frame, so the gaze holds while the body sways.
        /// Clamped, so it turns its head rather than its whole neck round.
        /// </summary>
        void Gaze(float deltaTime, out float yaw, out float pitch)
        {
            Vector3 target;
            if (!TrackedHead.IsTracked || !TrackedHead.TryGetPose(out target, out _))
                target = m_Room != null ? m_Room.TransformPoint(m_ViewerEye) : m_ViewerEye;

            var direction = m_Body.InverseTransformDirection(target - m_Head.position);
            float flat = new Vector2(direction.x, direction.z).magnitude;
            float targetYaw = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -m_MaxYaw, m_MaxYaw);
            float targetPitch = Mathf.Clamp(-Mathf.Atan2(direction.y, flat) * Mathf.Rad2Deg, -m_MaxPitch, m_MaxPitch);

            m_Yaw = Motion.Damp(m_Yaw, targetYaw, m_GazeHalfLife, deltaTime);
            m_Pitch = Motion.Damp(m_Pitch, targetPitch, m_GazeHalfLife, deltaTime);
            yaw = m_Yaw;
            pitch = m_Pitch;
        }

        /// <summary>A short burst of beak clicks every few seconds, skipped on some cycles so it never reads as a loop.</summary>
        static float IdleChatter()
        {
            const float period = 4.3f;
            int cycle = Mathf.FloorToInt(Clock / period);
            if (Motion.Hash01(cycle) < 0.4f)
                return 0f;

            float t = Clock - cycle * period;
            return t < 0.3f ? 9f * Mathf.Abs(Mathf.Sin(t / 0.3f * 3f * Mathf.PI)) : 0f;
        }

        /// <summary>A brief half-open of the wings every so often while idle — a bird settling its feathers.</summary>
        static float IdleRuffle()
        {
            const float period = 7.7f;
            int cycle = Mathf.FloorToInt(Clock / period);
            if (Motion.Hash01(cycle + 101) < 0.5f)
                return 0f;

            float t = Clock - cycle * period;
            return t < 0.6f ? 0.35f * Mathf.Sin(t / 0.6f * Mathf.PI) : 0f;
        }
    }
}

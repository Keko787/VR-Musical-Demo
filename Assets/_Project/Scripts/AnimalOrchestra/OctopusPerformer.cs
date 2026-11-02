using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The keys: an octopus on a stone pedestal, low in the centre of the room. It plays with one arm per
    /// sounding voice — a three-note chord lifts three arms, a broken chord walks across them — and the
    /// suckers of an arm light up, root to tip, as its note starts. The arms are laid out like a
    /// keyboard as the viewer sees them: low notes to the left, high to the right.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a keyboard layout.</b> Mapping each voice to the arm at its pitch's place, left to right,
    /// means a rising arpeggio is seen to rise and a chord's spread is seen as a spread. Sounding notes
    /// are sorted by pitch and placed in order, each at its pitch's place or the next free arm to the
    /// right, so two close notes never fight for an arm and the left-to-right order always holds.
    /// </para>
    /// <para>
    /// The arms ring the body, so "left to right" walks from the back-left arm round the front to the
    /// back-right one. The root's local +Z faces the viewer, which puts the viewer's left at local +X.
    /// </para>
    /// <para>
    /// <b>Why the flash is gated.</b> When a voice ends, the remaining ones can shift arm; an arm that
    /// picks up a note already sounding takes it without a flash, so only a real note-on lights an arm.
    /// Flashes on one arm faster than the room's cap are scaled down.
    /// </para>
    /// </remarks>
    public sealed class OctopusPerformer : Performer
    {
        const int k_Arms = 8;
        const int k_Segments = 8;
        const int k_MaxVoices = 16;
        const float k_SuckerIdleGlow = 0.12f;

        static readonly float[] k_SegmentLength = { 0.07f, 0.065f, 0.06f, 0.055f, 0.05f, 0.045f, 0.04f, 0.035f };
        static readonly float[] k_SegmentThickness = { 0.05f, 0.045f, 0.04f, 0.035f, 0.03f, 0.026f, 0.022f, 0.018f };

        // Pitch of each joint (degrees about its X; positive bends down). Resting: draped over the pedestal's
        // edge with the tip curled out. Playing: raised and curling up toward the viewer.
        // The first two stay nearly level so an arm clears the rim's edge (by about 1.4 cm) before it bends down.
        static readonly float[] k_Rest = { 4f, 4f, 34f, 32f, 18f, 0f, -25f, -35f };
        static readonly float[] k_Play = { -35f, -15f, -10f, -5f, 0f, 5f, 10f, 15f };

        [Header("Playing")]
        [SerializeField, Range(0f, 60f), Tooltip("How far a playing arm turns toward the viewer, degrees.")]
        float m_TurnToViewer = 35f;

        [SerializeField, Range(0.01f, 0.5f), Tooltip("Seconds for an arm to go halfway up when its note starts.")]
        float m_RiseHalfLife = 0.05f;

        [SerializeField, Range(0.01f, 1f), Tooltip("Seconds for an arm to go halfway down when its note ends.")]
        float m_FallHalfLife = 0.15f;

        [SerializeField, Range(0.02f, 1f), Tooltip("Seconds for a sucker's flash to fade by half.")]
        float m_FlashHalfLife = 0.12f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Mantle;
        [SerializeField] Transform[] m_Eyes;
        [SerializeField] Transform[] m_Joints;
        [SerializeField] float[] m_ArmYaw;
        [SerializeField] float[] m_ArmPhase;
        [SerializeField] CreatureTint[] m_Suckers;
        [SerializeField, Tooltip("Arm index for each keyboard slot, left to right as the viewer sees them.")]
        int[] m_KeyOrder;

        readonly List<Note> m_Active = new();
        readonly Note[] m_Sorted = new Note[k_MaxVoices];
        readonly int[] m_ArmVoice = new int[k_Arms];
        readonly float[] m_Lift = new float[k_Arms];
        readonly double[] m_ArmNoteTime = { -1, -1, -1, -1, -1, -1, -1, -1 };
        readonly float[] m_FlashAmount = new float[k_Arms];

        float m_Live;

        // ================================================================== body

        public override void Build(CreatureKit kit)
        {
            var stoneDark = new Color(0.32f, 0.32f, 0.38f);
            var stone = new Color(0.46f, 0.47f, 0.55f);
            var stoneLight = new Color(0.66f, 0.66f, 0.72f);
            var coral = new Color(0.96f, 0.45f, 0.33f);
            var coralDark = new Color(0.78f, 0.28f, 0.22f);
            var sucker = new Color(1.0f, 0.86f, 0.76f);
            var suckerGlow = new Color(0.35f, 0.85f, 1.0f) * 2f;
            var gold = new Color(1.0f, 0.86f, 0.42f);
            var black = new Color(0.05f, 0.05f, 0.06f);

            // The pedestal: plinth, column, a lighter rim. Its top is the octopus's floor.
            kit.Solid(transform, "Plinth", kit.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.02f, 0.46f), stoneDark);
            kit.Solid(transform, "Column", kit.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.36f, 0.20f, 0.36f), stone);
            kit.Solid(transform, "Rim", kit.Cylinder, new Vector3(0f, 0.41f, 0f), new Vector3(0.44f, 0.015f, 0.44f), stoneLight);

            var octopus = kit.Node(transform, "Octopus", new Vector3(0f, 0.425f, 0f));

            // Head low, mantle swept back above it, spotted.
            kit.Ball(octopus, "Head", new Vector3(0f, 0.075f, 0f), new Vector3(0.20f, 0.13f, 0.19f), coral);
            m_Mantle = kit.Node(octopus, "Mantle", new Vector3(0f, 0.10f, -0.02f), new Vector3(-25f, 0f, 0f));
            kit.Ball(m_Mantle, "Mantle_Skin", new Vector3(0f, 0.10f, 0f), new Vector3(0.19f, 0.24f, 0.20f), coral);
            var spots = new[]
            {
                new Vector3(-0.05f, 0.16f, 0.06f), new Vector3(0.05f, 0.16f, 0.06f), new Vector3(0f, 0.20f, 0.05f),
                new Vector3(-0.07f, 0.08f, 0.07f), new Vector3(0.07f, 0.08f, 0.07f),
            };
            for (int i = 0; i < spots.Length; i++)
                kit.Ball(m_Mantle, $"Spot_{i}", spots[i], new Vector3(0.03f, 0.03f, 0.012f), coralDark,
                    Quaternion.LookRotation(spots[i] - new Vector3(0f, 0.1f, 0f)).eulerAngles);

            // Big eyes either side of the head, gold with a horizontal bar of a pupil, under a fold of skin.
            m_Eyes = new Transform[2];
            for (int e = 0; e < 2; e++)
            {
                float s = e == 0 ? -1f : 1f;
                var eye = kit.Node(octopus, e == 0 ? "Eye_L" : "Eye_R", new Vector3(0.065f * s, 0.10f, 0.072f), new Vector3(0f, 25f * s, 0f));
                kit.Ball(eye, "Eyeball", Vector3.zero, Vector3.one * 0.055f, gold);
                kit.Ball(eye, "Pupil", new Vector3(0f, 0f, 0.026f), new Vector3(0.034f, 0.011f, 0.012f), black);
                kit.Ball(eye, "Glint", new Vector3(0.008f * s, 0.01f, 0.026f), Vector3.one * 0.008f, Color.white);
                kit.Ball(octopus, e == 0 ? "Brow_L" : "Brow_R", new Vector3(0.065f * s, 0.125f, 0.062f), new Vector3(0.06f, 0.03f, 0.05f), coral);
                m_Eyes[e] = eye;
            }

            // Eight arms in a ring, each a chain of tapering segments reaching out along its joint's +Z,
            // with a sucker under each segment.
            m_Joints = new Transform[k_Arms * k_Segments];
            m_Suckers = new CreatureTint[k_Arms * k_Segments];
            m_ArmYaw = new float[k_Arms];
            m_ArmPhase = new float[k_Arms];
            var place = new float[k_Arms];

            for (int a = 0; a < k_Arms; a++)
            {
                float yaw = 22.5f + a * 45f;
                if (yaw > 180f)
                    yaw -= 360f;

                float radians = yaw * Mathf.Deg2Rad;
                m_ArmYaw[a] = yaw;
                m_ArmPhase[a] = Motion.Hash01(a + 57) * 6.283f;

                // Where the arm sits on the viewer's left-to-right sweep: from the back-left (local +X, −Z)
                // round the front (+Z) to the back-right.
                float sweep = Mathf.Atan2(Mathf.Sin(radians), -Mathf.Cos(radians)) * Mathf.Rad2Deg;
                place[a] = sweep < 0f ? sweep + 360f : sweep;

                var parent = octopus;
                var position = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * 0.075f + new Vector3(0f, 0.06f, 0f);
                for (int k = 0; k < k_Segments; k++)
                {
                    float length = k_SegmentLength[k];
                    float thickness = k_SegmentThickness[k];
                    var euler = new Vector3(k_Rest[k], k == 0 ? yaw : 0f, 0f);
                    var joint = kit.Node(parent, $"Arm_{a}_{k}", position, euler);

                    kit.Solid(joint, "Segment", kit.Capsule, new Vector3(0f, 0f, length * 0.5f),
                        new Vector3(thickness, length * 0.5f + thickness * 0.25f, thickness), coral, new Vector3(90f, 0f, 0f));
                    var tint = kit.Lit(joint, "Sucker", kit.Sphere, new Vector3(0f, -thickness * 0.42f, length * 0.5f),
                        new Vector3(thickness * 0.55f, thickness * 0.22f, thickness * 0.55f), sucker, suckerGlow);
                    tint.Glow = k_SuckerIdleGlow;

                    m_Joints[a * k_Segments + k] = joint;
                    m_Suckers[a * k_Segments + k] = tint;
                    parent = joint;
                    position = new Vector3(0f, 0f, length);
                }
            }

            m_KeyOrder = new int[k_Arms];
            for (int a = 0; a < k_Arms; a++)
                m_KeyOrder[a] = a;
            System.Array.Sort(place, m_KeyOrder);
        }

        // ================================================================== performance

        protected override void OnSongChanged()
        {
            for (int a = 0; a < k_Arms; a++)
            {
                m_ArmNoteTime[a] = -1.0;
                m_FlashAmount[a] = 0f;
            }
        }

        protected override void Perform(float deltaTime)
        {
            if (m_Joints == null || m_Joints.Length < k_Arms * k_Segments || m_KeyOrder == null)
                return;

            m_Live = Motion.Damp(m_Live, Live ? 1f : 0f, 0.3f, deltaTime);
            AssignVoices();

            float time = Clock;
            float intensity = Intensity;
            for (int a = 0; a < k_Arms; a++)
            {
                int voice = m_ArmVoice[a];
                bool playing = voice >= 0;
                if (playing)
                    TakeNote(a, m_Sorted[voice]);

                float target = playing ? 1f : 0f;
                float halfLife = target > m_Lift[a] ? m_RiseHalfLife : m_FallHalfLife;
                m_Lift[a] = Motion.Damp(m_Lift[a], target, halfLife, deltaTime);

                PoseArm(a, time);

                // Suckers light root to tip, a few hundredths of a second apart.
                double since = m_ArmNoteTime[a] >= 0.0 ? SongTime - m_ArmNoteTime[a] : double.MaxValue;
                for (int k = 0; k < k_Segments; k++)
                {
                    float flash = since < 10.0 ? Motion.Pulse(since - k * 0.018, 0.015f, m_FlashHalfLife) * m_FlashAmount[a] : 0f;
                    m_Suckers[a * k_Segments + k].Glow = (k_SuckerIdleGlow + 2.2f * flash) * intensity;
                }
            }

            // Mantle breathes, a little harder with the keys' level; eyes blink now and then.
            float breath = Motion.Wave(time, 0.3f);
            float swell = Mathf.Clamp01(Level()) * 0.08f;
            m_Mantle.localScale = new Vector3(1f + 0.03f * breath, 1f + 0.05f * breath + swell, 1f + 0.03f * breath);

            float blink = Blink();
            foreach (var eye in m_Eyes)
                eye.localScale = new Vector3(1f, blink, 1f);
        }

        /// <summary>
        /// Gives each sounding note an arm: sorted by pitch, each at its pitch's place on the keyboard or
        /// the next free arm to its right, leaving room for the notes still to place.
        /// </summary>
        void AssignVoices()
        {
            for (int a = 0; a < k_Arms; a++)
                m_ArmVoice[a] = -1;

            ActiveNotes(m_Active);
            int count = Mathf.Min(m_Active.Count, k_MaxVoices);
            for (int i = 0; i < count; i++)
            {
                var note = m_Active[i];
                int j = i - 1;
                while (j >= 0 && m_Sorted[j].Pitch > note.Pitch)
                {
                    m_Sorted[j + 1] = m_Sorted[j];
                    j--;
                }

                m_Sorted[j + 1] = note;
            }

            // More voices than arms: the top ones go unplayed.
            int placed = Mathf.Min(count, k_Arms);
            int previous = -1;
            for (int i = 0; i < placed; i++)
            {
                float place = Motion.PitchNorm(m_Sorted[i].Pitch, Notes.MinPitch, Notes.MaxPitch);
                int slot = Mathf.Max(Mathf.RoundToInt(place * (k_Arms - 1)), previous + 1);
                slot = Mathf.Min(slot, k_Arms - placed + i);
                previous = slot;
                m_ArmVoice[m_KeyOrder[slot]] = i;
            }
        }

        /// <summary>
        /// Records the note an arm is playing. A note that has only just started is a note-on and flashes,
        /// scaled down if the arm flashed too recently; one already sounding (the arm took it over from
        /// another when a voice ended) does not.
        /// </summary>
        void TakeNote(int arm, in Note note)
        {
            if (note.Time == m_ArmNoteTime[arm])
                return;

            bool fresh = SongTime - note.Time < 0.1;
            float amount = 0f;
            if (fresh)
                amount = m_ArmNoteTime[arm] < 0.0 ? 1f : Mathf.Clamp01((float)(note.Time - m_ArmNoteTime[arm]) * MaxFlashHz);

            m_ArmNoteTime[arm] = note.Time;
            m_FlashAmount[arm] = amount;
        }

        void PoseArm(int arm, float time)
        {
            float lift = m_Lift[arm];
            float phase = m_ArmPhase[arm];
            float idleWave = 6f * (1f - 0.6f * lift);
            float wiggle = 4f * lift;

            // A playing arm also turns toward the front, where the viewer is, on its own side of the body.
            float yaw = m_ArmYaw[arm];
            float turn = -Mathf.Sign(yaw) * Mathf.Min(Mathf.Abs(yaw), m_TurnToViewer) * lift;
            float sway = 4f * Mathf.Sin(time * 0.5f + phase);

            for (int k = 0; k < k_Segments; k++)
            {
                float pitch = Mathf.Lerp(k_Rest[k], k_Play[k], lift)
                            + idleWave * Mathf.Sin(time * 2.2f - k * 0.7f + phase)
                            + wiggle * Mathf.Sin(time * 10f - k * 0.9f + phase);

                m_Joints[arm * k_Segments + k].localRotation = k == 0
                    ? Quaternion.Euler(pitch, yaw + turn + sway, 0f)
                    : Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        static float Blink()
        {
            const float period = 4.9f;
            const float length = 0.16f;
            float t = Clock + 0.7f;
            int cycle = Mathf.FloorToInt(t / period);
            float phase = t - cycle * period;
            if (Motion.Hash01(cycle + 211) < 0.25f || phase >= length)
                return 1f;

            return 1f - 0.85f * Mathf.Sin(phase / length * Mathf.PI);
        }
    }
}

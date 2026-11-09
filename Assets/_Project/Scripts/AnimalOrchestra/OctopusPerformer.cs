using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The keys: an octopus on a stone pedestal playing a small two-octave keyboard on a stand in front of
    /// it. For each note an arm arches out over the note's key, lifts as it winds up, comes down
    /// on the key exactly as the note sounds and holds it there while the note lasts; the key tips down and
    /// lights in the note's colour, so every note is a key you can see go down and glow. Between notes its
    /// arms hover over the keys; between songs they drape over the pedestal and wave.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a keyboard.</b> At first the arms were the keys: one arm lifted per sounding note, laid out
    /// low to high, with a flash along its suckers. It read as an octopus waving, and the flashes were too
    /// small to see. A keyboard says "keys" at a glance, its keys going down and lighting are big and
    /// bright, and an arm pressing one is what playing looks like.
    /// </para>
    /// <para>
    /// <b>Notes to keys.</b> The keyboard is two octaves from a C. A song's notes are placed from the C at or
    /// under its lowest note, and anything higher than two octaves folds down an octave at a time — a
    /// music-folder track's keys are pitch classes in one octave, the built-in songs' sit within two.
    /// </para>
    /// <para>
    /// <b>Keys to arms.</b> The four front arms each have a quarter of the keyboard, left to right; the two
    /// side arms help when a chord puts more notes in a quarter than one arm can take. An arm keeps its note
    /// until it ends, and takes an upcoming note a fifth of a second early, so it is over the key in time
    /// for the windup. A note with no free arm still presses and lights its key.
    /// </para>
    /// <para>
    /// <b>How an arm reaches.</b> The arm's root turns toward the key, and its eight joints bend by one
    /// angle each, an arc, with the first joint's pitch chosen so the arc's tip lands on the target: the
    /// bend from the target's distance, the first pitch from its direction. Only where the arm is going is
    /// smoothed; how high its tip is over the key is computed from the song clock, so the strike lands on
    /// the note.
    /// </para>
    /// </remarks>
    public sealed class OctopusPerformer : Performer
    {
        const int k_Arms = 8;
        const int k_Segments = 8;
        const int k_Keys = 24;
        const int k_MaxEvents = 16;

        /// <summary>Height of the pedestal's top, where the octopus node sits.</summary>
        const float k_OctopusBase = 0.425f;

        static readonly float[] k_SegmentLength = { 0.07f, 0.065f, 0.06f, 0.055f, 0.05f, 0.045f, 0.04f, 0.035f };
        static readonly float[] k_SegmentThickness = { 0.05f, 0.045f, 0.04f, 0.035f, 0.03f, 0.026f, 0.022f, 0.018f };

        // Pitch of each joint (degrees about its X; positive bends down) at rest: draped over the pedestal's
        // edge with the tip curled out. The first two stay nearly level so an arm clears the rim's edge.
        static readonly float[] k_Rest = { 4f, 4f, 34f, 32f, 18f, 0f, -25f, -35f };

        // The arms that play, left to right as the octopus faces the keyboard (its left is −X), and which of
        // them helps on each side. Arm a points along yaw 22.5° + 45°·a.
        static readonly int[] k_FrontArms = { 6, 7, 0, 1 };
        static readonly int[] k_SideArms = { 5, 2 };

        static readonly Color k_WhiteKey = new(0.95f, 0.94f, 0.9f);
        static readonly Color k_BlackKey = new(0.08f, 0.08f, 0.1f);

        /// <summary>Each semitone's colour, round the circle of fifths like every pitch colour here: what its key lights in.</summary>
        static readonly Color[] k_KeyColour = KeyColours();

        // Semitone within the octave → which white key it is or follows (C D E F G A B = 0..6), and whether black.
        static readonly int[] k_White = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
        static readonly bool[] k_Black = { false, true, false, true, false, false, true, false, true, false, true, false };

        // The keyboard, in the octopus's frame: +Z toward the keyboard, the way the octopus faces.
        const float k_KeyBed = 0.435f;          // height of the white keys' hinge line
        const float k_KeyBack = 0.385f;         // z of the keys' back edge, where they hinge
        const float k_WhiteWidth = 0.042f;
        const float k_WhiteLength = 0.13f;
        const float k_BlackLength = 0.08f;
        const float k_KeyThickness = 0.02f;
        const float k_BlackRise = 0.014f;

        [Header("Playing")]
        [SerializeField, Range(0.05f, 0.4f), Tooltip("Seconds before a note that its arm sets off for the key and winds up.")]
        float m_Lead = 0.2f;

        [SerializeField, Range(0f, 0.15f), Tooltip("How high an arm hovers over the keys between notes, metres.")]
        float m_Hover = 0.05f;

        [SerializeField, Range(0f, 0.15f), Tooltip("How much higher it lifts in the windup before it strikes, metres.")]
        float m_Windup = 0.05f;

        [SerializeField, Range(0f, 15f), Tooltip("How far a pressed key tips down, degrees.")]
        float m_KeyTravel = 5f;

        [SerializeField, Range(0f, 3f), Tooltip("How brightly a key lights the moment it is struck.")]
        float m_KeyFlash = 1.8f;

        [SerializeField, Range(0f, 2f), Tooltip("How brightly a key stays lit while its note is held.")]
        float m_KeyHeld = 0.7f;

        [SerializeField, Range(0.02f, 1f), Tooltip("Seconds for a key's flash to fade by half.")]
        float m_FlashHalfLife = 0.12f;

        [SerializeField, Range(0f, 3f), Tooltip("Seconds an arm keeps hovering over the keys after its last note before it settles.")]
        float m_Linger = 0.8f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Mantle;
        [SerializeField] Transform[] m_Eyes;
        [SerializeField] Transform[] m_Joints;
        [SerializeField] float[] m_ArmYaw;
        [SerializeField] float[] m_ArmPhase;
        [SerializeField] Transform[] m_KeyPivots;
        [SerializeField] CreatureTint[] m_KeyTints;

        readonly List<(Note note, int key)> m_Events = new();
        readonly int[] m_ArmEvent = new int[k_Arms];
        readonly double[] m_ArmNoteTime = new double[k_Arms];
        readonly int[] m_ArmNotePitch = new int[k_Arms];
        readonly float[] m_ArmEngaged = new float[k_Arms];
        readonly float[] m_ArmLastPlayed = new float[k_Arms];
        readonly Vector3[] m_ArmTarget = new Vector3[k_Arms];
        readonly float[] m_ArmBlend = new float[k_Arms];
        readonly float[] m_KeyDown = new float[k_Keys];
        readonly double[] m_KeyNoteTime = new double[k_Keys];
        readonly float[] m_KeyFlashAmount = new float[k_Keys];
        readonly bool[] m_EventTaken = new bool[k_MaxEvents];
        readonly float[] m_Pitches = new float[k_Segments];

        float m_Live;
        int m_LowC = 48;

        // ================================================================== body

        public override void Build(CreatureKit kit)
        {
            var stoneDark = new Color(0.32f, 0.32f, 0.38f);
            var stone = new Color(0.46f, 0.47f, 0.55f);
            var stoneLight = new Color(0.66f, 0.66f, 0.72f);
            var coral = new Color(0.96f, 0.45f, 0.33f);
            var coralDark = new Color(0.78f, 0.28f, 0.22f);
            var sucker = new Color(1.0f, 0.86f, 0.76f);
            var gold = new Color(1.0f, 0.86f, 0.42f);
            var black = new Color(0.05f, 0.05f, 0.06f);

            // The pedestal: plinth, column, a lighter rim. Its top is the octopus's floor.
            kit.Solid(transform, "Plinth", kit.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.02f, 0.46f), stoneDark);
            kit.Solid(transform, "Column", kit.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.36f, 0.20f, 0.36f), stone);
            kit.Solid(transform, "Rim", kit.Cylinder, new Vector3(0f, 0.41f, 0f), new Vector3(0.44f, 0.015f, 0.44f), stoneLight);

            BuildKeyboard(kit);

            var octopus = kit.Node(transform, "Octopus", new Vector3(0f, k_OctopusBase, 0f));

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
            m_ArmYaw = new float[k_Arms];
            m_ArmPhase = new float[k_Arms];

            for (int a = 0; a < k_Arms; a++)
            {
                float yaw = 22.5f + a * 45f;
                if (yaw > 180f)
                    yaw -= 360f;

                m_ArmYaw[a] = yaw;
                m_ArmPhase[a] = Motion.Hash01(a + 57) * 6.283f;

                var parent = octopus;
                var position = ArmRoot(yaw);
                for (int k = 0; k < k_Segments; k++)
                {
                    float length = k_SegmentLength[k];
                    float thickness = k_SegmentThickness[k];
                    var joint = kit.Node(parent, $"Arm_{a}_{k}", position, new Vector3(k_Rest[k], k == 0 ? yaw : 0f, 0f));

                    kit.Solid(joint, "Segment", kit.Capsule, new Vector3(0f, 0f, length * 0.5f),
                        new Vector3(thickness, length * 0.5f + thickness * 0.25f, thickness), coral, new Vector3(90f, 0f, 0f));
                    kit.Ball(joint, "Sucker", new Vector3(0f, -thickness * 0.42f, length * 0.5f),
                        new Vector3(thickness * 0.55f, thickness * 0.22f, thickness * 0.55f), sucker);

                    m_Joints[a * k_Segments + k] = joint;
                    parent = joint;
                    position = new Vector3(0f, 0f, length);
                }
            }
        }

        /// <summary>Where the arm pointing along <paramref name="yaw"/> leaves the body, in the octopus node's frame.</summary>
        static Vector3 ArmRoot(float yaw)
        {
            float radians = yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * 0.075f + new Vector3(0f, 0.06f, 0f);
        }

        /// <summary>
        /// A two-octave keyboard on a stand, between the octopus and the viewer: a dark case, fourteen white
        /// keys and ten black ones, each on a hinge at its back edge so a press tips its front down. Each key
        /// lights in its own note's colour — round the circle of fifths, as every pitch colour here is.
        /// </summary>
        void BuildKeyboard(CreatureKit kit)
        {
            var caseColour = new Color(0.12f, 0.12f, 0.15f);
            var standColour = new Color(0.22f, 0.22f, 0.25f);

            var keyboard = kit.Node(transform, "Keyboard", Vector3.zero);
            float width = 14 * k_WhiteWidth;
            float front = k_KeyBack - k_WhiteLength;
            float middle = (front + k_KeyBack) * 0.5f;

            kit.Solid(keyboard, "Case", kit.Cube, new Vector3(0f, k_KeyBed - 0.025f, middle + 0.01f),
                new Vector3(width + 0.05f, 0.03f, k_WhiteLength + 0.05f), caseColour);
            kit.Solid(keyboard, "Case_Back", kit.Cube, new Vector3(0f, k_KeyBed + 0.012f, k_KeyBack + 0.02f),
                new Vector3(width + 0.05f, 0.045f, 0.03f), caseColour);
            foreach (float s in new[] { -1f, 1f })
            {
                kit.Solid(keyboard, s < 0f ? "Case_End_L" : "Case_End_R", kit.Cube, new Vector3(s * (width * 0.5f + 0.0125f), k_KeyBed + 0.004f, middle + 0.01f),
                    new Vector3(0.025f, 0.03f, k_WhiteLength + 0.05f), caseColour);

                // An X stand under each end.
                float x = s * (width * 0.5f - 0.04f);
                float legHeight = k_KeyBed - 0.04f;
                float legLength = Mathf.Sqrt(legHeight * legHeight + 0.16f * 0.16f);
                float tilt = Mathf.Atan2(0.16f, legHeight) * Mathf.Rad2Deg;
                foreach (float lean in new[] { -1f, 1f })
                    kit.Solid(keyboard, $"Stand_Leg_{(s < 0f ? "L" : "R")}{(lean < 0f ? 0 : 1)}", kit.Cylinder,
                        new Vector3(x, legHeight * 0.5f, middle), new Vector3(0.016f, legLength * 0.5f, 0.016f), standColour,
                        new Vector3(tilt * lean, 0f, 0f));
            }

            m_KeyPivots = new Transform[k_Keys];
            m_KeyTints = new CreatureTint[k_Keys];
            for (int key = 0; key < k_Keys; key++)
            {
                int octave = key / 12, semitone = key % 12;
                bool isBlack = k_Black[semitone];
                int whiteIndex = octave * 7 + k_White[semitone];
                float x = -width * 0.5f + (whiteIndex + (isBlack ? 1f : 0.5f)) * k_WhiteWidth;

                var pivot = kit.Node(keyboard, $"Key_{key}", new Vector3(x, k_KeyBed + (isBlack ? k_BlackRise : 0f), k_KeyBack));
                float length = isBlack ? k_BlackLength : k_WhiteLength;
                var size = isBlack ? new Vector3(0.024f, k_KeyThickness, length) : new Vector3(k_WhiteWidth - 0.003f, k_KeyThickness, length);
                var tint = kit.Lit(pivot, "Key", kit.Cube, new Vector3(0f, 0f, -length * 0.5f), size, isBlack ? k_BlackKey : k_WhiteKey,
                    k_KeyColour[semitone]);
                tint.Glow = 0f;

                m_KeyPivots[key] = pivot;
                m_KeyTints[key] = tint;
            }
        }

        // ================================================================== performance

        protected override void OnSongChanged()
        {
            m_LowC = Live ? 12 * Mathf.FloorToInt(Notes.MinPitch / 12f) : 48;
            for (int a = 0; a < k_Arms; a++)
                m_ArmNoteTime[a] = -1.0;
            for (int key = 0; key < k_Keys; key++)
            {
                m_KeyNoteTime[key] = -1.0;
                m_KeyFlashAmount[key] = 0f;
            }
        }

        protected override void Perform(float deltaTime)
        {
            if (m_Joints == null || m_Joints.Length < k_Arms * k_Segments || m_KeyPivots == null || m_KeyPivots.Length < k_Keys)
                return;

            m_Live = Motion.Damp(m_Live, Live ? 1f : 0f, 0.3f, deltaTime);
            float time = Clock;

            GatherEvents();
            AssignArms();
            PlayKeys(deltaTime);

            for (int a = 0; a < k_Arms; a++)
                PoseArm(a, time, deltaTime);

            // Mantle breathes, a little harder with the keys' level; eyes blink now and then.
            float breath = Motion.Wave(time, 0.3f);
            float swell = Mathf.Clamp01(Level()) * 0.08f;
            m_Mantle.localScale = new Vector3(1f + 0.03f * breath, 1f + 0.05f * breath + swell, 1f + 0.03f * breath);

            float blink = Blink();
            foreach (var eye in m_Eyes)
                eye.localScale = new Vector3(1f, blink, 1f);
        }

        /// <summary>The key a pitch is played on: from the song's low C, folded down by octaves into the two the keyboard has.</summary>
        int KeyOf(int pitch) => ((pitch - m_LowC) % k_Keys + k_Keys) % k_Keys;

        /// <summary>
        /// The notes an arm could be busy with: sounding, about to sound (within the lead), or just let go
        /// (while the arm lifts off). Earliest first.
        /// </summary>
        void GatherEvents()
        {
            m_Events.Clear();
            if (!Live)
                return;

            double now = SongTime;
            int last = Notes.LastAtOrBefore(now + m_Lead);
            for (int i = last; i >= 0 && m_Events.Count < k_MaxEvents; i--)
            {
                var note = Notes[i];
                if (note.Time < now - Notes.MaxDuration - 0.15)
                    break;
                if (now <= note.End + 0.12)
                    m_Events.Add((note, KeyOf(note.Pitch)));
            }

            m_Events.Reverse();
        }

        /// <summary>
        /// Gives each event an arm: an arm keeps the note it already has; the rest go, earliest first, to the
        /// front arm whose quarter of the keyboard the key is in, or the nearest free one, or a side arm.
        /// </summary>
        void AssignArms()
        {
            for (int e = 0; e < k_MaxEvents; e++)
                m_EventTaken[e] = false;

            for (int a = 0; a < k_Arms; a++)
            {
                m_ArmEvent[a] = -1;
                if (m_ArmNoteTime[a] < 0.0)
                    continue;

                for (int e = 0; e < m_Events.Count; e++)
                {
                    var note = m_Events[e].note;
                    if (!m_EventTaken[e] && note.Time == m_ArmNoteTime[a] && note.Pitch == m_ArmNotePitch[a])
                    {
                        m_ArmEvent[a] = e;
                        m_EventTaken[e] = true;
                        break;
                    }
                }

                if (m_ArmEvent[a] < 0)
                    m_ArmNoteTime[a] = -1.0;
            }

            for (int e = 0; e < m_Events.Count; e++)
            {
                if (m_EventTaken[e])
                    continue;

                int arm = FreeArmFor(m_Events[e].key);
                if (arm < 0)
                    continue;

                m_ArmEvent[arm] = e;
                m_ArmNoteTime[arm] = m_Events[e].note.Time;
                m_ArmNotePitch[arm] = m_Events[e].note.Pitch;
                m_EventTaken[e] = true;
            }
        }

        int FreeArmFor(int key)
        {
            int quarter = Mathf.Clamp(key * k_FrontArms.Length / k_Keys, 0, k_FrontArms.Length - 1);
            for (int step = 0; step < k_FrontArms.Length; step++)
            {
                for (int way = 0; way < (step == 0 ? 1 : 2); way++)
                {
                    int q = way == 0 ? quarter - step : quarter + step;
                    if (q >= 0 && q < k_FrontArms.Length && m_ArmEvent[k_FrontArms[q]] < 0)
                        return k_FrontArms[q];
                }
            }

            int side = k_SideArms[key < k_Keys / 2 ? 0 : 1];
            return m_ArmEvent[side] < 0 ? side : -1;
        }

        /// <summary>
        /// Every sounding note holds its key down; a key lights on its note's start — scaled down if it was
        /// struck too recently for the room's flash cap — and stays softly lit while the note is held. Lit
        /// means coloured as well as glowing: a white key that only glowed stayed white.
        /// </summary>
        void PlayKeys(float deltaTime)
        {
            for (int key = 0; key < k_Keys; key++)
                m_KeyDown[key] = 0f;

            foreach (var (note, key) in m_Events)
            {
                double since = SongTime - note.Time;
                if (since < 0.0)
                    continue;

                if (note.Time != m_KeyNoteTime[key])
                {
                    m_KeyFlashAmount[key] = m_KeyNoteTime[key] < 0.0 ? 1f : Mathf.Clamp01((float)(note.Time - m_KeyNoteTime[key]) * MaxFlashHz);
                    m_KeyNoteTime[key] = note.Time;
                }

                if (SongTime <= note.End)
                    m_KeyDown[key] = 1f;
            }

            float intensity = Intensity;
            for (int key = 0; key < k_Keys; key++)
            {
                m_KeyPivots[key].localRotation = Quaternion.Euler(-m_KeyTravel * m_KeyDown[key], 0f, 0f);

                double since = m_KeyNoteTime[key] >= 0.0 ? SongTime - m_KeyNoteTime[key] : double.MaxValue;
                float flash = since < 10.0 ? Motion.Pulse(since, 0.01f, m_FlashHalfLife) * m_KeyFlashAmount[key] : 0f;
                m_KeyTints[key].Glow = (m_KeyFlash * flash + m_KeyHeld * m_KeyDown[key]) * intensity;

                float lit = Mathf.Clamp01(flash + 0.85f * m_KeyDown[key]) * Mathf.Clamp01(intensity);
                int semitone = key % 12;
                m_KeyTints[key].Color = Color.Lerp(k_Black[semitone] ? k_BlackKey : k_WhiteKey, k_KeyColour[semitone], lit);
            }
        }

        static Color[] KeyColours()
        {
            var colours = new Color[12];
            for (int semitone = 0; semitone < 12; semitone++)
                colours[semitone] = Motion.PitchColor(semitone, 0.85f, 1f);
            return colours;
        }

        /// <summary>Where on a key an arm presses, in the octopus's frame: near the front edge, on top.</summary>
        Vector3 PressPoint(int key)
        {
            var pivot = m_KeyPivots[key].localPosition;
            bool isBlack = k_Black[key % 12];
            float length = isBlack ? k_BlackLength : k_WhiteLength;
            return new Vector3(pivot.x, pivot.y + k_KeyThickness * 0.5f, pivot.z - length + 0.03f);
        }

        void PoseArm(int arm, float time, float deltaTime)
        {
            float phase = m_ArmPhase[arm];
            int e = m_ArmEvent[arm];

            // How high the tip is over the key: from the note's clock when it has one, hovering otherwise.
            float height = m_Hover;
            if (e >= 0)
            {
                var (note, key) = m_Events[e];
                double tau = SongTime - note.Time;
                if (tau < 0.0)
                {
                    Motion.Hit(tau, m_Lead, 0f, out float windup, out float strike);
                    height = Mathf.Lerp(m_Hover + m_Windup * windup, 0f, strike);
                }
                else if (SongTime <= note.End)
                {
                    height = 0f;
                }
                else
                {
                    height = Mathf.Lerp(0f, m_Hover, Motion.Smooth01((float)((SongTime - note.End) / 0.12)));
                }

                var press = PressPoint(key);
                m_ArmTarget[arm] = Motion.Damp(m_ArmTarget[arm], press, 0.04f, deltaTime);
                m_ArmLastPlayed[arm] = time;
            }

            // An arm that has played lately hovers over the keys; one that has not settles back on the pedestal.
            bool engaged = e >= 0 || (m_Live > 0.5f && time - m_ArmLastPlayed[arm] < m_Linger && m_ArmBlend[arm] > 0.01f);
            m_ArmBlend[arm] = Motion.Damp(m_ArmBlend[arm], engaged ? 1f : 0f, engaged ? 0.05f : 0.25f, deltaTime);
            float blend = m_ArmBlend[arm];

            float restYaw = m_ArmYaw[arm] + 4f * Mathf.Sin(time * 0.5f + phase);
            float yaw = restYaw;
            for (int k = 0; k < k_Segments; k++)
                m_Pitches[k] = k_Rest[k] + 6f * (1f - blend) * Mathf.Sin(time * 2.2f - k * 0.7f + phase);

            if (blend > 0.001f)
            {
                // The arm's root and its target in the same frame, the performer's; the tip clears the key by a
                // hair at the bottom of the strike, so it touches rather than sinks in.
                var root = ArmRoot(m_ArmYaw[arm]) + new Vector3(0f, k_OctopusBase, 0f);
                var target = m_ArmTarget[arm] + new Vector3(0f, height + 0.008f, 0f);
                Reach(root, target, out float reachYaw, out float first, out float bend);

                yaw = Mathf.LerpAngle(restYaw, reachYaw, blend);
                for (int k = 0; k < k_Segments; k++)
                    m_Pitches[k] = Mathf.Lerp(m_Pitches[k], k == 0 ? first : bend, blend);
            }

            for (int k = 0; k < k_Segments; k++)
                m_Joints[arm * k_Segments + k].localRotation = k == 0
                    ? Quaternion.Euler(m_Pitches[k], yaw, 0f)
                    : Quaternion.Euler(m_Pitches[k], 0f, 0f);
        }

        /// <summary>
        /// The arm's pose to put its tip on <paramref name="target"/> from <paramref name="root"/>: the yaw toward
        /// it, then an arc — every joint after the first bent by <paramref name="bend"/> — with the first joint's
        /// pitch, <paramref name="first"/>, turning the arc so its tip lands on the target. The bend is found
        /// from the target's distance (a curlier arc reaches less far), the first pitch from its direction.
        /// Degrees; positive pitch bends down. A target out of reach gets the arm straight toward it.
        /// </summary>
        static void Reach(Vector3 root, Vector3 target, out float yaw, out float first, out float bend)
        {
            var offset = target - root;
            float across = new Vector2(offset.x, offset.z).magnitude;
            float drop = -offset.y;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;

            float distance = Mathf.Sqrt(across * across + drop * drop);
            float low = 0f, high = 1.2f;
            if (Tip(0f).magnitude <= distance)
                high = 0f;
            else
                for (int i = 0; i < 18; i++)
                {
                    float mid = (low + high) * 0.5f;
                    if (Tip(mid).magnitude > distance)
                        low = mid;
                    else
                        high = mid;
                }

            float b = high;
            var tip = Tip(b);
            float toTarget = Mathf.Atan2(drop, across);
            float toTip = Mathf.Atan2(tip.y, tip.x);
            first = (toTarget - toTip) * Mathf.Rad2Deg;
            bend = b * Mathf.Rad2Deg;
        }

        /// <summary>The tip of the arm laid as an arc bending <paramref name="bend"/> radians a joint, first segment level: (along, down).</summary>
        static Vector2 Tip(float bend)
        {
            var tip = Vector2.zero;
            for (int k = 0; k < k_Segments; k++)
            {
                float heading = k * bend;
                tip += k_SegmentLength[k] * new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
            }

            return tip;
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

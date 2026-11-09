using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VRShootingGallery.CaveView;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.Haunted
{
    /// <summary>Which part of the music a possessed object answers to.</summary>
    public enum Listen
    {
        Bass,
        LowMid,
        Mid,
        HighMid,
        High,
        Loudness,
    }

    /// <summary>
    /// The haunting's director. It decides how possessed the room is, when the ghosts show
    /// themselves, where they go, and what the lights are doing; every possessed object, the fire,
    /// the candles and the ghosts read their cue from here, so the room moves as one story rather
    /// than a hundred things twitching independently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Possession</b> follows the song's structure, not its beat. When a track starts, the
    /// whole clip is read ahead of time (<see cref="TrackEnvelope"/>), so the director knows which
    /// sections are the quiet ones and which are the drops, measured against the track itself.
    /// A quiet intro wakes one lamp; a build wakes more; a drop wakes everything. Objects join in
    /// the order of their thresholds, and the ghost's nearness lowers an object's threshold, so
    /// the possession spreads through the room following the ghost.
    /// </para>
    /// <para>
    /// <b>The reveal</b> is earned: the ghost only shows itself on a loud section that follows a
    /// quieter one, for a limited time, and not again straight away. Because the envelope is known
    /// in advance, the lights start going down <see cref="m_RevealLead"/> seconds <em>before</em>
    /// the drop, which is what makes it land.
    /// </para>
    /// <para>
    /// <b>There are two ghosts.</b> Unseen, each goes its own way — never to the same thing as the
    /// other — so the possession spreads from two places at once. On show they take the stage
    /// together and dance as partners: a slow waltz round each other, hopping on the same beats and
    /// twirling on the same beat, opposite ways.
    /// </para>
    /// <para>
    /// Every flicker is driven by the analyser's beat, which is rate-limited to three a second
    /// (two in low intensity). The room is dark and enclosed; that cap is the photosensitivity
    /// guard, and nothing here flashes on its own clock.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-80)] // after the analyser, before the ghost and every possessed object
    public class Haunting : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] AudioAnalyser m_Analyser;
        [SerializeField] MusicDeck m_Deck;

        [Header("Possession")]
        [SerializeField, Range(0f, 0.3f), Tooltip("Possession while any music plays at all: something in the room is always faintly wrong.")]
        float m_MinPossession = 0.06f;

        [SerializeField, Tooltip("Seconds ahead in the track the possession reads, so the room is already stirring as a section arrives.")]
        float m_Lookahead = 0.6f;

        [SerializeField, Tooltip("Seconds for possession to rise toward a louder section.")]
        float m_PossessionRise = 2f;

        [SerializeField, Tooltip("Seconds for it to fall back. Slower than the rise: a haunting lingers.")]
        float m_PossessionFall = 4f;

        [SerializeField, Range(0f, 0.5f), Tooltip("How far the ghost's nearness lowers an object's threshold, so objects near it wake first.")]
        float m_GhostSpread = 0.2f;

        [SerializeField, Tooltip("Metres within which the ghost disturbs things: curtains, candles, thresholds.")]
        float m_WakeRadius = 1.1f;

        [Header("Reveal")]
        [SerializeField, Range(0f, 1f), Tooltip("Section loudness, 0 for the track's quiet parts and 1 for its loud ones, at which the ghost shows itself.")]
        float m_RevealAt = 0.8f;

        [SerializeField, Tooltip("Seconds before the drop the lights start going down. Only possible because the track was read ahead.")]
        float m_RevealLead = 1.5f;

        [SerializeField, Range(0f, 1f), Tooltip("A reveal is earned: the track must have been below this within the earn window.")]
        float m_EarnedBelow = 0.55f;

        [SerializeField] float m_EarnWindow = 25f;

        [SerializeField, Tooltip("Longest the ghost stays visible, seconds. It cannot be looked at for long.")]
        float m_RevealHold = 16f;

        [SerializeField, Tooltip("Seconds after a reveal before the next can begin.")]
        float m_RevealCooldown = 12f;

        [SerializeField] float m_RevealRise = 1.2f;
        [SerializeField] float m_RevealFall = 2.5f;

        [SerializeField, Range(0f, 1f), Tooltip("How far the room's lamps go down while the ghost is visible. A slow dim, never a cut.")]
        float m_RevealDim = 0.6f;

        [Header("Glimpses")]
        [SerializeField, Range(0f, 1f), Tooltip("Possession above which a strong beat can flash the ghost's face in the TV static.")]
        float m_GlimpseFrom = 0.35f;

        [SerializeField, Range(0f, 1f)] float m_GlimpseChance = 0.45f;
        [SerializeField] float m_GlimpseDecay = 0.35f;

        [Header("Ghosts")]
        [SerializeField, Tooltip("The ghosts. Unseen, each goes its own way; on show they take the stage together and dance as partners.")]
        SpectreGhost[] m_Ghosts;

        [SerializeField, HideInInspector, Tooltip("The one ghost of a scene built before there were two.")]
        SpectreGhost m_Ghost;

        [SerializeField, Tooltip("The volume the ghosts' centres keep to, room metres. The front of the room, clear of the open back " +
            "where there is no screen, and above head height so they are seen against the walls.")]
        Vector3 m_GhostMin = new(-1.25f, 1.05f, -0.45f);

        [SerializeField] Vector3 m_GhostMax = new(1.25f, 1.75f, 0.85f);

        [SerializeField, Tooltip("Where the ghosts take the stage when they reveal themselves: the middle of their dance.")]
        Vector3 m_Stage = new(0f, 1.38f, 0.35f);

        [SerializeField] float m_RetargetMin = 4f;
        [SerializeField] float m_RetargetMax = 8f;

        [SerializeField, Tooltip("Metres apart the ghosts keep their targets while unseen, so they haunt two places rather than one.")]
        float m_GhostSpacing = 1f;

        [SerializeField, Tooltip("Half-width and half-depth of the circle the partners waltz round each other on, room metres. Wider " +
            "than deep, so they swap sides in front of the viewer more than they pass in front of each other: the room has 0.7 m " +
            "of depth between the viewer's keep-away and the mantel.")]
        Vector2 m_WaltzRadius = new(0.5f, 0.22f);

        [SerializeField, Tooltip("Metres a partner rises going round the back of the waltz, and dips coming round the front, so where " +
            "they cross in front of the viewer the one behind shows over the one in front instead of the two merging.")]
        float m_WaltzRise = 0.15f;

        [SerializeField, Tooltip("Least distance across the floor from the viewer's head to the middle of the dance, metres. The " +
            "stage moves back from the viewer until it is this far, so the partner coming round the front stays out of the " +
            "viewer's face without being pushed into the other.")]
        float m_DanceClearance = 1.05f;

        [SerializeField, Tooltip("Seconds a turn of the waltz takes: when the music is quiet, and at full energy.")]
        Vector2 m_WaltzTurn = new(9f, 5f);

        [SerializeField, Range(0f, 1f), Tooltip("Chance a beat sets the ghosts twirling, while they are on show. They twirl together, " +
            "partners opposite ways.")]
        float m_TwirlChance = 0.25f;

        [Header("Viewer")]
        [SerializeField, Tooltip("MiddleVR node for the tracked head. The portraits' eyes follow it and the ghost keeps its distance from it.")]
        string m_HeadNode = "HeadNode";

        [SerializeField, Tooltip("Where the head is assumed to be when nothing tracks it: at a desk, or before the tracker is up.")]
        Vector3 m_DeskHead = new(0f, 1.6f, -0.55f);

        [Header("Intensity")]
        [SerializeField, Tooltip("The photosensitivity setting: lamps flicker a third as deep, and the beat rate is capped lower. On for a school group.")]
        bool m_LowIntensity;

        [SerializeField] float m_MaxFlashesPerSecond = 3f;
        [SerializeField] float m_LowIntensityFlashesPerSecond = 2f;

        [Header("Wand")]
        [SerializeField, Tooltip("Wand button that summons the ghosts, or sends them away. Indices are whatever the .vrx says; the VisBox wand has 0-4, 6-8, 11 and 15, and 0 is the trigger.")]
        int m_SummonButton = 1;

        [SerializeField] int m_IntensityButton = 2;

        [SerializeField, Tooltip("Wand button that opens the tracks on the front wall (or, without a menu, skips to the next track).")]
        int m_NextTrackButton = 3;

        [SerializeField] int m_PlayPauseButton = 4;

        [Header("Menu")]
        [SerializeField, Tooltip("The tracks on the front wall, which the track button opens. None, and the track button skips to " +
            "the next track instead.")]
        TrackMenu m_Menu;

        [Header("Desk")]
        [SerializeField, Tooltip("G summons the ghosts, L toggles low intensity, N opens the tracks (Shift+N skips straight to the next), " +
            "Space pauses, and the number-row keys stand in for the wand buttons with the same digit. In the menu: the arrows, Enter, " +
            "and Backspace or Escape. Stays on in builds so the room can be run from the server node.")]
        bool m_KeyboardControl = true;

        [SerializeField, Tooltip("A dim line on the floor at the back of the room, for whoever is running the demo.")]
        TMP_Text m_Sign;

        [SerializeField, Tooltip("The CAVE's 3D / 2D views (thumbstick up for 2D, down for 3D), named on the sign while the room is flat.")]
        CaveViewMode m_Views;

        // ---------------------------------------------------------------- outputs

        /// <summary>How possessed the room is, 0 to 1. Objects wake as it passes their thresholds.</summary>
        public float Possession { get; private set; }

        /// <summary>How visible the ghosts are, 0 to 1.</summary>
        public float Reveal { get; private set; }

        /// <summary>Multiplier the lamps apply to themselves: 1 normally, down to 1 − <see cref="m_RevealDim"/> as the ghosts appear.</summary>
        public float RoomLight { get; private set; } = 1f;

        /// <summary>A brief flash of a ghost's face in the TV static, 0 to 1.</summary>
        public float Glimpse { get; private set; }

        /// <summary>How much the ghosts are dancing as partners: 0 each on its own, 1 waltzing on the stage.</summary>
        public float Together { get; private set; }

        /// <summary>True on the frame a beat sets the ghosts twirling: all at once, partners opposite ways.</summary>
        public bool Twirl { get; private set; }

        public bool Beat => m_Analyser != null && m_Analyser.Beat;
        public float BeatStrength => m_Analyser != null ? m_Analyser.BeatStrength : 0f;
        public bool LowIntensity => m_LowIntensity;
        public bool Summoned { get; private set; }
        public AudioAnalyser Analyser => m_Analyser;
        public MusicDeck Deck => m_Deck;

        /// <summary>Every ghost in the room, in the order they were built.</summary>
        public IReadOnlyList<SpectreGhost> Ghosts => m_Active;

        /// <summary>The first ghost, or none.</summary>
        public SpectreGhost Ghost => m_Active.Count > 0 ? m_Active[0] : null;

        /// <summary>Where the viewer's head is, world space: the tracker when there is one, the desk default otherwise.</summary>
        public Vector3 HeadPosition { get; private set; }

        public Vector3 GhostMin => m_GhostMin;
        public Vector3 GhostMax => m_GhostMax;

        // ---------------------------------------------------------------- state

        readonly List<Possessable> m_Possessed = new();
        readonly List<SpectreGhost> m_Active = new();
        readonly float[] m_Levels = new float[6];
        readonly TrackEnvelope m_Envelope = new();
        readonly StringBuilder m_Text = new();
        System.Random m_Random = new(1234);

        Transform m_Head;
        float m_HeadSearchAt;

        bool m_Showing;
        float m_ShowStart;
        float m_RevealEndedAt = -999f;
        float m_LastLowAt;
        float m_PreDim;

        // Live fallback, for a clip that cannot be read ahead.
        float m_LiveDb = -60f;
        float m_LivePeakDb = -60f;

        // Per ghost: when it next picks somewhere to go, what it is going to, and where that is.
        float[] m_RetargetAt = new float[0];
        Possessable[] m_Targets = new Possessable[0];
        Vector3[] m_TargetPoints = new Vector3[0];
        float m_WaltzAngle;

        float m_SilentSince = -1f;
        float m_GaspAfter;
        bool m_Gasped = true;
        float m_PeakPossession;

        int m_ShownPercent = -1;

        public void Register(Possessable possessed)
        {
            if (possessed != null && !m_Possessed.Contains(possessed))
                m_Possessed.Add(possessed);
        }

        public void Unregister(Possessable possessed) => m_Possessed.Remove(possessed);

        /// <summary>Which ghost this is, 0 for the first; −1 if it is not one of this room's.</summary>
        public int IndexOf(SpectreGhost ghost) => m_Active.IndexOf(ghost);

        void Awake()
        {
            if (m_Ghosts != null)
                foreach (var ghost in m_Ghosts)
                    if (ghost != null && !m_Active.Contains(ghost))
                        m_Active.Add(ghost);

            if (m_Active.Count == 0 && m_Ghost != null)
            {
                m_Active.Add(m_Ghost);
                Debug.LogWarning("[Haunted] This scene was built when the haunted room had one ghost, so only one dances. Open the " +
                                 "scene, run VR-Music-Demo ▸ Haunted Room ▸ Rebuild Haunted Room, and save it for the pair.", this);
            }

            int count = m_Active.Count;
            m_RetargetAt = new float[count];
            m_Targets = new Possessable[count];
            m_TargetPoints = new Vector3[count];
            for (int i = 0; i < count; i++)
                m_TargetPoints[i] = m_Active[i].transform.position;
        }

        void OnEnable()
        {
            if (m_Deck != null)
            {
                m_Deck.TrackStarted += OnTrackStarted;
                m_Deck.Changed += UpdateSign;
            }

            if (m_Views != null)
                m_Views.Changed += UpdateSign;
        }

        void OnDisable()
        {
            if (m_Deck != null)
            {
                m_Deck.TrackStarted -= OnTrackStarted;
                m_Deck.Changed -= UpdateSign;
            }

            if (m_Views != null)
                m_Views.Changed -= UpdateSign;
        }

        void Start()
        {
            HeadPosition = transform.TransformPoint(m_DeskHead);
            m_LastLowAt = Time.time;

            if (m_Menu == null)
                Debug.LogWarning("[Haunted] This scene was built before the haunted room had its track menu, so the track button " +
                                 "skips instead of opening it, and there is no now-playing strip. Open the scene, run VR Shooting " +
                                 "Gallery ▸ Haunted Room ▸ Rebuild Haunted Room, and save it.", this);

            UpdateSign();
        }

        /// <summary>A new track: read it ahead, and let the reveal be earned afresh.</summary>
        void OnTrackStarted(AudioClip clip)
        {
            m_Envelope.Begin(clip);
            m_LastLowAt = Time.time;
            m_LivePeakDb = -60f;
            m_PeakPossession = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            ReadInput();
            FindHead();

            if (m_Analyser != null)
                m_Analyser.MaxBeatsPerSecond = m_LowIntensity ? m_LowIntensityFlashesPerSecond : m_MaxFlashesPerSecond;

            // A few seconds of the track are read each frame until the whole envelope is known.
            if (!m_Envelope.Ready && !m_Envelope.Failed && m_Envelope.Clip != null)
                m_Envelope.Step(4f);

            ReadLevels();

            bool playing = m_Deck != null && m_Deck.IsPlaying && (m_Analyser == null || m_Analyser.Idle < 0.5f);
            float time = m_Deck != null ? m_Deck.Time : 0f;

            UpdatePossession(playing, time, dt);
            UpdateReveal(playing, time, dt);
            UpdateGlimpse(dt);
            UpdateDance(dt);
            SteerGhosts();
            LastGasp(playing);

            int percent = Mathf.RoundToInt(Possession * 100f);
            if (percent != m_ShownPercent)
            {
                m_ShownPercent = percent;
                UpdateSign();
            }
        }

        // ------------------------------------------------------------------ the song's shape

        /// <summary>
        /// Section loudness at <paramref name="seconds"/>, 0–1 against the track's own range: from
        /// the envelope when the track has been read, from the live level otherwise.
        /// </summary>
        float Section(float seconds)
        {
            if (m_Envelope.Ready && m_Deck != null && m_Envelope.Clip == m_Deck.Clip)
                return m_Envelope.SectionAt(seconds);

            // The live fallback cannot see ahead: it only knows how loud the last few seconds were
            // against the loudest the track has been so far, whatever time is asked for.
            return Mathf.Clamp01(1f + (m_LiveDb - m_LivePeakDb) / 15f);
        }

        void UpdatePossession(bool playing, float time, float dt)
        {
            if (m_Analyser != null)
            {
                float db = 20f * Mathf.Log10(Mathf.Max(m_Analyser.Rms, 1e-5f));
                m_LiveDb = Mathf.Lerp(m_LiveDb, db, 1f - Mathf.Exp(-dt / 4f));
                m_LivePeakDb = Mathf.Max(m_LiveDb, m_LivePeakDb - dt * (6f / 60f));    // 6 dB a minute
            }

            float target = 0f;
            if (playing)
            {
                float section = Section(time + m_Lookahead);
                target = m_MinPossession + (1f - m_MinPossession) * Smooth01(section);
            }

            if (Summoned)
                target = Mathf.Max(target, 0.85f);

            float seconds = target > Possession ? m_PossessionRise : m_PossessionFall;
            Possession = Mathf.Lerp(Possession, target, 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, seconds)));

            if (playing)
                m_PeakPossession = Mathf.Max(m_PeakPossession, Possession);
        }

        /// <summary>
        /// Whether the ghost is on show. It appears on a loud section that follows a quieter one,
        /// holds for at most <see cref="m_RevealHold"/>, and then will not appear again for
        /// <see cref="m_RevealCooldown"/>. The lamps start going down when a qualifying section is
        /// <see cref="m_RevealLead"/> seconds away.
        /// </summary>
        void UpdateReveal(bool playing, float time, float dt)
        {
            float now = Section(time);
            float coming = Section(time + m_RevealLead);
            float clock = Time.time;

            if (!playing || now < m_EarnedBelow)
                m_LastLowAt = clock;

            bool earned = clock - m_LastLowAt < m_EarnWindow;
            bool rested = clock - m_RevealEndedAt > m_RevealCooldown;
            bool preDim = false;

            if (!m_Showing)
            {
                bool due = playing && earned && rested && coming >= m_RevealAt;
                preDim = due;
                if (due && now >= m_RevealAt)
                {
                    m_Showing = true;
                    m_ShowStart = clock;
                    RetargetNow();      // go to the stage now
                }
            }
            else if (!playing || now < m_RevealAt - 0.15f || clock - m_ShowStart > m_RevealHold)
            {
                m_Showing = false;
                m_RevealEndedAt = clock;
                RetargetNow();
            }

            float target = m_Showing || Summoned ? 1f : 0f;
            float seconds = target > Reveal ? m_RevealRise : m_RevealFall;
            Reveal = Mathf.MoveTowards(Reveal, target, dt / Mathf.Max(0.05f, seconds));

            m_PreDim = Mathf.MoveTowards(m_PreDim, preDim ? 1f : 0f, dt / Mathf.Max(0.1f, m_RevealLead));
            RoomLight = 1f - m_RevealDim * Smooth01(Mathf.Max(m_PreDim, Reveal));
        }

        void UpdateGlimpse(float dt)
        {
            Glimpse *= Mathf.Exp(-dt / Mathf.Max(0.05f, m_GlimpseDecay));

            if (Beat && Possession > m_GlimpseFrom && Reveal < 0.5f &&
                m_Random.NextDouble() < m_GlimpseChance * (0.4f + 0.6f * BeatStrength))
                Glimpse = 1f;
        }

        // ------------------------------------------------------------------ the ghosts

        void RetargetNow() => System.Array.Clear(m_RetargetAt, 0, m_RetargetAt.Length);

        /// <summary>
        /// The pair's dance: how far into partnering they are, where they are in the waltz, and
        /// whether this beat sets them twirling. One clock for all of them, so they move as a couple.
        /// </summary>
        void UpdateDance(float dt)
        {
            Together = m_Active.Count > 1 ? Smooth01(Reveal) : 0f;

            float turn = Mathf.Lerp(m_WaltzTurn.x, m_WaltzTurn.y, Level(Listen.Loudness));
            m_WaltzAngle = Mathf.Repeat(m_WaltzAngle + dt * Together * Mathf.PI * 2f / Mathf.Max(0.5f, turn), Mathf.PI * 2f);

            Twirl = Beat && Reveal > 0.5f && m_Random.NextDouble() < m_TwirlChance * BeatStrength;
        }

        /// <summary>
        /// Where ghost <paramref name="index"/> stands in the waltz, world metres from the middle of
        /// the dance: partners opposite each other on a flattened circle, rising round the back and
        /// dipping round the front, scaled by <see cref="Together"/>, so it opens out as they come
        /// together and closes as they part.
        /// </summary>
        public Vector3 WaltzOffset(int index)
        {
            int count = m_Active.Count;
            if (count < 2 || index < 0 || Together <= 0f)
                return Vector3.zero;

            float angle = m_WaltzAngle + index * Mathf.PI * 2f / count;
            float back = Mathf.Sin(angle);
            var local = new Vector3(Mathf.Cos(angle) * m_WaltzRadius.x, back * m_WaltzRise, back * m_WaltzRadius.y);
            return transform.TransformVector(local) * Together;
        }

        /// <summary>
        /// The middle of the dance: the stage, moved straight back from the viewer if it is nearer
        /// them than <see cref="m_DanceClearance"/> across the floor.
        /// </summary>
        Vector3 DanceCentre()
        {
            var stage = transform.TransformPoint(m_Stage);
            var away = stage - HeadPosition;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance < m_DanceClearance)
            {
                var direction = distance > 1e-3f ? away / distance : transform.forward;
                var moved = HeadPosition + direction * m_DanceClearance;
                stage = new Vector3(moved.x, stage.y, moved.z);
            }

            return ClampGhost(stage);
        }

        /// <summary>
        /// Picks where each ghost goes. On show, they take the stage together — every frame, so the
        /// dance keeps its distance from a viewer who walks toward it. Otherwise each drifts to
        /// whatever is about to wake — the lowest threshold not yet reached — so the possession
        /// spreads through the room following them; failing that it visits something already
        /// awake, or wanders. Never where another ghost is going: two ghosts, two places.
        /// </summary>
        void SteerGhosts()
        {
            bool onStage = m_Showing || Summoned;
            var centre = onStage ? DanceCentre() : Vector3.zero;

            for (int i = 0; i < m_Active.Count; i++)
            {
                var ghost = m_Active[i];
                if (ghost == null)
                    continue;

                if (onStage)
                {
                    m_Targets[i] = null;
                    m_TargetPoints[i] = centre;
                    ghost.SetTarget(centre);
                    continue;
                }

                if (Time.time < m_RetargetAt[i])
                    continue;

                m_RetargetAt[i] = Time.time + Mathf.Lerp(m_RetargetMin, m_RetargetMax, (float)m_Random.NextDouble());

                var next = PickTarget(i);
                m_Targets[i] = next;
                m_TargetPoints[i] = ClampGhost(next != null ? next.HauntPoint : Wander(i));
                ghost.SetTarget(m_TargetPoints[i]);
            }
        }

        Possessable PickTarget(int ghost)
        {
            Possessable next = null;
            foreach (var possessed in m_Possessed)
            {
                if (possessed == null || !possessed.Visit || possessed.Woken > 0.5f || possessed.Threshold > Possession + 0.2f ||
                    Taken(possessed, ghost))
                    continue;

                if (next == null || possessed.Threshold < next.Threshold)
                    next = possessed;
            }

            if (next != null)
                return next;

            var last = m_Targets[ghost];
            float total = 0f;
            foreach (var possessed in m_Possessed)
                if (possessed != null && possessed.Visit && possessed != last && !Taken(possessed, ghost))
                    total += 0.1f + possessed.Woken;

            float pick = (float)m_Random.NextDouble() * total;
            foreach (var possessed in m_Possessed)
            {
                if (possessed == null || !possessed.Visit || possessed == last || Taken(possessed, ghost))
                    continue;

                pick -= 0.1f + possessed.Woken;
                if (pick <= 0f)
                    return possessed;
            }

            return null;
        }

        /// <summary>Whether another ghost is going to <paramref name="possessed"/>, or somewhere too near it.</summary>
        bool Taken(Possessable possessed, int ghost)
        {
            for (int j = 0; j < m_Active.Count; j++)
                if (j != ghost && (m_Targets[j] == possessed || Vector3.Distance(m_TargetPoints[j], possessed.HauntPoint) < m_GhostSpacing))
                    return true;

            return false;
        }

        /// <summary>A point in the ghosts' volume, as far from where the others are going as a few tries find.</summary>
        Vector3 Wander(int ghost)
        {
            var best = transform.TransformPoint(m_Stage);
            float bestApart = -1f;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var local = new Vector3(
                    Mathf.Lerp(m_GhostMin.x, m_GhostMax.x, (float)m_Random.NextDouble()),
                    Mathf.Lerp(m_GhostMin.y, m_GhostMax.y, (float)m_Random.NextDouble()),
                    Mathf.Lerp(m_GhostMin.z, m_GhostMax.z, (float)m_Random.NextDouble()));
                var point = transform.TransformPoint(local);

                float apart = float.MaxValue;
                for (int j = 0; j < m_Active.Count; j++)
                    if (j != ghost)
                        apart = Mathf.Min(apart, Vector3.Distance(point, m_TargetPoints[j]));

                if (apart > bestApart)
                {
                    bestApart = apart;
                    best = point;
                }

                if (apart >= m_GhostSpacing)
                    break;
            }

            return best;
        }

        public Vector3 ClampGhost(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            local = Vector3.Max(m_GhostMin, Vector3.Min(m_GhostMax, local));
            return transform.TransformPoint(local);
        }

        /// <summary>The ghost nearest <paramref name="world"/>, or none.</summary>
        public SpectreGhost NearestGhost(Vector3 world)
        {
            SpectreGhost nearest = null;
            float best = float.MaxValue;
            foreach (var ghost in m_Active)
            {
                if (ghost == null)
                    continue;

                float d = (ghost.Position - world).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = ghost;
                }
            }

            return nearest;
        }

        /// <summary>Whether there is a partner to dance with, and to share the room's colours.</summary>
        public bool HasPartner => m_Active.Count > 1 && m_Active[0] != null && m_Active[1] != null;

        /// <summary>Metres across the line halfway between the ghosts over which their colours blend.</summary>
        const float k_ShareBlend = 0.3f;

        /// <summary>
        /// How much <paramref name="world"/> belongs to the partner rather than the first ghost: 0 the
        /// first's (blue), 1 the partner's (rose). The room is split across the floor at the line
        /// halfway between them, blending over <see cref="k_ShareBlend"/>; height is ignored, so the
        /// fire in the grate a metre below them splits as clearly as the candles beside them. Always
        /// 0 with one ghost.
        /// </summary>
        public float PartnerShare(Vector3 world)
        {
            if (!HasPartner)
                return 0f;

            var first = m_Active[0].Position;
            var partner = m_Active[1].Position;
            var across = partner - first;
            across.y = 0f;
            float length = across.magnitude;
            if (length < 1e-3f)
                return 0.5f;

            var from = world - (first + partner) * 0.5f;
            from.y = 0f;
            return Smooth01(Vector3.Dot(from, across / length) / k_ShareBlend + 0.5f);
        }

        /// <summary>How strongly the ghosts are disturbing <paramref name="world"/>: 1 with one on top of it, 0 beyond the wake radius of every one.</summary>
        public float WakeAt(Vector3 world)
        {
            var nearest = NearestGhost(world);
            if (nearest == null)
                return 0f;

            float d = Vector3.Distance(nearest.Position, world) / Mathf.Max(0.01f, m_WakeRadius);
            return 1f - Smooth01(d);
        }

        // ------------------------------------------------------------------ what objects read

        /// <summary>How awake an object with this threshold is, at this place: its threshold is lower where the ghost is.</summary>
        public float WokenFor(float threshold, Vector3 world)
        {
            float possession = Possession + m_GhostSpread * WakeAt(world);
            return Smooth01((possession - (threshold - 0.04f)) / 0.16f);
        }

        public float Level(Listen listen) => m_Levels[(int)listen];

        /// <summary>One band's level, 0 for the lowest band and 1 for the highest.</summary>
        public float Band(float fraction)
        {
            if (m_Analyser == null || m_Analyser.BandCount == 0)
                return 0f;

            return m_Analyser.BandAt(Mathf.Clamp01(fraction) * (m_Analyser.BandCount - 1));
        }

        void ReadLevels()
        {
            if (m_Analyser == null)
                return;

            m_Levels[(int)Listen.Bass] = Average(0f, 0.18f);
            m_Levels[(int)Listen.LowMid] = Average(0.18f, 0.36f);
            m_Levels[(int)Listen.Mid] = Average(0.36f, 0.6f);
            m_Levels[(int)Listen.HighMid] = Average(0.6f, 0.8f);
            m_Levels[(int)Listen.High] = Average(0.8f, 1f);
            m_Levels[(int)Listen.Loudness] = m_Analyser.Loudness;
        }

        float Average(float from, float to)
        {
            var bands = m_Analyser.Bands;
            int count = bands.Length;
            if (count == 0)
                return 0f;

            int lo = Mathf.Clamp(Mathf.FloorToInt(from * count), 0, count - 1);
            int hi = Mathf.Clamp(Mathf.CeilToInt(to * count), lo + 1, count);
            float sum = 0f;
            for (int i = lo; i < hi; i++)
                sum += bands[i];

            return sum / (hi - lo);
        }

        void FindHead()
        {
            if (m_Head == null && Time.time >= m_HeadSearchAt)
            {
                m_HeadSearchAt = Time.time + 1f;
                var node = GameObject.Find(m_HeadNode);
                if (node != null)
                    m_Head = node.transform;
            }

            // An untracked head sits at the node's origin on the floor; below half a metre it is not a person.
            var desk = transform.TransformPoint(m_DeskHead);
            HeadPosition = m_Head != null && m_Head.position.y > 0.5f ? m_Head.position : desk;
        }

        // ------------------------------------------------------------------ silence

        /// <summary>
        /// When the music stops after the room was properly possessed, everything settles — and
        /// then, a couple of seconds into the silence, one thing that was awake moves once more.
        /// </summary>
        void LastGasp(bool playing)
        {
            if (playing)
            {
                m_SilentSince = -1f;
                m_Gasped = false;
                return;
            }

            if (m_SilentSince < 0f)
            {
                m_SilentSince = Time.time;
                m_GaspAfter = 2f + 1.5f * (float)m_Random.NextDouble();
            }

            if (m_Gasped || m_PeakPossession < 0.3f || Time.time - m_SilentSince < m_GaspAfter)
                return;

            m_Gasped = true;
            m_PeakPossession = 0f;

            var candidates = new List<Possessable>();
            foreach (var possessed in m_Possessed)
                if (possessed != null && possessed.PeakWoken > 0.5f)
                    candidates.Add(possessed);

            if (candidates.Count > 0)
                candidates[m_Random.Next(candidates.Count)].Nudge(1f);

            foreach (var possessed in m_Possessed)
                possessed?.ClearPeak();
        }

        // ------------------------------------------------------------------ input

        public void SetLowIntensity(bool low)
        {
            m_LowIntensity = low;
            UpdateSign();
        }

        public void ToggleSummon()
        {
            Summoned = !Summoned;
            RetargetNow();
            UpdateSign();
        }

        /// <summary>The track button opens the tracks on the front wall, or closes them; with no menu it skips to the next track.</summary>
        void TrackButton()
        {
            if (m_Menu != null)
                m_Menu.Toggle();
            else
                m_Deck?.NextTrack();
        }

        void ReadInput()
        {
            if (m_SummonButton >= 0 && MiddleVRWand.PressedThisFrame(m_SummonButton))
                ToggleSummon();
            if (m_IntensityButton >= 0 && MiddleVRWand.PressedThisFrame(m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);
            if (m_NextTrackButton >= 0 && MiddleVRWand.PressedThisFrame(m_NextTrackButton))
                TrackButton();
            if (m_PlayPauseButton >= 0 && MiddleVRWand.PressedThisFrame(m_PlayPauseButton))
                m_Deck?.TogglePause();

            var keyboard = m_KeyboardControl ? Keyboard.current : null;
            if (keyboard == null)
                return;

            if (keyboard.gKey.wasPressedThisFrame || DigitPressed(keyboard, m_SummonButton))
                ToggleSummon();
            if (keyboard.lKey.wasPressedThisFrame || DigitPressed(keyboard, m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);

            // Shift skips the menu, straight to the next track.
            if (keyboard.nKey.wasPressedThisFrame && keyboard.shiftKey.isPressed)
                m_Deck?.NextTrack();
            else if (keyboard.nKey.wasPressedThisFrame || DigitPressed(keyboard, m_NextTrackButton))
                TrackButton();

            if (keyboard.spaceKey.wasPressedThisFrame || DigitPressed(keyboard, m_PlayPauseButton))
                m_Deck?.TogglePause();
        }

        /// <summary>The number-row key printed with the same digit as a wand button, the way the other demos do it.</summary>
        static bool DigitPressed(Keyboard keyboard, int button)
        {
            if (button < 0 || button > 9)
                return false;

            var key = button == 0 ? Key.Digit0 : Key.Digit1 + (button - 1);
            return keyboard[key].wasPressedThisFrame;
        }

        void UpdateSign()
        {
            if (m_Sign == null)
                return;

            m_Text.Clear();
            m_Text.Append("HAUNTED ROOM   ·   possessed ").Append(Mathf.RoundToInt(Possession * 100f)).Append('%');
            if (Summoned)
                m_Text.Append("   ·   SUMMONED");
            if (m_LowIntensity)
                m_Text.Append("   ·   LOW INTENSITY");
            if (m_Views != null && m_Views.Mono)
                m_Text.Append("   ·   ").Append(CaveViewMode.Label(m_Views.Current));

            m_Text.Append("   ·   ");
            var clip = m_Deck != null ? m_Deck.Clip : null;
            if (clip == null)
                m_Text.Append("no track — drop a clip in ").Append(MusicDeck.MusicFolder);
            else
            {
                m_Text.Append(TrackIndicator.TitleOf(clip));
                if (m_Deck.Paused)
                    m_Text.Append("  (paused)");

                var next = m_Deck.ClipAt(m_Deck.NextIndex);
                if (next != null && m_Deck.TrackCount > 1)
                    m_Text.Append("   ·   next: ").Append(TrackIndicator.TitleOf(next));
            }

            // Said where the operator looks, not only in the console.
            if (m_Menu == null)
                m_Text.Append("   ·   NO MENU: REBUILD THE SCENE");

            m_Sign.text = m_Text.ToString();
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}

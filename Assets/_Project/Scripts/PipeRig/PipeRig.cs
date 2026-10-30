using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.Pipes
{
    /// <summary>The rig's two ways of being used.</summary>
    public enum RigMode
    {
        /// <summary>Point at an instrument and pull the trigger to play it; the machine can loop what you play.</summary>
        Play,

        /// <summary>The machine plays along with the track, every ball landing as its note sounds.</summary>
        Visualize,
    }

    /// <summary>
    /// The pipe rig's director: a self-playing machine in the CAVE, pipes at the front throwing balls
    /// in arcs onto mallet bars, strings and drums round the walls, each ball solved backward from
    /// the note it must hit. It owns the mode, the clock, what is thrown when, the pointer, the
    /// machine's voices and the room's buttons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Visualize</b> plays the music folder through a <see cref="MusicDeck"/>. Each track is read
    /// ahead into notes (<see cref="RigScore"/>), and every note becomes a throw at the instrument
    /// that plays that part — the kick and snare and hats on the drums, the bass line on the bass
    /// strings, the middle on the mallet bars, the top on the strings — leaving its pipe exactly its
    /// flight time early. The track is the clock: pause it and every ball hangs in the air. The
    /// machine is silent in this mode; the track is what is heard.
    /// </para>
    /// <para>
    /// <b>Play</b> stops the music and hands over the instruments: point at one with the wand (or
    /// the mouse at a desk) and pull the trigger, and it sounds at once in the machine's own voices.
    /// With the loop recording, what is played is kept, quantized to a grid, and on every pass the
    /// pipes throw it back so it lands on time — the machine learning the riff. A backing beat can
    /// be switched on to play over. The clock here is the audio hardware's, and every looped note is
    /// scheduled on it with <see cref="AudioSource.PlayScheduled"/>, so sound and strike agree.
    /// </para>
    /// <para>
    /// Every path is checked at load against the room (<see cref="RigVolume"/>): a throw that would
    /// leave the screens or cross the people is an error in the editor and is never thrown.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-80)] // after the deck; before the menu, which must not take the press that opened it as a pick
    public class PipeRig : MonoBehaviour
    {
        [Header("Machine")]
        [SerializeField] RigPipe[] m_Pipes = new RigPipe[0];
        [SerializeField] RigTarget[] m_Targets = new RigTarget[0];
        [SerializeField] RigBalls m_Balls;

        [SerializeField, Tooltip("The part of the room throws may use, in the rig's space.")]
        RigVolume m_Room = RigVolume.Default;

        [SerializeField, Tooltip("Metres per second squared. Well under Earth's, so short room-sized arcs read as slow, floaty throws.")]
        float m_Gravity = 4f;

        [SerializeField] float m_BallRadius = 0.035f;

        [SerializeField, Tooltip("The mode the room opens in.")]
        RigMode m_Mode = RigMode.Play;

        [Header("Play")]
        [SerializeField, Tooltip("Beats a minute of the loop and the backing beat.")]
        float m_Tempo = 100f;

        [SerializeField, Tooltip("Beats in the loop: eight is two bars of four.")]
        int m_LoopBeats = 8;

        [SerializeField, Tooltip("Steps a beat a looped note is moved to: 4 is sixteenths, 2 eighths, 0 leaves it where it was played.")]
        int m_Quantize = 4;

        [SerializeField, Tooltip("Most notes the loop keeps.")]
        int m_MaxLoopNotes = 96;

        [SerializeField, Range(0f, 1f)] float m_Loudness = 0.8f;
        [SerializeField, Range(0f, 1f)] float m_HandStrength = 0.85f;

        [SerializeField, Tooltip("Sounds that can ring at once.")]
        int m_VoiceCount = 32;

        [Header("Visualize")]
        [SerializeField] MusicDeck m_Deck;

        [SerializeField, Tooltip("The first track waits, paused, for someone to press play.")]
        bool m_StartPaused = true;

        [SerializeField, Tooltip("Seconds the machine runs behind the track's clock, to match the delay between the mixer and the room's " +
            "speakers. Raise it if strikes land before their notes are heard.")]
        float m_VisualDelay;

        [SerializeField, Tooltip("Least seconds between two throws at one target, so a dense part does not stack balls on top of each other.")]
        float m_MinGap = 0.09f;

        [SerializeField, Tooltip("Least seconds between two throws from one pipe before the next pipe in a target's list takes over.")]
        float m_PipeGap = 0.22f;

        [Header("Pointer")]
        [SerializeField, Tooltip("The MiddleVR node the wand's pose is on. Its forward is where the wand points.")]
        string m_WandNode = "HandNode";

        [SerializeField, Tooltip("Wand button that strikes what the wand points at: the trigger.")]
        int m_StrikeButton;

        [SerializeField, Tooltip("Shows where the wand points in play mode.")]
        LineRenderer m_Ray;

        [SerializeField] Transform m_Cursor;
        [SerializeField] float m_RayLength = 4f;

        [Header("Lamps")]
        [SerializeField, Tooltip("One per beat of the loop, along the front of the pipe bank.")]
        Renderer[] m_BeatLamps = new Renderer[0];

        [SerializeField] Renderer m_RecordLamp;
        [SerializeField, ColorUsage(false, true)] Color m_LampColour = new(1.6f, 1.5f, 1.2f);
        [SerializeField, ColorUsage(false, true)] Color m_RecordColour = new(3f, 0.25f, 0.2f);

        [Header("Wand")]
        [SerializeField, Tooltip("Opens the modes on the front wall. Indices are whatever the .vrx says; the VisBox wand has 0-4, 6-8, 11 and 15, and 0 is the trigger.")]
        int m_ModeButton = 1;

        [SerializeField] int m_IntensityButton = 2;

        [SerializeField, Tooltip("Opens the tracks on the front wall.")]
        int m_TrackButton = 3;

        [SerializeField, Tooltip("Visualize: pauses the music. Play: the backing beat on or off.")]
        int m_PlayPauseButton = 4;

        [SerializeField, Tooltip("Play: the loop records what is played, or stops recording.")]
        int m_RecordButton = 6;

        [SerializeField, Tooltip("Play: empties the loop.")]
        int m_ClearButton = 7;

        [Header("Menu")]
        [SerializeField] PipeRigMenu m_Menu;

        [SerializeField, Tooltip("The now-playing strip. Shown only in visualize mode: in play mode there is no track.")]
        TrackIndicator m_Indicator;

        [Header("Desk")]
        [SerializeField, Tooltip("M opens the modes (Shift+M switches straight over), N the tracks (Shift+N skips), Space pauses or " +
            "starts the beat, R records the loop, C clears it, B the beat, L low intensity; the mouse points and clicks in play mode, and " +
            "the number-row keys stand in for the wand buttons with the same digit.")]
        bool m_KeyboardControl = true;

        [SerializeField, Tooltip("A dim line on the floor at the back of the room, for whoever is running the demo.")]
        TMP_Text m_Sign;

        [Header("Intensity")]
        [SerializeField, Tooltip("The photosensitivity setting: the glows on the walls and floor a third as bright and capped lower. On for a school group.")]
        bool m_LowIntensity;

        [SerializeField] float m_MaxFlashesPerSecond = 3f;
        [SerializeField] float m_LowIntensityFlashesPerSecond = 2f;

        /// <summary>A note of the loop or the backing beat: where in the loop, on what, how hard.</summary>
        struct LoopNote
        {
            public float Beat;
            public int Target;
            public float Strength;
        }

        /// <summary>The backing beat: an original two-bar groove, a drum kit over a bass line, by drum or bass note.</summary>
        static readonly (float Beat, RigDrum Drum, int Midi, float Strength)[] k_Groove =
        {
            (0f, RigDrum.Kick, 0, 1f), (2.5f, RigDrum.Kick, 0, 0.8f), (4f, RigDrum.Kick, 0, 1f), (6.5f, RigDrum.Kick, 0, 0.8f),
            (1f, RigDrum.Snare, 0, 0.9f), (3f, RigDrum.Snare, 0, 0.9f), (5f, RigDrum.Snare, 0, 0.9f), (7f, RigDrum.Snare, 0, 0.9f),
            (0.5f, RigDrum.HiHat, 0, 0.6f), (1.5f, RigDrum.HiHat, 0, 0.5f), (2.5f, RigDrum.HiHat, 0, 0.6f), (3.5f, RigDrum.HiHat, 0, 0.5f),
            (4.5f, RigDrum.HiHat, 0, 0.6f), (5.5f, RigDrum.HiHat, 0, 0.5f), (6.5f, RigDrum.HiHat, 0, 0.6f),
            (7.5f, RigDrum.Tom, 0, 0.7f), (7.75f, RigDrum.Tom, 0, 0.8f),
            (0f, RigDrum.None, 36, 0.9f), (1.5f, RigDrum.None, 36, 0.7f), (2.5f, RigDrum.None, 43, 0.8f), (3.5f, RigDrum.None, 40, 0.7f),
            (4f, RigDrum.None, 45, 0.9f), (5.5f, RigDrum.None, 45, 0.7f), (6.5f, RigDrum.None, 40, 0.8f), (7f, RigDrum.None, 43, 0.7f),
        };

        static readonly int k_Emission = Shader.PropertyToID("_EmissionColor");

        RigPaths m_Paths;
        int m_Problems;

        // Clock
        double m_Clock;
        bool m_ClockValid;

        // Visualize
        readonly Dictionary<AudioClip, RigScore> m_Scores = new();
        RigScore m_Score;
        AudioClip m_ScoredClip;
        readonly List<RigNote> m_Notes = new();
        int m_NotesCopied;
        int m_NextNote;

        // Who plays what
        readonly int[] m_DrumTarget = new int[6];
        readonly List<int>[] m_GroupTargets = new List<int>[4];
        readonly int[] m_PreviousMidi = new int[4];
        double[] m_TargetLast = new double[0];
        double[] m_PipeLast = new double[0];

        // Play
        readonly List<LoopNote> m_Loop = new();
        readonly List<LoopNote> m_GrooveNotes = new();
        readonly List<RigShot> m_Removed = new();
        bool[] m_BeatHasNote = new bool[0];
        double m_LoopOrigin;
        double m_ScheduledTo;
        bool m_Recording;
        bool m_Groove;

        // Voices
        AudioSource[] m_Voices = new AudioSource[0];
        double[] m_VoiceFree = new double[0];
        double[] m_VoiceStart = new double[0];

        // Pointer
        Transform m_Wand;
        float m_NextWandSearch;
        RigTarget m_Hovered;

        // Lamps and sign
        MaterialPropertyBlock m_Block;
        readonly StringBuilder m_Text = new();
        float m_SignAt;
        string m_Shown;

        public RigMode Mode => m_Mode;
        public MusicDeck Deck => m_Deck;
        public bool Recording => m_Recording;
        public bool Groove => m_Groove;
        public bool LowIntensity => m_LowIntensity;
        public int LoopNotes => m_Loop.Count;
        public RigTarget[] Targets => m_Targets;
        public RigBalls Balls => m_Balls;

        /// <summary>The paths, as solved and checked at load; null before.</summary>
        public RigPaths Paths => m_Paths;

        /// <summary>How many path problems the load-time check found. Zero is a layout that is safe to run.</summary>
        public int Problems => m_Problems;

        /// <summary>The score being thrown in visualize mode; null if there is no track.</summary>
        public RigScore Score => m_Score;

        double BeatSeconds => 60.0 / Mathf.Max(20f, m_Tempo);
        int LoopBeats => Mathf.Max(1, m_LoopBeats);

        // ================================================================== start

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();

            // Before the deck's Start, which plays or cues the first track: play mode has no music,
            // and visualize mode waits for play when it opens paused.
            if (m_Deck != null && (m_StartPaused || m_Mode == RigMode.Play))
                m_Deck.PlayOnStart = false;

            var problems = new List<string>();
            m_Paths = RigPaths.Build(transform, m_Pipes, m_Targets, m_Room, m_Gravity, m_BallRadius, problems);
            m_Problems = problems.Count;
            foreach (var problem in problems)
            {
                // A bad layout fails in the editor; in front of an audience the bad path is just never thrown.
#if UNITY_EDITOR
                Debug.LogError($"[PipeRig] {problem}", this);
#else
                Debug.LogWarning($"[PipeRig] {problem}", this);
#endif
            }

            m_TargetLast = new double[m_Targets.Length];
            m_PipeLast = new double[m_Pipes.Length];
            IndexTargets();
            MakeVoices();

            if (m_Balls != null)
                m_Balls.Init(m_Paths, m_Targets, m_Pipes, transform);
        }

        void Start()
        {
            if (m_Balls == null)
                Debug.LogError("[PipeRig] No balls: rebuild the scene with VR Shooting Gallery ▸ Pipe Rig ▸ Rebuild Pipe Rig.", this);

            EnterMode(m_Mode, false);
            UpdateSign();
        }

        void OnDestroy()
        {
            foreach (var score in m_Scores.Values)
                score.Cancel();
            m_Scores.Clear();
        }

        /// <summary>Which target is each drum, and each group's targets by pitch; and the backing beat resolved onto them.</summary>
        void IndexTargets()
        {
            for (int i = 0; i < m_DrumTarget.Length; i++)
                m_DrumTarget[i] = -1;
            for (int g = 0; g < m_GroupTargets.Length; g++)
                m_GroupTargets[g] = new List<int>();

            for (int i = 0; i < m_Targets.Length; i++)
            {
                var target = m_Targets[i];
                if (target == null)
                    continue;

                m_GroupTargets[(int)target.Group].Add(i);
                if (target.Group == RigGroup.Drums && target.Drum != RigDrum.None)
                    m_DrumTarget[(int)target.Drum] = i;
            }

            for (int g = 0; g < m_GroupTargets.Length; g++)
            {
                var list = m_GroupTargets[g];
                list.Sort((a, b) => m_Targets[a].Midi.CompareTo(m_Targets[b].Midi));
                m_PreviousMidi[g] = list.Count > 0 ? m_Targets[list[list.Count / 2]].Midi : 60;
            }

            m_GrooveNotes.Clear();
            foreach (var (beat, drum, midi, strength) in k_Groove)
            {
                int target = drum != RigDrum.None ? m_DrumTarget[(int)drum] : TargetWithMidi(RigGroup.Bass, midi);
                if (target >= 0)
                    m_GrooveNotes.Add(new LoopNote { Beat = beat, Target = target, Strength = strength });
            }
        }

        int TargetWithMidi(RigGroup group, int midi)
        {
            foreach (int i in m_GroupTargets[(int)group])
                if (m_Targets[i].Midi == midi)
                    return i;
            return -1;
        }

        /// <summary>A pool of 2D voices, panned by where each instrument stands, and every instrument's sound rendered.</summary>
        void MakeVoices()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            foreach (var target in m_Targets)
                if (target != null)
                    target.Voice = RigVoices.For(target, sampleRate);

            var holder = new GameObject("Voices").transform;
            holder.SetParent(transform, false);
            m_Voices = new AudioSource[Mathf.Max(4, m_VoiceCount)];
            m_VoiceFree = new double[m_Voices.Length];
            m_VoiceStart = new double[m_Voices.Length];
            for (int i = 0; i < m_Voices.Length; i++)
            {
                var source = holder.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                m_Voices[i] = source;
            }
        }

        // ================================================================== modes

        public void SetMode(RigMode mode)
        {
            if (mode == m_Mode)
                return;
            EnterMode(mode, true);
        }

        public void ToggleMode() => SetMode(m_Mode == RigMode.Play ? RigMode.Visualize : RigMode.Play);

        /// <summary>Plays <paramref name="index"/> from the tracks, going over to visualize mode if need be.</summary>
        public void PlayTrack(int index)
        {
            if (m_Deck == null)
                return;

            if (m_Mode != RigMode.Visualize)
                EnterMode(RigMode.Visualize, false);
            m_Deck.PlayTrack(index);
        }

        /// <summary>
        /// Leaves whatever was going on — the balls, the scheduled sounds — and starts
        /// <paramref name="mode"/>. Going to play pauses the music; going back to visualize picks it up
        /// again when <paramref name="startMusic"/>, starting the first track if none has played.
        /// </summary>
        void EnterMode(RigMode mode, bool startMusic)
        {
            m_Mode = mode;
            m_Balls?.Clear();
            StopScheduledVoices();
            for (int i = 0; i < m_PipeLast.Length; i++)
                m_PipeLast[i] = double.NegativeInfinity;
            for (int i = 0; i < m_TargetLast.Length; i++)
                m_TargetLast[i] = double.NegativeInfinity;
            m_ClockValid = false;
            SetHover(null);

            if (mode == RigMode.Play)
            {
                if (m_Deck != null && m_Deck.IsPlaying)
                    m_Deck.TogglePause();

                // The loop starts a moment from now, on the first beat, with nothing thrown for the time already gone.
                double now = AudioSettings.dspTime;
                m_LoopOrigin = now + 0.1;
                m_ScheduledTo = now;
            }
            else
            {
                if (startMusic && m_Deck != null && !m_Deck.IsPlaying)
                    m_Deck.TogglePause();
                m_NextNote = NoteAfter(m_Deck != null ? m_Deck.Time : 0f);
            }

            if (m_Indicator != null)
                m_Indicator.gameObject.SetActive(mode == RigMode.Visualize);
            if (m_Ray != null)
                m_Ray.enabled = false;
            if (m_Cursor != null)
                m_Cursor.gameObject.SetActive(false);

            UpdateSign();
        }

        // ================================================================== the frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            RigFlash.Intensity = m_LowIntensity ? 0.35f : 1f;
            RigFlash.MaxRate = m_LowIntensity ? m_LowIntensityFlashesPerSecond : m_MaxFlashesPerSecond;

            ReadInput();

            if (m_Mode == RigMode.Visualize)
                UpdateVisualize(dt);
            else
                UpdatePlay(dt);

            m_Balls?.Tick(m_Clock);
            UpdatePointer();
            UpdateLamps();

            if (Time.unscaledTime >= m_SignAt)
                UpdateSign();
        }

        /// <summary>
        /// Follows <paramref name="truth"/>, which only moves in audio-buffer steps, with a clock that
        /// moves every frame and is pulled gently back onto it; a jump (a seek, a hitch) is taken at once.
        /// </summary>
        void Advance(double truth, float dt, bool running)
        {
            if (!m_ClockValid || !running)
            {
                m_Clock = truth;
                m_ClockValid = true;
                return;
            }

            m_Clock += dt;
            double error = truth - m_Clock;
            if (Math.Abs(error) > 0.2)
                m_Clock = truth;
            else
                m_Clock += error * Math.Min(1.0, dt * 5.0);
        }

        // ================================================================== visualize

        void UpdateVisualize(float dt)
        {
            var clip = m_Deck != null ? m_Deck.Clip : null;
            if (clip != m_ScoredClip)
                UseScore(clip);

            if (m_Score != null)
            {
                if (!m_Score.Done && !m_Score.Failed)
                    m_Score.Step(4f);
                m_NotesCopied = m_Score.CopyNotes(m_Notes, m_NotesCopied);
            }

            double truth = (m_Deck != null ? m_Deck.Time : 0f) - m_VisualDelay;
            bool running = m_Deck != null && m_Deck.IsPlaying;

            // Back to the top of a track, or a jump: the throws in the air are for music that will not play.
            if (m_ClockValid && (truth < m_Clock - 0.3 || truth > m_Clock + 1.5))
            {
                m_Balls?.Clear();
                m_ClockValid = false;
                m_NextNote = NoteAfter(truth);
                for (int i = 0; i < m_TargetLast.Length; i++)
                    m_TargetLast[i] = double.NegativeInfinity;
            }

            Advance(truth, dt, running);
            ScheduleTrack();
        }

        /// <summary>The track has changed: its score from the ones already read, or a new one begun.</summary>
        void UseScore(AudioClip clip)
        {
            m_ScoredClip = clip;
            m_Balls?.Clear();
            m_Notes.Clear();
            m_NotesCopied = 0;
            m_NextNote = 0;
            m_ClockValid = false;

            if (clip == null)
            {
                m_Score = null;
                return;
            }

            if (!m_Scores.TryGetValue(clip, out m_Score) || m_Score.Failed)
            {
                m_Score?.Cancel();
                m_Score = new RigScore(clip);
                m_Scores[clip] = m_Score;
            }

            UpdateSign();
        }

        /// <summary>The first note after <paramref name="time"/>.</summary>
        int NoteAfter(double time)
        {
            int lo = 0, hi = m_Notes.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (m_Notes[mid].Time <= time)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>Throws every note coming up within the longest flight, each from a pipe that can reach its instrument.</summary>
        void ScheduleTrack()
        {
            if (m_Paths == null || m_Balls == null)
                return;

            double horizon = m_Clock + m_Paths.LongestFlight + 0.25;
            while (m_NextNote < m_Notes.Count && m_Notes[m_NextNote].Time <= horizon)
            {
                var note = m_Notes[m_NextNote++];
                if (note.Time <= m_Clock + 0.02)
                    continue;

                int target = TargetFor(note);
                if (target < 0 || note.Time - m_TargetLast[target] < m_MinGap)
                    continue;

                int pipe = PipeFor(target, note.Time);
                if (pipe < 0)
                    continue;

                m_TargetLast[target] = note.Time;
                m_Balls.Add(new RigShot { Target = target, Pipe = pipe, Hit = note.Time, Strength = note.Strength, Source = RigShotSource.Track, Voice = -1 });
            }
        }

        /// <summary>
        /// The instrument for a note: the drum for a drum part; for a tuned part, the bar or string of
        /// that pitch class in its group, the octave nearest the last one it played so a line moves
        /// like a line, or the nearest pitch class there is if the group has no such note.
        /// </summary>
        int TargetFor(RigNote note)
        {
            switch (note.Lane)
            {
                case RigLane.Kick: return m_DrumTarget[(int)RigDrum.Kick];
                case RigLane.Snare: return m_DrumTarget[(int)RigDrum.Snare];
                case RigLane.Tom: return m_DrumTarget[(int)RigDrum.Tom];
                case RigLane.HiHat: return m_DrumTarget[(int)RigDrum.HiHat];
                case RigLane.Crash: return m_DrumTarget[(int)RigDrum.Crash];
            }

            int group = note.Lane == RigLane.Bass ? (int)RigGroup.Bass : note.Lane == RigLane.Mid ? (int)RigGroup.Mallets : (int)RigGroup.Strings;
            var list = m_GroupTargets[group];
            if (list.Count == 0 || note.PitchClass < 0)
                return -1;

            for (int step = 0; step <= 6; step++)
            {
                int best = -1;
                int bestDistance = int.MaxValue;
                foreach (int i in list)
                {
                    int midi = m_Targets[i].Midi;
                    int away = (((midi - note.PitchClass) % 12) + 12) % 12;
                    if (away != step && 12 - away != step)
                        continue;
                    int distance = Mathf.Abs(midi - m_PreviousMidi[group]);
                    if (distance < bestDistance)
                    {
                        best = i;
                        bestDistance = distance;
                    }
                }

                if (best >= 0)
                {
                    m_PreviousMidi[group] = m_Targets[best].Midi;
                    return best;
                }
            }

            return -1;
        }

        /// <summary>
        /// The pipe to throw at <paramref name="target"/> so it strikes at <paramref name="hit"/>: the
        /// first in its list that has had a moment's rest, else the one that has rested longest, so a
        /// fast part is spread across a group's pipes instead of one pipe firing faster.
        /// </summary>
        int PipeFor(int target, double hit)
        {
            var pipes = m_Targets[target].Pipes;
            if (pipes == null || m_Paths == null)
                return -1;

            int best = -1;
            double bestRest = double.NegativeInfinity;
            double bestSpawn = 0.0;
            foreach (int p in pipes)
            {
                var flight = m_Paths.FlightOf(p, target);
                if (!flight.Valid)
                    continue;

                double spawn = hit - flight.Time;
                double rest = spawn - m_PipeLast[p];
                if (rest >= m_PipeGap)
                {
                    best = p;
                    bestSpawn = spawn;
                    break;
                }

                if (rest > bestRest)
                {
                    best = p;
                    bestRest = rest;
                    bestSpawn = spawn;
                }
            }

            if (best >= 0)
                m_PipeLast[best] = Math.Max(m_PipeLast[best], bestSpawn);
            return best;
        }

        // ================================================================== play

        void UpdatePlay(float dt)
        {
            double now = AudioSettings.dspTime;
            Advance(now, dt, true);
            SchedulePlay(now);
        }

        /// <summary>Throws the backing beat and the loop for the stretch of time just coming into reach.</summary>
        void SchedulePlay(double now)
        {
            if (m_Paths == null || m_Balls == null)
                return;

            double until = now + m_Paths.LongestFlight + 0.3;
            double from = Math.Max(m_ScheduledTo, now);
            if (until <= from)
                return;

            if (m_Groove)
                ScheduleLoop(m_GrooveNotes, from, until, now, RigShotSource.Groove);
            ScheduleLoop(m_Loop, from, until, now, RigShotSource.Loop);
            m_ScheduledTo = until;
        }

        void ScheduleLoop(List<LoopNote> notes, double from, double until, double now, RigShotSource source)
        {
            double beat = BeatSeconds;
            int length = LoopBeats;
            foreach (var note in notes)
            {
                // The first time round after `from` that this note comes up, then every loop after it.
                double into = (from - m_LoopOrigin) / beat - note.Beat;
                double pass = Math.Floor(into / length) + 1.0;
                for (double hit = m_LoopOrigin + (pass * length + note.Beat) * beat; hit <= until; hit += length * beat)
                {
                    if (hit <= from)
                        continue;

                    int pipe = PipeFor(note.Target, hit);
                    if (pipe < 0)
                        continue;

                    // Too late to throw on time: skipped, sound and all, rather than a ball out of nowhere.
                    if (m_Balls.SpawnFor(pipe, note.Target, hit) < now - 0.02)
                        continue;

                    int voice = Schedule(m_Targets[note.Target], note.Strength, hit);
                    m_Balls.Add(new RigShot { Target = note.Target, Pipe = pipe, Hit = hit, Strength = note.Strength, Source = source, Voice = voice });
                }
            }
        }

        /// <summary>Played by hand: struck and heard now, and kept in the loop if it is recording.</summary>
        public void StrikeByHand(RigTarget target)
        {
            if (target == null)
                return;

            target.Strike(m_HandStrength);
            Schedule(target, m_HandStrength, 0.0);

            if (m_Recording && m_Mode == RigMode.Play)
                Record(target, m_HandStrength);
        }

        void Record(RigTarget target, float strength)
        {
            int index = Array.IndexOf(m_Targets, target);
            if (index < 0 || m_Loop.Count >= m_MaxLoopNotes)
                return;

            double beats = (AudioSettings.dspTime - m_LoopOrigin) / BeatSeconds;
            int length = LoopBeats;
            double place = ((beats % length) + length) % length;
            if (m_Quantize > 0)
                place = Math.Round(place * m_Quantize) / m_Quantize % length;

            foreach (var note in m_Loop)
                if (note.Target == index && Math.Abs(note.Beat - place) < 1e-3)
                    return;

            m_Loop.Add(new LoopNote { Beat = (float)place, Target = index, Strength = strength });
            MarkBeats();
            UpdateSign();
        }

        public void ToggleRecord()
        {
            if (m_Mode != RigMode.Play)
                return;
            m_Recording = !m_Recording;
            UpdateSign();
        }

        /// <summary>Empties the loop. Balls already thrown land; the ones still waiting in their pipes, and their sounds, are taken back.</summary>
        public void ClearLoop()
        {
            m_Loop.Clear();
            Withdraw(RigShotSource.Loop);
            MarkBeats();
            UpdateSign();
        }

        public void ToggleGroove()
        {
            m_Groove = !m_Groove;
            if (!m_Groove)
                Withdraw(RigShotSource.Groove);
            UpdateSign();
        }

        void Withdraw(RigShotSource source)
        {
            if (m_Balls == null)
                return;

            m_Removed.Clear();
            m_Balls.RemoveUnlaunched(source, m_Removed);
            foreach (var shot in m_Removed)
                StopVoice(shot.Voice, shot.Hit);
        }

        void MarkBeats()
        {
            if (m_BeatHasNote.Length != LoopBeats)
                m_BeatHasNote = new bool[LoopBeats];
            Array.Clear(m_BeatHasNote, 0, m_BeatHasNote.Length);
            foreach (var note in m_Loop)
                m_BeatHasNote[Mathf.Clamp(Mathf.FloorToInt(note.Beat), 0, m_BeatHasNote.Length - 1)] = true;
        }

        // ================================================================== voices

        /// <summary>
        /// Plays <paramref name="target"/>'s sound at DSP time <paramref name="at"/>, or now for zero,
        /// on the voice that comes free soonest. Returns the voice, so a scheduled sound can be taken back.
        /// </summary>
        int Schedule(RigTarget target, float strength, double at)
        {
            if (target == null || target.Voice == null || m_Voices.Length == 0)
                return -1;

            double now = AudioSettings.dspTime;
            double start = at > 0.0 ? Math.Max(at, now) : now;
            int voice = 0;
            for (int i = 1; i < m_Voices.Length; i++)
                if (m_VoiceFree[i] < m_VoiceFree[voice])
                    voice = i;

            var source = m_Voices[voice];
            source.Stop();
            source.clip = target.Voice;
            source.volume = Mathf.Clamp01(m_Loudness * Mathf.Lerp(0.45f, 1f, strength) * target.Gain);

            // Low on the left, high on the right, as the instruments stand, so sound and sight agree.
            var local = transform.InverseTransformPoint(target.transform.position);
            source.panStereo = Mathf.Clamp(local.x / 1.778f, -1f, 1f) * 0.8f;

            if (at > 0.0)
                source.PlayScheduled(start);
            else
                source.Play();

            m_VoiceStart[voice] = start;
            m_VoiceFree[voice] = start + target.Voice.length;
            return voice;
        }

        /// <summary>Silences a sound scheduled for <paramref name="start"/> that has not started, if its voice has not been reused.</summary>
        void StopVoice(int voice, double start)
        {
            if (voice < 0 || voice >= m_Voices.Length || Math.Abs(m_VoiceStart[voice] - start) > 1e-4)
                return;
            if (start <= AudioSettings.dspTime)
                return;

            m_Voices[voice].Stop();
            m_VoiceFree[voice] = 0.0;
        }

        void StopScheduledVoices()
        {
            double now = AudioSettings.dspTime;
            for (int i = 0; i < m_Voices.Length; i++)
            {
                if (m_VoiceStart[i] > now)
                {
                    m_Voices[i].Stop();
                    m_VoiceFree[i] = 0.0;
                    m_VoiceStart[i] = 0.0;
                }
            }
        }

        // ================================================================== pointer

        void UpdatePointer()
        {
            bool menuOpen = m_Menu != null && m_Menu.IsOpen;
            bool active = m_Mode == RigMode.Play && !menuOpen;

            RigTarget best = null;
            bool wand = false;
            var ray = default(Ray);
            float bestDistance = float.PositiveInfinity;
            if (active && Pointer(out ray, out wand))
            {
                foreach (var target in m_Targets)
                {
                    if (target != null && target.Raycast(ray, out float distance) && distance < bestDistance)
                    {
                        best = target;
                        bestDistance = distance;
                    }
                }
            }

            SetHover(best);

            bool showRay = active && wand;
            if (m_Ray != null)
            {
                m_Ray.enabled = showRay;
                if (showRay)
                {
                    var end = ray.origin + ray.direction * (best != null ? bestDistance : m_RayLength);
                    m_Ray.positionCount = 2;
                    m_Ray.SetPosition(0, ray.origin);
                    m_Ray.SetPosition(1, end);
                }
            }

            if (m_Cursor != null)
            {
                bool showCursor = showRay && best != null;
                if (m_Cursor.gameObject.activeSelf != showCursor)
                    m_Cursor.gameObject.SetActive(showCursor);
                if (showCursor)
                    m_Cursor.position = ray.origin + ray.direction * bestDistance;
            }

            if (!active || best == null)
                return;

            bool pressed = m_StrikeButton >= 0 && MiddleVRWand.PressedThisFrame(m_StrikeButton);
            if (!pressed && m_KeyboardControl && !wand)
            {
                var mouse = Mouse.current;
                pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
            }

            if (pressed)
                StrikeByHand(best);
        }

        void SetHover(RigTarget target)
        {
            if (target == m_Hovered)
                return;
            if (m_Hovered != null)
                m_Hovered.Hover = false;
            m_Hovered = target;
            if (m_Hovered != null)
                m_Hovered.Hover = true;
        }

        /// <summary>The wand's ray when MiddleVR has one; at a desk, the mouse's through the main camera.</summary>
        bool Pointer(out Ray ray, out bool wand)
        {
            var node = Wand();
            if (node != null)
            {
                ray = new Ray(node.position, node.forward);
                wand = true;
                return true;
            }

            wand = false;
            var mouse = Mouse.current;
            var camera = Camera.main;
            if (mouse != null && camera != null && m_KeyboardControl)
            {
                ray = camera.ScreenPointToRay(mouse.position.ReadValue());
                return true;
            }

            ray = default;
            return false;
        }

        /// <summary>MiddleVR's wand node, once the kernel has made it. Looked for now and then, not every frame.</summary>
        Transform Wand()
        {
            if (m_Wand != null || !MiddleVRWand.Available || string.IsNullOrEmpty(m_WandNode))
                return m_Wand;
            if (Time.unscaledTime < m_NextWandSearch)
                return null;

            m_NextWandSearch = Time.unscaledTime + 1f;
            var node = GameObject.Find(m_WandNode);
            m_Wand = node != null ? node.transform : null;
            return m_Wand;
        }

        // ================================================================== lamps

        /// <summary>The beat lamps walk the loop in play mode, brighter where it has notes; the record lamp is red while it records.</summary>
        void UpdateLamps()
        {
            bool play = m_Mode == RigMode.Play;
            double beats = play ? (m_Clock - m_LoopOrigin) / BeatSeconds : 0.0;
            int length = LoopBeats;
            int current = play && beats >= 0.0 ? (int)Math.Floor(beats) % length : -1;
            float within = play && beats >= 0.0 ? (float)(beats - Math.Floor(beats)) : 1f;
            if (m_BeatHasNote.Length != length)
                MarkBeats();

            for (int i = 0; i < m_BeatLamps.Length; i++)
            {
                float level = !play ? 0.02f
                    : i == current ? Mathf.Lerp(1f, 0.45f, within)
                    : i < m_BeatHasNote.Length && m_BeatHasNote[i] ? 0.22f
                    : 0.05f;
                Lamp(m_BeatLamps[i], m_LampColour * level);
            }

            if (m_RecordLamp != null)
            {
                float level = !play ? 0.02f : m_Recording ? (current == 0 ? 1f : 0.65f) : 0.04f;
                Lamp(m_RecordLamp, m_RecordColour * level);
            }
        }

        void Lamp(Renderer lamp, Color colour)
        {
            if (lamp == null)
                return;
            lamp.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_Emission, colour);
            lamp.SetPropertyBlock(m_Block);
        }

        // ================================================================== input

        public void SetLowIntensity(bool low)
        {
            m_LowIntensity = low;
            UpdateSign();
        }

        /// <summary>The mode button opens the modes on the front wall, or closes them; with no menu it switches straight over.</summary>
        void ModeButton()
        {
            if (m_Menu != null)
                m_Menu.ToggleModes();
            else
                ToggleMode();
        }

        /// <summary>The track button opens the tracks, or closes them; with no menu it skips to the next track.</summary>
        void TrackButton()
        {
            if (m_Menu != null)
                m_Menu.ToggleTracks();
            else if (m_Deck != null)
                PlayTrack(m_Deck.NextIndex);
        }

        void PlayPause()
        {
            if (m_Mode == RigMode.Visualize)
                m_Deck?.TogglePause();
            else
                ToggleGroove();
        }

        void ReadInput()
        {
            if (m_ModeButton >= 0 && MiddleVRWand.PressedThisFrame(m_ModeButton))
                ModeButton();
            if (m_IntensityButton >= 0 && MiddleVRWand.PressedThisFrame(m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);
            if (m_TrackButton >= 0 && MiddleVRWand.PressedThisFrame(m_TrackButton))
                TrackButton();
            if (m_PlayPauseButton >= 0 && MiddleVRWand.PressedThisFrame(m_PlayPauseButton))
                PlayPause();
            if (m_RecordButton >= 0 && MiddleVRWand.PressedThisFrame(m_RecordButton))
                ToggleRecord();
            if (m_ClearButton >= 0 && MiddleVRWand.PressedThisFrame(m_ClearButton))
                ClearLoop();

            var keyboard = m_KeyboardControl ? Keyboard.current : null;
            if (keyboard == null)
                return;

            bool shift = keyboard.shiftKey.isPressed;

            // Shift skips the menus: straight to the other mode, straight to the next track.
            if (keyboard.mKey.wasPressedThisFrame && shift)
                ToggleMode();
            else if (keyboard.mKey.wasPressedThisFrame || DigitPressed(keyboard, m_ModeButton))
                ModeButton();

            if (keyboard.nKey.wasPressedThisFrame && shift)
                PlayTrack(m_Deck != null ? m_Deck.NextIndex : 0);
            else if (keyboard.nKey.wasPressedThisFrame || DigitPressed(keyboard, m_TrackButton))
                TrackButton();

            if (keyboard.lKey.wasPressedThisFrame || DigitPressed(keyboard, m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);
            if (keyboard.spaceKey.wasPressedThisFrame || DigitPressed(keyboard, m_PlayPauseButton))
                PlayPause();
            if (keyboard.rKey.wasPressedThisFrame || DigitPressed(keyboard, m_RecordButton))
                ToggleRecord();
            if (keyboard.cKey.wasPressedThisFrame || DigitPressed(keyboard, m_ClearButton))
                ClearLoop();
            if (keyboard.bKey.wasPressedThisFrame && m_Mode == RigMode.Play)
                ToggleGroove();
        }

        /// <summary>The number-row key printed with the same digit as a wand button, the way the other demos do it. Not the trigger's.</summary>
        static bool DigitPressed(Keyboard keyboard, int button)
        {
            if (button <= 0 || button > 9)
                return false;

            return keyboard[Key.Digit1 + (button - 1)].wasPressedThisFrame;
        }

        // ================================================================== sign

        void UpdateSign()
        {
            m_SignAt = Time.unscaledTime + 0.25f;
            if (m_Sign == null)
                return;

            m_Text.Clear();
            m_Text.Append("PIPE RIG   ·   ");
            if (m_Mode == RigMode.Play)
            {
                m_Text.Append("PLAY   ·   point at an instrument and pull the trigger");
                m_Text.Append("   ·   loop ").Append(m_Recording ? "RECORDING" : "off");
                if (m_Loop.Count > 0)
                    m_Text.Append(", ").Append(m_Loop.Count).Append(m_Loop.Count == 1 ? " note" : " notes");
                m_Text.Append("   ·   beat ").Append(m_Groove ? "on" : "off");
                m_Text.Append("   ·   R record  C clear  B beat  M modes");
            }
            else
            {
                m_Text.Append("VISUALIZE   ·   ");
                var clip = m_Deck != null ? m_Deck.Clip : null;
                if (clip == null)
                {
                    m_Text.Append("no track — drop a clip in ").Append(MusicDeck.MusicFolder);
                }
                else
                {
                    m_Text.Append(TrackIndicator.TitleOf(clip));
                    if (m_Deck.Paused)
                        m_Text.Append("  (paused — Space plays)");

                    if (m_Score != null && m_Score.Failed)
                        m_Text.Append("   ·   cannot read it ahead: ").Append(m_Score.Error);
                    else if (m_Score != null && !m_Score.Done)
                        m_Text.Append("   ·   reading ahead ").Append(Mathf.RoundToInt(m_Score.Progress * 100f)).Append('%');

                    var next = m_Deck.ClipAt(m_Deck.NextIndex);
                    if (next != null && m_Deck.TrackCount > 1)
                        m_Text.Append("   ·   next: ").Append(TrackIndicator.TitleOf(next));
                }
            }

            if (m_LowIntensity)
                m_Text.Append("   ·   LOW INTENSITY");
            if (m_Problems > 0)
                m_Text.Append("   ·   LAYOUT: ").Append(m_Problems).Append(" PATH PROBLEM").Append(m_Problems == 1 ? "" : "S").Append(", SEE CONSOLE");

            string text = m_Text.ToString();
            if (text != m_Shown)
            {
                m_Shown = text;
                m_Sign.text = text;
            }
        }
    }
}

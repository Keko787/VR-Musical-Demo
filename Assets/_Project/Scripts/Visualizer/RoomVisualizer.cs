using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.Visualizer
{
    /// <summary>Which of the room's shows is on. The order is the order the mode button cycles through.</summary>
    public enum VisualizerMode
    {
        /// <summary>Water tubes flat on the four screens. Reads from anywhere, no glasses needed.</summary>
        Surface2D,

        /// <summary>Lasers and waveforms in the volume. Right from the tracked viewpoint; the hit-spots and wash carry the rest of the room.</summary>
        Room3D,

        /// <summary>The spectrogram as a field of points flowing through the room.</summary>
        PointCloud,

        /// <summary>Musica universalis: a sun wearing the spectrogram, ringed by celestial spheres at harmonic radii.</summary>
        AstralBody,

        /// <summary>A swarm of fireflies in a box of fixed size; the music is how many are lit, how they dart and blink.</summary>
        Fireflies,
    }

    /// <summary>
    /// The room audio visualizer and the rave visualizer as one build, per the deck: the hands-off
    /// demo that runs first and needs nothing tracked. Five modes — 2D on the surfaces, and four
    /// in the volume — cross-faded rather than cut, an intensity control with a low-intensity
    /// setting for visitors who need it, and an auto-cycle so the room can run unattended between
    /// visitors.
    /// </summary>
    /// <remarks>
    /// This component owns the switches and the playlist; the analyser owns the numbers and each
    /// layer owns its own look. Every layer has its own blend that eases toward 1 when its mode is
    /// on and 0 otherwise, so any mode fades into any other, and the wash behind the tubes stays on
    /// in all of them — the walls never go black.
    /// </remarks>
    [DefaultExecutionOrder(-60)] // after the analyser, before the layers, so a fade lands the frame it is set
    public class RoomVisualizer : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] AudioAnalyser m_Analyser;

        [SerializeField, Tooltip("Plays the playlist. 2D, so the room's speakers carry the mix as mastered; a spatial " +
            "source that followed the tracked user is the deck's idea for later.")]
        AudioSource m_Music;

        [SerializeField, Tooltip("Tracks, in order. Filled from Assets/_Project/Audio/Music whenever that folder has anything " +
            "in it: by the builder, again each time play starts in the editor, and at player build time.")]
        AudioClip[] m_Playlist;

        [SerializeField] bool m_PlayOnStart = true;
        [SerializeField] bool m_LoopPlaylist = true;

        [Header("Mode")]
        [SerializeField] VisualizerMode m_Mode = VisualizerMode.Surface2D;

        [SerializeField, Tooltip("Seconds the cross-fade between modes takes.")]
        float m_FadeSeconds = 1.5f;

        [SerializeField, Tooltip("Seconds between automatic mode changes for a hands-off run. Zero leaves the mode where it is.")]
        float m_AutoCycleSeconds = 0f;

        [Header("Intensity")]
        [SerializeField, Range(0f, 1f)] float m_Intensity = 1f;

        [SerializeField, Tooltip("The photosensitivity setting from the deck: dimmer, no beat flash on the walls, and the beat " +
            "rate capped lower. Toggled from the wand or the keyboard; on for a school group.")]
        bool m_LowIntensity;

        [SerializeField, Range(0.1f, 1f)] float m_LowIntensityScale = 0.45f;
        [SerializeField] float m_MaxFlashesPerSecond = 3f;
        [SerializeField] float m_LowIntensityFlashesPerSecond = 2f;

        [Header("Layers")]
        [SerializeField, Tooltip("The four screens' water panels. Filled in by the builder.")]
        WaterTubePanel[] m_Panels;

        [SerializeField] LaserShow m_Lasers;

        [SerializeField, Tooltip("The waveform traces. Filled in by the builder.")]
        WaveformRibbon[] m_Ribbons;

        [SerializeField, Tooltip("The spectrum field cloud. Filled in by the builder.")]
        AudioPointCloud m_PointCloud;

        [SerializeField, Tooltip("The astral body cloud. Filled in by the builder.")]
        AudioPointCloud m_AstralBody;

        [SerializeField, Tooltip("The firefly swarm. Filled in by the builder.")]
        AudioPointCloud m_Fireflies;

        [SerializeField, Tooltip("A small line on the front wall with the mode and the track, for whoever is running the demo.")]
        TMP_Text m_Sign;

        [Header("Wand")]
        [SerializeField, Tooltip("Wand button that steps to the next mode. Indices are whatever the .vrx says; the VisBox wand has 0-4, 6-8, 11 and 15, and 0 is the trigger.")]
        int m_ModeButton = 1;

        [SerializeField, Tooltip("Wand button that toggles low intensity.")]
        int m_IntensityButton = 2;

        [SerializeField, Tooltip("Wand button that skips to the next track.")]
        int m_NextTrackButton = 3;

        [SerializeField, Tooltip("Wand button that pauses and resumes.")]
        int m_PlayPauseButton = 4;

        [Header("Desk")]
        [SerializeField, Tooltip("M steps to the next mode (Shift+M back), L toggles low intensity, N skips the track, Space " +
            "pauses, and the number-row keys stand in for the wand buttons with the same digit. Stays on in builds so " +
            "the room's audio routing can be checked from the server node.")]
        bool m_KeyboardControl = true;

        /// <summary>Where tracks go. Every clip in here, by name, is the playlist.</summary>
        public const string MusicFolder = MusicDeck.MusicFolder;

        static readonly VisualizerMode[] k_Cycle =
        {
            VisualizerMode.Surface2D, VisualizerMode.Room3D, VisualizerMode.PointCloud, VisualizerMode.AstralBody,
            VisualizerMode.Fireflies,
        };

        // One blend per layer, 0 … 1. A layer eases toward 1 while its mode is on and toward 0 otherwise.
        float m_BlendTubes;
        float m_BlendLasers;
        float m_BlendCloud;
        float m_BlendBody;
        float m_BlendFlies;

        float m_CycleTimer;
        int m_Track = -1;
        bool m_Paused;
        readonly StringBuilder m_Text = new();

        public VisualizerMode Mode => m_Mode;
        public bool LowIntensity => m_LowIntensity;
        public float Intensity => m_Intensity;
        public int Track => m_Track;
        public AudioClip CurrentClip => m_Music != null ? m_Music.clip : null;
        public AudioClip[] Playlist { get => m_Playlist; set => m_Playlist = value; }

#if UNITY_EDITOR
        /// <summary>Every audio clip in <see cref="MusicFolder"/>, sorted by name. See <see cref="MusicDeck.ClipsInMusicFolder"/>.</summary>
        public static AudioClip[] ClipsInMusicFolder() => MusicDeck.ClipsInMusicFolder();
#endif

        void Start()
        {
#if UNITY_EDITOR
            // The folder is read again every time play starts, so a track dropped in after the
            // scene was built plays without a rebuild. Player builds get the same refresh from
            // VisualizerPlaylistProcessor at build time.
            var folder = ClipsInMusicFolder();
            if (folder.Length > 0)
                m_Playlist = folder;
#endif

            // Start on the chosen mode with no fade-in.
            m_BlendTubes = m_Mode == VisualizerMode.Surface2D ? 1f : 0f;
            m_BlendLasers = m_Mode == VisualizerMode.Room3D ? 1f : 0f;
            m_BlendCloud = m_Mode == VisualizerMode.PointCloud ? 1f : 0f;
            m_BlendBody = m_Mode == VisualizerMode.AstralBody ? 1f : 0f;
            m_BlendFlies = m_Mode == VisualizerMode.Fireflies ? 1f : 0f;
            ApplyLayers();

            if (m_PlayOnStart)
                PlayTrack(0);
            else
                UpdateSign();
        }

        void Update()
        {
            ReadWand();
            if (m_KeyboardControl)
                ReadKeyboard();

            AutoCycle();
            Fade();
            ApplyLayers();
            AdvancePlaylist();
        }

        // ------------------------------------------------------------------ mode

        public void SetMode(VisualizerMode mode)
        {
            if (m_Mode == mode)
                return;

            m_Mode = mode;
            m_CycleTimer = 0f;
            UpdateSign();
        }

        public void NextMode() => StepMode(1);
        public void PreviousMode() => StepMode(-1);

        /// <summary>Kept for anything wired to the old two-mode toggle; steps forward.</summary>
        public void ToggleMode() => NextMode();

        void StepMode(int step)
        {
            int index = System.Array.IndexOf(k_Cycle, m_Mode);
            if (index < 0)
                index = 0;

            int next = ((index + step) % k_Cycle.Length + k_Cycle.Length) % k_Cycle.Length;
            SetMode(k_Cycle[next]);
        }

        public void SetLowIntensity(bool low)
        {
            m_LowIntensity = low;
            UpdateSign();
        }

        void AutoCycle()
        {
            if (m_AutoCycleSeconds <= 0f)
                return;

            m_CycleTimer += Time.deltaTime;
            if (m_CycleTimer >= m_AutoCycleSeconds)
                NextMode();
        }

        void Fade()
        {
            float step = m_FadeSeconds > 0f ? Time.deltaTime / m_FadeSeconds : 1f;
            m_BlendTubes = Mathf.MoveTowards(m_BlendTubes, m_Mode == VisualizerMode.Surface2D ? 1f : 0f, step);
            m_BlendLasers = Mathf.MoveTowards(m_BlendLasers, m_Mode == VisualizerMode.Room3D ? 1f : 0f, step);
            m_BlendCloud = Mathf.MoveTowards(m_BlendCloud, m_Mode == VisualizerMode.PointCloud ? 1f : 0f, step);
            m_BlendBody = Mathf.MoveTowards(m_BlendBody, m_Mode == VisualizerMode.AstralBody ? 1f : 0f, step);
            m_BlendFlies = Mathf.MoveTowards(m_BlendFlies, m_Mode == VisualizerMode.Fireflies ? 1f : 0f, step);
        }

        /// <summary>
        /// Pushes each layer's blend and the intensity into it, and switches a volume layer's
        /// objects off entirely once it has faded out so an idle layer costs nothing.
        /// </summary>
        void ApplyLayers()
        {
            float intensity = m_Intensity * (m_LowIntensity ? m_LowIntensityScale : 1f);

            if (m_Analyser != null)
                m_Analyser.MaxBeatsPerSecond = m_LowIntensity ? m_LowIntensityFlashesPerSecond : m_MaxFlashesPerSecond;

            float tubes = Mathf.SmoothStep(0f, 1f, m_BlendTubes);
            if (m_Panels != null)
            {
                foreach (var panel in m_Panels)
                {
                    if (panel == null)
                        continue;

                    panel.Opacity = tubes;
                    panel.Intensity = intensity;
                    panel.BeatFlashEnabled = !m_LowIntensity;
                }
            }

            float lasers = Mathf.SmoothStep(0f, 1f, m_BlendLasers);
            bool showLasers = m_BlendLasers > 0f;

            if (m_Lasers != null)
            {
                m_Lasers.Opacity = lasers;
                m_Lasers.Intensity = intensity;
                Show(m_Lasers.gameObject, showLasers);
            }

            if (m_Ribbons != null)
            {
                foreach (var ribbon in m_Ribbons)
                {
                    if (ribbon == null)
                        continue;

                    ribbon.Opacity = lasers;
                    ribbon.Intensity = intensity;
                    Show(ribbon.gameObject, showLasers);
                }
            }

            ApplyCloud(m_PointCloud, m_BlendCloud, intensity);
            ApplyCloud(m_AstralBody, m_BlendBody, intensity);
            ApplyCloud(m_Fireflies, m_BlendFlies, intensity);
        }

        static void ApplyCloud(AudioPointCloud cloud, float blend, float intensity)
        {
            if (cloud == null)
                return;

            cloud.Opacity = Mathf.SmoothStep(0f, 1f, blend);
            cloud.Intensity = intensity;
            Show(cloud.gameObject, blend > 0f);
        }

        static void Show(GameObject go, bool on)
        {
            if (go.activeSelf != on)
                go.SetActive(on);
        }

        // ------------------------------------------------------------------ playlist

        public void PlayTrack(int index)
        {
            if (m_Music == null || m_Playlist == null || m_Playlist.Length == 0)
            {
                if (m_Music != null && m_Music.clip != null && !m_Music.isPlaying)
                    m_Music.Play();

                UpdateSign();
                return;
            }

            m_Track = ((index % m_Playlist.Length) + m_Playlist.Length) % m_Playlist.Length;
            m_Music.clip = m_Playlist[m_Track];
            m_Music.loop = false;
            m_Paused = false;
            m_Music.Play();
            UpdateSign();
        }

        public void NextTrack() => PlayTrack(m_Track + 1);

        public void TogglePause()
        {
            if (m_Music == null)
                return;

            if (m_Music.isPlaying)
            {
                m_Music.Pause();
                m_Paused = true;
            }
            else
            {
                if (m_Music.clip == null)
                    PlayTrack(0);
                else
                    m_Music.UnPause();

                m_Paused = false;
            }

            UpdateSign();
        }

        void AdvancePlaylist()
        {
            if (m_Music == null || m_Paused || m_Playlist == null || m_Playlist.Length == 0 || m_Track < 0)
                return;

            // Stopped with the play head at zero is a clip that ran out; stopped part way through is
            // Unity pausing audio because the window lost focus, which must not skip a track.
            if (m_Music.isPlaying || m_Music.clip == null || m_Music.time > 0.01f)
                return;

            // A single track loops; a playlist walks on, or stops at the end.
            if (m_Playlist.Length == 1 || m_LoopPlaylist || m_Track + 1 < m_Playlist.Length)
                NextTrack();
            else
                m_Paused = true;
        }

        // ------------------------------------------------------------------ input

        void ReadWand()
        {
            if (m_ModeButton >= 0 && MiddleVRWand.PressedThisFrame(m_ModeButton))
                NextMode();

            if (m_IntensityButton >= 0 && MiddleVRWand.PressedThisFrame(m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);

            if (m_NextTrackButton >= 0 && MiddleVRWand.PressedThisFrame(m_NextTrackButton))
                NextTrack();

            if (m_PlayPauseButton >= 0 && MiddleVRWand.PressedThisFrame(m_PlayPauseButton))
                TogglePause();
        }

        void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.mKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                    PreviousMode();
                else
                    NextMode();
            }
            else if (DigitPressed(keyboard, m_ModeButton))
            {
                NextMode();
            }

            if (keyboard.lKey.wasPressedThisFrame || DigitPressed(keyboard, m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);

            if (keyboard.nKey.wasPressedThisFrame || DigitPressed(keyboard, m_NextTrackButton))
                NextTrack();

            if (keyboard.spaceKey.wasPressedThisFrame || DigitPressed(keyboard, m_PlayPauseButton))
                TogglePause();
        }

        /// <summary>The number-row key printed with the same digit as a wand button, the way the other demos do it.</summary>
        static bool DigitPressed(Keyboard keyboard, int button)
        {
            if (button < 0 || button > 9)
                return false;

            var key = button == 0 ? Key.Digit0 : Key.Digit1 + (button - 1);
            return keyboard[key].wasPressedThisFrame;
        }

        // ------------------------------------------------------------------ sign

        static string NameOf(VisualizerMode mode)
        {
            switch (mode)
            {
                case VisualizerMode.Room3D: return "3D  LASERS";
                case VisualizerMode.PointCloud: return "3D  POINT CLOUD";
                case VisualizerMode.AstralBody: return "3D  MUSICA UNIVERSALIS";
                case VisualizerMode.Fireflies: return "3D  FIREFLIES";
                default: return "2D  WATER";
            }
        }

        void UpdateSign()
        {
            if (m_Sign == null)
                return;

            m_Text.Clear();
            m_Text.Append(NameOf(m_Mode));
            if (m_LowIntensity)
                m_Text.Append("   ·   LOW INTENSITY");

            m_Text.Append("   ·   ");
            if (m_Music == null || m_Music.clip == null)
                m_Text.Append("no track — drop a clip in ").Append(MusicFolder).Append(" and press Play again");
            else
            {
                if (m_Playlist != null && m_Playlist.Length > 1)
                    m_Text.Append(m_Track + 1).Append('/').Append(m_Playlist.Length).Append("  ");

                m_Text.Append(m_Music.clip.name);
                if (m_Paused)
                    m_Text.Append("  (paused)");
            }

            m_Sign.text = m_Text.ToString();
        }
    }
}

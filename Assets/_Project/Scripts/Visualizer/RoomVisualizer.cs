using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// One of the room's shows. One is on, or several layered. The menu lists them in
    /// <see cref="RoomVisualizer.Modes"/>' order, the surface ones in one table and the volume ones in
    /// the other; the values are saved in scenes, so a new mode goes on the end.
    /// </summary>
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

        /// <summary>The four screens as one sheet of rainbow liquid the music drops things into. 2D, like the water.</summary>
        Ripples,

        /// <summary>The room as a lava lamp: wax warmed on the floor by the bass climbs the walls, cools, and sinks. 2D.</summary>
        LavaLamp,

        /// <summary>A holographic tree of points that sways with the song and drops its leaves on the beat. 3D.</summary>
        Tree,

        /// <summary>The room under the sea: sunlight through the waves, bubbles on the beat, kelp and a school of fish. 2D.</summary>
        Underwater,
    }

    /// <summary>
    /// The room audio visualizer and the rave visualizer as one build, per the deck: the hands-off
    /// demo that runs first and needs nothing tracked. Nine modes — four on the surfaces, five in
    /// the volume — cross-faded rather than cut and layered as well as switched, an intensity control
    /// with a low-intensity setting for visitors who need it, and an auto-cycle so the room can run
    /// unattended between visitors.
    /// </summary>
    /// <remarks>
    /// This component owns the switches; the <see cref="MusicDeck"/> owns the playlist, the analyser
    /// owns the numbers and each layer owns its own look. Every layer has its own blend that eases
    /// toward 1 when its mode is on and 0 otherwise, so any mode fades into any other, any set of
    /// them can be on together, and the wash behind the tubes stays on in all of them — the walls
    /// never go black. The mode and track buttons open the <see cref="VisualizerMenu"/> on the front
    /// wall: a pick there switches to one mode, a double pick layers it with the others.
    /// </remarks>
    [DefaultExecutionOrder(-60)] // after the analyser, before the layers and the menu, so a fade or a press lands the frame it happens
    public class RoomVisualizer : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] AudioAnalyser m_Analyser;

        [SerializeField, Tooltip("Plays the music folder: the playlist, play and pause, and the track that plays next, which the menu " +
            "and the now-playing strip show. 2D, so the room's speakers carry the mix as mastered.")]
        MusicDeck m_Deck;

        [SerializeField, Tooltip("Opens on the first track, paused, so the room waits at its idle breathing until someone presses " +
            "play (wand 4, Space) or picks a track. Off, and the music starts with the scene.")]
        bool m_StartPaused = true;

        [Header("Mode")]
        [SerializeField, Tooltip("The mode the room opens on. The menu's double pick layers others with it.")]
        VisualizerMode m_Mode = VisualizerMode.Surface2D;

        [SerializeField, Tooltip("Seconds the cross-fade between modes takes.")]
        float m_FadeSeconds = 1.5f;

        [SerializeField, Tooltip("Seconds between automatic mode changes for a hands-off run. Zero leaves the mode where it is. " +
            "It waits while the menu is open.")]
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

        [SerializeField, Tooltip("The rainbow ripples over the four screens. Filled in by the builder.")]
        RippleSurface m_Ripples;

        [SerializeField, Tooltip("The lava lamp over the four screens. Filled in by the builder.")]
        LavaLamp m_LavaLamp;

        [SerializeField, Tooltip("The sea over the four screens. Filled in by the builder.")]
        Underwater m_Underwater;

        [SerializeField] LaserShow m_Lasers;

        [SerializeField, Tooltip("The waveform traces. Filled in by the builder.")]
        WaveformRibbon[] m_Ribbons;

        [SerializeField, Tooltip("The spectrum field cloud. Filled in by the builder.")]
        AudioPointCloud m_PointCloud;

        [SerializeField, Tooltip("The astral body cloud. Filled in by the builder.")]
        AudioPointCloud m_AstralBody;

        [SerializeField, Tooltip("The firefly swarm. Filled in by the builder.")]
        AudioPointCloud m_Fireflies;

        [SerializeField, Tooltip("The holographic tree. Filled in by the builder.")]
        AudioTree m_Tree;

        [SerializeField, Tooltip("A small line on the front wall with the mode and the track, for whoever is running the demo.")]
        TMP_Text m_Sign;

        [Header("Menu")]
        [SerializeField, Tooltip("The menu on the front wall the mode and track buttons open. None, and the mode button steps to the " +
            "next mode and the track button skips to the next track instead.")]
        VisualizerMenu m_Menu;

        [SerializeField, Range(0f, 1f), Tooltip("How far the shows in the room's volume dim while the menu is open, so it can be " +
            "read through them. The water, on the walls behind the panel, stays.")]
        float m_MenuDim = 0.75f;

        [Header("Musica Universalis with the tree")]
        [SerializeField, Tooltip("How far Musica Universalis moves while the tree is on with it, so the two do not run into each " +
            "other: up, its spheres clear above the crown, its top still under the top of the walls. Measured in batch: with the " +
            "tree's move, the nearest points of the two stay about 15 cm apart.")]
        Vector3 m_BodyBesideTree = new(0f, 0.3f, 0f);

        [SerializeField, Tooltip("How far the tree moves while Musica Universalis is on with it: to the left, its crown still about " +
            "10 cm off the left wall as it sways, and back from the front wall. A second tree, its mirror image, stands as far " +
            "to the right, so the two flank the astral body.")]
        Vector3 m_TreeBesideBody = new(-0.85f, 0f, -0.45f);

        [Header("Wand")]
        [SerializeField, Tooltip("Wand button that opens the modes on the front wall (or, without a menu, steps to the next mode). " +
            "Indices are whatever the .vrx says; the VisBox wand has 0-4, 6-8, 11 and 15, and 0 is the trigger, which picks in the menu.")]
        int m_ModeButton = 1;

        [SerializeField, Tooltip("Wand button that toggles low intensity.")]
        int m_IntensityButton = 2;

        [SerializeField, Tooltip("Wand button that opens the tracks on the front wall (or, without a menu, skips to the next track).")]
        int m_NextTrackButton = 3;

        [SerializeField, Tooltip("Wand button that pauses and resumes.")]
        int m_PlayPauseButton = 4;

        [Header("Desk")]
        [SerializeField, Tooltip("M opens the modes (Shift+M steps straight to the next), N opens the tracks (Shift+N skips straight " +
            "to the next), L toggles low intensity, Space pauses, and the number-row keys stand in for the wand buttons with the " +
            "same digit. In the menu: the arrows, Enter, and Backspace or Escape. Stays on in builds so the room's audio routing " +
            "can be checked from the server node.")]
        bool m_KeyboardControl = true;

        /// <summary>Where tracks go. Every clip in here, by name, is the playlist.</summary>
        public const string MusicFolder = MusicDeck.MusicFolder;

        // The surface shows first, then the volume's: the order the menu's tables, Shift+M and the auto-cycle go in.
        static readonly VisualizerMode[] k_Modes =
        {
            VisualizerMode.Surface2D, VisualizerMode.Ripples, VisualizerMode.LavaLamp, VisualizerMode.Underwater,
            VisualizerMode.Room3D, VisualizerMode.PointCloud, VisualizerMode.AstralBody, VisualizerMode.Fireflies, VisualizerMode.Tree,
        };

        // One blend per layer, 0 … 1. A layer eases toward 1 while its mode is on and toward 0 otherwise.
        float m_BlendTubes;
        float m_BlendRipples;
        float m_BlendLava;
        float m_BlendSea;
        float m_BlendTree;
        float m_BlendPair;                            // Musica Universalis and the tree both on: each eases to its place beside the other
        Vector3 m_BodyHome;
        float m_BlendLasers;
        float m_BlendCloud;
        float m_BlendBody;
        float m_BlendFlies;
        float m_BlendMenu;

        int m_On;                                     // the modes on, a bit each; m_Mode is always one of them
        float m_CycleTimer;
        MusicDeck m_Watched;
        readonly StringBuilder m_Text = new();

        /// <summary>The mode on, or, with several layered, the one turned on last.</summary>
        public VisualizerMode Mode => m_Mode;

        /// <summary>Whether <paramref name="mode"/> is on, alone or layered with others.</summary>
        public bool IsOn(VisualizerMode mode) => (m_On & Bit(mode)) != 0;

        /// <summary>More than one mode on.</summary>
        public bool Layered => m_On != Bit(m_Mode);

        public bool LowIntensity => m_LowIntensity;
        public float Intensity => m_Intensity;
        public MusicDeck Deck => m_Deck;

        /// <summary>The modes, in the order the menu lists them.</summary>
        public static IReadOnlyList<VisualizerMode> Modes => k_Modes;

        /// <summary>
        /// Whether <paramref name="mode"/> is drawn on the screens themselves — the same for everyone in
        /// the room, glasses or not — rather than standing in the volume, right only from the tracked
        /// viewpoint. The menu lists the two kinds in separate tables.
        /// </summary>
        public static bool IsSurface(VisualizerMode mode) =>
            mode == VisualizerMode.Surface2D || mode == VisualizerMode.Ripples || mode == VisualizerMode.LavaLamp ||
            mode == VisualizerMode.Underwater;

        /// <summary>Whether this scene has the layer <paramref name="mode"/> shows. One built before the mode existed does not.</summary>
        public bool Has(VisualizerMode mode)
        {
            switch (mode)
            {
                case VisualizerMode.Ripples: return m_Ripples != null;
                case VisualizerMode.LavaLamp: return m_LavaLamp != null;
                case VisualizerMode.Underwater: return m_Underwater != null;
                case VisualizerMode.Tree: return m_Tree != null;
                case VisualizerMode.Room3D: return m_Lasers != null || (m_Ribbons != null && m_Ribbons.Length > 0);
                case VisualizerMode.PointCloud: return m_PointCloud != null;
                case VisualizerMode.AstralBody: return m_AstralBody != null;
                case VisualizerMode.Fireflies: return m_Fireflies != null;
                default: return m_Panels != null && m_Panels.Length > 0;
            }
        }

#if UNITY_EDITOR
        /// <summary>Every audio clip in <see cref="MusicFolder"/>, sorted by name. See <see cref="MusicDeck.ClipsInMusicFolder"/>.</summary>
        public static AudioClip[] ClipsInMusicFolder() => MusicDeck.ClipsInMusicFolder();
#endif

        void Awake()
        {
            m_On = Bit(m_Mode);
            if (m_AstralBody != null)
                m_BodyHome = m_AstralBody.transform.localPosition;

            // Before the deck's Start, which plays or cues the first track.
            if (m_StartPaused && m_Deck != null)
                m_Deck.PlayOnStart = false;
        }

        void OnEnable()
        {
            m_Watched = m_Deck;
            if (m_Watched != null)
                m_Watched.Changed += UpdateSign;
        }

        void OnDisable()
        {
            if (m_Watched != null)
                m_Watched.Changed -= UpdateSign;
            m_Watched = null;
        }

        void Start()
        {
            if (m_Deck == null)
                StandInDeck();

            if (m_Menu == null)
                Debug.LogWarning("[Visualizer] This scene was built before the visualizer had its menu, so the mode and track " +
                                 "buttons step and skip instead of opening it, and there is no now-playing strip. Open the scene, " +
                                 "run VR-Music-Demo ▸ Visualizer ▸ Rebuild Visualizer, and save it.", this);

            if (m_Ripples == null || m_LavaLamp == null || m_Underwater == null || m_Tree == null)
                Debug.LogWarning("[Visualizer] This scene was built before the visualizer had all of its rainbow ripples, lava " +
                                 "lamp, underwater and tree, so the missing modes show nothing (the menu says so). Open the scene, " +
                                 "run VR-Music-Demo ▸ Visualizer ▸ Rebuild Visualizer, and save it.", this);

            // Start on the chosen mode with no fade-in.
            m_BlendTubes = IsOn(VisualizerMode.Surface2D) ? 1f : 0f;
            m_BlendRipples = IsOn(VisualizerMode.Ripples) ? 1f : 0f;
            m_BlendLava = IsOn(VisualizerMode.LavaLamp) ? 1f : 0f;
            m_BlendSea = IsOn(VisualizerMode.Underwater) ? 1f : 0f;
            m_BlendTree = IsOn(VisualizerMode.Tree) ? 1f : 0f;
            m_BlendPair = Paired ? 1f : 0f;
            m_BlendLasers = IsOn(VisualizerMode.Room3D) ? 1f : 0f;
            m_BlendCloud = IsOn(VisualizerMode.PointCloud) ? 1f : 0f;
            m_BlendBody = IsOn(VisualizerMode.AstralBody) ? 1f : 0f;
            m_BlendFlies = IsOn(VisualizerMode.Fireflies) ? 1f : 0f;
            ApplyLayers();
            UpdateSign();
        }

        /// <summary>
        /// A scene built before the visualizer played through a <see cref="MusicDeck"/> still has its
        /// music source — the one the analyser listens to — but no deck. Rather than fall silent until
        /// someone rebuilds it, it gets a deck on that source for this run, which plays the music folder
        /// as the built one would.
        /// </summary>
        void StandInDeck()
        {
            var source = m_Analyser != null ? m_Analyser.Source : null;
            if (source == null && m_Analyser != null)
                source = m_Analyser.GetComponentInChildren<AudioSource>();
            if (source == null)
                return;

            m_Deck = source.GetComponent<MusicDeck>();
            if (m_Deck == null)
                m_Deck = source.gameObject.AddComponent<MusicDeck>();
            m_Deck.Source = source;
            if (m_StartPaused)
                m_Deck.PlayOnStart = false;            // a deck added just now has not started yet

            // OnEnable ran before there was a deck to listen to.
            m_Watched = m_Deck;
            m_Watched.Changed += UpdateSign;
        }

        void Update()
        {
            ReadWand();
            if (m_KeyboardControl)
                ReadKeyboard();

            AutoCycle();
            Fade();
            ApplyLayers();
        }

        // ------------------------------------------------------------------ mode

        /// <summary>Switches to <paramref name="mode"/> alone, cross-fading out whatever else was on.</summary>
        public void SetMode(VisualizerMode mode)
        {
            if (m_Mode == mode && !Layered)
                return;

            m_Mode = mode;
            m_On = Bit(mode);
            m_CycleTimer = 0f;
            UpdateSign();
        }

        /// <summary>
        /// Turns <paramref name="mode"/> on alongside whatever is on, or off leaving the rest — the
        /// menu's double pick. The last mode on stays on, so the room always has a show. Returns
        /// whether it is on now.
        /// </summary>
        public bool ToggleMode(VisualizerMode mode)
        {
            if (!IsOn(mode))
            {
                m_On |= Bit(mode);
                m_Mode = mode;
            }
            else if (Layered)
            {
                m_On &= ~Bit(mode);
                if (m_Mode == mode)
                    m_Mode = System.Array.Find(k_Modes, IsOn);
            }

            m_CycleTimer = 0f;
            UpdateSign();
            return IsOn(mode);
        }

        static int Bit(VisualizerMode mode) => 1 << (int)mode;

        /// <summary>Musica Universalis and the tree both on, layered: the one pair of shows that would otherwise run into each other.</summary>
        bool Paired => IsOn(VisualizerMode.AstralBody) && IsOn(VisualizerMode.Tree);

        public void NextMode() => StepMode(1);
        public void PreviousMode() => StepMode(-1);

        void StepMode(int step)
        {
            int index = System.Array.IndexOf(k_Modes, m_Mode);
            if (index < 0)
                index = 0;

            int next = ((index + step) % k_Modes.Length + k_Modes.Length) % k_Modes.Length;
            SetMode(k_Modes[next]);
        }

        public void SetLowIntensity(bool low)
        {
            m_LowIntensity = low;
            UpdateSign();
        }

        void AutoCycle()
        {
            // Nothing changes under someone choosing from the menu, and a mix layered by hand is left as it is.
            if (m_AutoCycleSeconds <= 0f || (m_Menu != null && m_Menu.IsOpen) || Layered)
                return;

            m_CycleTimer += Time.deltaTime;
            if (m_CycleTimer >= m_AutoCycleSeconds)
                NextMode();
        }

        void Fade()
        {
            float step = m_FadeSeconds > 0f ? Time.deltaTime / m_FadeSeconds : 1f;
            m_BlendTubes = Mathf.MoveTowards(m_BlendTubes, IsOn(VisualizerMode.Surface2D) ? 1f : 0f, step);
            m_BlendRipples = Mathf.MoveTowards(m_BlendRipples, IsOn(VisualizerMode.Ripples) ? 1f : 0f, step);
            m_BlendLava = Mathf.MoveTowards(m_BlendLava, IsOn(VisualizerMode.LavaLamp) ? 1f : 0f, step);
            m_BlendSea = Mathf.MoveTowards(m_BlendSea, IsOn(VisualizerMode.Underwater) ? 1f : 0f, step);
            m_BlendTree = Mathf.MoveTowards(m_BlendTree, IsOn(VisualizerMode.Tree) ? 1f : 0f, step);
            m_BlendPair = Mathf.MoveTowards(m_BlendPair, Paired ? 1f : 0f, step);
            m_BlendLasers = Mathf.MoveTowards(m_BlendLasers, IsOn(VisualizerMode.Room3D) ? 1f : 0f, step);
            m_BlendCloud = Mathf.MoveTowards(m_BlendCloud, IsOn(VisualizerMode.PointCloud) ? 1f : 0f, step);
            m_BlendBody = Mathf.MoveTowards(m_BlendBody, IsOn(VisualizerMode.AstralBody) ? 1f : 0f, step);
            m_BlendFlies = Mathf.MoveTowards(m_BlendFlies, IsOn(VisualizerMode.Fireflies) ? 1f : 0f, step);

            bool menu = m_Menu != null && m_Menu.IsOpen;
            m_BlendMenu = Mathf.MoveTowards(m_BlendMenu, menu ? 1f : 0f, Time.deltaTime / 0.25f);
        }

        /// <summary>
        /// Pushes each layer's blend and the intensity into it, and switches a volume layer's
        /// objects off entirely once it has faded out so an idle layer costs nothing. The volume's
        /// layers also dim while the menu is open.
        /// </summary>
        void ApplyLayers()
        {
            float intensity = m_Intensity * (m_LowIntensity ? m_LowIntensityScale : 1f);
            float volume = 1f - m_MenuDim * Mathf.SmoothStep(0f, 1f, m_BlendMenu);

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

            // On the screens like the water, so it stays bright while the menu is open; off once faded out.
            if (m_Ripples != null)
            {
                m_Ripples.Opacity = Mathf.SmoothStep(0f, 1f, m_BlendRipples);
                m_Ripples.Intensity = intensity;
                m_Ripples.BeatFlashEnabled = !m_LowIntensity;
                Show(m_Ripples.gameObject, m_BlendRipples > 0f);
            }

            if (m_LavaLamp != null)
            {
                m_LavaLamp.Opacity = Mathf.SmoothStep(0f, 1f, m_BlendLava);
                m_LavaLamp.Intensity = intensity;
                m_LavaLamp.BeatFlashEnabled = !m_LowIntensity;
                Show(m_LavaLamp.gameObject, m_BlendLava > 0f);
            }

            if (m_Underwater != null)
            {
                m_Underwater.Opacity = Mathf.SmoothStep(0f, 1f, m_BlendSea);
                m_Underwater.Intensity = intensity;
                m_Underwater.BeatFlashEnabled = !m_LowIntensity;
                Show(m_Underwater.gameObject, m_BlendSea > 0f);
            }

            // In the volume like the clouds: dims while the menu is open.
            if (m_Tree != null)
            {
                m_Tree.Opacity = Mathf.SmoothStep(0f, 1f, m_BlendTree) * volume;
                m_Tree.Intensity = intensity;
                Show(m_Tree.gameObject, m_BlendTree > 0f);
            }

            // Musica Universalis and the tree on together would run into each other, so each moves aside,
            // as it fades in, and back once the other has gone: the astral body up, and the tree splits in
            // two that part to either side of it, mirror images, for symmetry.
            float pair = Mathf.SmoothStep(0f, 1f, m_BlendPair);
            if (m_AstralBody != null)
                m_AstralBody.transform.localPosition = m_BodyHome + m_BodyBesideTree * pair;
            if (m_Tree != null)
            {
                m_Tree.Shift = m_TreeBesideBody * pair;
                m_Tree.Twin = pair;
            }

            float lasers = Mathf.SmoothStep(0f, 1f, m_BlendLasers);
            bool showLasers = m_BlendLasers > 0f;

            if (m_Lasers != null)
            {
                m_Lasers.Opacity = lasers * volume;
                m_Lasers.Intensity = intensity;
                Show(m_Lasers.gameObject, showLasers);
            }

            if (m_Ribbons != null)
            {
                foreach (var ribbon in m_Ribbons)
                {
                    if (ribbon == null)
                        continue;

                    ribbon.Opacity = lasers * volume;
                    ribbon.Intensity = intensity;
                    Show(ribbon.gameObject, showLasers);
                }
            }

            ApplyCloud(m_PointCloud, m_BlendCloud, intensity, volume);
            ApplyCloud(m_AstralBody, m_BlendBody, intensity, volume);
            ApplyCloud(m_Fireflies, m_BlendFlies, intensity, volume);
        }

        static void ApplyCloud(AudioPointCloud cloud, float blend, float intensity, float volume)
        {
            if (cloud == null)
                return;

            cloud.Opacity = Mathf.SmoothStep(0f, 1f, blend) * volume;
            cloud.Intensity = intensity;
            Show(cloud.gameObject, blend > 0f);
        }

        static void Show(GameObject go, bool on)
        {
            if (go.activeSelf != on)
                go.SetActive(on);
        }

        // ------------------------------------------------------------------ input

        /// <summary>The mode button opens the modes on the front wall, or closes them; with no menu it steps to the next mode.</summary>
        void ModeButton()
        {
            if (m_Menu != null)
                m_Menu.ToggleModes();
            else
                NextMode();
        }

        /// <summary>The track button opens the tracks on the front wall, or closes them; with no menu it skips to the next track.</summary>
        void TrackButton()
        {
            if (m_Menu != null)
                m_Menu.ToggleTracks();
            else if (m_Deck != null)
                m_Deck.NextTrack();
        }

        void ReadWand()
        {
            if (m_ModeButton >= 0 && MiddleVRWand.PressedThisFrame(m_ModeButton))
                ModeButton();

            if (m_IntensityButton >= 0 && MiddleVRWand.PressedThisFrame(m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);

            if (m_NextTrackButton >= 0 && MiddleVRWand.PressedThisFrame(m_NextTrackButton))
                TrackButton();

            if (m_PlayPauseButton >= 0 && MiddleVRWand.PressedThisFrame(m_PlayPauseButton) && m_Deck != null)
                m_Deck.TogglePause();
        }

        void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            // Shift skips the menu: straight to the next mode, straight to the next track.
            if (keyboard.mKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                    NextMode();
                else
                    ModeButton();
            }
            else if (DigitPressed(keyboard, m_ModeButton))
            {
                ModeButton();
            }

            if (keyboard.nKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                {
                    if (m_Deck != null)
                        m_Deck.NextTrack();
                }
                else
                {
                    TrackButton();
                }
            }
            else if (DigitPressed(keyboard, m_NextTrackButton))
            {
                TrackButton();
            }

            if (keyboard.lKey.wasPressedThisFrame || DigitPressed(keyboard, m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);

            if ((keyboard.spaceKey.wasPressedThisFrame || DigitPressed(keyboard, m_PlayPauseButton)) && m_Deck != null)
                m_Deck.TogglePause();
        }

        /// <summary>The number-row key printed with the same digit as a wand button, the way the other demos do it.</summary>
        static bool DigitPressed(Keyboard keyboard, int button)
        {
            if (button < 0 || button > 9)
                return false;

            var key = button == 0 ? Key.Digit0 : Key.Digit1 + (button - 1);
            return keyboard[key].wasPressedThisFrame;
        }

        // ------------------------------------------------------------------ names

        /// <summary>A mode's name, as the menu lists it.</summary>
        public static string TitleOf(VisualizerMode mode)
        {
            switch (mode)
            {
                case VisualizerMode.Ripples: return "Rainbow Ripples";
                case VisualizerMode.LavaLamp: return "Lava Lamp";
                case VisualizerMode.Underwater: return "Underwater";
                case VisualizerMode.Tree: return "Holographic Tree";
                case VisualizerMode.Room3D: return "Lasers & Waveforms";
                case VisualizerMode.PointCloud: return "Point Cloud";
                case VisualizerMode.AstralBody: return "Musica Universalis";
                case VisualizerMode.Fireflies: return "Fireflies";
                default: return "Water Tubes";
            }
        }

        /// <summary>What a mode shows, in a few words, for the line under its name in the menu. Its table says whether it is 2D or 3D.</summary>
        public static string KindOf(VisualizerMode mode)
        {
            switch (mode)
            {
                case VisualizerMode.Ripples: return "rainbow rings on every surface";
                case VisualizerMode.LavaLamp: return "wax rising on the heat of the bass";
                case VisualizerMode.Underwater: return "sunlit waves, bubbles on the beat";
                case VisualizerMode.Tree: return "sways, and drops leaves on the beat";
                case VisualizerMode.Room3D: return "beams from wall to wall";
                case VisualizerMode.PointCloud: return "the song flowing past";
                case VisualizerMode.AstralBody: return "a sun and its spheres";
                case VisualizerMode.Fireflies: return "a swarm lit by the music";
                default: return "water rising in glass tubes";
            }
        }

        // ------------------------------------------------------------------ sign

        static string SignNameOf(VisualizerMode mode)
        {
            switch (mode)
            {
                case VisualizerMode.Ripples: return "RIPPLES";
                case VisualizerMode.LavaLamp: return "LAVA LAMP";
                case VisualizerMode.Underwater: return "UNDERWATER";
                case VisualizerMode.Tree: return "TREE";
                case VisualizerMode.Room3D: return "LASERS";
                case VisualizerMode.PointCloud: return "POINT CLOUD";
                case VisualizerMode.AstralBody: return "MUSICA UNIVERSALIS";
                case VisualizerMode.Fireflies: return "FIREFLIES";
                default: return "WATER";
            }
        }

        void UpdateSign()
        {
            if (m_Sign == null)
                return;

            m_Text.Clear();
            if (!Layered)
            {
                m_Text.Append(IsSurface(m_Mode) ? "2D  " : "3D  ").Append(SignNameOf(m_Mode));
            }
            else
            {
                // A mix reads as its parts: "WATER + LASERS + FIREFLIES".
                bool first = true;
                foreach (var mode in k_Modes)
                {
                    if (!IsOn(mode))
                        continue;
                    if (!first)
                        m_Text.Append(" + ");
                    m_Text.Append(SignNameOf(mode));
                    first = false;
                }
            }

            if (m_LowIntensity)
                m_Text.Append("   ·   LOW INTENSITY");

            m_Text.Append("   ·   ");
            var clip = m_Deck != null ? m_Deck.Clip : null;
            if (m_Deck == null)
            {
                m_Text.Append("no music source — rebuild the scene: VR-Music-Demo ▸ Visualizer ▸ Rebuild Visualizer");
            }
            else if (clip == null)
            {
                m_Text.Append("no track — drop a clip in ").Append(MusicFolder).Append(" and press Play again");
            }
            else
            {
                if (m_Deck.TrackCount > 1)
                    m_Text.Append(m_Deck.Track + 1).Append('/').Append(m_Deck.TrackCount).Append("  ");

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
    }
}

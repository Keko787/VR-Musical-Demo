using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The glass room's director: which world is outside the glass, the music handed to every outside
    /// shader, the change from one world to the next, the room's light, and the controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The music</b> goes out once a frame as shader globals — the bands, bass, mids, treble and
    /// loudness, the beat, beat rings, and a twenty-second spectrogram — so every surface outside
    /// reads the same numbers and nothing outside needs a reference to anything in the room. The
    /// bands are eased a little beyond the analyser's own envelope: the outside is landscape, and it
    /// should move like weather rather than like a meter.
    /// </para>
    /// <para>
    /// <b>Changing worlds</b> is the simulation rebuilding itself: the view breaks into tiles that
    /// shrink away into a blank page in a wave round the room, the old world goes to sleep and the
    /// next wakes while nothing outside is visible, and the new view builds back out of tiles behind
    /// the wave. The room itself never changes; its light follows whatever is outside.
    /// </para>
    /// <para>
    /// Every flash outside rides the analyser's beat, capped at three a second (two in low
    /// intensity), and low intensity also dims every glow and the blank page between worlds.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-80)] // after the analyser, before the windows and everything outside
    public class Simulation : MonoBehaviour
    {
        const int k_MaxBands = 32;

        [Header("Audio")]
        [SerializeField] AudioAnalyser m_Analyser;
        [SerializeField] MusicDeck m_Deck;

        [Header("Worlds")]
        [SerializeField, Tooltip("The worlds outside the glass, in the order the wand steps through them.")]
        SimWorld[] m_Worlds;

        [SerializeField] int m_StartWorld;

        [SerializeField, Tooltip("Rebuild the outside into the next world whenever a new track starts. Off: the world stays " +
            "through every change of track, until someone picks another.")]
        bool m_WorldPerTrack;

        [SerializeField, Tooltip("Seconds between world changes with nobody touching anything. 0 is off.")]
        float m_AutoCycleSeconds;

        [SerializeField, Tooltip("Seconds for a world to dissolve into the blank page and the next to build out of it.")]
        float m_TransitionSeconds = 3.2f;

        [SerializeField, Tooltip("Seconds the glitch between a world's variants lasts; the look swaps half way.")]
        float m_GlitchSeconds = 1.4f;

        [SerializeField, Tooltip("Seconds between variant changes in a world that has them, with nobody touching anything. 0 is off.")]
        float m_VariantEverySeconds;

        [SerializeField, Tooltip("Where the worlds are: the glass room's place outside. The window cameras stand here.")]
        Transform m_Outside;

        [Header("Windows")]
        [SerializeField] WindowView[] m_Windows;

        [SerializeField, Tooltip("What the glass shows between worlds: the simulation's blank page.")]
        Color m_BlankPage = new(0.8f, 0.88f, 0.97f);

        [Header("Room")]
        [SerializeField, Tooltip("The scene's directional light. Each world sets its colour, strength and angle, so the room is lit by what is outside.")]
        Light m_Sun;

        [SerializeField] PendantLamp[] m_Lamps;

        [SerializeField, Tooltip("Seconds for the room's light to settle to a new world's.")]
        float m_LightBlend = 1.5f;

        [Header("Music")]
        [SerializeField, Tooltip("Seconds for a band outside to rise to a louder level.")]
        float m_Attack = 0.05f;

        [SerializeField, Tooltip("Seconds for it to fall back.")]
        float m_Release = 0.4f;

        [SerializeField, Tooltip("Slices a second written into the spectrogram the cloud sea's swells are made of.")]
        float m_SpectrogramRate = 12f;

        [SerializeField, Range(32, 512), Tooltip("Slices kept. 240 at 12 a second is twenty seconds of music on the sea.")]
        int m_SpectrogramSlices = 240;

        [Header("Intensity")]
        [SerializeField, Tooltip("The photosensitivity setting: every glow outside dims, beat rings halve, and the beat rate is capped lower. On for a school group.")]
        bool m_LowIntensity;

        [SerializeField, Range(0.1f, 1f)] float m_LowIntensityScale = 0.55f;
        [SerializeField] float m_MaxFlashesPerSecond = 3f;
        [SerializeField] float m_LowIntensityFlashesPerSecond = 2f;

        [Header("Furniture")]
        [SerializeField, Tooltip("The room's furniture: the desk with its tablet and teddy bear, the cabinet, the bed, the rug and the pendant " +
            "lamps. Off leaves the bare glass box — the iron frame, the glass and the floor. Kept through a rebuild; F toggles it in play mode.")]
        bool m_Furniture = true;

        [SerializeField, Tooltip("What the furniture switch shows and hides. Filled in by the builder.")]
        GameObject[] m_FurniturePieces;

        [Header("Wand")]
        [SerializeField, Tooltip("Wand button that opens the world menu (or, without one, changes the world outside). Indices are whatever the .vrx says; " +
            "the VisBox wand has 0-4, 6-8, 11 and 15, and 0 is the trigger.")]
        int m_WorldButton = 1;

        [SerializeField, Tooltip("The menu on the front wall the world button opens. None, and the button steps to the next world instead.")]
        WorldMenu m_Menu;

        [SerializeField] int m_IntensityButton = 2;
        [SerializeField] int m_NextTrackButton = 3;
        [SerializeField] int m_PlayPauseButton = 4;

        [SerializeField, Tooltip("Wand button that glitches the world into its next variant, in a world that has them.")]
        int m_VariantButton = 6;

        [SerializeField, Tooltip("Wand button that shows or hides the furniture. -1 for none.")]
        int m_FurnitureButton = -1;

        [Header("Desk")]
        [SerializeField, Tooltip("W opens the world menu (Shift+W goes back a world), V the world's variant (Shift+V goes back), F shows or hides the furniture, L toggles low intensity, N skips, Space pauses, and the number-row keys " +
            "stand in for the wand buttons with the same digit. Stays on in builds so the room can be run from the server node.")]
        bool m_KeyboardControl = true;

        [SerializeField, Tooltip("A dim line on the floor at the open back of the room, for whoever is running the demo.")]
        TMP_Text m_Sign;

        // ---------------------------------------------------------------- outputs

        public AudioAnalyser Analyser => m_Analyser;
        public MusicDeck Deck => m_Deck;
        public SimWorld World => m_Current >= 0 && m_Worlds != null && m_Current < m_Worlds.Length ? m_Worlds[m_Current] : null;
        public int WorldIndex => m_Current;
        public int WorldCount => m_Worlds != null ? m_Worlds.Length : 0;
        public SimWorld WorldAt(int index) => m_Worlds != null && index >= 0 && index < m_Worlds.Length ? m_Worlds[index] : null;

        /// <summary>0 when a world is simply outside; 0 → 1 while it dissolves, 1 → 2 while the next builds.</summary>
        public float Transition => m_Dissolve;

        public bool LowIntensity => m_LowIntensity;

        /// <summary>1, or the low-intensity scale: what every glow outside is multiplied by.</summary>
        public float Intensity => m_LowIntensity ? m_LowIntensityScale : 1f;

        public float Bass { get; private set; }
        public float Mid { get; private set; }
        public float High { get; private set; }
        public float Loudness { get; private set; }
        public bool Beat { get; private set; }
        public float BeatPulse { get; private set; }
        public float BeatStrength { get; private set; }
        public float Idle { get; private set; } = 1f;
        public float SinceBeat => Time.time - m_LastBeat;

        /// <summary>One band's eased level, 0 for the lowest band and 1 for the highest, interpolated.</summary>
        public float Band(float fraction)
        {
            if (m_BandCount <= 0)
                return 0f;

            float i = Mathf.Clamp01(fraction) * (m_BandCount - 1);
            int lo = Mathf.FloorToInt(i);
            int hi = Mathf.Min(lo + 1, m_BandCount - 1);
            return Mathf.Lerp(m_Bands[lo], m_Bands[hi], i - lo);
        }

        // ---------------------------------------------------------------- state

        static readonly int k_Bands = Shader.PropertyToID("_GR_Bands");
        static readonly int k_BandCount = Shader.PropertyToID("_GR_BandCount");
        static readonly int k_Music = Shader.PropertyToID("_GR_Music");
        static readonly int k_Beat = Shader.PropertyToID("_GR_Beat");
        static readonly int k_Intensity = Shader.PropertyToID("_GR_Intensity");
        static readonly int k_Clock = Shader.PropertyToID("_GR_Clock");
        static readonly int k_Origin = Shader.PropertyToID("_GR_Origin");
        static readonly int k_RingTimes = Shader.PropertyToID("_GR_RingTimes");
        static readonly int k_RingPower = Shader.PropertyToID("_GR_RingPower");
        static readonly int k_Spectrogram = Shader.PropertyToID("_GR_Spectrogram");
        static readonly int k_Spec = Shader.PropertyToID("_GR_Spec");

        readonly float[] m_Bands = new float[k_MaxBands];
        readonly StringBuilder m_Text = new();
        int m_BandCount;
        float m_LastBeat = -999f;

        Texture2D m_Spectrogram;
        ushort[] m_SpecPixels;
        int m_SpecHead;
        float m_SpecPhase;

        Vector4 m_RingTimes = new(-999f, -999f, -999f, -999f);
        Vector4 m_RingPower;
        int m_RingNext;

        int m_Current = -1;
        int m_Next = -1;
        float m_Dissolve;
        bool m_Transiting;
        float m_WorldSince;
        int m_TracksStarted;

        bool m_Glitching;
        float m_GlitchTime;
        float m_GlitchSeed;
        int m_PendingVariant;
        bool m_VariantSwapped;
        float m_VariantSince;

        Color m_Ambient;
        Color m_SunColour;
        float m_SunIntensity;
        float m_LampLevel;
        bool m_LightSet;

        // ---------------------------------------------------------------- lifecycle

        void OnEnable()
        {
            if (m_Deck != null)
            {
                m_Deck.TrackStarted += OnTrackStarted;
                m_Deck.Changed += UpdateSign;
            }
        }

        void OnDisable()
        {
            if (m_Deck != null)
            {
                m_Deck.TrackStarted -= OnTrackStarted;
                m_Deck.Changed -= UpdateSign;
            }
        }

        void Awake()
        {
            CreateSpectrogram();
            ApplyFurniture();

            // Wake every world once, so each generates its land now, at load, rather than in the
            // middle of the first change to it; then put them all to sleep.
            if (m_Worlds != null)
            {
                foreach (var world in m_Worlds)
                    if (world != null)
                        world.gameObject.SetActive(true);
                foreach (var world in m_Worlds)
                    if (world != null)
                        world.gameObject.SetActive(false);
            }
        }

        void Start()
        {
            // The first world builds out of the blank page.
            m_Dissolve = 1f;
            m_Transiting = WorldCount > 0;
            m_Next = WorldCount > 0 ? Mathf.Clamp(m_StartWorld, 0, WorldCount - 1) : -1;
            PushGlobals();
            UpdateSign();
        }

        void OnDestroy()
        {
            if (m_Spectrogram != null)
                Destroy(m_Spectrogram);
        }

        void OnTrackStarted(AudioClip clip)
        {
            // The first track starts as the scene opens; only a change of track changes the world, and only when asked to.
            m_TracksStarted++;
            if (m_TracksStarted > 1 && m_WorldPerTrack)
                NextWorld();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            ReadInput();

            if (m_Analyser != null)
                m_Analyser.MaxBeatsPerSecond = m_LowIntensity ? m_LowIntensityFlashesPerSecond : m_MaxFlashesPerSecond;

            ReadMusic(dt);
            WriteSpectrogram(dt);

            if (Beat)
                SetOffRing();

            if (m_AutoCycleSeconds > 0f && !m_Transiting && Time.time - m_WorldSince > m_AutoCycleSeconds)
                NextWorld();

            if (m_VariantEverySeconds > 0f && !m_Transiting && !m_Glitching && World != null && World.VariantCount > 1 &&
                Time.time - m_VariantSince > m_VariantEverySeconds)
                NextVariant();

            UpdateTransition(dt);
            UpdateGlitch(dt);
            PushGlobals();
            LightRoom(dt);
            DriveWindows();
        }

        // ---------------------------------------------------------------- the music

        void ReadMusic(float dt)
        {
            if (m_Analyser == null)
            {
                m_BandCount = 0;
                Beat = false;
                return;
            }

            var bands = m_Analyser.Bands;
            m_BandCount = Mathf.Min(bands.Length, k_MaxBands);
            for (int i = 0; i < m_BandCount; i++)
                m_Bands[i] = Ease(m_Bands[i], bands[i], dt);

            Bass = Ease(Bass, m_Analyser.Bass, dt);
            Mid = Ease(Mid, m_Analyser.Mid, dt);
            High = Ease(High, m_Analyser.High, dt);
            Loudness = Ease(Loudness, m_Analyser.Loudness, dt);
            Idle = m_Analyser.Idle;

            Beat = m_Analyser.Beat;
            BeatPulse = m_Analyser.BeatPulse;
            BeatStrength = m_Analyser.BeatStrength;
            if (Beat)
                m_LastBeat = Time.time;
        }

        float Ease(float current, float target, float dt)
        {
            float seconds = target > current ? m_Attack : m_Release;
            return current + (target - current) * (1f - Mathf.Exp(-dt / Mathf.Max(0.001f, seconds)));
        }

        void CreateSpectrogram()
        {
            int slices = Mathf.Clamp(m_SpectrogramSlices, 32, 512);
            m_Spectrogram = new Texture2D(k_MaxBands, slices, TextureFormat.RHalf, false, true)
            {
                name = "Glass_Room_Spectrogram",
                hideFlags = HideFlags.DontSave,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            m_SpecPixels = new ushort[k_MaxBands * slices];
            m_Spectrogram.SetPixelData(m_SpecPixels, 0);
            m_Spectrogram.Apply(false, false);
        }

        /// <summary>
        /// The spectrogram is a ring of slices written at a fixed rate. The slice after the newest
        /// always holds the music as it is this frame, so the swells leave the room continuously
        /// instead of a slice at a time; when the clock ticks it becomes the newest.
        /// </summary>
        void WriteSpectrogram(float dt)
        {
            if (m_Spectrogram == null)
                return;

            int slices = m_Spectrogram.height;
            m_SpecPhase += dt * Mathf.Max(0.1f, m_SpectrogramRate);
            while (m_SpecPhase >= 1f)
            {
                m_SpecPhase -= 1f;
                m_SpecHead = (m_SpecHead + 1) % slices;
            }

            int offset = (m_SpecHead + 1) % slices * k_MaxBands;
            for (int i = 0; i < k_MaxBands; i++)
                m_SpecPixels[offset + i] = Mathf.FloatToHalf(i < m_BandCount ? m_Bands[i] : 0f);

            m_Spectrogram.SetPixelData(m_SpecPixels, 0);
            m_Spectrogram.Apply(false, false);
        }

        void SetOffRing()
        {
            var world = World;
            if (world == null || world.RingOrigin == null)
                return;

            m_RingTimes[m_RingNext] = Time.time + world.RingDelay;
            m_RingPower[m_RingNext] = (0.35f + 0.65f * BeatStrength) * (m_LowIntensity ? 0.5f : 1f);
            m_RingNext = (m_RingNext + 1) & 3;
        }

        void PushGlobals()
        {
            Shader.SetGlobalFloatArray(k_Bands, m_Bands);
            Shader.SetGlobalFloat(k_BandCount, Mathf.Max(1, m_BandCount));
            Shader.SetGlobalVector(k_Music, new Vector4(Bass, Mid, High, Loudness));
            Shader.SetGlobalVector(k_Beat, new Vector4(BeatPulse, BeatStrength, SinceBeat, Idle));
            Shader.SetGlobalFloat(k_Intensity, Intensity);
            Shader.SetGlobalFloat(k_Clock, Time.time);
            Shader.SetGlobalVector(k_Origin, m_Outside != null ? m_Outside.position : transform.position);
            Shader.SetGlobalVector(k_RingTimes, m_RingTimes);
            Shader.SetGlobalVector(k_RingPower, m_RingPower);

            var world = World;
            var waves = world != null ? world.Waves : new Vector2(45f, 20f);
            if (m_Spectrogram != null)
            {
                int slices = m_Spectrogram.height;
                Shader.SetGlobalTexture(k_Spectrogram, m_Spectrogram);
                Shader.SetGlobalVector(k_Spec, new Vector4((m_SpecHead + 0.5f + m_SpecPhase) / slices,
                    slices / Mathf.Max(0.1f, m_SpectrogramRate), waves.x, waves.y));
            }

            // Every frame rather than only on waking, so a sky tuned in the Inspector shows at once.
            if (world != null)
                world.PushSky(Frame);
        }

        Quaternion Frame => m_Outside != null ? m_Outside.rotation : transform.rotation;

        // ---------------------------------------------------------------- worlds

        public void NextWorld() => ShowWorld(Target() + 1);
        public void PreviousWorld() => ShowWorld(Target() - 1);

        /// <summary>The world being shown or, mid-change, the one on its way.</summary>
        int Target() => m_Next >= 0 ? m_Next : Mathf.Max(0, m_Current);

        /// <summary>Dissolves the world outside and builds <paramref name="index"/> in its place.</summary>
        public void ShowWorld(int index)
        {
            int count = WorldCount;
            if (count == 0)
                return;

            index = ((index % count) + count) % count;

            if (!m_Transiting)
            {
                if (index == m_Current)
                    return;

                m_Transiting = true;
                m_Dissolve = 0.0001f;
            }
            else if (m_Dissolve > 1f)
            {
                // Already building the next world: turn round and dissolve again from where it is.
                m_Dissolve = 2f - m_Dissolve;
            }

            m_Next = index;
        }

        void UpdateTransition(float dt)
        {
            if (!m_Transiting)
                return;

            m_Dissolve += dt * 2f / Mathf.Max(0.2f, m_TransitionSeconds);

            if (m_Next >= 0 && m_Dissolve >= 1f)
            {
                SetWorld(m_Next);
                m_Next = -1;
                m_Dissolve = 1f;
            }
            else if (m_Next < 0 && m_Dissolve >= 2f)
            {
                m_Dissolve = 0f;
                m_Transiting = false;
            }
        }

        /// <summary>Puts the old world to sleep and wakes the new one, while the glass shows only the blank page.</summary>
        void SetWorld(int index)
        {
            if (World != null)
                World.gameObject.SetActive(false);

            m_Current = index;
            var world = World;
            if (world == null)
                return;

            world.gameObject.SetActive(true);
            world.PushSky(Frame);
            m_WorldSince = Time.time;
            m_RingPower = Vector4.zero;
            UpdateSign();

            if (!m_LightSet)
            {
                // The first world: the room starts in its light rather than easing in from the scene's.
                m_LightSet = true;
                m_Ambient = world.RoomAmbient;
                m_SunColour = world.RoomLight;
                m_SunIntensity = world.RoomLightIntensity;
                m_LampLevel = world.LampLevel;
                if (m_Sun != null)
                    m_Sun.transform.rotation = Frame * world.RoomLightRotation;
            }
        }

        // ---------------------------------------------------------------- variants

        public void NextVariant() => ShowVariant(VariantTarget() + 1);
        public void PreviousVariant() => ShowVariant(VariantTarget() - 1);

        /// <summary>The variant on show or, mid-glitch, the one on its way.</summary>
        int VariantTarget() => m_Glitching && !m_VariantSwapped ? m_PendingVariant : Mathf.Max(0, World != null ? World.VariantIndex : 0);

        /// <summary>
        /// Glitches the world outside into another of its looks. Only in a world that has variants,
        /// and not while the room is changing worlds; the look swaps half way through the glitch.
        /// </summary>
        public void ShowVariant(int index)
        {
            var world = World;
            if (world == null || world.VariantCount < 2 || m_Transiting)
                return;

            index = ((index % world.VariantCount) + world.VariantCount) % world.VariantCount;
            if (!m_Glitching && index == world.VariantIndex)
                return;

            m_PendingVariant = index;
            if (!m_Glitching || m_VariantSwapped)
            {
                m_Glitching = true;
                m_GlitchTime = 0f;
                m_VariantSwapped = false;
                m_GlitchSeed = Random.Range(0f, 100f);
            }
        }

        void UpdateGlitch(float dt)
        {
            if (!m_Glitching)
                return;

            m_GlitchTime += dt;
            float half = Mathf.Max(0.1f, m_GlitchSeconds) * 0.5f;

            if (!m_VariantSwapped && m_GlitchTime >= half)
            {
                m_VariantSwapped = true;
                World?.SetVariant(m_PendingVariant);
                World?.PushSky(Frame);
                m_VariantSince = Time.time;
                UpdateSign();
            }

            if (m_GlitchTime >= half * 2f)
            {
                m_Glitching = false;
                m_GlitchTime = 0f;
            }
        }

        /// <summary>How hard the glass is glitching, 0–1: up to its worst as the look swaps, then back. Gentler in low intensity.</summary>
        float GlitchAmount
        {
            get
            {
                if (!m_Glitching)
                    return 0f;

                float p = Mathf.Clamp01(m_GlitchTime / Mathf.Max(0.1f, m_GlitchSeconds));
                // Floored at 0, as a fractional power of a sine must be: at p = 1 the float sine of
                // Mathf.PI is a hair below zero, and the power of that is NaN.
                return Mathf.Pow(Mathf.Max(0f, Mathf.Sin(p * Mathf.PI)), 0.8f) * (m_LowIntensity ? 0.6f : 1f);
            }
        }

        // ---------------------------------------------------------------- the room

        /// <summary>
        /// Whether the room is furnished. Off leaves the bare glass box — the frame, the glass and the
        /// floor — with nothing between the visitors and the worlds outside; the hidden lamps take their
        /// light and their reflections in the glass with them.
        /// </summary>
        public bool Furniture
        {
            get => m_Furniture;
            set
            {
                m_Furniture = value;
                ApplyFurniture();
                UpdateSign();
            }
        }

        /// <summary>Shows or hides the furniture to match the switch. True if anything changed.</summary>
        public bool ApplyFurniture()
        {
            bool changed = false;
            if (m_FurniturePieces == null)
                return false;

            foreach (var piece in m_FurniturePieces)
            {
                if (piece == null || piece.activeSelf == m_Furniture)
                    continue;

                piece.SetActive(m_Furniture);
                changed = true;
            }

            return changed;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ticking the box in the Inspector shows or hides the furniture straight away, in or out of
        /// play mode. Deferred a frame: Unity does not allow objects to be switched on and off from
        /// inside OnValidate itself.
        /// </summary>
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !ApplyFurniture())
                    return;

                UpdateSign();
                if (!Application.isPlaying && gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            };
        }
#endif

        void LightRoom(float dt)
        {
            var world = World;
            if (world == null)
                return;

            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, m_LightBlend));
            m_Ambient = Color.Lerp(m_Ambient, world.RoomAmbient, k);
            m_SunColour = Color.Lerp(m_SunColour, world.RoomLight, k);
            m_SunIntensity = Mathf.Lerp(m_SunIntensity, world.RoomLightIntensity, k);
            m_LampLevel = Mathf.Lerp(m_LampLevel, world.LampLevel, k);

            RenderSettings.ambientLight = m_Ambient;

            if (m_Sun != null)
            {
                m_Sun.color = m_SunColour;
                m_Sun.intensity = m_SunIntensity;
                m_Sun.transform.rotation = Quaternion.Slerp(m_Sun.transform.rotation, Frame * world.RoomLightRotation, k);
            }

            if (m_Lamps != null)
                foreach (var lamp in m_Lamps)
                    if (lamp != null)
                        lamp.Level = m_LampLevel;
        }

        void DriveWindows()
        {
            if (m_Windows == null)
                return;

            var blank = m_BlankPage * (m_LowIntensity ? 0.7f : 1f);
            blank.a = 1f;

            foreach (var window in m_Windows)
            {
                if (window == null)
                    continue;

                window.SetLamps(m_Lamps);
                window.SetGlitch(GlitchAmount, m_GlitchSeed, !m_LowIntensity);
                window.SetTransition(m_Transiting ? m_Dissolve : 0f, blank);
            }
        }

        // ---------------------------------------------------------------- input

        public void SetLowIntensity(bool low)
        {
            m_LowIntensity = low;
            UpdateSign();
        }

        void ReadInput()
        {
            if (m_WorldButton >= 0 && MiddleVRWand.PressedThisFrame(m_WorldButton))
                WorldButton();
            if (m_IntensityButton >= 0 && MiddleVRWand.PressedThisFrame(m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);
            if (m_NextTrackButton >= 0 && MiddleVRWand.PressedThisFrame(m_NextTrackButton))
                m_Deck?.NextTrack();
            if (m_PlayPauseButton >= 0 && MiddleVRWand.PressedThisFrame(m_PlayPauseButton))
                m_Deck?.TogglePause();
            if (m_VariantButton >= 0 && MiddleVRWand.PressedThisFrame(m_VariantButton))
                NextVariant();
            if (m_FurnitureButton >= 0 && MiddleVRWand.PressedThisFrame(m_FurnitureButton))
                Furniture = !Furniture;

            var keyboard = m_KeyboardControl ? Keyboard.current : null;
            if (keyboard == null)
                return;

            if (keyboard.fKey.wasPressedThisFrame || DigitPressed(keyboard, m_FurnitureButton))
                Furniture = !Furniture;

            if (keyboard.wKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                    PreviousWorld();
                else
                    WorldButton();
            }
            else if (DigitPressed(keyboard, m_WorldButton))
            {
                WorldButton();
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                    PreviousVariant();
                else
                    NextVariant();
            }
            else if (DigitPressed(keyboard, m_VariantButton))
            {
                NextVariant();
            }

            if (keyboard.lKey.wasPressedThisFrame || DigitPressed(keyboard, m_IntensityButton))
                SetLowIntensity(!m_LowIntensity);
            if (keyboard.nKey.wasPressedThisFrame || DigitPressed(keyboard, m_NextTrackButton))
                m_Deck?.NextTrack();
            if (keyboard.spaceKey.wasPressedThisFrame || DigitPressed(keyboard, m_PlayPauseButton))
                m_Deck?.TogglePause();
        }

        /// <summary>The world button opens the menu on the front wall, or closes it; with no menu it steps to the next world.</summary>
        void WorldButton()
        {
            if (m_Menu != null)
                m_Menu.Toggle();
            else
                NextWorld();
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
            m_Text.Append("GLASS ROOM");

            var world = m_Next >= 0 && m_Next < WorldCount ? m_Worlds[m_Next] : World;
            if (world != null)
                m_Text.Append("   ·   ").Append(world.Group).Append(": ").Append(world.Title).Append(" (").Append(System.Array.IndexOf(m_Worlds, world) + 1)
                    .Append('/').Append(WorldCount).Append(')');

            if (world != null && world.VariantCount > 1)
                m_Text.Append(" — ").Append(world.VariantName).Append(" (").Append(world.VariantIndex + 1).Append('/')
                    .Append(world.VariantCount).Append(')');

            if (m_LowIntensity)
                m_Text.Append("   ·   LOW INTENSITY");
            if (!m_Furniture)
                m_Text.Append("   ·   NO FURNITURE");

            m_Text.Append("   ·   ");
            var clip = m_Deck != null ? m_Deck.Clip : null;
            if (clip == null)
                m_Text.Append("no track — drop a clip in ").Append(MusicDeck.MusicFolder);
            else
            {
                m_Text.Append(clip.name);
                if (m_Deck.Paused)
                    m_Text.Append("  (paused)");
            }

            m_Sign.text = m_Text.ToString();
        }
    }
}

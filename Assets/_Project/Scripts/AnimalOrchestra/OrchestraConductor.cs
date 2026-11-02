using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>A song ready to play: what the animals read, and the clips that sound it.</summary>
    public sealed class PreparedSong
    {
        public readonly SongData Data;

        /// <summary>One clip for a synthesised song (its mix); one per stem for a song folder.</summary>
        public readonly AudioClip[] Clips;

        public PreparedSong(SongData data, AudioClip[] clips)
        {
            Data = data;
            Clips = clips;
        }
    }

    /// <summary>
    /// The transport: plays the orchestra's songs one after another, unattended, and is the one clock
    /// every animal reads. Song time is the audio system's DSP time since the song's scheduled start,
    /// never frame time, so animation and sound cannot drift apart however the frame rate wanders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The playlist is every song folder under <c>StreamingAssets/AnimalOrchestra</c> (<see cref="SongFolder"/>)
    /// followed by the three built-in songs (<see cref="DemoSongs"/>), looping. Each song is prepared in
    /// the background while the one before it plays — a folder's stems loaded and measured, a built-in
    /// song synthesised — so the only wait is for the very first. Between songs, and while that first
    /// one renders, the room sits in its idle state (<see cref="Idleness"/>): the fireflies fill it and
    /// the animals breathe. That is also the attract mode.
    /// </para>
    /// <para>
    /// Every stem of a song is started with <see cref="AudioSource.PlayScheduled"/> at the same DSP time,
    /// so they are sample-aligned with one another. The DSP clock only advances once per audio buffer
    /// (about 21 ms here), so between buffers the song time is carried forward on the frame clock and
    /// pulled back to the DSP clock at each new buffer; it never runs backwards.
    /// </para>
    /// <para>
    /// The wand: <b>4</b> skips to the next song and <b>6</b> toggles low intensity mode (dimmer glows, a
    /// lower flash cap — the photosensitivity setting). Buttons 1–3 are the CAVE's 3D / 2D / 2D-Fixed views
    /// (<c>CaveViewMode</c>), which is why these two are not on 3 and 2 as in the other music rooms. At the
    /// desk, <b>N</b> or <b>4</b> skips and <b>L</b> or <b>6</b> toggles. Nothing is drawn on the screens.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class OrchestraConductor : MonoBehaviour
    {
        [Header("Playlist")]
        [SerializeField, Tooltip("Play every song folder under StreamingAssets/AnimalOrchestra (song.json + MIDI + stems).")]
        bool m_PlaySongFolders = true;

        [SerializeField, Tooltip("Play the three built-in, synthesised songs.")]
        bool m_PlayBuiltInSongs = true;

        [SerializeField, Min(0f), Tooltip("Seconds of idle between songs: the fireflies fill the room.")]
        float m_GapSeconds = 6f;

        [SerializeField, Min(0f), Tooltip("Seconds of idle before the first song, so the room settles after start-up.")]
        float m_FirstSongDelay = 3f;

        [Header("Sound")]
        [SerializeField, Range(0f, 1f)] float m_Volume = 0.9f;

        [SerializeField, Range(-250f, 250f), Tooltip("Shifts the animals against the sound, milliseconds; positive makes them " +
            "later. Measure in the room — film it at 240 fps and count frames between the audio hit and the visual hit — " +
            "then set this to cancel the difference.")]
        float m_VisualOffsetMs;

        [Header("Intensity")]
        [SerializeField, Tooltip("Dimmer glows and a lower flash cap. Toggled from the wand.")]
        bool m_LowIntensity;

        [SerializeField, Range(0f, 1f), Tooltip("How bright glows and flashes are in low intensity mode.")]
        float m_LowIntensityLevel = 0.45f;

        [SerializeField, Range(0.5f, 6f), Tooltip("Fastest anything may flash, Hz. Photosensitivity guidance keeps " +
            "flashing under 3 Hz.")]
        float m_MaxFlashHz = 3f;

        [SerializeField, Range(0.25f, 6f)] float m_LowIntensityMaxFlashHz = 1.5f;

        [Header("Wand and keys")]
        [SerializeField, Min(0), Tooltip("Wand button that skips to the next song. Not 3, the other music rooms' " +
            "next-track button: 1–3 are the view modes here. Indices are the .vrx's; MiddleVRWandButtonProbe lists them.")]
        int m_NextSongButton = 4;

        [SerializeField, Tooltip("Wand button that toggles low intensity mode; −1 for none. Not 2: 1–3 are the view modes.")]
        int m_LowIntensityButton = 6;

        [SerializeField, Tooltip("N or the skip button's digit skips; L or the intensity button's digit toggles low " +
            "intensity. Unity's keyboard, so the Editor or the build needs focus.")]
        bool m_KeyboardShortcuts = true;

        /// <summary>Most audio sources a song can need: one per stem.</summary>
        const int k_MaxStems = Parts.Count;

        /// <summary>Lead time for scheduling a start on the DSP clock: comfortably more than one audio buffer.</summary>
        const double k_ScheduleLead = 0.25;

        enum Entry { Folder, BuiltIn }

        readonly List<(Entry kind, string folder, int index)> m_Playlist = new();
        readonly List<AudioSource> m_Sources = new();

        int m_PlaylistIndex;
        PreparedSong m_Playing;
        PreparedSong m_Ready;
        bool m_Preparing;
        Task<SongData> m_Render;
        int m_RenderIndex;

        bool m_IsPlaying;
        double m_DspStart;
        double m_SongTime;
        double m_LastDsp;
        double m_LastDspAt;
        double m_BlockSeconds = 0.025;
        float m_NextStartAt;
        float m_Idleness = 1f;

        // ================================================================== what the animals read

        /// <summary>A song is playing (scheduled or sounding, until its tail has rung out).</summary>
        public bool IsPlaying => m_IsPlaying;

        /// <summary>Seconds into the playing song on the audio clock, with the visual offset applied.</summary>
        public double SongTime => m_SongTime;

        /// <summary>Beats since the playing song's first beat.</summary>
        public double Beats => m_Playing != null ? m_Playing.Data.BeatAt(m_SongTime) : 0.0;

        /// <summary>Changes every time a song starts or ends, so animals can reset per-song state.</summary>
        public int SongSerial { get; private set; }

        /// <summary>1 normally; lower in low intensity mode. Animals multiply glows and flashes by it.</summary>
        public float Intensity => m_LowIntensity ? m_LowIntensityLevel : 1f;

        /// <summary>The fastest anything may flash, Hz.</summary>
        public float MaxFlashHz => m_LowIntensity ? m_LowIntensityMaxFlashHz : m_MaxFlashHz;

        /// <summary>0 while a song plays, easing to 1 between songs — the room's idle state.</summary>
        public float Idleness => m_Idleness;

        public SongData Current => m_Playing?.Data;

        public bool LowIntensity
        {
            get => m_LowIntensity;
            set
            {
                m_LowIntensity = value;
                Debug.Log($"[AnimalOrchestra] Low intensity {(value ? "on" : "off")}.", this);
            }
        }

        public bool Has(Part part) => m_IsPlaying && m_Playing.Data.Has(part);

        public NoteTrack Track(Part part) => m_IsPlaying ? m_Playing.Data.Track(part) : NoteTrack.Empty;

        public float Level(Part part, Band band) => m_IsPlaying ? m_Playing.Data.Level(part, band, m_SongTime) : 0f;

        // ================================================================== lifecycle

        void Start()
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out _);
            m_BlockSeconds = Math.Max(0.005, bufferLength / (double)Math.Max(8000, AudioSettings.outputSampleRate));

            if (m_PlaySongFolders)
                foreach (var folder in SongFolder.FindAll())
                    m_Playlist.Add((Entry.Folder, folder, 0));

            if (m_PlayBuiltInSongs || m_Playlist.Count == 0)
                for (int i = 0; i < DemoSongs.Count; i++)
                    m_Playlist.Add((Entry.BuiltIn, null, i));

            Debug.Log($"[AnimalOrchestra] Playlist: {m_Playlist.Count} songs ({m_Playlist.FindAll(e => e.kind == Entry.Folder).Count} " +
                      $"from {SongFolder.Root}). Wand {m_NextSongButton} / N skips; wand {m_LowIntensityButton} / L is low intensity.", this);

            m_NextStartAt = Time.unscaledTime + m_FirstSongDelay;
            PrepareNext();
        }

        void OnDisable()
        {
            foreach (var source in m_Sources)
                if (source != null)
                    source.Stop();
        }

        void OnDestroy()
        {
            Release(m_Playing);
            Release(m_Ready);
        }

        void Update()
        {
            ReadInput();
            PollRender();

            if (m_IsPlaying)
            {
                AdvanceClock();
                if (m_SongTime >= m_Playing.Data.Length)
                    EndSong(m_GapSeconds);
            }
            else if (m_Ready != null && Time.unscaledTime >= m_NextStartAt)
            {
                Play(m_Ready);
                m_Ready = null;
                PrepareNext();
            }

            m_Idleness = Motion.Damp(m_Idleness, m_IsPlaying ? 0f : 1f, m_IsPlaying ? 0.4f : 1.2f, Time.unscaledDeltaTime);
        }

        // ================================================================== playlist

        /// <summary>Starts preparing the next song in the playlist, unless one is already ready or on its way.</summary>
        void PrepareNext()
        {
            if (m_Preparing || m_Ready != null || m_Playlist.Count == 0)
                return;

            var entry = m_Playlist[m_PlaylistIndex];
            m_PlaylistIndex = (m_PlaylistIndex + 1) % m_Playlist.Count;
            m_Preparing = true;

            if (entry.kind == Entry.Folder)
            {
                StartCoroutine(SongFolder.Load(entry.folder, song =>
                {
                    m_Preparing = false;
                    m_Ready = song;
                    Debug.Log($"[AnimalOrchestra] Loaded '{song.Data.Title}' ({song.Data.Length:0} s, {song.Clips.Length} stems).", this);
                }, error =>
                {
                    m_Preparing = false;
                    Debug.LogWarning($"[AnimalOrchestra] Skipping a song folder: {error}", this);
                    PrepareNext();
                }));
                return;
            }

            // A built-in song is composed and synthesised off the main thread; PollRender turns the result
            // into a clip, which only the main thread may do.
            int index = entry.index;
            int sampleRate = SongSynth.DefaultSampleRate;
            m_RenderIndex = index;
            m_Render = Task.Run(() =>
            {
                var song = DemoSongs.Compose(index);
                SongSynth.Render(song, sampleRate);
                return song;
            });
        }

        void PollRender()
        {
            if (m_Render == null || !m_Render.IsCompleted)
                return;

            var render = m_Render;
            m_Render = null;
            m_Preparing = false;

            if (render.IsFaulted || render.Result?.Pcm == null)
            {
                Debug.LogError($"[AnimalOrchestra] Built-in song {m_RenderIndex} failed to render: " +
                               $"{render.Exception?.InnerException?.Message ?? "no audio"}", this);
                return;
            }

            var song = render.Result;
            int frames = song.Pcm.Length / song.Channels;
            var clip = AudioClip.Create(song.Title, frames, song.Channels, song.SampleRate, false);
            clip.SetData(song.Pcm, 0);
            song.Pcm = null;   // the clip has its own copy; let the 30 MB go

            m_Ready = new PreparedSong(song, new[] { clip });
            Debug.Log($"[AnimalOrchestra] Rendered '{song.Title}' ({song.Length:0} s).", this);
        }

        // ================================================================== transport

        void Play(PreparedSong song)
        {
            Release(m_Playing);
            m_Playing = song;

            while (m_Sources.Count < Math.Min(k_MaxStems, song.Clips.Length))
                m_Sources.Add(NewSource(m_Sources.Count));

            // Every stem on the same DSP sample: they start together however long this frame takes.
            m_DspStart = AudioSettings.dspTime + k_ScheduleLead;
            for (int i = 0; i < m_Sources.Count; i++)
            {
                var source = m_Sources[i];
                source.Stop();
                source.clip = i < song.Clips.Length ? song.Clips[i] : null;
                source.volume = m_Volume;
                if (source.clip != null)
                    source.PlayScheduled(m_DspStart);
            }

            m_LastDsp = AudioSettings.dspTime;
            m_LastDspAt = Time.unscaledTimeAsDouble;
            m_SongTime = m_LastDsp - m_DspStart + m_VisualOffsetMs * 0.001;
            m_IsPlaying = true;
            SongSerial++;

            Debug.Log($"[AnimalOrchestra] Now playing '{song.Data.Title}' — {song.Data.Bpm:0} BPM, parts: " +
                      string.Join(", ", Array.FindAll(Parts.All, p => song.Data.Has(p))) + ".", this);
        }

        void EndSong(float gap)
        {
            foreach (var source in m_Sources)
                source.Stop();

            m_IsPlaying = false;
            m_SongTime = 0.0;
            m_NextStartAt = Time.unscaledTime + gap;
            SongSerial++;
        }

        /// <summary>Skips to the next song after a short pause, whatever is happening now.</summary>
        public void Skip()
        {
            if (m_IsPlaying)
                EndSong(1f);
            else
                m_NextStartAt = Time.unscaledTime;
        }

        /// <summary>
        /// Song time this frame. The DSP clock steps once per audio buffer; between steps the frame clock
        /// carries it forward (never further than a buffer and a half), and it is not allowed to run back.
        /// </summary>
        void AdvanceClock()
        {
            double dsp = AudioSettings.dspTime;
            double now = Time.unscaledTimeAsDouble;
            if (dsp != m_LastDsp)
            {
                m_LastDsp = dsp;
                m_LastDspAt = now;
            }

            double estimate = dsp + Math.Min(now - m_LastDspAt, m_BlockSeconds * 1.5);
            double time = estimate - m_DspStart + m_VisualOffsetMs * 0.001;

            // A stall (a long frame, the Editor paused) can leave the estimate behind; follow it back only
            // if it is a real jump rather than buffer jitter.
            m_SongTime = time >= m_SongTime || m_SongTime - time > 0.25 ? time : m_SongTime;
        }

        AudioSource NewSource(int index)
        {
            var go = new GameObject($"Stem {index}");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // the room's speakers, not a point in the room
            source.dopplerLevel = 0f;
            source.priority = 0;
            return source;
        }

        static void Release(PreparedSong song)
        {
            if (song == null)
                return;

            foreach (var clip in song.Clips)
                if (clip != null)
                    Destroy(clip);
        }

        // ================================================================== input

        void ReadInput()
        {
            bool next = MiddleVRWand.PressedThisFrame(m_NextSongButton);
            bool low = m_LowIntensityButton >= 0 && MiddleVRWand.PressedThisFrame(m_LowIntensityButton);

            var keyboard = Keyboard.current;
            if (m_KeyboardShortcuts && keyboard != null)
            {
                next |= keyboard.nKey.wasPressedThisFrame || DigitPressed(keyboard, m_NextSongButton);
                low |= keyboard.lKey.wasPressedThisFrame || DigitPressed(keyboard, m_LowIntensityButton);
            }

            if (next)
                Skip();

            if (low)
                LowIntensity = !LowIntensity;
        }

        /// <summary>The number key matching a wand button, so the desk can press what the wand would.</summary>
        static bool DigitPressed(Keyboard keyboard, int button)
        {
            if (button < 0 || button > 9)
                return false;

            var key = button == 0 ? Key.Digit0 : Key.Digit1 + (button - 1);
            return keyboard[key].wasPressedThisFrame;
        }
    }
}

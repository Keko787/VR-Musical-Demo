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

        /// <summary>One clip for a synthesised song (its mix) or a music-folder track; one per stem for a song folder.</summary>
        public readonly AudioClip[] Clips;

        /// <summary>
        /// Whether the clips were made for this song and go with it. A music-folder track's clip is a project
        /// asset that outlives it, and destroying an asset is refused.
        /// </summary>
        public readonly bool OwnsClips;

        public PreparedSong(SongData data, AudioClip[] clips, bool ownsClips = true)
        {
            Data = data;
            Clips = clips;
            OwnsClips = ownsClips;
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
    /// The <b>visualize mode</b> plays the music folder instead: the room's own tracks, which have no MIDI and
    /// no stems, read off the mix into the same parts (<see cref="MixedSong"/>). A track is read before it
    /// plays, a few seconds for a four-minute one, and the next is read while it plays; tracks already read
    /// are kept, so going back to one is instant. It walks on through the folder until it is left.
    /// </para>
    /// <para>
    /// Every stem of a song is started with <see cref="AudioSource.PlayScheduled"/> at the same DSP time,
    /// so they are sample-aligned with one another. The DSP clock only advances once per audio buffer
    /// (about 21 ms here), so between buffers the song time is carried forward on the frame clock and
    /// pulled back to the DSP clock at each new buffer; it never runs backwards.
    /// </para>
    /// <para>
    /// The wand: the joystick <b>right</b> goes into the visualize mode and <b>left</b> comes back to the
    /// orchestra's own songs; <b>up</b> or <b>down</b> brings up the music folder's tracks on the front wall
    /// (<see cref="OrchestraTrackMenu"/>), already a step along, and right or the trigger plays the one
    /// picked. <b>4</b> pauses and plays on, as it does in the other music rooms; <b>7</b> skips to the next
    /// song, in either mode, and <b>6</b> toggles low intensity mode (dimmer glows, a lower flash cap — the
    /// photosensitivity setting). Buttons 1–3 are the CAVE's 3D / 2D / 2D-Fixed views (<c>CaveViewMode</c>),
    /// which is why skip and intensity are not on 3 and 2 as in the other music rooms. At the desk, the arrow
    /// keys do what the joystick does, <b>Space</b> or <b>4</b> pauses, <b>N</b> or <b>7</b> skips and
    /// <b>L</b> or <b>6</b> toggles. Nothing is drawn on the screens but the track list, and that only while
    /// it is up.
    /// </para>
    /// <para>
    /// A pause stops the stems and holds the song time where it is, so the animals stop mid-gesture and wait.
    /// Playing on starts every stem again from the sample it stopped at, all scheduled on one DSP time, so they
    /// stay sample-aligned and the clock stays exact. Between songs, a pause holds the next one back. Skipping,
    /// picking a track and changing mode all let go of a pause, because they all start something.
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

        [Header("Visualize mode: the music folder")]
        [SerializeField, Tooltip("The tracks the visualize mode plays: every clip in Assets/_Project/Audio/Music. Filled by the " +
            "builder, again each time play starts in the editor, and at player build time.")]
        AudioClip[] m_MusicTracks;

        [SerializeField, Tooltip("The list of those tracks on the front wall, which up and down on the joystick bring up. Set by the builder.")]
        OrchestraTrackMenu m_Menu;

        [SerializeField, Min(0f), Tooltip("Seconds of idle between one track and the next in the visualize mode.")]
        float m_MusicGapSeconds = 2f;

        [SerializeField, Min(1), Tooltip("Tracks kept once read, so going back to one starts at once. A few megabytes each.")]
        int m_KeepRead = 8;

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
        [SerializeField, Min(-1), Tooltip("Wand button that pauses the music and plays it on from where it stopped, as 4 " +
            "does in the other music rooms; −1 for none. Indices are the .vrx's; MiddleVRWandButtonProbe lists them.")]
        int m_PlayPauseButton = 4;

        // A new field, not m_NextSongButton with a new default: scenes built before pause saved 4 there, and
        // 4 is pause now. The builder sets both on every build anyway.
        [SerializeField, Min(-1), Tooltip("Wand button that skips to the next song; −1 for none. Not 3, the other music " +
            "rooms' next-track button, because 1–3 are the view modes here, and not 4, which is pause.")]
        int m_SkipButton = 7;

        [SerializeField, Tooltip("Wand button that toggles low intensity mode; −1 for none. Not 2: 1–3 are the view modes.")]
        int m_LowIntensityButton = 6;

        [SerializeField, Tooltip("The joystick: right is the visualize mode, left the orchestra's own songs, up and down bring " +
            "up the music folder's tracks.")]
        bool m_Joystick = true;

        [SerializeField, Tooltip("Space or the pause button's digit pauses; N or the skip button's digit skips; L or the " +
            "intensity button's digit toggles low intensity; the arrow keys are the joystick. Unity's keyboard, so the " +
            "Editor or the build needs focus.")]
        bool m_KeyboardShortcuts = true;

        /// <summary>Most audio sources a song can need: one per stem.</summary>
        const int k_MaxStems = Parts.Count;

        /// <summary>Lead time for scheduling a start on the DSP clock: comfortably more than one audio buffer.</summary>
        const double k_ScheduleLead = 0.25;

        /// <summary>How far the joystick goes before a push counts, and how near the middle it must come back before the next.</summary>
        const float k_StickPush = 0.7f, k_StickRest = 0.3f;

        /// <summary>Seconds of quiet when a pick or a change of mode cuts a song off.</summary>
        const float k_CutGap = 0.4f;

        enum Entry { Folder, BuiltIn }

        enum Mode { Orchestra, Visualize }

        enum Stick { None, Up, Down, Left, Right }

        readonly List<(Entry kind, string folder, int index)> m_Playlist = new();
        readonly List<AudioSource> m_Sources = new();

        int m_PlaylistIndex;
        PreparedSong m_Playing;
        PreparedSong m_Ready;
        bool m_Preparing;
        Task<SongData> m_Render;
        int m_RenderIndex;

        Mode m_Mode;
        int m_MusicTrack = -1;                        // the track the visualize mode is on: playing, or waiting to be read
        int m_PlayingTrack = -1;                      // the music track sounding now; −1 for one of the orchestra's songs
        MixedSong m_Reading;
        int m_ReadingTrack = -1;
        int m_Failures;                               // tracks in a row that could not be read
        readonly Dictionary<AudioClip, SongData> m_Read = new();
        readonly List<AudioClip> m_ReadOrder = new(); // oldest first, for letting the oldest go
        Stick m_Stick;

        bool m_IsPlaying;
        double m_DspStart;
        double m_SongTime;
        double m_LastDsp;
        double m_LastDspAt;
        double m_BlockSeconds = 0.025;
        float m_NextStartAt;
        float m_Idleness = 1f;

        bool m_Paused;
        double m_PausedAt;    // where the sound stopped, seconds into the song before the visual offset; negative before it began
        double m_HoldUntil;   // the DSP time a song played on sounds again: the song time waits for it

        // ================================================================== what the animals read

        /// <summary>A song is playing (scheduled or sounding, until its tail has rung out). Still true while it is paused.</summary>
        public bool IsPlaying => m_IsPlaying;

        /// <summary>The music is held: the song stopped where it was, or, between songs, the next one held back.</summary>
        public bool Paused => m_Paused;

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

        // ================================================================== what the track list reads

        /// <summary>Raised when the mode, the track, or whether it is playing or being read changes.</summary>
        public event Action Changed;

        /// <summary>The visualize mode is on: the music folder is playing, or about to.</summary>
        public bool Visualizing => m_Mode == Mode.Visualize;

        public AudioClip[] MusicTracks { get => m_MusicTracks; set => m_MusicTracks = value; }

        public int MusicTrackCount => m_MusicTracks != null ? m_MusicTracks.Length : 0;

        public AudioClip MusicClip(int index) =>
            m_MusicTracks != null && index >= 0 && index < m_MusicTracks.Length ? m_MusicTracks[index] : null;

        /// <summary>The track the visualize mode is on, or was last on; −1 if it has not been used.</summary>
        public int MusicTrack => m_MusicTrack;

        /// <summary>The music track sounding now; −1 if none is, or one of the orchestra's own songs is.</summary>
        public int PlayingTrack => m_IsPlaying ? m_PlayingTrack : -1;

        /// <summary>The track the visualize mode is waiting on while it is read; −1 if it is waiting on nothing.</summary>
        public int WaitingTrack => m_Mode == Mode.Visualize && !m_IsPlaying ? m_MusicTrack : -1;

        // ================================================================== lifecycle

        void Start()
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out _);
            m_BlockSeconds = Math.Max(0.005, bufferLength / (double)Math.Max(8000, AudioSettings.outputSampleRate));

#if UNITY_EDITOR
            // A track dropped in after the scene was built is in the list without a rebuild.
            var clips = Visualizer.MusicDeck.ClipsInMusicFolder();
            if (clips.Length > 0)
                m_MusicTracks = clips;
#endif

            if (m_PlaySongFolders)
                foreach (var folder in SongFolder.FindAll())
                    m_Playlist.Add((Entry.Folder, folder, 0));

            if (m_PlayBuiltInSongs || m_Playlist.Count == 0)
                for (int i = 0; i < DemoSongs.Count; i++)
                    m_Playlist.Add((Entry.BuiltIn, null, i));

            Debug.Log($"[AnimalOrchestra] Playlist: {m_Playlist.Count} songs ({m_Playlist.FindAll(e => e.kind == Entry.Folder).Count} " +
                      $"from {SongFolder.Root}); {MusicTrackCount} tracks in the music folder for the visualize mode. " +
                      $"Joystick right / → visualizes them, left / ← comes back, up and down / ↑ ↓ list them; " +
                      $"wand {m_PlayPauseButton} / Space pauses; wand {m_SkipButton} / N skips; wand {m_LowIntensityButton} / L " +
                      $"is low intensity.", this);

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
            m_Reading?.Cancel();
            Release(m_Playing);
            Release(m_Ready);
        }

        void Update()
        {
            ReadInput();
            PollRender();
            StepReading();

            if (m_IsPlaying)
            {
                // Paused, or played on and not sounding yet: the song time holds where it stopped.
                if (!m_Paused && AudioSettings.dspTime >= m_HoldUntil)
                {
                    AdvanceClock();
                    if (m_SongTime >= m_Playing.Data.Length)
                        SongEnded();
                }
            }
            else if (m_Paused)
            {
                // Between songs: the next waits until it is played on.
            }
            else if (m_Mode == Mode.Visualize)
            {
                StartMusicWhenRead();
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

        // ================================================================== the visualize mode

        /// <summary>
        /// Plays music-folder track <paramref name="track"/> in the visualize mode, going into it if the
        /// orchestra's own songs are playing. Whatever is sounding stops; the track starts as soon as it has
        /// been read, at once if it has been before. Picking the track that is playing starts it again.
        /// </summary>
        public void Visualize(int track)
        {
            int count = MusicTrackCount;
            if (count == 0)
            {
                Debug.LogWarning($"[AnimalOrchestra] No tracks to visualize: put some in {Visualizer.MusicDeck.MusicFolder}.", this);
                return;
            }

            bool entering = m_Mode != Mode.Visualize;
            m_Mode = Mode.Visualize;
            m_MusicTrack = Wrap(track);
            m_Failures = 0;
            m_Paused = false;
            Cut();
            Read(m_MusicTrack);

            Debug.Log($"[AnimalOrchestra] {(entering ? "Visualize mode: " : "")}'{Title(m_MusicTrack)}' " +
                      $"({m_MusicTrack + 1} of {count}){(IsRead(m_MusicTrack) ? "" : ", reading it")}.", this);
            Changed?.Invoke();
        }

        /// <summary>Goes into the visualize mode on the track it was last on, or the first; nothing if it is on already.</summary>
        public void EnterVisualize()
        {
            if (m_Mode != Mode.Visualize)
                Visualize(Math.Max(0, m_MusicTrack));
        }

        /// <summary>Leaves the visualize mode for the orchestra's own songs, which carry on with the next one.</summary>
        public void ReturnToOrchestra()
        {
            if (m_Mode != Mode.Visualize)
                return;

            m_Mode = Mode.Orchestra;
            m_Reading?.Cancel();
            m_Reading = null;
            m_ReadingTrack = -1;
            m_Paused = false;
            Cut();
            PrepareNext();

            Debug.Log("[AnimalOrchestra] Back to the orchestra's own songs.", this);
            Changed?.Invoke();
        }

        /// <summary>Stops whatever is sounding, leaving a short quiet before the next song starts.</summary>
        void Cut()
        {
            if (m_IsPlaying)
                EndSong(k_CutGap);
            else
                m_NextStartAt = Mathf.Min(m_NextStartAt, Time.unscaledTime + k_CutGap);
        }

        /// <summary>Plays the track the visualize mode is on once it has been read and the gap has passed; starts reading it if nothing is.</summary>
        void StartMusicWhenRead()
        {
            var clip = MusicClip(m_MusicTrack);
            if (clip == null)
            {
                ReturnToOrchestra();
                return;
            }

            if (!m_Read.TryGetValue(clip, out var data))
            {
                Read(m_MusicTrack);
                return;
            }

            if (Time.unscaledTime < m_NextStartAt)
                return;

            Play(new PreparedSong(data, new[] { clip }, false));
            m_PlayingTrack = m_MusicTrack;

            // The next is read while this one plays, so the walk on through the folder does not wait.
            Read(Wrap(m_MusicTrack + 1));
            Changed?.Invoke();
        }

        void SongEnded()
        {
            if (m_Mode == Mode.Visualize && m_PlayingTrack >= 0)
            {
                EndSong(m_MusicGapSeconds);
                m_MusicTrack = Wrap(m_MusicTrack + 1);
            }
            else
            {
                EndSong(m_GapSeconds);
            }

            Changed?.Invoke();
        }

        /// <summary>Starts reading <paramref name="track"/>, unless it has been read or is being read; reading another stops.</summary>
        void Read(int track)
        {
            var clip = MusicClip(track);
            if (clip == null || m_Read.ContainsKey(clip) || (m_Reading != null && m_ReadingTrack == track))
                return;

            m_Reading?.Cancel();
            m_Reading = new MixedSong(clip);
            m_ReadingTrack = track;
        }

        void StepReading()
        {
            if (m_Reading == null)
                return;

            m_Reading.Step();
            if (m_Reading.Failed)
            {
                Debug.LogWarning($"[AnimalOrchestra] Could not read '{Title(m_ReadingTrack)}' to visualize it: {m_Reading.Error}.", this);
                int failed = m_ReadingTrack;
                m_Reading = null;
                m_ReadingTrack = -1;

                // A track waited on that cannot be read is passed over; if none can, back to the orchestra.
                if (m_Mode == Mode.Visualize && failed == m_MusicTrack && !m_IsPlaying)
                {
                    if (++m_Failures >= MusicTrackCount)
                        ReturnToOrchestra();
                    else
                        m_MusicTrack = Wrap(m_MusicTrack + 1);
                }

                Changed?.Invoke();
                return;
            }

            if (!m_Reading.Done)
                return;

            var song = m_Reading.Result;
            Keep(m_Reading.Clip, song);
            int parts = 0, notes = 0;
            foreach (var part in Parts.All)
            {
                if (song.Track(part).Count > 0)
                    parts++;
                notes += song.Track(part).Count;
            }

            Debug.Log($"[AnimalOrchestra] Read '{song.Title}' in {m_Reading.Elapsed:0.0} s: {song.Bpm:0.#} BPM, {notes} notes " +
                      $"over {parts} parts.", this);
            m_Reading = null;
            m_ReadingTrack = -1;
            m_Failures = 0;
            Changed?.Invoke();
        }

        /// <summary>
        /// Keeps a read track, letting the longest-kept go past <see cref="m_KeepRead"/>. One that is playing
        /// can go too: the song playing holds its own reference.
        /// </summary>
        void Keep(AudioClip clip, SongData song)
        {
            m_Read[clip] = song;
            m_ReadOrder.Remove(clip);
            m_ReadOrder.Add(clip);

            while (m_ReadOrder.Count > Math.Max(1, m_KeepRead))
            {
                m_Read.Remove(m_ReadOrder[0]);
                m_ReadOrder.RemoveAt(0);
            }
        }

        bool IsRead(int track)
        {
            var clip = MusicClip(track);
            return clip != null && m_Read.ContainsKey(clip);
        }

        int Wrap(int track)
        {
            int count = Math.Max(1, MusicTrackCount);
            return (track % count + count) % count;
        }

        string Title(int track) => Visualizer.TrackIndicator.TitleOf(MusicClip(track));

        // ================================================================== transport

        void Play(PreparedSong song)
        {
            Release(m_Playing);
            m_Playing = song;
            m_PlayingTrack = -1;

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
            m_HoldUntil = 0.0;
            m_IsPlaying = true;
            SongSerial++;

            Debug.Log($"[AnimalOrchestra] Now playing '{song.Data.Title}' — {song.Data.Bpm:0} BPM, parts: " +
                      string.Join(", ", Array.FindAll(Parts.All, p => song.Data.Has(p))) + ".", this);
        }

        void EndSong(float gap)
        {
            foreach (var source in m_Sources)
            {
                source.Stop();
                source.clip = null;
            }

            m_IsPlaying = false;
            m_SongTime = 0.0;
            m_NextStartAt = Time.unscaledTime + gap;
            SongSerial++;
        }

        /// <summary>
        /// Skips to the next song after a moment's quiet, whatever is happening now: the next track in the visualize
        /// mode. A pause is let go, because the point of skipping is to hear the next one.
        /// </summary>
        public void Skip()
        {
            m_Paused = false;
            if (m_Mode == Mode.Visualize)
            {
                Visualize(m_MusicTrack + 1);
                return;
            }

            if (m_IsPlaying)
                EndSong(1f);
            else
                m_NextStartAt = Time.unscaledTime;

            Changed?.Invoke();
        }

        /// <summary>Pauses the music, or plays it on from where it stopped.</summary>
        public void PlayPause()
        {
            if (m_Paused)
                Resume();
            else
                Pause();

            Changed?.Invoke();
        }

        /// <summary>
        /// Stops every stem and holds the song time. Stopped rather than <see cref="AudioSource.Pause"/>d, because a
        /// stem still waiting on its scheduled start may not keep the wait; where the song had got to is kept here.
        /// </summary>
        void Pause()
        {
            m_Paused = true;
            if (!m_IsPlaying)
            {
                Debug.Log("[AnimalOrchestra] Paused: the next song waits.", this);
                return;
            }

            // Paused again before a play-on had sounded: the song has not moved since the last pause.
            double dsp = AudioSettings.dspTime;
            if (dsp >= m_HoldUntil)
                m_PausedAt = dsp - m_DspStart;

            foreach (var source in m_Sources)
                source.Stop();

            Debug.Log($"[AnimalOrchestra] Paused '{m_Playing.Data.Title}' at {Math.Max(0.0, m_PausedAt):0.0} s.", this);
        }

        /// <summary>
        /// Plays on: every stem from the sample it stopped at, all on one DSP time a schedule lead from now, so they
        /// stay together and the song time picks up where it was held. One paused before the song began waits out
        /// what was left of its lead. Between songs the next starts after a moment's quiet.
        /// </summary>
        void Resume()
        {
            m_Paused = false;
            if (!m_IsPlaying)
            {
                m_NextStartAt = Mathf.Min(m_NextStartAt, Time.unscaledTime + k_CutGap);
                Debug.Log("[AnimalOrchestra] Playing on.", this);
                return;
            }

            double resumeAt = AudioSettings.dspTime + k_ScheduleLead;
            double from = Math.Max(0.0, m_PausedAt);
            m_DspStart = resumeAt - m_PausedAt;
            double start = Math.Max(resumeAt, m_DspStart);

            foreach (var source in m_Sources)
            {
                var clip = source.clip;
                if (clip == null)
                    continue;

                // A stem shorter than the song may already have ended; it stays quiet.
                int sample = (int)(from * clip.frequency);
                if (sample >= clip.samples)
                    continue;

                source.Stop();
                source.timeSamples = sample;
                source.PlayScheduled(start);
            }

            m_HoldUntil = resumeAt;
            Debug.Log($"[AnimalOrchestra] Playing '{m_Playing.Data.Title}' on from {from:0.0} s.", this);
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
            if (song == null || !song.OwnsClips)
                return;

            foreach (var clip in song.Clips)
                if (clip != null)
                    Destroy(clip);
        }

        // ================================================================== input

        void ReadInput()
        {
            bool pause = m_PlayPauseButton >= 0 && MiddleVRWand.PressedThisFrame(m_PlayPauseButton);
            bool next = m_SkipButton >= 0 && MiddleVRWand.PressedThisFrame(m_SkipButton);
            bool low = m_LowIntensityButton >= 0 && MiddleVRWand.PressedThisFrame(m_LowIntensityButton);
            var stick = m_Joystick ? ReadStick() : Stick.None;

            var keyboard = Keyboard.current;
            if (m_KeyboardShortcuts && keyboard != null)
            {
                pause |= keyboard.spaceKey.wasPressedThisFrame || DigitPressed(keyboard, m_PlayPauseButton);
                next |= keyboard.nKey.wasPressedThisFrame || DigitPressed(keyboard, m_SkipButton);
                low |= keyboard.lKey.wasPressedThisFrame || DigitPressed(keyboard, m_LowIntensityButton);

                if (stick == Stick.None)
                    stick = keyboard.upArrowKey.wasPressedThisFrame ? Stick.Up
                        : keyboard.downArrowKey.wasPressedThisFrame ? Stick.Down
                        : keyboard.rightArrowKey.wasPressedThisFrame ? Stick.Right
                        : keyboard.leftArrowKey.wasPressedThisFrame ? Stick.Left
                        : Stick.None;
            }

            if (next)
                Skip();
            else if (pause)
                PlayPause();

            if (low)
                LowIntensity = !LowIntensity;

            // While the list is up it has the joystick and the arrows; it runs after this, so it sees them this frame.
            if (m_Menu != null && m_Menu.IsOpen)
                return;

            switch (stick)
            {
                case Stick.Up:
                    OpenTracks(-1);
                    break;
                case Stick.Down:
                    OpenTracks(1);
                    break;
                case Stick.Right:
                    EnterVisualize();
                    break;
                case Stick.Left:
                    ReturnToOrchestra();
                    break;
            }
        }

        /// <summary>
        /// Brings up the track list with the track one <paramref name="step"/> from the current one selected:
        /// the push that brings it up is also the first step through it. Down from nothing is the first track.
        /// </summary>
        void OpenTracks(int step)
        {
            if (m_Menu == null || MusicTrackCount == 0)
                return;

            int from = m_MusicTrack >= 0 ? m_MusicTrack : step > 0 ? -1 : 0;
            m_Menu.OpenTracks(Wrap(from + step));
        }

        /// <summary>
        /// A new push of the joystick this frame, or none. A push counts once, when it passes
        /// <see cref="k_StickPush"/> along its stronger axis, and the next only after the stick has come back
        /// near the middle — so holding it does nothing more, and the push that picks from the list does not
        /// also act here once the list has closed.
        /// </summary>
        Stick ReadStick()
        {
            var axis = MiddleVRWand.Axis;
            if (Mathf.Abs(axis.x) < k_StickRest && Mathf.Abs(axis.y) < k_StickRest)
            {
                m_Stick = Stick.None;
                return Stick.None;
            }

            if (m_Stick != Stick.None)
                return Stick.None;

            var push = Mathf.Abs(axis.y) >= Mathf.Abs(axis.x)
                ? axis.y >= k_StickPush ? Stick.Up : axis.y <= -k_StickPush ? Stick.Down : Stick.None
                : axis.x >= k_StickPush ? Stick.Right : axis.x <= -k_StickPush ? Stick.Left : Stick.None;

            m_Stick = push;
            return push;
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

using System;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// Plays the music folder: one 2D <see cref="AudioSource"/>, a playlist, and the transport — play,
    /// pause, next, and walking on when a track ends. Any demo that plays the room's music uses this
    /// rather than keeping its own copy of the same forty lines.
    /// </summary>
    /// <remarks>
    /// The playlist is every clip in <see cref="MusicFolder"/>. The folder is read again each time
    /// play starts in the editor, and <c>VisualizerPlaylistProcessor</c> writes it into player
    /// builds, because a player has no asset database to scan at run time. The tracks themselves
    /// are gitignored — they are commercial recordings — so a fresh clone plays nothing until
    /// someone drops a file in.
    /// </remarks>
    [DefaultExecutionOrder(-110)] // before the analyser, so a track change is heard the frame it happens
    public class MusicDeck : MonoBehaviour
    {
        /// <summary>Where tracks go. Every clip in here, by name, is the playlist.</summary>
        public const string MusicFolder = "Assets/_Project/Audio/Music";

        [SerializeField, Tooltip("Plays the playlist. 2D, so the room's speakers carry the mix as mastered.")]
        AudioSource m_Source;

        [SerializeField, Tooltip("Tracks, in order. Filled from Assets/_Project/Audio/Music whenever that folder has anything " +
            "in it: by the scene builders, again each time play starts in the editor, and at player build time.")]
        AudioClip[] m_Playlist;

        [SerializeField] bool m_PlayOnStart = true;
        [SerializeField] bool m_LoopPlaylist = true;

        int m_Track = -1;
        bool m_Paused;

        /// <summary>Raised when a track starts from the top — not on resume. The haunting reads the whole track here.</summary>
        public event Action<AudioClip> TrackStarted;

        /// <summary>Raised on any change a status line would show: track, pause.</summary>
        public event Action Changed;

        public AudioSource Source => m_Source;
        public AudioClip Clip => m_Source != null ? m_Source.clip : null;
        public AudioClip[] Playlist { get => m_Playlist; set => m_Playlist = value; }
        public int Track => m_Track;
        public int TrackCount => m_Playlist != null ? m_Playlist.Length : 0;
        public bool Paused => m_Paused;
        public bool IsPlaying => m_Source != null && m_Source.isPlaying;

        /// <summary>Seconds into the current track, from the audio clock rather than the frame clock.</summary>
        public float Time => m_Source != null && m_Source.clip != null
            ? m_Source.timeSamples / (float)Mathf.Max(1, m_Source.clip.frequency)
            : 0f;

#if UNITY_EDITOR
        /// <summary>
        /// Every audio clip in <see cref="MusicFolder"/>, sorted by name. Editor only — the asset
        /// database does not exist in a player, which is why builds carry the list serialized.
        /// </summary>
        public static AudioClip[] ClipsInMusicFolder()
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            if (!UnityEditor.AssetDatabase.IsValidFolder(MusicFolder))
                return clips.ToArray();

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null)
                    clips.Add(clip);
            }

            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips.ToArray();
        }
#endif

        void Start()
        {
#if UNITY_EDITOR
            // A track dropped in after the scene was built plays without a rebuild.
            var folder = ClipsInMusicFolder();
            if (folder.Length > 0)
                m_Playlist = folder;
#endif

            if (m_PlayOnStart)
                PlayTrack(0);
            else
                Changed?.Invoke();
        }

        void Update()
        {
            if (m_Source == null || m_Paused || m_Playlist == null || m_Playlist.Length == 0 || m_Track < 0)
                return;

            // Stopped with the play head at zero is a clip that ran out; stopped part way through is
            // Unity pausing audio because the window lost focus, which must not skip a track.
            if (m_Source.isPlaying || m_Source.clip == null || m_Source.time > 0.01f)
                return;

            if (m_Playlist.Length == 1 || m_LoopPlaylist || m_Track + 1 < m_Playlist.Length)
                NextTrack();
            else
            {
                m_Paused = true;
                Changed?.Invoke();
            }
        }

        public void PlayTrack(int index)
        {
            if (m_Source == null)
                return;

            if (m_Playlist == null || m_Playlist.Length == 0)
            {
                // Nothing in the folder: play whatever was dragged onto the source by hand, if anything.
                if (m_Source.clip != null && !m_Source.isPlaying)
                {
                    m_Source.Play();
                    TrackStarted?.Invoke(m_Source.clip);
                }

                Changed?.Invoke();
                return;
            }

            m_Track = ((index % m_Playlist.Length) + m_Playlist.Length) % m_Playlist.Length;
            m_Source.clip = m_Playlist[m_Track];
            m_Source.loop = false;
            m_Paused = false;
            m_Source.Play();

            TrackStarted?.Invoke(m_Source.clip);
            Changed?.Invoke();
        }

        public void NextTrack() => PlayTrack(m_Track + 1);

        public void TogglePause()
        {
            if (m_Source == null)
                return;

            if (m_Source.isPlaying)
            {
                m_Source.Pause();
                m_Paused = true;
            }
            else if (m_Source.clip == null)
            {
                PlayTrack(0);
                return;
            }
            else
            {
                m_Source.UnPause();
                m_Paused = false;
            }

            Changed?.Invoke();
        }
    }
}

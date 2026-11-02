using System.Text;
using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The music folder's tracks on the front wall, for the visualize mode. Up or down on the joystick
    /// brings it up, already a step along from the track the mode is on; up and down move through it,
    /// right or the trigger plays the one picked in the visualize mode, and left puts it away. The track
    /// playing is marked in amber, as is one still being read before it plays.
    /// </summary>
    /// <remarks>
    /// The panel, the wand's ray, the joystick, the keys and the scrolling are the shared
    /// <see cref="WallMenu"/>'s, so it is worked like every other music room's list. It differs from their
    /// <see cref="TrackMenu"/> only in whose tracks it lists: the conductor's, which plays them on its own
    /// clock, not a <see cref="MusicDeck"/>'s.
    /// </remarks>
    [DefaultExecutionOrder(-30)] // after the conductor, which opens it, has read the joystick
    public sealed class OrchestraTrackMenu : WallMenu
    {
        [SerializeField] OrchestraConductor m_Conductor;

        OrchestraConductor m_Watched;

        // The orchestra can be rebuilt under it in the editor.
        protected override bool Valid => m_Conductor != null && m_Conductor.MusicTrackCount > 0;

        void OnEnable()
        {
            m_Watched = m_Conductor;
            if (m_Watched != null)
                m_Watched.Changed += OnConductorChanged;
        }

        void OnDisable()
        {
            if (m_Watched != null)
                m_Watched.Changed -= OnConductorChanged;
            m_Watched = null;
        }

        // A track that finishes reading, starts, or ends while the list is up moves the marks along.
        void OnConductorChanged()
        {
            if (IsOpen)
                Refresh();
        }

        /// <summary>Opens the tracks with <paramref name="selected"/> selected.</summary>
        public void OpenTracks(int selected)
        {
            if (!Valid)
                return;

            Entries.Clear();
            for (int i = 0; i < m_Conductor.MusicTrackCount; i++)
                Entries.Add(i);
            Open(selected);
        }

        // Right on the joystick plays a track, as right goes into the visualize mode when the list is down.
        protected override bool CanEnter(int entry) => true;

        protected override void Choose(int entry)
        {
            Close();
            m_Conductor.Visualize(entry);
        }

        protected override string Title() => Paged("VISUALIZE A TRACK");

        protected override void Describe(int entry, StringBuilder text, ref Color colour)
        {
            bool playing = entry == m_Conductor.PlayingTrack;
            bool reading = entry == m_Conductor.WaitingTrack;
            if (playing || reading)
            {
                colour = CurrentColour;
                text.Append("•  ");
            }

            text.Append(TrackIndicator.TitleOf(m_Conductor.MusicClip(entry)));
            if (playing)
                text.Append("   · playing");
            else if (reading)
                text.Append("   · reading");
        }
    }
}

using System.Text;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// A front-wall menu of the playlist and nothing else, for a room whose only list is its music:
    /// the track playing marked in amber, the one up next in blue, and picking one plays it from the
    /// start. The haunted room has one. A room with lists of its own — the glass room's worlds, the
    /// visualizer's modes — has its own menu, with a tracks page beside them.
    /// </summary>
    /// <remarks>
    /// The panel, the wand's ray, the joystick, the keys and the scrolling are the shared
    /// <see cref="WallMenu"/>'s, so it is worked the same way as every other room's menu.
    /// </remarks>
    [DefaultExecutionOrder(-50)] // after the room that opens it has read its buttons
    public class TrackMenu : WallMenu
    {
        [SerializeField] MusicDeck m_Deck;

        MusicDeck m_Watched;

        // The room can be rebuilt under it in the editor.
        protected override bool Valid => m_Deck != null && m_Deck.TrackCount > 0;

        void OnEnable()
        {
            m_Watched = m_Deck;
            if (m_Watched != null)
                m_Watched.Changed += OnDeckChanged;
        }

        void OnDisable()
        {
            if (m_Watched != null)
                m_Watched.Changed -= OnDeckChanged;
            m_Watched = null;
        }

        // A track that ends, or a pause, while the list is up moves the marks along.
        void OnDeckChanged()
        {
            if (IsOpen)
                Refresh();
        }

        /// <summary>Opens the tracks with the one playing selected; closes the menu if it is open.</summary>
        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                OpenTracks();
        }

        public void OpenTracks()
        {
            if (m_Deck == null || m_Deck.TrackCount == 0)
                return;

            FillTracks(m_Deck);
            Open(m_Deck.Track);
        }

        protected override void Choose(int entry)
        {
            Close();
            m_Deck.PlayTrack(entry);
        }

        protected override string Title() => Paged("TRACKS");

        protected override void Describe(int entry, StringBuilder text, ref Color colour) => DescribeTrack(m_Deck, entry, text, ref colour);
    }
}

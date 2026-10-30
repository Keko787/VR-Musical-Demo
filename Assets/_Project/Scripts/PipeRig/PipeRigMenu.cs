using System.Text;
using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.Pipes
{
    /// <summary>
    /// The pipe rig's menu on the front wall. The mode button opens its two modes, the one on marked;
    /// the track button opens the playlist, with the track playing and the one up next marked.
    /// Picking a mode switches to it; picking a track plays it, going over to visualize mode first
    /// if the room was in play mode.
    /// </summary>
    /// <remarks>
    /// The panel, the wand's ray, the joystick, the keys and the scrolling are the shared
    /// <see cref="WallMenu"/>'s, so it is worked the same way as every other room's menu. It stands
    /// well out from the front wall, clear of the mallet bars' shelf.
    /// </remarks>
    [DefaultExecutionOrder(-50)] // after the rig, which opens it, has read its buttons
    public class PipeRigMenu : WallMenu
    {
        enum Page { Modes, Tracks }

        [SerializeField] PipeRig m_Rig;

        static readonly RigMode[] k_Modes = { RigMode.Play, RigMode.Visualize };

        Page m_Page;
        MusicDeck m_Watched;

        public bool ShowingModes => IsOpen && m_Page == Page.Modes;
        public bool ShowingTracks => IsOpen && m_Page == Page.Tracks;

        MusicDeck Deck => m_Rig != null ? m_Rig.Deck : null;

        // The room can be rebuilt under it in the editor.
        protected override bool Valid => m_Rig != null && (m_Page != Page.Tracks || Deck != null);

        void OnEnable()
        {
            m_Watched = Deck;
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
            if (ShowingTracks)
                Refresh();
        }

        public void ToggleModes()
        {
            if (ShowingModes)
                Close();
            else
                OpenModes();
        }

        public void ToggleTracks()
        {
            if (ShowingTracks)
                Close();
            else
                OpenTracks();
        }

        public void OpenModes()
        {
            if (m_Rig == null)
                return;

            m_Page = Page.Modes;
            Entries.Clear();
            for (int i = 0; i < k_Modes.Length; i++)
                Entries.Add(i);
            Open(m_Rig.Mode == RigMode.Play ? 0 : 1);
        }

        public void OpenTracks()
        {
            var deck = Deck;
            if (deck == null || deck.TrackCount == 0)
                return;

            m_Page = Page.Tracks;
            FillTracks(deck);
            Open(deck.Track);
        }

        protected override void Choose(int entry)
        {
            Close();
            if (m_Page == Page.Modes)
                m_Rig.SetMode(k_Modes[entry]);
            else
                m_Rig.PlayTrack(entry);
        }

        protected override string Title() => m_Page == Page.Modes ? "PIPE RIG" : Paged("TRACKS");

        protected override void Describe(int entry, StringBuilder text, ref Color colour)
        {
            if (m_Page == Page.Tracks)
            {
                DescribeTrack(Deck, entry, text, ref colour);
                return;
            }

            var mode = k_Modes[entry];
            if (m_Rig.Mode == mode)
            {
                colour = CurrentColour;
                text.Append("•  ");
            }

            text.Append(mode == RigMode.Play ? "Play" : "Visualize").Append("   <alpha=#99>")
                .Append(mode == RigMode.Play ? "point at an instrument and pull the trigger" : "the machine plays along with the track")
                .Append("<alpha=#FF>");
        }
    }
}

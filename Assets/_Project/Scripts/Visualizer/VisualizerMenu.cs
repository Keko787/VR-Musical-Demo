using System.Text;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// The visualizer's menu on the front wall. The mode button opens the five shows, with the ones on
    /// marked; the track button opens the playlist, with the track playing and the one up next marked.
    /// Picking a mode cross-fades to it alone as the menu closes; picking it twice in quick succession
    /// layers it with whatever else is on, or takes it back out, and leaves the menu open for more.
    /// Picking a track plays it from the start.
    /// </summary>
    /// <remarks>
    /// The panel, the wand's ray, the joystick, the keys and the scrolling are the shared
    /// <see cref="WallMenu"/>'s — the same menu, worked the same way, as the glass room's. While it is
    /// open the shows in the room's volume dim (<see cref="RoomVisualizer"/> does that), so it can be
    /// read through them; the water stays, behind the panel.
    /// </remarks>
    [DefaultExecutionOrder(-50)] // after the RoomVisualizer, which opens it, has read its buttons
    public class VisualizerMenu : WallMenu
    {
        enum Page { Modes, Tracks }

        [SerializeField] RoomVisualizer m_Visualizer;

        Page m_Page;
        MusicDeck m_Watched;

        public bool ShowingModes => IsOpen && m_Page == Page.Modes;
        public bool ShowingTracks => IsOpen && m_Page == Page.Tracks;

        MusicDeck Deck => m_Visualizer != null ? m_Visualizer.Deck : null;

        // The room can be rebuilt under it in the editor.
        protected override bool Valid => m_Visualizer != null && (m_Page != Page.Tracks || Deck != null);

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

        // ---------------------------------------------------------------- opening

        /// <summary>Opens the modes with the one on selected; closes the menu if it is already showing them.</summary>
        public void ToggleModes()
        {
            if (ShowingModes)
                Close();
            else
                OpenModes();
        }

        /// <summary>Opens the tracks with the one playing selected; closes the menu if it is already showing them.</summary>
        public void ToggleTracks()
        {
            if (ShowingTracks)
                Close();
            else
                OpenTracks();
        }

        public void OpenModes()
        {
            if (m_Visualizer == null)
                return;

            m_Page = Page.Modes;
            Entries.Clear();
            var modes = RoomVisualizer.Modes;
            for (int i = 0; i < modes.Count; i++)
                Entries.Add(i);

            Open(Mathf.Max(0, IndexOf(m_Visualizer.Mode)));
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

        static int IndexOf(VisualizerMode mode)
        {
            var modes = RoomVisualizer.Modes;
            for (int i = 0; i < modes.Count; i++)
                if (modes[i] == mode)
                    return i;
            return -1;
        }

        // ---------------------------------------------------------------- choosing

        protected override void Choose(int entry)
        {
            Close();
            if (m_Page == Page.Modes)
                m_Visualizer.SetMode(RoomVisualizer.Modes[entry]);
            else
                Deck.PlayTrack(entry);
        }

        // A mode can be layered; a track only played.
        protected override bool CanDoublePick(int entry) => m_Page == Page.Modes;

        /// <summary>Layers the mode with the ones on, or takes it out, and stays open so another can be added.</summary>
        protected override void DoublePick(int entry)
        {
            m_Visualizer.ToggleMode(RoomVisualizer.Modes[entry]);
            Refresh();
        }

        // ---------------------------------------------------------------- display

        protected override string Title() => m_Page == Page.Modes
            ? "VISUALIZER   <size=60%><alpha=#99>double-pick to add or remove<alpha=#FF></size>"
            : Paged("TRACKS");

        protected override void Describe(int entry, StringBuilder text, ref Color colour)
        {
            if (m_Page == Page.Tracks)
            {
                DescribeTrack(Deck, entry, text, ref colour);
                return;
            }

            var mode = RoomVisualizer.Modes[entry];
            if (m_Visualizer.IsOn(mode))
            {
                colour = CurrentColour;
                text.Append("•  ");
            }

            // The ones on are told by their colour and their bullet, as the glass room marks the world
            // outside; a word after them would not fit beside the longest name.
            text.Append(RoomVisualizer.TitleOf(mode)).Append("   <alpha=#99>").Append(RoomVisualizer.KindOf(mode)).Append("<alpha=#FF>");
        }
    }
}

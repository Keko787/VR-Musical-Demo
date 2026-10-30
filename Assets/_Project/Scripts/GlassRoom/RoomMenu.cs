using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The menu on the front wall, a panel over the window. It shows one of two lists. The world
    /// button opens the worlds, by group (Shelter, Space, Norway) and then the worlds in the group
    /// picked. The track button opens the tracks, with the one playing and the one up next marked.
    /// Picking a world builds it outside as the menu closes; picking a track plays it.
    /// </summary>
    /// <remarks>
    /// The panel, the wand's ray, the joystick, the keys and the scrolling are the shared
    /// <see cref="WallMenu"/>'s, the same as the visualizer's menu; this says what the pages are. The
    /// groups come from each <see cref="SimWorld"/>'s group, in the order the worlds are listed; the
    /// tracks are the <see cref="MusicDeck"/>'s playlist.
    /// </remarks>
    [DefaultExecutionOrder(-70)] // after the Simulation, which opens it, has read its buttons
    public class RoomMenu : WallMenu
    {
        enum Page { Groups, Worlds, Tracks }

        [SerializeField] Simulation m_Simulation;

        readonly List<string> m_Groups = new();

        Page m_Page;
        int m_Group = -1;

        public bool ShowingTracks => IsOpen && m_Page == Page.Tracks;

        MusicDeck Deck => m_Simulation != null ? m_Simulation.Deck : null;

        // The worlds can be rebuilt under it in the editor.
        protected override bool Valid => m_Simulation != null && (m_Page != Page.Tracks || Deck != null);

        void OnEnable()
        {
            if (Deck != null)
                Deck.Changed += OnDeckChanged;
        }

        void OnDisable()
        {
            if (Deck != null)
                Deck.Changed -= OnDeckChanged;
        }

        // A track that ends while the list is up moves the marks along.
        void OnDeckChanged()
        {
            if (ShowingTracks)
                Refresh();
        }

        // ---------------------------------------------------------------- opening

        /// <summary>Opens the worlds on the page of the world outside; closes the menu if it is already showing them.</summary>
        public void ToggleWorlds()
        {
            if (IsOpen && m_Page != Page.Tracks)
                Close();
            else
                OpenWorlds();
        }

        /// <summary>Opens the tracks with the one playing selected; closes the menu if it is already showing them.</summary>
        public void ToggleTracks()
        {
            if (ShowingTracks)
                Close();
            else
                OpenTracks();
        }

        public void OpenWorlds()
        {
            if (m_Simulation == null || m_Simulation.WorldCount == 0)
                return;

            CollectGroups();
            var world = m_Simulation.World;
            m_Group = world != null ? m_Groups.IndexOf(world.Group) : -1;
            m_Page = m_Group >= 0 ? Page.Worlds : Page.Groups;
            Fill();
            Open(Mathf.Max(0, m_Page == Page.Worlds ? Entries.IndexOf(m_Simulation.WorldIndex) : 0));
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

        void CollectGroups()
        {
            m_Groups.Clear();
            for (int i = 0; i < m_Simulation.WorldCount; i++)
            {
                var world = m_Simulation.WorldAt(i);
                if (world != null && !m_Groups.Contains(world.Group))
                    m_Groups.Add(world.Group);
            }
        }

        /// <summary>The rows of the page being shown: the groups, the worlds of one group and the way back, or the tracks.</summary>
        void Fill()
        {
            Entries.Clear();
            switch (m_Page)
            {
                case Page.Groups:
                    for (int i = 0; i < m_Groups.Count; i++)
                        Entries.Add(i);
                    break;

                case Page.Worlds:
                    for (int i = 0; i < m_Simulation.WorldCount; i++)
                    {
                        var world = m_Simulation.WorldAt(i);
                        if (world != null && world.Group == m_Groups[m_Group])
                            Entries.Add(i);
                    }

                    // One group only: the groups page has nothing to offer, so there is no way back to it.
                    if (m_Groups.Count > 1)
                        Entries.Add(-1);
                    break;

                case Page.Tracks:
                    FillTracks(Deck);
                    break;
            }
        }

        // ---------------------------------------------------------------- choosing

        protected override void Choose(int entry)
        {
            switch (m_Page)
            {
                case Page.Groups:
                    Enter(entry);
                    break;

                case Page.Worlds when entry < 0:
                    Back();
                    break;

                case Page.Worlds:
                    Close();
                    m_Simulation.ShowWorld(entry);
                    break;

                case Page.Tracks:
                    Close();
                    Deck.PlayTrack(entry);
                    break;
            }
        }

        protected override bool CanEnter(int entry) => m_Page == Page.Groups;

        void Enter(int group)
        {
            int from = m_Group;
            m_Group = group;
            m_Page = Page.Worlds;
            Fill();
            ShowPage(Mathf.Max(0, Entries.IndexOf(m_Simulation.WorldIndex)), keepHover: from == group);
        }

        protected override void Back()
        {
            if (m_Page != Page.Worlds)
            {
                Close();
                return;
            }

            int was = m_Group;
            m_Page = Page.Groups;
            Fill();
            ShowPage(was);
        }

        // ---------------------------------------------------------------- display

        protected override string Title()
        {
            switch (m_Page)
            {
                case Page.Groups: return "WORLDS";
                case Page.Worlds: return m_Groups[m_Group].ToUpperInvariant();
                default: return Paged("TRACKS");
            }
        }

        protected override void Describe(int entry, StringBuilder text, ref Color colour)
        {
            var current = m_Simulation.World;

            switch (m_Page)
            {
                case Page.Groups:
                {
                    string group = m_Groups[entry];
                    if (current != null && current.Group == group)
                        colour = CurrentColour;
                    text.Append(group).Append("   ").Append(CountIn(group)).Append("  ›");
                    break;
                }

                case Page.Worlds when entry < 0:
                    text.Append("‹  Back");
                    break;

                case Page.Worlds:
                {
                    var world = m_Simulation.WorldAt(entry);
                    if (world == current)
                        colour = CurrentColour;
                    text.Append(world != null ? world.Title : "?");
                    break;
                }

                case Page.Tracks:
                    DescribeTrack(Deck, entry, text, ref colour);
                    break;
            }
        }

        int CountIn(string group)
        {
            int count = 0;
            for (int i = 0; i < m_Simulation.WorldCount; i++)
            {
                var world = m_Simulation.WorldAt(i);
                if (world != null && world.Group == group)
                    count++;
            }

            return count;
        }
    }
}

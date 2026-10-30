using UnityEditor;
using UnityEngine;
using VRShootingGallery.GlassRoom;
using VRShootingGallery.Visualizer;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// The glass room's front-wall overlays: the menu the world and track buttons open, and the strip
    /// that says what is playing and what is up next. Both are the shared <see cref="WallMenuBuilder"/>'s
    /// panel, the same as the visualizer's — rows, scrolling and all — in the glass room's materials.
    /// </summary>
    public static partial class GlassRoomBuilder
    {
        /// <summary>Height of the menu's middle on the front wall: above the desk, a little below the eye.</summary>
        const float k_MenuHeight = 1.5f;

        /// <summary>Height of the strip's middle: in the sky above the eye line, clear of the menu and below the frame's top rail.</summary>
        const float k_IndicatorHeight = 2.0f;

        /// <summary>Just inside the glass.</summary>
        const float k_OverlayInset = k_Inset + 0.03f;

        /// <summary>The menu's and the strip's materials, M_Glass_Menu_Panel and the rest, beside the room's own.</summary>
        const string k_MenuMaterialPrefix = k_Art + "/Materials/M_Glass_";

        /// <summary>
        /// The menu, on the front wall just inside the glass. Hidden until the world or the track
        /// button opens it.
        /// </summary>
        static RoomMenu BuildMenu(Transform root, Simulation simulation)
        {
            var menu = WallMenuBuilder.BuildMenu<RoomMenu>(k_Tag, root, k_MenuMaterialPrefix, k_MenuHeight, k_OverlayInset);
            var so = new SerializedObject(menu);
            SetRef(k_Tag, so, "m_Simulation", simulation);
            so.ApplyModifiedProperties();
            return menu;
        }

        /// <summary>
        /// The track strip, high on the front wall just inside the glass: the track playing, the one up
        /// next, and a bar for how far through it the music is. Faded out until a change of track brings it up.
        /// </summary>
        static TrackIndicator BuildTrackIndicator(Transform root, MusicDeck deck, RoomMenu menu) =>
            WallMenuBuilder.BuildTrackIndicator(k_Tag, root, k_MenuMaterialPrefix, deck, menu, k_IndicatorHeight, k_OverlayInset);
    }
}

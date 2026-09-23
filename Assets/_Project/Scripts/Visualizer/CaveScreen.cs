using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// One surface of the CAVE in room (MiddleVR CenterNode) coordinates: where it is, which way is
    /// across and up as seen from inside, and how big it is. The projected ones are what a 2D layer
    /// is drawn onto 1:1; the open ones (back wall, ceiling) only exist so a laser has somewhere to
    /// end when it is allowed to leave the screens.
    /// </summary>
    [System.Serializable]
    public struct CaveScreen
    {
        public string Name;
        public Vector3 Centre;

        [Tooltip("Unit vector along the screen's width, left to right as seen from inside the room.")]
        public Vector3 Right;

        [Tooltip("Unit vector along the screen's height, bottom to top as seen from inside the room.")]
        public Vector3 Up;

        public float Width;
        public float Height;

        [Tooltip("A real screen the projectors cover. Off for the surfaces the room model has but the CAVE does not draw on.")]
        public bool Projected;

        public CaveScreen(string name, Vector3 centre, Vector3 right, Vector3 up, float width, float height, bool projected)
        {
            Name = name;
            Centre = centre;
            Right = right.normalized;
            Up = up.normalized;
            Width = width;
            Height = height;
            Projected = projected;
        }

        /// <summary>Unit normal pointing into the room.</summary>
        public Vector3 Normal => Vector3.Cross(Up, Right).normalized;

        /// <summary>A point on the screen, <paramref name="u"/> and <paramref name="v"/> in 0–1 from bottom-left, pushed <paramref name="inset"/> metres into the room.</summary>
        public Vector3 Point(float u, float v, float inset = 0f)
        {
            return Centre + Right * ((u - 0.5f) * Width) + Up * ((v - 0.5f) * Height) + Normal * inset;
        }

        public Vector3 Point(Vector2 uv, float inset = 0f) => Point(uv.x, uv.y, inset);

        /// <summary>
        /// The rotation that lays a Unity Quad on this screen facing into the room. A Quad's face is
        /// its −Z side, so its +Z is pointed away from the room.
        /// </summary>
        public Quaternion FacingRotation => Quaternion.LookRotation(-Normal, Up);
    }

    /// <summary>
    /// The lab's VisCube M4, as MiddleVR describes it in <c>MVR.vrx</c> (the VisBox config the demos
    /// run on). MiddleVR is Z-up; the numbers here are the same screens with Y and Z swapped into
    /// Unity's frame. The side screens are 2.3876 m wide, which is 16.5 cm more than the floor is
    /// deep — they run past the floor's back edge — so a layout built from the floor's depth would
    /// leave a stripe of the side walls dark.
    /// </summary>
    public static class CaveScreens
    {
        public const float FrontWidth = 3.556f;
        public const float Height = 2.2225f;
        public const float FloorDepth = 2.2225f;
        public const float SideWidth = 2.3876f;
        public const float HalfWidth = FrontWidth * 0.5f;      // 1.778
        public const float FrontZ = FloorDepth * 0.5f;         // 1.11125
        public const float SideCentreZ = -0.08255f;

        public static readonly CaveScreen Front = new("Front",
            new Vector3(0f, Height * 0.5f, FrontZ), Vector3.right, Vector3.up, FrontWidth, Height, true);

        /// <summary>Up on the floor is toward the front wall, so a 2D layer's "rising" runs away from the door.</summary>
        public static readonly CaveScreen Floor = new("Floor",
            Vector3.zero, Vector3.right, Vector3.forward, FrontWidth, FloorDepth, true);

        public static readonly CaveScreen Left = new("Left",
            new Vector3(-HalfWidth, Height * 0.5f, SideCentreZ), Vector3.forward, Vector3.up, SideWidth, Height, true);

        public static readonly CaveScreen Right = new("Right",
            new Vector3(HalfWidth, Height * 0.5f, SideCentreZ), Vector3.back, Vector3.up, SideWidth, Height, true);

        public static readonly CaveScreen Back = new("Back",
            new Vector3(0f, Height * 0.5f, -FrontZ), Vector3.left, Vector3.up, FrontWidth, Height, false);

        public static readonly CaveScreen Ceiling = new("Ceiling",
            new Vector3(0f, Height, 0f), Vector3.right, Vector3.back, FrontWidth, FloorDepth, false);

        /// <summary>Every surface, projected first.</summary>
        public static readonly CaveScreen[] All = { Front, Floor, Left, Right, Back, Ceiling };

        /// <summary>The four the projectors cover: front, floor, left, right.</summary>
        public static readonly CaveScreen[] Projected = { Front, Floor, Left, Right };
    }
}

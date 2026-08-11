namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Abstraction over "the player is requesting to fire". <see cref="GunController"/>
    /// depends only on this, so a hand-tracking pinch source can be added later as a
    /// single new file with no change to the gun logic (see DESIGN scope notes).
    /// </summary>
    public interface IGunInputSource
    {
        /// <summary>True for as long as the trigger is down. Drives full-auto fire.</summary>
        bool FireHeld { get; }

        /// <summary>
        /// True only on the frame the trigger goes down, and exactly once per pull. Drives
        /// semi-auto fire, and is what keeps a released trigger from letting anything else through.
        /// </summary>
        bool FirePressedThisFrame { get; }
    }
}

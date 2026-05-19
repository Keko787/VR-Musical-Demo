namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Abstraction over "the player is requesting to fire". <see cref="GunController"/>
    /// depends only on this, so a hand-tracking pinch source can be added later as a
    /// single new file with no change to the gun logic (see DESIGN scope notes).
    /// </summary>
    public interface IGunInputSource
    {
        bool FireHeld { get; }
    }
}

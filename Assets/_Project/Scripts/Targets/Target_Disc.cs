namespace VRShootingGallery.Targets
{
    /// <summary>Static disc that flips backwards when hit and rights itself after the reset delay.</summary>
    public class Target_Disc : TargetBase
    {
        protected override void OnHitReaction() => KnockdownAndRespawn();
    }
}

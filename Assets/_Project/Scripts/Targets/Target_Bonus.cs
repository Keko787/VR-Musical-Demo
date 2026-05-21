using UnityEngine;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Rare high-value pop-up. Same motion as <see cref="Target_Duck"/> (tune a shorter up
    /// duration + higher points in the Inspector) plus a particle burst on hit.
    /// </summary>
    public class Target_Bonus : Target_Duck
    {
        [SerializeField] ParticleSystem m_HitVfx;

        protected override void OnHitReaction()
        {
            if (m_HitVfx != null)
                m_HitVfx.Play();

            base.OnHitReaction();
        }
    }
}

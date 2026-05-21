using UnityEngine;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// "Don't shoot" target. Set <c>m_Points</c> to a negative value in the Inspector.
    /// By default it stays down after being hit (no respawn until the round restarts).
    /// </summary>
    public class Target_Penalty : TargetBase
    {
        [SerializeField, Tooltip("If true, the penalty target comes back like a normal target.")]
        bool m_RespawnAfterHit = false;

        protected override void OnHitReaction()
        {
            if (m_RespawnAfterHit)
            {
                KnockdownAndRespawn();
            }
            else if (m_Visual != null)
            {
                // Stays hidden / down for the rest of the round (Hittable already false).
                m_Visual.gameObject.SetActive(false);
            }
        }
    }
}

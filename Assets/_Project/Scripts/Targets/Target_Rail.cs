using UnityEngine;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Slides back and forth between two points. Keeps moving when hit; the visual flips
    /// back and rights itself after the reset delay.
    /// </summary>
    public class Target_Rail : TargetBase
    {
        [SerializeField] Transform m_PointA;
        [SerializeField] Transform m_PointB;
        [SerializeField, Tooltip("Traversals per second along the rail.")]
        float m_Speed = 0.4f;

        float m_T;
        int m_Dir = 1;

        void Update()
        {
            if (m_PointA == null || m_PointB == null)
                return;

            m_T += m_Dir * m_Speed * Time.deltaTime;
            if (m_T >= 1f) { m_T = 1f; m_Dir = -1; }
            else if (m_T <= 0f) { m_T = 0f; m_Dir = 1; }

            transform.position = Vector3.Lerp(m_PointA.position, m_PointB.position, m_T);
        }

        protected override void OnHitReaction() => KnockdownAndRespawn();
    }
}

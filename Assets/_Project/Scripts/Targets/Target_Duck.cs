using UnityEngine;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Pop-up target: rises into view, stays up a while (hittable), then ducks back down
    /// (not hittable) and repeats. Getting hit forces it down early.
    /// </summary>
    public class Target_Duck : TargetBase
    {
        [SerializeField, Tooltip("How far the visual sinks when ducking (metres).")]
        float m_PopHeight = 0.6f;

        [SerializeField] float m_UpDuration = 1.5f;
        [SerializeField] float m_DownDuration = 1.5f;
        [SerializeField, Tooltip("Vertical move speed between up/down (m/s).")]
        float m_MoveSpeed = 6f;

        bool m_Up;
        float m_Timer;

        protected override void Awake()
        {
            base.Awake();
            EnterState(false);
        }

        void Update()
        {
            m_Timer -= Time.deltaTime;
            if (m_Timer <= 0f)
                EnterState(!m_Up);

            var goal = m_Up ? HomeLocalPos : HomeLocalPos - Vector3.up * m_PopHeight;
            m_Visual.localPosition = Vector3.MoveTowards(m_Visual.localPosition, goal, m_MoveSpeed * Time.deltaTime);
        }

        protected void EnterState(bool up)
        {
            m_Up = up;
            m_Timer = up ? m_UpDuration : m_DownDuration;
            Hittable = up;
        }

        protected override void OnHitReaction()
        {
            // Duck down immediately; the cycle will pop it back up after the down duration.
            EnterState(false);
        }
    }
}

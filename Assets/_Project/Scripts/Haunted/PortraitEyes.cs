using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// A portrait whose eyes follow you. The haunted-house trick, except here it is real: the CAVE
    /// tracks the viewer's head, so the painted face's eyes — two small eyeballs set into the
    /// canvas — turn to wherever the tracked viewer actually is. When the ghost comes close, or
    /// shows itself, they look at the ghost instead.
    /// </summary>
    /// <remarks>
    /// The eyes are on the wall, so the people without glasses see them move too; only the
    /// tracked viewer gets the full effect of being watched.
    /// </remarks>
    public class PortraitEyes : Possessable
    {
        [Header("Eyes")]
        [SerializeField, Tooltip("The eyeballs, each pivoting about its centre with its pupil on its local −Z. Filled in by the builder.")]
        Transform[] m_Eyes;

        [SerializeField, Tooltip("Furthest the eyes turn from straight ahead, degrees. Much past this and the whites show like a cartoon.")]
        float m_MaxAngle = 32f;

        [SerializeField, Tooltip("How quickly they track: high is a stare that snaps, low is a slow slide.")]
        float m_Speed = 5f;

        [SerializeField, Range(0f, 1f), Tooltip("How near the ghost has to be, as its wake, before the eyes leave the viewer and watch it.")]
        float m_GhostNear = 0.45f;

        Quaternion[] m_Rest;
        float m_Snap;

        void Awake()
        {
            int count = m_Eyes != null ? m_Eyes.Length : 0;
            m_Rest = new Quaternion[count];
            for (int i = 0; i < count; i++)
                if (m_Eyes[i] != null)
                    m_Rest[i] = m_Eyes[i].localRotation;
        }

        protected override void Animate(float dt)
        {
            if (m_Eyes == null)
                return;

            var ghost = m_Haunting.Ghost;
            bool watchGhost = ghost != null &&
                              (m_Haunting.Reveal > 0.4f || m_Haunting.WakeAt(transform.position) > m_GhostNear);
            var target = watchGhost ? ghost.Position : m_Haunting.HeadPosition;

            m_Snap *= Mathf.Exp(-dt / 0.6f);
            float follow = 1f - Mathf.Exp(-dt * m_Speed * (1f + 4f * m_Snap));
            float weight = Mathf.Max(Woken, m_Snap);

            for (int i = 0; i < m_Eyes.Length; i++)
            {
                var eye = m_Eyes[i];
                if (eye == null)
                    continue;

                var rest = eye.parent != null ? eye.parent.rotation * m_Rest[i] : m_Rest[i];

                // The pupil is on local −Z, so local +Z points from the target through the eye.
                var away = eye.position - target;
                if (away.sqrMagnitude < 1e-6f)
                    continue;

                var look = Quaternion.LookRotation(away, Vector3.up);
                var clamped = Quaternion.RotateTowards(rest, look, m_MaxAngle);
                var wanted = Quaternion.Slerp(rest, clamped, weight);
                eye.rotation = Quaternion.Slerp(eye.rotation, wanted, follow);
            }
        }

        /// <summary>The eyes snap to the viewer and hold them for a moment.</summary>
        public override void Nudge(float strength) => m_Snap = Mathf.Max(m_Snap, strength);
    }
}

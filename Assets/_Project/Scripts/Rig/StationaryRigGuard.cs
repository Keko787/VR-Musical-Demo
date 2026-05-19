using UnityEngine;

namespace VRShootingGallery.Rig
{
    /// <summary>
    /// Asserts at runtime that the XR Origin's world transform is not being moved by rogue virtual
    /// locomotion (continuous move, teleport, climb). Snap-turn rotation is allowed because we keep
    /// it as a calibration tool. Attach to the XR Origin GameObject.
    /// </summary>
    /// <remarks>
    /// Detects per-frame drift rather than absolute drift, so the guard plays nicely with snap turn
    /// (which pivots the origin around the head and produces a small one-frame position delta).
    /// Anything larger than <see cref="m_MaxPerFrameDriftMeters"/> in a single frame is treated as a
    /// rogue teleport/move and logged.
    /// </remarks>
    [DefaultExecutionOrder(int.MaxValue)]
    public class StationaryRigGuard : MonoBehaviour
    {
        [SerializeField, Tooltip("Per-frame position delta (m) above which we treat the motion as a rogue teleport/move. Snap-turn pivots produce deltas well under this — leave at 0.5 m.")]
        float m_MaxPerFrameDriftMeters = 0.5f;

        [SerializeField, Tooltip("If true, log a warning the first time rogue motion is detected.")]
        bool m_LogOnDrift = true;

        Vector3 m_LastPosition;
        bool m_Warned;

        void Start()
        {
            m_LastPosition = transform.position;
        }

        void LateUpdate()
        {
            var delta = transform.position - m_LastPosition;
            m_LastPosition = transform.position;

            if (delta.sqrMagnitude <= m_MaxPerFrameDriftMeters * m_MaxPerFrameDriftMeters)
                return;

            if (m_LogOnDrift && !m_Warned)
            {
                m_Warned = true;
                Debug.LogWarning(
                    $"[StationaryRigGuard] XR Origin '{name}' moved {delta.magnitude:F2} m in a single frame. " +
                    "A continuous-move, teleport, or climb provider is active and should be removed. " +
                    "(Snap-turn pivots are too small to trigger this — they're fine.)",
                    this);
            }
        }
    }
}

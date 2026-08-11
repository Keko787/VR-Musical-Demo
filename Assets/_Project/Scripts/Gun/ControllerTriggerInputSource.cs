using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Fire source backed by a controller trigger. Builds its own <see cref="InputAction"/>
    /// with a direct device binding so it does NOT depend on the rig's input-actions asset
    /// (swapping that asset orphaned references during Phase 0 — we avoid that entirely here).
    /// </summary>
    public class ControllerTriggerInputSource : MonoBehaviour, IGunInputSource
    {
        [SerializeField, Tooltip("Device binding path for the fire trigger.")]
        string m_BindingPath = "<XRController>{RightHand}/trigger";

        [SerializeField, Range(0f, 1f), Tooltip("Trigger pull fraction that counts as 'held'.")]
        float m_PressPoint = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Trigger must fall back below this before it can fire " +
            "again. Keeps a trigger resting near the press point from stuttering out extra rounds.")]
        float m_ReleasePoint = 0.35f;

        InputAction m_Action;
        int m_PolledFrame = -1;
        bool m_Held;
        bool m_Pressed;
        bool m_Primed;

        void Awake()
        {
            m_Action = new InputAction("Fire", InputActionType.Value, m_BindingPath, expectedControlType: "Axis");
        }

        void OnEnable() => m_Action?.Enable();
        void OnDisable() => m_Action?.Disable();
        void OnDestroy() => m_Action?.Dispose();

        public bool FireHeld
        {
            get
            {
                Poll();
                return m_Held;
            }
        }

        public bool FirePressedThisFrame
        {
            get
            {
                Poll();
                return m_Pressed;
            }
        }

        /// <summary>
        /// Schmitt trigger on the analog pull, latched once per frame. An analog trigger resting
        /// right on the press point would otherwise chatter across the threshold and fire repeatedly
        /// from one pull.
        /// </summary>
        void Poll()
        {
            if (m_PolledFrame == Time.frameCount)
                return;

            m_PolledFrame = Time.frameCount;

            float pull = m_Action != null ? m_Action.ReadValue<float>() : 0f;
            bool wasHeld = m_Held;
            m_Held = wasHeld ? pull > Mathf.Min(m_ReleasePoint, m_PressPoint) : pull >= m_PressPoint;

            m_Pressed = m_Primed && m_Held && !wasHeld;
            m_Primed = true;
        }
    }
}

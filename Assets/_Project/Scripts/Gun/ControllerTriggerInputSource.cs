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

        InputAction m_Action;

        void Awake()
        {
            m_Action = new InputAction("Fire", InputActionType.Value, m_BindingPath, expectedControlType: "Axis");
        }

        void OnEnable() => m_Action?.Enable();
        void OnDisable() => m_Action?.Disable();
        void OnDestroy() => m_Action?.Dispose();

        public bool FireHeld => m_Action != null && m_Action.ReadValue<float>() >= m_PressPoint;
    }
}

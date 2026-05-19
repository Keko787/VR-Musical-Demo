using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// TEMP — Phase 1 only. In-headset toggles for the tracer/model before the Modifiers
    /// menu (Phase 3) exists. Remove this component once settings UI is in.
    /// Defaults: left X = toggle gun model, left Y = toggle tracer line,
    /// right A = toggle tracer dot. Builds its own InputActions (no asset dependency).
    /// </summary>
    public class GunDebugToggles : MonoBehaviour
    {
        [SerializeField] Tracer m_Tracer;
        [SerializeField] GunVisibilityToggle m_Visibility;

        InputAction m_ToggleModel;
        InputAction m_ToggleLine;
        InputAction m_ToggleDot;

        void Awake()
        {
            m_ToggleModel = new InputAction("ToggleModel", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            m_ToggleLine = new InputAction("ToggleLine", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            m_ToggleDot = new InputAction("ToggleDot", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        }

        void OnEnable()
        {
            m_ToggleModel.Enable();
            m_ToggleLine.Enable();
            m_ToggleDot.Enable();
        }

        void OnDisable()
        {
            m_ToggleModel.Disable();
            m_ToggleLine.Disable();
            m_ToggleDot.Disable();
        }

        void OnDestroy()
        {
            m_ToggleModel.Dispose();
            m_ToggleLine.Dispose();
            m_ToggleDot.Dispose();
        }

        void Update()
        {
            if (m_Visibility != null && m_ToggleModel.WasPressedThisFrame())
                m_Visibility.ModelVisible = !m_Visibility.ModelVisible;

            if (m_Tracer != null && m_ToggleLine.WasPressedThisFrame())
                m_Tracer.LineEnabled = !m_Tracer.LineEnabled;

            if (m_Tracer != null && m_ToggleDot.WasPressedThisFrame())
                m_Tracer.DotEnabled = !m_Tracer.DotEnabled;
        }
    }
}

using UnityEngine;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// MiddleVR-wand equivalent of <see cref="GunDebugToggles"/>: edge-detected wand buttons
    /// toggle the gun model and the tracer line/dot. The Quest build mapped left-X / left-Y /
    /// right-A across two controllers; the CAVE wand is a single device, so each toggle is a
    /// configurable wand button index — set these to match your .vrx wand config.
    /// TEMP — Phase 1 only, same as the source it mirrors.
    /// </summary>
    public class MiddleVRWandDebugToggles : MonoBehaviour
    {
        [SerializeField] Tracer m_Tracer;
        [SerializeField] GunVisibilityToggle m_Visibility;

        [SerializeField, Tooltip("Wand button that toggles the gun model (Quest: left X).")]
        int m_ToggleModelButton = 1;

        [SerializeField, Tooltip("Wand button that toggles the tracer line (Quest: left Y).")]
        int m_ToggleLineButton = 2;

        [SerializeField, Tooltip("Wand button that toggles the tracer dot (Quest: right A).")]
        int m_ToggleDotButton = 3;

        void Update()
        {
            if (m_Visibility != null && MiddleVRWand.PressedThisFrame(m_ToggleModelButton))
                m_Visibility.ModelVisible = !m_Visibility.ModelVisible;

            if (m_Tracer != null && MiddleVRWand.PressedThisFrame(m_ToggleLineButton))
                m_Tracer.LineEnabled = !m_Tracer.LineEnabled;

            if (m_Tracer != null && MiddleVRWand.PressedThisFrame(m_ToggleDotButton))
                m_Tracer.DotEnabled = !m_Tracer.DotEnabled;
        }
    }
}

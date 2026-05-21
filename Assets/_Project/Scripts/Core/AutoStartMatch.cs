using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.Core
{
    /// <summary>
    /// TEMP — Phase 2 only. Starts a match on scene load and restarts it on the right
    /// controller's B button. Replaced by the menu flow in Phase 3.
    /// </summary>
    public class AutoStartMatch : MonoBehaviour
    {
        [SerializeField] MatchService m_Match;
        [SerializeField] MatchConfig m_Config;

        InputAction m_Restart;

        void Awake()
        {
            if (m_Match == null)
                m_Match = GetComponent<MatchService>();

            m_Restart = new InputAction("Restart", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        }

        void OnEnable() => m_Restart.Enable();
        void OnDisable() => m_Restart.Disable();
        void OnDestroy() => m_Restart.Dispose();

        void Start()
        {
            if (m_Match != null && m_Config != null)
                m_Match.StartMatch(m_Config);
        }

        void Update()
        {
            if (m_Restart.WasPressedThisFrame() && m_Match != null && m_Config != null)
                m_Match.StartMatch(m_Config);
        }
    }
}

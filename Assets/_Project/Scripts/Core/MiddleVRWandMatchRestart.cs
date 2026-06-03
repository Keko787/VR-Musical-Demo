using UnityEngine;

namespace VRShootingGallery.Core
{
    /// <summary>
    /// MiddleVR-wand equivalent of <see cref="AutoStartMatch"/>: starts a match on scene load
    /// and restarts it on a wand button (the Quest build used the right controller's B button).
    /// TEMP — Phase 2 only.
    /// </summary>
    public class MiddleVRWandMatchRestart : MonoBehaviour
    {
        [SerializeField] MatchService m_Match;
        [SerializeField] MatchConfig m_Config;

        [SerializeField, Tooltip("Wand button that restarts the match (Quest: right B).")]
        int m_RestartButton = 4;

        void Awake()
        {
            if (m_Match == null)
                m_Match = GetComponent<MatchService>();
        }

        void Start()
        {
            if (m_Match != null && m_Config != null)
                m_Match.StartMatch(m_Config);
        }

        void Update()
        {
            if (MiddleVRWand.PressedThisFrame(m_RestartButton) && m_Match != null && m_Config != null)
                m_Match.StartMatch(m_Config);
        }
    }
}

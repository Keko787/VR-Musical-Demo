using TMPro;
using UnityEngine;
using VRShootingGallery.Core;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Binds a world-space HUD to <see cref="MatchService"/>: live score + countdown timer,
    /// and a final state when the match ends.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] TMP_Text m_ScoreText;
        [SerializeField] TMP_Text m_TimerText;

        MatchService m_Match;

        void Start()
        {
            m_Match = MatchService.Instance;
            if (m_Match == null)
                return;

            m_Match.ScoreChanged += OnScoreChanged;
            m_Match.Ticked += OnTicked;
            m_Match.MatchEnded += OnMatchEnded;

            OnScoreChanged(m_Match.Score);
            OnTicked(m_Match.TimeRemaining);
        }

        void OnDestroy()
        {
            if (m_Match == null)
                return;

            m_Match.ScoreChanged -= OnScoreChanged;
            m_Match.Ticked -= OnTicked;
            m_Match.MatchEnded -= OnMatchEnded;
        }

        void OnScoreChanged(int score)
        {
            if (m_ScoreText != null)
                m_ScoreText.text = $"Score: {score}";
        }

        void OnTicked(float timeRemaining)
        {
            if (m_TimerText != null)
                m_TimerText.text = Mathf.CeilToInt(timeRemaining).ToString();
        }

        void OnMatchEnded(MatchResult result)
        {
            if (m_TimerText != null)
                m_TimerText.text = "TIME!";
        }
    }
}

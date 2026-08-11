using TMPro;
using UnityEngine;
using VRShootingGallery.Core;

namespace VRShootingGallery.UI
{
    /// <summary>
    /// Binds a world-space scoreboard to <see cref="MatchService"/>: targets shot, countdown
    /// timer, running score, and a status line that tells the player what to shoot next.
    /// Every field is optional, so the same component drives a full CAVE wall board or a
    /// single number on a panel.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Readouts")]
        [SerializeField, Tooltip("Targets shot this round.")]
        TMP_Text m_HitsText;

        [SerializeField, Tooltip("Countdown, shown as m:ss.")]
        TMP_Text m_TimerText;

        [SerializeField] TMP_Text m_ScoreText;

        [SerializeField, Tooltip("What to do next: press start, go, time's up.")]
        TMP_Text m_StatusText;

        [Header("Timer colours")]
        [SerializeField, Tooltip("Seconds remaining at which the timer switches to the warning colour.")]
        float m_WarnThreshold = 10f;

        [SerializeField] Color m_TimerNormal = Color.white;
        [SerializeField] Color m_TimerWarning = new(1f, 0.36f, 0.24f);

        [Header("Status copy")]
        [SerializeField] string m_IdleMessage = "SHOOT  START  TO PLAY";
        [SerializeField] string m_RunningMessage = "GO!";

        MatchService m_Match;

        void Start()
        {
            m_Match = MatchService.Instance;
            if (m_Match == null)
            {
                Debug.LogWarning($"[HUDController] No MatchService in the scene — '{name}' has nothing to show.", this);
                return;
            }

            m_Match.TargetsHitChanged += OnTargetsHitChanged;
            m_Match.ScoreChanged += OnScoreChanged;
            m_Match.Ticked += OnTicked;
            m_Match.StateChanged += OnStateChanged;
            m_Match.MatchEnded += OnMatchEnded;

            OnTargetsHitChanged(m_Match.TargetsHit);
            OnScoreChanged(m_Match.Score);
            OnTicked(RoundLength());
            OnStateChanged(m_Match.State);
        }

        void OnDestroy()
        {
            if (m_Match == null)
                return;

            m_Match.TargetsHitChanged -= OnTargetsHitChanged;
            m_Match.ScoreChanged -= OnScoreChanged;
            m_Match.Ticked -= OnTicked;
            m_Match.StateChanged -= OnStateChanged;
            m_Match.MatchEnded -= OnMatchEnded;
        }

        void OnTargetsHitChanged(int hits)
        {
            if (m_HitsText != null)
                m_HitsText.text = hits.ToString();
        }

        void OnScoreChanged(int score)
        {
            if (m_ScoreText != null)
                m_ScoreText.text = $"SCORE  {score}";
        }

        void OnTicked(float timeRemaining)
        {
            if (m_TimerText == null)
                return;

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeRemaining));
            m_TimerText.text = $"{seconds / 60}:{seconds % 60:00}";
            m_TimerText.color = seconds <= m_WarnThreshold ? m_TimerWarning : m_TimerNormal;
        }

        void OnStateChanged(MatchState state)
        {
            if (m_StatusText == null)
                return;

            switch (state)
            {
                case MatchState.Idle:
                    m_StatusText.text = m_IdleMessage;
                    break;
                case MatchState.Running:
                    m_StatusText.text = m_RunningMessage;
                    break;
                // Ended is filled in by OnMatchEnded, which knows the final numbers.
            }
        }

        void OnMatchEnded(MatchResult result)
        {
            if (m_StatusText != null)
                m_StatusText.text = $"TIME!   {result.TargetsHit} TARGETS   {result.Score} PTS";
        }

        /// <summary>Length of the round the buttons are about to start, for the pre-match timer.</summary>
        float RoundLength()
        {
            if (m_Match.State != MatchState.Idle)
                return m_Match.TimeRemaining;

            var config = m_Match.Config;
            return config != null ? config.Duration : 0f;
        }
    }
}

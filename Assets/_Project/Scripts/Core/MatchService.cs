using System;
using System.Collections.Generic;
using UnityEngine;
using VRShootingGallery.Targets;

namespace VRShootingGallery.Core
{
    public readonly struct MatchResult
    {
        public readonly int Score;
        public readonly string ConfigName;

        public MatchResult(int score, MatchConfig config)
        {
            Score = score;
            ConfigName = config != null ? config.DisplayName : string.Empty;
        }
    }

    /// <summary>
    /// Owns the active round: score, timer, and run state. Subscribes to every
    /// <see cref="TargetBase"/> in the scene when a match starts. Lives on a scene object for
    /// Phase 2; becomes a persistent manager in Phase 3.
    /// </summary>
    public class MatchService : MonoBehaviour
    {
        public static MatchService Instance { get; private set; }

        public int Score { get; private set; }
        public float TimeRemaining { get; private set; }
        public bool IsRunning { get; private set; }

        public event Action<int> ScoreChanged;
        public event Action<float> Ticked;
        public event Action MatchStarted;
        public event Action<MatchResult> MatchEnded;

        MatchConfig m_Config;
        readonly List<TargetBase> m_Targets = new();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void StartMatch(MatchConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[MatchService] StartMatch called with null config.", this);
                return;
            }

            m_Config = config;
            Score = 0;
            TimeRemaining = config.Duration;
            IsRunning = true;

            UnsubscribeTargets();
            m_Targets.Clear();
            m_Targets.AddRange(FindObjectsByType<TargetBase>(FindObjectsSortMode.None));
            foreach (var t in m_Targets)
                t.Scored += OnTargetScored;

            ScoreChanged?.Invoke(Score);
            Ticked?.Invoke(TimeRemaining);
            MatchStarted?.Invoke();
        }

        public void EndMatch()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            UnsubscribeTargets();
            MatchEnded?.Invoke(new MatchResult(Score, m_Config));
        }

        void Update()
        {
            if (!IsRunning)
                return;

            if (m_Config.Mode == MatchMode.TimeAttack)
            {
                TimeRemaining -= Time.deltaTime;
                if (TimeRemaining <= 0f)
                {
                    TimeRemaining = 0f;
                    Ticked?.Invoke(0f);
                    EndMatch();
                    return;
                }
                Ticked?.Invoke(TimeRemaining);
            }
        }

        void OnTargetScored(int points)
        {
            if (!IsRunning)
                return;

            Score += points;
            ScoreChanged?.Invoke(Score);
        }

        void UnsubscribeTargets()
        {
            foreach (var t in m_Targets)
                if (t != null)
                    t.Scored -= OnTargetScored;
        }
    }
}

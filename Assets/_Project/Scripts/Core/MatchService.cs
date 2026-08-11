using System;
using System.Collections.Generic;
using UnityEngine;
using VRShootingGallery.Targets;

namespace VRShootingGallery.Core
{
    /// <summary>
    /// Where a round currently is. Drives the HUD copy and which control-panel buttons accept a shot.
    /// </summary>
    public enum MatchState
    {
        Idle,
        Running,
        Ended,
    }

    public readonly struct MatchResult
    {
        public readonly int Score;
        public readonly int TargetsHit;
        public readonly string ConfigName;

        public MatchResult(int score, int targetsHit, MatchConfig config)
        {
            Score = score;
            TargetsHit = targetsHit;
            ConfigName = config != null ? config.DisplayName : string.Empty;
        }
    }

    /// <summary>
    /// Owns the active round: score, targets-hit count, timer, and run state. Targets register
    /// themselves as they come and go (see <see cref="TargetBase"/>), so spawned targets are
    /// counted the same as ones authored into the scene.
    /// </summary>
    public class MatchService : MonoBehaviour
    {
        public static MatchService Instance { get; private set; }

        [SerializeField, Tooltip("Config used when a caller starts a round without passing one (the control-panel buttons do this).")]
        MatchConfig m_DefaultConfig;

        public int Score { get; private set; }

        /// <summary>How many targets have been shot this round (what the CAVE wall counter shows).</summary>
        public int TargetsHit { get; private set; }

        public float TimeRemaining { get; private set; }
        public MatchState State { get; private set; } = MatchState.Idle;
        public bool IsRunning => State == MatchState.Running;

        /// <summary>Config of the round in flight, or the default one before the first round.</summary>
        public MatchConfig Config => m_Config != null ? m_Config : m_DefaultConfig;

        public event Action<int> ScoreChanged;
        public event Action<int> TargetsHitChanged;
        public event Action<float> Ticked;
        public event Action MatchStarted;
        public event Action<MatchResult> MatchEnded;
        public event Action<MatchState> StateChanged;

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

        /// <summary>Starts a round using <c>m_DefaultConfig</c>. This is what the START button calls.</summary>
        public void StartMatch() => StartMatch(null);

        public void StartMatch(MatchConfig config)
        {
            if (config == null)
                config = m_DefaultConfig;

            if (config == null)
            {
                Debug.LogError("[MatchService] StartMatch needs a MatchConfig — none passed in and no default assigned.", this);
                return;
            }

            m_Config = config;
            Score = 0;
            TargetsHit = 0;
            TimeRemaining = config.Duration;

            // Pick up anything already placed in the scene; spawners register what they create.
            foreach (var t in FindObjectsByType<TargetBase>(FindObjectsSortMode.None))
                Register(t);

            SetState(MatchState.Running);

            ScoreChanged?.Invoke(Score);
            TargetsHitChanged?.Invoke(TargetsHit);
            Ticked?.Invoke(TimeRemaining);
            MatchStarted?.Invoke();
        }

        /// <summary>Restarts with the config of the last round (or the default on a cold start).</summary>
        public void RestartMatch() => StartMatch(Config);

        public void EndMatch()
        {
            if (State != MatchState.Running)
                return;

            SetState(MatchState.Ended);
            MatchEnded?.Invoke(new MatchResult(Score, TargetsHit, m_Config));
        }

        /// <summary>Ends any round in flight and quits the application (stops play mode in the Editor).</summary>
        public void QuitGame()
        {
            EndMatch();
            Debug.Log("[MatchService] Quit requested from the control panel.", this);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Starts counting hits from <paramref name="target"/>. Safe to call twice; targets call this
        /// themselves from <c>OnEnable</c> so spawned ones are picked up mid-round.
        /// </summary>
        public void Register(TargetBase target)
        {
            if (target == null || m_Targets.Contains(target))
                return;

            m_Targets.Add(target);
            target.Scored += OnTargetScored;
        }

        public void Unregister(TargetBase target)
        {
            if (target == null || !m_Targets.Remove(target))
                return;

            target.Scored -= OnTargetScored;
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

            TargetsHit++;
            TargetsHitChanged?.Invoke(TargetsHit);
        }

        void SetState(MatchState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(State);
        }
    }
}

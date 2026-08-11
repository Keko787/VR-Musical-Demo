using System.Collections.Generic;
using UnityEngine;
using VRShootingGallery.Core;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Keeps the gallery stocked while a round is running. Fills the wall to
    /// <see cref="m_InitialCount"/> at the start, tops back up to <see cref="m_MaxAlive"/> on a
    /// timer, and queues an early refill whenever a target is shot. Targets get progressively
    /// faster, smaller and more frequent as the clock runs down.
    /// </summary>
    public class TargetSpawner : MonoBehaviour
    {
        const int k_PlacementTries = 24;

        [Header("What to spawn")]
        [SerializeField, Tooltip("Target prefab. Target_MovingSphere gets its motion set per spawn.")]
        TargetBase m_TargetPrefab;

        [SerializeField, Tooltip("Parent for spawned targets. Defaults to this transform.")]
        Transform m_SpawnParent;

        [Header("Spawn area (local space of the spawn parent)")]
        [SerializeField, Tooltip("Centre of the rectangle targets appear in.")]
        Vector3 m_AreaCenter = new(0f, 1.0f, -0.09f);

        [SerializeField, Tooltip("Width and height of that rectangle, in metres.")]
        Vector2 m_AreaSize = new(2.6f, 1.5f);

        [SerializeField, Tooltip("Closest two live targets may be placed, centre to centre (metres).")]
        float m_MinSeparation = 0.6f;

        [Header("Population")]
        [SerializeField, Tooltip("Targets placed the moment a round starts.")]
        int m_InitialCount = 4;

        [SerializeField, Tooltip("Most targets allowed on the wall at once.")]
        int m_MaxAlive = 6;

        [SerializeField, Tooltip("Seconds between top-up spawns at the start of a round.")]
        float m_SpawnInterval = 2.5f;

        [SerializeField, Tooltip("Seconds after a target is shot before its replacement appears.")]
        float m_RespawnDelay = 0.5f;

        [SerializeField, Tooltip("Seconds a target survives unshot. 0 = it waits to be shot.")]
        float m_TargetLifetime;

        [Header("Difficulty ramp (start of round to end of round)")]
        [SerializeField, Tooltip("Ramp the numbers below across the round instead of holding the start values.")]
        bool m_RampUp = true;

        [SerializeField, Tooltip("Seconds between top-up spawns by the end of the round.")]
        float m_EndSpawnInterval = 1f;

        [SerializeField, Tooltip("Target diameter in metres: start of round, end of round.")]
        Vector2 m_Diameter = new(0.5f, 0.32f);

        [SerializeField, Tooltip("Path loops per second: start of round, end of round.")]
        Vector2 m_SpeedRange = new(0.2f, 0.6f);

        [SerializeField, Tooltip("Half-width and half-height of the path each target traces (metres).")]
        Vector2 m_Amplitude = new(0.4f, 0.2f);

        [SerializeField, Tooltip("Motion patterns picked from at random for each spawn.")]
        TargetMotion[] m_MotionMix =
        {
            TargetMotion.Horizontal,
            TargetMotion.Vertical,
            TargetMotion.Circular,
            TargetMotion.Figure8,
        };

        readonly List<TargetBase> m_Alive = new();
        MatchService m_Match;
        float m_NextSpawn;

        Transform Parent => m_SpawnParent != null ? m_SpawnParent : transform;

        void Start()
        {
            m_Match = MatchService.Instance;
            if (m_Match == null)
            {
                Debug.LogWarning($"[TargetSpawner] No MatchService in the scene — '{name}' will stay idle.", this);
                return;
            }

            m_Match.MatchStarted += OnMatchStarted;
            m_Match.MatchEnded += OnMatchEnded;

            if (m_Match.IsRunning)
                OnMatchStarted();
        }

        void OnDestroy()
        {
            if (m_Match == null)
                return;

            m_Match.MatchStarted -= OnMatchStarted;
            m_Match.MatchEnded -= OnMatchEnded;
        }

        void Update()
        {
            if (m_Match == null || !m_Match.IsRunning)
                return;

            m_NextSpawn -= Time.deltaTime;
            if (m_NextSpawn > 0f)
                return;

            Spawn();
            m_NextSpawn = Mathf.Lerp(m_SpawnInterval, m_EndSpawnInterval, Progress01());
        }

        void OnMatchStarted()
        {
            ClearAll();

            for (int i = 0; i < m_InitialCount; i++)
                Spawn();

            m_NextSpawn = m_SpawnInterval;
        }

        void OnMatchEnded(MatchResult result) => ClearAll();

        /// <summary>Places one target if there is room and a free spot for it.</summary>
        void Spawn()
        {
            if (m_TargetPrefab == null || m_Alive.Count >= m_MaxAlive)
                return;

            float progress = Progress01();
            float diameter = Mathf.Lerp(m_Diameter.x, m_Diameter.y, progress);

            if (!TryPickPoint(diameter, out var localPos))
                return;

            var target = Instantiate(m_TargetPrefab, Parent);
            target.name = $"{m_TargetPrefab.name}_{m_Alive.Count}";
            target.transform.localPosition = localPos;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one * diameter;
            target.CaptureHome();

            if (target is Target_MovingSphere moving)
            {
                var motion = m_MotionMix != null && m_MotionMix.Length > 0
                    ? m_MotionMix[Random.Range(0, m_MotionMix.Length)]
                    : TargetMotion.None;

                float speed = Mathf.Lerp(m_SpeedRange.x, m_SpeedRange.y, progress) * Random.Range(0.8f, 1.2f);
                moving.Configure(motion, speed, m_Amplitude, m_TargetLifetime, Random.value);
            }

            target.Despawned += OnTargetDespawned;
            m_Alive.Add(target);
        }

        void OnTargetDespawned(TargetBase target)
        {
            target.Despawned -= OnTargetDespawned;
            m_Alive.Remove(target);
            Destroy(target.gameObject);

            // Refill sooner than the regular cadence so shooting well keeps the wall busy.
            m_NextSpawn = Mathf.Min(m_NextSpawn, m_RespawnDelay);
        }

        void ClearAll()
        {
            foreach (var target in m_Alive)
            {
                if (target == null)
                    continue;

                target.Despawned -= OnTargetDespawned;
                Destroy(target.gameObject);
            }

            m_Alive.Clear();
        }

        /// <summary>Finds a spot inside the area that is clear of every live target.</summary>
        bool TryPickPoint(float diameter, out Vector3 localPos)
        {
            // Inset so the whole target, plus the swing of its path, stays inside the area.
            var half = new Vector2(
                Mathf.Max(0f, m_AreaSize.x * 0.5f - m_Amplitude.x - diameter * 0.5f),
                Mathf.Max(0f, m_AreaSize.y * 0.5f - m_Amplitude.y - diameter * 0.5f));

            float minSqr = m_MinSeparation * m_MinSeparation;

            for (int i = 0; i < k_PlacementTries; i++)
            {
                var candidate = new Vector3(
                    m_AreaCenter.x + Random.Range(-half.x, half.x),
                    m_AreaCenter.y + Random.Range(-half.y, half.y),
                    m_AreaCenter.z);

                bool clear = true;
                foreach (var target in m_Alive)
                {
                    if (target == null)
                        continue;

                    if ((target.transform.localPosition - candidate).sqrMagnitude < minSqr)
                    {
                        clear = false;
                        break;
                    }
                }

                if (clear)
                {
                    localPos = candidate;
                    return true;
                }
            }

            localPos = default;
            return false;
        }

        float Progress01()
        {
            var config = m_Match != null ? m_Match.Config : null;
            if (!m_RampUp || config == null || config.Duration <= 0f)
                return 0f;

            return Mathf.Clamp01(1f - m_Match.TimeRemaining / config.Duration);
        }

        void OnDrawGizmosSelected()
        {
            var parent = Parent;
            Gizmos.matrix = parent.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(m_AreaCenter, new Vector3(m_AreaSize.x, m_AreaSize.y, 0.01f));
        }
    }
}

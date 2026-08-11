using System;
using System.Collections;
using UnityEngine;
using VRShootingGallery.Core;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Base for all shootable targets. Owns scoring, the hittable gate, and a shared
    /// "knock down then respawn" reaction. Subclasses define their own motion/behaviour by
    /// overriding <see cref="OnHitReaction"/>.
    /// </summary>
    public abstract class TargetBase : MonoBehaviour, IShootable
    {
        [SerializeField, Tooltip("Points awarded (use a negative value for penalty targets).")]
        protected int m_Points = 10;

        [SerializeField, Tooltip("Seconds before the target becomes hittable again after a hit.")]
        protected float m_ResetDelay = 1f;

        [SerializeField, Tooltip("Visual that reacts to hits. Defaults to this transform.")]
        protected Transform m_Visual;

        /// <summary>Raised when the target is shot; argument is the points to award.</summary>
        public event Action<int> Scored;

        /// <summary>Raised when the target takes itself out of play, so a spawner can recycle it.</summary>
        public event Action<TargetBase> Despawned;

        public int Points => m_Points;
        protected bool Hittable { get; set; } = true;

        protected Vector3 HomeLocalPos { get; private set; }
        protected Quaternion HomeLocalRot { get; private set; }
        protected Vector3 HomeLocalScale { get; private set; }

        protected virtual void Awake()
        {
            CaptureHome();
        }

        protected virtual void OnEnable()
        {
            // Spawned targets appear mid-round, so they opt in here rather than waiting to be
            // found by a scene scan at StartMatch.
            if (MatchService.Instance != null)
                MatchService.Instance.Register(this);
        }

        protected virtual void OnDisable()
        {
            if (MatchService.Instance != null)
                MatchService.Instance.Unregister(this);
        }

        /// <summary>
        /// Re-reads the current pose as the target's home. Spawners call this after placing and
        /// sizing a freshly instantiated target, since <see cref="Awake"/> already ran at the
        /// prefab's pose.
        /// </summary>
        public void CaptureHome()
        {
            if (m_Visual == null)
                m_Visual = transform;

            HomeLocalPos = m_Visual.localPosition;
            HomeLocalRot = m_Visual.localRotation;
            HomeLocalScale = m_Visual.localScale;
        }

        public void OnShot(in ShotInfo info)
        {
            if (!Hittable)
                return;

            Hittable = false;
            Scored?.Invoke(m_Points);
            OnHitReaction();
        }

        /// <summary>What the target does when hit. Must eventually set <see cref="Hittable"/> back to true.</summary>
        protected abstract void OnHitReaction();

        /// <summary>Shared reaction: tip the visual back, then restore it after the reset delay.</summary>
        protected void KnockdownAndRespawn(float angle = 80f)
        {
            StartCoroutine(KnockdownRoutine(angle));
        }

        /// <summary>
        /// Takes the target out of play and hands it back to whoever spawned it. Deactivating first
        /// means listeners are free to destroy the object without racing this frame's coroutines.
        /// </summary>
        protected void Despawn()
        {
            Hittable = false;
            gameObject.SetActive(false);
            Despawned?.Invoke(this);
        }

        IEnumerator KnockdownRoutine(float angle)
        {
            m_Visual.localRotation = HomeLocalRot * Quaternion.Euler(angle, 0f, 0f);
            yield return new WaitForSeconds(m_ResetDelay);
            m_Visual.localRotation = HomeLocalRot;
            Hittable = true;
        }
    }
}

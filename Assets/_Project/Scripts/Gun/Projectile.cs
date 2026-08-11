using UnityEngine;
using VRShootingGallery.Targets;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Pooled physics projectile (foam dart / BB). Returns itself to its pool on impact
    /// or after a lifetime, and forwards hits to any <see cref="IShootable"/> it strikes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds before an un-collided projectile returns to the pool.")]
        float m_Lifetime = 3f;

        [SerializeField, Tooltip("Ask PhysX to sweep for contacts between steps as well.")]
        bool m_ContinuousCollision = true;

        [SerializeField, Tooltip("Also trace the path travelled each step and report the first shootable on it. " +
                                 "This is what makes small targets and thin buttons reliably hittable.")]
        bool m_SweepForHits = true;

        Rigidbody m_Rb;
        ProjectilePool m_Pool;
        Vector3 m_LastPos;
        float m_ReleaseTime;
        bool m_Live;

        void Awake()
        {
            m_Rb = GetComponent<Rigidbody>();

            // Speculative CCD is the one mode that also catches kinematic bodies, which is what
            // moving targets use.
            if (m_ContinuousCollision)
                m_Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void SetPool(ProjectilePool pool) => m_Pool = pool;

        public void Launch(Vector3 velocity)
        {
            m_Rb.velocity = Vector3.zero;
            m_Rb.angularVelocity = Vector3.zero;
            m_Rb.velocity = velocity;
            m_LastPos = transform.position;
            m_ReleaseTime = Time.time + m_Lifetime;
            m_Live = true;
        }

        void FixedUpdate()
        {
            if (!m_Live)
                return;

            // Point the nose along the current velocity so darts follow their arc. Shots at the
            // floor console are near-vertical, where the default up vector is degenerate.
            if (m_Rb.velocity.sqrMagnitude > 0.01f)
            {
                var heading = m_Rb.velocity.normalized;
                var up = Mathf.Abs(heading.y) > 0.99f ? Vector3.forward : Vector3.up;
                m_Rb.MoveRotation(Quaternion.LookRotation(heading, up));
            }

            if (m_SweepForHits && SweepHitShootable())
                return;

            m_LastPos = transform.position;

            if (Time.time >= m_ReleaseTime)
                Release();
        }

        /// <summary>
        /// Collision events only fire when the round happens to *end* a physics step inside a
        /// collider. At 14 m/s it moves ~0.28 m per step, so anything shallower than that — a
        /// button plate, a small target — is usually stepped straight over. Tracing the segment
        /// actually travelled closes that gap. Non-shootable geometry is left to the normal
        /// collision response, but it still blocks: only the *first* thing on the segment counts,
        /// so this cannot shoot a target through a wall.
        /// </summary>
        bool SweepHitShootable()
        {
            var travel = transform.position - m_LastPos;
            float distance = travel.magnitude;
            if (distance < 1e-4f)
                return false;

            var direction = travel / distance;

            // Everything except our own layer — the segment ends at this projectile's centre, so
            // without the mask the ray's last few centimetres are inside its own collider.
            int mask = ~(1 << gameObject.layer);
            if (!Physics.Raycast(m_LastPos, direction, out var hit, distance, mask, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.collider.GetComponentInParent<IShootable>() is not { } shootable)
                return false;

            shootable.OnShot(new ShotInfo(hit.point, hit.normal, direction, gameObject));
            Release();
            return true;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!m_Live)
                return;

            // Check for something shootable before releasing. When a round ends a step touching two
            // colliders, Unity delivers a callback per collider in arbitrary order — releasing on
            // the first one would drop the shot if the scenery happened to arrive before the target.
            if (collision.collider.GetComponentInParent<IShootable>() is { } shootable)
            {
                var contact = collision.GetContact(0);
                shootable.OnShot(new ShotInfo(contact.point, contact.normal, transform.forward, gameObject));
                Release();
                return;
            }

            // FixedUpdate runs before the physics step and this callback after it, so the one
            // segment the sweep never gets to see is the step that ends in a collision. Trace it
            // here, or a round that clips the floor short of a floor-mounted button dies unreported.
            if (m_SweepForHits && SweepHitShootable())
                return;

            Release();
        }

        void Release()
        {
            if (!m_Live)
                return;

            m_Live = false;
            if (m_Pool != null)
                m_Pool.Release(this);
            else
                Destroy(gameObject);
        }
    }
}

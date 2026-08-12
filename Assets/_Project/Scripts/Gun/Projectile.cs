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

        [SerializeField, Tooltip("Resolve every hit by tracing the round's path instead of waiting for a PhysX " +
                                 "contact. Exact at any speed, and the only thing that makes small targets " +
                                 "reliably hittable. Turning this off falls back to speculative contacts.")]
        bool m_SweepForHits = true;

        Rigidbody m_Rb;
        ProjectilePool m_Pool;
        Vector3 m_LastPos;
        float m_ReleaseTime;
        bool m_Live;

        void Awake()
        {
            m_Rb = GetComponent<Rigidbody>();

            // When the sweep is authoritative, PhysX contacts are only a backstop, and speculative
            // CCD actively gets in the way: its contacts are approximate, so a round can be stopped
            // by something it visibly flew past. Otherwise speculative is the best available mode,
            // being the only one that also catches the kinematic bodies moving targets use.
            m_Rb.collisionDetectionMode = m_SweepForHits
                ? CollisionDetectionMode.Discrete
                : CollisionDetectionMode.ContinuousSpeculative;
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

            if (m_SweepForHits && SweepForHit())
                return;

            m_LastPos = transform.position;

            if (Time.time >= m_ReleaseTime)
                Release();
        }

        /// <summary>
        /// Resolves the hit geometrically instead of waiting for a contact. A collision event only
        /// fires when the round happens to *end* a step inside a collider, and at 18 m/s it covers
        /// 0.36 m per step — so a 0.14 m ball is stepped straight over roughly six times in ten. The
        /// swept ray has no such blind spot, and being an exact line it also means a round threaded
        /// past one object really does carry on to whatever is behind it.
        /// </summary>
        /// <remarks>
        /// The segment spans the gap since the last step *and* the step about to be taken, so nothing
        /// slips between steps and nothing is reported after the round has already flown past. It
        /// stops at the first collider either way: this cannot shoot through a wall.
        /// </remarks>
        bool SweepForHit()
        {
            var position = transform.position;
            var velocity = m_Rb.velocity;

            var segment = position + velocity * Time.fixedDeltaTime - m_LastPos;
            float distance = segment.magnitude;
            if (distance < 1e-4f)
                return false;

            var direction = segment / distance;

            // Everything but our own layer, or the ray would end inside this projectile's collider.
            int mask = ~(1 << gameObject.layer);
            if (!Physics.Raycast(m_LastPos, direction, out var hit, distance, mask, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.collider.GetComponentInParent<IShootable>() is { } shootable)
                shootable.OnShot(new ShotInfo(hit.point, hit.normal, direction, gameObject));

            // Hand over the round's momentum. PhysX would have done this for us on a real contact,
            // but the sweep gets there first and retires the round — so anything loose in the scene
            // (the tethered balls) needs the impulse applying by hand to react at all.
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                hit.rigidbody.AddForceAtPosition(direction * (m_Rb.mass * velocity.magnitude),
                    hit.point, ForceMode.Impulse);
            }

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

            // Only a backstop now: the sweep runs ahead of the step and normally retires the round
            // before PhysX ever reports a contact. It still matters if the sweep is switched off.
            if (m_SweepForHits && SweepForHit())
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

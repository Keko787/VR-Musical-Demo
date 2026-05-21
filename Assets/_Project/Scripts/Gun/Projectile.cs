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

        Rigidbody m_Rb;
        ProjectilePool m_Pool;
        float m_ReleaseTime;
        bool m_Live;

        void Awake() => m_Rb = GetComponent<Rigidbody>();

        public void SetPool(ProjectilePool pool) => m_Pool = pool;

        public void Launch(Vector3 velocity)
        {
            m_Rb.linearVelocity = Vector3.zero;
            m_Rb.angularVelocity = Vector3.zero;
            m_Rb.linearVelocity = velocity;
            m_ReleaseTime = Time.time + m_Lifetime;
            m_Live = true;
        }

        void FixedUpdate()
        {
            if (!m_Live)
                return;

            // Point the nose along the current velocity so darts follow their arc.
            if (m_Rb.linearVelocity.sqrMagnitude > 0.01f)
                m_Rb.MoveRotation(Quaternion.LookRotation(m_Rb.linearVelocity));

            if (Time.time >= m_ReleaseTime)
                Release();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!m_Live)
                return;

            var contact = collision.GetContact(0);
            if (collision.collider.GetComponentInParent<IShootable>() is { } shootable)
            {
                var info = new ShotInfo(contact.point, contact.normal, transform.forward, gameObject);
                shootable.OnShot(info);
            }

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

using UnityEngine;
using UnityEngine.Pool;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Object pool for one projectile prefab. Avoids per-shot Instantiate/Destroy.
    /// Swap the prefab field to switch foam dart vs. BB.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        [SerializeField] Projectile m_Prefab;
        [SerializeField] int m_DefaultCapacity = 20;
        [SerializeField] int m_MaxSize = 50;

        ObjectPool<Projectile> m_Pool;

        ObjectPool<Projectile> Pool => m_Pool ??= new ObjectPool<Projectile>(
            createFunc: CreateInstance,
            actionOnGet: p => p.gameObject.SetActive(true),
            actionOnRelease: p => p.gameObject.SetActive(false),
            actionOnDestroy: p => Destroy(p.gameObject),
            collectionCheck: true,
            defaultCapacity: m_DefaultCapacity,
            maxSize: m_MaxSize);

        Projectile CreateInstance()
        {
            var p = Instantiate(m_Prefab, transform);
            p.SetPool(this);
            return p;
        }

        public Projectile Get(Vector3 position, Quaternion rotation)
        {
            var p = Pool.Get();
            p.transform.SetPositionAndRotation(position, rotation);
            return p;
        }

        public void Release(Projectile projectile) => Pool.Release(projectile);
    }
}

using System;
using UnityEngine;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Drives aiming and firing. Raycasts forward from the muzzle every frame (the source
    /// of truth for the tracer dot and, later, UI hover), and spawns a pooled projectile
    /// on the fire input, rate-limited by a cooldown.
    /// </summary>
    public class GunController : MonoBehaviour
    {
        [SerializeField] Transform m_Muzzle;
        [SerializeField] ProjectilePool m_Pool;

        [SerializeField, Tooltip("Projectile launch speed (m/s).")]
        float m_LaunchSpeed = 25f;

        [SerializeField, Tooltip("One round per trigger pull. Turn off for full auto while the trigger is held.")]
        bool m_SemiAuto = true;

        [SerializeField, Tooltip("Minimum seconds between shots. In semi-auto this is only a debounce, " +
            "so keep it short — it is not the thing that limits the rate of fire.")]
        float m_FireCooldown = 0.05f;

        [SerializeField, Tooltip("Max raycast distance for the aim point.")]
        float m_AimMaxDistance = 50f;

        [SerializeField, Tooltip("Layers the aim ray can hit.")]
        LayerMask m_AimMask = ~0;

        [SerializeField, Tooltip("Component implementing IGunInputSource. Auto-found on this GameObject if empty.")]
        MonoBehaviour m_InputSourceBehaviour;

        IGunInputSource m_Input;
        float m_NextFireTime;

        /// <summary>Raised the frame a shot is fired (the tracer line listens to this).</summary>
        public event Action Fired;

        /// <summary>True if this frame's aim ray hit something within range.</summary>
        public bool HasAimHit { get; private set; }

        /// <summary>World point the gun is aiming at (hit point, or a far point if no hit).</summary>
        public Vector3 AimPoint { get; private set; }

        public RaycastHit AimHit { get; private set; }

        void Awake()
        {
            m_Input = m_InputSourceBehaviour as IGunInputSource ?? GetComponent<IGunInputSource>();
            if (m_Input == null)
                Debug.LogWarning($"[GunController] No IGunInputSource found on '{name}'. The gun cannot fire.", this);
        }

        void Update()
        {
            UpdateAim();

            if (m_Input == null)
                return;

            // Semi-auto reads the press edge, not the held state. That is what makes releasing the
            // trigger stop the gun on the same frame: there is no "still held" state left over to
            // let another round through once the player has let go.
            bool wantsToFire = m_SemiAuto ? m_Input.FirePressedThisFrame : m_Input.FireHeld;

            if (wantsToFire && Time.time >= m_NextFireTime)
                Fire();
        }

        void UpdateAim()
        {
            var origin = m_Muzzle.position;
            var dir = m_Muzzle.forward;

            if (Physics.Raycast(origin, dir, out var hit, m_AimMaxDistance, m_AimMask, QueryTriggerInteraction.Ignore))
            {
                HasAimHit = true;
                AimHit = hit;
                AimPoint = hit.point;
            }
            else
            {
                HasAimHit = false;
                AimPoint = origin + dir * m_AimMaxDistance;
            }
        }

        void Fire()
        {
            m_NextFireTime = Time.time + m_FireCooldown;

            if (m_Pool != null)
            {
                var projectile = m_Pool.Get(m_Muzzle.position, m_Muzzle.rotation);
                projectile.Launch(m_Muzzle.forward * m_LaunchSpeed);
            }

            Fired?.Invoke();
        }
    }
}

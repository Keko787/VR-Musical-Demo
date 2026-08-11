using System.Collections;
using UnityEngine;

namespace VRShootingGallery.Targets
{
    /// <summary>Motion patterns a spawned gallery target can be given.</summary>
    public enum TargetMotion
    {
        None,
        Horizontal,
        Vertical,
        Circular,
        Figure8,
    }

    /// <summary>
    /// Gallery target that drifts around its spawn point and pops out of existence when shot.
    /// Built to be spawned by <see cref="TargetSpawner"/>: it reports itself through
    /// <c>TargetBase.Despawned</c> when it leaves play, whether that is from a hit or old age.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Target_MovingSphere : TargetBase
    {
        [Header("Motion")]
        [SerializeField, Tooltip("Path the target traces around its spawn point.")]
        TargetMotion m_Motion = TargetMotion.Horizontal;

        [SerializeField, Tooltip("Full loops of the path per second.")]
        float m_Speed = 0.35f;

        [SerializeField, Tooltip("Half-width and half-height of the path, in the parent's space (metres).")]
        Vector2 m_Amplitude = new(0.6f, 0.3f);

        [SerializeField, Tooltip("Seconds before the target retires on its own. 0 = it waits to be shot.")]
        float m_Lifetime;

        [Header("Hit reaction")]
        [SerializeField, Tooltip("Colour flashed the instant the target is hit.")]
        Color m_HitColor = Color.black;

        [SerializeField, Tooltip("Seconds the hit target shrinks away for before it disappears.")]
        float m_VanishDuration = 0.15f;

        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        Renderer m_Renderer;
        Rigidbody m_Body;
        MaterialPropertyBlock m_Block;
        Color m_HomeColor;
        float m_Phase;
        float m_Age;
        bool m_Vanishing;

        protected override void Awake()
        {
            base.Awake();

            m_Renderer = GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();

            // URP Lit/Unlit drive their main colour through _BaseColor; fall back to white otherwise.
            m_HomeColor = m_Renderer.sharedMaterial != null && m_Renderer.sharedMaterial.HasProperty(s_BaseColor)
                ? m_Renderer.sharedMaterial.GetColor(s_BaseColor)
                : Color.white;

            // A collider that moves every frame has to belong to a kinematic body, otherwise PhysX
            // re-bakes it as static geometry each frame and fast projectiles sail straight through.
            m_Body = GetComponent<Rigidbody>();
            if (m_Body == null)
                m_Body = gameObject.AddComponent<Rigidbody>();

            m_Body.isKinematic = true;
            m_Body.useGravity = false;
        }

        /// <summary>Applied by <see cref="TargetSpawner"/> right after the target is placed.</summary>
        public void Configure(TargetMotion motion, float speed, Vector2 amplitude, float lifetime, float phase01 = 0f)
        {
            m_Motion = motion;
            m_Speed = speed;
            m_Amplitude = amplitude;
            m_Lifetime = lifetime;
            m_Phase = phase01;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // Instances are pooled by the spawner only in the sense that they are re-enabled; reset
            // everything a hit changed so a recycled target looks brand new.
            m_Age = 0f;
            m_Vanishing = false;
            Hittable = true;
            m_Visual.localScale = HomeLocalScale;
            SetColor(m_HomeColor);
        }

        void Update()
        {
            if (m_Vanishing)
                return;

            if (m_Lifetime > 0f)
            {
                m_Age += Time.deltaTime;
                if (m_Age >= m_Lifetime)
                {
                    Despawn();
                    return;
                }
            }

            if (m_Motion == TargetMotion.None)
                return;

            m_Phase += Time.deltaTime * m_Speed;
            m_Visual.localPosition = HomeLocalPos + Offset(m_Phase);
        }

        protected override void OnHitReaction()
        {
            SetColor(m_HitColor);
            StartCoroutine(VanishRoutine());
        }

        Vector3 Offset(float phase)
        {
            float a = phase * Mathf.PI * 2f;
            return m_Motion switch
            {
                TargetMotion.Horizontal => new Vector3(Mathf.Sin(a) * m_Amplitude.x, 0f, 0f),
                TargetMotion.Vertical => new Vector3(0f, Mathf.Sin(a) * m_Amplitude.y, 0f),
                TargetMotion.Circular => new Vector3(Mathf.Cos(a) * m_Amplitude.x, Mathf.Sin(a) * m_Amplitude.y, 0f),
                TargetMotion.Figure8 => new Vector3(Mathf.Sin(a) * m_Amplitude.x, Mathf.Sin(a * 2f) * m_Amplitude.y, 0f),
                _ => Vector3.zero,
            };
        }

        IEnumerator VanishRoutine()
        {
            m_Vanishing = true;

            for (float t = 0f; t < m_VanishDuration; t += Time.deltaTime)
            {
                m_Visual.localScale = Vector3.Lerp(HomeLocalScale, Vector3.zero, t / m_VanishDuration);
                yield return null;
            }

            m_Visual.localScale = HomeLocalScale;
            SetColor(m_HomeColor);
            Despawn();
        }

        void SetColor(Color color)
        {
            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(s_BaseColor, color);
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}

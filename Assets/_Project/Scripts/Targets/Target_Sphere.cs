using UnityEngine;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Sphere target that turns black when shot. Stays black (and non-hittable) unless
    /// <see cref="m_RevertOnRespawn"/> is enabled, in which case it restores its original
    /// colour after the reset delay and becomes hittable again.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Target_Sphere : TargetBase
    {
        [SerializeField, Tooltip("Colour applied when the sphere is shot.")]
        Color m_HitColor = Color.black;

        [SerializeField, Tooltip("If true, restore the original colour after the reset delay and allow being shot again.")]
        bool m_RevertOnRespawn = false;

        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;
        Color m_HomeColor;

        protected override void Awake()
        {
            base.Awake();
            m_Renderer = GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();

            // URP Lit/Unlit drive their main colour through _BaseColor; fall back to .color otherwise.
            m_HomeColor = m_Renderer.sharedMaterial != null && m_Renderer.sharedMaterial.HasProperty(s_BaseColor)
                ? m_Renderer.sharedMaterial.GetColor(s_BaseColor)
                : Color.white;
        }

        protected override void OnHitReaction()
        {
            SetColor(m_HitColor);

            if (m_RevertOnRespawn)
                StartCoroutine(RevertRoutine());
            // Otherwise the sphere stays black and Hittable remains false.
        }

        System.Collections.IEnumerator RevertRoutine()
        {
            yield return new WaitForSeconds(m_ResetDelay);
            SetColor(m_HomeColor);
            Hittable = true;
        }

        void SetColor(Color color)
        {
            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(s_BaseColor, color);
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}

using UnityEngine;

namespace VRShootingGallery.Pipes
{
    /// <summary>
    /// A soft glow on a wall or the floor behind an instrument, lit when a ball strikes it. This is
    /// the rig's 2D layer: drawn on the screens themselves, so it carries the rhythm for everyone in
    /// the room, glasses or not, wherever they stand.
    /// </summary>
    /// <remarks>
    /// A glow only comes up to full strength a few times a second (<see cref="MaxRate"/>, 3 Hz by
    /// default); strikes in between only top it up a little. That is the photosensitivity cap: a
    /// fast part flickers the instrument, never the wall.
    /// </remarks>
    public class RigFlash : MonoBehaviour
    {
        [SerializeField] Renderer m_Renderer;

        [SerializeField, ColorUsage(false, true)]
        Color m_Colour = Color.white;

        [SerializeField, Tooltip("Seconds for the glow to fall to a third.")]
        float m_Decay = 0.22f;

        /// <summary>Full-strength flashes a second, at most, per glow.</summary>
        public static float MaxRate = 3f;

        static readonly int k_Color = Shader.PropertyToID("_Color");

        MaterialPropertyBlock m_Block;
        float m_Level;
        float m_FullAt = -10f;
        bool m_Shown = true;

        public Color Colour { get => m_Colour; set => m_Colour = value; }

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            if (m_Renderer == null)
                m_Renderer = GetComponent<Renderer>();
            Apply();
        }

        /// <summary>A strike of <paramref name="strength"/>, 0–1.</summary>
        public void Trigger(float strength)
        {
            strength = Mathf.Clamp01(strength);
            float now = Time.time;
            if (now - m_FullAt >= 1f / Mathf.Max(0.5f, MaxRate))
            {
                m_FullAt = now;
                m_Level = Mathf.Max(m_Level, strength);
            }
            else
            {
                // Too soon for another full flash: a third of one at most.
                m_Level = Mathf.Max(m_Level, strength * 0.35f);
            }
        }

        void Update()
        {
            if (m_Level <= 0f && !m_Shown)
                return;

            m_Level *= Mathf.Exp(-Time.deltaTime / Mathf.Max(0.02f, m_Decay));
            if (m_Level < 0.004f)
                m_Level = 0f;
            Apply();
        }

        void Apply()
        {
            if (m_Renderer == null)
                return;

            bool show = m_Level > 0f;
            if (show != m_Shown)
            {
                m_Shown = show;
                m_Renderer.enabled = show;
            }

            if (!show)
                return;

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_Color, m_Colour * m_Level);
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}

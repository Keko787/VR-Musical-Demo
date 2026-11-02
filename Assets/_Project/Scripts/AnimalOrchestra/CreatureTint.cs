using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The colour of one body part. Every part of every animal shares one material per shader, and the
    /// colour rides in a property block, so a hundred parts in forty colours is still one material — and
    /// with instancing on, one draw per mesh. Saved on the part, applied when it is enabled (in the Editor
    /// too), and changed at runtime through <see cref="Glow"/> and <see cref="Emission"/>.
    /// </summary>
    /// <remarks>
    /// A property block is not saved with the scene, which is why the colours live here as serialized
    /// fields rather than being set once by the builder.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class CreatureTint : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        static MaterialPropertyBlock s_Block;

        [SerializeField] Color m_Color = Color.white;

        [SerializeField, ColorUsage(false, true), Tooltip("Light the part gives off, scaled at runtime by Glow.")]
        Color m_Emission = Color.black;

        Renderer m_Renderer;
        float m_Glow = 1f;
        Color m_AppliedColor, m_AppliedEmission;
        bool m_Applied;

        /// <summary>Surface colour. Alpha is opacity on the glow shader.</summary>
        public Color Color
        {
            get => m_Color;
            set
            {
                m_Color = value;
                Apply();
            }
        }

        /// <summary>The emission at a <see cref="Glow"/> of 1.</summary>
        public Color Emission
        {
            get => m_Emission;
            set
            {
                m_Emission = value;
                Apply();
            }
        }

        /// <summary>Multiplies <see cref="Emission"/>. Performers drive this from their notes and stems.</summary>
        public float Glow
        {
            get => m_Glow;
            set
            {
                m_Glow = Mathf.Max(0f, value);
                Apply();
            }
        }

        /// <summary>Sets colour and emission together. Cheap to call every frame: nothing is uploaded unless they changed.</summary>
        public void Set(Color color, Color emission)
        {
            m_Color = color;
            m_Emission = emission;
            Apply();
        }

        void OnEnable()
        {
            m_Applied = false;
            Apply();
        }

        void OnValidate()
        {
            m_Applied = false;
            Apply();
        }

        void Apply()
        {
            if (m_Renderer == null && !TryGetComponent(out m_Renderer))
                return;

            var emission = m_Emission * m_Glow;

            // Most parts never change after the first frame; skip the block round trip for them.
            if (m_Applied && m_AppliedColor == m_Color && m_AppliedEmission == emission)
                return;

            s_Block ??= new MaterialPropertyBlock();
            m_Renderer.GetPropertyBlock(s_Block);
            s_Block.SetColor(k_BaseColor, m_Color);
            s_Block.SetColor(k_EmissionColor, emission);
            m_Renderer.SetPropertyBlock(s_Block);

            m_AppliedColor = m_Color;
            m_AppliedEmission = emission;
            m_Applied = true;
        }
    }
}

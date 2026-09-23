using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// A candle flame that gives the ghost away: it leans away from the ghost as it passes, the
    /// way a flame bends in a draught, and turns blue while the ghost is out. Otherwise it burns,
    /// taller with the treble.
    /// </summary>
    /// <remarks>
    /// This transform is the wick: the flame's renderer is a child above it, so leaning is a
    /// rotation about the wick.
    /// </remarks>
    public class CandleFlame : MonoBehaviour
    {
        [SerializeField] Haunting m_Haunting;
        [SerializeField] Renderer m_Flame;

        [SerializeField, ColorUsage(false, true)] Color m_Colour = new(1f, 0.6f, 0.2f);
        [SerializeField, ColorUsage(false, true)] Color m_GhostColour = new(0.3f, 0.7f, 1.2f);

        [SerializeField, Tooltip("Degrees the flame leans at the ghost's nearest.")]
        float m_Lean = 35f;

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        Quaternion m_Rest;
        Vector3 m_FlameScale;
        float m_Seed;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            m_Rest = transform.localRotation;
            m_FlameScale = m_Flame != null ? m_Flame.transform.localScale : Vector3.one;
            m_Seed = Random.value * 100f;
            m_Block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (m_Haunting == null || m_Flame == null)
                return;

            float time = Time.time;
            float flicker = Mathf.PerlinNoise(time * 6f, m_Seed);
            float wake = m_Haunting.WakeAt(transform.position);
            var ghost = m_Haunting.Ghost;

            // Away from the ghost, horizontally, with a small idle sway so a still flame is still alive.
            var away = ghost != null ? transform.position - ghost.Position : Vector3.forward;
            away.y = 0f;
            var tilt = away.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, away.normalized) : Vector3.right;
            float angle = m_Lean * wake + 4f * (Mathf.PerlinNoise(time * 1.3f, m_Seed + 7f) - 0.5f);

            var parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            transform.rotation = Quaternion.AngleAxis(angle, tilt) * parent * m_Rest;

            float tall = 1f + 0.15f * (flicker - 0.5f) * 2f + 0.3f * m_Haunting.Level(Listen.High) + 0.25f * wake;
            m_Flame.transform.localScale = new Vector3(m_FlameScale.x, m_FlameScale.y * tall, m_FlameScale.z);

            var colour = Color.Lerp(m_Colour, m_GhostColour, Mathf.SmoothStep(0f, 1f, m_Haunting.Reveal));
            m_Block.SetColor(k_EmissionColor, colour * (0.85f + 0.3f * flicker));
            m_Flame.SetPropertyBlock(m_Block);
        }
    }
}

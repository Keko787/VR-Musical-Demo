using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A glass pendant lamp hanging in the room. It is the calm inside the show: it burns low by day
    /// and full at night (the world outside sets its <see cref="Level"/>), breathes a little warmer
    /// with the loudness, and swings gently on its cord, a touch further with the bass. It never
    /// flickers — the room is the shelter; the music happens outside.
    /// </summary>
    /// <remarks>
    /// The pivot is this transform, at the top of the cord; the lamp hangs below it as children.
    /// Its reflection in the glass walls is placed by <see cref="WindowView"/> from <see cref="BulbPosition"/>.
    /// </remarks>
    public class PendantLamp : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;
        [SerializeField] Light m_Light;

        [SerializeField, Tooltip("The bulb, whose emission follows the lamp's brightness.")]
        Renderer[] m_Glows;

        [SerializeField, Tooltip("The glass round the bulb, which catches a little of its glow.")]
        Renderer[] m_Shades;

        [SerializeField, Range(0f, 1f)] float m_ShadeGlow = 0.12f;

        [SerializeField] Transform m_Bulb;
        [SerializeField, ColorUsage(false, true)] Color m_Glow = new(2f, 1.3f, 0.7f);
        [SerializeField] float m_LightIntensity = 0.9f;

        [SerializeField, Tooltip("Degrees of swing at full bass. A little of it is always there.")]
        float m_SwayDegrees = 2.5f;

        [SerializeField, Tooltip("Metres of cord, which sets the swing's period like any pendulum.")]
        float m_CordLength = 0.6f;

        [SerializeField] Vector3 m_SwayAxis = Vector3.right;
        [SerializeField] float m_Phase;

        /// <summary>How bright the world outside wants the lamps, 0 to about 1.5.</summary>
        public float Level { get; set; } = 1f;

        /// <summary>This frame's brightness, for the reflection in the glass.</summary>
        public float Brightness { get; private set; }

        public Vector3 BulbPosition => (m_Bulb != null ? m_Bulb : transform).position;

        static readonly int k_Emission = Shader.PropertyToID("_EmissionColor");

        Quaternion m_Rest;
        float m_Swing;
        float m_Breath;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            m_Rest = transform.localRotation;
            m_Block = new MaterialPropertyBlock();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            float bass = m_Simulation != null ? m_Simulation.Bass : 0f;
            float loudness = m_Simulation != null ? m_Simulation.Loudness : 0f;

            float target = 0.25f + 0.75f * bass;
            float seconds = target > m_Swing ? 1.2f : 3f;
            m_Swing += (target - m_Swing) * (1f - Mathf.Exp(-dt / seconds));

            float omega = Mathf.Sqrt(9.81f / Mathf.Max(0.1f, m_CordLength));
            float angle = m_Swing * m_SwayDegrees * Mathf.Sin(Time.time * omega + m_Phase);
            transform.localRotation = m_Rest * Quaternion.AngleAxis(angle, m_SwayAxis);

            m_Breath += (loudness - m_Breath) * (1f - Mathf.Exp(-dt / 0.6f));
            Brightness = Level * (0.85f + 0.3f * m_Breath);

            if (m_Light != null)
                m_Light.intensity = m_LightIntensity * Brightness;

            Glow(m_Glows, m_Glow * Brightness);
            Glow(m_Shades, m_Glow * (Brightness * m_ShadeGlow));
        }

        void Glow(Renderer[] renderers, Color emission)
        {
            if (renderers == null)
                return;

            foreach (var glow in renderers)
            {
                if (glow == null)
                    continue;

                glow.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_Emission, emission);
                glow.SetPropertyBlock(m_Block);
            }
        }
    }
}

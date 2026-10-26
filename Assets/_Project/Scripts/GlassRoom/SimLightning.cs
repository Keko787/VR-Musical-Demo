using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// Lightning in a storm's clouds on the strong beats: the cloud lights up from inside round where
    /// it struck, flickers once more and dies away, and the land below catches a little of it. Only
    /// the strongest beats, and never two strikes closer than the gap, so it stays an event; dimmer in
    /// low intensity, and the beat itself is already capped by the analyser.
    /// </summary>
    public class SimLightning : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Range(0f, 1f), Tooltip("How strong a beat has to be to strike.")]
        float m_Threshold = 0.65f;

        [SerializeField, Tooltip("Seconds at least between strikes.")]
        float m_Gap = 1.6f;

        [SerializeField, Tooltip("Degrees round from straight ahead, either side, that strikes land within; and how high they are, lowest and highest.")]
        Vector3 m_Sky = new(80f, 8f, 30f);

        [SerializeField, Range(0f, 2f)] float m_Brightness = 1f;

        static readonly int k_Flash = Shader.PropertyToID("_GR_Flash");

        float m_Last = -10f;
        float m_Strength;
        Vector3 m_Toward = Vector3.forward;

        void OnDisable() => Shader.SetGlobalVector(k_Flash, Vector4.zero);

        void Update()
        {
            if (m_Simulation == null)
                return;

            if (m_Simulation.Beat && m_Simulation.BeatStrength >= m_Threshold && Time.time - m_Last >= m_Gap)
            {
                m_Last = Time.time;
                m_Strength = Mathf.Lerp(0.6f, 1f, m_Simulation.BeatStrength) * m_Brightness;
                float azimuth = Random.Range(-m_Sky.x, m_Sky.x) * Mathf.Deg2Rad;
                float elevation = Random.Range(m_Sky.y, m_Sky.z) * Mathf.Deg2Rad;
                var local = new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Cos(azimuth) * Mathf.Cos(elevation));
                m_Toward = transform.rotation * local;
            }

            // A strike, a flicker a tenth of a second later, then gone.
            float age = Time.time - m_Last;
            float flash = Mathf.Exp(-age * 16f) + 0.6f * Mathf.Exp(-Mathf.Abs(age - 0.13f) * 30f);
            float level = m_Strength * flash * m_Simulation.Intensity * (m_Simulation.LowIntensity ? 0.5f : 1f);
            Shader.SetGlobalVector(k_Flash, new Vector4(m_Toward.x, m_Toward.y, m_Toward.z, level));
        }
    }
}

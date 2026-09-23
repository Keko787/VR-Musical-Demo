using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// A lamp the haunting takes over: it swells with its part of the music, dips on the beat, sags
    /// when the room is deeply possessed, and goes down with the rest of the room as the ghost
    /// appears. The <see cref="Light"/> and the glowing shade move together, so what the lamp looks
    /// like and what it lights agree.
    /// </summary>
    /// <remarks>
    /// Flicker is a <em>dip</em> — the lamp darkens briefly and recovers — never a flash above its
    /// normal level, and it only ever happens on the analyser's beat, which is capped at three a
    /// second. Low intensity cuts the dip to a third. There is no flicker on a timer of its own:
    /// in a dark enclosed room that is the photosensitivity rule.
    /// </remarks>
    public class PossessedLight : Possessable
    {
        [Header("Lamp")]
        [SerializeField] Light m_Light;

        [SerializeField, Tooltip("The shade, bulbs or flame that glow with the light. Their emission follows its intensity.")]
        Renderer[] m_Glows;

        [SerializeField, ColorUsage(false, true)] Color m_GlowColour = new(1f, 0.7f, 0.4f);

        [SerializeField, Range(0f, 1f), Tooltip("How far a beat dips the lamp, as a fraction of its level.")]
        float m_FlickerDepth = 0.65f;

        [SerializeField, Range(0f, 1f), Tooltip("Chance a beat dips this lamp, once it is awake.")]
        float m_FlickerChance = 0.6f;

        [SerializeField, Tooltip("Seconds a dip takes to recover to a third.")]
        float m_DipSeconds = 0.09f;

        [SerializeField, Range(0f, 1f), Tooltip("How much brighter the lamp swells with its part of the music.")]
        float m_Swell = 0.3f;

        [SerializeField, Range(0f, 1f), Tooltip("How far the lamp sags — slowly, like a brownout — when the room is deeply possessed.")]
        float m_Sag = 0.35f;

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        float m_Intensity;
        float m_Dip;
        float m_Sagging;
        MaterialPropertyBlock m_Block;
        System.Random m_Random;

        void Awake()
        {
            m_Intensity = m_Light != null ? m_Light.intensity : 1f;
            m_Block = new MaterialPropertyBlock();
            m_Random = new System.Random(GetInstanceID());
        }

        protected override void Animate(float dt)
        {
            if (m_Haunting.Beat && Woken > 0.15f && m_Random.NextDouble() < m_FlickerChance)
            {
                float depth = m_FlickerDepth * (0.5f + 0.5f * m_Haunting.BeatStrength) * Woken;
                if (m_Haunting.LowIntensity)
                    depth *= 0.33f;

                m_Dip = Mathf.Max(m_Dip, depth);
            }

            m_Dip *= Mathf.Exp(-dt / Mathf.Max(0.01f, m_DipSeconds));

            float sag = Mathf.Clamp01((m_Haunting.Possession - 0.75f) * 4f) * Woken;
            m_Sagging = Mathf.MoveTowards(m_Sagging, sag, dt / 2f);

            float level = m_Haunting.RoomLight * (1f + m_Swell * Level) * (1f - m_Dip) * (1f - m_Sag * m_Sagging);
            Apply(level);
        }

        public override void Nudge(float strength) => m_Dip = Mathf.Max(m_Dip, m_FlickerDepth * strength);

        void Apply(float level)
        {
            if (m_Light != null)
                m_Light.intensity = m_Intensity * level;

            if (m_Glows == null)
                return;

            m_Block.SetColor(k_EmissionColor, m_GlowColour * level);
            foreach (var glow in m_Glows)
                if (glow != null)
                    glow.SetPropertyBlock(m_Block);
        }
    }
}

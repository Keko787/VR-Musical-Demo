using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// An upright piano that plays itself: every key goes down when its own note is loud in the
    /// music. A keyboard is already a logarithmic frequency axis, so this is a spectrum analyser in
    /// the shape of an instrument — the most legible thing in the room.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each key looks up its own pitch in the analyser's raw spectrum (the 24 bands are far too
    /// coarse for 88 keys), weighted up toward the treble the way the bands are, and measured in
    /// decibels against a running peak across the whole keyboard, so the loud notes stand out and
    /// quiet ones stay down. A key only goes down if it is louder than both neighbours: without
    /// that, one bass note spreads across the several keys that share its bins, and the keyboard
    /// reads as a smear rather than as playing.
    /// </para>
    /// <para>
    /// At 11.7 Hz a bin, keys are resolved individually from about middle C up. Below that,
    /// neighbouring keys share bins and the local-maximum rule picks one of them. It is a show,
    /// not a transcription.
    /// </para>
    /// </remarks>
    public class PossessedPiano : Possessable
    {
        [Header("Keys")]
        [SerializeField, Tooltip("Key pivots, lowest first, each at the back of its key so a press tips the front down. Filled in by the builder.")]
        Transform[] m_Keys;

        [SerializeField, Tooltip("MIDI note of the first key. A full piano starts on A0, note 21.")]
        int m_LowestNote = 21;

        [SerializeField, Tooltip("Degrees a key tips when fully down.")]
        float m_PressAngle = 4f;

        [SerializeField, Tooltip("Decibels below the keyboard's running peak that read as silent.")]
        float m_RangeDb = 24f;

        [SerializeField, Range(0f, 1f), Tooltip("Level at which a key starts to go down.")]
        float m_PressFrom = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("Level at which it is fully down.")]
        float m_PressFull = 0.88f;

        [SerializeField, Tooltip("Seconds for the keyboard's running peak to fall by half.")]
        float m_PeakHalfLife = 2.5f;

        [SerializeField, Tooltip("Emission a key gets while it is down: the invisible player's touch.")]
        [ColorUsage(false, true)] Color m_Touch = new(0.25f, 0.55f, 1.1f);

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        Quaternion[] m_Rest;
        Renderer[] m_Renderers;
        float[] m_Frequency;
        float[] m_Magnitude;
        float[] m_Press;
        float m_Peak = 1e-4f;
        MaterialPropertyBlock m_Block;
        System.Random m_Random;

        void Awake()
        {
            int count = m_Keys != null ? m_Keys.Length : 0;
            m_Rest = new Quaternion[count];
            m_Renderers = new Renderer[count];
            m_Frequency = new float[count];
            m_Magnitude = new float[count];
            m_Press = new float[count];
            m_Block = new MaterialPropertyBlock();
            m_Random = new System.Random(GetInstanceID());

            for (int k = 0; k < count; k++)
            {
                if (m_Keys[k] == null)
                    continue;

                m_Rest[k] = m_Keys[k].localRotation;
                m_Renderers[k] = m_Keys[k].GetComponentInChildren<Renderer>();
                m_Frequency[k] = 440f * Mathf.Pow(2f, (m_LowestNote + k - 69) / 12f);
            }
        }

        protected override void Animate(float dt)
        {
            var analyser = m_Haunting.Analyser;
            var spectrum = analyser != null ? analyser.Spectrum : null;
            if (spectrum == null || spectrum.Length < 2 || m_Keys == null)
                return;

            float binHz = analyser.BinHz;
            float quarterTone = Mathf.Pow(2f, 1f / 24f);
            float loudest = 0f;

            for (int k = 0; k < m_Keys.Length; k++)
            {
                float f = m_Frequency[k];
                int lo = Mathf.Clamp(Mathf.FloorToInt(f / quarterTone / binHz), 1, spectrum.Length - 1);
                int hi = Mathf.Clamp(Mathf.CeilToInt(f * quarterTone / binHz), lo, spectrum.Length - 1);

                float magnitude = 0f;
                for (int b = lo; b <= hi; b++)
                    magnitude = Mathf.Max(magnitude, spectrum[b]);

                // The same treble lift the bands get: music falls off toward the top.
                m_Magnitude[k] = magnitude * Mathf.Sqrt(f / 1000f);
                loudest = Mathf.Max(loudest, m_Magnitude[k]);
            }

            float decay = Mathf.Pow(0.5f, dt / Mathf.Max(0.1f, m_PeakHalfLife));
            m_Peak = Mathf.Max(loudest, m_Peak * decay, 1e-4f);

            float attack = 1f - Mathf.Exp(-dt / 0.02f);
            float release = 1f - Mathf.Exp(-dt / 0.12f);

            for (int k = 0; k < m_Keys.Length; k++)
            {
                if (m_Keys[k] == null)
                    continue;

                float target = 0f;
                bool peak = (k == 0 || m_Magnitude[k] >= m_Magnitude[k - 1]) &&
                            (k == m_Keys.Length - 1 || m_Magnitude[k] >= m_Magnitude[k + 1]);

                if (peak && m_Magnitude[k] > 0f)
                {
                    float db = 20f * Mathf.Log10(m_Magnitude[k] / m_Peak);
                    float level = Mathf.Clamp01(1f + db / Mathf.Max(1f, m_RangeDb));
                    target = Mathf.InverseLerp(m_PressFrom, m_PressFull, level) * Woken;
                }

                m_Press[k] = Mathf.Lerp(m_Press[k], target, target > m_Press[k] ? attack : release);
                Show(k);
            }
        }

        /// <summary>The last gasp: a single chord struck in the silence.</summary>
        public override void Nudge(float strength)
        {
            if (m_Keys == null || m_Keys.Length < 12)
                return;

            int root = 27 + m_Random.Next(Mathf.Max(1, m_Keys.Length - 48));
            foreach (int interval in new[] { 0, 3, 7 })
            {
                int k = root + interval;
                if (k < m_Keys.Length)
                    m_Press[k] = strength;
            }
        }

        void Show(int k)
        {
            m_Keys[k].localRotation = m_Rest[k] * Quaternion.AngleAxis(-m_Press[k] * m_PressAngle, Vector3.right);

            if (m_Renderers[k] == null)
                return;

            m_Block.SetColor(k_EmissionColor, m_Touch * m_Press[k]);
            m_Renderers[k].SetPropertyBlock(m_Block);
        }
    }
}

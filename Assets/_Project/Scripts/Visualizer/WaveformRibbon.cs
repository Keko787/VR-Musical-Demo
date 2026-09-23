using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// An oscilloscope trace hung in the air: a glowing line strung between two points in the room
    /// and displaced sideways by the live waveform. Three of these at different heights and
    /// headings put the music's shape in the volume at arm's reach, where the deck says 3D content
    /// holds up best.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class WaveformRibbon : MonoBehaviour
    {
        [SerializeField] AudioAnalyser m_Analyser;

        [Header("Placement")]
        [SerializeField, Tooltip("Where the trace starts, in room metres.")]
        Vector3 m_From = new(-1.7f, 1.3f, 0f);

        [SerializeField, Tooltip("Where it ends.")]
        Vector3 m_To = new(1.7f, 1.3f, 0f);

        [SerializeField, Tooltip("Direction the waveform displaces the line along.")]
        Vector3 m_Axis = Vector3.up;

        [SerializeField, Tooltip("A second displacement axis fed a delayed copy of the waveform, which twists the trace " +
            "into a ribbon instead of a flat line. Its length scales that displacement; zero for a flat trace.")]
        Vector3 m_SecondAxis = Vector3.zero;

        [SerializeField, Tooltip("Samples the second axis lags the first by.")]
        int m_SecondLag = 32;

        [Header("Trace")]
        [SerializeField, Range(16, 512)] int m_Points = 160;
        [SerializeField, Tooltip("Metres of displacement at full scale.")]
        float m_Amplitude = 0.3f;

        [SerializeField, Tooltip("Seconds the drawn trace takes to follow the live one. A little makes it read as a wave " +
            "rather than a flicker; too much smears the transients away.")]
        float m_Smoothing = 0.03f;

        [Header("Look")]
        [SerializeField] Color m_Color = new(0.4f, 0.9f, 1f);
        [SerializeField] float m_Width = 0.012f;
        [SerializeField] float m_LoudWidth = 0.03f;
        [SerializeField] float m_Brightness = 1.6f;
        [SerializeField] float m_BeatBrightness = 0.8f;

        public float Opacity { get; set; } = 1f;
        public float Intensity { get; set; } = 1f;
        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        static readonly int k_Color = Shader.PropertyToID("_Color");

        LineRenderer m_Line;
        MaterialPropertyBlock m_Block;
        Vector3[] m_Positions = new Vector3[0];
        float[] m_Shown = new float[0];
        float[] m_ShownLag = new float[0];

        void Awake()
        {
            m_Line = GetComponent<LineRenderer>();
            m_Line.useWorldSpace = false;
            m_Block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (m_Analyser == null)
                return;

            var wave = m_Analyser.Waveform;
            if (wave.Length == 0)
                return;

            if (m_Positions.Length != m_Points)
            {
                m_Positions = new Vector3[m_Points];
                m_Shown = new float[m_Points];
                m_ShownLag = new float[m_Points];
                m_Line.positionCount = m_Points;
            }

            float dt = Time.deltaTime;
            float follow = m_Smoothing > 0f ? 1f - Mathf.Exp(-dt / m_Smoothing) : 1f;
            float idle = m_Analyser.Idle;
            float amplitude = m_Amplitude * (1f - 0.8f * idle);

            // The trace shows the most recent stretch of the output, one sample per point, averaged
            // over the samples between points so a dense waveform does not alias into a jitter.
            int stride = Mathf.Max(1, wave.Length / m_Points);
            int first = wave.Length - stride * m_Points;
            var axis = m_Axis.normalized;
            var second = m_SecondAxis;   // not normalised: its length is the second axis's share of the amplitude

            for (int k = 0; k < m_Points; k++)
            {
                float sample = Average(wave, first + k * stride, stride);
                float lagged = Average(wave, first + k * stride - m_SecondLag, stride);

                m_Shown[k] += (sample - m_Shown[k]) * follow;
                m_ShownLag[k] += (lagged - m_ShownLag[k]) * follow;

                float t = m_Points > 1 ? k / (float)(m_Points - 1) : 0f;
                m_Positions[k] = Vector3.Lerp(m_From, m_To, t)
                                 + axis * (m_Shown[k] * amplitude)
                                 + second * (m_ShownLag[k] * amplitude);
            }

            m_Line.SetPositions(m_Positions);

            float loud = m_Analyser.Loudness;
            float beat = m_Analyser.BeatPulse;
            m_Line.widthMultiplier = Mathf.Lerp(m_Width, m_LoudWidth, loud);

            float level = m_Brightness * Intensity * Opacity * (0.35f + 0.65f * loud) * (1f + m_BeatBrightness * beat) * (1f - 0.7f * idle);
            m_Block.SetColor(k_Color, m_Color * level);
            m_Line.SetPropertyBlock(m_Block);
        }

        static float Average(float[] wave, int start, int count)
        {
            float sum = 0f;
            int n = 0;
            for (int i = start; i < start + count; i++)
            {
                if (i < 0 || i >= wave.Length)
                    continue;

                sum += wave[i];
                n++;
            }

            return n > 0 ? sum / n : 0f;
        }
    }
}

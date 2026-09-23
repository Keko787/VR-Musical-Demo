using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>How a panel's tubes map onto the analyser's bands, left to right across the panel.</summary>
    public enum TubeLayout
    {
        /// <summary>Bass on the left, treble on the right.</summary>
        Ascending,

        /// <summary>Treble on the left, bass on the right. The right wall's mirror of the left's.</summary>
        Descending,

        /// <summary>Bass in the middle, treble at both edges. Symmetric, which is how a front wall reads best.</summary>
        Mirrored,
    }

    /// <summary>
    /// One CAVE screen's worth of the 2D visualizer: a row of glass tubes drawn flat on the surface,
    /// each holding a column of water whose level is one frequency band. The look is all in the
    /// <c>VRMusic/WaterTubes</c> shader; this side owns what a shader cannot — the water's motion.
    /// </summary>
    /// <remarks>
    /// Each tube's level is a mass on a spring chasing its band, so it overshoots a little and
    /// settles, and the surface is a second, lighter oscillator that the level's acceleration kicks,
    /// so a sudden rise sloshes and a held note goes still. Both are per tube and both are passed to
    /// the shader as arrays through a MaterialPropertyBlock, so the four panels share one material.
    /// The panel is 2D on purpose: it sits a few millimetres inside the screen plane, so everyone in
    /// the room sees it in the same place, glasses or not.
    /// </remarks>
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterTubePanel : MonoBehaviour
    {
        public const int MaxTubes = AudioAnalyser.MaxBands;

        [SerializeField] AudioAnalyser m_Analyser;

        [Header("Layout")]
        [SerializeField, Range(1, MaxTubes)] int m_TubeCount = 24;
        [SerializeField] TubeLayout m_Layout = TubeLayout.Mirrored;

        [SerializeField, Tooltip("The screen's width and height in metres. Set by the builder from the CAVE config; the shader " +
            "needs it to draw round tubes on a quad that is not square.")]
        Vector2 m_PanelSize = new(3.556f, 2.2225f);

        [SerializeField, Range(0.2f, 1f), Tooltip("Tube diameter as a fraction of the space each tube gets.")]
        float m_TubeFill = 0.7f;

        [SerializeField, Tooltip("Metres of panel left clear above and below the tubes.")]
        float m_EndMargin = 0.08f;

        [Header("Water")]
        [SerializeField, Tooltip("Spring stiffness of the water level toward its band. Higher is snappier.")]
        float m_Stiffness = 80f;

        [SerializeField, Range(0.1f, 1.5f), Tooltip("Damping ratio of the level spring. Under 1 overshoots, which reads as weight.")]
        float m_Damping = 0.55f;

        [SerializeField, Tooltip("Natural frequency of the surface slosh, in Hz. A wide tube sloshes slower than a narrow one.")]
        float m_SloshHz = 1.4f;

        [SerializeField, Range(0.02f, 1f), Tooltip("Damping ratio of the slosh. Low keeps a wave going after the level has settled.")]
        float m_SloshDamping = 0.12f;

        [SerializeField, Tooltip("How hard the level's acceleration kicks the surface.")]
        float m_SloshDrive = 0.012f;

        [SerializeField, Tooltip("Largest slosh, as a fraction of the tube radius, so a drop does not throw the surface out of the glass.")]
        float m_MaxSlosh = 0.6f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Where the water sits when nothing is playing, as a fraction of the tube.")]
        float m_IdleLevel = 0.18f;

        [Header("Look")]
        [SerializeField, Tooltip("Water colour by band, bass at 0 and treble at 1. Left plain white for the builder's blues.")]
        Gradient m_Palette;

        [SerializeField, Tooltip("The wash behind the tubes cycles between these two over the wash period.")]
        Color m_WashA = new(0.01f, 0.02f, 0.08f);

        [SerializeField] Color m_WashB = new(0.06f, 0.0f, 0.07f);

        [SerializeField] float m_WashCycleSeconds = 45f;

        [SerializeField, Range(0f, 1f), Tooltip("How much the bass lifts the wash. The rave part of the show.")]
        float m_WashBass = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip("How much a beat flashes the wash. Zero in low-intensity mode.")]
        float m_BeatFlash = 0.3f;

        /// <summary>0 hides the tubes and leaves the wash; the visualizer fades this on a mode change.</summary>
        public float Opacity { get; set; } = 1f;

        /// <summary>Overall brightness multiplier.</summary>
        public float Intensity { get; set; } = 1f;

        /// <summary>Off in low-intensity mode: the wash still breathes with the bass, it just does not flash.</summary>
        public bool BeatFlashEnabled { get; set; } = true;

        public int TubeCount => m_TubeCount;
        public TubeLayout Layout => m_Layout;
        public Vector2 PanelSize => m_PanelSize;
        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        static readonly int k_Level = Shader.PropertyToID("_Level");
        static readonly int k_Slosh = Shader.PropertyToID("_Slosh");
        static readonly int k_Glow = Shader.PropertyToID("_Glow");
        static readonly int k_Color = Shader.PropertyToID("_Color");
        static readonly int k_PanelSize = Shader.PropertyToID("_PanelSize");
        static readonly int k_TubeCount = Shader.PropertyToID("_TubeCount");
        static readonly int k_TubeFill = Shader.PropertyToID("_TubeFill");
        static readonly int k_EndMargin = Shader.PropertyToID("_EndMargin");
        static readonly int k_Opacity = Shader.PropertyToID("_Opacity");
        static readonly int k_Intensity = Shader.PropertyToID("_Intensity");
        static readonly int k_WashColor = Shader.PropertyToID("_WashColor");
        static readonly int k_Beat = Shader.PropertyToID("_Beat");
        static readonly int k_Bass = Shader.PropertyToID("_Bass");

        // Fixed at the shader's array size: Unity sizes a property block's array on the first set
        // and rejects anything longer afterwards.
        readonly float[] m_Level = new float[MaxTubes];
        readonly float[] m_Velocity = new float[MaxTubes];
        readonly float[] m_Slosh = new float[MaxTubes];
        readonly float[] m_SloshVelocity = new float[MaxTubes];
        readonly float[] m_Glow = new float[MaxTubes];
        readonly Vector4[] m_Colors = new Vector4[MaxTubes];

        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        int m_ColouredFor = -1;

        void Awake()
        {
            m_Renderer = GetComponent<MeshRenderer>();
            m_Block = new MaterialPropertyBlock();

            if (Palettes.IsUnset(m_Palette))
                m_Palette = DefaultPalette();

            for (int i = 0; i < MaxTubes; i++)
                m_Level[i] = m_IdleLevel;
        }

        void OnValidate()
        {
            m_TubeCount = Mathf.Clamp(m_TubeCount, 1, MaxTubes);
            m_ColouredFor = -1;
        }

        /// <summary>Deep water to bright shallows: navy at the bass end, aqua-white at the treble.</summary>
        public static Gradient DefaultPalette()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.05f, 0.25f, 0.95f), 0f),
                    new GradientColorKey(new Color(0.05f, 0.6f, 1f), 0.4f),
                    new GradientColorKey(new Color(0.2f, 0.95f, 0.95f), 0.75f),
                    new GradientColorKey(new Color(0.75f, 1f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);   // a hitch must not fling the springs
            if (dt <= 0f || m_Analyser == null)
                return;

            if (m_ColouredFor != m_TubeCount)
                Recolour();

            float idle = m_Analyser.Idle;
            float beat = BeatFlashEnabled ? m_Analyser.BeatPulse : 0f;
            float time = Time.time;

            // Spring constants from stiffness and damping ratio: c = 2ζ√k.
            float k = Mathf.Max(1f, m_Stiffness);
            float c = 2f * m_Damping * Mathf.Sqrt(k);
            float w = 2f * Mathf.PI * Mathf.Max(0.05f, m_SloshHz);
            float sloshC = 2f * m_SloshDamping * w;

            for (int i = 0; i < m_TubeCount; i++)
            {
                float band = m_Analyser.BandAt(BandIndex(i));

                // When the room falls silent the water settles to a low, slow breathing level
                // rather than draining to nothing, so the walls do not go dead between tracks.
                float breathe = m_IdleLevel + 0.06f * Mathf.Sin(time * 0.6f + i * 0.35f);
                float target = Mathf.Lerp(band * (1f - m_IdleLevel * 0.5f) + m_IdleLevel * 0.5f, breathe, idle);

                float accel = (target - m_Level[i]) * k - m_Velocity[i] * c;
                m_Velocity[i] += accel * dt;
                m_Level[i] = Mathf.Clamp01(m_Level[i] + m_Velocity[i] * dt);

                float sloshAccel = -m_Slosh[i] * w * w - m_SloshVelocity[i] * sloshC + accel * m_SloshDrive;
                m_SloshVelocity[i] += sloshAccel * dt;
                m_Slosh[i] = Mathf.Clamp(m_Slosh[i] + m_SloshVelocity[i] * dt, -m_MaxSlosh, m_MaxSlosh);

                // Rising water glows at the surface, and every tube catches the beat.
                float rise = Mathf.Clamp01(m_Velocity[i] * 1.5f);
                m_Glow[i] = Mathf.Max(rise, beat * (0.3f + 0.7f * band)) * Intensity;
            }

            for (int i = m_TubeCount; i < MaxTubes; i++)
                m_Level[i] = m_Slosh[i] = m_Glow[i] = 0f;

            m_Block.SetFloatArray(k_Level, m_Level);
            m_Block.SetFloatArray(k_Slosh, m_Slosh);
            m_Block.SetFloatArray(k_Glow, m_Glow);
            m_Block.SetVectorArray(k_Color, m_Colors);
            m_Block.SetVector(k_PanelSize, new Vector4(m_PanelSize.x, m_PanelSize.y, 0f, 0f));
            m_Block.SetFloat(k_TubeCount, m_TubeCount);
            m_Block.SetFloat(k_TubeFill, m_TubeFill);
            m_Block.SetFloat(k_EndMargin, m_EndMargin);
            m_Block.SetFloat(k_Opacity, Mathf.Clamp01(Opacity));
            m_Block.SetFloat(k_Intensity, Mathf.Max(0f, Intensity));
            m_Block.SetColor(k_WashColor, Wash(time, beat, idle));
            m_Block.SetFloat(k_Beat, beat);
            m_Block.SetFloat(k_Bass, m_Analyser.Bass);
            m_Renderer.SetPropertyBlock(m_Block);
        }

        /// <summary>The analyser band a tube reads, as a fractional index the analyser interpolates.</summary>
        float BandIndex(int tube)
        {
            int bands = m_Analyser != null ? m_Analyser.BandCount : 1;
            float span = Mathf.Max(1, bands - 1);
            float t;

            switch (m_Layout)
            {
                case TubeLayout.Descending:
                    t = m_TubeCount > 1 ? 1f - tube / (float)(m_TubeCount - 1) : 0f;
                    break;
                case TubeLayout.Mirrored:
                    float half = (m_TubeCount - 1) * 0.5f;
                    t = half > 0f ? Mathf.Abs(tube - half) / half : 0f;
                    break;
                default:
                    t = m_TubeCount > 1 ? tube / (float)(m_TubeCount - 1) : 0f;
                    break;
            }

            return t * span;
        }

        void Recolour()
        {
            int bands = m_Analyser != null ? m_Analyser.BandCount : m_TubeCount;
            float span = Mathf.Max(1, bands - 1);

            for (int i = 0; i < MaxTubes; i++)
            {
                float t = i < m_TubeCount ? BandIndex(i) / span : 0f;
                var colour = m_Palette.Evaluate(t);
                m_Colors[i] = new Vector4(colour.r, colour.g, colour.b, 1f);
            }

            m_ColouredFor = m_TubeCount;
        }

        /// <summary>
        /// The colour behind the tubes: a slow drift between two dark colours, lifted by the bass,
        /// flashed by the beat. Time-based rather than stateful, so every panel computes the same
        /// wash from the same analyser and the four screens agree.
        /// </summary>
        Color Wash(float time, float beat, float idle)
        {
            float cycle = m_WashCycleSeconds > 0f ? 0.5f - 0.5f * Mathf.Cos(time * 2f * Mathf.PI / m_WashCycleSeconds) : 0f;
            var wash = Color.Lerp(m_WashA, m_WashB, cycle);

            float bass = m_Analyser != null ? m_Analyser.Bass : 0f;
            float lift = 1f + m_WashBass * 3f * bass * (1f - idle) + m_BeatFlash * 4f * beat;
            wash *= lift * Intensity;
            wash.a = 1f;
            return wash;
        }
    }
}

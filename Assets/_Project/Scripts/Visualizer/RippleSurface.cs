using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>How one kind of drop makes its rings.</summary>
    [System.Serializable]
    public struct RippleKind
    {
        [Tooltip("Metres between crests.")]
        public float Wavelength;

        [Tooltip("Metres a second the rings spread.")]
        public float Speed;

        [Tooltip("Seconds until the rings have died away.")]
        public float Life;

        [Tooltip("Height of the first crest at full strength. The relief, the glow and the colour all go with it.")]
        public float Height;

        [Tooltip("Where on the colour wheel this kind's rings start, 0–1, before the wheel turns: red, yellow, green, cyan, blue, violet.")]
        public float Hue;

        [Range(0f, 1f), Tooltip("The share of these drops that land on the floor. The rest land on the walls, a bigger wall more often.")]
        public float FloorShare;

        public RippleKind(float wavelength, float speed, float life, float height, float hue, float floorShare)
        {
            Wavelength = wavelength;
            Speed = speed;
            Life = life;
            Height = height;
            Hue = hue;
            FloorShare = floorShare;
        }
    }

    /// <summary>
    /// The second 2D show: the four screens as one sheet of rainbow liquid the music drops things
    /// into. A kick lands a big drop, mostly on the floor, whose long rings roll across the room; a
    /// hit in the mids lands a smaller one on a wall; the hats and cymbals patter small quick ones
    /// everywhere. The rings are rainbows, each crest a step further round the colour wheel, laid
    /// over a gradient that lies across the whole room and turns slowly, and they stand up off the
    /// surface in relief, lit by a lamp inside the room. The look is the <c>VRMusic/RippleSurface</c>
    /// shader's; this side owns the drops.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screens are one surface here, not four. Each drop's centre is handed to every panel
    /// <em>unfolded</em> into that panel's plane — the panel it landed on folded flat about the edge
    /// the two share — so a ring's distance is measured along the walls and floor, and a ring dropped
    /// on the floor climbs the wall at the speed it crossed the floor, unbroken at the seam. The two
    /// side walls face each other with no shared edge, so a drop on one reaches the other two ways
    /// round, over the floor and over the front wall, and the shader takes the nearer.
    /// </para>
    /// <para>
    /// The panels sit a few millimetres in front of the water panels and are see-through: alone, the
    /// rings run over the water panels' dark wash, which stays on in every mode; layered with the
    /// tubes, over the tubes. It is 2D on purpose, like the tubes: drawn on the screens, lit from a
    /// fixed lamp and seen from a fixed eye, so everyone in the room sees the same picture.
    /// </para>
    /// </remarks>
    public class RippleSurface : MonoBehaviour
    {
        /// <summary>Drops alive at once; the shader's array size.</summary>
        public const int MaxDrops = 16;

        const int k_MaxPanels = 8;
        const float k_Far = SurfaceMap.Far;

        [SerializeField] AudioAnalyser m_Analyser;

        [SerializeField, Tooltip("One quad per projected screen, on the VRMusic/RippleSurface material, each a child of this object. " +
            "Filled in by the builder.")]
        MeshRenderer[] m_Panels;

        [Header("Drops")]
        [SerializeField, Tooltip("A kick: a big drop, long rolling rings, mostly on the floor.")]
        RippleKind m_Kick = new(0.42f, 1.2f, 3.2f, 1f, 0f, 0.5f);

        [SerializeField, Tooltip("A hit in the mids (snare, chords, a voice coming in): a middling drop, mostly on a wall.")]
        RippleKind m_Mid = new(0.24f, 0.85f, 2.4f, 0.65f, 0.3f, 0.15f);

        [SerializeField, Tooltip("A hit in the highs (hats, cymbals): a small quick drop, anywhere.")]
        RippleKind m_High = new(0.13f, 0.6f, 1.4f, 0.4f, 0.62f, 0.35f);

        [SerializeField, Tooltip("While nothing is landing — paused, between tracks, a quiet passage: a soft drop now and then, " +
            "so the room keeps breathing.")]
        RippleKind m_Drizzle = new(0.3f, 0.55f, 3.6f, 0.45f, 0.8f, 0.4f);

        [SerializeField, Tooltip("Seconds between drizzle drops, on average.")]
        float m_DrizzleSeconds = 1.4f;

        [SerializeField, Range(0.05f, 0.5f), Tooltip("How far the mids or the highs have to jump over their own recent level to count " +
            "as a hit, in their 0–1 range.")]
        float m_OnsetJump = 0.16f;

        [SerializeField, Tooltip("Fewest seconds between two hits in the mids (x) and in the highs (y).")]
        Vector2 m_OnsetGap = new(0.2f, 0.12f);

        [SerializeField, Tooltip("Metres a drop's centre keeps clear of a screen's edges.")]
        float m_EdgeMargin = 0.2f;

        [Header("Colour")]
        [SerializeField, Tooltip("Seconds for the colour wheel to turn once. Every ring, and the gradient behind them, shifts through " +
            "the rainbow over this.")]
        float m_HueCycleSeconds = 90f;

        [SerializeField, Tooltip("Metres of surface one full rainbow of the gradient across the room spans.")]
        float m_GradientSpan = 6f;

        [SerializeField, Tooltip("Seconds for the gradient's direction to come round once.")]
        float m_GradientTurnSeconds = 140f;

        [SerializeField, Tooltip("How far each kick turns the wheel on, so a busy track walks the colours faster.")]
        float m_HuePerKick = 0.012f;

        [Header("Light")]
        [SerializeField, Tooltip("The lamp that lights the relief, in the room's frame. Inside the room, so it lights every screen.")]
        Vector3 m_Lamp = new(0f, 2.6f, -0.7f);

        [SerializeField, Tooltip("Where the glints are seen from, in the room's frame: a nominal eye, not the tracked one, so every eye " +
            "and every viewer sees the same picture.")]
        Vector3 m_Eye = new(0f, 1.6f, -0.3f);

        [Header("Swell")]
        [SerializeField, Tooltip("Height of the slow swell that keeps the surface moving between drops, with nothing playing.")]
        float m_IdleSwell = 0.28f;

        [SerializeField, Tooltip("How much the bass raises the swell while music plays.")]
        float m_BassSwell = 0.45f;

        [SerializeField, Range(0f, 1f), Tooltip("How much a beat brightens the whole surface for a moment. Off in low intensity.")]
        float m_BeatLift = 0.35f;

        /// <summary>0 hides the ripples; the visualizer fades this on a mode change.</summary>
        public float Opacity { get; set; } = 1f;

        /// <summary>Overall brightness multiplier.</summary>
        public float Intensity { get; set; } = 1f;

        /// <summary>Off in low-intensity mode: the drops still land, the surface just does not brighten on the beat.</summary>
        public bool BeatFlashEnabled { get; set; } = true;

        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        static readonly int k_DropCount = Shader.PropertyToID("_DropCount");
        static readonly int k_DropPos = Shader.PropertyToID("_DropPos");
        static readonly int k_DropWave = Shader.PropertyToID("_DropWave");
        static readonly int k_PanelSize = Shader.PropertyToID("_PanelSize");
        static readonly int k_PanelCentre = Shader.PropertyToID("_PanelCentre");
        static readonly int k_PanelRight = Shader.PropertyToID("_PanelRight");
        static readonly int k_PanelUp = Shader.PropertyToID("_PanelUp");
        static readonly int k_LightPos = Shader.PropertyToID("_LightPos");
        static readonly int k_EyePos = Shader.PropertyToID("_EyePos");
        static readonly int k_HueField = Shader.PropertyToID("_HueField");
        static readonly int k_Ambient = Shader.PropertyToID("_Ambient");
        static readonly int k_Opacity = Shader.PropertyToID("_Opacity");
        static readonly int k_Intensity = Shader.PropertyToID("_Intensity");

        struct Drop
        {
            public bool Alive;
            public Vector3 Position;
            public float Born;
            public RippleKind Kind;
            public float Strength;
            public float Hue;
        }

        /// <summary>A band's hit detector: its recent level, and whether it has fallen back enough to fire again.</summary>
        struct Onset
        {
            public float Average;
            public bool Armed;
            public float Next;
        }

        readonly Drop[] m_Drops = new Drop[MaxDrops];

        // Each drop's centre on each panel, worked out once when it lands.
        readonly Vector4[,] m_Images = new Vector4[MaxDrops, k_MaxPanels];

        // What goes to the shader, at the shader's array size: Unity sizes a property block's array on
        // the first set and rejects anything longer afterwards.
        readonly Vector4[][] m_Pos = new Vector4[k_MaxPanels][];
        readonly Vector4[] m_Wave = new Vector4[MaxDrops];

        SurfaceMap m_Map;
        MaterialPropertyBlock m_Block;
        readonly System.Random m_Random = new();
        Onset m_Mids;
        Onset m_Highs;
        float m_HueOffset;
        float m_NextDrizzle;
        float m_LastDrop = -100f;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            for (int i = 0; i < k_MaxPanels; i++)
                m_Pos[i] = new Vector4[MaxDrops];
        }

        void OnEnable() => ReadFaces();

        void Update()
        {
            if (m_Analyser == null || m_Panels == null || m_Panels.Length == 0)
                return;

            if (m_Map == null || m_Map.Count != Mathf.Min(m_Panels.Length, k_MaxPanels))
                ReadFaces();

            float now = Time.time;
            Listen(now, Mathf.Min(Time.deltaTime, 0.05f));
            Upload(now);
        }

        // ---------------------------------------------------------------- the music

        /// <summary>A kick, a hit in the mids or a hit in the highs each land a drop of their kind; quiet brings the drizzle.</summary>
        void Listen(float now, float dt)
        {
            bool playing = !m_Analyser.Silent && m_Analyser.Idle < 0.5f;
            bool mid = Hit(ref m_Mids, m_Analyser.Mid, m_OnsetGap.x, now, dt);
            bool high = Hit(ref m_Highs, m_Analyser.High, m_OnsetGap.y, now, dt);

            if (playing)
            {
                if (m_Analyser.Beat)
                {
                    Spawn(m_Kick, 0.55f + 0.45f * m_Analyser.BeatStrength, now);
                    m_HueOffset += m_HuePerKick;
                }

                if (mid)
                    Spawn(m_Mid, 0.5f + 0.5f * m_Analyser.Mid, now);
                if (high)
                    Spawn(m_High, 0.4f + 0.6f * m_Analyser.High, now);
            }

            // Nothing playing, or nothing landing for a while: a soft drop now and then.
            bool quiet = !playing || now - m_LastDrop > m_DrizzleSeconds * 1.5f;
            if (quiet && now >= m_NextDrizzle)
            {
                Spawn(m_Drizzle, 0.6f + 0.4f * Random01(), now);
                m_NextDrizzle = now + Mathf.Max(0.2f, m_DrizzleSeconds) * (0.6f + 0.8f * Random01());
            }
        }

        /// <summary>
        /// Whether <paramref name="value"/> has just jumped well clear of its own recent level: a hit.
        /// It has to fall back toward that level before it can fire again, and not sooner than
        /// <paramref name="gap"/> after the last, so a held chord is one hit, not a stream of them.
        /// </summary>
        bool Hit(ref Onset onset, float value, float gap, float now, float dt)
        {
            onset.Average = Mathf.Lerp(onset.Average, value, 1f - Mathf.Exp(-dt / 0.4f));
            float over = value - onset.Average;

            if (!onset.Armed)
            {
                if (over < m_OnsetJump * 0.4f)
                    onset.Armed = true;
                return false;
            }

            if (over < m_OnsetJump || value < 0.2f || now < onset.Next)
                return false;

            onset.Armed = false;
            onset.Next = now + gap;
            return true;
        }

        // ---------------------------------------------------------------- drops

        void Spawn(in RippleKind kind, float strength, float now)
        {
            int face = PickFace(kind.FloorShare);
            if (face < 0)
                return;

            var position = PickSpot(face, now);
            int slot = FreeSlot(now);
            float wheel = (m_HueCycleSeconds > 0f ? now / m_HueCycleSeconds : 0f) + m_HueOffset;

            m_Drops[slot] = new Drop
            {
                Alive = true,
                Position = position,
                Born = now,
                Kind = kind,
                Strength = strength,
                Hue = kind.Hue + wheel + (Random01() - 0.5f) * 0.1f,
            };

            for (int panel = 0; panel < m_Map.Count; panel++)
                m_Images[slot, panel] = m_Map[panel].Valid ? m_Map.ImageOn(panel, position, face) : new Vector4(k_Far, k_Far, k_Far, k_Far);

            m_LastDrop = now;
        }

        /// <summary>The floor <paramref name="floorShare"/> of the time; otherwise a wall, a bigger wall more often.</summary>
        int PickFace(float floorShare)
        {
            int floor = m_Map.Floor;
            float walls = 0f;
            for (int i = 0; i < m_Map.Count; i++)
                if (m_Map[i].Valid && !m_Map[i].Floor)
                    walls += m_Map[i].Size.x * m_Map[i].Size.y;

            if (floor >= 0 && (walls <= 0f || Random01() < floorShare))
                return floor;

            float pick = Random01() * walls;
            int last = -1;
            for (int i = 0; i < m_Map.Count; i++)
            {
                if (!m_Map[i].Valid || m_Map[i].Floor)
                    continue;
                last = i;
                pick -= m_Map[i].Size.x * m_Map[i].Size.y;
                if (pick <= 0f)
                    return i;
            }

            return last >= 0 ? last : floor;
        }

        /// <summary>A spot on the face, clear of its edges, and of the three tried the one farthest from the drops still running.</summary>
        Vector3 PickSpot(int face, float now)
        {
            var f = m_Map[face];
            float mx = Mathf.Min(m_EdgeMargin, f.Size.x * 0.25f);
            float my = Mathf.Min(m_EdgeMargin, f.Size.y * 0.25f);

            var best = f.Centre;
            float bestScore = -1f;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float u = Mathf.Lerp(-f.Size.x * 0.5f + mx, f.Size.x * 0.5f - mx, Random01());
                float v = Mathf.Lerp(-f.Size.y * 0.5f + my, f.Size.y * 0.5f - my, Random01());
                var spot = f.Centre + f.Right * u + f.Up * v;

                float nearest = float.MaxValue;
                foreach (var drop in m_Drops)
                    if (drop.Alive && now - drop.Born < drop.Kind.Life)
                        nearest = Mathf.Min(nearest, (drop.Position - spot).sqrMagnitude);

                if (nearest > bestScore)
                {
                    bestScore = nearest;
                    best = spot;
                }
            }

            return best;
        }

        /// <summary>A slot for a new drop: a free one, or else the one whose rings have died down the most.</summary>
        int FreeSlot(float now)
        {
            int weakest = 0;
            float weakestHeight = float.MaxValue;
            for (int i = 0; i < MaxDrops; i++)
            {
                if (!m_Drops[i].Alive)
                    return i;

                float height = HeightOf(m_Drops[i], now - m_Drops[i].Born);
                if (height < weakestHeight)
                {
                    weakestHeight = height;
                    weakest = i;
                }
            }

            return weakest;
        }

        /// <summary>
        /// How high a drop's leading crest stands <paramref name="age"/> seconds after it landed: up
        /// at once, falling away with time and with the ring's spread (a ring's energy is spread round
        /// a longer circle as it grows), and gone at the end of its life. Zero or less is dead.
        /// </summary>
        static float HeightOf(in Drop drop, float age)
        {
            float life = Mathf.Max(0.1f, drop.Kind.Life);
            if (age >= life || age < 0f)
                return 0f;

            float radius = drop.Kind.Speed * age;
            float landing = Mathf.Clamp01(age / 0.06f);
            float decay = Mathf.Exp(-2f * age / life);
            float end = 1f - Smooth01((age - 0.7f * life) / (0.3f * life));
            float spread = 1f / Mathf.Sqrt(1f + radius / 0.35f);
            return drop.Kind.Height * drop.Strength * landing * decay * end * spread;
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        float Random01() => (float)m_Random.NextDouble();

        // ---------------------------------------------------------------- the shader

        void Upload(float now)
        {
            int count = 0;
            for (int slot = 0; slot < MaxDrops; slot++)
            {
                ref var drop = ref m_Drops[slot];
                if (!drop.Alive)
                    continue;

                // Not by its height, which starts from nothing on the frame it lands.
                float age = now - drop.Born;
                if (age < 0f || age >= Mathf.Max(0.1f, drop.Kind.Life))
                {
                    drop.Alive = false;
                    continue;
                }

                float height = HeightOf(drop, age);
                m_Wave[count] = new Vector4(drop.Kind.Speed * age, height, Mathf.Max(0.02f, drop.Kind.Wavelength), drop.Hue);
                for (int panel = 0; panel < m_Map.Count; panel++)
                    m_Pos[panel][count] = m_Images[slot, panel];
                count++;
            }

            float idle = m_Analyser.Idle;
            float swell = Mathf.Lerp(m_IdleSwell * 0.6f + m_BassSwell * m_Analyser.Bass, m_IdleSwell, idle);
            float beat = BeatFlashEnabled ? m_Analyser.BeatPulse : 0f;
            float lift = 1f + 0.3f * m_Analyser.Loudness * (1f - idle) + m_BeatLift * beat;

            // The gradient: one rainbow per m_GradientSpan metres, along a direction that comes round
            // slowly and leans upward, so the walls and the floor carry different parts of it.
            float wheel = (m_HueCycleSeconds > 0f ? now / m_HueCycleSeconds : 0f) + m_HueOffset;
            float turn = m_GradientTurnSeconds > 0f ? now * 2f * Mathf.PI / m_GradientTurnSeconds : 0f;
            var along = new Vector3(Mathf.Cos(turn), 0.5f, Mathf.Sin(turn)).normalized / Mathf.Max(0.5f, m_GradientSpan);

            m_Block.Clear();
            m_Block.SetFloat(k_DropCount, count);
            m_Block.SetVectorArray(k_DropWave, m_Wave);
            m_Block.SetVector(k_LightPos, m_Lamp);
            m_Block.SetVector(k_EyePos, m_Eye);
            m_Block.SetVector(k_HueField, new Vector4(along.x, along.y, along.z, wheel - Mathf.Floor(wheel)));
            m_Block.SetVector(k_Ambient, new Vector4(swell, lift, now, 0f));
            m_Block.SetFloat(k_Opacity, Mathf.Clamp01(Opacity));
            m_Block.SetFloat(k_Intensity, Mathf.Max(0f, Intensity));

            for (int panel = 0; panel < m_Map.Count; panel++)
            {
                var face = m_Map[panel];
                var renderer = m_Panels[panel];
                if (!face.Valid || renderer == null)
                    continue;

                m_Block.SetVector(k_PanelSize, new Vector4(face.Size.x, face.Size.y, 0f, 0f));
                m_Block.SetVector(k_PanelCentre, face.Centre);
                m_Block.SetVector(k_PanelRight, face.Right);
                m_Block.SetVector(k_PanelUp, face.Up);
                m_Block.SetVectorArray(k_DropPos, m_Pos[panel]);
                renderer.SetPropertyBlock(m_Block);
            }
        }

        // ---------------------------------------------------------------- the room's surfaces

        /// <summary>The panels as one surface, in this object's frame. Drops already running were placed on the old one.</summary>
        void ReadFaces()
        {
            m_Map = new SurfaceMap(transform, m_Panels, k_MaxPanels);
            for (int i = 0; i < MaxDrops; i++)
                m_Drops[i].Alive = false;
        }
    }
}

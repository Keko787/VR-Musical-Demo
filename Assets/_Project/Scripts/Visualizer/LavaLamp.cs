using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// A 2D show: the room as a lava lamp. The floor is the heater; wax warms there, slides out to a
    /// wall, climbs it, cools near the top and sinks back down to the floor to warm again. The bass
    /// turns the heater up, so a heavy track keeps the wax moving and a quiet one lets it settle;
    /// the beat swells the blobs for a moment, the mids churn their outlines, and the highs make the
    /// glitter in the liquid twinkle. The look is the <c>VRMusic/LavaLamp</c> shader's: the blobs
    /// as metaballs that merge and split, lit as rounded lumps, glowing hotter where they are hot.
    /// </summary>
    /// <remarks>
    /// The blobs live on the room's screens as one surface (<see cref="SurfaceMap"/>): a blob that
    /// slides off the floor goes up the wall it reached, and one that sinks off the bottom of a wall
    /// comes out onto the floor; one that drifts round a corner goes on along the next wall. Each
    /// panel is handed every blob's centre unfolded into its own plane, so the wax is unbroken at the
    /// seams. On the floor, "up" is out toward the nearest wall — front, left or right; the back is
    /// open — so a hot blob heads for a wall and a cold one for the middle of the floor. It draws a
    /// few millimetres in front of the water panels, mostly opaque: the liquid hides the wash, and
    /// layered with the tubes the tubes show faintly through it.
    /// </remarks>
    public class LavaLamp : MonoBehaviour
    {
        /// <summary>Blobs at most; the shader's array size.</summary>
        public const int MaxBlobs = 24;

        const int k_MaxPanels = 8;

        [SerializeField] AudioAnalyser m_Analyser;

        [SerializeField, Tooltip("One quad per projected screen, on the VRMusic/LavaLamp material, each a child of this object. " +
            "Filled in by the builder.")]
        MeshRenderer[] m_Panels;

        [Header("Wax")]
        [SerializeField, Range(4, MaxBlobs)] int m_Blobs = 18;

        [SerializeField, Tooltip("Radius of the smallest and the largest blob, in metres.")]
        Vector2 m_Radius = new(0.13f, 0.3f);

        [SerializeField, Tooltip("Metres a second a blob rises at when it is as hot as it gets, with nothing playing. A cold one sinks as fast.")]
        float m_RiseSpeed = 0.12f;

        [SerializeField, Tooltip("How much faster everything moves at full loudness, on top of 1.")]
        float m_MusicPace = 1.4f;

        [SerializeField, Tooltip("How fast the heater warms the wax on the floor and low on the walls. Slow, so the wax pools on the floor a while.")]
        float m_HeatRate = 0.12f;

        [SerializeField, Tooltip("How fast the wax cools near the top of a wall. Much slower lower down, so it climbs all the way.")]
        float m_CoolRate = 0.2f;

        [SerializeField, Tooltip("Metres above the floor the heater still warms.")]
        float m_HeaterReach = 0.45f;

        [SerializeField, Tooltip("How much the bass turns the heater up.")]
        float m_BassHeat = 1.2f;

        [SerializeField, Tooltip("Metres a second a blob wanders sideways.")]
        float m_Drift = 0.05f;

        [SerializeField, Range(0f, 0.4f), Tooltip("How much a beat swells the blobs for a moment. Off in low intensity.")]
        float m_BeatSwell = 0.12f;

        [Header("Colour")]
        [SerializeField, Tooltip("Seconds each of the lamp's colourings lasts before it fades into the next.")]
        float m_PaletteSeconds = 70f;

        [Header("Light")]
        [SerializeField, Tooltip("The lamp that lights the wax's relief, in the room's frame. Fixed, like the ripples', so everyone sees the same.")]
        Vector3 m_Lamp = new(0f, 2.6f, -0.7f);

        [SerializeField, Tooltip("Where the wax's gloss is seen from, in the room's frame: a nominal eye, not the tracked one.")]
        Vector3 m_Eye = new(0f, 1.6f, -0.3f);

        [SerializeField] int m_Seed = 5;

        /// <summary>0 hides the lamp; the visualizer fades this on a mode change.</summary>
        public float Opacity { get; set; } = 1f;

        /// <summary>Overall brightness multiplier.</summary>
        public float Intensity { get; set; } = 1f;

        /// <summary>Off in low-intensity mode: no swell on the beat.</summary>
        public bool BeatFlashEnabled { get; set; } = true;

        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        /// <summary>
        /// The lamp's colourings, each the hot wax, the cooled wax, the liquid low down where the
        /// heater lights it, and the liquid at the top. Classic first.
        /// </summary>
        static readonly (Color hot, Color cool, Color low, Color high)[] k_Palettes =
        {
            (new Color(1f, 0.55f, 0.1f), new Color(0.85f, 0.12f, 0.05f), new Color(0.32f, 0.04f, 0.24f), new Color(0.05f, 0f, 0.1f)),
            (new Color(0.45f, 1f, 0.8f), new Color(0.05f, 0.5f, 0.75f), new Color(0.03f, 0.12f, 0.35f), new Color(0f, 0.02f, 0.08f)),
            (new Color(1f, 0.4f, 0.9f), new Color(0.55f, 0.05f, 0.6f), new Color(0f, 0.24f, 0.3f), new Color(0f, 0.03f, 0.07f)),
            (new Color(1f, 0.9f, 0.3f), new Color(1f, 0.32f, 0.08f), new Color(0.38f, 0f, 0.1f), new Color(0.07f, 0f, 0.04f)),
            (new Color(0.85f, 1f, 0.3f), new Color(0.2f, 0.65f, 0.1f), new Color(0.24f, 0.02f, 0.3f), new Color(0.04f, 0f, 0.08f)),
        };

        static readonly int k_BlobCount = Shader.PropertyToID("_BlobCount");
        static readonly int k_BlobPos = Shader.PropertyToID("_BlobPos");
        static readonly int k_BlobInfo = Shader.PropertyToID("_BlobInfo");
        static readonly int k_PanelSize = Shader.PropertyToID("_PanelSize");
        static readonly int k_PanelCentre = Shader.PropertyToID("_PanelCentre");
        static readonly int k_PanelRight = Shader.PropertyToID("_PanelRight");
        static readonly int k_PanelUp = Shader.PropertyToID("_PanelUp");
        static readonly int k_LightPos = Shader.PropertyToID("_LightPos");
        static readonly int k_EyePos = Shader.PropertyToID("_EyePos");
        static readonly int k_Room = Shader.PropertyToID("_Room");
        static readonly int k_Ambient = Shader.PropertyToID("_Ambient");
        static readonly int k_WaxHot = Shader.PropertyToID("_WaxHot");
        static readonly int k_WaxCool = Shader.PropertyToID("_WaxCool");
        static readonly int k_LiquidLow = Shader.PropertyToID("_LiquidLow");
        static readonly int k_LiquidHigh = Shader.PropertyToID("_LiquidHigh");
        static readonly int k_Opacity = Shader.PropertyToID("_Opacity");
        static readonly int k_Intensity = Shader.PropertyToID("_Intensity");

        struct Blob
        {
            public Vector3 Position;
            public int Face;
            public float Radius;
            public float Heat;                       // 0 cold … 1 as hot as it gets
            public bool Up;                          // rising: from warm enough until cooled right down, then sinking
            public float Rise;                       // metres a second, up a wall or out across the floor
            public float Seed;
        }

        readonly Blob[] m_Wax = new Blob[MaxBlobs];
        readonly Vector4[][] m_Pos = new Vector4[k_MaxPanels][];
        readonly Vector4[] m_Info = new Vector4[MaxBlobs];

        SurfaceMap m_Map;
        MaterialPropertyBlock m_Block;
        System.Random m_Random;
        int m_Count;
        float m_Heater;
        float m_Wobble;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            for (int i = 0; i < k_MaxPanels; i++)
                m_Pos[i] = new Vector4[MaxBlobs];
        }

        void OnEnable()
        {
            m_Map = null;                            // poured afresh, on the faces as they are now
        }

        void Update()
        {
            if (m_Analyser == null || m_Panels == null || m_Panels.Length == 0)
                return;

            if (m_Map == null || m_Map.Count != Mathf.Min(m_Panels.Length, k_MaxPanels) || m_Count != Mathf.Clamp(m_Blobs, 1, MaxBlobs))
                Pour();
            if (m_Map.Floor < 0)
                return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Flow(dt);
            Upload();
        }

        // ---------------------------------------------------------------- the wax

        /// <summary>The wax, poured: about half pooled on the floor, the rest part way up the walls at all temperatures.</summary>
        void Pour()
        {
            m_Map = new SurfaceMap(transform, m_Panels, k_MaxPanels);
            m_Random = new System.Random(m_Seed);
            m_Count = Mathf.Clamp(m_Blobs, 1, MaxBlobs);

            int walls = 0;
            for (int i = 0; i < m_Map.Count; i++)
                if (m_Map[i].Valid && !m_Map[i].Floor)
                    walls++;

            for (int i = 0; i < m_Count; i++)
            {
                bool onFloor = m_Map.Floor >= 0 && (walls == 0 || i % 2 == 0);
                int face = onFloor ? m_Map.Floor : NthWall(i / 2 % Mathf.Max(1, walls));
                var f = m_Map[face];
                float u = Mathf.Lerp(-0.42f, 0.42f, Random01()) * f.Size.x;
                float v = (onFloor ? Mathf.Lerp(-0.4f, 0.4f, Random01()) : Mathf.Lerp(-0.4f, 0.2f, Random01())) * f.Size.y;

                float heat = onFloor ? Random01() * 0.5f : Random01();
                m_Wax[i] = new Blob
                {
                    Position = m_Map.Point(face, new Vector2(u, v)),
                    Face = face,
                    Radius = Mathf.Lerp(m_Radius.x, m_Radius.y, Random01() * Random01()),
                    Heat = heat,
                    Up = !onFloor && heat > 0.5f,
                    Seed = Random01() * 100f,
                };
            }
        }

        int NthWall(int n)
        {
            for (int i = 0, seen = 0; i < m_Map.Count; i++)
            {
                if (!m_Map[i].Valid || m_Map[i].Floor)
                    continue;
                if (seen++ == n)
                    return i;
            }
            return m_Map.Floor;
        }

        void Flow(float dt)
        {
            float idle = m_Analyser.Idle;
            float playing = 1f - idle;
            float bass = m_Analyser.Bass * playing;

            // The heater follows the bass, quick to warm and slow to cool, like a real element.
            float target = 0.55f + m_BassHeat * bass;
            m_Heater = Mathf.Lerp(m_Heater, target, 1f - Mathf.Exp(-dt / (target > m_Heater ? 0.25f : 1.2f)));
            m_Wobble = Mathf.Lerp(m_Wobble, m_Analyser.Mid * playing, 1f - Mathf.Exp(-dt / 0.3f));

            float pace = 1f + m_MusicPace * m_Analyser.Loudness * playing;
            float height = CaveScreens.Height;
            float time = Time.time;

            for (int i = 0; i < m_Count; i++)
            {
                ref var blob = ref m_Wax[i];
                var face = m_Map[blob.Face];
                var local = m_Map.Local(blob.Face, blob.Position);
                float above = face.Floor ? 0f : local.y + face.Half.y;

                // Warmed near the floor; cooled a little on the way up and most near the top.
                float warmth = face.Floor ? 1f : Mathf.Exp(-above / Mathf.Max(0.05f, m_HeaterReach));
                float high = Mathf.Clamp01(above / height);
                blob.Heat += dt * (m_HeatRate * m_Heater * warmth - m_CoolRate * (0.05f + 1.2f * high * high * high));
                blob.Heat = Mathf.Clamp01(blob.Heat);

                // Warm enough, it sets off up and keeps going until it has cooled right down; then it sinks all
                // the way, until it has warmed again. Without the gap between the two it would hover at the
                // height where warming and cooling balance. On the floor it moves slower, so the wax pools there.
                if (blob.Heat > 0.7f)
                    blob.Up = true;
                else if (blob.Heat < 0.3f)
                    blob.Up = false;

                float speed = m_RiseSpeed * pace * (face.Floor ? 0.6f : 1f);
                blob.Rise = Mathf.Lerp(blob.Rise, blob.Up ? speed : -speed, 1f - Mathf.Exp(-dt / 0.8f));
                float wander = (Mathf.PerlinNoise(blob.Seed, time * 0.07f) - 0.5f) * 2f * m_Drift * pace;

                Vector2 step;
                if (face.Floor)
                {
                    var outward = TowardWalls(local, face.Half);
                    step = (outward * blob.Rise + new Vector2(-outward.y, outward.x) * wander) * dt;
                }
                else
                {
                    step = new Vector2(wander, blob.Rise) * dt;
                }

                var position = m_Map.Point(blob.Face, local + step);
                int onFace = blob.Face;
                bool held = m_Map.Carry(ref position, ref onFace, blob.Radius * 0.25f);
                blob.Position = position;
                blob.Face = onFace;

                // Against the top of a wall it can climb no further, and it cools there.
                if (held && !m_Map[onFace].Floor && m_Map.Local(onFace, position).y > 0f)
                {
                    blob.Rise = Mathf.Min(blob.Rise, 0f);
                    blob.Heat = Mathf.Max(0f, blob.Heat - dt * 0.3f);
                }
            }

            Separate(dt);
        }

        /// <summary>
        /// On the floor, the way out toward the walls from <paramref name="local"/>: mostly toward the
        /// nearest of the front, left and right edges (the back is open), blended so a blob between two
        /// does not flick from one to the other.
        /// </summary>
        static Vector2 TowardWalls(Vector2 local, Vector2 half)
        {
            float left = local.x + half.x;
            float right = half.x - local.x;
            float front = half.y - local.y;
            const float reach = 0.4f;

            var direction = new Vector2(-1f, 0f) * Mathf.Exp(-left / reach) + new Vector2(1f, 0f) * Mathf.Exp(-right / reach) +
                            new Vector2(0f, 1f) * Mathf.Exp(-front / reach);
            if (direction.sqrMagnitude < 1e-6f)
                direction = new Vector2(0f, 1f);
            return direction.normalized;
        }

        /// <summary>Blobs on the same screen ease apart when they crowd, so the wax does not all end up in one lump.</summary>
        void Separate(float dt)
        {
            for (int i = 0; i < m_Count; i++)
            {
                for (int j = i + 1; j < m_Count; j++)
                {
                    if (m_Wax[i].Face != m_Wax[j].Face)
                        continue;

                    var apart = m_Wax[i].Position - m_Wax[j].Position;
                    float distance = apart.magnitude;
                    float room = (m_Wax[i].Radius + m_Wax[j].Radius) * 0.9f;
                    if (distance >= room || distance < 1e-4f)
                        continue;

                    var push = apart / distance * ((room - distance) * Mathf.Min(1f, dt * 1.5f));
                    m_Wax[i].Position += push * 0.5f;
                    m_Wax[j].Position -= push * 0.5f;
                }
            }
        }

        float Random01() => (float)m_Random.NextDouble();

        // ---------------------------------------------------------------- the shader

        void Upload()
        {
            float beat = BeatFlashEnabled ? m_Analyser.BeatPulse : 0f;
            float swell = 1f + m_BeatSwell * beat;

            for (int i = 0; i < m_Count; i++)
            {
                var blob = m_Wax[i];
                float radius = blob.Radius * swell * (0.9f + 0.2f * blob.Heat);   // hot wax swells
                m_Info[i] = new Vector4(radius, blob.Heat, blob.Seed, 0f);
                for (int panel = 0; panel < m_Map.Count; panel++)
                    m_Pos[panel][i] = m_Map[panel].Valid ? m_Map.ImageOn(panel, blob.Position, blob.Face) : new Vector4(SurfaceMap.Far, SurfaceMap.Far, SurfaceMap.Far, SurfaceMap.Far);
            }

            // The colouring, fading from one to the next.
            float at = m_PaletteSeconds > 0f ? Time.time / m_PaletteSeconds : 0f;
            int from = Mathf.FloorToInt(at) % k_Palettes.Length;
            int to = (from + 1) % k_Palettes.Length;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((at - Mathf.Floor(at) - 0.7f) / 0.3f));
            var a = k_Palettes[from];
            var b = k_Palettes[to];

            float idle = m_Analyser.Idle;
            float glitter = m_Analyser.High * (1f - idle);

            m_Block.Clear();
            m_Block.SetFloat(k_BlobCount, m_Count);
            m_Block.SetVectorArray(k_BlobInfo, m_Info);
            m_Block.SetVector(k_LightPos, m_Lamp);
            m_Block.SetVector(k_EyePos, m_Eye);
            m_Block.SetVector(k_Room, new Vector4(CaveScreens.HalfWidth, CaveScreens.FrontZ, CaveScreens.Height, 0f));
            m_Block.SetVector(k_Ambient, new Vector4(Time.time, m_Wobble, glitter, m_Heater));
            m_Block.SetColor(k_WaxHot, Color.Lerp(a.hot, b.hot, blend));
            m_Block.SetColor(k_WaxCool, Color.Lerp(a.cool, b.cool, blend));
            m_Block.SetColor(k_LiquidLow, Color.Lerp(a.low, b.low, blend));
            m_Block.SetColor(k_LiquidHigh, Color.Lerp(a.high, b.high, blend));
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
                m_Block.SetVectorArray(k_BlobPos, m_Pos[panel]);
                renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}

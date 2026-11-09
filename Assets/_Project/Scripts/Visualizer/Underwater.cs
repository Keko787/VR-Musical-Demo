using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// A 2D show: the room under the sea. Sunlight comes down through the waves overhead and dances
    /// across the seabed (the floor) as a net of caustics, and up the walls, the open water, as
    /// slanting shafts; bubbles rise in streams from vents at the foot of the walls, kelp sways along
    /// the sides, a school of fish cruises round the room, and specks of plankton drift down through
    /// it all. The music is the sea's weather: the bass brightens the light and surges the kelp over,
    /// the loudness quickens the current, the beat sends a burst of bubbles up from a vent and now and
    /// then turns the school, which flashes silver as it wheels, and the highs make the plankton and
    /// the surface glitter. The look is the <c>VRMusic/Underwater</c> shader's.
    /// </summary>
    /// <remarks>
    /// The light is worked out from the point in the room, not from the screen it lands on, so it is
    /// unbroken at the seams: the caustic net is the pattern under the waves, carried down along the
    /// sun's slant to wherever it lands — across the floor a slice through it, up a wall a slice
    /// nearly along it, which draws it out into shafts. The bubbles, the fish and the kelp live on the
    /// walls in one coordinate that runs round them, from the open end of the left wall across the
    /// front to the open end of the right, so a fish swims round a corner without a break. It draws a
    /// few millimetres in front of the water panels and under the lava lamp and the ripples, mostly
    /// opaque, so layered with either they play over the sea.
    /// </remarks>
    public class Underwater : MonoBehaviour
    {
        /// <summary>The shader's array sizes.</summary>
        public const int MaxBubbles = 64;
        public const int MaxFish = 32;
        public const int MaxKelp = 12;

        const int k_MaxPanels = 8;
        const int k_MaxVents = 12;

        /// <summary>Metres a speck of plankton sinks a second, at the slow pace.</summary>
        const float k_SnowFall = 0.018f;

        /// <summary>Metres of sinking after which the plankton's offset wraps; the shader's pattern repeats over it, so there is no jump.</summary>
        const float k_SnowWrap = 30f;

        [SerializeField] AudioAnalyser m_Analyser;

        [SerializeField, Tooltip("One quad per projected screen, on the VRMusic/Underwater material, each a child of this object. " +
            "Filled in by the builder.")]
        MeshRenderer[] m_Panels;

        [Header("Light")]
        [SerializeField, Tooltip("How fast the light dances on the seabed with nothing playing, in the units of the caustic pattern.")]
        float m_Current = 1.1f;

        [SerializeField, Tooltip("How much faster everything moves at full loudness, on top of 1.")]
        float m_MusicPace = 1.0f;

        [SerializeField, Tooltip("The slant of the sunlight: metres the light moves along x and along z for each metre it comes down. It draws " +
            "the caustics out into slanting shafts up the walls.")]
        Vector2 m_SunSlant = new(0.16f, 0.1f);

        [SerializeField, Tooltip("How much the bass brightens the light through the waves.")]
        float m_BassLight = 0.45f;

        [SerializeField, Range(0f, 1f), Tooltip("How much a beat brightens the light for a moment. Off in low intensity.")]
        float m_BeatLight = 0.2f;

        [Header("Bubbles")]
        [SerializeField, Range(1, k_MaxVents), Tooltip("Vents along the foot of the walls that bubbles rise from.")]
        int m_Vents = 6;

        [SerializeField, Tooltip("Bubbles a second from each vent at full loudness; a tenth of that with nothing playing.")]
        float m_StreamRate = 0.45f;

        [SerializeField, Range(0, 12), Tooltip("Bubbles in the burst a strong beat sends up from one vent.")]
        int m_BurstSize = 5;

        [SerializeField, Tooltip("Seconds at least between two bursts, so a fast track does not fill the walls.")]
        float m_BurstGap = 0.8f;

        [SerializeField, Tooltip("Radius of the smallest and the largest bubble as it leaves its vent, in metres. Each grows by a third " +
            "on the way up.")]
        Vector2 m_BubbleRadius = new(0.006f, 0.024f);

        [Header("Fish")]
        [SerializeField, Range(0, MaxFish)] int m_Fish = 26;

        [SerializeField, Tooltip("Length of the smallest and the largest fish, in metres. The far ones are the small ones.")]
        Vector2 m_FishLength = new(0.09f, 0.16f);

        [SerializeField, Tooltip("Metres a second the school cruises at with nothing playing.")]
        float m_SchoolSpeed = 0.3f;

        [SerializeField, Tooltip("Seconds at least between the school turning on a beat. It also turns before either open end of the walls.")]
        float m_TurnGap = 8f;

        [Header("Kelp")]
        [SerializeField, Range(0, MaxKelp), Tooltip("Stalks of kelp, in clumps of three.")]
        int m_Kelp = 12;

        [SerializeField, Tooltip("Metres the tip of a stalk sways with nothing playing, and at full loudness.")]
        Vector2 m_KelpSway = new(0.05f, 0.14f);

        [SerializeField, Tooltip("Metres the bass leans the kelp over, as a surge of current.")]
        float m_KelpSurge = 0.1f;

        [Header("Colour")]
        [SerializeField, Tooltip("Seconds each of the seas lasts before it fades into the next.")]
        float m_PaletteSeconds = 80f;

        [SerializeField] int m_Seed = 7;

        /// <summary>0 hides the sea; the visualizer fades this on a mode change.</summary>
        public float Opacity { get; set; } = 1f;

        /// <summary>Overall brightness multiplier.</summary>
        public float Intensity { get; set; } = 1f;

        /// <summary>Off in low-intensity mode: the light does not brighten on the beat.</summary>
        public bool BeatFlashEnabled { get; set; } = true;

        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        /// <summary>
        /// The seas, each the water high up and deep down, the sunlight, the sand, the kelp, and how
        /// much the plankton glows on its own. A lagoon first; the last is the sea at night.
        /// </summary>
        static readonly (Color shallow, Color deep, Color light, Color sand, Color kelp, float glow)[] k_Palettes =
        {
            (new Color(0.07f, 0.38f, 0.46f), new Color(0f, 0.07f, 0.17f), new Color(0.75f, 1f, 0.95f), new Color(0.62f, 0.56f, 0.42f), new Color(0.3f, 0.34f, 0.08f), 0f),
            (new Color(0.03f, 0.22f, 0.5f), new Color(0f, 0.03f, 0.13f), new Color(0.6f, 0.85f, 1f), new Color(0.36f, 0.4f, 0.46f), new Color(0.16f, 0.28f, 0.2f), 0f),
            (new Color(0.08f, 0.32f, 0.24f), new Color(0.01f, 0.07f, 0.07f), new Color(0.85f, 1f, 0.7f), new Color(0.44f, 0.44f, 0.3f), new Color(0.42f, 0.36f, 0.07f), 0f),
            (new Color(0.02f, 0.05f, 0.16f), new Color(0f, 0.005f, 0.04f), new Color(0.18f, 0.3f, 0.6f), new Color(0.14f, 0.16f, 0.28f), new Color(0.04f, 0.08f, 0.1f), 1f),
        };

        static readonly int k_BubbleCount = Shader.PropertyToID("_BubbleCount");
        static readonly int k_BubblesId = Shader.PropertyToID("_Bubbles");
        static readonly int k_FishCount = Shader.PropertyToID("_FishCount");
        static readonly int k_FishId = Shader.PropertyToID("_Fish");
        static readonly int k_FishInfo = Shader.PropertyToID("_FishInfo");
        static readonly int k_KelpCount = Shader.PropertyToID("_KelpCount");
        static readonly int k_KelpId = Shader.PropertyToID("_Kelp");
        static readonly int k_PanelSize = Shader.PropertyToID("_PanelSize");
        static readonly int k_PanelCentre = Shader.PropertyToID("_PanelCentre");
        static readonly int k_PanelRight = Shader.PropertyToID("_PanelRight");
        static readonly int k_PanelUp = Shader.PropertyToID("_PanelUp");
        static readonly int k_Room = Shader.PropertyToID("_Room");
        static readonly int k_Sun = Shader.PropertyToID("_Sun");
        static readonly int k_Phase = Shader.PropertyToID("_Phase");
        static readonly int k_Music = Shader.PropertyToID("_Music");
        static readonly int k_Shallow = Shader.PropertyToID("_Shallow");
        static readonly int k_Deep = Shader.PropertyToID("_Deep");
        static readonly int k_LightColour = Shader.PropertyToID("_LightColour");
        static readonly int k_Sand = Shader.PropertyToID("_Sand");
        static readonly int k_KelpColour = Shader.PropertyToID("_KelpColour");
        static readonly int k_Plankton = Shader.PropertyToID("_Plankton");
        static readonly int k_Opacity = Shader.PropertyToID("_Opacity");
        static readonly int k_Intensity = Shader.PropertyToID("_Intensity");

        struct Bubble
        {
            public bool Alive;
            public float U;                          // round the walls, metres; see the class remarks
            public float Y;                          // metres up the wall
            public float Radius;                     // as it left the vent
            public float Age;
            public float Wobble;                     // radians a second it zigzags at
            public float Seed;
        }

        struct Fish
        {
            public Vector2 Position;                 // u round the walls, y up them
            public Vector2 Velocity;
            public Vector2 Slot;                     // its place in the school
            public float Yaw;                        // 0 facing along +u, π facing back along −u; in between it is turning, end on
            public float TurnSpeed;                  // radians a second
            public float Tail;                       // the tail's beat, radians
            public float Length;
            public float Haze;                       // 0 near … 1 lost in the blue
            public float Lag;                        // seconds it takes to follow the school
            public float Seed;
        }

        readonly Bubble[] m_Bubbles = new Bubble[MaxBubbles];
        readonly Fish[] m_School = new Fish[MaxFish];
        readonly Vector4[] m_BubbleData = new Vector4[MaxBubbles];
        readonly Vector4[] m_FishData = new Vector4[MaxFish];
        readonly Vector4[] m_FishInfo = new Vector4[MaxFish];
        readonly Vector4[] m_KelpData = new Vector4[MaxKelp];
        readonly float[] m_VentU = new float[k_MaxVents];
        readonly float[] m_VentRate = new float[k_MaxVents];
        readonly float[] m_VentClock = new float[k_MaxVents];

        SurfaceMap m_Map;
        MaterialPropertyBlock m_Block;
        System.Random m_Random;
        bool m_Stocked;
        int m_VentCount;
        int m_FishCount;
        int m_KelpCount;

        Vector2 m_SchoolAt;
        Vector2 m_SchoolVelocity;
        float m_SchoolDirection = 1f;
        float m_LastTurn = -100f;

        int m_BurstVent;
        int m_BurstLeft;
        float m_BurstClock;
        float m_LastBurst = -100f;

        float m_CausticPhase;
        float m_SnowPhase;
        float m_KelpPhase;
        float m_SwayAmplitude;
        float m_Surge;
        float m_Glitter;
        float m_Light = 1f;

        /// <summary>Metres from the middle of the front wall to either open end of the side walls, along them.</summary>
        static float WallEnd => CaveScreens.HalfWidth + CaveScreens.SideWidth;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            m_Map = null;                            // the faces as they are now
        }

        void Update()
        {
            if (m_Analyser == null || m_Panels == null || m_Panels.Length == 0)
                return;

            if (m_Map == null || m_Map.Count != Mathf.Min(m_Panels.Length, k_MaxPanels))
                m_Map = new SurfaceMap(transform, m_Panels, k_MaxPanels);
            if (!m_Stocked || m_VentCount != Mathf.Clamp(m_Vents, 1, k_MaxVents) || m_FishCount != Mathf.Clamp(m_Fish, 0, MaxFish) ||
                m_KelpCount != Mathf.Clamp(m_Kelp, 0, MaxKelp))
                Stock();

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float playing = 1f - m_Analyser.Idle;
            float pace = 1f + m_MusicPace * m_Analyser.Loudness * playing;

            Weather(dt, playing, pace);
            Bubbles(dt, playing, pace);
            Swim(dt, playing, pace);
            Upload();
        }

        // ---------------------------------------------------------------- the sea

        /// <summary>The vents, the kelp and the school, set out from the seed.</summary>
        void Stock()
        {
            m_Random = new System.Random(m_Seed);
            float end = WallEnd;

            // Vents spread along the foot of the walls, each with a pace of its own.
            m_VentCount = Mathf.Clamp(m_Vents, 1, k_MaxVents);
            for (int v = 0; v < m_VentCount; v++)
            {
                m_VentU[v] = Mathf.Lerp(-end + 0.4f, end - 0.4f, (v + 0.2f + 0.6f * Random01()) / m_VentCount);
                m_VentRate[v] = Mathf.Lerp(0.5f, 1.5f, Random01());
                m_VentClock[v] = Random01();
            }

            for (int i = 0; i < MaxBubbles; i++)
                m_Bubbles[i].Alive = false;

            // Kelp in clumps of three, toward the front corners and along the side walls, clear of the middle
            // of the front wall, where the menu opens and the school shows best: x the root, y the height,
            // z a phase, w the stalk's half width.
            m_KelpCount = Mathf.Clamp(m_Kelp, 0, MaxKelp);
            int clumps = Mathf.CeilToInt(m_KelpCount / 3f);
            float perSide = Mathf.Max(1f, Mathf.Ceil(clumps / 2f));
            var clumpAt = new float[clumps];
            for (int c = 0; c < clumps; c++)
            {
                float side = c % 2 == 0 ? -1f : 1f;
                clumpAt[c] = side * Mathf.Lerp(1.15f, end - 0.5f, (c / 2 + 0.3f + 0.4f * Random01()) / perSide);
            }
            for (int k = 0; k < m_KelpCount; k++)
            {
                float root = clumpAt[k / 3] + (k % 3 - 1) * 0.13f + (Random01() - 0.5f) * 0.06f;
                m_KelpData[k] = new Vector4(root, Mathf.Lerp(0.9f, 1.85f, Random01()), Random01() * 6.283f,
                    Mathf.Lerp(0.008f, 0.012f, Random01()));
            }

            // The school, farthest fish first so the near ones draw over them.
            m_FishCount = Mathf.Clamp(m_Fish, 0, MaxFish);
            m_SchoolAt = new Vector2(-1.2f, 1.3f);
            m_SchoolDirection = 1f;
            m_SchoolVelocity = new Vector2(m_SchoolSpeed, 0f);
            for (int i = 0; i < m_FishCount; i++)
            {
                float haze = Mathf.Lerp(0.7f, 0.05f, (i + Random01()) / Mathf.Max(1, m_FishCount));
                float angle = Random01() * 6.283f;
                float reach = Mathf.Sqrt(Random01());
                var slot = new Vector2(Mathf.Cos(angle) * 0.55f, Mathf.Sin(angle) * 0.28f) * reach;

                m_School[i] = new Fish
                {
                    Position = m_SchoolAt + slot,
                    Velocity = m_SchoolVelocity,
                    Slot = slot,
                    Yaw = 0f,
                    TurnSpeed = Mathf.Lerp(2.5f, 4.5f, Random01()),
                    Tail = Random01() * 6.283f,
                    Length = Mathf.Lerp(m_FishLength.y, m_FishLength.x, haze / 0.7f) * Mathf.Lerp(0.9f, 1.1f, Random01()),
                    Haze = haze,
                    Lag = Mathf.Lerp(0.35f, 0.9f, Random01()),
                    Seed = Random01() * 100f,
                };
            }

            m_Stocked = true;
        }

        void Weather(float dt, float playing, float pace)
        {
            // Paces are added up here rather than multiplied by the time in the shader, so a change of
            // loudness changes the speed and never jumps the pattern.
            m_CausticPhase += dt * m_Current * pace;
            m_SnowPhase = Mathf.Repeat(m_SnowPhase + dt * k_SnowFall * pace, k_SnowWrap);
            m_KelpPhase += dt * 0.9f * pace;

            float bass = m_Analyser.Bass * playing;
            m_Surge = Mathf.Lerp(m_Surge, bass, 1f - Mathf.Exp(-dt / (bass > m_Surge ? 0.15f : 1.5f)));
            m_Glitter = Mathf.Lerp(m_Glitter, m_Analyser.High * playing, 1f - Mathf.Exp(-dt / 0.08f));
            float sway = Mathf.Lerp(m_KelpSway.x, m_KelpSway.y, m_Analyser.Loudness * playing);
            m_SwayAmplitude = Mathf.Lerp(m_SwayAmplitude, sway, 1f - Mathf.Exp(-dt / 1.0f));

            float beat = BeatFlashEnabled ? m_Analyser.BeatPulse : 0f;
            m_Light = 0.85f + m_BassLight * m_Surge + m_BeatLight * beat;
        }

        // ---------------------------------------------------------------- bubbles

        void Bubbles(float dt, float playing, float pace)
        {
            // A steady stream from each vent, quicker the louder it is.
            float rate = m_StreamRate * (0.1f + 0.9f * m_Analyser.Loudness * playing);
            for (int v = 0; v < m_VentCount; v++)
            {
                m_VentClock[v] -= dt * rate * m_VentRate[v];
                if (m_VentClock[v] > 0f)
                    continue;

                m_VentClock[v] += -Mathf.Log(Mathf.Max(1e-4f, Random01()));      // the next after a random wait
                Spawn(v, Mathf.Lerp(m_BubbleRadius.x, Mathf.Lerp(m_BubbleRadius.x, m_BubbleRadius.y, 0.45f), Random01()));
            }

            // A beat sends up a burst of big ones from one vent, one after another.
            float now = Time.time;
            if (m_Analyser.Beat && playing > 0.5f && m_BurstSize > 0 && now - m_LastBurst >= m_BurstGap)
            {
                m_BurstVent = m_Random.Next(m_VentCount);
                m_BurstLeft = Mathf.Max(2, Mathf.RoundToInt(m_BurstSize * (0.5f + 0.5f * m_Analyser.BeatStrength)));
                m_BurstClock = 0f;
                m_LastBurst = now;
            }

            if (m_BurstLeft > 0)
            {
                m_BurstClock -= dt;
                if (m_BurstClock <= 0f)
                {
                    Spawn(m_BurstVent, Mathf.Lerp(m_BubbleRadius.x * 1.5f, m_BubbleRadius.y, Random01()));
                    m_BurstLeft--;
                    m_BurstClock = Mathf.Lerp(0.04f, 0.09f, Random01());
                }
            }

            // Big bubbles rise faster; each pops at the surface.
            float top = CaveScreens.Height - 0.08f;
            float quicken = Mathf.Lerp(1f, pace, 0.4f);
            for (int i = 0; i < MaxBubbles; i++)
            {
                ref var bubble = ref m_Bubbles[i];
                if (!bubble.Alive)
                    continue;

                bubble.Age += dt;
                float rise = (0.12f + 9f * bubble.Radius) * quicken;
                bubble.Y += rise * dt * Mathf.Clamp01(0.2f + bubble.Age / 0.25f);   // slow off the vent as it forms
                if (bubble.Y > top)
                    bubble.Alive = false;
            }
        }

        void Spawn(int vent, float radius)
        {
            for (int i = 0; i < MaxBubbles; i++)
            {
                if (m_Bubbles[i].Alive)
                    continue;

                m_Bubbles[i] = new Bubble
                {
                    Alive = true,
                    U = m_VentU[vent] + (Random01() - 0.5f) * 0.03f,
                    Y = 0.015f + radius,
                    Radius = radius,
                    Age = 0f,
                    Wobble = Mathf.Lerp(6f, 10f, Random01()),
                    Seed = Random01() * 6.283f,
                };
                return;
            }
        }

        // ---------------------------------------------------------------- fish

        void Swim(float dt, float playing, float pace)
        {
            if (m_FishCount == 0)
                return;

            float now = Time.time;
            float speed = m_SchoolSpeed * Mathf.Lerp(1f, pace, 0.6f);

            // It turns back before the open ends of the side walls, and now and then on a strong beat. A
            // turn overshoots by about the school's speed times its easing, so it starts well short of the end.
            float end = WallEnd - 1.2f;
            bool turn = (m_SchoolDirection > 0f && m_SchoolAt.x > end) || (m_SchoolDirection < 0f && m_SchoolAt.x < -end);
            if (!turn && m_Analyser.Beat && playing > 0.5f && m_Analyser.BeatStrength > 0.3f && now - m_LastTurn > m_TurnGap)
                turn = true;
            if (turn && now - m_LastTurn > 1.5f)
            {
                m_SchoolDirection = -m_SchoolDirection;
                m_LastTurn = now;
                m_SchoolVelocity.y += (Random01() < 0.5f ? -0.8f : 0.8f) * speed;     // it wheels up or down as it turns
            }

            float height = CaveScreens.Height;
            float cruise = Mathf.Lerp(0.8f, 1.7f, Mathf.PerlinNoise(m_Seed * 0.37f, now * 0.025f));
            var want = new Vector2(m_SchoolDirection * speed, Mathf.Clamp((cruise - m_SchoolAt.y) * 0.5f, -0.15f, 0.15f));
            m_SchoolVelocity = Vector2.Lerp(m_SchoolVelocity, want, 1f - Mathf.Exp(-dt / 1.2f));
            m_SchoolAt += m_SchoolVelocity * dt;
            m_SchoolAt.y = Mathf.Clamp(m_SchoolAt.y, 0.6f, height - 0.45f);

            for (int i = 0; i < m_FishCount; i++)
            {
                ref var fish = ref m_School[i];

                // Each heads for its place in the school, a little late, so a turn runs through the school.
                var wander = new Vector2(Mathf.PerlinNoise(fish.Seed, now * 0.2f), Mathf.PerlinNoise(now * 0.2f, fish.Seed)) - new Vector2(0.5f, 0.5f);
                var target = m_SchoolAt + fish.Slot + wander * 0.12f;
                var desired = m_SchoolVelocity + (target - fish.Position) * 1.2f;
                fish.Velocity = Vector2.Lerp(fish.Velocity, desired, 1f - Mathf.Exp(-dt / fish.Lag));
                fish.Position += fish.Velocity * dt;
                fish.Position.y = Mathf.Clamp(fish.Position.y, 0.3f, height - 0.25f);

                // It turns about its own spine to face the way it is going: end on half way round.
                float facing = fish.Velocity.x >= 0f ? 0f : Mathf.PI;
                fish.Yaw = Mathf.MoveTowards(fish.Yaw, facing, fish.TurnSpeed * dt);

                float swim = fish.Velocity.magnitude / Mathf.Max(speed, 0.05f);
                fish.Tail += dt * (7f + 9f * Mathf.Clamp(swim, 0f, 2f));
            }
        }

        float Random01() => (float)m_Random.NextDouble();

        // ---------------------------------------------------------------- the shader

        void Upload()
        {
            if (m_Map == null)
                return;

            // Bubbles, packed: u and height, radius, opacity. Each forms at its vent, swells as it rises,
            // zigzags more the bigger it is, and fades as it reaches the surface.
            float height = CaveScreens.Height;
            float top = height - 0.08f;
            int bubbles = 0;
            for (int i = 0; i < MaxBubbles; i++)
            {
                var bubble = m_Bubbles[i];
                if (!bubble.Alive)
                    continue;

                float grow = Mathf.Clamp01(bubble.Age / 0.2f);
                float radius = bubble.Radius * (1f + 0.35f * bubble.Y / height) * Mathf.Lerp(0.3f, 1f, grow);
                float u = bubble.U + (0.002f + 0.35f * bubble.Radius) * Mathf.Sin(bubble.Age * bubble.Wobble + bubble.Seed);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(top - 0.15f, top, bubble.Y));
                m_BubbleData[bubbles++] = new Vector4(u, bubble.Y, radius, fade);
            }

            // Fish: u and height, then the way it faces, shortened as it turns end on; and its length, the
            // tail's swing, its flash and its haze. A fish flashes silver as it turns, its flank to the light.
            float beat = BeatFlashEnabled ? m_Analyser.BeatPulse * 0.15f : 0f;
            for (int i = 0; i < m_FishCount; i++)
            {
                var fish = m_School[i];
                float side = Mathf.Cos(fish.Yaw);
                float pitch = Mathf.Clamp(Mathf.Atan2(fish.Velocity.y, Mathf.Max(Mathf.Abs(fish.Velocity.x), 0.05f)), -0.5f, 0.5f);
                float angle = side >= 0f ? pitch : Mathf.PI - pitch;
                float shown = Mathf.Max(Mathf.Abs(side), 0.22f);
                float turning = Mathf.Sin(fish.Yaw);
                float flash = Mathf.Clamp01(turning * turning + beat);

                m_FishData[i] = new Vector4(fish.Position.x, fish.Position.y, Mathf.Cos(angle) * shown, Mathf.Sin(angle) * shown);
                m_FishInfo[i] = new Vector4(fish.Length, Mathf.Sin(fish.Tail), flash, fish.Haze);
            }

            // The seas, fading from one to the next.
            float at = m_PaletteSeconds > 0f ? Time.time / m_PaletteSeconds : 0f;
            int from = Mathf.FloorToInt(at) % k_Palettes.Length;
            int to = (from + 1) % k_Palettes.Length;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((at - Mathf.Floor(at) - 0.75f) / 0.25f));
            var a = k_Palettes[from];
            var b = k_Palettes[to];

            // The current leans the kelp one way, then, over a minute or so, the other.
            float lean = m_KelpSurge * m_Surge * Mathf.Sin(Time.time * 0.05f + 0.6f);

            m_Block.Clear();
            m_Block.SetFloat(k_BubbleCount, bubbles);
            m_Block.SetVectorArray(k_BubblesId, m_BubbleData);
            m_Block.SetFloat(k_FishCount, m_FishCount);
            m_Block.SetVectorArray(k_FishId, m_FishData);
            m_Block.SetVectorArray(k_FishInfo, m_FishInfo);
            m_Block.SetFloat(k_KelpCount, m_KelpCount);
            m_Block.SetVectorArray(k_KelpId, m_KelpData);
            m_Block.SetVector(k_Room, new Vector4(CaveScreens.HalfWidth, CaveScreens.FrontZ, height, 0f));
            m_Block.SetVector(k_Sun, new Vector4(m_SunSlant.x, m_SunSlant.y, 0f, 0f));
            m_Block.SetVector(k_Phase, new Vector4(m_CausticPhase, m_SnowPhase, m_KelpPhase, Time.time));
            m_Block.SetVector(k_Music, new Vector4(m_Light, m_Glitter, m_SwayAmplitude, lean));
            m_Block.SetColor(k_Shallow, Color.Lerp(a.shallow, b.shallow, blend));
            m_Block.SetColor(k_Deep, Color.Lerp(a.deep, b.deep, blend));
            m_Block.SetColor(k_LightColour, Color.Lerp(a.light, b.light, blend));
            m_Block.SetColor(k_Sand, Color.Lerp(a.sand, b.sand, blend));
            m_Block.SetColor(k_KelpColour, Color.Lerp(a.kelp, b.kelp, blend));
            m_Block.SetFloat(k_Plankton, Mathf.Lerp(a.glow, b.glow, blend));
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
                renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}

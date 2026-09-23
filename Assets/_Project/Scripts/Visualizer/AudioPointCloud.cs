using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>The things a cloud of points can be arranged into.</summary>
    public enum CloudShape
    {
        /// <summary>
        /// The spectrogram as a field of points: frequency across the room, time flowing from the
        /// front wall toward the back, level as a column of points rising from the floor.
        /// </summary>
        SpectrumField,

        /// <summary>
        /// Musica universalis. A sun at the room's centre wearing the spectrogram on its surface,
        /// ringed by celestial spheres at harmonic radii, each sphere one band, orbiting at
        /// Kepler's rates, a small planet riding each one.
        /// </summary>
        AstralBody,

        /// <summary>
        /// A swarm of fireflies in a box that never changes size. The music shows as how many are
        /// lit, how fast they dart and how they blink — a fixed scale, so a change in the audio is
        /// a change in the swarm, never in its extent.
        /// </summary>
        Fireflies,
    }

    /// <summary>
    /// A point cloud driven by the analyser, drawn as billboarded sprites through a
    /// <see cref="ParticleSystem"/> whose particles are set by hand every frame. The system does no
    /// emitting or simulating of its own; it is used for what it is good at — thousands of
    /// camera-facing quads that render correctly through every one of MiddleVR's cameras.
    /// </summary>
    /// <remarks>
    /// The arithmetic per point is small and the count is a few thousand, so the layout is done
    /// on the CPU in plain C# where every number is in the Inspector, rather than in a vertex
    /// shader where it would be faster and opaque. Per-point colour carries hue and level; the
    /// material's HDR colour, set through a property block, carries the brightness, opacity and
    /// the beat, so a fade costs nothing per point.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class AudioPointCloud : MonoBehaviour
    {
        [SerializeField] AudioAnalyser m_Analyser;
        [SerializeField] CloudShape m_Shape = CloudShape.SpectrumField;

        [Header("Points")]
        [SerializeField, Tooltip("Sprite size in metres of a point at zero level.")]
        float m_PointSize = 0.016f;

        [SerializeField, Tooltip("Extra size at full level.")]
        float m_SizeByLevel = 0.028f;

        [SerializeField] float m_Brightness = 1.2f;
        [SerializeField] float m_BeatBrightness = 0.7f;

        [SerializeField, Tooltip("Metres the cloud jumps on a beat: the field bounces, the body's spheres breathe out.")]
        float m_BeatKick = 0.06f;

        [Header("Spectrum field")]
        [SerializeField, Tooltip("Where the floor of the field sits, in room metres. Columns rise from here.")]
        Vector3 m_FieldFloor = new(0f, 0.12f, 0f);

        [SerializeField, Tooltip("Width across the room, height at full level, depth of history from the front wall back.")]
        Vector3 m_FieldSize = new(3.0f, 1.8f, 2.0f);

        [SerializeField, Range(1, 16), Tooltip("Points in each column. A column at full level spreads them up its whole height.")]
        int m_Stack = 6;

        [SerializeField, Tooltip("Band order across the room. Mirrored puts the bass down the middle, matching the walls.")]
        TubeLayout m_FieldLayout = TubeLayout.Mirrored;

        [SerializeField, Tooltip("Metres of sideways shimmer a point has at full level.")]
        float m_Shimmer = 0.02f;

        [SerializeField, Tooltip("Point colour by band, bass at 0 and treble at 1. Left plain white for the builder's default.")]
        Gradient m_FieldPalette;

        [Header("Astral body")]
        [SerializeField, Tooltip("The sun's centre. Forward of the room's middle so a viewer standing in the middle is not inside it.")]
        Vector3 m_BodyCentre = new(0f, 1.3f, 0.3f);

        [SerializeField] float m_CoreRadius = 0.24f;
        [SerializeField] int m_CorePoints = 3200;

        [SerializeField, Tooltip("How far the sun's surface rises at full level, as a fraction of its radius.")]
        float m_CoreBulge = 0.4f;

        [SerializeField, Tooltip("Sun colour by level, 0 quiet to 1 loud. Left plain white for the builder's default.")]
        Gradient m_CorePalette;

        [SerializeField, Tooltip("Radii of the celestial spheres as multiples of the core radius. Just intonation: major third, " +
            "fifth, major sixth, octave, tenth, twelfth. Innermost first.")]
        float[] m_SphereRatios = { 1.25f, 1.5f, 1.6667f, 2f, 2.5f, 3f };

        [SerializeField, Tooltip("Tilt of each sphere's plane in degrees, so the rig reads as an armillary sphere rather than a stack of discs.")]
        float[] m_Inclinations = { 0f, 14f, -20f, 27f, -9f, 34f };

        [SerializeField] int m_SpherePoints = 480;

        [SerializeField, Tooltip("How far a sphere's points scatter from its circle, in metres, at zero level. Triples at full level.")]
        float m_SphereThickness = 0.012f;

        [SerializeField, Tooltip("Revolutions per second of a sphere at ratio 1. The others follow Kepler's third law, slower with radius^1.5.")]
        float m_OrbitRate = 0.09f;

        [SerializeField] int m_PlanetPoints = 64;
        [SerializeField] float m_PlanetRadius = 0.04f;

        [SerializeField, Tooltip("Revolutions per second the whole body turns about the vertical, so it moves for a viewer standing still.")]
        float m_Precession = 0.012f;

        [SerializeField, Tooltip("Sphere and planet colour by band, bass at 0 and treble at 1. Left plain white for the builder's default.")]
        Gradient m_SpherePalette;

        [Header("Fireflies")]
        [SerializeField, Tooltip("Centre of the swarm's box, in room metres.")]
        Vector3 m_SwarmCentre = new(0f, 1.1f, 0f);

        [SerializeField, Tooltip("The box the swarm fills. Fixed: it does not grow with the music, which is the point.")]
        Vector3 m_SwarmSize = new(3.0f, 1.7f, 2.0f);

        [SerializeField] int m_Fireflies = 2400;

        [SerializeField, Tooltip("Bass fireflies live low in the box and treble ones high, so the spectrum reads as height.")]
        bool m_BandsByHeight = true;

        [SerializeField, Tooltip("Metres a firefly wanders from its home.")]
        float m_Wander = 0.3f;

        [SerializeField, Tooltip("Wander speed when its band is silent, in cycles per second.")]
        float m_RestSpeed = 0.12f;

        [SerializeField, Tooltip("Extra wander speed at full level. Loud bands dart.")]
        float m_DartSpeed = 1.0f;

        [SerializeField, Tooltip("Blinks per second of a lit firefly at full level. A fifth of that at silence.")]
        float m_BlinkRate = 0.7f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Brightness of an unlit firefly, so the swarm's box always shows.")]
        float m_Unlit = 0.05f;

        [SerializeField, Range(0f, 1f), Tooltip("How hard a beat flashes the whole swarm at once, like synchronous fireflies.")]
        float m_Synchrony = 0.8f;

        [SerializeField, Tooltip("Firefly colour by band, bass at 0 and treble at 1. Left plain white for the builder's default.")]
        Gradient m_FireflyPalette;

        [SerializeField] int m_Seed = 11;

        public float Opacity { get; set; } = 1f;
        public float Intensity { get; set; } = 1f;
        public CloudShape Shape => m_Shape;
        public int Count => m_Count;
        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        static readonly int k_Color = Shader.PropertyToID("_Color");
        const float k_Lifetime = 1e6f;

        ParticleSystem m_System;
        ParticleSystemRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        ParticleSystem.Particle[] m_Particles = new ParticleSystem.Particle[0];
        int m_Count;

        // Per-point constants, laid out once per allocation.
        Vector3[] m_Direction = new Vector3[0];  // unit sphere directions for the core and the planets
        float[] m_Seed1 = new float[0];
        float[] m_Seed2 = new float[0];

        // Per-firefly state: where it lives, and two clocks that run faster when its band is louder.
        Vector3[] m_Home = new Vector3[0];
        float[] m_WanderClock = new float[0];
        float[] m_BlinkClock = new float[0];

        // Per-sphere state.
        Quaternion[] m_SphereFrame = new Quaternion[0];
        float[] m_SphereAngle = new float[0];
        float[] m_SphereLevel = new float[0];

        int m_AllocatedBands = -1;
        int m_AllocatedSlices = -1;
        CloudShape m_AllocatedShape;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            m_Block = new MaterialPropertyBlock();

            if (Empty(m_FieldPalette))
                m_FieldPalette = DefaultFieldPalette();
            if (Empty(m_CorePalette))
                m_CorePalette = DefaultCorePalette();
            if (Empty(m_SpherePalette))
                m_SpherePalette = DefaultSpherePalette();
            if (Empty(m_FireflyPalette))
                m_FireflyPalette = DefaultFireflyPalette();

            ConfigureSystem();
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        static bool Empty(Gradient gradient) => Palettes.IsUnset(gradient);

        /// <summary>Deep blue at the bass end to white at the treble, for points that also carry level as brightness.</summary>
        public static Gradient DefaultFieldPalette() => Make(
            (new Color(0.15f, 0.25f, 1f), 0f), (new Color(0.2f, 0.8f, 1f), 0.45f),
            (new Color(0.7f, 1f, 0.95f), 0.8f), (new Color(1f, 1f, 1f), 1f));

        /// <summary>A sun: dark ember when quiet, through orange to white-hot when loud.</summary>
        public static Gradient DefaultCorePalette() => Make(
            (new Color(0.35f, 0.05f, 0.02f), 0f), (new Color(1f, 0.35f, 0.05f), 0.4f),
            (new Color(1f, 0.8f, 0.3f), 0.75f), (new Color(1f, 1f, 0.9f), 1f));

        /// <summary>The spheres: violet-blue for the slow outer bass ones, cyan-white for the quick inner treble.</summary>
        public static Gradient DefaultSpherePalette() => Make(
            (new Color(0.45f, 0.2f, 1f), 0f), (new Color(0.25f, 0.55f, 1f), 0.4f),
            (new Color(0.3f, 0.95f, 1f), 0.75f), (new Color(0.9f, 1f, 1f), 1f));

        /// <summary>Firefly light: amber for the bass, the classic yellow-green through the middle, cool white at the treble.</summary>
        public static Gradient DefaultFireflyPalette() => Make(
            (new Color(1f, 0.6f, 0.15f), 0f), (new Color(0.85f, 1f, 0.3f), 0.4f),
            (new Color(0.5f, 1f, 0.55f), 0.75f), (new Color(0.8f, 1f, 0.95f), 1f));

        static Gradient Make(params (Color colour, float at)[] keys)
        {
            var gradient = new Gradient();
            var colours = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                colours[i] = new GradientColorKey(keys[i].colour, keys[i].at);

            gradient.SetKeys(colours, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // ---------------------------------------------------------------- allocation

        /// <summary>
        /// Nothing emits and nothing simulates: the particles are written every frame. Their
        /// lifetime is set huge and reset each frame so the system's own ageing never removes one.
        /// </summary>
        void ConfigureSystem()
        {
            var main = m_System.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = k_Lifetime;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.Max(main.maxParticles, 1);

            var emission = m_System.emission;
            emission.enabled = false;

            var shape = m_System.shape;
            shape.enabled = false;

            m_Renderer.renderMode = ParticleSystemRenderMode.Billboard;
            m_Renderer.alignment = ParticleSystemRenderSpace.View;
            m_Renderer.sortMode = ParticleSystemSortMode.None;
            m_Renderer.minParticleSize = 0f;
            m_Renderer.maxParticleSize = 1f;
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
        }

        int Spheres => Mathf.Min(m_SphereRatios != null ? m_SphereRatios.Length : 0,
            m_Inclinations != null ? m_Inclinations.Length : 0);

        int CountFor(CloudShape shape, int bands, int slices)
        {
            switch (shape)
            {
                case CloudShape.SpectrumField:
                    return bands * slices * m_Stack;
                case CloudShape.Fireflies:
                    return Mathf.Max(0, m_Fireflies);
                default:
                    return m_CorePoints + Spheres * (m_SpherePoints + m_PlanetPoints);
            }
        }

        void Allocate(int bands, int slices)
        {
            m_Count = CountFor(m_Shape, bands, slices);
            m_Particles = new ParticleSystem.Particle[m_Count];
            m_Direction = new Vector3[m_Count];
            m_Seed1 = new float[m_Count];
            m_Seed2 = new float[m_Count];

            var random = new System.Random(m_Seed);
            for (int i = 0; i < m_Count; i++)
            {
                m_Seed1[i] = (float)random.NextDouble();
                m_Seed2[i] = (float)random.NextDouble();
                m_Particles[i].startLifetime = k_Lifetime;
                m_Particles[i].remainingLifetime = k_Lifetime;
            }

            // The sun's points on a Fibonacci sphere: even everywhere, no pole crowding, so the
            // spectrogram wrapped onto it has the same resolution at the equator and the poles.
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < Mathf.Min(m_CorePoints, m_Count); i++)
            {
                float y = 1f - 2f * (i + 0.5f) / m_CorePoints;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float angle = golden * i;
                m_Direction[i] = new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle));
            }

            // Everything after the core gets a random direction in the unit ball, for the planets.
            for (int i = m_CorePoints; i < m_Count; i++)
            {
                var direction = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f,
                    (float)random.NextDouble() * 2f - 1f);
                m_Direction[i] = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.up;
            }

            // Each sphere's plane: tilted by its inclination, its line of nodes turned on from the
            // last so the tilts do not all lean the same way.
            int spheres = Spheres;
            m_SphereFrame = new Quaternion[spheres];
            m_SphereAngle = new float[spheres];
            m_SphereLevel = new float[spheres];
            for (int k = 0; k < spheres; k++)
            {
                m_SphereFrame[k] = Quaternion.AngleAxis(k * 61f, Vector3.up) * Quaternion.AngleAxis(m_Inclinations[k], Vector3.right);
                m_SphereAngle[k] = k * 1.7f;
            }

            // Fireflies: a home each, inside the box by the wander margin so the swarm's full
            // extent is the box and no more; by band up the box if asked, with enough spread that
            // the bands overlap rather than stack in slabs.
            if (m_Shape == CloudShape.Fireflies)
            {
                m_Home = new Vector3[m_Count];
                m_WanderClock = new float[m_Count];
                m_BlinkClock = new float[m_Count];

                var inner = m_SwarmSize - Vector3.one * (2f * m_Wander);
                inner = Vector3.Max(inner, Vector3.one * 0.1f);

                for (int i = 0; i < m_Count; i++)
                {
                    float x = (float)random.NextDouble() - 0.5f;
                    float y = (float)random.NextDouble() - 0.5f;
                    float z = (float)random.NextDouble() - 0.5f;

                    if (m_BandsByHeight)
                    {
                        float bandT = BandOfFirefly(i, bands, Mathf.Max(1, m_Count)) / (float)Mathf.Max(1, bands - 1);
                        y = Mathf.Clamp(bandT + ((float)random.NextDouble() - 0.5f) * 0.25f, 0f, 1f) - 0.5f;
                    }

                    m_Home[i] = m_SwarmCentre + new Vector3(x * inner.x, y * inner.y, z * inner.z);
                    m_WanderClock[i] = (float)random.NextDouble() * 100f;
                    m_BlinkClock[i] = (float)random.NextDouble();
                }
            }

            var main = m_System.main;
            main.maxParticles = Mathf.Max(1, m_Count);

            m_AllocatedBands = bands;
            m_AllocatedSlices = slices;
            m_AllocatedShape = m_Shape;
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (m_Analyser == null)
                return;

            int bands = m_Analyser.BandCount;
            int slices = m_Analyser.HistorySlices;
            if (bands != m_AllocatedBands || slices != m_AllocatedSlices || m_Shape != m_AllocatedShape ||
                m_Particles.Length != CountFor(m_Shape, bands, slices))
                Allocate(bands, slices);

            if (m_Count == 0)
                return;

            float beat = m_Analyser.BeatPulse;
            float idle = m_Analyser.Idle;

            switch (m_Shape)
            {
                case CloudShape.SpectrumField:
                    LayoutField(bands, slices, beat, idle);
                    break;
                case CloudShape.Fireflies:
                    LayoutFireflies(bands, beat, idle);
                    break;
                default:
                    LayoutBody(bands, slices, beat, idle);
                    break;
            }

            m_System.SetParticles(m_Particles, m_Count);

            float gain = m_Brightness * Intensity * Opacity * (1f - 0.7f * idle) * (1f + m_BeatBrightness * beat);
            m_Block.SetColor(k_Color, new Color(gain, gain, gain, 1f));
            m_Renderer.SetPropertyBlock(m_Block);
        }

        /// <summary>
        /// One column of points per band per history slice. The newest slice sits at the front
        /// wall and the field slides toward the back between samples, so the music arrives from
        /// the front and flows through whoever is standing in the room.
        /// </summary>
        void LayoutField(int bands, int slices, float beat, float idle)
        {
            float time = Time.time;
            float phase = m_Analyser.HistoryPhase;
            float spacing = m_FieldSize.z / slices;
            float front = m_FieldFloor.z + m_FieldSize.z * 0.5f;
            float left = m_FieldFloor.x - m_FieldSize.x * 0.5f;
            float columnWidth = m_FieldSize.x / bands;
            float breathe = 0.08f + 0.04f * Mathf.Sin(time * 0.5f);

            int i = 0;
            for (int s = 0; s < slices; s++)
            {
                float z = front - (s + phase) * spacing;
                float age = s / (float)slices;

                for (int c = 0; c < bands; c++)
                {
                    int band = BandOfColumn(c, bands);
                    float level = Mathf.Lerp(m_Analyser.HistoryAt(s, band), breathe, idle);
                    float x = left + (c + 0.5f) * columnWidth;
                    var colour = m_FieldPalette.Evaluate(band / (float)Mathf.Max(1, bands - 1));
                    float fade = 1f - 0.6f * age;    // older slices dim toward the back wall

                    for (int k = 0; k < m_Stack; k++, i++)
                    {
                        float f = (k + 0.5f) / m_Stack;
                        float wobble = m_Shimmer * level * Mathf.Sin(time * 3f + m_Seed1[i] * 12.6f);
                        float y = m_FieldFloor.y + f * (level * m_FieldSize.y + m_BeatKick * beat);

                        ref var p = ref m_Particles[i];
                        p.position = new Vector3(x + wobble, y, z + wobble * 0.5f);
                        p.startSize = m_PointSize + m_SizeByLevel * level * f;
                        p.startColor = colour * ((0.2f + 0.8f * level) * (0.5f + 0.5f * f) * fade);
                        p.remainingLifetime = k_Lifetime;
                    }
                }
            }
        }

        /// <summary>The band a column reads, by the same rule the water panels use.</summary>
        int BandOfColumn(int column, int bands)
        {
            float half = (bands - 1) * 0.5f;
            switch (m_FieldLayout)
            {
                case TubeLayout.Descending:
                    return bands - 1 - column;
                case TubeLayout.Mirrored:
                    return half > 0f ? Mathf.RoundToInt(Mathf.Abs(column - half) / half * (bands - 1)) : 0;
                default:
                    return column;
            }
        }

        /// <summary>
        /// The sun wears the spectrogram: latitude is the band (bass at the equator, treble at the
        /// poles), longitude is time, newest at the front meridian, and the surface rises with the
        /// level. Around it the spheres, innermost highest in pitch and quickest in orbit, as
        /// Kepler had it. The whole body precesses slowly about the vertical.
        /// </summary>
        void LayoutBody(int bands, int slices, float beat, float idle)
        {
            float time = Time.time;
            float dt = Time.deltaTime;
            var precess = Quaternion.AngleAxis(time * m_Precession * 360f, Vector3.up);
            float breathe = 0.1f + 0.05f * Mathf.Sin(time * 0.4f);
            int i = 0;

            // The core.
            for (int c = 0; c < m_CorePoints && i < m_Count; c++, i++)
            {
                var direction = m_Direction[i];
                float latitude = Mathf.Abs(Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f))) / (Mathf.PI * 0.5f);
                float longitude = (Mathf.Atan2(direction.z, direction.x) + Mathf.PI) / (2f * Mathf.PI);   // 0 … 1
                int band = Mathf.RoundToInt(latitude * (bands - 1));
                float level = Mathf.Lerp(m_Analyser.HistoryAt(longitude * (slices - 1), band), breathe, idle);

                float radius = m_CoreRadius * (1f + m_CoreBulge * level) + m_BeatKick * 0.5f * beat;

                ref var p = ref m_Particles[i];
                p.position = m_BodyCentre + precess * (direction * radius);
                p.startSize = m_PointSize + m_SizeByLevel * level;
                p.startColor = m_CorePalette.Evaluate(level);
                p.remainingLifetime = k_Lifetime;
            }

            // The spheres, each one band: the innermost the highest band, the outermost the lowest.
            int spheres = Spheres;
            for (int k = 0; k < spheres; k++)
            {
                float ratio = Mathf.Max(0.1f, m_SphereRatios[k]);
                float bandT = spheres > 1 ? 1f - k / (float)(spheres - 1) : 0.5f;
                int band = Mathf.RoundToInt(bandT * (bands - 1));
                float level = Mathf.Lerp(m_Analyser.Bands[Mathf.Clamp(band, 0, bands - 1)], breathe, idle);
                m_SphereLevel[k] += (level - m_SphereLevel[k]) * Mathf.Min(1f, dt * 12f);
                level = m_SphereLevel[k];

                // Kepler's third law: period grows with radius^1.5. The music hurries the orbit a
                // little, so the angle is integrated rather than read off the clock.
                float rate = m_OrbitRate * Mathf.Pow(ratio, -1.5f) * (1f + 0.6f * level);
                m_SphereAngle[k] += rate * 2f * Mathf.PI * dt;

                float radius = m_CoreRadius * ratio * (1f + 0.1f * level) + m_BeatKick * beat;
                var frame = precess * m_SphereFrame[k];
                var colour = m_SpherePalette.Evaluate(bandT) * (0.3f + 0.7f * level);
                float scatter = m_SphereThickness * (1f + 2f * level);

                for (int j = 0; j < m_SpherePoints && i < m_Count; j++, i++)
                {
                    float angle = m_SphereAngle[k] + 2f * Mathf.PI * j / m_SpherePoints;
                    float dr = (m_Seed1[i] - 0.5f) * 2f * scatter;
                    float dy = (m_Seed2[i] - 0.5f) * 2f * scatter;
                    var local = new Vector3(Mathf.Cos(angle) * (radius + dr), dy, Mathf.Sin(angle) * (radius + dr));

                    ref var p = ref m_Particles[i];
                    p.position = m_BodyCentre + frame * local;
                    p.startSize = m_PointSize * (0.7f + 0.8f * level);
                    p.startColor = colour;
                    p.remainingLifetime = k_Lifetime;
                }

                // The planet: a ball of points riding the sphere, swelling with its band.
                float planetAngle = m_SphereAngle[k] * 1.5f + k * 2.1f;
                var planetCentre = new Vector3(Mathf.Cos(planetAngle) * radius, 0f, Mathf.Sin(planetAngle) * radius);
                float planetRadius = m_PlanetRadius * (0.7f + 0.8f * level);
                var planetColour = Color.Lerp(colour, Color.white, 0.4f) * (0.6f + 0.6f * level);

                for (int j = 0; j < m_PlanetPoints && i < m_Count; j++, i++)
                {
                    // Cube root of a uniform number puts points evenly through the ball, not bunched at its centre.
                    float depth = Mathf.Pow(m_Seed1[i], 1f / 3f);
                    var local = planetCentre + m_Direction[i] * (planetRadius * depth);

                    ref var p = ref m_Particles[i];
                    p.position = m_BodyCentre + frame * local;
                    p.startSize = m_PointSize * 1.3f;
                    p.startColor = planetColour;
                    p.remainingLifetime = k_Lifetime;
                }
            }

            // Anything left over (a count that shrank mid-frame) is parked out of sight.
            for (; i < m_Count; i++)
            {
                m_Particles[i].startSize = 0f;
                m_Particles[i].remainingLifetime = k_Lifetime;
            }
        }

        /// <summary>Fireflies are dealt to the bands in equal blocks, so every band has the same number to light.</summary>
        static int BandOfFirefly(int index, int bands, int count) => Mathf.Clamp(index * bands / count, 0, bands - 1);

        /// <summary>
        /// The swarm. Each firefly wanders around its home on a sum of sines whose clock runs
        /// faster the louder its band is; it is lit when its band's level is above its own random
        /// threshold, so the lit fraction of a band <em>is</em> its level; a lit one blinks at a
        /// rate that rises with the level; and a beat flashes every one of them together. The box
        /// never changes size — that is what makes the rest legible.
        /// </summary>
        void LayoutFireflies(int bands, float beat, float idle)
        {
            float dt = Time.deltaTime;
            float breathe = 0.08f + 0.05f * Mathf.Sin(Time.time * 0.4f);
            float flash = beat * m_Synchrony;
            float twoPi = 2f * Mathf.PI;

            for (int i = 0; i < m_Count; i++)
            {
                int band = BandOfFirefly(i, bands, m_Count);
                float bandT = band / (float)Mathf.Max(1, bands - 1);
                float level = Mathf.Lerp(m_Analyser.Bands[band], breathe, idle);

                // Wander: three incommensurate sines per axis, seeded per fly, on a clock the band drives.
                m_WanderClock[i] += (m_RestSpeed + m_DartSpeed * level) * dt;
                float c = m_WanderClock[i];
                float s1 = m_Seed1[i] * twoPi;
                float s2 = m_Seed2[i] * twoPi;
                var offset = new Vector3(
                    0.6f * Mathf.Sin(c * 1.0f + s1) + 0.4f * Mathf.Sin(c * 2.3f + s2),
                    0.6f * Mathf.Sin(c * 1.3f + s2) + 0.4f * Mathf.Sin(c * 0.7f + s1 * 0.5f),
                    0.6f * Mathf.Sin(c * 0.9f + s1 * 0.33f) + 0.4f * Mathf.Sin(c * 1.9f + s2 * 0.8f));

                // Lit or not: the threshold is this fly's own, spread so the last few need a full-scale level.
                bool lit = level > m_Seed1[i] * 0.97f;

                // Blink: a clock that runs faster when louder, shaped into a flash with a dark gap.
                float rate = Mathf.Lerp(m_BlinkRate * 0.2f, m_BlinkRate, level) * (0.7f + 0.6f * m_Seed2[i]);
                m_BlinkClock[i] += rate * dt;
                float pulse = Mathf.Sin(Mathf.Repeat(m_BlinkClock[i], 1f) * Mathf.PI);
                pulse = pulse * pulse * pulse;

                float glow = lit ? 0.3f + 0.7f * pulse : 0f;
                glow = Mathf.Max(glow, flash);
                float brightness = m_Unlit + glow * (1f - m_Unlit);

                ref var p = ref m_Particles[i];
                p.position = m_Home[i] + offset * m_Wander;
                p.startSize = m_PointSize * (0.5f + 1.2f * glow);
                p.startColor = m_FireflyPalette.Evaluate(bandT) * brightness;
                p.remainingLifetime = k_Lifetime;
            }
        }
    }
}

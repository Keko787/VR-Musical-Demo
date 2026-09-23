using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// The 3D visualizer's lasers: a few emitters sitting on the CAVE's screens, each fanning a
    /// handful of beams across the volume to another screen. Every beam is one frequency band —
    /// its width and brightness follow that band — and the fan opens and closes with the music.
    /// On a beat an emitter may hop to another wall and re-aim, the cut a real laser rig makes;
    /// between beats the beams drift.
    /// </summary>
    /// <remarks>
    /// Both ends of every beam are on a surface. That is the CAVE's design rule from the deck
    /// (treat the room as the stage, not a window): the beam lives inside the bounded volume, and
    /// where it lands there is a hit-spot drawn flat on the screen, so the people without glasses
    /// still see a light show of moving dots on the walls even though the beams themselves are
    /// only right from the tracked viewpoint.
    /// </remarks>
    public class LaserShow : MonoBehaviour
    {
        [SerializeField] AudioAnalyser m_Analyser;

        [Header("Materials")]
        [SerializeField, Tooltip("Additive beam material (VRMusic/LaserBeam). Set by the builder.")]
        Material m_BeamMaterial;

        [SerializeField, Tooltip("Additive radial spot for where a beam meets a screen (VRMusic/LaserBeam, radial). Set by the builder.")]
        Material m_SpotMaterial;

        [Header("Rig")]
        [SerializeField, Range(1, 8)] int m_Emitters = 3;
        [SerializeField, Range(1, 12)] int m_BeamsPerEmitter = 5;

        [SerializeField, Tooltip("Beams may also end on the back wall and ceiling, which the projectors do not cover. " +
            "Off keeps every endpoint on a real screen, so every hit-spot is visible.")]
        bool m_OpenSurfaces;

        [SerializeField, Tooltip("Metres a beam's end sits in front of its screen, clear of the 2D panels.")]
        float m_Inset = 0.012f;

        [Header("Motion")]
        [SerializeField, Tooltip("How far the fan spreads across its target screen, as a fraction of the screen, when the band is silent.")]
        float m_FanClosed = 0.08f;

        [SerializeField, Tooltip("Spread at full band energy.")]
        float m_FanOpen = 0.45f;

        [SerializeField, Tooltip("Speed of the noise that drifts the endpoints between beats.")]
        float m_DriftSpeed = 0.12f;

        [SerializeField, Tooltip("How far the drift can wander, as a fraction of the screen.")]
        float m_DriftRange = 0.25f;

        [SerializeField, Range(0f, 1f), Tooltip("Chance that a beat makes one emitter hop to a new wall.")]
        float m_HopOnBeat = 0.55f;

        [SerializeField, Tooltip("Seconds a re-aim within a wall takes. A hop between walls is a cut.")]
        float m_SweepSeconds = 0.35f;

        [Header("Look")]
        [SerializeField, Tooltip("Beam colour by band, bass at 0 and treble at 1. Left empty for the builder's rave palette.")]
        Gradient m_Palette;

        [SerializeField] float m_CoreWidth = 0.008f;
        [SerializeField] float m_EnergyWidth = 0.022f;
        [SerializeField] float m_Brightness = 1.4f;
        [SerializeField] float m_BeatBrightness = 1.2f;
        [SerializeField] float m_SpotSize = 0.14f;
        [SerializeField] int m_Seed = 7;

        public float Opacity { get; set; } = 1f;
        public float Intensity { get; set; } = 1f;
        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        static readonly int k_Color = Shader.PropertyToID("_Color");

        class Emitter
        {
            public int Screen;
            public Vector2 Anchor;
            public float NoiseSeed;
            public Transform Spot;
            public Renderer SpotRenderer;
            public readonly List<Beam> Beams = new();
        }

        class Beam
        {
            public int Band;
            public int Screen;
            public Vector2 Anchor;
            public Vector2 From, To;   // sweep endpoints in target-screen UV
            public float Sweep;        // 0 → 1 over the sweep
            public Vector2 FanDirection;
            public float NoiseSeed;
            public LineRenderer Line;
            public Transform Spot;
            public Renderer SpotRenderer;
        }

        readonly List<Emitter> m_Rig = new();
        readonly Vector3[] m_Ends = new Vector3[2];
        MaterialPropertyBlock m_Block;
        System.Random m_Random;
        CaveScreen[] m_Screens;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_Random = new System.Random(m_Seed);

            if (m_Palette == null || m_Palette.colorKeys == null || m_Palette.colorKeys.Length == 0)
                m_Palette = DefaultPalette();

            if (m_Analyser != null)
                m_Analyser.OnBeat += OnBeat;

            BuildRig();
        }

        void OnDestroy()
        {
            if (m_Analyser != null)
                m_Analyser.OnBeat -= OnBeat;
        }

        /// <summary>Club colours: red at the bass end through magenta and blue to a green-white treble.</summary>
        public static Gradient DefaultPalette()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.08f, 0.1f), 0f),
                    new GradientColorKey(new Color(1f, 0.1f, 0.7f), 0.3f),
                    new GradientColorKey(new Color(0.2f, 0.3f, 1f), 0.6f),
                    new GradientColorKey(new Color(0.1f, 1f, 0.5f), 0.85f),
                    new GradientColorKey(new Color(0.8f, 1f, 0.9f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // ---------------------------------------------------------------- rig

        void BuildRig()
        {
            m_Screens = m_OpenSurfaces ? CaveScreens.All : CaveScreens.Projected;

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            m_Rig.Clear();

            int totalBeams = m_Emitters * m_BeamsPerEmitter;
            int bands = m_Analyser != null ? m_Analyser.BandCount : 24;

            for (int e = 0; e < m_Emitters; e++)
            {
                var emitter = new Emitter
                {
                    Screen = e % CaveScreens.Projected.Length,   // emitters always sit on a real screen
                    Anchor = RandomAnchor(),
                    NoiseSeed = (float)m_Random.NextDouble() * 100f,
                };

                var root = new GameObject($"Emitter_{e}").transform;
                root.SetParent(transform, false);

                emitter.Spot = MakeSpot(root, "Spot");
                emitter.SpotRenderer = emitter.Spot.GetComponent<Renderer>();

                for (int b = 0; b < m_BeamsPerEmitter; b++)
                {
                    int index = e * m_BeamsPerEmitter + b;
                    var beam = new Beam
                    {
                        Band = Mathf.RoundToInt(index / (float)Mathf.Max(1, totalBeams - 1) * (bands - 1)),
                        NoiseSeed = (float)m_Random.NextDouble() * 100f,
                        FanDirection = FanDirection(b, m_BeamsPerEmitter),
                    };

                    var go = new GameObject($"Beam_{b}", typeof(LineRenderer));
                    go.transform.SetParent(root, false);

                    var line = go.GetComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.alignment = LineAlignment.View;
                    line.textureMode = LineTextureMode.Stretch;
                    line.numCapVertices = 4;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    line.sharedMaterial = m_BeamMaterial;
                    line.startColor = line.endColor = Color.white;
                    beam.Line = line;

                    beam.Spot = MakeSpot(go.transform, "Spot");
                    beam.SpotRenderer = beam.Spot.GetComponent<Renderer>();

                    Retarget(emitter, beam, true);
                    emitter.Beams.Add(beam);
                }

                m_Rig.Add(emitter);
            }
        }

        Transform MakeSpot(Transform parent, string name)
        {
            var spot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spot.name = name;
            Destroy(spot.GetComponent<Collider>());
            spot.transform.SetParent(parent, false);

            var renderer = spot.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = m_SpotMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return spot.transform;
        }

        /// <summary>Beams of one fan spread along evenly spaced directions, so an open fan is a star, not a line.</summary>
        static Vector2 FanDirection(int index, int count)
        {
            float angle = count > 1 ? Mathf.PI * (index / (float)(count - 1)) - Mathf.PI * 0.5f : 0f;
            angle += 0.3f * Mathf.Sin(index * 2.7f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f);
        }

        Vector2 RandomAnchor()
        {
            return new Vector2(0.15f + 0.7f * (float)m_Random.NextDouble(), 0.2f + 0.6f * (float)m_Random.NextDouble());
        }

        /// <summary>A screen other than <paramref name="not"/>, projected ones preferred.</summary>
        int OtherScreen(int not)
        {
            int pick = not;
            for (int attempt = 0; attempt < 8 && pick == not; attempt++)
                pick = m_Random.Next(m_Screens.Length);

            return pick;
        }

        void Retarget(Emitter emitter, Beam beam, bool cut)
        {
            beam.Screen = OtherScreen(emitter.Screen);
            beam.Anchor = RandomAnchor();
            beam.From = cut ? beam.Anchor : Current(beam);
            beam.To = beam.Anchor;
            beam.Sweep = cut ? 1f : 0f;
        }

        static Vector2 Current(Beam beam)
        {
            float t = Mathf.SmoothStep(0f, 1f, beam.Sweep);
            return Vector2.Lerp(beam.From, beam.To, t);
        }

        // ---------------------------------------------------------------- beats

        void OnBeat(float strength)
        {
            if (m_Rig.Count == 0 || m_Random.NextDouble() > m_HopOnBeat * (0.5f + strength))
                return;

            var emitter = m_Rig[m_Random.Next(m_Rig.Count)];

            // A strong beat moves the whole emitter to another wall; a softer one only re-aims its fan.
            if (strength > 0.4f)
            {
                emitter.Screen = (emitter.Screen + 1 + m_Random.Next(CaveScreens.Projected.Length - 1)) % CaveScreens.Projected.Length;
                emitter.Anchor = RandomAnchor();
                foreach (var beam in emitter.Beams)
                    Retarget(emitter, beam, true);
            }
            else
            {
                foreach (var beam in emitter.Beams)
                {
                    beam.From = Current(beam);
                    beam.To = RandomAnchor();
                    beam.Sweep = 0f;
                }
            }
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (m_Analyser == null || m_Rig.Count == 0)
                return;

            float dt = Time.deltaTime;
            float time = Time.time;
            float idle = m_Analyser.Idle;
            float beat = m_Analyser.BeatPulse;
            float bright = m_Brightness * Intensity * Opacity * (1f - 0.75f * idle);
            int bands = Mathf.Max(1, m_Analyser.BandCount - 1);

            foreach (var emitter in m_Rig)
            {
                var source = m_Screens[emitter.Screen];
                var sourceUv = Drift(emitter.Anchor, emitter.NoiseSeed, time);
                var from = source.Point(sourceUv, m_Inset);

                float emitterEnergy = 0f;

                foreach (var beam in emitter.Beams)
                {
                    beam.Sweep = Mathf.Min(1f, beam.Sweep + dt / Mathf.Max(0.01f, m_SweepSeconds));

                    float energy = m_Analyser.Bands[Mathf.Clamp(beam.Band, 0, bands)];
                    emitterEnergy = Mathf.Max(emitterEnergy, energy);

                    var target = m_Screens[beam.Screen];
                    float spread = Mathf.Lerp(m_FanClosed, m_FanOpen, energy);
                    var uv = Current(beam) + beam.FanDirection * spread;
                    uv = Drift(uv, beam.NoiseSeed, time);
                    uv.x = Mathf.Clamp(uv.x, 0.03f, 0.97f);
                    uv.y = Mathf.Clamp(uv.y, 0.03f, 0.97f);
                    var to = target.Point(uv, m_Inset);

                    m_Ends[0] = from;
                    m_Ends[1] = to;
                    beam.Line.SetPositions(m_Ends);
                    beam.Line.widthMultiplier = m_CoreWidth + m_EnergyWidth * energy;

                    var colour = m_Palette.Evaluate(beam.Band / (float)bands);
                    float level = bright * (0.2f + 0.8f * energy) * (1f + m_BeatBrightness * beat);
                    SetColour(beam.Line, colour * level);

                    PlaceSpot(beam.Spot, target, to, m_SpotSize * (0.5f + energy));
                    SetColour(beam.SpotRenderer, colour * level * 1.3f);
                }

                var emitterColour = m_Palette.Evaluate(emitter.Beams.Count > 0
                    ? emitter.Beams[emitter.Beams.Count / 2].Band / (float)bands
                    : 0.5f);
                PlaceSpot(emitter.Spot, source, from, m_SpotSize * (0.8f + emitterEnergy));
                SetColour(emitter.SpotRenderer, emitterColour * bright * (0.5f + emitterEnergy) * (1f + m_BeatBrightness * beat));
            }
        }

        Vector2 Drift(Vector2 anchor, float seed, float time)
        {
            float t = time * m_DriftSpeed;
            float dx = Mathf.PerlinNoise(t + seed, seed * 0.37f) - 0.5f;
            float dy = Mathf.PerlinNoise(seed * 0.71f, t + seed * 1.3f) - 0.5f;
            return anchor + new Vector2(dx, dy) * (2f * m_DriftRange);
        }

        static void PlaceSpot(Transform spot, CaveScreen screen, Vector3 point, float size)
        {
            spot.localPosition = point - screen.Normal * 0.004f;
            spot.localRotation = screen.FacingRotation;
            spot.localScale = new Vector3(size, size, 1f);
        }

        void SetColour(Renderer renderer, Color colour)
        {
            m_Block.SetColor(k_Color, colour);
            renderer.SetPropertyBlock(m_Block);
        }
    }
}

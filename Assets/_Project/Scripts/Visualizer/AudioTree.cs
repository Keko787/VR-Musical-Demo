using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// A 3D show: a holographic tree of points standing in the room. It sways with the song — the
    /// bass sets how far, the beat sends a gust through it — and the beat shakes leaves loose, which
    /// flutter down turning autumn-coloured, lie on the floor a while, fade, and grow back on their
    /// twig. Each leaf is lit by one band, bass low in the crown and treble at the top, so the crown
    /// shows the spectrum; the trunk and branches are a thin holographic shell with scanlines
    /// running up it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree is grown once from a seed: a trunk that splits two or three ways, five times, each
    /// branch shorter and thinner than its parent and reaching a little upward, then scaled to fit
    /// <see cref="m_Height"/> and <see cref="m_Spread"/> so it stays inside the room. The sway is
    /// hierarchical: every branch bends on its parent toward the wind, the thin ones more and a little
    /// later than the thick ones, so a gust runs from the trunk out to the twigs.
    /// </para>
    /// <para>
    /// Drawn as the point clouds are: a <see cref="ParticleSystem"/> written by hand every frame
    /// (<see cref="ManualParticles"/>), the visualizer's additive point sprite, so it renders correctly
    /// through every one of MiddleVR's cameras. Right from the tracked viewpoint, like the other
    /// shows in the volume.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public class AudioTree : MonoBehaviour
    {
        [SerializeField] AudioAnalyser m_Analyser;

        [Header("Shape")]
        [SerializeField, Tooltip("Where the trunk stands on the floor, in the room's frame.")]
        Vector3 m_Base = new(0f, 0f, 0.2f);

        [SerializeField, Tooltip("Metres from the floor to the top of the crown.")]
        float m_Height = 2.0f;

        [SerializeField, Tooltip("Metres the crown reaches out from the trunk at most, so it stays clear of the walls.")]
        float m_Spread = 0.8f;

        [SerializeField, Range(3, 6), Tooltip("Times the branches split.")]
        int m_Depth = 5;

        [SerializeField, Tooltip("Leaves at the end of each twig.")]
        int m_LeavesPerTwig = 14;

        [SerializeField, Tooltip("Metres a twig's leaves spread round it.")]
        float m_LeafCluster = 0.08f;

        [SerializeField, Tooltip("Points the trunk and branches are drawn with, at most.")]
        int m_BranchPoints = 3200;

        [SerializeField, Tooltip("Revolutions a second the whole tree turns, so its depth shows to someone standing still.")]
        float m_Turn = 0.008f;

        [SerializeField] int m_Seed = 23;

        [Header("Sway")]
        [SerializeField, Tooltip("Sway with nothing playing.")]
        float m_CalmSway = 0.2f;

        [SerializeField, Tooltip("Extra sway at full bass.")]
        float m_BassSway = 0.9f;

        [SerializeField, Tooltip("Sway a beat adds for a moment, by its strength: the gust.")]
        float m_Gust = 0.6f;

        [SerializeField, Tooltip("Swings a second.")]
        float m_SwayHz = 0.3f;

        [SerializeField, Tooltip("Radians each depth of branch bends at sway 1, trunk first. Thin branches bend more.")]
        float[] m_Flex = { 0.02f, 0.04f, 0.06f, 0.08f, 0.1f, 0.12f, 0.14f };

        [SerializeField, Tooltip("Metres a leaf flutters at full mids.")]
        float m_Flutter = 0.012f;

        [Header("Leaves falling")]
        [SerializeField, Tooltip("Leaves a beat shakes loose, at middling strength. A soft beat drops fewer, a big one more.")]
        float m_LeavesPerBeat = 14f;

        [SerializeField, Range(0f, 1f), Tooltip("Leaves stop falling while fewer than this share are still on the tree.")]
        float m_KeepLeaves = 0.45f;

        [SerializeField, Tooltip("Metres a second a leaf falls.")]
        Vector2 m_FallSpeed = new(0.22f, 0.4f);

        [SerializeField, Tooltip("Seconds a fallen leaf lies on the floor before it fades.")]
        float m_LieSeconds = 4f;

        [SerializeField, Tooltip("Seconds a leaf takes to grow back on its twig.")]
        float m_RegrowSeconds = 3f;

        [Header("Look")]
        [SerializeField] float m_Brightness = 1.2f;
        [SerializeField] float m_BeatBrightness = 0.35f;
        [SerializeField] Color m_Hologram = new(0.3f, 0.85f, 1f);
        [SerializeField] Color m_Autumn = new(1f, 0.55f, 0.12f);
        [SerializeField] float m_BranchSize = 0.013f;
        [SerializeField] float m_LeafSize = 0.024f;

        [SerializeField, Tooltip("Leaf colour by band, bass at 0 and treble at 1. Left plain white for the default greens.")]
        Gradient m_LeafPalette;

        public float Opacity { get; set; } = 1f;
        public float Intensity { get; set; } = 1f;
        public AudioAnalyser Analyser { get => m_Analyser; set => m_Analyser = value; }

        static readonly int k_Color = Shader.PropertyToID("_Color");

        struct Branch
        {
            public int Parent;
            public Vector3 Start;                    // at rest, in the tree's frame (base at the origin)
            public Vector3 Direction;
            public float Length;
            public float Radius;
            public int Depth;
            public bool Twig;
        }

        enum LeafState { On, Falling, Lying, Growing }

        struct Leaf
        {
            public int Branch;
            public Vector3 Rest;                     // on the tree, at rest, in the tree's frame
            public float Band;                       // 0 bass … 1 treble, as a fraction
            public LeafState State;
            public float Timer;
            public Vector3 Position;                 // while falling or lying, in the tree's frame
            public float Fall;
            public float Seed;
        }

        readonly List<Branch> m_Branches = new();
        readonly List<(int branch, Vector3 rest)> m_Bark = new();
        Leaf[] m_Leaves = new Leaf[0];

        Vector3[] m_NowStart = new Vector3[0];
        Quaternion[] m_NowTurn = new Quaternion[0];

        ParticleSystem m_System;
        ParticleSystemRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        ParticleSystem.Particle[] m_Particles = new ParticleSystem.Particle[0];
        System.Random m_Random;

        float m_Sway;
        float m_GustLevel;
        float m_SwayPhase;
        float m_WindAngle;
        float m_TurnAngle;
        int m_OnTree;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            m_Block = new MaterialPropertyBlock();
            if (Palettes.IsUnset(m_LeafPalette))
                m_LeafPalette = DefaultLeafPalette();

            ManualParticles.Configure(m_System, m_Renderer);
            Grow();
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        /// <summary>Young green for the bass leaves low in the crown, through aqua to a pale violet at the treble top.</summary>
        public static Gradient DefaultLeafPalette()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.25f, 1f, 0.35f), 0f),
                    new GradientColorKey(new Color(0.3f, 1f, 0.8f), 0.45f),
                    new GradientColorKey(new Color(0.45f, 0.8f, 1f), 0.75f),
                    new GradientColorKey(new Color(0.8f, 0.7f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // ---------------------------------------------------------------- growing

        void Grow()
        {
            m_Random = new System.Random(m_Seed);
            m_Branches.Clear();

            // The trunk, leaning a touch, then the splits.
            var lean = new Vector3(Random(-0.06f, 0.06f), 1f, Random(-0.06f, 0.06f)).normalized;
            Add(-1, Vector3.zero, lean, 0.85f, 0.055f, 0);
            for (int i = 0; i < m_Branches.Count; i++)
            {
                var parent = m_Branches[i];
                if (parent.Depth >= m_Depth)
                    continue;

                int children = parent.Depth == 0 ? 3 : Random01() < 0.5f ? 2 : 3;
                float spin = Random01() * Mathf.PI * 2f;
                var side = Vector3.Cross(parent.Direction, Mathf.Abs(parent.Direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var other = Vector3.Cross(parent.Direction, side);

                for (int c = 0; c < children; c++)
                {
                    float around = spin + c * Mathf.PI * 2f / children + Random(-0.3f, 0.3f);
                    float tilt = Random(25f, 42f) * Mathf.Deg2Rad;
                    var outward = side * Mathf.Cos(around) + other * Mathf.Sin(around);
                    var direction = (parent.Direction * Mathf.Cos(tilt) + outward * Mathf.Sin(tilt) + Vector3.up * 0.18f).normalized;

                    // Of three, one comes off part way up, so the crown is not all forks at the ends.
                    float along = children == 3 && c == 0 && parent.Depth > 0 ? 0.65f : 1f;
                    var start = parent.Start + parent.Direction * (parent.Length * along);
                    Add(i, start, direction, parent.Length * Random(0.62f, 0.8f), parent.Radius * 0.62f, parent.Depth + 1);
                }

                parent.Twig = false;
                m_Branches[i] = parent;
            }

            Fit();
            Bark();
            Sprout();

            m_NowStart = new Vector3[m_Branches.Count];
            m_NowTurn = new Quaternion[m_Branches.Count];
            m_Particles = new ParticleSystem.Particle[m_Bark.Count + m_Leaves.Length];
            for (int i = 0; i < m_Particles.Length; i++)
                m_Particles[i].startLifetime = m_Particles[i].remainingLifetime = ManualParticles.Lifetime;
            ManualParticles.Reserve(m_System, m_Particles.Length);
        }

        void Add(int parent, Vector3 start, Vector3 direction, float length, float radius, int depth)
        {
            m_Branches.Add(new Branch
            {
                Parent = parent, Start = start, Direction = direction, Length = length, Radius = radius, Depth = depth, Twig = true,
            });
        }

        /// <summary>Scales the tree so its top is at <see cref="m_Height"/> and its crown reaches no further than <see cref="m_Spread"/>.</summary>
        void Fit()
        {
            float top = 0.01f, reach = 0.01f;
            foreach (var branch in m_Branches)
            {
                var end = branch.Start + branch.Direction * branch.Length;
                top = Mathf.Max(top, end.y + m_LeafCluster);
                reach = Mathf.Max(reach, new Vector2(end.x, end.z).magnitude + m_LeafCluster);
            }

            float scale = Mathf.Min(m_Height / top, m_Spread / reach);
            for (int i = 0; i < m_Branches.Count; i++)
            {
                var branch = m_Branches[i];
                branch.Start *= scale;
                branch.Length *= scale;
                branch.Radius *= scale;
                m_Branches[i] = branch;
            }
        }

        /// <summary>
        /// The points of the trunk and branches: rings round the thick ones, so they read as a shell
        /// rather than a line, and single points along the thin ones, spaced so the whole is about
        /// <see cref="m_BranchPoints"/>.
        /// </summary>
        void Bark()
        {
            m_Bark.Clear();
            float surface = 0f;
            foreach (var branch in m_Branches)
                surface += branch.Length * Mathf.Max(1f, 2f * Mathf.PI * branch.Radius / 0.02f);

            float spacing = Mathf.Max(0.006f, surface / Mathf.Max(100, m_BranchPoints));
            for (int b = 0; b < m_Branches.Count; b++)
            {
                var branch = m_Branches[b];
                var side = Vector3.Cross(branch.Direction, Mathf.Abs(branch.Direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var other = Vector3.Cross(branch.Direction, side);
                int around = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * branch.Radius / 0.02f));
                int steps = Mathf.Max(2, Mathf.CeilToInt(branch.Length / spacing));

                for (int s = 0; s < steps; s++)
                {
                    float along = (s + Random01()) / steps;
                    float taper = Mathf.Lerp(1f, 0.7f, along);
                    for (int a = 0; a < around && m_Bark.Count < m_BranchPoints; a++)
                    {
                        float angle = (a + Random01() * 0.5f) * Mathf.PI * 2f / around;
                        var ring = around > 1 ? (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * (branch.Radius * taper) : Vector3.zero;
                        m_Bark.Add((b, branch.Start + branch.Direction * (branch.Length * along) + ring));
                    }
                }
            }
        }

        /// <summary>Leaves round the outer part of every twig, each lit by a band: bass low in the crown, treble at the top.</summary>
        void Sprout()
        {
            var leaves = new List<Leaf>();
            for (int b = 0; b < m_Branches.Count; b++)
            {
                var branch = m_Branches[b];
                if (!branch.Twig)
                    continue;

                for (int i = 0; i < m_LeavesPerTwig; i++)
                {
                    float along = Random(0.35f, 1.05f);
                    var scatter = new Vector3(Random(-1f, 1f), Random(-1f, 1f), Random(-1f, 1f)) * m_LeafCluster;
                    leaves.Add(new Leaf
                    {
                        Branch = b,
                        Rest = branch.Start + branch.Direction * (branch.Length * along) + scatter,
                        Seed = Random01() * 100f,
                    });
                }
            }

            float low = float.MaxValue, high = float.MinValue;
            foreach (var leaf in leaves)
            {
                low = Mathf.Min(low, leaf.Rest.y);
                high = Mathf.Max(high, leaf.Rest.y);
            }

            m_Leaves = leaves.ToArray();
            for (int i = 0; i < m_Leaves.Length; i++)
                m_Leaves[i].Band = Mathf.Clamp01(Mathf.InverseLerp(low, high, m_Leaves[i].Rest.y) + Random(-0.08f, 0.08f));
            m_OnTree = m_Leaves.Length;
        }

        float Random01() => (float)m_Random.NextDouble();
        float Random(float from, float to) => Mathf.Lerp(from, to, Random01());

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (m_Analyser == null || m_Particles.Length == 0)
                return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float time = Time.time;
            float idle = m_Analyser.Idle;
            float playing = 1f - idle;

            Wind(dt, playing);
            Bend(time);
            ShakeLoose(playing);

            int n = 0;
            float scan = time * 0.35f;
            var hologram = m_Hologram;

            // The trunk and branches: a dim shell, scanlines rising up it.
            foreach (var (branch, rest) in m_Bark)
            {
                var at = Now(branch, rest);
                float line = 0.65f + 0.35f * Mathf.Sin((rest.y * 9f - scan * 6f) * Mathf.PI);
                float level = 0.3f + 0.15f * line;
                Set(n++, at, hologram * level, m_BranchSize);
            }

            Leaves(ref n, dt, time, playing);

            m_System.SetParticles(m_Particles, n);

            float beat = m_Analyser.BeatPulse;
            float gain = m_Brightness * Intensity * Opacity * (1f - 0.5f * idle) * (1f + m_BeatBrightness * beat);
            m_Block.SetColor(k_Color, new Color(gain, gain, gain, 1f));
            m_Renderer.SetPropertyBlock(m_Block);
        }

        /// <summary>
        /// How hard the wind blows and where from: a calm sway, more with the bass, and a gust on each
        /// beat that dies away over a second or so; its direction comes slowly round, and the tree turns.
        /// </summary>
        void Wind(float dt, float playing)
        {
            float target = m_CalmSway + m_BassSway * m_Analyser.Bass * playing;
            m_Sway = Mathf.Lerp(m_Sway, target, 1f - Mathf.Exp(-dt / (target > m_Sway ? 0.3f : 1.2f)));

            if (m_Analyser.Beat && playing > 0.5f)
                m_GustLevel = Mathf.Max(m_GustLevel, m_Gust * (0.5f + m_Analyser.BeatStrength));
            m_GustLevel *= Mathf.Exp(-dt / 0.9f);

            m_SwayPhase += dt * 2f * Mathf.PI * m_SwayHz * (0.8f + 0.5f * m_Analyser.Loudness * playing);
            m_WindAngle += dt * 0.05f;
            m_TurnAngle += dt * 360f * m_Turn;
        }

        /// <summary>Where every branch is now: each bends on its parent toward the wind, the thinner ones further and a little later.</summary>
        void Bend(float time)
        {
            var wind = new Vector3(Mathf.Cos(m_WindAngle), 0f, Mathf.Sin(m_WindAngle));
            var turn = Quaternion.AngleAxis(m_TurnAngle, Vector3.up);
            float strength = m_Sway + m_GustLevel;

            for (int b = 0; b < m_Branches.Count; b++)
            {
                var branch = m_Branches[b];
                Quaternion parentTurn;
                Vector3 start;
                if (branch.Parent < 0)
                {
                    parentTurn = turn;
                    start = m_Base;
                }
                else
                {
                    var parent = m_Branches[branch.Parent];
                    parentTurn = m_NowTurn[branch.Parent];
                    start = m_NowStart[branch.Parent] + parentTurn * (branch.Start - parent.Start);
                }

                var heading = parentTurn * branch.Direction;
                var axis = Vector3.Cross(heading, wind);
                float flex = m_Flex != null && m_Flex.Length > 0 ? m_Flex[Mathf.Min(branch.Depth, m_Flex.Length - 1)] : 0.05f;
                float swing = Mathf.Sin(m_SwayPhase - branch.Depth * 0.35f) + 0.25f * Mathf.Sin(m_SwayPhase * 2.3f + b);
                float angle = strength * flex * swing * Mathf.Rad2Deg;

                m_NowTurn[b] = axis.sqrMagnitude > 1e-6f ? Quaternion.AngleAxis(angle, axis.normalized) * parentTurn : parentTurn;
                m_NowStart[b] = start;
            }
        }

        /// <summary>A point of branch <paramref name="branch"/>, given at rest, where it is now, in the room's frame.</summary>
        Vector3 Now(int branch, Vector3 rest) => m_NowStart[branch] + m_NowTurn[branch] * (rest - m_Branches[branch].Start);

        /// <summary>A beat shakes leaves loose, more on a big one, while enough are still on the tree.</summary>
        void ShakeLoose(float playing)
        {
            if (!m_Analyser.Beat || playing < 0.5f || m_Leaves.Length == 0)
                return;

            int keep = Mathf.CeilToInt(m_Leaves.Length * m_KeepLeaves);
            int drop = Mathf.RoundToInt(m_LeavesPerBeat * (0.4f + 1.2f * m_Analyser.BeatStrength));
            for (int tries = 0; drop > 0 && m_OnTree > keep && tries < drop * 6; tries++)
            {
                int i = m_Random.Next(m_Leaves.Length);
                if (m_Leaves[i].State != LeafState.On)
                    continue;

                m_Leaves[i].State = LeafState.Falling;
                m_Leaves[i].Timer = 0f;
                m_Leaves[i].Position = Now(m_Leaves[i].Branch, m_Leaves[i].Rest);
                m_Leaves[i].Fall = Random(m_FallSpeed.x, m_FallSpeed.y);
                m_OnTree--;
                drop--;
            }
        }

        void Leaves(ref int n, float dt, float time, float playing)
        {
            var wind = new Vector3(Mathf.Cos(m_WindAngle), 0f, Mathf.Sin(m_WindAngle));
            float flutter = m_Flutter * (0.3f + m_Analyser.Mid * playing);
            int bands = Mathf.Max(1, m_Analyser.BandCount);

            for (int i = 0; i < m_Leaves.Length; i++)
            {
                ref var leaf = ref m_Leaves[i];
                float level = m_Analyser.BandAt(leaf.Band * (bands - 1)) * playing;
                var colour = m_LeafPalette.Evaluate(leaf.Band);

                switch (leaf.State)
                {
                    case LeafState.On:
                    {
                        var at = Now(leaf.Branch, leaf.Rest) +
                                 new Vector3(Mathf.Sin(time * 7f + leaf.Seed), Mathf.Sin(time * 5.3f + leaf.Seed * 1.7f),
                                     Mathf.Cos(time * 6.1f + leaf.Seed)) * flutter;
                        Set(n++, at, colour * (0.35f + 0.65f * level), m_LeafSize * (0.8f + 0.6f * level));
                        break;
                    }

                    case LeafState.Falling:
                    {
                        // Down, rocking side to side, carried a little by the wind, turning to autumn as it goes.
                        leaf.Timer += dt;
                        float rock = Mathf.Sin(leaf.Timer * 2.6f + leaf.Seed);
                        var drift = wind * (0.08f + 0.25f * m_Sway) + new Vector3(Mathf.Cos(leaf.Seed), 0f, Mathf.Sin(leaf.Seed)) * (0.22f * rock);
                        leaf.Position += (drift + Vector3.down * leaf.Fall * (0.7f + 0.3f * Mathf.Abs(rock))) * dt;
                        leaf.Position = Inside(leaf.Position);
                        if (leaf.Position.y <= 0.012f)
                        {
                            leaf.Position.y = 0.012f;
                            leaf.State = LeafState.Lying;
                            leaf.Timer = 0f;
                        }

                        float autumn = Mathf.Clamp01(leaf.Timer / 1.5f);
                        Set(n++, leaf.Position, Color.Lerp(colour * 0.8f, m_Autumn, autumn) * 0.9f, m_LeafSize * 1.1f);
                        break;
                    }

                    case LeafState.Lying:
                    {
                        leaf.Timer += dt;
                        float fade = 1f - Mathf.Clamp01((leaf.Timer - m_LieSeconds) / 1.5f);
                        if (fade <= 0f)
                        {
                            leaf.State = LeafState.Growing;
                            leaf.Timer = 0f;
                            break;
                        }

                        Set(n++, leaf.Position, m_Autumn * (0.6f * fade), m_LeafSize);
                        break;
                    }

                    default:
                    {
                        // Back on its twig, from nothing.
                        leaf.Timer += dt;
                        float grown = Mathf.Clamp01(leaf.Timer / Mathf.Max(0.1f, m_RegrowSeconds));
                        if (grown >= 1f)
                        {
                            leaf.State = LeafState.On;
                            m_OnTree++;
                        }

                        Set(n++, Now(leaf.Branch, leaf.Rest), colour * (0.35f * grown), m_LeafSize * grown);
                        break;
                    }
                }
            }
        }

        /// <summary>Holds a falling leaf inside the room: clear of the three walls, and no further back than the open back.</summary>
        static Vector3 Inside(Vector3 position)
        {
            const float margin = 0.05f;
            position.x = Mathf.Clamp(position.x, -CaveScreens.HalfWidth + margin, CaveScreens.HalfWidth - margin);
            position.z = Mathf.Clamp(position.z, -CaveScreens.FrontZ + margin, CaveScreens.FrontZ - margin);
            return position;
        }

        void Set(int index, Vector3 position, Color colour, float size)
        {
            ref var particle = ref m_Particles[index];
            particle.position = position;
            particle.startSize = size;
            colour.a = 1f;
            particle.startColor = colour;
            particle.remainingLifetime = ManualParticles.Lifetime;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The pads: three jellyfish drifting in the room — one high by the left wall, one lower by the right,
    /// one at chest height in front of the viewer, where Kevin placed them. Each chord change
    /// makes every bell contract — a quick squeeze, a slow release — and the squeeze lifts the jelly a
    /// few centimetres before it sinks back; between chords the bells throb with the pad's envelope. Their
    /// colour is the chord's: the root's pitch class picks the hue, so the harmony is visible as the
    /// colour of the jellies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why never straight overhead.</b> The plan hangs the pads overhead, but the CAVE has no ceiling
    /// screen: a jelly straight above the viewer would be drawn on no screen at all. A jelly up high has
    /// to stay low enough that the line from the viewer's eye through its crown still lands on a wall — the
    /// position is the bell's rim at the top of its lift, and nothing rises above it. The ones placed now
    /// keep their crowns under about 1.9 m.
    /// </para>
    /// <para>
    /// <b>Why beads for tentacles.</b> A chain of small glowing spheres sways as a chain of joints and
    /// needs no mesh rebuilt per frame; at the size of a jellyfish across a CAVE it reads as a tentacle,
    /// and it suits the stylised look of the rest of the cast.
    /// </para>
    /// <para>
    /// The root sits at the room's origin with no rotation, so the positions here are room coordinates.
    /// </para>
    /// </remarks>
    public sealed class JellyfishPerformer : Performer
    {
        const int k_Tentacles = 8;
        const int k_TentacleBeads = 7;
        const float k_TentacleSegment = 0.055f;
        const int k_OralArms = 4;
        const int k_OralBeads = 5;
        const float k_OralSegment = 0.05f;

        // Joint kinds in the flattened joint arrays.
        const int k_Tentacle = 0;
        const int k_Oral = 1;

        // Each jelly answers a chord a little after the one before it, and leans its hue a little apart,
        // so three bells read as a group rather than a copy.
        static readonly float[] k_Delay = { 0f, 0.035f, 0.07f };
        static readonly float[] k_HueShift = { 0f, 0.045f, -0.045f };

        [Header("Placement")]
        // "Overhead" in a CAVE has a ceiling: there is no screen above, so a crown is seen only while the
        // line from the eye through it still lands on the front wall. The nearer a jelly is to the viewer,
        // the lower it has to hang; these clear the wall's top edge for eyes from 1.5 to 1.75 m.
        // These are where Kevin placed the jellies by hand in the scene (2026-11-02): one high by the left
        // wall, one lower by the right, and one at chest height just in front of the viewer's spot. Each
        // jelly can still be dragged in the scene afterwards: at runtime it moves about wherever it was left,
        // not about these.
        [SerializeField, Tooltip("Rim centres at the top of each jelly's lift, in this object's frame. Used by Build; " +
            "at runtime each jelly moves about where it stands in the scene. Keep the nearer ones lower: higher, and " +
            "their crowns pass over the top of the front wall and vanish.")]
        Vector3[] m_Positions =
        {
            new(-1.462f, 1.74f, 0.595f),
            new(1.231f, 1.323f, 0.598f),
            new(0f, 1.141f, -0.075f),
        };

        [SerializeField, Range(0.1f, 0.5f), Tooltip("Bell diameter, metres. Used by Build.")]
        float m_BellSize = 0.28f;

        [Header("Pulse")]
        [SerializeField, Range(0.02f, 0.5f), Tooltip("Seconds from a chord to a fully contracted bell.")]
        float m_Squeeze = 0.12f;

        [SerializeField, Range(0.1f, 3f), Tooltip("Seconds for the bell to relax again.")]
        float m_Release = 0.8f;

        [SerializeField, Range(0f, 0.5f), Tooltip("How much a full contraction narrows the bell, as a fraction of its width.")]
        float m_Contraction = 0.22f;

        [SerializeField, Range(0f, 0.1f), Tooltip("How far a full contraction lifts the jelly, metres. It rests this far " +
            "below its position and rises to it.")]
        float m_Rise = 0.025f;

        [Header("Colour")]
        [SerializeField, Range(0f, 1f)] float m_Saturation = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("Body opacity of the outer bell; the rest is rim and glow.")]
        float m_BellOpacity = 0.22f;

        [SerializeField, Range(0.05f, 2f), Tooltip("Seconds for the colour to go halfway to a new chord's.")]
        float m_ColourHalfLife = 0.35f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform[] m_Jellies;
        [SerializeField] Transform[] m_Bells;
        [SerializeField] Transform[] m_Joints;
        [SerializeField] Vector3[] m_JointRest;
        [SerializeField] int[] m_JointJelly;
        [SerializeField] int[] m_JointDepth;
        [SerializeField] int[] m_JointKind;
        [SerializeField] float[] m_JointPhase;
        [SerializeField] float[] m_JointYaw;
        [SerializeField] CreatureTint[] m_Tints;
        [SerializeField] int[] m_TintJelly;
        [SerializeField] float[] m_TintAlpha;
        [SerializeField] float[] m_TintBrightness;

        readonly List<Note> m_Active = new();
        readonly float[] m_Pulse = new float[3];
        readonly Color[] m_Colours = { new(0.45f, 0.8f, 1f), new(0.45f, 0.8f, 1f), new(0.45f, 0.8f, 1f) };

        // Where each jelly's lift tops out, taken from where it stands when the scene starts, so a jelly
        // moved by hand in the scene stays where it was put rather than snapping back to m_Positions.
        Vector3[] m_Base;

        float m_Live;

        void Awake()
        {
            if (m_Jellies == null)
                return;

            m_Base = new Vector3[m_Jellies.Length];
            for (int j = 0; j < m_Jellies.Length; j++)
                m_Base[j] = m_Jellies[j] != null
                    ? m_Jellies[j].localPosition + Vector3.up * m_Rise
                    : m_Positions[Mathf.Min(j, m_Positions.Length - 1)];
        }

        // ================================================================== body

        public override void Build(CreatureKit kit)
        {
            int jellies = m_Positions.Length;
            m_Jellies = new Transform[jellies];
            m_Bells = new Transform[jellies];

            var joints = new List<Transform>();
            var rest = new List<Vector3>();
            var jointJelly = new List<int>();
            var depth = new List<int>();
            var kind = new List<int>();
            var phase = new List<float>();
            var jointYaw = new List<float>();
            var tints = new List<CreatureTint>();
            var tintJelly = new List<int>();
            var alpha = new List<float>();
            var brightness = new List<float>();

            var colour = new Color(0.45f, 0.8f, 1f);
            float rim = m_BellSize * 0.5f;

            void AddTint(CreatureTint tint, int jelly, float a, float b)
            {
                tints.Add(tint);
                tintJelly.Add(jelly);
                alpha.Add(a);
                brightness.Add(b);
            }

            for (int j = 0; j < jellies; j++)
            {
                var jelly = kit.Node(transform, $"Jellyfish_{j}", m_Positions[j] - Vector3.up * m_Rise);
                m_Jellies[j] = jelly;

                // The bell and the brighter organs inside it scale together on a pivot at the rim.
                var bell = kit.Node(jelly, "Bell", Vector3.zero);
                m_Bells[j] = bell;
                AddTint(kit.Glowing(bell, "Bell_Outer", kit.Bell, Vector3.zero, Vector3.one * m_BellSize,
                    new Color(colour.r, colour.g, colour.b, m_BellOpacity), colour * 0.5f), j, m_BellOpacity, 0.5f);
                AddTint(kit.Glowing(bell, "Bell_Inner", kit.Bell, new Vector3(0f, 0.01f, 0f),
                    new Vector3(m_BellSize * 0.57f, m_BellSize * 0.64f, m_BellSize * 0.57f),
                    new Color(colour.r, colour.g, colour.b, 0.35f), colour * 1.1f), j, 0.35f, 1.1f);

                // Tentacles round the rim. Each root is turned to face out, so a turn about its X splays it.
                for (int t = 0; t < k_Tentacles; t++)
                {
                    float yaw = t * 360f / k_Tentacles + j * 13f;
                    float a = yaw * Mathf.Deg2Rad;
                    var at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (rim * 0.93f);
                    float tentaclePhase = Motion.Hash01(j * 31 + t) * 6.283f;

                    var parent = jelly;
                    for (int k = 0; k < k_TentacleBeads; k++)
                    {
                        var position = k == 0 ? at : new Vector3(0f, -k_TentacleSegment, 0f);
                        var joint = kit.Node(parent, $"Tentacle_{t}_{k}", position, k == 0 ? new Vector3(0f, yaw, 0f) : Vector3.zero);
                        float size = Mathf.Lerp(0.024f, 0.009f, k / (float)(k_TentacleBeads - 1));
                        // Stretched along the tentacle to a little more than the spacing, so neighbours overlap
                        // into one strand.
                        AddTint(kit.Glowing(joint, "Bead", kit.Sphere, Vector3.zero, new Vector3(size, k_TentacleSegment * 1.2f, size),
                            new Color(colour.r, colour.g, colour.b, 0.5f), colour), j, 0.5f, 1.2f);

                        joints.Add(joint);
                        rest.Add(position);
                        jointJelly.Add(j);
                        depth.Add(k);
                        kind.Add(k_Tentacle);
                        phase.Add(tentaclePhase);
                        jointYaw.Add(k == 0 ? yaw : 0f);
                        parent = joint;
                    }
                }

                // Oral arms: four thicker, frillier chains in the middle.
                for (int o = 0; o < k_OralArms; o++)
                {
                    float yaw = o * 90f + 45f;
                    float a = yaw * Mathf.Deg2Rad;
                    var at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.03f;
                    float armPhase = Motion.Hash01(j * 17 + o + 100) * 6.283f;

                    var parent = jelly;
                    for (int k = 0; k < k_OralBeads; k++)
                    {
                        var position = k == 0 ? at : new Vector3(0f, -k_OralSegment, 0f);
                        var joint = kit.Node(parent, $"Oral_{o}_{k}", position, k == 0 ? new Vector3(0f, yaw, 0f) : Vector3.zero);
                        float size = Mathf.Lerp(0.04f, 0.024f, k / (float)(k_OralBeads - 1));
                        AddTint(kit.Glowing(joint, "Frill", kit.Sphere, Vector3.zero, new Vector3(size, size * 1.3f, size * 0.6f),
                            new Color(colour.r, colour.g, colour.b, 0.3f), colour * 0.8f, new Vector3(0f, 0f, 30f * k)),
                            j, 0.3f, 0.8f);

                        joints.Add(joint);
                        rest.Add(position);
                        jointJelly.Add(j);
                        depth.Add(k);
                        kind.Add(k_Oral);
                        phase.Add(armPhase);
                        jointYaw.Add(k == 0 ? yaw : 0f);
                        parent = joint;
                    }
                }
            }

            m_Joints = joints.ToArray();
            m_JointRest = rest.ToArray();
            m_JointJelly = jointJelly.ToArray();
            m_JointDepth = depth.ToArray();
            m_JointKind = kind.ToArray();
            m_JointPhase = phase.ToArray();
            m_JointYaw = jointYaw.ToArray();
            m_Tints = tints.ToArray();
            m_TintJelly = tintJelly.ToArray();
            m_TintAlpha = alpha.ToArray();
            m_TintBrightness = brightness.ToArray();
        }

        // ================================================================== performance

        protected override void Perform(float deltaTime)
        {
            if (m_Jellies == null || m_Jellies.Length == 0)
                return;

            m_Live = Motion.Damp(m_Live, Live ? 1f : 0f, 0.4f, deltaTime);
            float idle = 1f - m_Live;
            float throb = Mathf.Clamp01(Level()) * 0.35f;

            bool chord = TryLast(out var note, out _);
            int count = Mathf.Min(m_Jellies.Length, m_Pulse.Length);

            // Bells: a squeeze on each chord, a throb from the pad's level, a slow pulse when idle.
            for (int j = 0; j < count; j++)
            {
                float squeeze = chord ? Contraction(SongTime - note.Time - k_Delay[j % k_Delay.Length]) * Gain : 0f;
                float idlePulse = 0.25f * (0.5f + 0.5f * Motion.Wave(Clock, 0.22f, j * 0.31f)) * idle;
                float pulse = Mathf.Clamp01(Mathf.Max(Mathf.Max(squeeze, throb), idlePulse));
                m_Pulse[j] = pulse;

                float narrow = 1f - m_Contraction * pulse;
                float lengthen = 1f + m_Contraction * 0.55f * pulse;
                m_Bells[j].localScale = new Vector3(narrow, lengthen, narrow);

                // The squeeze lifts it to its position; it sinks back as the bell relaxes, and bobs a
                // little — only ever downward, so the crown never rises past where Build put it.
                float bob = 0.01f * (0.5f + 0.5f * Motion.Wave(Clock, 0.13f, j * 0.4f));
                var position = m_Base != null && j < m_Base.Length ? m_Base[j] : m_Positions[Mathf.Min(j, m_Positions.Length - 1)];
                m_Jellies[j].localPosition = position + Vector3.up * (m_Rise * (pulse - 1f) - bob);
            }

            // Colour: the chord's root (the lowest pad note sounding), each jelly leaning its hue apart.
            Color target;
            if (TryLowestActive(out int root))
            {
                target = Motion.PitchColor(root, m_Saturation, 1f);
            }
            else
            {
                // Between chords and between songs: a slow drift between cyan and violet.
                float drift = 0.5f + 0.5f * Mathf.Sin(Clock * 0.05f);
                target = Color.HSVToRGB(Mathf.Lerp(0.5f, 0.76f, drift), m_Saturation * 0.8f, 1f);
            }

            Color.RGBToHSV(target, out float hue, out float saturation, out float value);
            for (int j = 0; j < count; j++)
            {
                var shifted = Color.HSVToRGB(Mathf.Repeat(hue + k_HueShift[j % k_HueShift.Length], 1f), saturation, value);
                m_Colours[j] = Motion.Damp(m_Colours[j], shifted, m_ColourHalfLife, deltaTime);
            }

            float intensity = Intensity;
            for (int i = 0; i < m_Tints.Length; i++)
            {
                int j = Mathf.Min(m_TintJelly[i], count - 1);
                var c = m_Colours[j];
                float glow = m_TintBrightness[i] * (0.35f + 0.9f * m_Pulse[j]) * intensity;
                m_Tints[i].Set(new Color(c.r, c.g, c.b, m_TintAlpha[i]), new Color(c.r * glow, c.g * glow, c.b * glow, 1f));
            }

            PoseTentacles();
        }

        /// <summary>
        /// The bell's contraction <paramref name="since"/> seconds after a chord: a quick squeeze, then a
        /// slow relax — the jet a real jellyfish swims by.
        /// </summary>
        float Contraction(double since)
        {
            if (since < 0.0)
                return 0f;

            if (since < m_Squeeze)
                return Motion.Smooth01((float)(since / m_Squeeze));

            return 1f - Motion.Smooth01((float)(since - m_Squeeze) / m_Release);
        }

        bool TryLowestActive(out int pitch)
        {
            ActiveNotes(m_Active);
            pitch = int.MaxValue;
            foreach (var note in m_Active)
                pitch = Mathf.Min(pitch, note.Pitch);

            return m_Active.Count > 0;
        }

        /// <summary>
        /// Every joint sways on a wave that runs down its chain, so each tentacle trails its own root. On
        /// a squeeze the roots ride in with the narrowing rim and splay out, and the rest of each chain
        /// lags back in — the shape of a bell pushing water.
        /// </summary>
        void PoseTentacles()
        {
            float time = Clock;
            for (int i = 0; i < m_Joints.Length; i++)
            {
                var joint = m_Joints[i];
                int j = Mathf.Min(m_JointJelly[i], m_Pulse.Length - 1);
                int k = m_JointDepth[i];
                float pulse = m_Pulse[j];
                float phase = m_JointPhase[i];
                bool oral = m_JointKind[i] == k_Oral;

                float amplitude = oral ? 10f : 7f;
                float x = amplitude * Mathf.Sin(time * 1.9f + phase - k * 0.55f);
                float z = amplitude * 0.7f * Mathf.Cos(time * 1.45f + phase * 1.3f - k * 0.5f);

                if (k == 0)
                {
                    // The root rides the rim in as the bell narrows.
                    float narrow = 1f - m_Contraction * pulse;
                    var restPosition = m_JointRest[i];
                    joint.localPosition = new Vector3(restPosition.x * narrow, restPosition.y, restPosition.z * narrow);
                    x -= pulse * (oral ? 10f : 24f);
                }
                else
                {
                    x += pulse * (oral ? 3f : 7f);
                }

                // Yaw outermost: the splay about X happens in the plane facing out from the bell.
                joint.localRotation = Quaternion.Euler(x, m_JointYaw[i], z);
            }
        }
    }
}

using UnityEngine;

namespace VRShootingGallery.Pipes
{
    /// <summary>What sort of thing a ball strikes, which decides how it moves when struck.</summary>
    public enum RigKind
    {
        Bar,
        String,
        Drum,
        Cymbal,
    }

    /// <summary>The four instrument groups round the room, low on the left to high on the right.</summary>
    public enum RigGroup
    {
        Bass,
        Mallets,
        Strings,
        Drums,
    }

    public enum RigDrum
    {
        None,
        Kick,
        Tom,
        Snare,
        HiHat,
        Crash,
    }

    /// <summary>
    /// One thing on the machine that can be struck: a mallet bar, a string, a drum or a cymbal. It
    /// knows where a ball must be to strike it, which pipes may throw at it, where the ball goes
    /// after, and the box a pointer has to hit to play it by hand; and it moves, rings and glows
    /// when it is struck, whichever did the striking.
    /// </summary>
    /// <remarks>
    /// Keep the object itself unscaled — its scale is the hit test's — and put the visible pieces
    /// under it. The hop and the roll home are authored in the rig's space, since they cross from
    /// the instrument to the floor and back to the pipe bank.
    /// </remarks>
    [DisallowMultipleComponent]
    public class RigTarget : MonoBehaviour
    {
        [SerializeField] RigKind m_Kind;
        [SerializeField] RigGroup m_Group;

        [SerializeField, Tooltip("MIDI note for the tuned ones; 60 is middle C.")]
        int m_Midi = 60;

        [SerializeField] RigDrum m_Drum;

        [SerializeField, Range(0f, 2f), Tooltip("Its loudness against the others.")]
        float m_Gain = 1f;

        [Header("Machine")]
        [SerializeField, Tooltip("Where a ball's centre is at the moment it strikes, in this object's space.")]
        Vector3 m_HitPoint;

        [SerializeField, Tooltip("The pipes that may throw at it, as indices into the rig's pipes, the one to use first first.")]
        int[] m_Pipes = new int[0];

        [SerializeField, Tooltip("The highest a ball thrown at it may go, metres above the floor. The room's cap still applies.")]
        float m_Apex = 1.85f;

        [SerializeField, Tooltip("How far the ball hops up off it after the strike, metres. Zero and it drops straight on, as off a string.")]
        float m_Hop = 0.12f;

        [SerializeField, Tooltip("Where the ball comes down after the strike, in the rig's space.")]
        Vector3 m_Landing;

        [SerializeField, Tooltip("The way it rolls home from there, in the rig's space, ending in the pipe bank.")]
        Vector3[] m_Roll = new Vector3[0];

        [Header("Pointing")]
        [SerializeField, Tooltip("The box a pointer has to hit to play it, in this object's space. More generous than what is drawn.")]
        Vector3 m_BoxCentre;

        [SerializeField] Vector3 m_BoxSize = new(0.1f, 0.1f, 0.1f);

        [Header("Look")]
        [SerializeField, Tooltip("What moves when it is struck: the bar dips, the drum head pumps, the cymbal rocks.")]
        Transform m_Body;

        [SerializeField, Tooltip("Lit surfaces whose emission brightens with a strike.")]
        Renderer[] m_Glow = new Renderer[0];

        [SerializeField, ColorUsage(false, true)]
        Color m_GlowColour = Color.white;

        [SerializeField, Range(0f, 1f), Tooltip("Glow at rest, as a share of a full strike's.")]
        float m_RestGlow = 0.08f;

        [SerializeField, Tooltip("Seconds for the strike's glow to fall to a third.")]
        float m_GlowDecay = 0.35f;

        [SerializeField, Tooltip("A string is drawn as a line the strike sets swinging.")]
        LineRenderer m_String;

        [SerializeField] Vector3 m_StringFrom;
        [SerializeField] Vector3 m_StringTo;

        [SerializeField, Tooltip("The way the string swings, in this object's space, metres at full strength.")]
        Vector3 m_StringSwing = new(0.02f, 0f, 0f);

        [SerializeField, Tooltip("Seconds for the swing to fall to a third.")]
        float m_StringDecay = 0.8f;

        [SerializeField, Tooltip("The glow on the wall or floor behind it.")]
        RigFlash m_Flash;

        const int k_StringPoints = 16;
        static readonly int k_Emission = Shader.PropertyToID("_EmissionColor");
        static readonly string[] k_NoteNames = { "C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B" };

        MaterialPropertyBlock m_Block;
        Vector3 m_BodyPosition;
        Vector3 m_BodyScale = Vector3.one;
        Quaternion m_BodyRotation = Quaternion.identity;
        Vector3[] m_Points = new Vector3[0];
        float m_StruckAt = -100f;
        float m_Strength;
        float m_Phase;
        bool m_Hover;
        float m_HoverLevel;
        bool m_Settled;

        public RigKind Kind => m_Kind;
        public RigGroup Group => m_Group;
        public int Midi => m_Midi;
        public RigDrum Drum => m_Drum;
        public float Gain => m_Gain;
        public int[] Pipes => m_Pipes;
        public float Apex => m_Apex;
        public float Hop => m_Hop;
        public Vector3 Landing => m_Landing;
        public Vector3[] Roll => m_Roll;
        public RigFlash Flash => m_Flash;

        /// <summary>The sound it makes, rendered by the director at load.</summary>
        public AudioClip Voice { get; set; }

        /// <summary>Its note as people write it, or the drum's name: "C3", "Snare".</summary>
        public string Title => m_Group == RigGroup.Drums ? m_Drum.ToString() : NoteName(m_Midi);

        public static string NoteName(int midi) => k_NoteNames[((midi % 12) + 12) % 12] + (midi / 12 - 1);

        public static float Frequency(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>Whether the pointer is on it; it brightens, so it is plain what the trigger will play.</summary>
        public bool Hover { get => m_Hover; set => m_Hover = value; }

        /// <summary>Where a ball's centre is as it strikes, in <paramref name="space"/>'s coordinates.</summary>
        public Vector3 HitPointIn(Transform space)
        {
            var world = transform.TransformPoint(m_HitPoint);
            return space != null ? space.InverseTransformPoint(world) : world;
        }

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            if (m_Body != null)
            {
                m_BodyPosition = m_Body.localPosition;
                m_BodyScale = m_Body.localScale;
                m_BodyRotation = m_Body.localRotation;
            }

            m_Points = new Vector3[k_StringPoints];
            m_Phase = (m_Midi * 0.37f) % 1f;
            if (m_String != null)
            {
                m_String.useWorldSpace = false;
                m_String.positionCount = k_StringPoints;
            }

            Animate(true);
        }

        /// <summary>Struck by a ball or by hand, <paramref name="strength"/> 0–1.</summary>
        public void Strike(float strength)
        {
            m_StruckAt = Time.time;
            m_Strength = Mathf.Clamp(strength, 0.15f, 1f);
            m_Settled = false;
            if (m_Flash != null)
                m_Flash.Trigger(m_Strength);
        }

        /// <summary>The distance along <paramref name="ray"/> to its pointing box, if the ray hits it.</summary>
        public bool Raycast(Ray ray, out float distance)
        {
            distance = 0f;
            var origin = transform.InverseTransformPoint(ray.origin) - m_BoxCentre;
            var direction = transform.InverseTransformDirection(ray.direction);
            var half = m_BoxSize * 0.5f;

            float near = float.NegativeInfinity;
            float far = float.PositiveInfinity;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = origin[axis];
                float d = direction[axis];
                float h = half[axis];
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (o < -h || o > h)
                        return false;
                    continue;
                }

                float a = (-h - o) / d;
                float b = (h - o) / d;
                if (a > b)
                    (a, b) = (b, a);
                near = Mathf.Max(near, a);
                far = Mathf.Min(far, b);
                if (near > far)
                    return false;
            }

            if (far < 0f)
                return false;

            distance = Mathf.Max(0f, near);
            return true;
        }

        void Update() => Animate(false);

        void Animate(bool force)
        {
            float since = Time.time - m_StruckAt;
            m_HoverLevel = Mathf.MoveTowards(m_HoverLevel, m_Hover ? 1f : 0f, Time.deltaTime / 0.12f);

            float glow = m_Strength * Mathf.Exp(-since / Mathf.Max(0.02f, m_GlowDecay));
            float moving = m_Strength * Mathf.Exp(-since / 0.07f);
            bool active = glow > 0.002f || m_HoverLevel > 0f || (m_Kind == RigKind.Cymbal && since < 3f) ||
                          (m_Kind == RigKind.String && since < m_StringDecay * 5f);
            if (!active && m_Settled && !force)
                return;

            m_Settled = !active;
            float level = m_RestGlow + glow + m_HoverLevel * 0.3f;

            if (m_Body != null)
            {
                switch (m_Kind)
                {
                    case RigKind.Bar:
                        m_Body.localPosition = m_BodyPosition + Vector3.down * (0.012f * moving);
                        break;
                    case RigKind.Drum:
                        m_Body.localScale = new Vector3(m_BodyScale.x * (1f + 0.05f * moving), m_BodyScale.y * (1f - 0.3f * moving),
                            m_BodyScale.z * (1f + 0.05f * moving));
                        break;
                    case RigKind.Cymbal:
                        float rock = 9f * m_Strength * Mathf.Exp(-since / 0.7f);
                        float swing = 2f * Mathf.PI * 2.3f * since;
                        m_Body.localRotation = m_BodyRotation * Quaternion.Euler(rock * Mathf.Sin(swing), 0f, rock * 0.6f * Mathf.Cos(swing * 0.83f));
                        break;
                }
            }

            if (m_String != null)
                DrawString(since, level);

            if (m_Glow != null && m_Glow.Length > 0)
            {
                var colour = m_GlowColour * level;
                foreach (var renderer in m_Glow)
                {
                    if (renderer == null)
                        continue;
                    renderer.GetPropertyBlock(m_Block);
                    m_Block.SetColor(k_Emission, colour);
                    renderer.SetPropertyBlock(m_Block);
                }
            }
        }

        /// <summary>The string as a line bowed out by a decaying standing wave, its brightness in the line's colour.</summary>
        void DrawString(float since, float level)
        {
            float amplitude = m_Strength * Mathf.Exp(-since / Mathf.Max(0.05f, m_StringDecay));
            float wobble = Mathf.Sin(2f * Mathf.PI * (9f * since + m_Phase));
            for (int i = 0; i < k_StringPoints; i++)
            {
                float s = i / (float)(k_StringPoints - 1);
                m_Points[i] = Vector3.Lerp(m_StringFrom, m_StringTo, s) + m_StringSwing * (amplitude * wobble * Mathf.Sin(Mathf.PI * s));
            }

            m_String.SetPositions(m_Points);
            var colour = new Color(1f, 1f, 1f, 1f) * Mathf.Clamp01(0.25f + level);
            colour.a = 1f;
            m_String.startColor = colour;
            m_String.endColor = colour;
        }
    }
}

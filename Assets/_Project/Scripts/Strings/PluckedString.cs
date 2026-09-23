using UnityEngine;

namespace VRShootingGallery.Strings
{
    /// <summary>
    /// One string of a holographic instrument: its open note, its voice, and the two things the deck
    /// asks a pluck to show — displacement and a light pulse. The string is a thin tube mesh, unit
    /// length along its own Y axis with the real length carried by the transform's scale, so one
    /// shared mesh serves every string and a pluck can bend it by moving vertices.
    /// </summary>
    /// <remarks>
    /// Detection lives on the instrument (<see cref="StringSweep"/>); this component only answers
    /// <see cref="Pluck(float, float, double)"/>. Its own clip is rendered in <c>Awake</c> from
    /// <see cref="PluckedStringSynth"/> — there are no recordings to assign. An instrument that
    /// changes the note (a fretted guitar) hands in a clip of its own instead.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PluckedString : MonoBehaviour
    {
        [Header("Note")]
        [SerializeField, Tooltip("MIDI note number of the open string; 60 is middle C. The clip is synthesised from this at load.")]
        int m_Midi = 60;

        [SerializeField, Tooltip("Display name for the open note, e.g. C4. Informational.")]
        string m_NoteName = "C4";

        [SerializeField, Tooltip("Seconds until the note has fallen 60 dB. Bass strings ring longer than treble.")]
        float m_RingSeconds = 3.5f;

        [SerializeField, Tooltip("Where along the string the pluck lands, 0..0.5. Nearer the bridge is brighter.")]
        float m_PluckPosition = 0.28f;

        [SerializeField, Tooltip("Brightness of the attack, 0..1. A thumb on gut is low; a pick on steel is high.")]
        float m_Tone = 0.55f;

        [SerializeField, Tooltip("Plays the string's clip. Volume follows how hard it was plucked.")]
        AudioSource m_Source;

        [Header("Shape")]
        [SerializeField, Tooltip("Tube radius in metres, as baked into the mesh. Read by the sweep test.")]
        float m_Radius = 0.004f;

        [SerializeField, Tooltip("Emission colour. Rest and pluck are multiples of this.")]
        Color m_Glow = new(0.45f, 0.9f, 1f);

        [Header("Response")]
        [SerializeField, Tooltip("How far the middle of the string swings on a full-strength pluck, in metres. " +
            "Exaggerated well past a real string on purpose: at true scale the motion is a blur too fine " +
            "to read from across the room.")]
        float m_SwingAmplitude = 0.025f;

        [SerializeField, Tooltip("Visible swing rate in Hz. Nowhere near the audio pitch — anything above " +
            "about 15 Hz just strobes on a projector — but the treble still shivers faster than the bass.")]
        float m_SwingRate = 9f;

        [SerializeField, Tooltip("Seconds for the swing to fall to a third.")]
        float m_SwingDecay = 0.7f;

        [SerializeField, Tooltip("Emission multiplier while the string is idle.")]
        float m_RestGlow = 0.5f;

        [SerializeField, Tooltip("Fraction of the rest glow a muted string keeps, so the X'd strings of a chord read as off.")]
        float m_MutedGlow = 0.25f;

        [SerializeField, Tooltip("Emission multiplier at the moment of a full-strength pluck.")]
        float m_PluckGlow = 5f;

        [SerializeField, Tooltip("Seconds for the flash to fall to a third.")]
        float m_GlowDecay = 0.45f;

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        Mesh m_Mesh;
        Vector3[] m_RestVertices;
        Vector3[] m_Vertices;
        float[] m_Mode;
        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        AudioClip m_OwnClip;

        float m_PluckTime = float.NegativeInfinity;
        float m_Strength;
        float m_Direction = 1f;
        bool m_Bent;
        bool m_Muted;

        public int Midi => m_Midi;
        public string NoteName => m_NoteName;
        public Color Glow => m_Glow;
        public float Radius => m_Radius;
        public float RingSeconds => m_RingSeconds;
        public float PluckPosition => m_PluckPosition;
        public float Tone => m_Tone;
        public float Frequency => FrequencyOf(m_Midi);

        /// <summary>Ends of the string in world space. The mesh runs 0..1 along local Y; scale carries the length.</summary>
        public Vector3 Start => transform.position;
        public Vector3 End => transform.TransformPoint(Vector3.up);
        public Vector3 Middle => transform.TransformPoint(Vector3.up * 0.5f);

        public AudioSource Source => m_Source;

        /// <summary>
        /// A muted string keeps only a fraction of its glow and is the instrument's business not to
        /// sound. The string itself still bends if asked, so a strum across a muted string reads as
        /// the wand touching it.
        /// </summary>
        public bool Muted
        {
            get => m_Muted;
            set
            {
                m_Muted = value;
                if (!Ringing)
                    SetGlow(RestGlow);
            }
        }

        float RestGlow => m_Muted ? m_RestGlow * m_MutedGlow : m_RestGlow;
        bool Ringing => Time.time - m_PluckTime < 30f;

        public static float FrequencyOf(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        void Awake()
        {
            m_Renderer = GetComponent<MeshRenderer>();
            m_Block = new MaterialPropertyBlock();

            // The renderer's own copy, because the vertices get moved. The shared asset stays pristine
            // for the other strings and for the editor.
            var filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh != null)
            {
                m_Mesh = filter.mesh;
                m_RestVertices = m_Mesh.vertices;
                m_Vertices = new Vector3[m_RestVertices.Length];

                // Fundamental mode shape: a half sine, zero at both ends, largest in the middle.
                m_Mode = new float[m_RestVertices.Length];
                for (int i = 0; i < m_Mode.Length; i++)
                    m_Mode[i] = Mathf.Sin(Mathf.PI * Mathf.Clamp01(m_RestVertices[i].y));

                // Bent vertices leave the mesh's authored bounds, and a string caught at the edge of a
                // wall's frustum would be culled mid-swing. Widened once, here, rather than every frame.
                var bounds = m_Mesh.bounds;
                bounds.Expand(new Vector3(0f, 0f, m_SwingAmplitude * 2f));
                m_Mesh.bounds = bounds;
            }

            if (m_Source != null)
            {
                m_OwnClip = PluckedStringSynth.Render($"String {m_NoteName}", Frequency, m_RingSeconds,
                    AudioSettings.outputSampleRate, m_Midi, m_PluckPosition, m_Tone);
                m_Source.clip = m_OwnClip;
                m_Source.playOnAwake = false;
                m_Source.loop = false;
            }

            SetGlow(RestGlow);
        }

        void OnDestroy()
        {
            if (m_Mesh != null)
                Destroy(m_Mesh);
        }

        /// <summary>Sounds the open string. See <see cref="Pluck(AudioClip, float, float, double)"/>.</summary>
        public void Pluck(float strength, float direction = 1f, double dspTime = 0d)
        {
            Pluck(m_OwnClip, strength, direction, dspTime);
        }

        /// <summary>
        /// Sounds the string with <paramref name="clip"/> — the open note, or whatever a fret has made
        /// of it. <paramref name="strength"/> 0..1 sets volume, swing and flash; <paramref name="direction"/>
        /// is which side of the string plane the pluck came from, so the first swing goes the way the
        /// wand was moving. A positive <paramref name="dspTime"/> starts the sound at that point on the
        /// audio clock rather than as soon as possible, which is how a strum keeps its spread.
        /// </summary>
        public void Pluck(AudioClip clip, float strength, float direction = 1f, double dspTime = 0d)
        {
            m_Strength = Mathf.Clamp01(strength);
            m_Direction = direction < 0f ? -1f : 1f;
            m_PluckTime = Time.time;

            if (m_Source == null || clip == null)
                return;

            // Play, not PlayOneShot: a string plucked while still ringing restarts, the way a real one
            // does when the finger lands on it again. PlayOneShot would stack a second copy.
            m_Source.clip = clip;
            m_Source.volume = m_Strength;

            if (dspTime > 0d)
                m_Source.PlayScheduled(dspTime);
            else
                m_Source.Play();
        }

        // The instrument runs its sweep in LateUpdate and is ordered ahead of this, so a pluck it
        // finds shows on the string in the same frame it is heard.
        void LateUpdate()
        {
            float t = Time.time - m_PluckTime;
            if (t < 0f || t > 30f)
                return;

            float swing = m_Strength * Mathf.Exp(-t / Mathf.Max(0.01f, m_SwingDecay));
            float flash = m_Strength * Mathf.Exp(-t / Mathf.Max(0.01f, m_GlowDecay));

            if (swing < 0.01f && flash < 0.01f)
            {
                if (m_Bent)
                {
                    Bend(0f);
                    SetGlow(RestGlow);
                }
                return;
            }

            float offset = m_SwingAmplitude * swing * m_Direction * Mathf.Sin(2f * Mathf.PI * m_SwingRate * t);
            Bend(offset);
            SetGlow(Mathf.Lerp(RestGlow, m_PluckGlow, flash));
        }

        /// <summary>Moves the middle of the string <paramref name="offset"/> metres out of the instrument's plane.</summary>
        void Bend(float offset)
        {
            if (m_Mesh == null)
                return;

            for (int i = 0; i < m_Vertices.Length; i++)
            {
                var v = m_RestVertices[i];
                v.z += offset * m_Mode[i];
                m_Vertices[i] = v;
            }

            m_Mesh.vertices = m_Vertices;
            m_Bent = offset != 0f;
        }

        void SetGlow(float multiplier)
        {
            if (m_Renderer == null)
                return;

            m_Block.SetColor(k_EmissionColor, m_Glow * multiplier);
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}

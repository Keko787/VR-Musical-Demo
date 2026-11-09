using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// An old CRT that switches itself on. Its screen is static — scanlines, snow, a rolling bar —
    /// and in the static, now and then, a face: the ghost glimpsed in the machine, on a strong beat
    /// or while it is out in the room. The picture is drawn into a small texture on the CPU each
    /// frame, which needs no shader of its own and renders the same through every CAVE camera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It <b>glitches in the ghosts' colours</b>. On a beat, more often the more possessed the room,
    /// the picture tears into a blue copy and a rose copy pulled apart sideways, and a few bands of it
    /// slip and go wholly blue or rose, settling in a quarter of a second. While the ghosts are out
    /// the split stays, lightly: their face in the static is two faces, blue and rose.
    /// </para>
    /// <para>
    /// A glitch only ever comes on the analyser's beat, which is capped at three a second, and it is
    /// never brighter than the plain picture — the blue and rose copies together sum to less than the
    /// tint — so it reads as the picture breaking up, not as a flash. Low intensity halves it.
    /// </para>
    /// </remarks>
    public class HauntedTV : Possessable
    {
        [Header("Screen")]
        [SerializeField] Renderer m_Screen;

        [SerializeField, Tooltip("Resolution of the picture. A CRT from across a room does not need more, and it is redrawn every frame.")]
        Vector2Int m_Resolution = new(96, 72);

        [SerializeField] Color m_Tint = new(0.78f, 0.9f, 1f);

        [SerializeField, Tooltip("Snow brightness, before the music turns it up.")]
        float m_Snow = 0.3f;

        [SerializeField, Tooltip("How much the snow brightens with the music.")]
        float m_SnowByLevel = 0.45f;

        [Header("Glitch")]
        [SerializeField, Tooltip("The first ghost's colour in the glitches: the copy pulled left, and half the bands.")]
        Color m_GlitchBlue = new(0.3f, 0.6f, 1f);

        [SerializeField, Tooltip("Its partner's: the copy pulled right, and the other bands. Blue too with one ghost in the room.")]
        Color m_GlitchRose = new(1f, 0.4f, 0.72f);

        [SerializeField, Range(0f, 1f), Tooltip("Chance a beat glitches the picture once the set is on, at full possession; " +
            "less in a quieter room.")]
        float m_GlitchChance = 0.5f;

        [SerializeField, Tooltip("Seconds a glitch takes to settle.")]
        float m_GlitchDecay = 0.25f;

        [SerializeField, Range(0f, 1f), Tooltip("How split the picture stays while the ghosts are out.")]
        float m_RevealSplit = 0.35f;

        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");

        /// <summary>Rows in a band that slips together.</summary>
        const int k_BandRows = 5;

        Texture2D m_Texture;
        Color32[] m_Pixels;
        MaterialPropertyBlock m_Block;
        System.Random m_Random;
        uint m_Frame;
        float m_Flash;
        bool m_Dark;
        float m_Glitch;
        uint m_GlitchSeed;

        void Awake()
        {
            int w = Mathf.Max(8, m_Resolution.x);
            int h = Mathf.Max(8, m_Resolution.y);
            m_Texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "HauntedTV",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            m_Pixels = new Color32[w * h];
            m_Block = new MaterialPropertyBlock();
            m_Random = new System.Random(GetInstanceID());
            Clear();
        }

        void OnDestroy()
        {
            if (m_Texture != null)
                Destroy(m_Texture);
        }

        protected override void Animate(float dt)
        {
            m_Flash *= Mathf.Exp(-dt / 0.3f);
            float on = Mathf.Max(Woken, m_Flash);

            // Off is a dark screen, drawn once.
            if (on < 0.02f)
            {
                if (!m_Dark)
                    Clear();
                return;
            }

            m_Dark = false;
            m_Frame++;

            int w = m_Texture.width;
            int h = m_Texture.height;
            float time = Time.time;
            float snow = (m_Snow + m_SnowByLevel * m_Haunting.Level(Listen.Loudness)) * on;
            float face = Mathf.Max(Mathf.Max(m_Haunting.Glimpse, m_Haunting.Reveal * 0.85f), m_Flash) * on;
            float bar = Mathf.Repeat(time * 0.3f, 1.2f) - 0.1f;

            // The face drifts slightly, the way a picture on a badly tuned set never quite holds still.
            float driftU = 0.04f * Mathf.Sin(time * 1.3f);
            float driftV = 0.03f * Mathf.Sin(time * 0.9f);

            // Glitches: kicked by a beat, settling fast; a light split held while the ghosts are out.
            m_Glitch *= Mathf.Exp(-dt / Mathf.Max(0.05f, m_GlitchDecay));
            if (m_Haunting.Beat && on > 0.3f && m_Random.NextDouble() < m_GlitchChance * (0.4f + 0.6f * m_Haunting.Possession))
            {
                float strength = (0.5f + 0.5f * m_Haunting.BeatStrength) * (m_Haunting.LowIntensity ? 0.5f : 1f);
                m_Glitch = Mathf.Max(m_Glitch, strength);
                m_GlitchSeed = (uint)m_Random.Next();
            }

            float reveal = m_Haunting.Reveal;
            float held = m_RevealSplit * reveal * reveal * (3f - 2f * reveal);
            float glitch = Mathf.Max(m_Glitch, held) * on;
            // The held split pulls the two copies wider than its strength alone would — far enough apart to read
            // as two faces, a blue one and a rose one — and keeps them mostly in their own colours.
            float mix = Mathf.Clamp01(Mathf.Max(m_Glitch * 1.6f, held * 2.3f) * on);
            float split = 0.008f + 0.05f * glitch + 0.06f * held * on;
            float slipping = m_Glitch * on;
            var blue = m_GlitchBlue;
            var rose = m_Haunting.HasPartner ? m_GlitchRose : m_GlitchBlue;

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float scan = (y & 1) == 0 ? 1f : 0.62f;
                float roll = 1f + 0.35f * Mathf.Exp(-(v - bar) * (v - bar) / 0.003f);
                float tear = Hash(0u, (uint)y, m_Frame / 3u) > 0.97f ? 0.06f : 0f;     // the odd torn line

                // A glitch slips some bands of rows sideways and paints each one blue or rose.
                uint band = (uint)(y / k_BandRows);
                bool slipped = slipping > 0.05f && Hash(band, 7u, m_GlitchSeed) > 1f - 0.45f * slipping;
                float slip = slipped ? (Hash(band, 11u, m_GlitchSeed) - 0.5f) * 0.3f * slipping : 0f;
                bool roseBand = Hash(band, 13u, m_GlitchSeed) > 0.5f;

                float shade = scan * roll;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w + tear + slip;
                    float n = Hash((uint)x, (uint)y, m_Frame);
                    float value = Mathf.Clamp01(Picture(u, v, n, snow, face, time, driftU, driftV) * shade);
                    var colour = m_Tint * value;

                    if (mix > 0.001f)
                    {
                        // Kept under the plain tint's brightness: a picture breaking up, not a flash.
                        Color torn;
                        if (slipped)
                        {
                            torn = (roseBand ? rose : blue) * (0.85f * value);
                        }
                        else
                        {
                            float left = Mathf.Clamp01(Picture(u - split, v, n, snow, face, time, driftU, driftV) * shade);
                            float right = Mathf.Clamp01(Picture(u + split, v, n, snow, face, time, driftU, driftV) * shade);
                            torn = blue * (0.6f * left) + rose * (0.6f * right);
                        }

                        colour = Color.Lerp(colour, torn, mix);
                    }

                    m_Pixels[y * w + x] = new Color32(Byte(colour.r), Byte(colour.g), Byte(colour.b), 255);
                }
            }

            Push();
        }

        /// <summary>The static at (<paramref name="u"/>, <paramref name="v"/>), 0–1, with the ghost's face in it if it is showing.</summary>
        static float Picture(float u, float v, float n, float snow, float face, float time, float driftU, float driftV)
        {
            float value = n * snow;
            if (face <= 0.01f)
                return value;

            // The ghost in the static: a steadier, brighter sheet with dark holes, snow still through it.
            float fu = (u - 0.5f) / 0.8f + 0.5f - driftU;
            float fv = (v - 0.5f) / 0.9f + 0.5f - driftV;
            float cover = GhostSilhouette.Coverage(fu, fv, time, 0.03f, false);
            float lit = GhostSilhouette.InHole(fu, fv) ? 0.04f : 0.75f + 0.25f * n;
            return Mathf.Lerp(value, lit, cover * face);
        }

        static byte Byte(float value) => (byte)(Mathf.Clamp01(value) * 255f);

        /// <summary>The set flicks on by itself for a moment, the face already in it.</summary>
        public override void Nudge(float strength) => m_Flash = Mathf.Max(m_Flash, strength);

        void Clear()
        {
            for (int i = 0; i < m_Pixels.Length; i++)
                m_Pixels[i] = new Color32(6, 7, 8, 255);

            m_Dark = true;
            Push();
        }

        void Push()
        {
            m_Texture.SetPixels32(m_Pixels);
            m_Texture.Apply(false);

            if (m_Screen == null)
                return;

            m_Block.SetTexture(k_BaseMap, m_Texture);
            m_Screen.SetPropertyBlock(m_Block);
        }

        /// <summary>A cheap integer hash to 0–1: fresh snow every frame, no texture lookups.</summary>
        static float Hash(uint x, uint y, uint z)
        {
            uint h = x * 374761393u + y * 668265263u + z * 2147483647u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFu) / 65535f;
        }
    }
}

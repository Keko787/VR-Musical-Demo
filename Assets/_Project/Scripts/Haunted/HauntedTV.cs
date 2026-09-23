using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// An old CRT that switches itself on. Its screen is static — scanlines, snow, a rolling bar —
    /// and in the static, now and then, a face: the ghost glimpsed in the machine, on a strong beat
    /// or while it is out in the room. The picture is drawn into a small texture on the CPU each
    /// frame, which needs no shader of its own and renders the same through every CAVE camera.
    /// </summary>
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

        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");

        Texture2D m_Texture;
        Color32[] m_Pixels;
        MaterialPropertyBlock m_Block;
        uint m_Frame;
        float m_Flash;
        bool m_Dark;

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

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float scan = (y & 1) == 0 ? 1f : 0.62f;
                float roll = 1f + 0.35f * Mathf.Exp(-(v - bar) * (v - bar) / 0.003f);
                float tear = Hash(0u, (uint)y, m_Frame / 3u) > 0.97f ? 0.06f : 0f;     // the odd torn line

                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w + tear;
                    float n = Hash((uint)x, (uint)y, m_Frame);
                    float value = n * snow;

                    if (face > 0.01f)
                    {
                        // The ghost in the static: a steadier, brighter sheet with dark holes, snow still through it.
                        float fu = (u - 0.5f) / 0.8f + 0.5f - driftU;
                        float fv = (v - 0.5f) / 0.9f + 0.5f - driftV;
                        float cover = GhostSilhouette.Coverage(fu, fv, time, 0.03f, false);
                        float lit = GhostSilhouette.InHole(fu, fv) ? 0.04f : 0.75f + 0.25f * n;
                        value = Mathf.Lerp(value, lit, cover * face);
                    }

                    value = Mathf.Clamp01(value * scan * roll);
                    byte r = (byte)(value * m_Tint.r * 255f);
                    byte g = (byte)(value * m_Tint.g * 255f);
                    byte b = (byte)(value * m_Tint.b * 255f);
                    m_Pixels[y * w + x] = new Color32(r, g, b, 255);
                }
            }

            Push();
        }

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

using TMPro;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// A slim strip near the top of the front wall: the track playing, the one up next, and a bar
    /// for how far through this one the music is. It comes up when a track starts, again for the
    /// last stretch of a track (counting down to the next), and while the music is paused, and
    /// fades away in between so the world outside has the wall to itself. It steps aside while the
    /// menu is open, because the menu's own list marks the same two tracks. The glass room and the
    /// visualizer both have one.
    /// </summary>
    public class TrackIndicator : MonoBehaviour
    {
        [SerializeField] MusicDeck m_Deck;

        [SerializeField, Tooltip("The menu on the front wall. The strip hides while it is open.")]
        WallMenu m_Menu;

        [SerializeField, Tooltip("Everything that fades: the panel, the text, the bar.")]
        Transform m_Content;

        [SerializeField] TMP_Text m_Now;
        [SerializeField] TMP_Text m_Next;

        [SerializeField, Tooltip("The bar's fill, a quad stretched from its left end as the track plays.")]
        Transform m_Bar;

        [SerializeField, Tooltip("Metres the bar runs when the track is done.")]
        float m_BarLength = 1.5f;

        [SerializeField, Tooltip("The panel and the bar, faded with the text.")]
        Renderer[] m_Fade;

        [Header("When")]
        [SerializeField, Tooltip("Seconds it shows after a track starts.")]
        float m_AfterStart = 8f;

        [SerializeField, Tooltip("Seconds before a track ends that it comes back, counting down to the next.")]
        float m_BeforeEnd = 20f;

        [SerializeField, Tooltip("Shown all the time, not only round the changes of track.")]
        bool m_Always;

        [SerializeField, Tooltip("Seconds to fade in or out.")]
        float m_FadeSeconds = 0.5f;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock m_Block;
        Color[] m_FadeColours;
        Color m_NowColour, m_NextColour;
        float m_Alpha;
        float m_StartedAt = -100f;
        string m_Shown;
        int m_ShownSeconds = -1;
        bool m_ShownPaused;

        /// <summary>
        /// A track's name as people say it: the file name without the uploader's tags, so
        /// "Good Kid - Orbit (Official Audio)" reads "Good Kid - Orbit". Tags people would say
        /// ("Acoustic", "Remastered") stay.
        /// </summary>
        public static string TitleOf(AudioClip clip)
        {
            if (clip == null)
                return "";

            string title = clip.name;
            foreach (var tag in k_Tags)
            {
                int at = title.IndexOf(tag, System.StringComparison.OrdinalIgnoreCase);
                if (at > 0)
                    title = title.Remove(at, tag.Length);
            }

            // Characters a file name can't hold come through as underscores: "Porter Robinson _ Madeon".
            return title.Replace(" _ ", " & ").Trim();
        }

        static readonly string[] k_Tags = { " (Official Audio)", " (Official Video)", " (Official Music Video)", " (Lyrics)", " (Lyric Video)", " (Audio)" };

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_FadeColours = new Color[m_Fade?.Length ?? 0];
            for (int i = 0; i < m_FadeColours.Length; i++)
            {
                var material = m_Fade[i] != null ? m_Fade[i].sharedMaterial : null;
                m_FadeColours[i] = material != null && material.HasProperty(k_BaseColor) ? material.GetColor(k_BaseColor) : Color.white;
            }

            m_NowColour = m_Now != null ? m_Now.color : Color.white;
            m_NextColour = m_Next != null ? m_Next.color : Color.white;
            Apply(0f);
        }

        void OnEnable()
        {
            if (m_Deck != null)
                m_Deck.TrackStarted += OnTrackStarted;
        }

        void OnDisable()
        {
            if (m_Deck != null)
                m_Deck.TrackStarted -= OnTrackStarted;
        }

        void OnTrackStarted(AudioClip clip) => m_StartedAt = Time.time;

        void Update()
        {
            var clip = m_Deck != null ? m_Deck.Clip : null;
            float length = clip != null ? clip.length : 0f;
            float into = m_Deck != null ? m_Deck.Time : 0f;
            float left = Mathf.Max(0f, length - into);
            int next = m_Deck != null ? m_Deck.NextIndex : -1;

            bool show = clip != null && (m_Always || Time.time - m_StartedAt < m_AfterStart || m_Deck.Paused ||
                                         (next >= 0 && length > m_BeforeEnd * 2f && left < m_BeforeEnd));
            if (m_Menu != null && m_Menu.IsOpen)
                show = false;

            m_Alpha = Mathf.MoveTowards(m_Alpha, show ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, m_FadeSeconds));
            if (m_Alpha <= 0f && !show)
            {
                Apply(0f);
                return;
            }

            // The text only when it changes; the countdown only once a second.
            int seconds = left < m_BeforeEnd ? Mathf.CeilToInt(left) : -1;
            string title = TitleOf(clip);
            if (title != m_Shown || seconds != m_ShownSeconds || m_Deck.Paused != m_ShownPaused)
            {
                m_Shown = title;
                m_ShownSeconds = seconds;
                m_ShownPaused = m_Deck.Paused;
                if (m_Now != null)
                    m_Now.text = (m_Deck.Paused ? "PAUSED   " : "NOW PLAYING   ") + "<b>" + title + "</b>";
                if (m_Next != null)
                {
                    string upNext = next >= 0 ? TitleOf(m_Deck.ClipAt(next)) : "";
                    m_Next.text = next < 0 ? "" :
                        seconds >= 0 ? $"UP NEXT IN {seconds / 60}:{seconds % 60:00}   <b>{upNext}</b>" : $"UP NEXT   <b>{upNext}</b>";
                }
            }

            if (m_Bar != null)
            {
                float t = length > 0f ? Mathf.Clamp01(into / length) : 0f;
                var scale = m_Bar.localScale;
                scale.x = Mathf.Max(0.0001f, m_BarLength * t);
                m_Bar.localScale = scale;
                var at = m_Bar.localPosition;
                at.x = -m_BarLength * 0.5f + scale.x * 0.5f;
                m_Bar.localPosition = at;
            }

            Apply(m_Alpha);
        }

        void Apply(float alpha)
        {
            if (m_Content != null && m_Content.gameObject.activeSelf != alpha > 0f)
                m_Content.gameObject.SetActive(alpha > 0f);
            if (alpha <= 0f)
                return;

            // Eased, so it doesn't pop at the start or the end of the fade.
            float a = alpha * alpha * (3f - 2f * alpha);
            if (m_Now != null)
                m_Now.color = new Color(m_NowColour.r, m_NowColour.g, m_NowColour.b, m_NowColour.a * a);
            if (m_Next != null)
                m_Next.color = new Color(m_NextColour.r, m_NextColour.g, m_NextColour.b, m_NextColour.a * a);

            for (int i = 0; i < m_FadeColours.Length; i++)
            {
                if (m_Fade[i] == null)
                    continue;

                var colour = m_FadeColours[i];
                colour.a *= a;
                m_Fade[i].GetPropertyBlock(m_Block);
                m_Block.SetColor(k_BaseColor, colour);
                m_Fade[i].SetPropertyBlock(m_Block);
            }
        }
    }
}

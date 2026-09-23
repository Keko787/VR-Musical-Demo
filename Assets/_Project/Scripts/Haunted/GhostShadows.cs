using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// The ghost's shadow on the walls, with nothing there to cast it. While the ghost is unseen
    /// its shadow slides across whichever walls it is near — sharper and darker close to a wall,
    /// larger, fainter and thrown higher further off, as a shadow from the room's low light would
    /// be — and it fades as the ghost itself appears.
    /// </summary>
    /// <remarks>
    /// This is flat content on the screens, so it reads for everyone in the room, glasses or not:
    /// the part of the haunting the whole audience sees. A shadow fades before it reaches a
    /// screen's edge rather than being cut by it, which is the deck's rule for anything drawn
    /// across the CAVE's seams.
    /// </remarks>
    public class GhostShadows : MonoBehaviour
    {
        [SerializeField] Haunting m_Haunting;

        [SerializeField, Tooltip("One quad per wall, each a child of its wall's frame: x along the wall, y up, −z into the room.")]
        Renderer[] m_Shadows;

        [SerializeField, Tooltip("Each wall's width and height, metres, in the same order.")]
        Vector2[] m_WallSizes;

        [SerializeField, Range(0f, 1f)] float m_Darkness = 0.75f;

        [SerializeField, Tooltip("Metres from a wall beyond which the ghost casts nothing on it.")]
        float m_Reach = 2.2f;

        [SerializeField, Tooltip("Lowest the middle of a shadow goes, metres. The walls are furnished below this — mantel, " +
            "piano, dado — and a shadow drawn behind the furniture is a shadow nobody sees.")]
        float m_Floor = 1.4f;

        [SerializeField, Tooltip("How far a shadow is thrown up the wall per metre the ghost is from it: the room is lit from " +
            "below, by the fire and the lamps.")]
        float m_Throw = 0.2f;

        [SerializeField, Tooltip("Height of the shadow, metres, with the ghost against the wall.")]
        float m_Height = 0.7f;

        [SerializeField, Tooltip("How much bigger the shadow gets per metre the ghost is from the wall.")]
        float m_Growth = 0.5f;

        [SerializeField, Tooltip("Metres from a screen's edge over which the shadow fades out.")]
        float m_EdgeFade = 0.3f;

        [SerializeField, Range(0f, 1f), Tooltip("Possession at which the shadows start to appear: an early sign, before anything is seen.")]
        float m_From = 0.18f;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock m_Block;

        void Awake() => m_Block = new MaterialPropertyBlock();

        void LateUpdate()
        {
            if (m_Haunting == null || m_Haunting.Ghost == null || m_Shadows == null)
                return;

            var ghost = m_Haunting.Ghost.Position;
            float time = Time.time;
            float presence = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(m_From, m_From + 0.15f, m_Haunting.Possession));
            float hidden = 1f - Mathf.SmoothStep(0f, 1f, m_Haunting.Reveal);

            for (int i = 0; i < m_Shadows.Length; i++)
            {
                var shadow = m_Shadows[i];
                if (shadow == null || shadow.transform.parent == null)
                    continue;

                var wall = shadow.transform.parent;
                var size = m_WallSizes != null && i < m_WallSizes.Length ? m_WallSizes[i] : new Vector2(3f, 2.2f);
                var local = wall.InverseTransformPoint(ghost);
                float distance = Mathf.Max(0f, -local.z);

                float height = m_Height * (1f + m_Growth * distance);
                float width = height * (1f + 0.06f * Mathf.Sin(time * 1.7f + i));

                // Lit from below, by the fire and the lamps, so the shadow is thrown up the wall, higher
                // the further the ghost is from it — onto the open wallpaper above the furniture.
                float x = Mathf.Clamp(local.x, -size.x * 0.5f, size.x * 0.5f);
                float y = Mathf.Clamp(local.y + m_Throw * distance, Mathf.Max(height * 0.5f, m_Floor), size.y - height * 0.5f);

                float edge = Mathf.Min(Mathf.Min(x + size.x * 0.5f, size.x * 0.5f - x) - width * 0.35f, size.y - (y + height * 0.5f) + 0.2f);
                float fade = Mathf.Clamp01(edge / Mathf.Max(0.01f, m_EdgeFade));
                float near = 1f - Mathf.SmoothStep(0f, 1f, distance / Mathf.Max(0.01f, m_Reach));
                float alpha = m_Darkness * presence * hidden * near * fade * (1f - 0.35f * Mathf.Clamp01(distance / m_Reach));

                shadow.enabled = alpha > 0.003f;
                if (!shadow.enabled)
                    continue;

                shadow.transform.localPosition = new Vector3(x, y, shadow.transform.localPosition.z);
                shadow.transform.localScale = new Vector3(width, height, 1f);

                m_Block.SetColor(k_BaseColor, new Color(0f, 0f, 0f, alpha));
                shadow.SetPropertyBlock(m_Block);
            }
        }
    }
}

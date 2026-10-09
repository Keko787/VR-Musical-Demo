using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A rock formation of the kind the red-rock canyon is made of: tiers of near-vertical cliff, each
    /// narrower than the one below, with a ledge between them, a flat or peaked cap, and a skirt of
    /// fallen rock at the foot. One tier tall and thin is a spire; two or three broad ones are a
    /// butte or a mesa. Generated when the world first wakes; the colour is the land shader's strata.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimButte : MonoBehaviour
    {
        [SerializeField, Tooltip("Metres from the axis to the foot of the cliffs.")]
        float m_Radius = 80f;

        [SerializeField, Tooltip("Stretch of the outline across (x) and along (z) this object.")]
        Vector2 m_Aspect = Vector2.one;

        [SerializeField, Tooltip("Height of the ground at the foot, in this object's space.")]
        float m_Ground = -7f;

        [SerializeField, Tooltip("Height of the top of each tier, lowest first.")]
        float[] m_TierTops = { 70f, 118f };

        [SerializeField, Tooltip("Each tier's size as a fraction of the radius.")]
        float[] m_TierRadii = { 1f, 0.62f };

        [SerializeField, Range(0f, 0.6f), Tooltip("How ragged the outline is.")]
        float m_Jagged = 0.22f;

        [SerializeField, Range(0f, 0.4f), Tooltip("How much wider a cliff is at its foot than its top.")]
        float m_Taper = 0.08f;

        [SerializeField, Tooltip("Metres the middle of the cap rises above its edge. Spires come to a point.")]
        float m_Peak;

        [SerializeField, Range(0f, 1f), Tooltip("How far the skirt of fallen rock spreads, as a fraction of the radius.")]
        float m_Talus = 0.3f;

        [SerializeField, Range(8, 128)] int m_Sides = 56;
        [SerializeField, Range(1, 12)] int m_WallRows = 5;
        [SerializeField] int m_Seed;

        public float Radius => m_Radius;
        public float Top => m_TierTops != null && m_TierTops.Length > 0 ? m_TierTops[^1] : m_Ground;

        void Awake() => Generate();

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            int tiers = Mathf.Min(m_TierTops?.Length ?? 0, m_TierRadii?.Length ?? 0);
            if (tiers == 0)
                return;

            var b = new SimMeshes.Builder();
            int sides = m_Sides;
            var white = Color.white;

            // The cliffs start a little above the ground; the skirt covers the join.
            float footTop = m_Ground + (m_TierTops[0] - m_Ground) * 0.1f;
            float bottom = footTop;
            int previousTop = -1;

            for (int tier = 0; tier < tiers; tier++)
            {
                float top = m_TierTops[tier];
                float scale = m_TierRadii[tier];

                // Wall: rows of rings from this tier's foot to its top.
                int firstRing = b.Count;
                for (int row = 0; row <= m_WallRows; row++)
                {
                    float f = row / (float)m_WallRows;
                    float y = Mathf.Lerp(bottom, top, f);
                    float flare = 1f + m_Taper * (1f - f);
                    for (int j = 0; j < sides; j++)
                        b.Add(Point(j, tier, scale * flare, y, true), white);
                }

                for (int row = 0; row < m_WallRows; row++)
                    b.Band(firstRing + row * sides, firstRing + (row + 1) * sides, sides);

                // Ledge from the tier below's top edge in to this tier's foot. Its own vertices, so the
                // edge stays sharp under cel shading; outer ring first, so it faces up like the cap.
                if (previousTop >= 0)
                {
                    int inner = b.Count;
                    for (int j = 0; j < sides; j++)
                        b.Add(b.Vertices[firstRing + j], white);
                    b.Band(previousTop, inner, sides);
                }

                previousTop = b.Count;
                for (int j = 0; j < sides; j++)
                    b.Add(b.Vertices[firstRing + m_WallRows * sides + j], white);

                bottom = top;
            }

            // Cap: the last tier's top edge, an inner ring, and the middle.
            float capY = m_TierTops[tiers - 1];
            int capInner = b.Count;
            float lastScale = m_TierRadii[tiers - 1];

            // Lumps on top in proportion to the top: rubble on a mesa, nothing to trip on under the room.
            float bumps = Mathf.Min(2f, m_Radius * lastScale * 0.03f);
            for (int j = 0; j < sides; j++)
            {
                var p = Point(j, tiers - 1, lastScale * 0.55f, capY, false);
                p.y += m_Peak * 0.45f + (SimMeshes.Fbm(p.x / 20f, p.z / 20f + m_Seed) - 0.5f) * bumps;
                b.Add(p, white);
            }
            b.Band(previousTop, capInner, sides);

            int middle = b.Add(new Vector3(0f, capY + m_Peak + bumps * 0.25f, 0f), white);
            for (int j = 0; j < sides; j++)
                b.Triangle(capInner + j, capInner + (j + 1) % sides, middle);

            // Skirt: from the foot of the cliffs out and down into the ground.
            int skirtTop = b.Count;
            for (int j = 0; j < sides; j++)
                b.Add(Point(j, 0, m_TierRadii[0] * (1f + m_Taper), footTop, true), white);
            int skirtFoot = b.Count;
            for (int j = 0; j < sides; j++)
            {
                var p = Point(j, 0, m_TierRadii[0] * (1f + m_Taper + m_Talus), m_Ground - 2.5f, true);
                b.Add(p, white);
            }
            b.Band(skirtFoot, skirtTop, sides);

            SimMeshes.Assign(GetComponent<MeshFilter>(), b.ToMesh($"{name}_Rock"));
        }

        /// <summary>A point on the outline: side <paramref name="j"/>, at a height, scaled. Walls get a little relief.</summary>
        Vector3 Point(int j, int tier, float scale, float y, bool relief)
        {
            float angle = j / (float)m_Sides * Mathf.PI * 2f;
            float rag = 1f + m_Jagged * (SimMeshes.Around(angle, 2.4f, m_Seed * 3 + tier, 4) - 0.5f) * 2f;
            if (relief)
                rag *= 1f + 0.07f * (SimMeshes.Fbm(angle * 6f + m_Seed, y / 22f, 2) - 0.5f);

            float r = m_Radius * scale * rag;
            return new Vector3(Mathf.Sin(angle) * r * m_Aspect.x, y, Mathf.Cos(angle) * r * m_Aspect.y);
        }
    }
}

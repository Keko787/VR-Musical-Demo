using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The pool of the Sky Pool world: a long swimming pool standing in the open sea, with the glass
    /// room on the deck at its near end. A white deck runs round it and a grey coping along the water's
    /// edge. At the waterline is a frieze of blue tiles with a white Greek key, there are steel ladders
    /// near both ends, and the far end is an infinity edge where the water meets the sea. Its water is
    /// a child drawn by <c>VRMusic/SimSkyPool</c> (not water but sky), and this hands that shader the
    /// pool's shape.
    /// </summary>
    /// <remarks>
    /// The platform is one mesh generated when the world wakes, on the land material, painted with
    /// vertex colours. Its blocks run down past the sea level, so their outer faces are the walls the
    /// sea washes against. Assumes it stands at the outside's origin, unrotated, which is how the
    /// builder places it: the water's shader works in the outside's own frame.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimPool : MonoBehaviour
    {
        [SerializeField, Tooltip("The water: a child drawn by the sky-beneath shader. Its mesh is made here.")]
        MeshFilter m_Water;

        [Header("Shape")]
        [SerializeField, Tooltip("Metres across the water.")]
        float m_Width = 10f;

        [SerializeField, Tooltip("Metres from the near end of the water to the far end.")]
        float m_Length = 50f;

        [SerializeField, Tooltip("Metres from the room to the near end of the water.")]
        float m_Near = 2.4f;

        [SerializeField, Tooltip("Metres the water stands below the deck: the height of the waterline frieze.")]
        float m_Freeboard = 0.15f;

        [SerializeField, Tooltip("Width of the grey coping along the water's edge.")]
        float m_Coping = 0.45f;

        [SerializeField, Tooltip("Width of the white deck beyond the coping, either side.")]
        float m_Deck = 3f;

        [SerializeField, Tooltip("Metres the deck runs back behind the room.")]
        float m_Behind = 7f;

        [SerializeField, Tooltip("Height of the sea below the deck. The platform's walls run down past it.")]
        float m_SeaLevel = -1f;

        [Header("Details")]
        [SerializeField, Tooltip("Metres along the wall for each turn of the Greek key in the frieze.")]
        float m_KeyLength = 0.13f;

        [SerializeField, Tooltip("Metres in from the near end, and from the far end, to the ladders on both sides.")]
        Vector2 m_Ladders = new(3f, 5f);

        [SerializeField, Tooltip("Metres the infinity edge's lip stands above the water: the dark line where the pool meets the sea.")]
        float m_Lip = 0.08f;

        [Header("Colours")]
        [SerializeField] Color m_DeckColour = new(0.82f, 0.81f, 0.78f);
        [SerializeField] Color m_CopingColour = new(0.5f, 0.53f, 0.57f);
        [SerializeField] Color m_TileColour = new(0.07f, 0.26f, 0.62f);
        [SerializeField] Color m_KeyColour = new(0.93f, 0.94f, 0.96f);
        [SerializeField] Color m_LipColour = new(0.2f, 0.25f, 0.34f);
        [SerializeField] Color m_SteelColour = new(0.78f, 0.8f, 0.84f);

        /// <summary>
        /// One turn of the Greek key, as bars (from, to along the wall; from, to up the band) in a unit
        /// cell: up the left, across the top, down the right, back in and up into the hook. A rail
        /// along the bottom joins the turns.
        /// </summary>
        static readonly Vector4[] k_Key =
        {
            new(0f, 0.16f, 0f, 1f),
            new(0f, 0.8f, 0.84f, 1f),
            new(0.64f, 0.8f, 0.36f, 1f),
            new(0.34f, 0.8f, 0.36f, 0.52f),
            new(0.34f, 0.5f, 0.36f, 0.68f),
        };

        static readonly int k_Pool = Shader.PropertyToID("_Pool");

        Renderer m_WaterRenderer;
        MaterialPropertyBlock m_Block;

        /// <summary>The water's shape for its shader: (half width, near end, far end, where the music pours in), metres from the room.</summary>
        public Vector4 Shape => new(m_Width * 0.5f, m_Near, m_Near + m_Length, m_Near);

        void Awake() => Generate();

        void OnEnable()
        {
            m_WaterRenderer = m_Water != null ? m_Water.GetComponent<Renderer>() : null;
            Push();
        }

        /// <summary>Every frame, so the pool can be tuned in play mode and its water follows.</summary>
        void Update() => Push();

        void OnDestroy()
        {
            foreach (var filter in new[] { GetComponent<MeshFilter>(), m_Water })
                if (filter != null && filter.sharedMesh != null && (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                    SimMeshes.Destroy(filter.sharedMesh);
        }

        void Push()
        {
            if (m_WaterRenderer == null)
                return;

            m_Block ??= new MaterialPropertyBlock();
            m_WaterRenderer.GetPropertyBlock(m_Block);
            m_Block.SetVector(k_Pool, Shape);
            m_WaterRenderer.SetPropertyBlock(m_Block);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            var b = new SimMeshes.Builder();
            float half = m_Width * 0.5f;
            float edge = half + m_Coping;
            float outer = edge + m_Deck;
            float near = m_Near;
            float far = m_Near + m_Length;
            float end = far + 0.3f;
            float bottom = m_SeaLevel - 4f;
            float water = -m_Freeboard;

            // The deck: behind the room and round the near end, and along both sides.
            Slab(b, -outer, outer, -m_Behind, near - m_Coping, bottom, 0f, m_DeckColour);
            Slab(b, -outer, -edge, near - m_Coping, end, bottom, 0f, m_DeckColour);
            Slab(b, edge, outer, near - m_Coping, end, bottom, 0f, m_DeckColour);

            // The coping round the water's three walled sides. Its inner faces are the pool's walls.
            Slab(b, -edge, edge, near - m_Coping, near, bottom, 0f, m_CopingColour);
            Slab(b, -edge, -half, near, end, bottom, 0f, m_CopingColour);
            Slab(b, half, edge, near, end, bottom, 0f, m_CopingColour);

            // The infinity edge: at the far end the wall stops just above the water, which spills over into the sea.
            Slab(b, -half, half, far, end, bottom, water + m_Lip, m_LipColour);

            Frieze(b, -half, 1f, near, far);
            Frieze(b, half, -1f, near, far);

            foreach (float side in new[] { -1f, 1f })
            {
                Ladder(b, side, near + m_Ladders.x);
                Ladder(b, side, far - m_Ladders.y);
            }

            SimMeshes.Assign(GetComponent<MeshFilter>(), b.ToMesh($"{name}_Pool"));

            // The water: one quad at the waterline. Its shader does the rest.
            if (m_Water != null)
            {
                var w = new SimMeshes.Builder();
                int a = w.Add(new Vector3(-half, water, near), Color.white);
                w.Add(new Vector3(-half, water, far), Color.white);
                w.Add(new Vector3(half, water, far), Color.white);
                w.Add(new Vector3(half, water, near), Color.white);
                w.Triangle(a, a + 1, a + 2);
                w.Triangle(a, a + 2, a + 3);
                SimMeshes.Assign(m_Water, w.ToMesh($"{name}_Water"));
            }
        }

        static void Slab(SimMeshes.Builder b, float x0, float x1, float z0, float z1, float y0, float y1, Color colour)
        {
            SimMeshes.Box(b, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0, y1 - y0, z1 - z0), colour);
        }

        /// <summary>A quad on a wall that faces along x, standing at <paramref name="x"/>.</summary>
        static void WallQuad(SimMeshes.Builder b, float x, float z0, float z1, float y0, float y1, Color colour)
        {
            int a = b.Add(new Vector3(x, y0, z0), colour);
            b.Add(new Vector3(x, y1, z0), colour);
            b.Add(new Vector3(x, y1, z1), colour);
            b.Add(new Vector3(x, y0, z1), colour);
            b.Triangle(a, a + 1, a + 2);
            b.Triangle(a, a + 2, a + 3);
        }

        /// <summary>
        /// The waterline frieze along one side wall: a band of blue tiles from below the water to just
        /// under the coping, and on it a white Greek key. Each layer stands a centimetre proud of the
        /// last, which is plenty at the outside's distance from the scene's origin.
        /// </summary>
        void Frieze(SimMeshes.Builder b, float x, float facing, float from, float to)
        {
            float water = -m_Freeboard;
            float top = -0.012f;
            WallQuad(b, x + facing * 0.01f, from, to, water - 0.05f, top, m_TileColour);

            float margin = (top - water) * 0.14f;
            float y0 = water + margin;
            float height = top - water - margin * 2f;
            float kx = x + facing * 0.02f;
            float length = Mathf.Max(0.02f, m_KeyLength);

            WallQuad(b, kx, from, to, y0, y0 + height * 0.16f, m_KeyColour);
            int turns = Mathf.FloorToInt((to - from) / length);
            for (int i = 0; i < turns; i++)
            {
                float z = from + i * length;
                foreach (var bar in k_Key)
                    WallQuad(b, kx, z + bar.x * length, z + bar.y * length, y0 + bar.z * height, y0 + bar.w * height, m_KeyColour);
            }
        }

        /// <summary>
        /// A pool ladder's pair of grab rails on one side wall: up from the deck, over the coping in a
        /// half circle, and down into the water. The treads are under the water, where the sky hides them.
        /// </summary>
        void Ladder(SimMeshes.Builder b, float side, float z)
        {
            float half = m_Width * 0.5f;
            float outer = side * (half + 0.75f);
            float inner = side * (half - 0.15f);
            float centre = (outer + inner) * 0.5f;
            float radius = Mathf.Abs(outer - inner) * 0.5f;
            const float rise = 0.75f;
            const int steps = 12;

            var path = new List<Vector3>(steps + 3);
            foreach (float dz in new[] { -0.28f, 0.28f })
            {
                path.Clear();
                path.Add(new Vector3(outer, 0f, z + dz));
                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.PI * i / steps;
                    path.Add(new Vector3(centre + side * Mathf.Cos(a) * radius, rise + Mathf.Sin(a) * radius, z + dz));
                }

                path.Add(new Vector3(inner, -m_Freeboard - 0.4f, z + dz));
                Tube(b, path, 0.03f, m_SteelColour);
            }
        }

        /// <summary>A six-sided tube along a path, its rings sharing vertices so it shades round.</summary>
        static void Tube(SimMeshes.Builder b, List<Vector3> path, float radius, Color colour)
        {
            const int sides = 6;
            int previous = -1;
            for (int i = 0; i < path.Count; i++)
            {
                var along = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                var across = Vector3.Cross(along, Vector3.forward);
                if (across.sqrMagnitude < 1e-4f)
                    across = Vector3.Cross(along, Vector3.up);
                across.Normalize();
                var other = Vector3.Cross(along, across);

                int start = b.Count;
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    b.Add(path[i] + (across * Mathf.Cos(a) + other * Mathf.Sin(a)) * radius, colour);
                }

                if (previous >= 0)
                    b.Band(previous, start, sides);
                previous = start;
            }
        }
    }
}

using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// A curtain the ghost's passing lifts. It billows into the room when the ghost comes near —
    /// the wake that gives away where something invisible is — stirs with the mids once the room is
    /// possessed, and gusts on the beat. The cloth is a grid mesh deformed on the CPU: a couple of
    /// hundred vertices, no physics, no cloth solver.
    /// </summary>
    /// <remarks>
    /// The mesh hangs from its top edge at the origin, across +X and down −Y, with its pleats in Z
    /// and its face toward −Z, the room. The builder saves one to disk through <see cref="BuildMesh"/>
    /// so the curtain is there in the editor; at run time the component works on its own copy.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter))]
    public class HauntedCurtain : Possessable
    {
        [Header("Cloth")]
        [SerializeField, Tooltip("Metres the bottom of the curtain lifts into the room at a full billow.")]
        float m_Billow = 0.3f;

        [SerializeField, Tooltip("How much the ghost's nearness billows it, against the music.")]
        float m_GhostDraught = 1f;

        [SerializeField, Tooltip("Metres of the ripple running down the cloth while it moves.")]
        float m_Ripple = 0.025f;

        [SerializeField] float m_RiseSeconds = 0.4f;
        [SerializeField] float m_FallSeconds = 1.4f;

        Mesh m_Mesh;
        Vector3[] m_Rest;
        Vector3[] m_Vertices;
        float m_Width = 1f;
        float m_Length = 1f;
        float m_Blow;
        float m_Gust;

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh == null)
                return;

            m_Mesh = Instantiate(filter.sharedMesh);
            m_Mesh.name = filter.sharedMesh.name + " (billowing)";
            m_Mesh.MarkDynamic();
            filter.sharedMesh = m_Mesh;

            m_Rest = m_Mesh.vertices;
            m_Vertices = (Vector3[])m_Rest.Clone();

            var bounds = m_Mesh.bounds;
            m_Width = Mathf.Max(0.01f, bounds.size.x);
            m_Length = Mathf.Max(0.01f, bounds.size.y);

            // Room for the billow, so the cloth is not culled while it is out in the room.
            m_Mesh.bounds = new Bounds(bounds.center + Vector3.back * m_Billow * 0.5f,
                bounds.size + new Vector3(0.2f, 0.2f, m_Billow * 2f));
        }

        void OnDestroy()
        {
            if (m_Mesh != null)
                Destroy(m_Mesh);
        }

        protected override void Animate(float dt)
        {
            if (m_Mesh == null)
                return;

            if (m_Haunting.Beat && Woken > 0.3f)
                m_Gust = Mathf.Max(m_Gust, 0.35f * m_Haunting.BeatStrength * Woken);

            m_Gust *= Mathf.Exp(-dt / 0.5f);

            float want = Mathf.Clamp01(m_GhostDraught * m_Haunting.WakeAt(transform.position) + 0.45f * Level + m_Gust);
            float seconds = want > m_Blow ? m_RiseSeconds : m_FallSeconds;
            m_Blow = Mathf.Lerp(m_Blow, want, 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, seconds)));

            float time = Time.time;
            for (int i = 0; i < m_Rest.Length; i++)
            {
                var rest = m_Rest[i];
                float down = Mathf.Clamp01(-rest.y / m_Length);     // 0 at the rod, 1 at the hem
                float across = rest.x / m_Width;

                // Lifts most at the hem, like cloth blown from below, with a slow wave across it.
                float lift = Mathf.Pow(down, 1.4f) * (0.8f + 0.2f * Mathf.Sin(across * Mathf.PI * 2f + time * 1.7f));
                float ripple = m_Ripple * Mathf.Sin(down * 9f - time * 4f + across * 3f) * down;
                float sway = 0.03f * Mathf.Sin(time * 1.3f + down * 2f) * down;

                m_Vertices[i] = new Vector3(
                    rest.x + sway * m_Blow,
                    rest.y + 0.08f * lift * m_Blow,             // blown cloth rides up a little
                    rest.z - m_Blow * (m_Billow * lift + ripple));
            }

            m_Mesh.vertices = m_Vertices;
            m_Mesh.RecalculateNormals();
        }

        public override void Nudge(float strength) => m_Gust = Mathf.Max(m_Gust, strength);

        /// <summary>
        /// A pleated curtain: <paramref name="width"/> across +X from the origin and
        /// <paramref name="length"/> down −Y, <paramref name="folds"/> pleats deep
        /// <paramref name="foldDepth"/> in Z, facing −Z.
        /// </summary>
        public static Mesh BuildMesh(float width, float length, int columns, int rows, float foldDepth, int folds)
        {
            columns = Mathf.Max(2, columns);
            rows = Mathf.Max(2, rows);

            var vertices = new Vector3[columns * rows];
            var uvs = new Vector2[vertices.Length];
            for (int r = 0; r < rows; r++)
            {
                float v = r / (float)(rows - 1);
                for (int c = 0; c < columns; c++)
                {
                    float u = c / (float)(columns - 1);

                    // Pleats deepen toward the hem, where the cloth hangs free of the rod.
                    float pleat = foldDepth * Mathf.Sin(u * folds * Mathf.PI * 2f) * (0.6f + 0.4f * v);
                    vertices[r * columns + c] = new Vector3(u * width, -v * length, pleat);
                    uvs[r * columns + c] = new Vector2(u, 1f - v);
                }
            }

            // Wound so the faces point to −Z: across the top of a quad, then down.
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            int t = 0;
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < columns - 1; c++)
                {
                    int a = r * columns + c;
                    int b = a + 1;
                    int d = a + columns;
                    int e = d + 1;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = d;
                    triangles[t++] = b;
                    triangles[t++] = e;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = "Curtain" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

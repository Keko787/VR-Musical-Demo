using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The shape of one aurora curtain: a strip hung in an arc round the glass room, folded a little
    /// along its length. All the life in it — the rays reaching up with their bands, the folds
    /// swaying, the surge on the beat — is in its shader (<c>VRMusic/SimAurora</c>), which reads the
    /// music from the shared globals; this only builds the strip.
    /// </summary>
    /// <remarks>
    /// The curtain's mirror image in the lake is a second one of these on a transform flipped about
    /// the water level, drawn with the reflection material.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AuroraCurtain : MonoBehaviour
    {
        [SerializeField, Tooltip("Metres from the room to the curtain.")]
        float m_Radius = 2600f;

        [SerializeField, Tooltip("Where the arc starts and ends, degrees from straight ahead, clockwise from above.")]
        float m_FromDegrees = -55f;

        [SerializeField] float m_ToDegrees = 55f;

        [SerializeField, Tooltip("Height of the curtain's lower edge, metres.")]
        float m_Base = 260f;

        [SerializeField, Tooltip("Metres from the lower edge to the top of a ray at full level.")]
        float m_Height = 1000f;

        [SerializeField, Range(0f, 0.4f), Tooltip("How much the arc folds in and out along its length.")]
        float m_Folds = 0.12f;

        [SerializeField, Range(8, 400)] int m_Columns = 180;
        [SerializeField, Range(2, 32)] int m_Rows = 10;
        [SerializeField] int m_Seed = 1;

        [Header("Look")]
        [SerializeField, ColorUsage(false, true)] Color m_LowColour = new(0.15f, 1f, 0.45f);
        [SerializeField, ColorUsage(false, true)] Color m_HighColour = new(0.8f, 0.25f, 0.95f);
        [SerializeField] float m_Brightness = 1.2f;

        [SerializeField, Tooltip("The part of the spectrum the curtain spans, middle to ends: 0 the lowest band, 1 the highest.")]
        Vector2 m_Bands = new(0f, 1f);

        static readonly int k_ColorLow = Shader.PropertyToID("_ColorLow");
        static readonly int k_ColorHigh = Shader.PropertyToID("_ColorHigh");
        static readonly int k_Brightness = Shader.PropertyToID("_Brightness");
        static readonly int k_BandRange = Shader.PropertyToID("_BandRange");

        void Awake()
        {
            Generate();

            var renderer = GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(k_ColorLow, m_LowColour);
            block.SetColor(k_ColorHigh, m_HighColour);
            block.SetFloat(k_Brightness, m_Brightness);
            block.SetVector(k_BandRange, new Vector4(m_Bands.x, m_Bands.y, 0f, 0f));
            renderer.SetPropertyBlock(block);
        }

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            var b = new SimMeshes.Builder();
            float from = m_FromDegrees * Mathf.Deg2Rad;
            float to = m_ToDegrees * Mathf.Deg2Rad;

            for (int row = 0; row <= m_Rows; row++)
            {
                float v = row / (float)m_Rows;
                for (int column = 0; column <= m_Columns; column++)
                {
                    float u = column / (float)m_Columns;
                    float angle = Mathf.Lerp(from, to, u);
                    float fold = 1f + m_Folds * (SimMeshes.Fbm(u * 6f + m_Seed * 5.1f, 0.5f, 3) - 0.5f) * 2f;
                    float r = m_Radius * fold;
                    var p = new Vector3(Mathf.Sin(angle) * r, m_Base + v * m_Height, Mathf.Cos(angle) * r);
                    b.Add(p, Color.white, new Vector2(u, v));
                }
            }

            int stride = m_Columns + 1;
            for (int row = 0; row < m_Rows; row++)
                for (int column = 0; column < m_Columns; column++)
                {
                    int a = row * stride + column;
                    b.Triangle(a, a + 1, a + stride);
                    b.Triangle(a + 1, a + stride + 1, a + stride);
                }

            var mesh = b.ToMesh($"{name}_Curtain", false);

            // The shader sways the folds sideways; keep the curtain on screen when it does.
            SimMeshes.Inflate(mesh, 200f);
            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);
        }
    }
}

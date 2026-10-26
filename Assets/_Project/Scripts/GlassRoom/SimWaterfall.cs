using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A waterfall down a mountainside: from where it starts, it runs the way water does, straight
    /// down the steepest slope of the ground, until the ground levels out or it reaches the water.
    /// It is one ribbon lying on the rock, drawn by <c>VRMusic/SimFall</c>, which pours streaks of
    /// white down it — faster and brighter with its band of the music, and wider as it swells.
    /// </summary>
    /// <remarks>
    /// The ribbon's first UV channel is (across, −1 to 1; metres down), the second its width and its
    /// whole length, and each vertex's normal is the direction across it, so the shader can widen it.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimWaterfall : MonoBehaviour
    {
        [SerializeField] SimSurface m_Ground;

        [SerializeField, Tooltip("Where the water comes over the edge (x, z, on the ground's own scale).")]
        Vector2 m_Source;

        [SerializeField, Tooltip("Metres across the fall.")]
        float m_Width = 14f;

        [SerializeField, Tooltip("It stops when it gets down to this height: the water at the foot.")]
        float m_StopAt;

        [SerializeField, Tooltip("Metres of fall at most.")]
        float m_MaxLength = 1800f;

        [SerializeField, Tooltip("Metres the ribbon floats off the rock, so the ground doesn't cut through it.")]
        float m_Lift = 3f;

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
            if (m_Ground == null)
                return;

            var path = Trace();
            if (path.Count < 2)
                return;

            var b = new SimMeshes.Builder();
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
                total += Vector3.Distance(path[i - 1], path[i]);

            float along = 0f;
            var across = Vector3.right;
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0)
                    along += Vector3.Distance(path[i - 1], path[i]);

                var ahead = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)];
                var flat = new Vector3(ahead.x, 0f, ahead.z);
                if (flat.sqrMagnitude > 1e-4f)
                    across = Vector3.Cross(Vector3.up, flat.normalized);

                // The foot spreads out where it lands.
                float spread = 1f + 1.5f * Mathf.Pow(along / Mathf.Max(1f, total), 6f);
                float half = m_Width * 0.5f * spread;
                var width = new Vector2(half, total);
                var ground = ToLocal(path[i]);
                var acrossLocal = transform.InverseTransformDirection(m_Ground.transform.TransformDirection(across));
                int left = b.Add(ground - acrossLocal * half, Color.white, new Vector2(-1f, along), width);
                b.Add(ground + acrossLocal * half, Color.white, new Vector2(1f, along), width);

                if (i > 0)
                {
                    b.Triangle(left - 2, left, left - 1);
                    b.Triangle(left - 1, left, left + 1);
                }
            }

            var mesh = b.ToMesh($"{name}_Fall", false);

            // The normal carries the direction across the ribbon, for the shader to widen it along.
            var normals = new Vector3[b.Count];
            for (int i = 0; i < normals.Length; i += 2)
            {
                var dir = (b.Vertices[i + 1] - b.Vertices[i]).normalized;
                normals[i] = dir;
                normals[i + 1] = dir;
            }

            mesh.normals = normals;
            SimMeshes.Inflate(mesh, m_Width * 2f);
            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);
        }

        /// <summary>Downhill from the source in steps, along the steepest way down, on the ground's own scale.</summary>
        List<Vector3> Trace()
        {
            var path = new List<Vector3>();
            float x = m_Source.x, z = m_Source.y;
            float length = 0f;
            float flatFor = 0f;
            const float step = 6f;

            for (int i = 0; i < 2000 && length < m_MaxLength; i++)
            {
                float h = m_Ground.HeightAt(x, z);
                path.Add(new Vector3(x, h + m_Lift, z));
                if (h <= m_StopAt)
                    break;

                float gx = m_Ground.HeightAt(x + 2f, z) - m_Ground.HeightAt(x - 2f, z);
                float gz = m_Ground.HeightAt(x, z + 2f) - m_Ground.HeightAt(x, z - 2f);
                var down = new Vector2(-gx, -gz);
                float slope = down.magnitude / 4f;

                // Run out across the valley floor for a little, then stop: it has become a river.
                flatFor = slope < 0.08f ? flatFor + step : 0f;
                if (flatFor > 60f || down.sqrMagnitude < 1e-8f)
                    break;

                down.Normalize();
                x += down.x * step;
                z += down.y * step;
                float drop = h - m_Ground.HeightAt(x, z);
                length += Mathf.Sqrt(step * step + drop * drop);
            }

            return path;
        }

        Vector3 ToLocal(Vector3 groundPoint) => transform.InverseTransformPoint(m_Ground.transform.TransformPoint(groundPoint));
    }
}

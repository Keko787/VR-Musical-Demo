using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>One arch: where it leaves the ground and comes back down, how high, how thick.</summary>
    [System.Serializable]
    public struct TubeArch
    {
        public Vector3 From;
        public Vector3 To;
        public float Height;
        public float Radius;
    }

    /// <summary>
    /// Ribbed alien tubes arching out of the meadow and back into it, like the worm-like growths in
    /// the film the space world is drawn from. Ribs alternate in thickness and shade, so the tube reads
    /// as segmented under cel shading. Painted from the material's palette slot. One mesh.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimTubes : MonoBehaviour
    {
        [SerializeField] TubeArch[] m_Arches;

        [SerializeField, Tooltip("The ground the arches grow out of: each end is set on it.")]
        SimSurface m_Ground;

        [SerializeField, Range(8, 120)] int m_Segments = 48;
        [SerializeField, Range(6, 24)] int m_Sides = 12;
        [SerializeField, Tooltip("Ribs along each arch.")] int m_Ribs = 14;

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
            if (m_Arches == null)
                return;

            var b = new SimMeshes.Builder();
            foreach (var arch in m_Arches)
                Arch(b, arch);

            SimMeshes.Assign(GetComponent<MeshFilter>(), b.ToMesh($"{name}_Tubes"));
        }

        float GroundAt(Vector3 local)
        {
            if (m_Ground == null)
                return local.y;

            var onGround = m_Ground.transform.InverseTransformPoint(transform.TransformPoint(local));
            onGround.y = m_Ground.HeightAt(onGround.x, onGround.z);
            return transform.InverseTransformPoint(m_Ground.transform.TransformPoint(onGround)).y;
        }

        void Arch(SimMeshes.Builder b, TubeArch arch)
        {
            arch.From.y = GroundAt(arch.From);
            arch.To.y = GroundAt(arch.To);

            // Sunk a little at both ends, so the tube grows out of the ground rather than resting on it.
            var from = arch.From + Vector3.down * arch.Radius;
            var to = arch.To + Vector3.down * arch.Radius;
            Vector3 Point(float t) => Vector3.Lerp(from, to, t) + Vector3.up * (arch.Height * 4f * t * (1f - t));

            int first = b.Count;
            for (int i = 0; i <= m_Segments; i++)
            {
                float t = i / (float)m_Segments;
                var centre = Point(t);
                var tangent = (Point(Mathf.Min(1f, t + 0.01f)) - Point(Mathf.Max(0f, t - 0.01f))).normalized;
                var u = Vector3.Cross(tangent, Vector3.up).normalized;
                if (u.sqrMagnitude < 0.01f)
                    u = Vector3.right;
                var v = Vector3.Cross(u, tangent);

                float rib = Mathf.Abs(Mathf.Sin(t * m_Ribs * Mathf.PI));
                float r = arch.Radius * (0.85f + 0.15f * Mathf.Pow(rib, 0.5f)) * (0.85f + 0.3f * Mathf.Sin(t * Mathf.PI));
                float shade = 0.82f + 0.2f * rib;
                var colour = new Color(shade, shade, shade, 1f);

                for (int j = 0; j < m_Sides; j++)
                {
                    float a = j / (float)m_Sides * Mathf.PI * 2f;
                    b.Add(centre + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r, colour);
                }
            }

            for (int i = 0; i < m_Segments; i++)
                b.Band(first + i * m_Sides, first + (i + 1) * m_Sides, m_Sides);
        }
    }
}

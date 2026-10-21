using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A planet or a star in a space world's sky: one quad that its shader (<c>VRMusic/SimPlanet</c>)
    /// turns to face the room and paints — the banded planet and its ring of light that is the
    /// spectrum, or the star whose corona breathes with the bass. Its look is its material; this only
    /// gives it a size and a quad whose bounds cover it wherever the shader puts it.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimCelestial : MonoBehaviour
    {
        [SerializeField, Tooltip("Metres from the middle to the edge of the planet or star.")]
        float m_Radius = 800f;

        [SerializeField, Tooltip("Half the quad's size in radii: room for a ring or a corona round the body.")]
        float m_Extent = 2.4f;

        static readonly int k_Radius = Shader.PropertyToID("_Radius");
        static readonly int k_Extent = Shader.PropertyToID("_Extent");

        void Awake()
        {
            var mesh = SimMeshes.NewMesh($"{name}_Quad", 4);
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            float reach = m_Radius * m_Extent * 2f;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * reach);
            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);

            var renderer = GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(k_Radius, m_Radius);
            block.SetFloat(k_Extent, m_Extent);
            renderer.SetPropertyBlock(block);
        }

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }
    }
}

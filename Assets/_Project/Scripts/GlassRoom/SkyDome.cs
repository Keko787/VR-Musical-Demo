using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The sphere the outside's sky is painted on, centred on the glass room. One dome serves every
    /// world: what it shows comes from the active <see cref="SimWorld"/> through shader globals.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SkyDome : MonoBehaviour
    {
        [SerializeField, Tooltip("Metres. Beyond everything in the worlds, inside the window cameras' far plane.")]
        float m_Radius = 9000f;

        void Awake()
        {
            SimMeshes.Assign(GetComponent<MeshFilter>(), SimMeshes.Dome("Sky_Dome", m_Radius, 32, 64));
        }

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }
    }
}

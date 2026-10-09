using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// Tells the sea where the beach is. The sea's shader (<c>VRMusic/SimOcean</c>) works out its own
    /// waves, foam and surge from the music; what it cannot know is the shape of the sand it washes up,
    /// so this hands it the beach's waterline from the beach's own <see cref="SimSurface"/> — one
    /// source for the shore, whatever is tuned on the beach.
    /// </summary>
    /// <remarks>Assumes the beach stands at the outside's origin, unrotated, which is how the builder places it.</remarks>
    [RequireComponent(typeof(Renderer))]
    public class SimOcean : MonoBehaviour
    {
        [SerializeField, Tooltip("The beach this sea washes up.")]
        SimSurface m_Beach;

        static readonly int k_Shore = Shader.PropertyToID("_Shore");

        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;

        void OnEnable()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Block ??= new MaterialPropertyBlock();
            Push();
        }

        /// <summary>Every frame, so the shore can be tuned on the beach in play mode and the sea follows.</summary>
        void Update() => Push();

        void Push()
        {
            if (m_Renderer == null || m_Beach == null)
                return;

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetVector(k_Shore, m_Beach.ShoreParams);
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}

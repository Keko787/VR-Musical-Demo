using UnityEngine;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Shows/hides the gun mesh without disabling the GameObject, so the muzzle, tracer,
    /// and aim transforms keep working when the model is hidden (cave-prop mode).
    /// </summary>
    public class GunVisibilityToggle : MonoBehaviour
    {
        [SerializeField] Renderer[] m_ModelRenderers;
        [SerializeField] bool m_ModelVisible = true;

        public bool ModelVisible
        {
            get => m_ModelVisible;
            set
            {
                m_ModelVisible = value;
                Apply();
            }
        }

        void Start() => Apply();

        void Apply()
        {
            if (m_ModelRenderers == null)
                return;

            foreach (var r in m_ModelRenderers)
                if (r != null)
                    r.enabled = m_ModelVisible;
        }
    }
}

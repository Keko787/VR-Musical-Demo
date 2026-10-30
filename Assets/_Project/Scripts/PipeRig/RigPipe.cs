using UnityEngine;

namespace VRShootingGallery.Pipes
{
    /// <summary>
    /// One pipe in the bank. Balls leave from its mouth; its nozzle swings round to face the way the
    /// next ball will go a moment before it fires, kicks back as it fires, and the band round it
    /// flares, so the eye is drawn to where each throw starts.
    /// </summary>
    public class RigPipe : MonoBehaviour
    {
        [SerializeField, Tooltip("Swings toward the next throw. Its up axis is the way the pipe points at rest.")]
        Transform m_Nozzle;

        [SerializeField, Tooltip("Where balls leave, in this object's space.")]
        Vector3 m_Mouth;

        [SerializeField, Tooltip("Lit surfaces whose emission flares as it fires.")]
        Renderer[] m_Glow = new Renderer[0];

        [SerializeField, ColorUsage(false, true)]
        Color m_GlowColour = Color.white;

        [SerializeField, Range(0f, 1f), Tooltip("How far the nozzle turns toward a throw. Not all the way: the mouth stays put.")]
        float m_Follow = 0.55f;

        static readonly int k_Emission = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock m_Block;
        Quaternion m_Rest = Quaternion.identity;
        Vector3 m_RestPosition;
        Vector3 m_AimWorld = Vector3.up;
        float m_AimWeight;
        float m_Weight;
        float m_FiredAt = -100f;
        float m_Strength;

        public Vector3 MouthIn(Transform space)
        {
            var world = transform.TransformPoint(m_Mouth);
            return space != null ? space.InverseTransformPoint(world) : world;
        }

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            if (m_Nozzle != null)
            {
                m_Rest = m_Nozzle.localRotation;
                m_RestPosition = m_Nozzle.localPosition;
            }

            Glow(0f);
        }

        /// <summary>Turns the nozzle toward <paramref name="worldDirection"/>; <paramref name="weight"/> 0 is at rest, 1 is aimed.</summary>
        public void Aim(Vector3 worldDirection, float weight)
        {
            if (worldDirection.sqrMagnitude > 1e-6f)
                m_AimWorld = worldDirection.normalized;
            m_AimWeight = Mathf.Clamp01(weight);
        }

        public void Fire(float strength)
        {
            m_FiredAt = Time.time;
            m_Strength = Mathf.Clamp(strength, 0.3f, 1f);
        }

        void Update()
        {
            float since = Time.time - m_FiredAt;
            float kick = m_Strength * Mathf.Exp(-since / 0.09f);
            float flare = m_Strength * Mathf.Exp(-since / 0.25f);

            if (m_Nozzle != null)
            {
                m_Weight = Mathf.MoveTowards(m_Weight, m_AimWeight, Time.deltaTime / 0.25f);
                var parent = m_Nozzle.parent;
                var restUp = parent != null ? parent.TransformDirection(m_Rest * Vector3.up) : m_Rest * Vector3.up;
                var turn = Quaternion.FromToRotation(restUp, Vector3.Slerp(restUp, m_AimWorld, m_Follow));
                var aimed = (parent != null ? Quaternion.Inverse(parent.rotation) * turn * parent.rotation : turn) * m_Rest;
                m_Nozzle.localRotation = Quaternion.Slerp(m_Rest, aimed, m_Weight);
                m_Nozzle.localPosition = m_RestPosition - (m_Nozzle.localRotation * Vector3.up) * (0.018f * kick);
            }

            Glow(flare);
        }

        void Glow(float level)
        {
            if (m_Glow == null)
                return;

            var colour = m_GlowColour * (0.06f + level);
            foreach (var renderer in m_Glow)
            {
                if (renderer == null)
                    continue;
                renderer.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_Emission, colour);
                renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}

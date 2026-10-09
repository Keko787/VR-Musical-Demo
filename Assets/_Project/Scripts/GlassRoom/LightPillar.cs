using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// A pillar of light from the sky to the ground — the image the room is drawn from. Its width
    /// breathes with its part of the music and its light with the loudness; a beat falls down it
    /// from the sky (in the shader), and a pool of light at its foot swells with the bass. In the
    /// canyon it is also where the beat rings start: the world's ring origin is its foot.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LightPillar : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Tooltip("Metres across at rest.")]
        float m_Width = 16f;

        [SerializeField] float m_Height = 3400f;

        [SerializeField, ColorUsage(false, true)] Color m_Colour = new(0.8f, 0.95f, 1.25f);

        [SerializeField, Range(-1f, 1f), Tooltip("What it breathes with: below 0 the bass, otherwise the band at that position (0 lowest, 1 highest).")]
        float m_Listen = -1f;

        [SerializeField, Tooltip("How much wider it gets at full level, as a fraction of its width.")]
        float m_Swell = 0.8f;

        [SerializeField] float m_Brightness = 1f;

        [SerializeField, Tooltip("A flat glow lying at the foot. Optional.")]
        Renderer m_FootGlow;

        [SerializeField] float m_FootSize = 140f;
        [SerializeField, ColorUsage(false, true)] Color m_FootColour = new(0.5f, 0.85f, 1f);

        static readonly int k_Width = Shader.PropertyToID("_Width");
        static readonly int k_Height = Shader.PropertyToID("_Height");
        static readonly int k_Color = Shader.PropertyToID("_Color");
        static readonly int k_Brightness = Shader.PropertyToID("_Brightness");

        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;
        MaterialPropertyBlock m_FootBlock;
        float m_Level;

        void Awake()
        {
            m_Renderer = GetComponent<MeshRenderer>();
            m_Block = new MaterialPropertyBlock();
            m_FootBlock = new MaterialPropertyBlock();

            // One quad; the shader stands it up facing the camera. The bounds cover the whole column.
            var mesh = SimMeshes.NewMesh($"{name}_Pillar", 4);
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            float reach = m_Width * 4f;
            mesh.bounds = new Bounds(new Vector3(0f, m_Height * 0.5f, 0f), new Vector3(reach, m_Height, reach));
            SimMeshes.Assign(GetComponent<MeshFilter>(), mesh);
        }

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }

        void Update()
        {
            float target = 0f;
            float loudness = 0f;
            if (m_Simulation != null)
            {
                target = m_Listen < 0f ? m_Simulation.Bass : m_Simulation.Band(m_Listen);
                loudness = m_Simulation.Loudness;
            }

            m_Level = Mathf.Lerp(m_Level, target, 1f - Mathf.Exp(-Time.deltaTime / 0.08f));

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetFloat(k_Width, m_Width * (1f + m_Swell * m_Level));
            m_Block.SetFloat(k_Height, m_Height);
            m_Block.SetColor(k_Color, m_Colour);
            m_Block.SetFloat(k_Brightness, m_Brightness * (0.55f + 0.45f * loudness + 0.35f * m_Level));
            m_Renderer.SetPropertyBlock(m_Block);

            if (m_FootGlow != null)
            {
                float size = m_FootSize * (0.7f + 0.6f * m_Level);
                m_FootGlow.transform.localScale = new Vector3(size, size, 1f);
                float intensity = m_Simulation != null ? m_Simulation.Intensity : 1f;
                m_FootGlow.GetPropertyBlock(m_FootBlock);
                m_FootBlock.SetColor(k_Color, m_FootColour * ((0.35f + 0.65f * m_Level) * intensity));
                m_FootGlow.SetPropertyBlock(m_FootBlock);
            }
        }
    }
}
